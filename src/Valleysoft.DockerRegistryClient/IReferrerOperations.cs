using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

namespace Valleysoft.DockerRegistryClient;

public interface IReferrerOperations
{
    /// <summary>
    /// Gets the list of referrers to the target digest.
    /// </summary>
    /// <param name="repositoryName">Name of repository.</param>
    /// <param name="digest">Digest of the target manifest.</param>
    /// <param name="artifactType">Artifact media type to filter by.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    Task<Page<OciImageIndex>> GetAsync(string repositoryName, string digest, string? artifactType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next page of results of the list of referrers.
    /// </summary>
    /// <param name="nextPageLink">Link URL contained from the previous <see cref="Page"/> result.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    Task<Page<OciImageIndex>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default);
}
