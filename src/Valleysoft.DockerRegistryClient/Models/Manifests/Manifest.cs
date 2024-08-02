using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public abstract class Manifest : IManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>
    /// The MIME type of the manifest.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;
}
