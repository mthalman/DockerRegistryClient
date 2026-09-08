namespace Valleysoft.DockerRegistryClient.Models.Manifests;

/// <summary>
/// Represents a manifest that selects platform-specific manifests.
/// </summary>
public interface IManifestList : IManifest
{
    /// <summary>
    /// Gets the referenced platform-specific manifests.
    /// </summary>
    IManifestReference[] Manifests { get; }
}
