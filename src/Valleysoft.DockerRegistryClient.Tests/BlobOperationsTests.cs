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
            HttpMethod.Get,
            "https://registry.example/v2/repo/blobs/sha256:abc",
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
}
