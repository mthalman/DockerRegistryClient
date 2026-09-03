using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Docker;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class ManifestOperationsTests
{
    public static TheoryData<string, Type> SupportedManifestTypes => new()
    {
        { ManifestMediaTypes.DockerManifestSchema2, typeof(DockerManifest) },
        { ManifestMediaTypes.DockerManifestList, typeof(ManifestList) },
        { ManifestMediaTypes.OciManifestSchema1, typeof(OciImageManifest) },
        { ManifestMediaTypes.OciImageIndex1, typeof(OciImageIndex) }
    };

    [Theory]
    [MemberData(nameof(SupportedManifestTypes))]
    public async Task GetAsync_SupportedMediaType_DeserializesManifestAndSendsAcceptHeaders(
        string mediaType,
        Type expectedManifestType)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request =>
            {
                MediaTypeWithQualityHeaderValue[] acceptedTypes = request.Headers.Accept.ToArray();
                return request.Method == HttpMethod.Get &&
                    request.RequestUri == new Uri("https://registry.example/v2/repo/manifests/latest") &&
                    acceptedTypes.Select(value => value.MediaType).SequenceEqual(new[]
                    {
                        ManifestMediaTypes.DockerManifestSchema2,
                        ManifestMediaTypes.DockerManifestList,
                        ManifestMediaTypes.OciManifestSchema1,
                        ManifestMediaTypes.OciImageIndex1,
                        "*/*"
                    }) &&
                    acceptedTypes.Take(4).All(value => value.Quality is null) &&
                    acceptedTypes[4].Quality == 0.1;
            },
            ManifestResponse(mediaType));
        using var client = CreateClient(handler);

        var result = await client.Manifests.GetAsync("repo", "latest");

        Assert.Equal(mediaType, result.MediaType);
        Assert.Equal("sha256:manifest", result.DockerContentDigest);
        Assert.IsType(expectedManifestType, result.Manifest);
        Assert.Equal("{}", Encoding.UTF8.GetString(result.Content.Span));
    }

    [Fact]
    public async Task GetAsync_UnknownMediaType_ReturnsRawManifest()
    {
        byte[] content = Encoding.UTF8.GetBytes(
            """{"schemaVersion":2,"mediaType":"application/vnd.example.unknown","value":"raw"}""");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/latest",
            ManifestResponse("application/vnd.example.unknown", content));
        using var client = CreateClient(handler);

        ManifestInfo result = await client.Manifests.GetAsync("repo", "latest");

        Assert.Equal("application/vnd.example.unknown", result.MediaType);
        Assert.Equal("sha256:manifest", result.DockerContentDigest);
        RawManifest manifest = Assert.IsType<RawManifest>(result.Manifest);
        Assert.Equal("application/vnd.example.unknown", manifest.MediaType);
        Assert.Equal(content, manifest.Content.ToArray());
        Assert.Equal(content, result.Content.ToArray());
    }

    [Fact]
    public async Task GetAsync_KnownMediaTypeWithDifferentCasing_DeserializesManifest()
    {
        const string mediaType = "Application/Vnd.Oci.Image.Manifest.V1+Json";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/latest",
            ManifestResponse(mediaType));
        using var client = CreateClient(handler);

        ManifestInfo result = await client.Manifests.GetAsync("repo", "latest");

        Assert.Equal(mediaType, result.MediaType);
        Assert.IsType<OciImageManifest>(result.Manifest);
    }

    [Fact]
    public async Task GetAsync_InvalidJson_IncludesResponseContentInException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "not-json",
                System.Text.Encoding.UTF8,
                ManifestMediaTypes.OciManifestSchema1)
        };
        response.Headers.Add("Docker-Content-Digest", "sha256:manifest");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/latest",
            response);
        using var client = CreateClient(handler);

        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            () => client.Manifests.GetAsync("repo", "latest"));

        Assert.Contains("not-json", exception.Message);
    }

    [Fact]
    public async Task GetAsync_NotFound_UsesManifestSpecificMessage()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/missing",
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    """{"errors":[{"code":"MANIFEST_UNKNOWN","message":"manifest unknown"}]}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Manifests.GetAsync("repo", "missing"));

        Assert.Equal("Manifest not found.", exception.Message);
        Assert.IsType<RegistryException>(exception.InnerException);
    }

    [Fact]
    public async Task GetAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var handler = new MockHttpMessageHandler();
        using var client = CreateClient(handler);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Manifests.GetAsync("repo", "latest", cancellationTokenSource.Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task ExistsAsync_ReturnsResponseSuccessState(HttpStatusCode statusCode, bool expected)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Head,
            "https://registry.example/v2/repo/manifests/sha256:manifest",
            new HttpResponseMessage(statusCode));
        using var client = CreateClient(handler);

        bool result = await client.Manifests.ExistsAsync("repo", "sha256:manifest");

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetDigestAsync_ReturnsDockerContentDigest()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("Docker-Content-Digest", "sha256:manifest");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Head,
            "https://registry.example/v2/repo/manifests/latest",
            response);
        using var client = CreateClient(handler);

        string digest = await client.Manifests.GetDigestAsync("repo", "latest");

        Assert.Equal("sha256:manifest", digest);
    }

    private static RegistryClient CreateClient(HttpMessageHandler handler) =>
        new("registry.example", null, new HttpClient(handler), disposeHttpClient: true);

    private static HttpResponseMessage ManifestResponse(string mediaType, byte[]? content = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content ?? Encoding.UTF8.GetBytes("{}"))
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        response.Headers.Add("Docker-Content-Digest", "sha256:manifest");
        return response;
    }
}
