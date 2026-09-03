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
        Assert.Equal(
            "https://registry.example/v2/_catalog?n=2&last=repo2",
            page.NextPageLink);
    }

    [Fact]
    public async Task CatalogGetNextAsync_AbsoluteSameOriginLink_RequestsLink()
    {
        const string NextPageLink = "https://registry.example/v2/_catalog?n=2&last=repo2";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            NextPageLink,
            JsonResponse(new Catalog { RepositoryNames = ["repo3"] }));
        using var client = CreateClient(handler);

        Page<Catalog> page = await client.Catalog.GetNextAsync(NextPageLink);

        Assert.Equal(["repo3"], page.Value.RepositoryNames);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task CatalogGetAllAsync_QueryRelativeLink_ResolvesAgainstCurrentPage()
    {
        var handler = new MockHttpMessageHandler();
        HttpResponseMessage firstResponse =
            JsonResponse(new Catalog { RepositoryNames = ["repo1"] });
        firstResponse.Headers.Add("Link", "<?n=1&last=repo1>; rel=\"next\"");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/_catalog?n=1",
            firstResponse);
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/_catalog?n=1&last=repo1",
            JsonResponse(new Catalog { RepositoryNames = ["repo2"] }));
        using var client = CreateClient(handler);

        List<string> repositories = [];
        await foreach (string repository in client.Catalog.GetAllAsync(count: 1))
        {
            repositories.Add(repository);
        }

        Assert.Equal(["repo1", "repo2"], repositories);
        Assert.Equal(0, handler.RemainingRequestCount);
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
    public async Task TagsGetNextAsync_AbsoluteSameOriginLink_RequestsLink()
    {
        const string NextPageLink =
            "https://registry.example/v2/repo/tags/list?n=2&last=v2";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            NextPageLink,
            JsonResponse(new RepositoryTags { RepositoryName = "repo", Tags = ["v3"] }));
        using var client = CreateClient(handler);

        Page<RepositoryTags> page = await client.Tags.GetNextAsync(NextPageLink);

        Assert.Equal(["v3"], page.Value.Tags);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task TagsGetAllAsync_QueryRelativeLink_ResolvesAgainstCurrentPage()
    {
        var handler = new MockHttpMessageHandler();
        HttpResponseMessage firstResponse = JsonResponse(
            new RepositoryTags { RepositoryName = "repo", Tags = ["v1"] });
        firstResponse.Headers.Add("Link", "<?n=1&last=v1>; rel=\"next\"");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/tags/list?n=1",
            firstResponse);
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/tags/list?n=1&last=v1",
            JsonResponse(new RepositoryTags { RepositoryName = "repo", Tags = ["v2"] }));
        using var client = CreateClient(handler);

        List<string> tags = [];
        await foreach (string tag in client.Tags.GetAllAsync("repo", count: 1))
        {
            tags.Add(tag);
        }

        Assert.Equal(["v1", "v2"], tags);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(true, "https://attacker.example/v2/_catalog")]
    [InlineData(true, "http://registry.example:443/v2/_catalog")]
    [InlineData(true, "https://registry.example:444/v2/_catalog")]
    [InlineData(false, "https://attacker.example/v2/repo/tags/list")]
    [InlineData(false, "http://registry.example:443/v2/repo/tags/list")]
    [InlineData(false, "https://registry.example:444/v2/repo/tags/list")]
    public async Task GetNextAsync_CrossOriginLink_ThrowsWithoutSendingRequest(
        bool useCatalog,
        string nextPageLink)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            nextPageLink,
            useCatalog
                ? JsonResponse(new Catalog())
                : JsonResponse(new RepositoryTags()));
        using var client = CreateClient(handler);

        Task request = useCatalog
            ? client.Catalog.GetNextAsync(nextPageLink)
            : client.Tags.GetNextAsync(nextPageLink);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => request);

        Assert.StartsWith($"Location '{nextPageLink}' resolves outside", exception.Message);
        Assert.Equal(1, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAsync_CrossOriginRedirect_Throws(bool useCatalog)
    {
        string initialRequestUri = useCatalog
            ? "https://registry.example/v2/_catalog"
            : "https://registry.example/v2/repo/tags/list";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            initialRequestUri,
            new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
            {
                Headers =
                {
                    Location = new Uri("https://attacker.example/continuation")
                }
            });
        using var client = new RegistryClient("registry.example", null, handler);

        Task request = useCatalog
            ? client.Catalog.GetAsync()
            : client.Tags.GetAsync("repo");

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => request);

        Assert.Contains("outside the configured registry origin", exception.Message);
        Assert.Equal(0, handler.RemainingRequestCount);
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
