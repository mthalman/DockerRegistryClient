using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public partial class ManifestOperationsTests
{
    /// <summary>
    /// Verifies response and request identities remain checked when digest verification is shared with recursive copy.
    /// </summary>
    [Theory]
    [InlineData("sha256", false)]
    [InlineData("sha256", true)]
    [InlineData("sha384", false)]
    [InlineData("sha384", true)]
    [InlineData("sha512", false)]
    [InlineData("sha512", true)]
    public async Task GetAsync_ResponseDigest_VerifiesRequestedDigest(string algorithm, bool mismatchReference)
    {
        byte[] content = Encoding.UTF8.GetBytes("""{"schemaVersion":2,"manifests":[]}""");
        byte[] referenceContent = mismatchReference ? Encoding.UTF8.GetBytes("{}") : content;
        byte[] hash = algorithm switch
        {
            "sha384" => SHA384.HashData(referenceContent),
            "sha512" => SHA512.HashData(referenceContent),
            _ => SHA256.HashData(referenceContent)
        };
        string reference = $"{algorithm}:{Convert.ToHexString(hash).ToLowerInvariant()}";
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{reference}",
            ManifestResponse(ManifestMediaTypes.OciImageIndex1, content));
        using var client = CreateClient(handler);

        if (mismatchReference)
        {
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.Manifests.GetAsync("repo", reference));
            Assert.Contains(reference, exception.Message);
        }
        else
        {
            ManifestInfo result = await client.Manifests.GetAsync("repo", reference);
            Assert.Equal(RegistryFixture.GetDigest(content), result.DockerContentDigest);
            Assert.Equal(content, result.Content.ToArray());
        }

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Verifies digest checks hash only the supplied memory slice and preserve unsupported-algorithm behavior.
    /// </summary>
    [Theory]
    [InlineData("sha256")]
    [InlineData("sha384")]
    [InlineData("sha512")]
    [InlineData("custom")]
    public void VerifyDigestIfSupported_UsesMemorySlice(string algorithm)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("prefix{}suffix");
        ReadOnlyMemory<byte> content = bytes.AsMemory(6, 2);
        byte[] hash = algorithm switch
        {
            "sha384" => SHA384.HashData(content.Span),
            "sha512" => SHA512.HashData(content.Span),
            _ => SHA256.HashData(content.Span)
        };
        string digest = $"{algorithm}:{Convert.ToHexString(hash).ToLowerInvariant()}";

        Assert.Equal(algorithm != "custom", ManifestOperations.VerifyDigestIfSupported(digest, content));
    }

    /// <summary>
    /// Verifies manifest identity is resolved from the response bytes or digest reference when the registry omits its digest header.
    /// </summary>
    [Theory]
    [InlineData("latest")]
    [InlineData("sha256")]
    [InlineData("sha384")]
    [InlineData("sha512")]
    [InlineData("1algo:AbC_=-")]
    public async Task GetAsync_WithoutDigestHeader_UsesOriginalBytesAndReference(string reference)
    {
        byte[] content = Encoding.UTF8.GetBytes(" { \"schemaVersion\": 2, \"manifests\": [] } \n");
        string expectedDigest;
        if (reference.StartsWith("sha", StringComparison.Ordinal))
        {
            byte[] hash = reference switch
            {
                "sha384" => SHA384.HashData(content),
                "sha512" => SHA512.HashData(content),
                _ => SHA256.HashData(content)
            };
            reference = $"{reference}:{Convert.ToHexString(hash).ToLowerInvariant()}";
            expectedDigest = reference;
        }
        else
        {
            expectedDigest = reference == "latest" ? RegistryFixture.GetDigest(content) : reference;
        }

        HttpResponseMessage response = ManifestResponse(ManifestMediaTypes.OciImageIndex1, content);
        response.Headers.Remove("Docker-Content-Digest");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{reference}", response);
        using var client = CreateClient(handler);

        ManifestInfo result = await client.Manifests.GetAsync("repo", reference);

        Assert.Equal(expectedDigest, result.DockerContentDigest);
        Assert.Equal(content, result.Content.ToArray());
    }

    /// <summary>
    /// Verifies supported response and request digests are rejected when they do not match the manifest bytes.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAsync_MismatchedDigest_Throws(bool useResponseHeader)
    {
        HttpResponseMessage response = ManifestResponse(ManifestMediaTypes.OciImageIndex1);
        response.Headers.Remove("Docker-Content-Digest");
        string reference = "latest";
        if (useResponseHeader)
        {
            response.Headers.Add("Docker-Content-Digest", SubjectDigest);
        }
        else
        {
            reference = SubjectDigest;
        }

        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{reference}", response);
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.Manifests.GetAsync("repo", reference));
    }

    /// <summary>
    /// Verifies tag-based digest lookup downloads the manifest when the HEAD response omits its digest header.
    /// </summary>
    [Fact]
    public async Task GetDigestAsync_WithoutHeadDigest_DownloadsManifest()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Head,
            "https://registry.example/v2/repo/manifests/latest", new HttpResponseMessage(HttpStatusCode.OK));
        HttpResponseMessage response = ManifestResponse(ManifestMediaTypes.OciImageIndex1);
        response.Headers.Remove("Docker-Content-Digest");
        handler.AddExpectedRequest(HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/latest", response);
        using var client = CreateClient(handler);

        string digest = await client.Manifests.GetDigestAsync("repo", "latest");

        Assert.Equal(ManifestDigest, digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Verifies digest-pinned lookups use HEAD metadata or the requested digest without downloading the manifest.
    /// </summary>
    [Theory]
    [InlineData("sha256", 64, false)]
    [InlineData("sha256", 64, true)]
    [InlineData("sha384", 96, false)]
    [InlineData("sha384", 96, true)]
    [InlineData("sha512", 128, false)]
    [InlineData("sha512", 128, true)]
    [InlineData("custom", 3, false)]
    [InlineData("custom", 3, true)]
    public async Task GetDigestAsync_DigestReference_DoesNotDownloadManifest(
        string algorithm, int encodedLength, bool includeResponseDigest)
    {
        string reference = $"{algorithm}:{new string('a', encodedLength)}";
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        if (includeResponseDigest)
        {
            response.Headers.Add("Docker-Content-Digest", ManifestDigest);
        }

        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Head,
            $"https://registry.example/v2/repo/manifests/{reference}", response);
        using var client = CreateClient(handler);

        string digest = await client.Manifests.GetDigestAsync("repo", reference);

        Assert.Equal(includeResponseDigest ? ManifestDigest : reference, digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Verifies a supplied digest does not bypass HTTP errors from the existence check.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetDigestAsync_DigestReference_HeadFailureThrows(HttpStatusCode statusCode)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Head,
            $"https://registry.example/v2/repo/manifests/{ManifestDigest}",
            new HttpResponseMessage(statusCode));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<RegistryException>(
            () => client.Manifests.GetDigestAsync("repo", ManifestDigest));

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Verifies typed OCI manifest serialization omits null optional properties while preserving required values.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_OciModels_OmitsNullOptionalFieldsAndKeepsRequiredFields(bool index)
    {
        var descriptor = new ManifestReference
        {
            MediaType = ManifestMediaTypes.OciManifestSchema1,
            Digest = ManifestDigest,
            Size = 0,
            Urls = null!,
            Annotations = null!,
            Platform = new ManifestPlatform { Architecture = "amd64", Os = "linux" }
        };
        IManifest manifest = index
            ? new OciImageIndex { Manifests = [descriptor, new ManifestReference()] }
            : new OciImageManifest { Config = descriptor, Layers = [new OciDescriptor()], Annotations = null! };
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(request =>
        {
            using JsonDocument document = JsonDocument.Parse(
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult());
            JsonElement root = document.RootElement;
            Assert.False(root.TryGetProperty("artifactType", out _));
            Assert.False(root.TryGetProperty("subject", out _));
            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            JsonElement value = index ? root.GetProperty("manifests")[0] : root.GetProperty("config");
            Assert.Equal(0, value.GetProperty("size").GetInt64());
            Assert.Equal(ManifestDigest, value.GetProperty("digest").GetString());
            Assert.False(value.TryGetProperty("artifactType", out _));
            Assert.False(value.TryGetProperty("data", out _));
            Assert.False(value.TryGetProperty("urls", out _));
            Assert.False(value.TryGetProperty("annotations", out _));
            if (index)
            {
                JsonElement platform = value.GetProperty("platform");
                Assert.False(platform.TryGetProperty("variant", out _));
                Assert.False(platform.TryGetProperty("os.version", out _));
                Assert.False(root.GetProperty("manifests")[1].TryGetProperty("platform", out _));
            }
            else
            {
                Assert.False(root.TryGetProperty("annotations", out _));
                Assert.Equal(JsonValueKind.Array, root.GetProperty("layers").ValueKind);
            }
            return true;
        }, PublishResponse());
        using var client = CreateClient(handler);

        await client.Manifests.PublishAsync("repo", "typed", manifest);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Verifies fallback updates retry with a fresh strong ETag and preserve concurrent index entries.
    /// </summary>
    [Fact]
    public async Task PublishAsync_FallbackPreconditionFailed_RereadsIndexAndUsesFreshStrongValidator()
    {
        byte[] content = CreateSubjectManifestContent();
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Put,
            "https://registry.example/v2/repo/manifests/artifact", PublishResponse(content));
        var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"schemaVersion":2,"manifests":[]}""",
                Encoding.UTF8, ManifestMediaTypes.OciImageIndex1)
        };
        firstResponse.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
        handler.AddExpectedRequest(HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{SubjectTag}", firstResponse);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("\"v1\"", Assert.Single(request.Headers.IfMatch).ToString());
            return request.Method == HttpMethod.Put;
        }, new HttpResponseMessage(HttpStatusCode.PreconditionFailed));
        HttpResponseMessage secondResponse = ExistingFallbackResponse();
        secondResponse.Headers.ETag = new EntityTagHeaderValue("\"v2\"");
        handler.AddExpectedRequest(HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{SubjectTag}", secondResponse);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("\"v2\"", Assert.Single(request.Headers.IfMatch).ToString());
            using JsonDocument document = JsonDocument.Parse(
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult());
            JsonElement manifests = document.RootElement.GetProperty("manifests");
            Assert.Equal(2, manifests.GetArrayLength());
            Assert.Equal("1algo:AbC_=-", manifests[0].GetProperty("digest").GetString());
            Assert.Equal(RegistryFixture.GetDigest(content), manifests[1].GetProperty("digest").GetString());
            return request.Method == HttpMethod.Put;
        }, PublishResponse());
        using var client = CreateClient(handler);

        await client.Manifests.PublishAsync("repo", "artifact", content, ManifestMediaTypes.OciManifestSchema1);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Verifies fallback maintenance accepts an omitted index mediaType and uses only strong ETags conditionally.
    /// </summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("W/\"index-v1\"", false)]
    [InlineData("\"index-v1\"", false)]
    [InlineData(null, true)]
    [InlineData("W/\"index-v1\"", true)]
    [InlineData("\"index-v1\"", true)]
    public async Task FallbackMaintenance_OptionalMediaTypeAndEntityTag_AreHandled(string? entityTag, bool delete)
    {
        byte[] artifact = CreateSubjectManifestContent();
        string artifactDigest = RegistryFixture.GetDigest(artifact);
        var handler = new MockHttpMessageHandler();
        if (delete)
        {
            handler.AddExpectedRequest(HttpMethod.Get,
                $"https://registry.example/v2/repo/manifests/{artifactDigest}",
                ManifestResponse(ManifestMediaTypes.OciManifestSchema1, artifact));
            handler.AddExpectedRequest(HttpMethod.Get,
                $"https://registry.example/v2/repo/referrers/{SubjectDigest}", CreateManifestNotFoundResponse());
            handler.AddExpectedRequest(HttpMethod.Delete,
                $"https://registry.example/v2/repo/manifests/{artifactDigest}",
                new HttpResponseMessage(HttpStatusCode.Accepted));
        }
        else
        {
            handler.AddExpectedRequest(HttpMethod.Put,
                "https://registry.example/v2/repo/manifests/artifact", PublishResponse(artifact));
        }

        string indexJson = delete
            ? $$"""{"schemaVersion":2,"manifests":[{"mediaType":"{{ManifestMediaTypes.OciManifestSchema1}}","digest":"{{artifactDigest}}","size":{{artifact.Length}}}]}"""
            : """{"schemaVersion":2,"manifests":[]}""";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(indexJson, Encoding.UTF8, ManifestMediaTypes.OciImageIndex1)
        };
        if (entityTag is not null)
        {
            response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag);
        }
        handler.AddExpectedRequest(HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{SubjectTag}", response);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal($"https://registry.example/v2/repo/manifests/{SubjectTag}", request.RequestUri!.AbsoluteUri);
            Assert.Empty(request.Headers.IfNoneMatch);
            if (entityTag == "\"index-v1\"")
            {
                Assert.Equal(entityTag, Assert.Single(request.Headers.IfMatch).ToString());
            }
            else
            {
                Assert.Empty(request.Headers.IfMatch);
            }
            using JsonDocument document = JsonDocument.Parse(
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult());
            Assert.False(document.RootElement.TryGetProperty("subject", out _));
            Assert.False(document.RootElement.TryGetProperty("artifactType", out _));
            JsonElement manifests = document.RootElement.GetProperty("manifests");
            Assert.Equal(delete ? 0 : 1, manifests.GetArrayLength());
            if (!delete)
            {
                Assert.False(manifests[0].TryGetProperty("platform", out _));
                Assert.False(manifests[0].TryGetProperty("data", out _));
            }
            return true;
        }, PublishResponse());
        using var client = CreateClient(handler);

        if (delete)
        {
            await client.Manifests.DeleteAsync("repo", artifactDigest);
        }
        else
        {
            await client.Manifests.PublishAsync("repo", "artifact", artifact, ManifestMediaTypes.OciManifestSchema1);
        }

        Assert.Equal(0, handler.RemainingRequestCount);
    }
}
