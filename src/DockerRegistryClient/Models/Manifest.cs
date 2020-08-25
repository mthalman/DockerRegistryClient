using Newtonsoft.Json;

namespace DockerRegistry.Models
{
    public abstract class Manifest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }
    }
}
