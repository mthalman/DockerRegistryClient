namespace Valleysoft.DockerRegistryClient.Models.Manifests;

/// <summary>
/// Represents a registry manifest.
/// </summary>
public interface IManifest
{
    /// <summary>
    /// Gets the manifest media type.
    /// </summary>
    string? MediaType { get; }
}
