using Newtonsoft.Json;

namespace Valleysoft.DockerRegistry.Models
{
    public abstract class Manifest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }
    }
}
