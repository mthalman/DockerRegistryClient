namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Contains the state returned after a chunk of blob data is uploaded.
/// </summary>
public class BlobUploadStreamResult
{
    /// <summary>
    /// Initializes a blob upload stream result.
    /// </summary>
    /// <param name="location">Absolute or relative upload URL returned by the registry.</param>
    /// <param name="uploadId">Identifier assigned to the upload by the registry.</param>
    /// <param name="rangeOffset">Zero-based inclusive offset of the bytes accepted by the registry.</param>
    public BlobUploadStreamResult(string location, Guid uploadId, long rangeOffset)
    {
        Location = location;
        UploadId = uploadId;
        RangeOffset = rangeOffset;
    }

    /// <summary>
    /// Gets the absolute or relative upload URL returned by the registry. Pass this value unchanged to subsequent upload operations.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets the identifier of the blob upload.
    /// </summary>
    public Guid UploadId { get; }

    /// <summary>
    /// Gets the offset from 0 of the inclusive range of bytes that have been uploaded.
    /// </summary>
    public long RangeOffset { get; }
}
