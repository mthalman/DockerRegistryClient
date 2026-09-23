using System.Net;
using System.Net.Http.Headers;
using Valleysoft.DockerRegistryClient.Credentials;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class BlobUploadSecurityTests
{
    private const string Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData("https://registry.example", "/upload", true)]
    [InlineData("https://registry.example", "https://registry.example/upload", true)]
    [InlineData("https://registry.example", "https://registry.example:443/upload", true)]
    [InlineData("https://registry.example", "https://storage.example/upload", false)]
    [InlineData("https://registry.example", "//storage.example/upload", false)]
    [InlineData("https://registry.example", "https://registry.example:444/upload", false)]
    [InlineData("http://registry.example", "/upload", true)]
    [InlineData("http://registry.example", "https://storage.example/upload", false)]
    [InlineData("http://registry.example", "http://storage.example/upload", false)]
    [InlineData("https://registry.example", "http://storage.example/upload", false)]
    [InlineData("http://registry.example", "//storage.example/upload", false)]
    public async Task UploadLocations_ScopeCredentialsWithoutRestrictingHttpDestinations(
        string registry, string location, bool sameOrigin)
    {
        var handler = new MockHttpMessageHandler();
        using var client = new RegistryClient(registry, new TokenCredentials("registry-token"), handler);
        var context = new BlobUploadContext(new AuthenticationHeaderValue("Bearer", "registry-token"), client.BaseUri);
        Uri uploadUri = new(client.BaseUri, location);

        foreach (string operation in new[] { "status", "cancel", "chunk", "complete" })
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Location = uploadUri;
            response.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
            response.Headers.Add("Range", "0-2");
            response.Headers.Add("Docker-Content-Digest", Digest);
            handler.AddExpectedRequest(request =>
            {
                Assert.Equal(operation == "complete"
                    ? new Uri($"{uploadUri}?digest={Uri.EscapeDataString(Digest)}") : uploadUri, request.RequestUri);
                Assert.Equal(sameOrigin ? "registry-token" : null, request.Headers.Authorization?.Parameter);
                if (request.Content is not null)
                {
                    Assert.Equal(new byte[] { 1, 2, 3 }, request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                }
                return true;
            }, response);

            await InvokeUploadAsync(client, operation, location, context);
        }

        Assert.Equal(4, handler.RequestCount);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task UploadRedirects_HttpToHttp_FollowWithoutAuthorization()
    {
        var handler = new MockHttpMessageHandler();
        var redirect = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
        redirect.Headers.Location = new Uri("http://storage.example/upload");
        handler.AddExpectedRequest(request => request.RequestUri!.Host == "registry.example", redirect);
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Location = new Uri("http://storage.example/upload");
        response.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        response.Headers.Add("Range", "0-2");
        response.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("storage.example", request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization);
            return true;
        }, response);
        using var client = new RegistryClient("http://registry.example", new TokenCredentials("registry-token"), handler);

        using var stream = new MemoryStream([1, 2, 3]);
        await client.Blobs.EndUploadAsync("/upload", Digest, new BlobUploadContext(null, client.BaseUri), stream);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task UploadAsync_AcceptsExternalHttpSessionLocation()
    {
        var handler = new MockHttpMessageHandler();
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Location = new Uri("http://storage.example/upload");
        response.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        response.Headers.Add("Range", "0-0");
        handler.AddExpectedRequest(request => request.RequestUri!.Host == "registry.example", response);
        var completed = new HttpResponseMessage(HttpStatusCode.Created);
        completed.Headers.Location = new Uri("http://storage.example/blob");
        completed.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("storage.example", request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization);
            return true;
        }, completed);
        using var client = new RegistryClient("http://registry.example", null, handler);

        using var stream = new MemoryStream([1, 2, 3]);
        await client.Blobs.UploadAsync("repo", stream, Digest);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(null, "https://storage.example/upload", null)]
    [InlineData("storage-token", "https://storage.example/upload", "storage-token")]
    [InlineData("storage-token", "https://storage.example:443/upload", "storage-token")]
    [InlineData("storage-token", "https://storage.example:444/upload", null)]
    [InlineData("storage-token", "https://other-storage.example/upload", null)]
    [InlineData("storage-token", "https://registry.example/upload", "registry-token")]
    public async Task UploadContext_UsesEffectiveRequestCredentialsOnlyAtTheirOrigin(
        string? storageToken, string uploadLocation, string? expectedToken)
    {
        var handler = new MockHttpMessageHandler();
        using var effectiveRequest = new HttpRequestMessage(HttpMethod.Post, "https://storage.example/start");
        if (storageToken is not null)
        {
            effectiveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", storageToken);
        }

        var beginResponse = new HttpResponseMessage(HttpStatusCode.Accepted) { RequestMessage = effectiveRequest };
        beginResponse.Headers.Location = new Uri(uploadLocation);
        beginResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Post && request.Headers.Authorization?.Parameter == "registry-token",
            beginResponse);
        var chunkResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        chunkResponse.Headers.Location = new Uri(uploadLocation);
        chunkResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        chunkResponse.Headers.Add("Range", "0-2");
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(HttpMethod.Patch, request.Method);
            Assert.Equal(new Uri(uploadLocation), request.RequestUri);
            Assert.Equal(expectedToken, request.Headers.Authorization?.Parameter);
            return true;
        }, chunkResponse);
        var endResponse = new HttpResponseMessage(HttpStatusCode.Created);
        endResponse.Headers.Location = new Uri("https://storage.example/completed");
        endResponse.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal(expectedToken, request.Headers.Authorization?.Parameter);
            return true;
        }, endResponse);
        using var client = new RegistryClient(
            "registry.example", new TokenCredentials("registry-token"), new HttpClient(handler), disposeHttpClient: true);

        BlobUploadInitializationResult initialization = await client.Blobs.BeginUploadAsync("repo");
        using var stream = new MemoryStream([1, 2, 3]);
        BlobUploadStreamResult chunk = await client.Blobs.SendUploadStreamAsync(
            initialization.Location, stream, initialization.UploadContext);
        await client.Blobs.EndUploadAsync(chunk.Location, Digest, initialization.UploadContext);

        Assert.Equal(3, handler.RequestCount);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("chunk")]
    [InlineData("complete")]
    public async Task UploadContext_FromAnotherRegistryDoesNotOverrideDestinationCredentials(string operation)
    {
        var handler = new MockHttpMessageHandler();
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Location = new Uri("/upload", UriKind.Relative);
        response.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        response.Headers.Add("Range", "0-2");
        response.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("destination-token", request.Headers.Authorization?.Parameter);
            return true;
        }, response);
        using var client = new RegistryClient("registry.example", new TokenCredentials("destination-token"), handler);
        var context = new BlobUploadContext(
            new AuthenticationHeaderValue("Bearer", "source-token"), new Uri("https://source.example"));

        await InvokeUploadAsync(client, operation, "/upload", context);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("http://storage.example/completed")]
    [InlineData("https://storage.example/completed")]
    public async Task UploadCompletion_ReturnsExternalBlobMetadataWithoutFollowingIt(string location)
    {
        var handler = new MockHttpMessageHandler();
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri(location);
        response.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(HttpMethod.Put,
            $"http://registry.example/upload?digest={Uri.EscapeDataString(Digest)}", response);
        using var client = new RegistryClient("http://registry.example", null, handler);

        BlobUploadResult result = await client.Blobs.EndUploadAsync(
            "/upload", Digest, new BlobUploadContext(null, client.BaseUri));

        Assert.Equal(location, result.Location);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    private static async Task InvokeUploadAsync(
        RegistryClient client, string operation, string location, BlobUploadContext context)
    {
        using var stream = new MemoryStream([1, 2, 3]);
        await (operation switch
        {
            "status" => client.Blobs.GetUploadAsync(location),
            "cancel" => client.Blobs.DeleteUploadAsync(location),
            "chunk" => client.Blobs.SendUploadStreamAsync(location, stream, context),
            "complete" => client.Blobs.EndUploadAsync(location, Digest, context, stream),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        });
    }
}
