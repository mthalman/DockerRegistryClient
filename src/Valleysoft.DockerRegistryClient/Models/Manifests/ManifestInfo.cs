namespace Valleysoft.DockerRegistryClient.Models.Manifests;

/// <summary>
/// Contains a manifest together with registry response metadata and its original serialized content.
/// </summary>
public class ManifestInfo
{
    /// <summary>
    /// Initializes manifest information without original serialized content.
    /// </summary>
    /// <param name="mediaType">Media type reported by the registry.</param>
    /// <param name="dockerContentDigest">Digest identifying the manifest.</param>
    /// <param name="manifest">Parsed manifest.</param>
    public ManifestInfo(string mediaType, string dockerContentDigest, IManifest manifest)
        : this(mediaType, dockerContentDigest, manifest, ReadOnlyMemory<byte>.Empty)
    {
    }

    /// <summary>
    /// Initializes manifest information.
    /// </summary>
    /// <param name="mediaType">Media type reported by the registry.</param>
    /// <param name="dockerContentDigest">Digest identifying the manifest.</param>
    /// <param name="manifest">Parsed manifest.</param>
    /// <param name="content">Original serialized manifest bytes.</param>
    public ManifestInfo(
        string mediaType,
        string dockerContentDigest,
        IManifest manifest,
        ReadOnlyMemory<byte> content)
    {
        MediaType = mediaType;
        DockerContentDigest = dockerContentDigest;
        Manifest = manifest;
        Content = content;
    }

    /// <summary>
    /// Gets the media type reported by the registry.
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// Gets the registry's digest, or the requested digest or a SHA-256 digest of the original content when the response omits it.
    /// </summary>
    public string DockerContentDigest { get; }

    /// <summary>
    /// Gets the parsed manifest.
    /// </summary>
    public IManifest Manifest { get; }

    /// <summary>
    /// Gets the original serialized manifest bytes, or an empty value when they were not supplied.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }
}
