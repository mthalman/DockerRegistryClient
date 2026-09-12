namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Provides blob download, upload, and deletion operations for a registry.
/// </summary>
public interface IBlobOperations
{
    /// <summary>
    /// Downloads a blob.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the blob.</param>
    /// <param name="digest">Blob digest, such as <c>sha256:&lt;value&gt;</c>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>A readable stream whose disposal releases the underlying HTTP response.</returns>
    /// <exception cref="ArgumentNullException">A required repository name or digest is null.</exception>
    /// <exception cref="ArgumentException">The repository name or digest is invalid.</exception>
    Task<Stream> GetAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a range of bytes from a blob.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the blob.</param>
    /// <param name="digest">Blob digest, such as <c>sha256:&lt;value&gt;</c>.</param>
    /// <param name="offset">Zero-based starting offset.</param>
    /// <param name="length">Number of bytes to request, or <see langword="null"/> for all remaining bytes.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The response stream and range metadata reported by the registry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is negative, <paramref name="length"/> is not positive,
    /// or the requested range exceeds the maximum supported offset.
    /// </exception>
    /// <exception cref="ArgumentNullException">A required repository name or digest is null.</exception>
    /// <exception cref="ArgumentException">The repository name or digest is invalid.</exception>
    Task<BlobDownloadResult> GetRangeAsync(
        string repositoryName, string digest, long offset, long? length = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a blob exists.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the blob.</param>
    /// <param name="digest">Blob digest, such as <c>sha256:&lt;value&gt;</c>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns><see langword="true"/> when the registry returns a successful response; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">A required repository name or digest is null.</exception>
    /// <exception cref="ArgumentException">The repository name or digest is invalid.</exception>
    Task<bool> ExistsAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob.
    /// </summary>
    /// <param name="repositoryName">Name of the repository containing the blob.</param>
    /// <param name="digest">Blob digest, such as <c>sha256:&lt;value&gt;</c>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <exception cref="ArgumentNullException">A required repository name or digest is null.</exception>
    /// <exception cref="ArgumentException">The repository name or digest is invalid.</exception>
    Task DeleteAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of an in-progress blob upload.
    /// </summary>
    /// <param name="uploadLocation">Upload URL returned by a previous upload operation.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The registry's current upload identifier and offset.</returns>
    Task<BlobUpload> GetUploadAsync(
        string uploadLocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an in-progress blob upload.
    /// </summary>
    /// <param name="uploadLocation">Upload URL returned by a previous upload operation.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    Task DeleteUploadAsync(
        string uploadLocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a resumable blob upload.
    /// </summary>
    /// <param name="repositoryName">Name of the target repository.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>State required to continue the upload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="repositoryName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="repositoryName"/> is invalid.</exception>
    Task<BlobUploadInitializationResult> BeginUploadAsync(
        string repositoryName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chunk of data to an in-progress blob upload.
    /// </summary>
    /// <param name="uploadLocation">Upload URL returned by a previous upload operation.</param>
    /// <param name="stream">Readable stream containing the next chunk.</param>
    /// <param name="uploadContext">Authentication state returned when the upload was initialized.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>Updated upload state, including the next upload location and accepted offset.</returns>
    Task<BlobUploadStreamResult> SendUploadStreamAsync(
        string uploadLocation, Stream stream, BlobUploadContext uploadContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes an in-progress blob upload.
    /// </summary>
    /// <param name="uploadLocation">Upload URL returned by a previous upload operation.</param>
    /// <param name="digest">Expected digest of the complete blob.</param>
    /// <param name="uploadContext">Authentication state returned when the upload was initialized.</param>
    /// <param name="stream">Optional final chunk of blob data.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The location and canonical digest of the uploaded blob.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="digest"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="digest"/> is invalid.</exception>
    Task<BlobUploadResult> EndUploadAsync(
        string uploadLocation, string digest, BlobUploadContext uploadContext, Stream? stream = null, CancellationToken cancellationToken = default);
}
