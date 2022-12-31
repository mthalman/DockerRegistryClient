namespace Valleysoft.DockerRegistryClient;

public class BlobUpload
{
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
