namespace Valleysoft.DockerRegistryClient;

public class ManifestPublishResult
{
    public ManifestPublishResult(string location, string? digest)
    {
        Location = location;
        Digest = digest;
    }

    /// <summary>
    /// Gets the manifest location returned by the registry.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets the canonical digest returned by the registry, or <see langword="null"/> if the registry omitted it.
    /// </summary>
    public string? Digest { get; }
}
