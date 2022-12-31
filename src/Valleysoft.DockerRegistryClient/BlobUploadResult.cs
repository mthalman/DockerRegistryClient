namespace Valleysoft.DockerRegistryClient;

public class BlobUploadResult
{
    public BlobUploadResult(string location, string digest)
    {
        Location = location;
        Digest = digest;
    }

    /// <summary>
    /// Gets the relative URL of the registry where the blob is located.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets the digest of the uploaded blob.
    /// </summary>
    public string Digest { get; }
}
