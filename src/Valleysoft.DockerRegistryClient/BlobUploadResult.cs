namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Describes a completed blob upload.
/// </summary>
public class BlobUploadResult
{
    /// <summary>
    /// Initializes a completed blob upload result.
    /// </summary>
    /// <param name="location">Absolute or relative blob URL returned by the registry.</param>
    /// <param name="digest">Canonical digest of the uploaded blob.</param>
    public BlobUploadResult(string location, string digest)
    {
        Location = location;
        Digest = digest;
    }

    /// <summary>
    /// Gets the absolute or relative location URL returned by the registry.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets the digest of the uploaded blob.
    /// </summary>
    public string Digest { get; }
}
