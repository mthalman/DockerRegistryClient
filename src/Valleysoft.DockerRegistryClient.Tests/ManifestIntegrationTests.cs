using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Docker;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;
using Xunit;
using DockerManifestReference = Valleysoft.DockerRegistryClient.Models.Manifests.Docker.ManifestReference;
using OciManifestReference = Valleysoft.DockerRegistryClient.Models.Manifests.Oci.ManifestReference;

namespace Valleysoft.DockerRegistryClient.Tests;

[Collection(RegistryCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ManifestIntegrationTests
{
    private readonly RegistryFixture fixture;

    public ManifestIntegrationTests(RegistryFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task GetAsync_ReturnsEverySupportedManifestShape()
    {
        string repository = fixture.GetRepositoryName(nameof(GetAsync_ReturnsEverySupportedManifestShape));
        BlobSeed config = await fixture.UploadBlobAsync(repository, Encoding.UTF8.GetBytes("{}"));
        BlobSeed layer = await fixture.UploadBlobAsync(repository, Encoding.UTF8.GetBytes("layer"));

        object dockerManifestBody = new
        {
            schemaVersion = 2,
            mediaType = ManifestMediaTypes.DockerManifestSchema2,
            config = new
            {
                mediaType = "application/vnd.docker.container.image.v1+json",
                size = config.Size,
                digest = config.Digest
            },
            layers = new[]
            {
                new
                {
                    mediaType = "application/vnd.docker.image.rootfs.diff.tar.gzip",
                    size = layer.Size,
                    digest = layer.Digest,
                    urls = Array.Empty<string>()
                }
            }
        };
        ManifestSeed dockerManifest = await fixture.PutManifestAsync(
            repository,
            "docker",
            ManifestMediaTypes.DockerManifestSchema2,
            dockerManifestBody);

        object ociManifestBody = new
        {
            schemaVersion = 2,
            mediaType = ManifestMediaTypes.OciManifestSchema1,
            artifactType = "application/vnd.example.image",
            config = new
            {
                mediaType = "application/vnd.oci.image.config.v1+json",
                size = config.Size,
                digest = config.Digest
            },
            layers = new[]
            {
                new
                {
                    mediaType = "application/vnd.oci.image.layer.v1.tar",
                    size = layer.Size,
                    digest = layer.Digest,
                    annotations = new Dictionary<string, string> { ["kind"] = "test" }
                }
            },
            annotations = new Dictionary<string, string> { ["org.opencontainers.image.title"] = "integration" }
        };
        ManifestSeed ociManifest = await fixture.PutManifestAsync(
            repository,
            "oci",
            ManifestMediaTypes.OciManifestSchema1,
            ociManifestBody);

        ManifestSeed dockerList = await fixture.PutManifestAsync(
            repository,
            "docker-list",
            ManifestMediaTypes.DockerManifestList,
            new
            {
                schemaVersion = 2,
                mediaType = ManifestMediaTypes.DockerManifestList,
                manifests = new[]
                {
                    new
                    {
                        mediaType = ManifestMediaTypes.DockerManifestSchema2,
                        size = dockerManifest.Size,
                        digest = dockerManifest.Digest,
                        platform = new
                        {
                            architecture = "amd64",
                            os = "linux",
                            features = new[] { "sse4" }
                        }
                    }
                }
            });

        ManifestSeed ociIndex = await fixture.PutManifestAsync(
            repository,
            "oci-index",
            ManifestMediaTypes.OciImageIndex1,
            new
            {
                schemaVersion = 2,
                mediaType = ManifestMediaTypes.OciImageIndex1,
                manifests = new[]
                {
                    new
                    {
                        mediaType = ManifestMediaTypes.OciManifestSchema1,
                        size = ociManifest.Size,
                        digest = ociManifest.Digest,
                        platform = new
                        {
                            architecture = "arm64",
                            os = "linux",
                            variant = "v8"
                        },
                        annotations = new Dictionary<string, string> { ["channel"] = "stable" }
                    }
                },
                annotations = new Dictionary<string, string> { ["index"] = "oci" }
            });

        using RegistryClient client = fixture.CreateClient();

        ManifestInfo dockerInfo = await client.Manifests.GetAsync(repository, "docker");
        Assert.Equal(ManifestMediaTypes.DockerManifestSchema2, dockerInfo.MediaType);
        Assert.Equal(dockerManifest.Digest, dockerInfo.DockerContentDigest);
        DockerManifest docker = Assert.IsType<DockerManifest>(dockerInfo.Manifest);
        Assert.Equal(config.Digest, docker.Config!.Digest);
        Assert.Equal(layer.Digest, Assert.Single(docker.Layers).Digest);
        Assert.Equal(dockerManifest.Json, Encoding.UTF8.GetString(dockerInfo.Content.Span));

        ManifestInfo ociInfo = await client.Manifests.GetAsync(repository, ociManifest.Digest);
        Assert.Equal(ManifestMediaTypes.OciManifestSchema1, ociInfo.MediaType);
        Assert.Equal(ociManifest.Digest, ociInfo.DockerContentDigest);
        OciImageManifest oci = Assert.IsType<OciImageManifest>(ociInfo.Manifest);
        Assert.Equal("application/vnd.example.image", oci.ArtifactType);
        Assert.Equal("integration", oci.Annotations["org.opencontainers.image.title"]);
        Assert.Equal("test", Assert.Single(oci.Layers).Annotations["kind"]);

        ManifestInfo dockerListInfo = await client.Manifests.GetAsync(repository, dockerList.Digest);
        ManifestList list = Assert.IsType<ManifestList>(dockerListInfo.Manifest);
        DockerManifestReference dockerReference = Assert.Single(list.Manifests);
        Assert.Equal(dockerManifest.Digest, dockerReference.Digest);
        Assert.Equal("amd64", dockerReference.Platform!.Architecture);
        Assert.Equal(["sse4"], dockerReference.Platform.Features);

        ManifestInfo ociIndexInfo = await client.Manifests.GetAsync(repository, "oci-index");
        OciImageIndex index = Assert.IsType<OciImageIndex>(ociIndexInfo.Manifest);
        Assert.Equal("oci", index.Annotations["index"]);
        OciManifestReference ociReference = Assert.Single(index.Manifests);
        Assert.Equal(ociManifest.Digest, ociReference.Digest);
        Assert.Equal("arm64", ociReference.Platform!.Architecture);
        Assert.Equal("v8", ociReference.Platform.Variant);
        Assert.Equal("stable", ociReference.Annotations["channel"]);
        Assert.Equal(ociIndex.Digest, ociIndexInfo.DockerContentDigest);

        const string customMediaType = "application/vnd.example.manifest.v1+json";
        using RegistryClient rawClient = fixture.CreateClient(new ManifestMediaTypeHandler(customMediaType));
        ManifestInfo customInfo = await rawClient.Manifests.GetAsync(repository, "oci");
        Assert.Equal(customMediaType, customInfo.MediaType);
        Assert.Equal(ociManifest.Digest, customInfo.DockerContentDigest);
        RawManifest rawManifest = Assert.IsType<RawManifest>(customInfo.Manifest);
        Assert.Equal(ociManifest.Json, Encoding.UTF8.GetString(rawManifest.Content.Span));
        Assert.Equal(ociManifest.Json, Encoding.UTF8.GetString(customInfo.Content.Span));
    }

    [Fact]
    public async Task ManifestQueries_ExerciseTagDigestExistenceAndMissingResults()
    {
        string repository = fixture.GetRepositoryName(nameof(ManifestQueries_ExerciseTagDigestExistenceAndMissingResults));
        BlobSeed config = await fixture.UploadBlobAsync(repository, Encoding.UTF8.GetBytes("{}"));
        ManifestSeed manifest = await fixture.PutManifestAsync(
            repository,
            "latest",
            ManifestMediaTypes.OciManifestSchema1,
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
            });
        using RegistryClient client = fixture.CreateClient();

        Assert.True(await client.Manifests.ExistsAsync(repository, "latest"));
        Assert.True(await client.Manifests.ExistsAsync(repository, manifest.Digest));
        Assert.False(await client.Manifests.ExistsAsync(repository, "missing"));
        Assert.Equal(manifest.Digest, await client.Manifests.GetDigestAsync(repository, "latest"));
        Assert.Equal(manifest.Digest, await client.Manifests.GetDigestAsync(repository, manifest.Digest));

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Manifests.GetAsync(repository, "missing"));
        Assert.Equal("Manifest not found.", exception.Message);
        RegistryException innerException = Assert.IsType<RegistryException>(exception.InnerException);
        Assert.Equal(HttpStatusCode.NotFound, innerException.StatusCode);
        Assert.Contains(innerException.Errors, error => error.Code == "MANIFEST_UNKNOWN");
    }

    private sealed class ManifestMediaTypeHandler(string mediaType) : DelegatingHandler(new HttpClientHandler())
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode &&
                request.Method == HttpMethod.Get &&
                request.RequestUri?.AbsolutePath.Contains("/manifests/") == true)
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            }

            return response;
        }
    }
}
