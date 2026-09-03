using System.Text;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

[Collection(RegistryCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ReferrerIntegrationTests
{
    private readonly RegistryFixture fixture;

    public ReferrerIntegrationTests(RegistryFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task ReferrerQueries_FallBackToReferrersTag()
    {
        const string SbomType = "application/spdx+json";
        const string SignatureType = "application/vnd.example.signature";
        string repository = fixture.GetRepositoryName(nameof(ReferrerQueries_FallBackToReferrersTag));
        BlobSeed config = await fixture.UploadBlobAsync(repository, Encoding.UTF8.GetBytes("{}"));
        ManifestSeed subject = await fixture.PutManifestAsync(
            repository,
            "subject",
            ManifestMediaTypes.OciManifestSchema1,
            CreateManifest(config));
        using RegistryClient client = fixture.CreateClient();

        Page<Models.Manifests.Oci.OciImageIndex> empty = await client.Referrers.GetAsync(
            repository,
            subject.Digest);
        Assert.Empty(empty.Value.Manifests);
        Assert.Equal(2, empty.Value.SchemaVersion);
        Assert.Equal(ManifestMediaTypes.OciImageIndex1, empty.Value.MediaType);
        Assert.Null(empty.NextPageLink);

        ManifestSeed sbom = await fixture.PutManifestAsync(
            repository,
            "sbom",
            ManifestMediaTypes.OciManifestSchema1,
            CreateArtifact(config, subject, SbomType, "sbom"));
        ManifestSeed signature = await fixture.PutManifestAsync(
            repository,
            "signature",
            ManifestMediaTypes.OciManifestSchema1,
            CreateArtifact(config, subject, SignatureType, "signature"));
        await fixture.PutManifestAsync(
            repository,
            subject.Digest.Replace(':', '-'),
            ManifestMediaTypes.OciImageIndex1,
            new
            {
                schemaVersion = 2,
                mediaType = ManifestMediaTypes.OciImageIndex1,
                manifests = new[]
                {
                    CreateDescriptor(sbom, SbomType, "sbom"),
                    CreateDescriptor(signature, SignatureType, "signature")
                }
            });

        List<Models.Manifests.Oci.ManifestReference> all = [];
        await foreach (Models.Manifests.Oci.ManifestReference manifestReference in
            client.Referrers.GetAllAsync(repository, subject.Digest))
        {
            all.Add(manifestReference);
        }

        Assert.Equal(2, all.Count);
        Assert.Contains(all, descriptor =>
            descriptor.Digest == sbom.Digest &&
            descriptor.ArtifactType == SbomType &&
            descriptor.Annotations["name"] == "sbom");
        Assert.Contains(all, descriptor =>
            descriptor.Digest == signature.Digest &&
            descriptor.ArtifactType == SignatureType &&
            descriptor.Annotations["name"] == "signature");

        List<Models.Manifests.Oci.ManifestReference> filtered = [];
        await foreach (Models.Manifests.Oci.ManifestReference manifestReference in
            client.Referrers.GetAllAsync(repository, subject.Digest, SbomType))
        {
            filtered.Add(manifestReference);
        }

        Models.Manifests.Oci.ManifestReference filteredDescriptor = Assert.Single(filtered);
        Assert.Equal(sbom.Digest, filteredDescriptor.Digest);
        Assert.Equal(SbomType, filteredDescriptor.ArtifactType);
    }

    private static object CreateManifest(BlobSeed config) =>
        new
        {
            schemaVersion = 2,
            mediaType = ManifestMediaTypes.OciManifestSchema1,
            config = new
            {
                mediaType = "application/vnd.oci.image.config.v1+json",
                size = config.Size,
                digest = config.Digest
            },
            layers = Array.Empty<object>()
        };

    private static object CreateArtifact(
        BlobSeed config,
        ManifestSeed subject,
        string artifactType,
        string name) =>
        new
        {
            schemaVersion = 2,
            mediaType = ManifestMediaTypes.OciManifestSchema1,
            artifactType,
            config = new
            {
                mediaType = "application/vnd.oci.empty.v1+json",
                size = config.Size,
                digest = config.Digest
            },
            layers = Array.Empty<object>(),
            subject = new
            {
                mediaType = ManifestMediaTypes.OciManifestSchema1,
                size = subject.Size,
                digest = subject.Digest
            },
            annotations = new Dictionary<string, string> { ["name"] = name }
        };

    private static object CreateDescriptor(
        ManifestSeed manifest,
        string artifactType,
        string name) =>
        new
        {
            mediaType = ManifestMediaTypes.OciManifestSchema1,
            size = manifest.Size,
            digest = manifest.Digest,
            artifactType,
            annotations = new Dictionary<string, string> { ["name"] = name }
        };
}
