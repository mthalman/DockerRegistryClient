using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Provides operations for listing a repository's tags.
/// </summary>
public interface ITagOperations
{
    /// <summary>
    /// Gets the first page of tags for a repository.
    /// </summary>
    /// <param name="repositoryName">Name of the repository.</param>
    /// <param name="count">Maximum number of tags requested, or <see langword="null"/> to use the registry default.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The first tag page and its continuation link, if any.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="repositoryName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="repositoryName"/> is invalid.</exception>
    Task<Page<RepositoryTags>> GetAsync(
        string repositoryName, int? count = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the page identified by a continuation link from a previous tag response.
    /// </summary>
    /// <param name="nextPageLink">Continuation URL from <see cref="Page{T}.NextPageLink"/>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The requested tag page and its continuation link, if any.</returns>
    Task<Page<RepositoryTags>> GetNextAsync(
        string nextPageLink, CancellationToken cancellationToken = default);
}
