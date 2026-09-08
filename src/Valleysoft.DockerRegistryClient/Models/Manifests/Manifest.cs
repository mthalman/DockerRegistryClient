using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests;

/// <summary>
/// Provides the common schema and media type fields for registry manifests.
/// </summary>
public abstract class Manifest : IManifest
{
    /// <summary>
    /// Gets or sets the manifest schema version.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>
    /// The MIME type of the manifest.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;
}
