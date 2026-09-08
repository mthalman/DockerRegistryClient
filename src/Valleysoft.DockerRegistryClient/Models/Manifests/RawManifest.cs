namespace Valleysoft.DockerRegistryClient.Models.Manifests;

/// <summary>
/// Represents serialized manifest content that can be published without reserialization.
/// </summary>
public sealed class RawManifest : IManifest
{
    /// <summary>
    /// Initializes a raw manifest.
    /// </summary>
    /// <param name="mediaType">Media type of the serialized content.</param>
    /// <param name="content">Exact manifest bytes.</param>
    public RawManifest(string mediaType, ReadOnlyMemory<byte> content)
    {
        MediaType = mediaType;
        Content = content;
    }

    /// <inheritdoc />
    public string MediaType { get; }

    /// <summary>
    /// Gets the exact serialized manifest bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }
}
