using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    [Fact]
    public async Task PublishAsync_SendsExactContentAndReturnsResponseHeaders()
    {
        byte[] content = [0, 1, 2, 255];
        string expectedDigest = RegistryFixture.GetDigest(content);
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri("/v2/repo/manifests/sha256:published", UriKind.Relative);
        response.Headers.Add("Docker-Content-Digest", expectedDigest);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request =>
            {
                byte[] requestContent = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return request.Method == HttpMethod.Put &&
                    request.RequestUri == new Uri("https://registry.example/v2/repo/manifests/latest") &&
                    request.Content.Headers.ContentType?.MediaType == "application/vnd.example.manifest" &&
                    requestContent.SequenceEqual(content);
            },
            response);
        using var client = CreateClient(handler);

        ManifestPublishResult result = await client.Manifests.PublishAsync(
            "repo",
            "latest",
            content,
            "application/vnd.example.manifest");

        Assert.Equal("/v2/repo/manifests/sha256:published", result.Location);
        Assert.Equal(expectedDigest, result.Digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_ResponseWithoutDigest_ReturnsNullDigest()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri("/v2/repo/manifests/latest", UriKind.Relative);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/latest",
            response);
        using var client = CreateClient(handler);

        ManifestPublishResult result = await client.Manifests.PublishAsync(
            "repo",
            "latest",
            Encoding.UTF8.GetBytes("{}"),
            ManifestMediaTypes.OciManifestSchema1);

        Assert.Equal("/v2/repo/manifests/latest", result.Location);
        Assert.Null(result.Digest);
    }

    [Fact]
    public async Task PublishAsync_MismatchedResponseDigest_Throws()
    {
        byte[] content = Encoding.UTF8.GetBytes("{}");
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri("/v2/repo/manifests/latest", UriKind.Relative);
        response.Headers.Add("Docker-Content-Digest", $"sha256:{new string('0', 64)}");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/latest",
            response);
        using var client = CreateClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Manifests.PublishAsync(
                "repo",
                "latest",
                content,
                "application/vnd.example.manifest"));

        Assert.Contains(RegistryFixture.GetDigest(content), exception.Message);
    }

    [Theory]
    [InlineData("sha256:abc")]
    [InlineData("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaA")]
    [InlineData("sha384:abc")]
    [InlineData("sha512:abc")]
    public async Task PublishAsync_InvalidResponseDigest_Throws(string digest)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri("/v2/repo/manifests/latest", UriKind.Relative);
        response.Headers.Add("Docker-Content-Digest", digest);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/latest",
            response);
        using var client = CreateClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Manifests.PublishAsync(
                "repo",
                "latest",
                Encoding.UTF8.GetBytes("{}"),
                "application/vnd.example.manifest"));

        Assert.Contains("invalid manifest digest", exception.Message);
    }

    [Theory]
    [InlineData("1algo:AbC_=-")]
    [InlineData("blake3:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task PublishAsync_ValidUnsupportedResponseDigest_ReturnsDigest(string digest)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri("/v2/repo/manifests/latest", UriKind.Relative);
        response.Headers.Add("Docker-Content-Digest", digest);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/latest",
            response);
        using var client = CreateClient(handler);

        ManifestPublishResult result = await client.Manifests.PublishAsync(
            "repo",
            "latest",
            Encoding.UTF8.GetBytes("{}"),
            "application/vnd.example.manifest");

        Assert.Equal(digest, result.Digest);
    }

    [Fact]
    public async Task PublishAsync_OAuthChallenge_RetriesWithOriginalContent()
    {
        byte[] content = Encoding.UTF8.GetBytes("""{"schemaVersion":2}""");
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => IsExpectedPublishRequest(request, content, authorization: null),
            unauthorizedResponse);
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Get &&
                request.RequestUri?.Host == "auth.example" &&
                request.RequestUri.Query.Contains("scope=repository:repo:push"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"access-token"}""")
            });
        handler.AddExpectedRequest(
            request => IsExpectedPublishRequest(request, content, authorization: "access-token"),
            PublishResponse(content));
        using var client = new RegistryClient("registry.example", null, handler);

        ManifestPublishResult result = await client.Manifests.PublishAsync(
            "repo",
            "latest",
            content,
            ManifestMediaTypes.OciManifestSchema1);

        Assert.Equal(RegistryFixture.GetDigest(content), result.Digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_ReadOnlyImplementation_ThrowsNotSupportedException()
    {
        IManifestOperations operations = new ReadOnlyManifestOperations();

        NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => operations.PublishAsync(
                "repo",
                "latest",
                Encoding.UTF8.GetBytes("{}"),
                ManifestMediaTypes.OciManifestSchema1));

        Assert.Contains(nameof(ReadOnlyManifestOperations), exception.Message);
    }

    [Fact]
    public async Task PublishAsync_SubjectAcknowledged_DoesNotPublishFallbackIndex()
    {
        byte[] content = CreateSubjectManifestContent();
        HttpResponseMessage response = PublishResponse(content);
        response.Headers.Add("OCI-Subject", "sha256:subject");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/artifact",
            response);
        using var client = CreateClient(handler);

        await client.Manifests.PublishAsync(
            "repo",
            "artifact",
            content,
            ManifestMediaTypes.OciManifestSchema1);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_SubjectNotAcknowledged_PublishesFallbackIndex()
    {
        byte[] content = CreateSubjectManifestContent();
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri("/v2/repo/manifests/artifact", UriKind.Relative);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/artifact",
            response);
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/sha256-subject",
            CreateManifestNotFoundResponse());
        handler.AddExpectedRequest(
            request =>
            {
                using JsonDocument document = JsonDocument.Parse(
                    request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                JsonElement descriptor = document.RootElement.GetProperty("manifests")[0];
                return request.Method == HttpMethod.Put &&
                    request.RequestUri == new Uri("https://registry.example/v2/repo/manifests/sha256-subject") &&
                    request.Headers.IfNoneMatch.Any(value => value == EntityTagHeaderValue.Any) &&
                    request.Content.Headers.ContentType?.MediaType == ManifestMediaTypes.OciImageIndex1 &&
                    descriptor.GetProperty("mediaType").GetString() == ManifestMediaTypes.OciManifestSchema1 &&
                    descriptor.GetProperty("digest").GetString() == RegistryFixture.GetDigest(content) &&
                    descriptor.GetProperty("size").GetInt64() == content.LongLength &&
                    descriptor.GetProperty("artifactType").GetString() == "application/vnd.example.sbom" &&
                    descriptor.GetProperty("annotations").GetProperty("name").GetString() == "test";
            },
            PublishResponse());
        using var client = CreateClient(handler);

        ManifestPublishResult result = await client.Manifests.PublishAsync(
            "repo",
            "artifact",
            content,
            ManifestMediaTypes.OciManifestSchema1);

        Assert.Null(result.Digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_SubjectAtUnknownDigest_UsesDigestInFallbackIndex()
    {
        const string digest = "1algo:AbC_=-";
        byte[] content = CreateSubjectManifestContent();
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            $"https://registry.example/v2/repo/manifests/{digest}",
            PublishResponse());
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/sha256-subject",
            CreateManifestNotFoundResponse());
        handler.AddExpectedRequest(
            request =>
            {
                using JsonDocument document = JsonDocument.Parse(
                    request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                return document.RootElement.GetProperty("manifests")[0]
                    .GetProperty("digest").GetString() == digest;
            },
            PublishResponse());
        using var client = CreateClient(handler);

        await client.Manifests.PublishAsync(
            "repo",
            digest,
            content,
            ManifestMediaTypes.OciManifestSchema1);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{}]}""")]
    [InlineData("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[null]}""")]
    [InlineData("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"invalid","size":1}]}""")]
    [InlineData("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:abc","size":1}]}""")]
    [InlineData("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeE","size":1}]}""")]
    [InlineData("""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"application/vnd.oci.image.manifest.v1+json","digest":"sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee","size":-1}]}""")]
    public async Task PublishAsync_InvalidExistingFallback_ThrowsWithoutOverwriting(string indexJson)
    {
        byte[] content = CreateSubjectManifestContent();
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/artifact",
            PublishResponse(content));
        var fallbackResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(indexJson, Encoding.UTF8, ManifestMediaTypes.OciImageIndex1)
        };
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/sha256-subject",
            fallbackResponse);
        using var client = CreateClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Manifests.PublishAsync(
                "repo",
                "artifact",
                content,
                ManifestMediaTypes.OciManifestSchema1));

        Assert.Contains("valid OCI image index", exception.Message);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_ExistingFallbackWithoutDigest_PreservesIndexWithoutRecursing()
    {
        byte[] content = CreateSubjectManifestContent();
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/artifact",
            PublishResponse(content));
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/sha256-subject",
            ExistingFallbackResponse());
        handler.AddExpectedRequest(
            request =>
            {
                using JsonDocument document = JsonDocument.Parse(
                    request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                JsonElement manifests = document.RootElement.GetProperty("manifests");
                return request.Method == HttpMethod.Put &&
                    manifests.GetArrayLength() == 2 &&
                    manifests[0].GetProperty("digest").GetString() == "1algo:AbC_=-" &&
                    manifests[1].GetProperty("digest").GetString() == RegistryFixture.GetDigest(content);
            },
            PublishResponse());
        using var client = CreateClient(handler);

        await client.Manifests.PublishAsync(
            "repo",
            "artifact",
            content,
            ManifestMediaTypes.OciManifestSchema1);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_TypedManifest_SerializesRuntimeType()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request =>
            {
                string requestContent = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using JsonDocument document = JsonDocument.Parse(requestContent);
                return request.Content.Headers.ContentType?.MediaType == "application/vnd.example.typed" &&
                    document.RootElement.GetProperty("customValue").GetString() == "preserved";
            },
            PublishResponse());
        using var client = CreateClient(handler);
        IManifest manifest = new CustomManifest
        {
            MediaType = "application/vnd.example.typed",
            CustomValue = "preserved"
        };

        await client.Manifests.PublishAsync("repo", "typed", manifest);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_KnownManifestModels_SendTheirMediaTypes()
    {
        (IManifest Manifest, string MediaType)[] cases =
        [
            (new DockerManifest(), ManifestMediaTypes.DockerManifestSchema2),
            (new ManifestList(), ManifestMediaTypes.DockerManifestList),
            (new OciImageManifest(), ManifestMediaTypes.OciManifestSchema1),
            (new OciImageIndex(), ManifestMediaTypes.OciImageIndex1)
        ];
        var handler = new MockHttpMessageHandler();
        foreach ((IManifest _, string expectedMediaType) in cases)
        {
            handler.AddExpectedRequest(
                request =>
                {
                    string requestContent = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    using JsonDocument document = JsonDocument.Parse(requestContent);
                    return request.Content.Headers.ContentType?.MediaType == expectedMediaType &&
                        document.RootElement.GetProperty("schemaVersion").GetInt32() == 2;
                },
                PublishResponse());
        }
        using var client = CreateClient(handler);

        foreach ((IManifest manifest, string _) in cases)
        {
            await client.Manifests.PublishAsync("repo", "typed", manifest);
        }

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_RawManifest_PreservesOriginalBytes()
    {
        byte[] content = Encoding.UTF8.GetBytes("""{"spacing": true, "value": 1}""");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request =>
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(content) &&
                request.Content.Headers.ContentType?.MediaType == "application/vnd.example.raw",
            PublishResponse());
        using var client = CreateClient(handler);

        await client.Manifests.PublishAsync(
            "repo",
            "raw",
            new RawManifest("application/vnd.example.raw", content));

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task PublishAsync_MissingMediaType_ThrowsBeforeSendingRequest()
    {
        var handler = new MockHttpMessageHandler();
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.Manifests.PublishAsync("repo", "latest", new CustomManifest()));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.Manifests.PublishAsync("repo", "latest", ReadOnlyMemory<byte>.Empty, " "));
    }

    [Fact]
    public async Task PublishAsync_UnsuccessfulResponse_ThrowsRegistryException()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/latest",
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"errors":[{"code":"MANIFEST_INVALID","message":"manifest invalid"}]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Manifests.PublishAsync(
                "repo",
                "latest",
                Encoding.UTF8.GetBytes("{}"),
                ManifestMediaTypes.OciManifestSchema1));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains(exception.Errors, error => error.Code == "MANIFEST_INVALID");
    }

    [Fact]
    public async Task PublishAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var handler = new MockHttpMessageHandler();
        using var client = CreateClient(handler);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Manifests.PublishAsync(
                "repo",
                "latest",
                ReadOnlyMemory<byte>.Empty,
                ManifestMediaTypes.OciManifestSchema1,
                cancellationTokenSource.Token));
    }

    [Fact]
    public async Task DeleteAsync_SendsDigestReference()
    {
        string digest = $"sha256:{new string('a', 64)}";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{digest}",
            StoredManifestResponse());
        handler.AddExpectedRequest(
            HttpMethod.Delete,
            $"https://registry.example/v2/repo/manifests/{digest}",
            new HttpResponseMessage(HttpStatusCode.Accepted));
        using var client = CreateClient(handler);

        await client.Manifests.DeleteAsync("repo", digest);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task DeleteAsync_NumericLeadingUnknownAlgorithm_SendsDigestReference()
    {
        const string digest = "1algo:AbC_=-";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{digest}",
            StoredManifestResponse());
        handler.AddExpectedRequest(
            HttpMethod.Delete,
            $"https://registry.example/v2/repo/manifests/{digest}",
            new HttpResponseMessage(HttpStatusCode.Accepted));
        using var client = CreateClient(handler);

        await client.Manifests.DeleteAsync("repo", digest);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task DeleteAsync_TagReference_ThrowsBeforeSendingRequest()
    {
        var handler = new MockHttpMessageHandler();
        using var client = CreateClient(handler);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => client.Manifests.DeleteAsync("repo", "latest"));

        Assert.Equal("digest", exception.ParamName);
        Assert.Contains("Tags cannot be deleted", exception.Message);
    }

    [Theory]
    [InlineData("sha256:abc")]
    [InlineData("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaA")]
    [InlineData("sha384:abc")]
    [InlineData("sha512:abc")]
    [InlineData("Blake3:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task DeleteAsync_InvalidDigest_ThrowsBeforeSendingRequest(string digest)
    {
        var handler = new MockHttpMessageHandler();
        using var client = CreateClient(handler);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => client.Manifests.DeleteAsync("repo", digest));

        Assert.Equal("digest", exception.ParamName);
    }

    [Fact]
    public async Task DeleteAsync_UnsuccessfulResponse_ThrowsRegistryException()
    {
        string digest = $"sha256:{new string('b', 64)}";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{digest}",
            StoredManifestResponse());
        handler.AddExpectedRequest(
            HttpMethod.Delete,
            $"https://registry.example/v2/repo/manifests/{digest}",
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    """{"errors":[{"code":"MANIFEST_UNKNOWN","message":"manifest unknown"}]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Manifests.DeleteAsync("repo", digest));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains(exception.Errors, error => error.Code == "MANIFEST_UNKNOWN");
    }

    [Fact]
    public async Task DeleteAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var handler = new MockHttpMessageHandler();
        using var client = CreateClient(handler);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Manifests.DeleteAsync(
                "repo",
                $"sha256:{new string('c', 64)}",
                cancellationTokenSource.Token));
    }

    private static RegistryClient CreateClient(HttpMessageHandler handler) =>
        new("registry.example", null, new HttpClient(handler), disposeHttpClient: true);

    private static bool IsExpectedPublishRequest(
        HttpRequestMessage request,
        byte[] expectedContent,
        string? authorization) =>
        request.Method == HttpMethod.Put &&
        request.RequestUri == new Uri("https://registry.example/v2/repo/manifests/latest") &&
        request.Headers.Authorization?.Parameter == authorization &&
        request.Content?.Headers.ContentType?.MediaType == ManifestMediaTypes.OciManifestSchema1 &&
        request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(expectedContent);

    private static HttpResponseMessage PublishResponse(byte[]? content = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri("/v2/repo/manifests/sha256:manifest", UriKind.Relative);
        if (content is not null)
        {
            response.Headers.Add("Docker-Content-Digest", RegistryFixture.GetDigest(content));
        }

        return response;
    }

    private static HttpResponseMessage StoredManifestResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/vnd.example.manifest")
        };

    private static HttpResponseMessage ExistingFallbackResponse()
    {
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            mediaType = ManifestMediaTypes.OciImageIndex1,
            manifests = new[]
            {
                new
                {
                    mediaType = ManifestMediaTypes.OciManifestSchema1,
                    digest = "1algo:AbC_=-",
                    size = 10
                }
            },
            subject = new
            {
                mediaType = ManifestMediaTypes.OciManifestSchema1,
                digest = "sha256:self",
                size = 20
            }
        });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(
            ManifestMediaTypes.OciImageIndex1);
        return response;
    }

    private static byte[] CreateSubjectManifestContent() =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            mediaType = ManifestMediaTypes.OciManifestSchema1,
            artifactType = "application/vnd.example.sbom",
            config = new
            {
                mediaType = "application/vnd.oci.empty.v1+json",
                size = 2,
                digest = "sha256:config"
            },
            layers = Array.Empty<object>(),
            subject = new
            {
                mediaType = ManifestMediaTypes.OciManifestSchema1,
                size = 100,
                digest = "sha256:subject"
            },
            annotations = new Dictionary<string, string> { ["name"] = "test" }
        });

    private static HttpResponseMessage CreateManifestNotFoundResponse() =>
        new(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{"errors":[{"code":"MANIFEST_UNKNOWN","message":"manifest unknown"}]}""",
                Encoding.UTF8,
                "application/json")
        };

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

    private sealed class CustomManifest : Manifest
    {
        [JsonPropertyName("customValue")]
        public string? CustomValue { get; set; }
    }

    private sealed class ReadOnlyManifestOperations : IManifestOperations
    {
        public Task<ManifestInfo> GetAsync(
            string repositoryName,
            string tagOrDigest,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> ExistsAsync(
            string repositoryName,
            string digest,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string> GetDigestAsync(
            string repositoryName,
            string tagOrDigest,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
