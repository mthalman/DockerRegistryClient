using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public partial class ManifestOperationsTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task DeleteAsync_NativeReferrers_DoesNotBufferProbeContent(HttpStatusCode status)
    {
        var probeContent = new ProbeContent();
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{ManifestDigest}",
            ManifestResponse(ManifestMediaTypes.OciManifestSchema1, CreateSubjectManifestContent()));
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/referrers/{SubjectDigest}",
            new HttpResponseMessage(status) { Content = probeContent });
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Delete &&
                request.RequestUri == new Uri($"https://registry.example/v2/repo/manifests/{ManifestDigest}") &&
                probeContent.IsDisposed && !probeContent.IsSerialized,
            new HttpResponseMessage(HttpStatusCode.Accepted));
        using var client = CreateClient(handler);

        await client.Manifests.DeleteAsync("repo", ManifestDigest);

        Assert.False(probeContent.IsSerialized);
        Assert.True(probeContent.IsDisposed);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task DeleteAsync_NativeReferrersNotFound_UpdatesFallback()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{ManifestDigest}",
            ManifestResponse(ManifestMediaTypes.OciManifestSchema1, CreateSubjectManifestContent()));
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/referrers/{SubjectDigest}",
            CreateManifestNotFoundResponse());
        handler.AddExpectedRequest(
            HttpMethod.Delete,
            $"https://registry.example/v2/repo/manifests/{ManifestDigest}",
            new HttpResponseMessage(HttpStatusCode.Accepted));
        byte[] index = Encoding.UTF8.GetBytes(
            $$"""{"schemaVersion":2,"mediaType":"{{ManifestMediaTypes.OciImageIndex1}}","manifests":[{"mediaType":"{{ManifestMediaTypes.OciManifestSchema1}}","digest":"{{ManifestDigest}}","size":10}]}""");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{SubjectTag}",
            ManifestResponse(ManifestMediaTypes.OciImageIndex1, index));
        handler.AddExpectedRequest(
            request =>
            {
                using JsonDocument document = JsonDocument.Parse(
                    request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                return request.Method == HttpMethod.Put &&
                    request.RequestUri == new Uri($"https://registry.example/v2/repo/manifests/{SubjectTag}") &&
                    document.RootElement.GetProperty("manifests").GetArrayLength() == 0;
            },
            PublishResponse());
        using var client = CreateClient(handler);

        await client.Manifests.DeleteAsync("repo", ManifestDigest);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task DeleteAsync_NativeReferrersFailure_PreservesErrorsAndDoesNotDelete(HttpStatusCode status)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{ManifestDigest}",
            ManifestResponse(ManifestMediaTypes.OciManifestSchema1, CreateSubjectManifestContent()));
        var errorContent = new StringContent(
            """{"errors":[{"code":"DENIED","message":"access denied"}]}""",
            Encoding.UTF8, "application/json");
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/referrers/{SubjectDigest}",
            new HttpResponseMessage(status) { Content = errorContent });
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Manifests.DeleteAsync("repo", ManifestDigest));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal("DENIED", Assert.Single(exception.Errors).Code);
        Assert.Equal(2, handler.RequestCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => errorContent.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("not-json", ManifestMediaTypes.OciManifestSchema1, true)]
    [InlineData("""{"annotations":42}""", ManifestMediaTypes.OciManifestSchema1, true)]
    [InlineData("not-json", ManifestMediaTypes.OciImageIndex1, true)]
    [InlineData("""{"manifests":42}""", ManifestMediaTypes.OciImageIndex1, true)]
    [InlineData("not-json", ManifestMediaTypes.OciManifestSchema1, false)]
    [InlineData("""{"manifests":42}""", ManifestMediaTypes.OciImageIndex1, false)]
    public async Task PublishAsync_UnusableMetadata_OnlyParsesWhenFallbackIsNeeded(
        string json, string mediaType, bool acknowledged)
    {
        byte[] content = Encoding.UTF8.GetBytes(json);
        HttpResponseMessage response = PublishResponse(content);
        if (acknowledged)
        {
            response.Headers.Add("OCI-Subject", SubjectDigest);
        }
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Put, "https://registry.example/v2/repo/manifests/latest", response);
        using var client = CreateClient(handler);

        if (acknowledged)
        {
            ManifestPublishResult result = await client.Manifests.PublishAsync("repo", "latest", content, mediaType);
            Assert.Equal(RegistryFixture.GetDigest(content), result.Digest);
            Assert.Equal("/v2/repo/manifests/sha256:manifest", result.Location);
        }
        else
        {
            await Assert.ThrowsAsync<JsonException>(
                () => client.Manifests.PublishAsync("repo", "latest", content, mediaType));
        }

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("digest-mismatch")]
    [InlineData("digest-invalid")]
    [InlineData("location")]
    public async Task PublishAsync_SubjectAcknowledged_StillEnforcesResponseChecks(string failure)
    {
        byte[] content = Encoding.UTF8.GetBytes("not-json");
        HttpResponseMessage response = PublishResponse(content);
        response.Headers.Add("OCI-Subject", SubjectDigest);
        if (failure == "http")
        {
            response.StatusCode = HttpStatusCode.Forbidden;
            response.Content = new StringContent(
                """{"errors":[{"code":"DENIED","message":"access denied"}]}""",
                Encoding.UTF8, "application/json");
        }
        else if (failure == "location")
        {
            response.Headers.Location = null;
        }
        else
        {
            response.Headers.Remove("Docker-Content-Digest");
            response.Headers.Add("Docker-Content-Digest",
                failure == "digest-invalid" ? "sha256:abc" : $"sha256:{new string('0', 64)}");
        }
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Put, "https://registry.example/v2/repo/manifests/latest", response);
        using var client = CreateClient(handler);

        Task Publish() => client.Manifests.PublishAsync(
            "repo", "latest", content, ManifestMediaTypes.OciManifestSchema1);
        if (failure == "http")
        {
            RegistryException exception = await Assert.ThrowsAsync<RegistryException>(Publish);
            Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
            Assert.Equal("DENIED", Assert.Single(exception.Errors).Code);
        }
        else
        {
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(Publish);
            Assert.Contains(failure switch
            {
                "location" => "Location header not set.",
                "digest-invalid" => "invalid manifest digest",
                _ => "but the content has digest"
            }, exception.Message);
        }

        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("sha256")]
    [InlineData("sha384")]
    [InlineData("sha512")]
    public void VerifyDigestIfSupported_MemorySlice_VerifiesSupportedAlgorithms(string algorithm)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("prefix-content-suffix");
        ReadOnlyMemory<byte> content = bytes.AsMemory(7, 7);
        byte[] hash = algorithm switch
        {
            "sha256" => SHA256.HashData(content.Span),
            "sha384" => SHA384.HashData(content.Span),
            _ => SHA512.HashData(content.Span)
        };
        string encoded = Convert.ToHexString(hash).ToLowerInvariant();
        string digest = $"{algorithm}:{encoded}";
        byte[] emptyHash = algorithm switch
        {
            "sha256" => SHA256.HashData([]),
            "sha384" => SHA384.HashData([]),
            _ => SHA512.HashData([])
        };

        Assert.True(ManifestOperations.VerifyDigestIfSupported(digest, content));
        Assert.True(ManifestOperations.VerifyDigestIfSupported(
            $"{algorithm}:{Convert.ToHexString(emptyHash).ToLowerInvariant()}", ReadOnlyMemory<byte>.Empty));
        string mismatch = $"{algorithm}:{new string('0', encoded.Length)}";
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ManifestOperations.VerifyDigestIfSupported(mismatch, content));
        Assert.Equal(
            $"Registry returned manifest digest '{mismatch}', but the content has digest '{digest}'.",
            exception.Message);
    }

    [Theory]
    [InlineData("1algo:AbC_=-")]
    [InlineData("blake3:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void VerifyDigestIfSupported_UnsupportedAlgorithm_ReturnsFalse(string digest) =>
        Assert.False(ManifestOperations.VerifyDigestIfSupported(digest, new byte[1024 * 1024]));

    [Theory]
    [InlineData("sha256:abc")]
    [InlineData("sha384:abc")]
    [InlineData("sha512:abc")]
    [InlineData("invalid")]
    public void VerifyDigestIfSupported_InvalidDigest_PreservesException(string digest)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ManifestOperations.VerifyDigestIfSupported(digest, ReadOnlyMemory<byte>.Empty));
        Assert.Equal($"Registry returned an invalid manifest digest '{digest}'.", exception.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CopyAsync_ChildDigestMismatch_ValidatesDescriptorAndResponse(bool mismatchDescriptor)
    {
        byte[] child = Encoding.UTF8.GetBytes("""{"child":true}""");
        string actualDigest = RegistryFixture.GetDigest(child);
        string incorrectDigest = $"sha256:{new string('0', 64)}";
        string descriptorDigest = mismatchDescriptor ? incorrectDigest : actualDigest;
        string responseDigest = mismatchDescriptor ? actualDigest : incorrectDigest;
        const string childMediaType = "application/vnd.example.manifest";
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""{"schemaVersion":2,"mediaType":"{{ManifestMediaTypes.OciImageIndex1}}","manifests":[{"mediaType":"{{childMediaType}}","digest":"{{descriptorDigest}}","size":{{child.Length}}}]}""");
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/manifests/latest",
            ManifestResponse(ManifestMediaTypes.OciImageIndex1, root));
        HttpResponseMessage childResponse = ManifestResponse(childMediaType, child);
        childResponse.Headers.Remove("Docker-Content-Digest");
        childResponse.Headers.Add("Docker-Content-Digest", responseDigest);
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/manifests/{descriptorDigest}",
            childResponse);
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/manifests/{descriptorDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        using var source = CreateClient(sourceHandler);
        using var destination = CreateClient(destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.CopyAsync("repo", "latest", destination, "destination", "latest"));

        Assert.Contains(incorrectDigest, exception.Message);
        Assert.Contains(actualDigest, exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
        Assert.Equal(1, destinationHandler.RequestCount);
    }

    private sealed class ProbeContent : HttpContent
    {
        public bool IsSerialized { get; private set; }
        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            IsSerialized = true;
            return stream.WriteAsync(new byte[1024 * 1024]).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 1024 * 1024;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
