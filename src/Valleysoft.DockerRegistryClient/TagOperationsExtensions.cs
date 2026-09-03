using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Extension methods for traversing repository tag results.
/// </summary>
public static class TagOperationsExtensions
{
    /// <summary>
    /// Asynchronously enumerates all tag pages, requesting each subsequent page as enumeration advances.
    /// </summary>
    /// <param name="operations">Provider of the tag operations.</param>
    /// <param name="repositoryName">Name of the repository.</param>
    /// <param name="count">Maximum number of tags requested per page.</param>
    /// <param name="cancellationToken">Propagates notification that enumeration should be canceled.</param>
    /// <returns>An asynchronous sequence of repository tag pages.</returns>
    public static IAsyncEnumerable<Page<RepositoryTags>> GetAllPagesAsync(
        this ITagOperations operations,
        string repositoryName,
        int? count = null,
        CancellationToken cancellationToken = default) =>
        PaginationHelper.GetPagesAsync(
            token => operations.GetAsync(repositoryName, count, token),
            operations.GetNextAsync,
            cancellationToken);

    /// <summary>
    /// Asynchronously enumerates all tags, requesting pages as enumeration advances.
    /// </summary>
    /// <param name="operations">Provider of the tag operations.</param>
    /// <param name="repositoryName">Name of the repository.</param>
    /// <param name="count">Maximum number of tags requested per page.</param>
    /// <param name="cancellationToken">Propagates notification that enumeration should be canceled.</param>
    /// <returns>An asynchronous sequence of tags.</returns>
    public static IAsyncEnumerable<string> GetAllAsync(
        this ITagOperations operations,
        string repositoryName,
        int? count = null,
        CancellationToken cancellationToken = default) =>
        PaginationHelper.GetItemsAsync(
            operations.GetAllPagesAsync(repositoryName, count, cancellationToken),
            repositoryTags => repositoryTags.Tags,
            cancellationToken);
}
