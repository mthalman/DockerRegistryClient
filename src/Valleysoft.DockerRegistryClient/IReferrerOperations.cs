using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Provides operations for discovering OCI artifacts that refer to a manifest.
/// </summary>
public interface IReferrerOperations
{
    /// <summary>
    /// Gets the list of referrers to the target digest.
    /// </summary>
    /// <param name="repositoryName">Name of repository.</param>
    /// <param name="digest">Digest of the target manifest.</param>
    /// <param name="artifactType">Artifact media type to filter by.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>A page containing an OCI image index of matching referrers.</returns>
    Task<Page<OciImageIndex>> GetAsync(string repositoryName, string digest, string? artifactType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next page of results of the list of referrers.
    /// </summary>
    /// <param name="nextPageLink">Link URL contained in the previous <see cref="Page{T}"/> result.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The requested page of referrers.</returns>
    Task<Page<OciImageIndex>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default);
}
