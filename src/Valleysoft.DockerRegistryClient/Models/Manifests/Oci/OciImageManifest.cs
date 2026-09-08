using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

/// <summary>
/// Represents an OCI image or artifact manifest.
/// </summary>
/// <remarks>
/// See the <see href="https://github.com/opencontainers/image-spec/blob/v1.0/manifest.md">OCI Image Format Specification manifest</see>.
/// </remarks>
public class OciImageManifest : Manifest, IImageManifest
{
    /// <summary>
    /// Initializes an OCI image manifest.
    /// </summary>
    public OciImageManifest()
    {
        MediaType = ManifestMediaTypes.OciManifestSchema1;
        SchemaVersion = 2;
    }

    /// <summary>
    /// Gets or sets the media type of the artifact represented by this manifest.
    /// </summary>
    [JsonPropertyName("artifactType")]
    public string? ArtifactType { get; set; }

    /// <summary>
    /// Gets or sets the image configuration descriptor.
    /// </summary>
    [JsonPropertyName("config")]
    public OciDescriptor Config { get; set; } = new();

    IDescriptor? IImageManifest.Config => Config;

    /// <summary>
    /// Gets or sets the filesystem or artifact layer descriptors.
    /// </summary>
    [JsonPropertyName("layers")]
    public OciDescriptor[] Layers { get; set; } = [];

    IDescriptor[] IImageManifest.Layers => Layers;

    /// <summary>
    /// Gets or sets the descriptor of the manifest to which this artifact refers.
    /// </summary>
    [JsonPropertyName("subject")]
    public OciDescriptor? Subject { get; set; }

    /// <summary>
    /// Gets or sets arbitrary metadata associated with the manifest.
    /// </summary>
    [JsonPropertyName("annotations")]
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
