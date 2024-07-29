using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public abstract class Manifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }
}
