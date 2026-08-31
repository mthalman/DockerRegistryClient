using System.Net;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class ListOperationsTests
{
    [Fact]
    public async Task CatalogGetAsync_AppliesCountAndReturnsNextPageLink()
    {
        var handler = new MockHttpMessageHandler();
        var response = JsonResponse(new Catalog { RepositoryNames = ["repo1", "repo2"] });
        response.Headers.Add("Link", "</v2/_catalog?n=2&last=repo2>; rel=\"next\"");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/_catalog?n=2",
            response);
        using var client = CreateClient(handler);

        Page<Catalog> page = await client.Catalog.GetAsync(2);

        Assert.Equal(["repo1", "repo2"], page.Value.RepositoryNames);
        Assert.Equal("/v2/_catalog?n=2&last=repo2", page.NextPageLink);
    }

    [Fact]
    public async Task TagsGetNextAsync_NotFound_UsesRepositorySpecificMessage()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/tags/list?n=2&last=v2",
            ErrorResponse(HttpStatusCode.NotFound));
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Tags.GetNextAsync("/v2/repo/tags/list?n=2&last=v2"));

        Assert.Equal("Repository not found.", exception.Message);
        var innerException = Assert.IsType<RegistryException>(exception.InnerException);
        Assert.Equal(HttpStatusCode.NotFound, innerException.StatusCode);
    }

    [Fact]
    public async Task ReferrersGetAsync_IncludesArtifactTypeAndDeserializesIndex()
    {
        const string ArtifactType = "application/spdx+json";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/referrers/sha256:abc?artifactType=application%2Fspdx%2Bjson",
            JsonResponse(new OciImageIndex
            {
                Manifests = [new ManifestReference { ArtifactType = ArtifactType }]
            }));
        using var client = CreateClient(handler);

        Page<OciImageIndex> page = await client.Referrers.GetAsync("repo", "sha256:abc", ArtifactType);

        Assert.Single(page.Value.Manifests);
        Assert.Equal(ArtifactType, page.Value.Manifests[0].ArtifactType);
        Assert.Null(page.NextPageLink);
    }

    [Fact]
    public async Task ReferrersGetNextAsync_NotFound_UsesManifestSpecificMessage()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/referrers/sha256:abc",
            ErrorResponse(HttpStatusCode.NotFound));
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Referrers.GetNextAsync("/v2/repo/referrers/sha256:abc"));

        Assert.Equal("Manifest not found.", exception.Message);
        Assert.IsType<RegistryException>(exception.InnerException);
    }

    private static RegistryClient CreateClient(HttpMessageHandler handler) =>
        new("registry.example", null, new HttpClient(handler), disposeHttpClient: true);

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(value),
                System.Text.Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage ErrorResponse(HttpStatusCode statusCode) =>
        new(statusCode)
        {
            Content = new StringContent(
                """{"errors":[{"code":"NAME_UNKNOWN","message":"repository not found"}]}""",
                System.Text.Encoding.UTF8,
                "application/json")
        };
}
