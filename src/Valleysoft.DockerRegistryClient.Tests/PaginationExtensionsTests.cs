using Moq;
using Valleysoft.DockerRegistryClient.Models;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class PaginationExtensionsTests
{
    [Fact]
    public async Task CatalogGetAllPagesAsync_ReturnsEveryPage()
    {
        var firstPage = new Page<Catalog>(
            new Catalog { RepositoryNames = ["repo1"] },
            "/v2/_catalog?n=1&last=repo1");
        var secondPage = new Page<Catalog>(
            new Catalog { RepositoryNames = ["repo2"] },
            nextPageLink: null);
        var operations = new Mock<ICatalogOperations>();
        operations
            .Setup(value => value.GetAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage);
        operations
            .Setup(value => value.GetNextAsync(firstPage.NextPageLink!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondPage);

        List<Page<Catalog>> pages = await CollectAsync(operations.Object.GetAllPagesAsync(count: 1));

        Assert.Equal([firstPage, secondPage], pages);
    }

    [Fact]
    public async Task CatalogGetAllAsync_FetchesPagesAsEnumerationAdvances()
    {
        var firstPage = new Page<Catalog>(
            new Catalog { RepositoryNames = ["repo1"] },
            "/v2/_catalog?n=1&last=repo1");
        var secondPage = new Page<Catalog>(
            new Catalog { RepositoryNames = ["repo2"] },
            nextPageLink: null);
        var operations = new Mock<ICatalogOperations>();
        operations
            .Setup(value => value.GetAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage);
        operations
            .Setup(value => value.GetNextAsync(firstPage.NextPageLink!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondPage);
        IAsyncEnumerable<string> repositories = operations.Object.GetAllAsync(count: 1);

        operations.Verify(
            value => value.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        await using IAsyncEnumerator<string> enumerator = repositories.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("repo1", enumerator.Current);
        operations.Verify(
            value => value.GetNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.True(await enumerator.MoveNextAsync());

        Assert.Equal("repo2", enumerator.Current);
        operations.Verify(
            value => value.GetNextAsync(firstPage.NextPageLink!, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task TagGetAllPagesAsync_ReturnsEveryPage()
    {
        var firstPage = new Page<RepositoryTags>(
            new RepositoryTags { RepositoryName = "repo", Tags = ["v1"] },
            "/v2/repo/tags/list?n=1&last=v1");
        var secondPage = new Page<RepositoryTags>(
            new RepositoryTags { RepositoryName = "repo", Tags = ["v2"] },
            nextPageLink: null);
        var operations = new Mock<ITagOperations>();
        operations
            .Setup(value => value.GetAsync("repo", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage);
        operations
            .Setup(value => value.GetNextAsync(firstPage.NextPageLink!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondPage);

        List<Page<RepositoryTags>> pages = await CollectAsync(
            operations.Object.GetAllPagesAsync("repo", count: 1));

        Assert.Equal([firstPage, secondPage], pages);
    }

    [Fact]
    public async Task TagGetAllAsync_ReturnsEveryTag()
    {
        var firstPage = new Page<RepositoryTags>(
            new RepositoryTags { RepositoryName = "repo", Tags = ["v1", "v2"] },
            "/v2/repo/tags/list?n=2&last=v2");
        var secondPage = new Page<RepositoryTags>(
            new RepositoryTags { RepositoryName = "repo", Tags = ["v3"] },
            nextPageLink: null);
        var operations = new Mock<ITagOperations>();
        operations
            .Setup(value => value.GetAsync("repo", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage);
        operations
            .Setup(value => value.GetNextAsync(firstPage.NextPageLink!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondPage);

        List<string> tags = await CollectAsync(operations.Object.GetAllAsync("repo", count: 2));

        Assert.Equal(["v1", "v2", "v3"], tags);
    }

    [Fact]
    public async Task ReferrerGetAllPagesAsync_ReturnsEveryPage()
    {
        const string ArtifactType = "application/spdx+json";
        var firstPage = new Page<OciImageIndex>(
            new OciImageIndex { Manifests = [new ManifestReference { Digest = "sha256:1" }] },
            "/v2/repo/referrers/sha256:subject?last=sha256%3A1");
        var secondPage = new Page<OciImageIndex>(
            new OciImageIndex { Manifests = [new ManifestReference { Digest = "sha256:2" }] },
            nextPageLink: null);
        var operations = new Mock<IReferrerOperations>();
        operations
            .Setup(value => value.GetAsync(
                "repo",
                "sha256:subject",
                ArtifactType,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage);
        operations
            .Setup(value => value.GetNextAsync(firstPage.NextPageLink!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondPage);

        List<Page<OciImageIndex>> pages = await CollectAsync(
            operations.Object.GetAllPagesAsync(
                "repo",
                "sha256:subject",
                artifactType: ArtifactType));

        Assert.Equal([firstPage, secondPage], pages);
    }

    [Fact]
    public async Task ReferrerGetAllAsync_ReturnsEveryManifestReference()
    {
        var firstManifest = new ManifestReference { Digest = "sha256:1" };
        var secondManifest = new ManifestReference { Digest = "sha256:2" };
        var firstPage = new Page<OciImageIndex>(
            new OciImageIndex { Manifests = [firstManifest] },
            "/v2/repo/referrers/sha256:subject?last=sha256%3A1");
        var secondPage = new Page<OciImageIndex>(
            new OciImageIndex { Manifests = [secondManifest] },
            nextPageLink: null);
        var operations = new Mock<IReferrerOperations>();
        operations
            .Setup(value => value.GetAsync(
                "repo",
                "sha256:subject",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage);
        operations
            .Setup(value => value.GetNextAsync(firstPage.NextPageLink!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondPage);

        List<ManifestReference> manifests = await CollectAsync(
            operations.Object.GetAllAsync("repo", "sha256:subject"));

        Assert.Equal([firstManifest, secondManifest], manifests);
    }

    [Fact]
    public async Task GetAllPagesAsync_PassesCancellationTokenToEveryRequest()
    {
        using var cancellationSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationSource.Token;
        var firstPage = new Page<Catalog>(
            new Catalog { RepositoryNames = ["repo1"] },
            "/v2/_catalog?n=1&last=repo1");
        var operations = new Mock<ICatalogOperations>();
        operations
            .Setup(value => value.GetAsync(null, cancellationToken))
            .ReturnsAsync(firstPage);
        operations
            .Setup(value => value.GetNextAsync(firstPage.NextPageLink!, cancellationToken))
            .ReturnsAsync(new Page<Catalog>(new Catalog(), nextPageLink: null));

        await CollectAsync(operations.Object.GetAllPagesAsync(cancellationToken: cancellationToken));

        operations.Verify(value => value.GetAsync(null, cancellationToken), Times.Once);
        operations.Verify(
            value => value.GetNextAsync(firstPage.NextPageLink!, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_CancellationStopsEnumerationWithinCurrentPage()
    {
        using var cancellationSource = new CancellationTokenSource();
        var operations = new Mock<ICatalogOperations>();
        operations
            .Setup(value => value.GetAsync(null, cancellationSource.Token))
            .ReturnsAsync(new Page<Catalog>(
                new Catalog { RepositoryNames = ["repo1", "repo2"] },
                "/v2/_catalog?n=2&last=repo2"));
        await using IAsyncEnumerator<string> enumerator = operations.Object
            .GetAllAsync(cancellationToken: cancellationSource.Token)
            .GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumerator.MoveNextAsync().AsTask());
        operations.Verify(
            value => value.GetNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_PropagatesRegistryException()
    {
        var expectedException = new RegistryException("Catalog page not found.");
        var operations = new Mock<ICatalogOperations>();
        operations
            .Setup(value => value.GetAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        await using IAsyncEnumerator<string> enumerator = operations.Object
            .GetAllAsync()
            .GetAsyncEnumerator();

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => enumerator.MoveNextAsync().AsTask());

        Assert.Same(expectedException, exception);
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> values)
    {
        List<T> results = [];
        await foreach (T value in values)
        {
            results.Add(value);
        }

        return results;
    }
}
