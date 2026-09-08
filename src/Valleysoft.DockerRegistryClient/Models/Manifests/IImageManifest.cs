namespace Valleysoft.DockerRegistryClient.Models.Manifests;

/// <summary>
/// Represents an image manifest containing configuration and filesystem layer descriptors.
/// </summary>
public interface IImageManifest : IManifest
{
    /// <summary>
    /// Gets the image configuration descriptor.
    /// </summary>
    IDescriptor? Config { get; }

    /// <summary>
    /// Gets the filesystem layer descriptors in base-to-top order.
    /// </summary>
    IDescriptor[] Layers { get; }
}
