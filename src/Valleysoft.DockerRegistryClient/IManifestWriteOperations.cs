namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Provides manifest publishing and deletion capabilities.
/// </summary>
public interface IManifestWriteOperations : IManifestOperations
{
    /// <summary>
    /// Publishes the exact manifest content under a tag or digest reference.
    /// </summary>
    /// <param name="repositoryName">Name of the target repository.</param>
    /// <param name="tagOrDigest">Tag or digest reference for the manifest.</param>
    /// <param name="content">Manifest content to publish.</param>
    /// <param name="mediaType">Media type of the manifest content.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    Task<ManifestPublishResult> PublishAsync(
        string repositoryName,
        string tagOrDigest,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the manifest identified by a digest.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the manifest.</param>
    /// <param name="digest">Digest of the manifest. This operation does not accept tags.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    Task DeleteAsync(string repositoryName, string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tag association without deleting the referenced manifest.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the tag.</param>
    /// <param name="tag">Tag to delete. This operation does not accept digests.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// Tag deletion is optional in the OCI Distribution specification. Registries that do not support it may reject the request.
    /// </remarks>
    Task DeleteTagAsync(string repositoryName, string tag, CancellationToken cancellationToken = default);
}
