using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class StreamedCopyTransportTests
{
    [Theory]
    [InlineData("complete")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("read-error")]
    [InlineData("caller-cancel")]
    [InlineData("transport-cancel")]
    [InlineData("server-abandon")]
    [InlineData("status-error")]
    [InlineData("http1-control")]
    [InlineData("http2-normal")]
    public async Task CopyAsync_RealTransport_WaitsForSerialization(string outcome)
    {
        const int size = 1024 * 1024;
        bool http2 = outcome != "http1-control";
        bool normalResponse = outcome == "http2-normal";
        bool successful = outcome is "complete" or "http1-control" or "http2-normal";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using var sourceStream = new GatedBlobStream(size, outcome);
        var responseSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int manifestUploads = 0;
        int uploadDeletes = 0;
        byte[] manifest = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{ManifestMediaTypes.OciManifestSchema1}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"custom:blob","size":{{size}}},"layers":[]}
            """);
        string manifestDigest = $"sha256:{Convert.ToHexString(SHA256.HashData(manifest)).ToLowerInvariant()}";
        var sourceHandler = new MockHttpMessageHandler();
        var manifestResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(manifest)
        };
        manifestResponse.Content.Headers.ContentType = new MediaTypeHeaderValue(ManifestMediaTypes.OciManifestSchema1);
        manifestResponse.Headers.Add("Docker-Content-Digest", manifestDigest);
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get, "https://source.example/v2/source/manifests/latest", manifestResponse);
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get, "https://source.example/v2/source/blobs/custom:blob",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(sourceStream) });
        using var sourceClient = new RegistryClient("source.example", null, sourceHandler);

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0,
                listen => listen.Protocols = http2 ? HttpProtocols.Http2 : HttpProtocols.Http1);
            // Flow control must not mask whether SendAsync waits for the gated source.
            options.Limits.Http2.InitialStreamWindowSize = 2 * size;
            options.Limits.Http2.InitialConnectionWindowSize = 2 * size;
        });
        await using WebApplication server = builder.Build();
        server.Run(async context =>
        {
            Assert.Equal(http2 ? "HTTP/2" : "HTTP/1.1", context.Request.Protocol);
            if (context.Request.Method == "HEAD")
            {
                context.Response.StatusCode = 404;
            }
            else if (context.Request.Method == "POST")
            {
                context.Response.StatusCode = 202;
                context.Response.Headers.Location = "/v2/destination/blobs/uploads/session";
                context.Response.Headers["Docker-Upload-UUID"] = "session";
            }
            else if (context.Request.Method == "DELETE")
            {
                Interlocked.Increment(ref uploadDeletes);
                context.Response.StatusCode = 204;
            }
            else if (context.Request.Path.StartsWithSegments("/v2/destination/manifests"))
            {
                using var body = new MemoryStream();
                await context.Request.Body.CopyToAsync(body, timeout.Token);
                Assert.Equal(manifest, body.ToArray());
                Interlocked.Increment(ref manifestUploads);
                context.Response.StatusCode = 201;
                context.Response.Headers.Location = "/v2/destination/manifests/latest";
                context.Response.Headers["Docker-Content-Digest"] = manifestDigest;
            }
            else
            {
                Assert.Equal("PUT", context.Request.Method);
                Assert.Equal(size, context.Request.ContentLength);
                byte[] prefix = new byte[1];
                await context.Request.Body.ReadExactlyAsync(prefix, timeout.Token);
                await sourceStream.ReadBlocked.WaitAsync(timeout.Token);
                if (normalResponse)
                {
                    await context.Request.Body.CopyToAsync(Stream.Null, timeout.Token);
                }

                context.Response.StatusCode = outcome == "status-error" ? 400 : 201;
                context.Response.ContentLength = 0;
                context.Response.Headers.Location = "/v2/destination/blobs/custom:blob";
                context.Response.Headers["Docker-Content-Digest"] = "custom:blob";
                await context.Response.CompleteAsync();
                responseSent.TrySetResult();
                if (!http2)
                {
                    await context.Request.Body.CopyToAsync(Stream.Null, timeout.Token);
                }
                else if (outcome != "server-abandon")
                {
                    // Returning now would send RST_STREAM(NO_ERROR) and cancel the source read.
                    await finishHandler.Task.WaitAsync(timeout.Token);
                }
            }
        });
        await server.StartAsync(timeout.Token);
        using var httpClient = new HttpClient(new VersionHandler(http2, transportCancellation.Token))
        {
            DefaultRequestVersion = HttpVersion.Version20
        };
        using var destinationClient = new RegistryClient(server.Urls.Single(), null, httpClient);
        Task<ManifestPublishResult> copy = sourceClient.CopyAsync(
            "source", "latest", destinationClient, "destination", "latest", callerCancellation.Token);
        try
        {
            await sourceStream.ReadBlocked.WaitAsync(timeout.Token);
            if (!normalResponse)
            {
                await responseSent.Task.WaitAsync(timeout.Token);
                if (outcome != "server-abandon")
                {
                    // Give an incorrectly duplex send a chance to finish while the source stays gated.
                    Task winner = await Task.WhenAny(copy, Task.Delay(TimeSpan.FromMilliseconds(300), timeout.Token));
                    Assert.NotSame(copy, winner);
                    Assert.False(sourceStream.Disposed);
                }
            }

            Assert.Equal(0, Volatile.Read(ref manifestUploads));

            if (outcome == "caller-cancel")
            {
                callerCancellation.Cancel();
            }
            else if (outcome == "transport-cancel")
            {
                transportCancellation.Cancel();
            }
            else if (outcome != "server-abandon")
            {
                sourceStream.Release();
            }

            if (successful)
            {
                ManifestPublishResult result = await copy.WaitAsync(timeout.Token);
                Assert.Equal(manifestDigest, result.Digest);
                Assert.Equal(1, manifestUploads);
                Assert.Equal(0, uploadDeletes);
            }
            else if (outcome.EndsWith("-cancel", StringComparison.Ordinal))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => copy.WaitAsync(timeout.Token));
            }
            else if (outcome == "server-abandon")
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => copy.WaitAsync(TimeSpan.FromSeconds(5), timeout.Token));
            }
            else if (outcome == "status-error")
            {
                RegistryException error = await Assert.ThrowsAsync<RegistryException>(() => copy.WaitAsync(timeout.Token));
                Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
            }
            else
            {
                Exception error = await Assert.ThrowsAnyAsync<Exception>(() => copy.WaitAsync(timeout.Token));
                string message = error.ToString();
                Assert.Contains(outcome switch
                {
                    "short" => "has size 81920",
                    "long" => "contains more data",
                    _ => "source read failed"
                }, message);
            }

            Assert.True(sourceStream.Disposed);
            Assert.Equal(0, sourceHandler.RemainingRequestCount);
            if (!successful)
            {
                Assert.Equal(0, manifestUploads);
                if (outcome != "transport-cancel")
                {
                    Assert.Equal(1, uploadDeletes);
                }
            }
        }
        finally
        {
            callerCancellation.Cancel();
            sourceStream.Release();
            finishHandler.TrySetResult();
            await server.StopAsync(timeout.Token);
        }
    }

    private sealed class VersionHandler(bool http2, CancellationToken transportCancellation) : DelegatingHandler(
        new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (http2)
            {
                request.Version = HttpVersion.Version20;
                request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, transportCancellation);
            return await base.SendAsync(request, linked.Token);
        }
    }

    private sealed class GatedBlobStream(int size, string outcome) : Stream
    {
        private readonly TaskCompletionSource readBlocked =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int position;

        public Task ReadBlocked => readBlocked.Task;
        public bool Disposed { get; private set; }
        public void Release() => release.TrySetResult();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (position != 0)
            {
                readBlocked.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
                if (outcome == "read-error")
                {
                    throw new IOException("source read failed");
                }
            }

            int length = outcome == "short" ? 81920 : size + (outcome == "long" ? 1 : 0);
            int read = Math.Min(Math.Min(count, 81920), length - position);
            Array.Clear(buffer, offset, read);
            position += read;
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
