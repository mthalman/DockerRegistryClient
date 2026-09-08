namespace Valleysoft.DockerRegistryClient.Models.Manifests;

/// <summary>
/// Describes content-addressable data referenced by a manifest.
/// </summary>
public interface IDescriptor
{
    /// <summary>
    /// Gets the media type of the referenced content.
    /// </summary>
    string MediaType { get; }

    /// <summary>
    /// Gets the expected size of the referenced content in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Gets the content digest used to identify and verify the referenced content.
    /// </summary>
    string Digest { get; }
}
