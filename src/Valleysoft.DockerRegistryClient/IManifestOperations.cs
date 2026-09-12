using Valleysoft.DockerRegistryClient.Models.Manifests;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Provides operations for retrieving and inspecting image manifests.
/// </summary>
public interface IManifestOperations
{
    /// <summary>
    /// Gets a manifest by tag or digest.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the manifest.</param>
    /// <param name="tagOrDigest">Manifest tag or digest.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The parsed manifest together with its media type, digest, and original content.</returns>
    /// <exception cref="ArgumentNullException">A required repository name or reference is null.</exception>
    /// <exception cref="ArgumentException">The repository name, tag, or digest is invalid.</exception>
    Task<ManifestInfo> GetAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a manifest identified by a tag or digest exists.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the manifest.</param>
    /// <param name="digest">Manifest tag or digest.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns><see langword="true"/> when the registry returns a successful response; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">A required repository name or reference is null.</exception>
    /// <exception cref="ArgumentException">The repository name, tag, or digest is invalid.</exception>
    Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the canonical digest for a manifest.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the manifest.</param>
    /// <param name="tagOrDigest">Manifest tag or digest.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The digest from the registry's <c>Docker-Content-Digest</c> response header.</returns>
    /// <exception cref="ArgumentNullException">A required repository name or reference is null.</exception>
    /// <exception cref="ArgumentException">The repository name, tag, or digest is invalid.</exception>
    Task<string> GetDigestAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
}
