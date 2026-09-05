using System.Net;
using System.Net.Http.Headers;
using Valleysoft.DockerRegistryClient.Credentials;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class BlobOperationsTests
{
    [Fact]
    public async Task GetAsync_ReturnedStreamOwnsResponseLifetime()
    {
        var body = new MemoryStream([1, 2, 3]);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Get &&
                request.RequestUri == new Uri("https://registry.example/v2/repo/blobs/sha256:abc"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
            });
        using var client = CreateClient(handler);

        Stream result = await client.Blobs.GetAsync("repo", "sha256:abc");

        Assert.True(body.CanRead);
        result.Dispose();
        Assert.False(body.CanRead);
    }

    [Fact]
    public async Task GetAsync_BuffersResponseBeforeReturning()
    {
        var body = new ThrowOnReadStream([1, 2, 3]);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/blobs/sha256:abc",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
            });
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blobs.GetAsync("repo", "sha256:abc"));
    }

    [Fact]
    public async Task GetRangeAsync_BoundedRange_ReturnsPartialContentMetadata()
    {
        var body = new MemoryStream([5, 6, 7]);
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new StreamContent(body)
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(5, 7, 10);
        response.Content.Headers.ContentLength = 3;
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Get &&
                request.RequestUri == new Uri("https://registry.example/v2/repo/blobs/sha256:abc") &&
                request.Headers.Range?.ToString() == "bytes=5-7" &&
                request.Headers.AcceptEncoding.Single().Value == "identity",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", "sha256:abc", 5, 3);

        Assert.True(result.IsRangeHonored);
        Assert.Equal(5, result.RangeStart);
        Assert.Equal(7, result.RangeEnd);
        Assert.Equal(10, result.TotalLength);
        Assert.Equal([5, 6, 7], await ReadAllBytesAsync(result.Content));
        result.Content.Dispose();
        Assert.False(body.CanRead);
    }

    [Fact]
    public async Task GetRangeAsync_OpenEndedRange_SendsRangeWithoutEnd()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent([8, 9])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(8, 9, 10);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Headers.Range?.ToString() == "bytes=8-",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", "sha256:abc", 8);

        Assert.True(result.IsRangeHonored);
        Assert.Equal(8, result.RangeStart);
        Assert.Equal(9, result.RangeEnd);
        Assert.Equal(10, result.TotalLength);
        result.Content.Dispose();
    }

    [Fact]
    public async Task GetRangeAsync_RangeIgnored_ReturnsFullContentMetadata()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3, 4])
        };
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Headers.Range?.ToString() == "bytes=2-3",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", "sha256:abc", 2, 2);

        Assert.False(result.IsRangeHonored);
        Assert.Equal(0, result.RangeStart);
        Assert.Equal(3, result.RangeEnd);
        Assert.Equal(4, result.TotalLength);
        Assert.Equal([1, 2, 3, 4], await ReadAllBytesAsync(result.Content));
        result.Content.Dispose();
    }

    [Theory]
    [InlineData(-1, null, "offset")]
    [InlineData(0L, 0L, "length")]
    [InlineData(0L, -1L, "length")]
    [InlineData(long.MaxValue, 2L, "length")]
    public async Task GetRangeAsync_InvalidRange_Throws(
        long offset,
        long? length,
        string parameterName)
    {
        using var client = CreateClient(new MockHttpMessageHandler());

        ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.Blobs.GetRangeAsync("repo", "sha256:abc", offset, length));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(long.MaxValue, 1L, "bytes=9223372036854775807-9223372036854775807")]
    [InlineData(long.MaxValue - 1, 2L, "bytes=9223372036854775806-9223372036854775807")]
    public async Task GetRangeAsync_MaximumValidEndOffset_SendsRequest(
        long offset,
        long length,
        string expectedRange)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Headers.Range?.ToString() == expectedRange,
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            });
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", "sha256:abc", offset, length);

        result.Content.Dispose();
    }

    [Fact]
    public async Task GetRangeAsync_RangeNotSatisfiable_ThrowsRegistryException()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/blobs/sha256:abc",
            new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                Content = new StringContent("""{"errors":[{"code":"RANGE_INVALID","message":"invalid range"}]}""")
            });
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Blobs.GetRangeAsync("repo", "sha256:abc", 10));

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, exception.StatusCode);
    }

    [Fact]
    public async Task GetRangeAsync_MissingContentRange_ThrowsAndDisposesResponse()
    {
        var body = new MemoryStream([1, 2, 3]);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/blobs/sha256:abc",
            new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new StreamContent(body)
            });
        using var client = CreateClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blobs.GetRangeAsync("repo", "sha256:abc", 0, 3));

        Assert.Contains("Content-Range", exception.Message);
        Assert.False(body.CanRead);
    }

    [Fact]
    public async Task GetRangeAsync_InconsistentContentRange_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent([1, 2])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(1, 2, 10);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/blobs/sha256:abc",
            response);
        using var client = CreateClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blobs.GetRangeAsync("repo", "sha256:abc", 0, 2));

        Assert.Contains("inconsistent", exception.Message);
    }

    [Theory]
    [InlineData(5, 9, 5, 7)]
    [InlineData(5, 9, 6, 7)]
    public async Task GetRangeAsync_ContainedPartialResponse_ReturnsActualRange(
        long requestedStart,
        long requestedEnd,
        long returnedStart,
        long returnedEnd)
    {
        int returnedLength = checked((int)(returnedEnd - returnedStart + 1));
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[returnedLength])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(returnedStart, returnedEnd, 20);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Headers.Range?.ToString() == $"bytes={requestedStart}-{requestedEnd}",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync(
            "repo",
            "sha256:abc",
            requestedStart,
            requestedEnd - requestedStart + 1);

        Assert.True(result.IsRangeHonored);
        Assert.Equal(returnedStart, result.RangeStart);
        Assert.Equal(returnedEnd, result.RangeEnd);
        result.Content.Dispose();
    }

    [Fact]
    public async Task GetRangeAsync_PreCanceledToken_DoesNotSendRequest()
    {
        using var client = CreateClient(new MockHttpMessageHandler());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Blobs.GetRangeAsync("repo", "sha256:abc", 0, cancellationToken: cancellationSource.Token));
    }

    [Fact]
    public async Task GetRangeAsync_DoesNotBufferResponseBody()
    {
        var body = new ThrowOnReadStream([1, 2, 3]);
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new StreamContent(body)
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 2, 3);
        response.Content.Headers.ContentLength = 3;
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/blobs/sha256:abc",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", "sha256:abc", 0, 3);

        result.Content.Dispose();
    }

    [Fact]
    public async Task UploadPrimitives_ReuseAuthorizationAndParseResponseHeaders()
    {
        var uploadId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        var beginResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        beginResponse.Headers.Location = new Uri("/v2/repo/blobs/uploads/upload-id", UriKind.Relative);
        beginResponse.Headers.Add("Docker-Upload-UUID", uploadId.ToString());
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Post &&
                request.RequestUri == new Uri("https://registry.example/v2/repo/blobs/uploads/") &&
                request.Headers.Authorization?.Parameter == "credential-token",
            beginResponse);

        var streamResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        streamResponse.Headers.Location = new Uri("/v2/repo/blobs/uploads/upload-id", UriKind.Relative);
        streamResponse.Headers.Add("Docker-Upload-UUID", uploadId.ToString());
        streamResponse.Headers.Add("Range", "0-2");
        handler.AddExpectedRequest(
            request =>
            {
                byte[] content = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return request.Method.Method == "PATCH" &&
                    request.RequestUri == new Uri("https://registry.example/v2/repo/blobs/uploads/upload-id") &&
                    request.Headers.Authorization?.Parameter == "credential-token" &&
                    request.Content.Headers.ContentType?.MediaType == "application/octet-stream" &&
                    content.SequenceEqual(new byte[] { 1, 2, 3 });
            },
            streamResponse);

        var endResponse = new HttpResponseMessage(HttpStatusCode.Created);
        endResponse.Headers.Location = new Uri("/v2/repo/blobs/sha256:abc", UriKind.Relative);
        endResponse.Headers.Add("Docker-Content-Digest", "sha256:abc");
        handler.AddExpectedRequest(
            request =>
            {
                byte[] content = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return request.Method == HttpMethod.Put &&
                    request.RequestUri == new Uri("https://registry.example/v2/repo/blobs/uploads/upload-id?digest=sha256:abc") &&
                    request.Headers.Authorization?.Parameter == "credential-token" &&
                    request.Content.Headers.ContentType?.MediaType == "application/octet-stream" &&
                    content.SequenceEqual(new byte[] { 4 });
            },
            endResponse);

        using var client = new RegistryClient(
            "registry.example",
            new TokenCredentials("credential-token"),
            new HttpClient(handler),
            disposeHttpClient: true);

        BlobUploadInitializationResult initialization = await client.Blobs.BeginUploadAsync("repo");
        BlobUploadStreamResult streamResult = await client.Blobs.SendUploadStreamAsync(
            initialization.Location,
            new MemoryStream([1, 2, 3]),
            initialization.UploadContext);
        BlobUploadResult result = await client.Blobs.EndUploadAsync(
            streamResult.Location,
            "sha256:abc",
            initialization.UploadContext,
            new MemoryStream([4]));

        Assert.Equal(uploadId, initialization.UploadId);
        Assert.Equal(2, streamResult.RangeOffset);
        Assert.Equal("/v2/repo/blobs/sha256:abc", result.Location);
        Assert.Equal("sha256:abc", result.Digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task GetUploadAsync_InvalidRangeHeader_ThrowsDescriptiveException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        response.Headers.Add("Docker-Upload-UUID", Guid.NewGuid().ToString());
        response.Headers.Add("Range", "invalid");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/blobs/uploads/upload-id",
            response);
        using var client = CreateClient(handler);

        Exception exception = await Assert.ThrowsAsync<Exception>(
            () => client.Blobs.GetUploadAsync("/v2/repo/blobs/uploads/upload-id"));

        Assert.Contains("Expected '0-<offset>'", exception.Message);
    }

    private static RegistryClient CreateClient(HttpMessageHandler handler) =>
        new("registry.example", null, new HttpClient(handler), disposeHttpClient: true);

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var destination = new MemoryStream();
        await stream.CopyToAsync(destination);
        return destination.ToArray();
    }

    private sealed class ThrowOnReadStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("The response body was buffered.");

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The response body was buffered.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The response body was buffered.");
    }
}
