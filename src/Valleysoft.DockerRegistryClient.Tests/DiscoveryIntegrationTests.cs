using System.Text;
using Valleysoft.DockerRegistryClient.Models;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

[Collection(RegistryCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DiscoveryIntegrationTests
{
    private readonly RegistryFixture fixture;

    public DiscoveryIntegrationTests(RegistryFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task TagQueries_ExerciseDefaultCountExplicitCountAndAllPages()
    {
        string repository = fixture.GetRepositoryName(nameof(TagQueries_ExerciseDefaultCountExplicitCountAndAllPages));
        BlobSeed config = await fixture.UploadBlobAsync(repository, Encoding.UTF8.GetBytes("{}"));
        object manifest = CreateManifest(config);
        await fixture.PutManifestAsync(repository, "alpha", ManifestMediaTypes.OciManifestSchema1, manifest);
        await fixture.PutManifestAsync(repository, "beta", ManifestMediaTypes.OciManifestSchema1, manifest);
        await fixture.PutManifestAsync(repository, "gamma", ManifestMediaTypes.OciManifestSchema1, manifest);
        using RegistryClient client = fixture.CreateClient();

        Page<RepositoryTags> defaultPage = await client.Tags.GetAsync(repository);
        Assert.Equal(repository, defaultPage.Value.RepositoryName);
        Assert.Equal(["alpha", "beta", "gamma"], defaultPage.Value.Tags.Order());
        Assert.Null(defaultPage.NextPageLink);

        List<string> pagedTags = [];
        Page<RepositoryTags>? lastPage = null;
        await foreach (Page<RepositoryTags> page in client.Tags.GetAllPagesAsync(repository, count: 1))
        {
            Assert.Equal(repository, page.Value.RepositoryName);
            pagedTags.AddRange(page.Value.Tags);
            lastPage = page;
        }

        Assert.Equal(["alpha", "beta", "gamma"], pagedTags.Order());
        Assert.NotNull(lastPage);
        Assert.Empty(lastPage.Value.Tags);
        Assert.Null(lastPage.NextPageLink);
    }

    [Fact]
    public async Task CatalogQueries_ExerciseDefaultCountExplicitCountAndAllPages()
    {
        string prefix = $"integration/catalog-{Guid.NewGuid():N}";
        string[] repositories = [$"{prefix}-a", $"{prefix}-b", $"{prefix}-c"];
        foreach (string repository in repositories)
        {
            BlobSeed config = await fixture.UploadBlobAsync(repository, Encoding.UTF8.GetBytes("{}"));
            await fixture.PutManifestAsync(
                repository,
                "latest",
                ManifestMediaTypes.OciManifestSchema1,
                CreateManifest(config));
        }

        using RegistryClient client = fixture.CreateClient();
        Page<Catalog> defaultPage = await client.Catalog.GetAsync();
        Assert.All(repositories, repository => Assert.Contains(repository, defaultPage.Value.RepositoryNames));
        Assert.Null(defaultPage.NextPageLink);

        HashSet<string> allRepositories = [];
        await foreach (string repository in client.Catalog.GetAllAsync(count: 1))
        {
            allRepositories.Add(repository);
        }

        Assert.All(repositories, repository => Assert.Contains(repository, allRepositories));
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
}
