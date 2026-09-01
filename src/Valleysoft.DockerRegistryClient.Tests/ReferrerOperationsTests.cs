using System.Net;
using System.Text;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public sealed class ReferrerOperationsTests
{
    [Fact]
    public async Task GetAsync_WithoutFilter_ReturnsEmptyTerminalPage()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            CreateJsonResponse("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[]}"""));
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> page = await client.Referrers.GetAsync("repository", "sha256:subject");

        Assert.Equal(2, page.Value.SchemaVersion);
        Assert.Equal(ManifestMediaTypes.OciImageIndex1, page.Value.MediaType);
        Assert.Empty(page.Value.Manifests);
        Assert.Null(page.NextPageLink);
    }

    [Fact]
    public async Task GetAsync_NullCollections_ReturnsEmptyCollections()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            CreateJsonResponse("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":null,"annotations":null}"""));
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> page = await client.Referrers.GetAsync("repository", "sha256:subject");

        Assert.Empty(page.Value.Manifests);
        Assert.Empty(page.Value.Annotations);
    }

    [Fact]
    public async Task GetAsync_WithFilter_AndGetNextAsync_ReturnDescriptorPages()
    {
        const string ArtifactType = "application/spdx+json";
        var handler = new MockHttpMessageHandler();
        HttpResponseMessage firstResponse = CreateJsonResponse(
            """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:first","size":101,"artifactType":"application/spdx+json","annotations":{"name":"first"}}],"annotations":{"page":"one"}}""");
        firstResponse.Headers.Add(
            "Link",
            "</v2/repository/referrers/sha256:subject?artifactType=application%2Fspdx%2Bjson&last=sha256:first>; rel=\"next\"");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject?artifactType=application%2Fspdx%2Bjson",
            firstResponse);
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject?artifactType=application%2Fspdx%2Bjson&last=sha256:first",
            CreateJsonResponse(
                """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:second","size":202,"artifactType":"application/spdx+json","annotations":{"name":"second"}}]}"""));
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> firstPage = await client.Referrers.GetAsync(
            "repository",
            "sha256:subject",
            ArtifactType);
        ManifestReference first = Assert.Single(firstPage.Value.Manifests);
        Assert.Equal("sha256:first", first.Digest);
        Assert.Equal(101, first.Size);
        Assert.Equal(ArtifactType, first.ArtifactType);
        Assert.Equal("first", first.Annotations["name"]);
        Assert.Equal("one", firstPage.Value.Annotations["page"]);
        Assert.NotNull(firstPage.NextPageLink);

        Page<OciImageIndex> secondPage = await client.Referrers.GetNextAsync(firstPage.NextPageLink);
        ManifestReference second = Assert.Single(secondPage.Value.Manifests);
        Assert.Equal("sha256:second", second.Digest);
        Assert.Equal(202, second.Size);
        Assert.Equal("second", second.Annotations["name"]);
        Assert.Null(secondPage.NextPageLink);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task GetAsync_FilterNotAppliedByRegistry_FiltersNativePages()
    {
        const string ArtifactType = "application/spdx+json";
        var handler = new MockHttpMessageHandler();
        HttpResponseMessage firstResponse = CreateJsonResponse(
            """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:first","size":101,"artifactType":"application/spdx+json"},{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:excluded-first","size":102,"artifactType":"application/example"}]}""");
        firstResponse.Headers.Add(
            "Link",
            "</v2/repository/referrers/sha256:subject?last=sha256:first>; rel=\"next\"");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject?artifactType=application%2Fspdx%2Bjson",
            firstResponse);
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject?last=sha256:first&artifactType=application%2Fspdx%2Bjson",
            CreateJsonResponse(
                """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:second","size":201,"artifactType":"application/spdx+json"},{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:excluded-second","size":202,"artifactType":"application/example"}]}"""));
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> firstPage = await client.Referrers.GetAsync(
            "repository",
            "sha256:subject",
            ArtifactType);
        Assert.Equal("sha256:first", Assert.Single(firstPage.Value.Manifests).Digest);

        Page<OciImageIndex> secondPage = await client.Referrers.GetNextAsync(
            Assert.IsType<string>(firstPage.NextPageLink));
        Assert.Equal("sha256:second", Assert.Single(secondPage.Value.Manifests).Digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task GetAsync_FilterAppliedByRegistry_PreservesNativePage()
    {
        var handler = new MockHttpMessageHandler();
        HttpResponseMessage response = CreateJsonResponse(
            """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:native","size":101,"artifactType":"application/example"}]}""");
        response.Headers.Add("OCI-Filters-Applied", "artifactType");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject?artifactType=application%2Fspdx%2Bjson",
            response);
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> page = await client.Referrers.GetAsync(
            "repository",
            "sha256:subject",
            "application/spdx+json");

        Assert.Equal("sha256:native", Assert.Single(page.Value.Manifests).Digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(
        "https://registry.example/v2/repository/referrers/sha256:subject?last=sha256:first",
        "https://registry.example/v2/repository/referrers/sha256:subject?last=sha256:first")]
    [InlineData(
        "?last=sha256:first",
        "https://registry.example/v2/repository/referrers/sha256:subject?last=sha256:first")]
    public async Task GetNextAsync_ResolvesLinkAgainstOriginatingRequest(
        string nextPageLink,
        string expectedRequestUri)
    {
        var handler = new MockHttpMessageHandler();
        HttpResponseMessage firstResponse = CreateJsonResponse(
            """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[]}""");
        firstResponse.Headers.Add("Link", $"<{nextPageLink}>; rel=\"next\"");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            firstResponse);
        handler.AddExpectedRequest(
            HttpMethod.Get,
            expectedRequestUri,
            CreateJsonResponse(
                """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[]}"""));
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> firstPage = await client.Referrers.GetAsync(
            "repository",
            "sha256:subject");
        Page<OciImageIndex> secondPage = await client.Referrers.GetNextAsync(
            Assert.IsType<string>(firstPage.NextPageLink));

        Assert.Null(secondPage.NextPageLink);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("https://attacker.example/v2/repository/referrers/sha256:subject?last=sha256:first")]
    [InlineData("http://registry.example/v2/repository/referrers/sha256:subject?last=sha256:first")]
    public async Task GetAsync_CrossOriginNextLink_Throws(string nextPageLink)
    {
        var handler = new MockHttpMessageHandler();
        HttpResponseMessage response = CreateJsonResponse(
            """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[]}""");
        response.Headers.Add("Link", $"<{nextPageLink}>; rel=\"next\"");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            response);
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Referrers.GetAsync("repository", "sha256:subject"));

        Assert.Contains("registry origin", exception.Message);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task GetAsync_SameOriginRequestRedirectedCrossOrigin_Throws()
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
            {
                Headers =
                {
                    Location = new Uri("https://attacker.example/continuation")
                }
            });
        using var client = new RegistryClient("registry.example", null, innerHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Referrers.GetAsync("repository", "sha256:subject"));

        Assert.Contains("outside the configured registry origin", exception.Message);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(
        "sha512:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "sha512-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData(
        "test+algorithm+using+algorithm+separators+and+lots+of+characters+to+excercise+overall+truncation:alsoSome=InTheEncodedSectionToShowHyphenReplacementAndLotsAndLotsOfCharactersToExcerciseEncodedTruncation",
        "test-algorithm-using-algorithm-s-alsoSome-InTheEncodedSectionToShowHyphenReplacementAndLotsAndLot")]
    public async Task GetAsync_FallbackUsesTruncatedSanitizedTag(string digest, string expectedTag)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repository/referrers/{digest}",
            CreateNotFoundResponse());
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repository/manifests/{expectedTag}",
            CreateIndexResponse());
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> page = await client.Referrers.GetAsync("repository", digest);

        Assert.Empty(page.Value.Manifests);
        Assert.Null(page.NextPageLink);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task GetAsync_MissingFallbackTag_ReturnsEmptyPage()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            CreateNotFoundResponse());
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/manifests/sha256-subject",
            CreateNotFoundResponse());
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> page = await client.Referrers.GetAsync("repository", "sha256:subject");

        Assert.Equal(2, page.Value.SchemaVersion);
        Assert.Equal(ManifestMediaTypes.OciImageIndex1, page.Value.MediaType);
        Assert.Empty(page.Value.Manifests);
        Assert.Null(page.NextPageLink);
    }

    [Fact]
    public async Task GetAsync_FallbackTagThatIsNotAnIndex_ReturnsEmptyPage()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            CreateNotFoundResponse());
        HttpResponseMessage manifestResponse = CreateJsonResponse(
            """{"schemaVersion":2,"mediaType":"application/vnd.docker.distribution.manifest.v2+json","config":null,"layers":[]}""");
        manifestResponse.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(ManifestMediaTypes.DockerManifestSchema2);
        manifestResponse.Headers.Add("Docker-Content-Digest", "sha256:fallback");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/manifests/sha256-subject",
            manifestResponse);
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> page = await client.Referrers.GetAsync("repository", "sha256:subject");

        Assert.Empty(page.Value.Manifests);
        Assert.Null(page.NextPageLink);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":null}""")]
    public async Task GetAsync_StructurallyInvalidFallbackIndex_ReturnsEmptyPage(string json)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject?artifactType=application%2Fspdx%2Bjson",
            CreateNotFoundResponse());
        HttpResponseMessage invalidIndex = CreateJsonResponse(json);
        invalidIndex.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(ManifestMediaTypes.OciImageIndex1);
        invalidIndex.Headers.Add("Docker-Content-Digest", "sha256:fallback");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/manifests/sha256-subject",
            invalidIndex);
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> page = await client.Referrers.GetAsync(
            "repository",
            "sha256:subject",
            "application/spdx+json");

        Assert.Empty(page.Value.Manifests);
        Assert.Null(page.NextPageLink);
    }

    [Fact]
    public async Task GetAsync_MalformedReferrersError_FallsBack()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not json", Encoding.UTF8, "application/json")
            });
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/manifests/sha256-subject",
            CreateIndexResponse());
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        Page<OciImageIndex> page = await client.Referrers.GetAsync("repository", "sha256:subject");

        Assert.Empty(page.Value.Manifests);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task GetAsync_NonNotFoundError_DoesNotFallBack()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("")
            });
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Referrers.GetAsync("repository", "sha256:subject"));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task GetAsync_CancellationDoesNotFallBack()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            CreateNotFoundResponse());
        using var client = new RegistryClient("registry.example", null, new HttpClient(handler));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Referrers.GetAsync("repository", "sha256:subject", cancellationToken: cancellation.Token));

        Assert.Equal(1, handler.RemainingRequestCount);
    }

    private static HttpResponseMessage CreateJsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage CreateNotFoundResponse() =>
        new(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{"errors":[{"code":"MANIFEST_UNKNOWN","message":"manifest unknown"}]}""",
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage CreateIndexResponse()
    {
        HttpResponseMessage response = CreateJsonResponse(
            """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[]}""");
        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(ManifestMediaTypes.OciImageIndex1);
        response.Headers.Add("Docker-Content-Digest", "sha256:fallback");
        return response;
    }
}
