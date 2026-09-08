namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Contains the state returned when a blob upload is started.
/// </summary>
public class BlobUploadInitializationResult
{
    /// <summary>
    /// Initializes a blob upload result.
    /// </summary>
    /// <param name="location">Absolute or relative upload URL returned by the registry.</param>
    /// <param name="uploadId">Identifier assigned to the upload by the registry.</param>
    /// <param name="uploadContext">Authentication state required by subsequent upload requests.</param>
    public BlobUploadInitializationResult(string location, Guid uploadId, BlobUploadContext uploadContext)
    {
        Location = location;
        UploadId = uploadId;
        UploadContext = uploadContext;
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
    /// Gets the state associated with the initialization of the blob upload.
    /// </summary>
    public BlobUploadContext UploadContext { get; }
}
