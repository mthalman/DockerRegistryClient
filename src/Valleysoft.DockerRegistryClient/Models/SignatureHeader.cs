using Newtonsoft.Json;

namespace Valleysoft.DockerRegistry.Models
{
    public class SignatureHeader
    {
        [JsonProperty("jwk")]
        public JsonWebKey? Jwk { get; set; }

        [JsonProperty("alg")]
        public string? Algorithm { get; set; }
    }
}
