using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models.Manifests;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Extension methods for the <see cref="IManifestOperations"/> interface.
/// </summary>
public static class ManifestOperationsExtensions
{
    /// <summary>
    /// Publishes the exact manifest content under a tag or digest reference.
    /// </summary>
    /// <param name="operations">Provider of the manifest operations.</param>
    /// <param name="repositoryName">Name of the target repository.</param>
    /// <param name="tagOrDigest">Tag or digest reference for the manifest.</param>
    /// <param name="content">Manifest content to publish.</param>
    /// <param name="mediaType">Media type of the manifest content.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public static Task<ManifestPublishResult> PublishAsync(
        this IManifestOperations operations,
        string repositoryName,
        string tagOrDigest,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken = default) =>
        GetWriteOperations(operations).PublishAsync(
            repositoryName,
            tagOrDigest,
            content,
            mediaType,
            cancellationToken);

    /// <summary>
    /// Serializes and publishes a manifest under a tag or digest reference.
    /// </summary>
    /// <param name="operations">Provider of the manifest operations.</param>
    /// <param name="repositoryName">Name of the target repository.</param>
    /// <param name="tagOrDigest">Tag or digest reference for the manifest.</param>
    /// <param name="manifest">Manifest to publish.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// A <see cref="RawManifest"/> is published without changing its content. Other manifests are serialized using their runtime type.
    /// </remarks>
    public static Task<ManifestPublishResult> PublishAsync(
        this IManifestOperations operations,
        string repositoryName,
        string tagOrDigest,
        IManifest manifest,
        CancellationToken cancellationToken = default)
    {
        if (operations is null)
        {
            throw new ArgumentNullException(nameof(operations));
        }

        if (manifest is null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        RegistryReferenceValidator.ValidateRepository(repositoryName, nameof(repositoryName));
        RegistryReferenceValidator.ValidateReference(tagOrDigest, nameof(tagOrDigest));
        string mediaType = manifest.MediaType ??
            throw new ArgumentException("The manifest media type must be set.", nameof(manifest));
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException("The manifest media type must be set.", nameof(manifest));
        }

        ReadOnlyMemory<byte> content = manifest is RawManifest rawManifest
            ? rawManifest.Content
            : JsonSerializer.SerializeToUtf8Bytes(manifest, manifest.GetType());

        return operations.PublishAsync(
            repositoryName,
            tagOrDigest,
            content,
            mediaType,
            cancellationToken);
    }

    /// <summary>
    /// Deletes the manifest identified by a digest.
    /// </summary>
    /// <param name="operations">Provider of the manifest operations.</param>
    /// <param name="repositoryName">Name of the repository containing the manifest.</param>
    /// <param name="digest">Digest of the manifest. This operation does not accept tags.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public static Task DeleteAsync(
        this IManifestOperations operations,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default) =>
        GetWriteOperations(operations).DeleteAsync(repositoryName, digest, cancellationToken);

    /// <summary>
    /// Deletes a tag association without deleting the referenced manifest.
    /// </summary>
    /// <param name="operations">Provider of the manifest operations.</param>
    /// <param name="repositoryName">Name of the repository containing the tag.</param>
    /// <param name="tag">Tag to delete. This operation does not accept digests.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// Tag deletion is optional in the OCI Distribution specification. Registries that do not support it may reject the request.
    /// </remarks>
    public static Task DeleteTagAsync(
        this IManifestOperations operations,
        string repositoryName,
        string tag,
        CancellationToken cancellationToken = default) =>
        GetWriteOperations(operations).DeleteTagAsync(repositoryName, tag, cancellationToken);

    private static IManifestWriteOperations GetWriteOperations(IManifestOperations operations)
    {
        if (operations is null)
        {
            throw new ArgumentNullException(nameof(operations));
        }

        return operations as IManifestWriteOperations ??
            throw new NotSupportedException(
                $"The {operations.GetType().FullName} implementation does not support manifest write operations.");
    }
}
