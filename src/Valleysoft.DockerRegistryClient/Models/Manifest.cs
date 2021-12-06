using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

public abstract class Manifest
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; }
}
