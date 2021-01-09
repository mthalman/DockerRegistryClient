using Newtonsoft.Json;

namespace Valleysoft.DockerRegistry.Models
{
    public class JsonWebKey
    {
        [JsonProperty("crv")]
        public string? Crv { get; set; }

        [JsonProperty("kid")]
        public string? Kid { get; set; }

        [JsonProperty("kty")]
        public string? Kty { get; set; }

        [JsonProperty("x")]
        public string? X { get; set; }

        [JsonProperty("y")]
        public string? Y { get; set; }
    }
}
