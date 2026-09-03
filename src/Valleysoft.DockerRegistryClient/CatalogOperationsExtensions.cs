using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Extension methods for traversing catalog results.
/// </summary>
public static class CatalogOperationsExtensions
{
    /// <summary>
    /// Asynchronously enumerates all catalog pages, requesting each subsequent page as enumeration advances.
    /// </summary>
    /// <param name="operations">Provider of the catalog operations.</param>
    /// <param name="count">Maximum number of repositories requested per page.</param>
    /// <param name="cancellationToken">Propagates notification that enumeration should be canceled.</param>
    /// <returns>An asynchronous sequence of catalog pages.</returns>
    public static IAsyncEnumerable<Page<Catalog>> GetAllPagesAsync(
        this ICatalogOperations operations,
        int? count = null,
        CancellationToken cancellationToken = default) =>
        PaginationHelper.GetPagesAsync(
            token => operations.GetAsync(count, token),
            operations.GetNextAsync,
            cancellationToken);

    /// <summary>
    /// Asynchronously enumerates all repository names, requesting pages as enumeration advances.
    /// </summary>
    /// <param name="operations">Provider of the catalog operations.</param>
    /// <param name="count">Maximum number of repositories requested per page.</param>
    /// <param name="cancellationToken">Propagates notification that enumeration should be canceled.</param>
    /// <returns>An asynchronous sequence of repository names.</returns>
    public static IAsyncEnumerable<string> GetAllAsync(
        this ICatalogOperations operations,
        int? count = null,
        CancellationToken cancellationToken = default) =>
        PaginationHelper.GetItemsAsync(
            operations.GetAllPagesAsync(count, cancellationToken),
            catalog => catalog.RepositoryNames,
            cancellationToken);
}
