using System.Net;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class ListOperationsTests
{
    [Theory]
    [InlineData("catalog")]
    [InlineData("tags")]
    [InlineData("referrers")]
    public async Task Pagination_RelativeLinkAfterCrossOriginRedirect_UsesResponseOrigin(string operation)
    {
        string digest = RegistryFixture.GetDigest([1, 2, 3]);
        string initialUri = "https://registry.example" + (operation switch
        {
            "catalog" => "/v2/_catalog",
            "tags" => "/v2/repo/tags/list",
            _ => $"/v2/repo/referrers/{digest}"
        });
        const string RedirectUri = "https://pages.example/pages/sub/A?state=~";
        const string NextUri = "https://pages.example/pages/%252e%252e/final?state=~";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.RequestUri!.AbsoluteUri == initialUri &&
                request.Headers.Authorization?.Parameter == "registry-secret",
            new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
            {
                Headers = { Location = new Uri("https://pages.example/pages/sub/%41?state=%7E") }
            });
        var response = JsonResponse(new
        {
            repositories = Array.Empty<string>(),
            name = "repo",
            tags = Array.Empty<string>(),
            schemaVersion = 2,
            manifests = Array.Empty<object>()
        });
        response.Headers.Add("Link", "<../%252e%252e/final?state=%7E>; rel=\"next\"");
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(RedirectUri, request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return true;
        }, response);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(NextUri, request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return true;
        }, JsonResponse(new { repositories = Array.Empty<string>(), tags = Array.Empty<string>(), manifests = Array.Empty<object>() }));
        using var client = new RegistryClient(
            "registry.example", new Credentials.TokenCredentials("registry-secret"), handler);

        string? nextLink = operation switch
        {
            "catalog" => (await client.Catalog.GetAsync()).NextPageLink,
            "tags" => (await client.Tags.GetAsync("repo")).NextPageLink,
            _ => (await client.Referrers.GetAsync("repo", digest)).NextPageLink
        };
        Assert.Equal(NextUri, nextLink);
        switch (operation)
        {
            case "catalog":
                await client.Catalog.GetNextAsync(nextLink!);
                break;
            case "tags":
                await client.Tags.GetNextAsync(nextLink!);
                break;
            default:
                await client.Referrers.GetNextAsync(nextLink!);
                break;
        }

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task CatalogGetNextAsync_RelativeLinkKeepsCredentialsOnRegistryOrigin()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request =>
            {
                Assert.Equal("https://registry.example/attacker.example/next", request.RequestUri!.AbsoluteUri);
                Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
                return true;
            },
            JsonResponse(new Catalog { RepositoryNames = ["repo"] }));
        using var client = new RegistryClient(
            "registry.example",
            new Credentials.TokenCredentials("test-token"),
            handler);

        await client.Catalog.GetNextAsync("attacker.example/next");

        Assert.Equal(0, handler.RemainingRequestCount);
    }

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
    public async Task GetNextAsync_CrossOriginLink_DoesNotSendRegistryCredentials(
        bool useCatalog,
        string nextPageLink)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request =>
            {
                Assert.Equal(nextPageLink, request.RequestUri!.AbsoluteUri);
                Assert.Null(request.Headers.Authorization);
                return true;
            },
            useCatalog
                ? JsonResponse(new Catalog())
                : JsonResponse(new RepositoryTags()));
        using var client = new RegistryClient(
            "registry.example", new Credentials.TokenCredentials("registry-secret"), handler);

        Task request = useCatalog
            ? client.Catalog.GetNextAsync(nextPageLink)
            : client.Tags.GetNextAsync(nextPageLink);

        await request;

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAsync_CrossOriginRedirect_DoesNotSendRegistryCredentials(bool useCatalog)
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
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("https://attacker.example/continuation", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return true;
        }, useCatalog ? JsonResponse(new Catalog()) : JsonResponse(new RepositoryTags()));
        using var client = new RegistryClient(
            "registry.example", new Credentials.TokenCredentials("registry-secret"), handler);

        Task request = useCatalog
            ? client.Catalog.GetAsync()
            : client.Tags.GetAsync("repo");

        await request;
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task ReferrersGetAsync_IncludesArtifactTypeAndDeserializesIndex()
    {
        const string ArtifactType = "application/spdx+json";
        string digest = RegistryFixture.GetDigest([1, 2, 3]);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/referrers/{digest}?artifactType=application%2Fspdx%2Bjson",
            JsonResponse(new OciImageIndex
            {
                Manifests = [new ManifestReference { ArtifactType = ArtifactType }]
            }));
        using var client = CreateClient(handler);

        Page<OciImageIndex> page = await client.Referrers.GetAsync("repo", digest, ArtifactType);

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
