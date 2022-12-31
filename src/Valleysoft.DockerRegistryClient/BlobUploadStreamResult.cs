namespace Valleysoft.DockerRegistryClient;

public class BlobUploadStreamResult
{
    public BlobUploadStreamResult(string location, Guid uploadId, long rangeOffset)
    {
        Location = location;
        UploadId = uploadId;
        RangeOffset = rangeOffset;
    }

    /// <summary>
    /// Gets the relative URL of the registry where the blob is located.
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
