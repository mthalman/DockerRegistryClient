using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public abstract class Manifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }
}
