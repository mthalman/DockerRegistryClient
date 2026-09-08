namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Describes a published manifest.
/// </summary>
public class ManifestPublishResult
{
    /// <summary>
    /// Initializes a manifest publication result.
    /// </summary>
    /// <param name="location">Manifest location returned by the registry.</param>
    /// <param name="digest">Canonical digest returned by the registry, or <see langword="null"/> when omitted.</param>
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
