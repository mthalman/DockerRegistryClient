namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Describes the current state of an in-progress blob upload.
/// </summary>
public class BlobUpload
{
    /// <summary>
    /// Initializes blob upload state.
    /// </summary>
    /// <param name="uploadId">Identifier assigned to the upload by the registry.</param>
    /// <param name="rangeOffset">Zero-based inclusive offset of the bytes already uploaded.</param>
    public BlobUpload(Guid uploadId, long rangeOffset)
    {
        UploadId = uploadId;
        RangeOffset = rangeOffset;
    }

    /// <summary>
    /// Gets the offset from 0 of the inclusive range of bytes that have been uploaded.
    /// </summary>
    public long RangeOffset { get; }

    /// <summary>
    /// Gets the identifier of the blob upload.
    /// </summary>
    public Guid UploadId { get; }
}
