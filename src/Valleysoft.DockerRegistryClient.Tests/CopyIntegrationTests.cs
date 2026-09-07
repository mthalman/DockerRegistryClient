using System.Text;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

[Collection(RegistryCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CopyIntegrationTests
{
    private readonly RegistryFixture fixture;

    public CopyIntegrationTests(RegistryFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task CopyAsync_CopiesIndexSubjectManifestsAndBlobsBetweenRepositories()
    {
        string sourceRepository = fixture.GetRepositoryName(nameof(CopyAsync_CopiesIndexSubjectManifestsAndBlobsBetweenRepositories));
        string destinationRepository = fixture.GetRepositoryName(
            $"{nameof(CopyAsync_CopiesIndexSubjectManifestsAndBlobsBetweenRepositories)}-destination");
        BlobSeed config = await fixture.UploadBlobAsync(sourceRepository, Encoding.UTF8.GetBytes("{}"));
        BlobSeed layer = await fixture.UploadBlobAsync(sourceRepository, Encoding.UTF8.GetBytes("artifact-layer"));
        await fixture.UploadBlobAsync(destinationRepository, Encoding.UTF8.GetBytes("{}"));

        ManifestSeed subject = await fixture.PutManifestAsync(
            sourceRepository,
            "subject",
            ManifestMediaTypes.OciManifestSchema1,
            new
            {
                schemaVersion = 2,
                mediaType = ManifestMediaTypes.OciManifestSchema1,
                config = Descriptor("application/vnd.oci.image.config.v1+json", config),
                layers = Array.Empty<object>()
            });
        ManifestSeed artifact = await fixture.PutManifestAsync(
            sourceRepository,
            "artifact",
            ManifestMediaTypes.OciManifestSchema1,
            new
            {
                schemaVersion = 2,
                mediaType = ManifestMediaTypes.OciManifestSchema1,
                artifactType = "application/vnd.example.artifact",
                config = Descriptor("application/vnd.oci.empty.v1+json", config),
                layers = new[]
                {
                    Descriptor("application/vnd.example.artifact.layer.v1", layer)
                },
                subject = Descriptor(ManifestMediaTypes.OciManifestSchema1, subject)
            });
        ManifestSeed index = await fixture.PutManifestAsync(
            sourceRepository,
            "source-tag",
            ManifestMediaTypes.OciImageIndex1,
            new
            {
                schemaVersion = 2,
                mediaType = ManifestMediaTypes.OciImageIndex1,
                manifests = new[]
                {
                    Descriptor(ManifestMediaTypes.OciManifestSchema1, artifact)
                },
                subject = Descriptor(ManifestMediaTypes.OciManifestSchema1, subject)
            });
        using RegistryClient sourceClient = fixture.CreateClient();
        using RegistryClient destinationClient = fixture.CreateClient();

        ManifestPublishResult result = await sourceClient.CopyAsync(
            sourceRepository,
            "source-tag",
            destinationClient,
            destinationRepository,
            "destination-tag");

        Assert.Equal(index.Digest, result.Digest);
        Assert.True(await destinationClient.Manifests.ExistsAsync(destinationRepository, subject.Digest));
        Assert.True(await destinationClient.Manifests.ExistsAsync(destinationRepository, artifact.Digest));
        Assert.True(await destinationClient.Blobs.ExistsAsync(destinationRepository, config.Digest));
        Assert.True(await destinationClient.Blobs.ExistsAsync(destinationRepository, layer.Digest));
        ManifestInfo copied = await destinationClient.Manifests.GetAsync(
            destinationRepository,
            "destination-tag");
        Assert.Equal(index.Digest, copied.DockerContentDigest);
        Assert.Equal(index.Json, Encoding.UTF8.GetString(copied.Content.Span));
    }

    private static object Descriptor(string mediaType, BlobSeed blob) =>
        new
        {
            mediaType,
            digest = blob.Digest,
            size = blob.Size
        };

    private static object Descriptor(string mediaType, ManifestSeed manifest) =>
        new
        {
            mediaType,
            digest = manifest.Digest,
            size = manifest.Size
        };
}
