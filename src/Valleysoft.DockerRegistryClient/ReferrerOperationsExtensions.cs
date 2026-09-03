using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Extension methods for traversing referrer results.
/// </summary>
public static class ReferrerOperationsExtensions
{
    /// <summary>
    /// Asynchronously enumerates all referrer pages, requesting each subsequent page as enumeration advances.
    /// </summary>
    /// <param name="operations">Provider of the referrer operations.</param>
    /// <param name="repositoryName">Name of the repository.</param>
    /// <param name="digest">Digest of the target manifest.</param>
    /// <param name="artifactType">Artifact media type to filter by.</param>
    /// <param name="cancellationToken">Propagates notification that enumeration should be canceled.</param>
    /// <returns>An asynchronous sequence of referrer pages.</returns>
    public static IAsyncEnumerable<Page<OciImageIndex>> GetAllPagesAsync(
        this IReferrerOperations operations,
        string repositoryName,
        string digest,
        string? artifactType = null,
        CancellationToken cancellationToken = default) =>
        PaginationHelper.GetPagesAsync(
            token => operations.GetAsync(repositoryName, digest, artifactType, token),
            operations.GetNextAsync,
            cancellationToken);

    /// <summary>
    /// Asynchronously enumerates all referrers, requesting pages as enumeration advances.
    /// </summary>
    /// <param name="operations">Provider of the referrer operations.</param>
    /// <param name="repositoryName">Name of the repository.</param>
    /// <param name="digest">Digest of the target manifest.</param>
    /// <param name="artifactType">Artifact media type to filter by.</param>
    /// <param name="cancellationToken">Propagates notification that enumeration should be canceled.</param>
    /// <returns>An asynchronous sequence of referrer manifest references.</returns>
    public static IAsyncEnumerable<ManifestReference> GetAllAsync(
        this IReferrerOperations operations,
        string repositoryName,
        string digest,
        string? artifactType = null,
        CancellationToken cancellationToken = default) =>
        PaginationHelper.GetItemsAsync(
            operations.GetAllPagesAsync(repositoryName, digest, artifactType, cancellationToken),
            index => index.Manifests,
            cancellationToken);
}
