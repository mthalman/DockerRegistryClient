using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models.Images;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Extension methods for the <see cref="IBlobOperations"/> interface.
/// </summary>
public static class BlobOperationsExtensions
{
    /// <summary>
    /// Returns the object model of a blob that represents an image config (e.g. mediaType of application/vnd.docker.container.image.v1+json or application/vnd.oci.image.config.v1+json).
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <exception cref="JsonException">Unable to deserialize the data to the object model.</exception>
    public static async Task<Image> GetImageAsync(this IBlobOperations operations, string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        using Stream blob = await operations.GetAsync(repositoryName, digest, cancellationToken);
        using StreamReader reader = new(blob);
        string content = await reader.ReadToEndAsync();
        const string ErrorMessage = "The result could not be deserialized into an image model. Verify the digest represents an image config and not a layer.";
        try
        {
            return JsonSerializer.Deserialize<Image>(content) ?? throw new JsonException(ErrorMessage);
        }
        catch (JsonException e)
        {
            throw new JsonException(ErrorMessage, e);
        }
    }

    /// <summary>
    /// Uploads a blob to the registry.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="repositoryName">Name of the repository to upload the blob to.</param>
    /// <param name="stream">Data stream to upload as the blob.</param>
    /// <param name="digest">Digest of the data stream (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// This is a convenience method that uses the more primitive <see cref="IBlobOperations.BeginUploadAsync"/> and <see cref="IBlobOperations.EndUploadAsync"/> methods.
    /// </remarks>
    public static async Task<BlobUploadResult> UploadAsync(this IBlobOperations operations, string repositoryName, Stream stream, string digest, CancellationToken cancellationToken = default)
    {
        BlobUploadInitializationResult result = await operations.BeginUploadAsync(repositoryName, cancellationToken).ConfigureAwait(false);
        return await operations.EndUploadAsync(result.Location, digest, result.UploadContext, stream, cancellationToken).ConfigureAwait(false);
    }
}
