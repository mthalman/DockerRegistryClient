namespace Valleysoft.DockerRegistryClient.Models.Manifests;

/// <summary>
/// Describes a manifest and the platform on which it can run.
/// </summary>
public interface IManifestReference : IDescriptor
{
    /// <summary>
    /// Gets the target platform, or <see langword="null"/> when no platform is specified.
    /// </summary>
    ManifestPlatform? Platform { get; }
}
