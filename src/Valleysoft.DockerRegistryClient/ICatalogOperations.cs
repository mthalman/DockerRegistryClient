using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Provides operations for listing repositories in a registry catalog.
/// </summary>
public interface ICatalogOperations
{
    /// <summary>
    /// Gets the first page of repository names.
    /// </summary>
    /// <param name="count">Maximum number of repositories requested, or <see langword="null"/> to use the registry default.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The first catalog page and its continuation link, if any.</returns>
    Task<Page<Catalog>> GetAsync(int? count = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the page identified by a continuation link from a previous catalog response.
    /// </summary>
    /// <param name="nextPageLink">Continuation URL from <see cref="Page{T}.NextPageLink"/>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The requested catalog page and its continuation link, if any.</returns>
    Task<Page<Catalog>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default);
}
