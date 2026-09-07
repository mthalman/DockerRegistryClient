using System.Net;
using System.Text;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class RegistryClientExtensionsTests
{
    private const string OciManifestMediaType = ManifestMediaTypes.OciManifestSchema1;
    private const string OciIndexMediaType = ManifestMediaTypes.OciImageIndex1;
    private const string BlobDigest = "custom:blob";
    private const string RootDigest = "custom:root";

    [Fact]
    public async Task CopyAsync_CrossRegistry_CopiesBlobsAndPublishesExactRootContent()
    {
        byte[] config = Encoding.UTF8.GetBytes("config");
        byte[] layer = Encoding.UTF8.GetBytes("layer");
        byte[] manifest = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciManifestMediaType}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"custom:config","size":6},"layers":[{"mediaType":"application/vnd.oci.image.layer.v1.tar","digest":"custom:layer","size":5}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/repo/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/repo/blobs/custom:config",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(config) });
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/repo/blobs/custom:layer",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(layer) });

        var destinationHandler = new MockHttpMessageHandler();
        AddBlobUpload(destinationHandler, "custom:config", config);
        AddBlobUpload(destinationHandler, "custom:layer", layer);
        destinationHandler.AddExpectedRequest(
            request =>
                request.Method == HttpMethod.Put &&
                request.RequestUri == new Uri("https://destination.example/v2/destination/repo/manifests/copied") &&
                request.Content!.Headers.ContentType?.MediaType == OciManifestMediaType &&
                request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(manifest),
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        ManifestPublishResult result = await sourceClient.CopyAsync(
            "source/repo",
            "latest",
            destinationClient,
            "destination/repo",
            "copied");

        Assert.Equal(RootDigest, result.Digest);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_CrossRegistry_StreamsBlobAfterDestinationUploadStarts()
    {
        byte[] blob = Encoding.UTF8.GetBytes("blob");
        byte[] manifest = SingleBlobManifest();
        bool destinationUploadStarted = false;
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(
                    new GatedReadStream(blob, () => destinationUploadStarted))
            });
        Guid uploadId = Guid.NewGuid();
        string uploadLocation = $"/v2/destination/blobs/uploads/{uploadId}";
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Post,
            "https://destination.example/v2/destination/blobs/uploads/",
            UploadInitializationResponse(uploadLocation));
        destinationHandler.AddExpectedRequest(
            request =>
            {
                destinationUploadStarted = true;
                return request.Method == HttpMethod.Put &&
                    request.Content!.Headers.ContentLength == blob.LongLength &&
                    request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(blob);
            },
            BlobUploadResponse(BlobDigest));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://destination.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.True(destinationUploadStarted);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_CrossRegistry_ReplaysBlobOnTemporaryRedirect()
    {
        byte[] blob = Encoding.UTF8.GetBytes("blob");
        byte[] manifest = SingleBlobManifest();
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(blob) });
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(blob) });

        Guid uploadId = Guid.NewGuid();
        string uploadLocation = $"/v2/destination/blobs/uploads/{uploadId}";
        string redirectedLocation = $"https://storage.example/uploads/{uploadId}?digest={Uri.EscapeDataString(BlobDigest)}";
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
        redirectResponse.Headers.Location = new Uri(redirectedLocation);
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Post,
            "https://destination.example/v2/destination/blobs/uploads/",
            UploadInitializationResponse(uploadLocation));
        destinationHandler.AddExpectedRequest(
            request =>
                request.Method == HttpMethod.Put &&
                request.Content!.Headers.ContentLength == blob.LongLength &&
                SerializeContent(request.Content).SequenceEqual(blob),
            redirectResponse);
        destinationHandler.AddExpectedRequest(
            request =>
                request.Method == HttpMethod.Put &&
                request.RequestUri == new Uri(redirectedLocation) &&
                request.Content!.Headers.ContentLength == blob.LongLength &&
                SerializeContent(request.Content).SequenceEqual(blob),
            BlobUploadResponse(BlobDigest));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://destination.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_SameRegistry_MountsBlobWithoutDownloadingIt()
    {
        byte[] manifest = SingleBlobManifest();
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request =>
                request.Method == HttpMethod.Post &&
                request.RequestUri?.AbsoluteUri ==
                    "https://registry.example/v2/destination/blobs/uploads/?mount=custom%3Ablob&from=source",
            new HttpResponseMessage(HttpStatusCode.Created));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            BlobExistsResponse(4));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("registry.example", sourceHandler);
        using var destinationClient = CreateClient("registry.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_ConflictingDuplicateBlobSizes_Throws()
    {
        byte[] blob = Encoding.UTF8.GetBytes("blob");
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciManifestMediaType}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"{{BlobDigest}}","size":4},"layers":[{"mediaType":"application/vnd.oci.image.layer.v1.tar","digest":"{{BlobDigest}}","size":5}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, root));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(blob) });
        var destinationHandler = new MockHttpMessageHandler();
        AddBlobUpload(destinationHandler, BlobDigest, blob);
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination/repo",
                "latest"));

        Assert.Contains("conflicting sizes", exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_ExistingBlobWithMismatchedSize_Throws()
    {
        byte[] manifest = SingleBlobManifest();
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/blobs/{BlobDigest}",
            BlobExistsResponse(5));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains("has size 5", exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_MountedBlobWithMismatchedSize_Throws()
    {
        byte[] manifest = SingleBlobManifest();
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Post &&
                request.RequestUri!.Query.Contains("mount="),
            new HttpResponseMessage(HttpStatusCode.Created));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            BlobExistsResponse(5));
        using var sourceClient = CreateClient("registry.example", sourceHandler);
        using var destinationClient = CreateClient("registry.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains("has size 5", exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_MountReturnsUploadSession_ReusesItForFallbackUpload()
    {
        byte[] blob = Encoding.UTF8.GetBytes("blob");
        byte[] manifest = SingleBlobManifest();
        Guid uploadId = Guid.NewGuid();
        string uploadLocation = $"/v2/destination/blobs/uploads/{uploadId}";
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(blob) });
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Post && request.RequestUri!.Query.Contains("mount="),
            UploadInitializationResponse(uploadLocation));
        destinationHandler.AddExpectedRequest(
            request =>
                request.Method == HttpMethod.Put &&
                request.RequestUri == new Uri(
                    $"https://registry.example{uploadLocation}?digest={Uri.EscapeDataString(BlobDigest)}") &&
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(blob),
            BlobUploadResponse(BlobDigest));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("registry.example", sourceHandler);
        using var destinationClient = CreateClient("registry.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_SameRegistryFallback_ReadsSourceBeforeStartingUpload()
    {
        byte[] blob = Encoding.UTF8.GetBytes("blob");
        byte[] manifest = SingleBlobManifest();
        bool destinationUploadStarted = false;
        Guid uploadId = Guid.NewGuid();
        string uploadLocation = $"/v2/destination/blobs/uploads/{uploadId}";
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(
                    new GatedReadStream(blob, () => !destinationUploadStarted))
            });
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Post && request.RequestUri!.Query.Contains("mount="),
            UploadInitializationResponse(uploadLocation));
        destinationHandler.AddExpectedRequest(
            request =>
            {
                destinationUploadStarted = true;
                return request.Method == HttpMethod.Put &&
                    request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(blob);
            },
            BlobUploadResponse(BlobDigest));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("registry.example", sourceHandler);
        using var destinationClient = CreateClient("registry.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.True(destinationUploadStarted);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_SameRegistryFallback_StopsAfterDescriptorSizeIsExceeded()
    {
        byte[] manifest = SingleBlobManifest();
        var oversizedBlob = new TrackingReadStream(new byte[1024 * 1024]);
        Guid uploadId = Guid.NewGuid();
        string uploadLocation = $"/v2/destination/blobs/uploads/{uploadId}";
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(oversizedBlob)
            });
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Post && request.RequestUri!.Query.Contains("mount="),
            UploadInitializationResponse(uploadLocation));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Delete,
            $"https://registry.example{uploadLocation}",
            new HttpResponseMessage(HttpStatusCode.NoContent));
        using var sourceClient = CreateClient("registry.example", sourceHandler);
        using var destinationClient = CreateClient("registry.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains("more data than its declared size of 4", exception.Message);
        Assert.Equal(5, oversizedBlob.BytesRead);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CopyAsync_ChildDescriptorMismatch_ThrowsBeforePublishingChild(
        bool mismatchSize)
    {
        byte[] child = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","manifests":[]}
            """);
        string childDigest = RegistryFixture.GetDigest(child);
        string descriptorMediaType = mismatchSize
            ? OciIndexMediaType
            : "application/vnd.example.unexpected";
        long descriptorSize = mismatchSize ? child.LongLength + 1 : child.LongLength;
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","manifests":[{"mediaType":"{{descriptorMediaType}}","digest":"{{childDigest}}","size":{{descriptorSize}}}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciIndexMediaType, RootDigest, root));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/manifests/{childDigest}",
            ManifestResponse(OciIndexMediaType, childDigest, child));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/manifests/{childDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains(childDigest, exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_ConflictingDuplicateManifestDescriptors_Throws()
    {
        byte[] child = Encoding.UTF8.GetBytes("""{"opaque":true}""");
        string childDigest = RegistryFixture.GetDigest(child);
        const string childMediaType = "application/vnd.example.manifest";
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","manifests":[{"mediaType":"{{childMediaType}}","digest":"{{childDigest}}","size":{{child.LongLength}}},{"mediaType":"{{childMediaType}}","digest":"{{childDigest}}","size":{{child.LongLength + 1}}}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciIndexMediaType, RootDigest, root));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/manifests/{childDigest}",
            ManifestResponse(childMediaType, childDigest, child));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/manifests/{childDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            $"https://destination.example/v2/destination/manifests/{childDigest}",
            PublishResponse(childDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains("conflicting descriptors", exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_UnsupportedMount_StartsNormalUpload()
    {
        byte[] blob = Encoding.UTF8.GetBytes("blob");
        byte[] manifest = SingleBlobManifest();
        Guid uploadId = Guid.NewGuid();
        string uploadLocation = $"/v2/destination/blobs/uploads/{uploadId}";
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(blob) });
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request => request.RequestUri!.Query.Contains("mount="),
            new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
            {
                Content = new StringContent("""{"errors":[]}""")
            });
        destinationHandler.AddExpectedRequest(
            HttpMethod.Post,
            "https://registry.example/v2/destination/blobs/uploads/",
            UploadInitializationResponse(uploadLocation));
        destinationHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Put &&
                request.RequestUri == new Uri(
                    $"https://registry.example{uploadLocation}?digest={Uri.EscapeDataString(BlobDigest)}"),
            BlobUploadResponse(BlobDigest));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("registry.example", sourceHandler);
        using var destinationClient = CreateClient("registry.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CopyAsync_MountAuthorizationFailure_StartsNormalUpload(
        HttpStatusCode mountStatusCode)
    {
        byte[] blob = Encoding.UTF8.GetBytes("blob");
        byte[] manifest = SingleBlobManifest();
        Guid uploadId = Guid.NewGuid();
        string uploadLocation = $"/v2/destination/blobs/uploads/{uploadId}";
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, manifest));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/source/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(blob) });
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/destination/blobs/{BlobDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request => request.RequestUri!.Query.Contains("mount="),
            new HttpResponseMessage(mountStatusCode)
            {
                Content = new StringContent("""{"errors":[]}""")
            });
        destinationHandler.AddExpectedRequest(
            HttpMethod.Post,
            "https://registry.example/v2/destination/blobs/uploads/",
            UploadInitializationResponse(uploadLocation));
        destinationHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Put &&
                request.RequestUri == new Uri(
                    $"https://registry.example{uploadLocation}?digest={Uri.EscapeDataString(BlobDigest)}") &&
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(blob),
            BlobUploadResponse(BlobDigest));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://registry.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("registry.example", sourceHandler);
        using var destinationClient = CreateDirectClient("registry.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_SubjectAndIndexReferenceSameManifest_CopiesItOnceBeforeRoot()
    {
        const string childDigest = "custom:child";
        const string rawMediaType = "application/vnd.example.manifest.v1+json";
        byte[] child = Encoding.UTF8.GetBytes("""{"opaque":true}""");
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","subject":{"mediaType":"{{rawMediaType}}","digest":"{{childDigest}}","size":15},"manifests":[{"mediaType":"{{rawMediaType}}","digest":"{{childDigest}}","size":15}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciIndexMediaType, RootDigest, root));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/manifests/{childDigest}",
            ManifestResponse(rawMediaType, childDigest, child));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/manifests/{childDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Put &&
                request.RequestUri == new Uri($"https://destination.example/v2/destination/manifests/{childDigest}") &&
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(child),
            PublishResponse(childDigest));
        HttpResponseMessage rootPublishResponse = PublishResponse(RootDigest);
        rootPublishResponse.Headers.Add("OCI-Subject", childDigest);
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://destination.example/v2/destination/manifests/promoted",
            rootPublishResponse);
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "promoted");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_ExistingChildManifest_StillCopiesItsMissingSubject()
    {
        const string childDigest = "custom:child";
        const string subjectDigest = "custom:subject";
        const string rawMediaType = "application/vnd.example.manifest.v1+json";
        byte[] subject = Encoding.UTF8.GetBytes("""{"subject":true}""");
        byte[] child = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","subject":{"mediaType":"{{rawMediaType}}","digest":"{{subjectDigest}}","size":16},"manifests":[]}
            """);
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","manifests":[{"mediaType":"{{OciIndexMediaType}}","digest":"{{childDigest}}","size":{{child.LongLength}}}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciIndexMediaType, RootDigest, root));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/manifests/{childDigest}",
            ManifestResponse(OciIndexMediaType, childDigest, child));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/manifests/{subjectDigest}",
            ManifestResponse(rawMediaType, subjectDigest, subject));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/manifests/{childDigest}",
            new HttpResponseMessage(HttpStatusCode.OK));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/manifests/{subjectDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Put &&
                request.RequestUri == new Uri(
                    $"https://destination.example/v2/destination/manifests/{subjectDigest}") &&
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(subject),
            PublishResponse(subjectDigest));
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://destination.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_DigestAlgorithmWithPlus_EscapesUploadQuery()
    {
        const string digest = "sha256+b64u:abc_DEF-123";
        byte[] blob = Encoding.UTF8.GetBytes("blob");
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciManifestMediaType}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"{{digest}}","size":4},"layers":[]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, root));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/blobs/{digest}",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(blob) });
        var destinationHandler = new MockHttpMessageHandler();
        AddBlobUpload(destinationHandler, digest, blob);
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://destination.example/v2/destination/repo/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        await sourceClient.CopyAsync(
            "source",
            "latest",
            destinationClient,
            "destination/repo",
            "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_PathTraversalManifestDigest_ThrowsBeforeDestinationRequest()
    {
        const string maliciousDigest = "../../../other/manifests/latest";
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","subject":{"mediaType":"application/vnd.example.manifest","digest":"{{maliciousDigest}}","size":1},"manifests":[]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciIndexMediaType, RootDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains(maliciousDigest, exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_PathTraversalBlobDigest_ThrowsBeforeDestinationRequest()
    {
        const string maliciousDigest = "../../../other/blobs/content";
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciManifestMediaType}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"{{maliciousDigest}}","size":1},"layers":[]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains(maliciousDigest, exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_MismatchedRootResponseDigest_ThrowsBeforeDestinationRequest()
    {
        const string childDigest =
            "sha256:1111111111111111111111111111111111111111111111111111111111111111";
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","manifests":[{"mediaType":"application/vnd.example.manifest","digest":"{{childDigest}}","size":1}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciIndexMediaType, childDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains(childDigest, exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_MismatchedDigestSourceReference_ThrowsBeforeDestinationRequest()
    {
        const string requestedDigest =
            "sha256:1111111111111111111111111111111111111111111111111111111111111111";
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","manifests":[]}
            """);
        string actualDigest = RegistryFixture.GetDigest(root);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/manifests/{requestedDigest}",
            ManifestResponse(OciIndexMediaType, actualDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                requestedDigest,
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains(requestedDigest, exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_MismatchedChildManifestDigest_ThrowsBeforePublishingChild()
    {
        byte[] expectedChild = Encoding.UTF8.GetBytes("""{"expected":true}""");
        byte[] returnedChild = Encoding.UTF8.GetBytes("""{"returned":true}""");
        string childDigest = RegistryFixture.GetDigest(expectedChild);
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciIndexMediaType}}","manifests":[{"mediaType":"application/vnd.example.manifest","digest":"{{childDigest}}","size":17}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciIndexMediaType, RootDigest, root));
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://source.example/v2/source/manifests/{childDigest}",
            ManifestResponse(
                "application/vnd.example.manifest",
                RegistryFixture.GetDigest(returnedChild),
                returnedChild));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/manifests/{childDigest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains(childDigest, exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_DockerManifestListWithNullManifests_PublishesRoot()
    {
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{ManifestMediaTypes.DockerManifestList}}","manifests":null}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(ManifestMediaTypes.DockerManifestList, RootDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://destination.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(ManifestMediaTypes.DockerManifestList)]
    [InlineData(ManifestMediaTypes.OciImageIndex1)]
    public async Task CopyAsync_ManifestListWithNullEntry_ThrowsValidationError(
        string mediaType)
    {
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{mediaType}}","manifests":[null]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(mediaType, RootDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains("null manifest descriptor", exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(ManifestMediaTypes.DockerManifestSchema2)]
    [InlineData(ManifestMediaTypes.OciManifestSchema1)]
    public async Task CopyAsync_ImageManifestWithNullLayers_PublishesRoot(string mediaType)
    {
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{mediaType}}","config":null,"layers":null}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(mediaType, RootDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://destination.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(ManifestMediaTypes.DockerManifestSchema2)]
    [InlineData(ManifestMediaTypes.OciManifestSchema1)]
    public async Task CopyAsync_ImageManifestWithNullLayerEntry_ThrowsValidationError(
        string mediaType)
    {
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{mediaType}}","config":null,"layers":[null]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(mediaType, RootDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sourceClient.CopyAsync(
                "source",
                "latest",
                destinationClient,
                "destination",
                "latest"));

        Assert.Contains("null layer descriptor", exception.Message);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("application/vnd.docker.image.rootfs.foreign.diff.tar.gzip")]
    [InlineData("application/vnd.oci.image.layer.nondistributable.v1.tar")]
    [InlineData("application/vnd.oci.image.layer.nondistributable.v1.tar+gzip")]
    [InlineData("application/vnd.oci.image.layer.nondistributable.v1.tar+zstd")]
    public async Task CopyAsync_NonDistributableLayer_PublishesManifestWithoutCopyingLayer(
        string layerMediaType)
    {
        byte[] root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciManifestMediaType}}","config":null,"layers":[{"mediaType":"{{layerMediaType}}","digest":"sha256:external","size":100,"urls":["https://cdn.example/layer"]}]}
            """);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/manifests/latest",
            ManifestResponse(OciManifestMediaType, RootDigest, root));
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            HttpMethod.Put,
            "https://destination.example/v2/destination/manifests/latest",
            PublishResponse(RootDigest));
        using var sourceClient = CreateClient("source.example", sourceHandler);
        using var destinationClient = CreateClient("destination.example", destinationHandler);

        await sourceClient.CopyAsync("source", "latest", destinationClient, "destination", "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyAsync_CanceledToken_ThrowsBeforeSendingRequest()
    {
        using var sourceClient = CreateClient("source.example", new MockHttpMessageHandler());
        using var destinationClient = CreateClient("destination.example", new MockHttpMessageHandler());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sourceClient.CopyAsync(
            "source",
            "latest",
            destinationClient,
            "destination",
            "latest",
            cancellationTokenSource.Token));
    }

    [Theory]
    [InlineData("", "latest", "destination", "latest", "sourceRepositoryName")]
    [InlineData("source", " ", "destination", "latest", "sourceReference")]
    [InlineData("source", "latest", "", "latest", "destinationRepositoryName")]
    [InlineData("source", "latest", "destination", " ", "destinationReference")]
    public async Task CopyAsync_InvalidStringArgument_Throws(
        string sourceRepository,
        string sourceReference,
        string destinationRepository,
        string destinationReference,
        string expectedParameterName)
    {
        using var sourceClient = CreateClient("source.example", new MockHttpMessageHandler());
        using var destinationClient = CreateClient("destination.example", new MockHttpMessageHandler());

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => sourceClient.CopyAsync(
            sourceRepository,
            sourceReference,
            destinationClient,
            destinationRepository,
            destinationReference));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    private static RegistryClient CreateClient(string registry, MockHttpMessageHandler handler) =>
        new(registry, serviceClientCredentials: null, handler);

    private static RegistryClient CreateDirectClient(string registry, HttpMessageHandler handler) =>
        new(
            registry,
            serviceClientCredentials: null,
            new HttpClient(handler),
            disposeHttpClient: true);

    private static byte[] SingleBlobManifest() =>
        Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{OciManifestMediaType}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"{{BlobDigest}}","size":4},"layers":[]}
            """);

    private static HttpResponseMessage ManifestResponse(
        string mediaType,
        string digest,
        byte[] content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        response.Headers.Add("Docker-Content-Digest", digest);
        return response;
    }

    private static void AddBlobUpload(
        MockHttpMessageHandler handler,
        string digest,
        byte[] expectedContent)
    {
        Guid uploadId = Guid.NewGuid();
        string uploadLocation = $"/v2/destination/repo/blobs/uploads/{uploadId}";
        handler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://destination.example/v2/destination/repo/blobs/{digest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        handler.AddExpectedRequest(
            HttpMethod.Post,
            "https://destination.example/v2/destination/repo/blobs/uploads/",
            UploadInitializationResponse(uploadLocation));
        handler.AddExpectedRequest(
            request =>
                request.Method == HttpMethod.Put &&
                request.RequestUri == new Uri(
                    $"https://destination.example{uploadLocation}?digest={Uri.EscapeDataString(digest)}") &&
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(expectedContent),
            BlobUploadResponse(digest));
    }

    private static HttpResponseMessage UploadInitializationResponse(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Location = new Uri(location, UriKind.Relative);
        return response;
    }

    private static HttpResponseMessage BlobUploadResponse(string digest)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri($"/v2/blobs/{digest}", UriKind.Relative);
        response.Headers.Add("Docker-Content-Digest", digest);
        return response;
    }

    private static HttpResponseMessage BlobExistsResponse(long size)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        };
        response.Content.Headers.ContentLength = size;
        return response;
    }

    private static HttpResponseMessage PublishResponse(string digest)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri($"/v2/manifests/{digest}", UriKind.Relative);
        response.Headers.Add("Docker-Content-Digest", digest);
        return response;
    }

    private static byte[] SerializeContent(HttpContent content)
    {
        using var stream = new MemoryStream();
        content.CopyToAsync(stream).GetAwaiter().GetResult();
        return stream.ToArray();
    }

    private sealed class GatedReadStream(byte[] buffer, Func<bool> canRead) : MemoryStream(buffer)
    {
        public override bool CanSeek => false;

        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureReadable();
            return base.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            EnsureReadable();
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            EnsureReadable();
            return base.ReadAsync(buffer, cancellationToken);
        }

        private void EnsureReadable()
        {
            if (!canRead())
            {
                throw new InvalidOperationException(
                    "The source blob was read before the destination upload started.");
            }
        }
    }

    private sealed class TrackingReadStream(byte[] buffer) : MemoryStream(buffer)
    {
        public long BytesRead { get; private set; }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            int bytesRead = Read(buffer, offset, count);
            return Task.FromResult(bytesRead);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = base.Read(buffer, offset, count);
            BytesRead += bytesRead;
            return bytesRead;
        }
    }
}
