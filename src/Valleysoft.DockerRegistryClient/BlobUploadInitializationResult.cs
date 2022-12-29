namespace Valleysoft.DockerRegistryClient;

public class BlobUploadInitializationResult
{
    public BlobUploadInitializationResult(string location, Guid uploadId, BlobUploadContext uploadContext)
    {
        Location = location;
        UploadId = uploadId;
        UploadContext = uploadContext;
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
    /// Gets the state associated with the initialization of the blob upload.
    /// </summary>
    public BlobUploadContext UploadContext { get; }
}
