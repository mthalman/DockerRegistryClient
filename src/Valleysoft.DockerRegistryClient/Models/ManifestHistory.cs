using Newtonsoft.Json;

namespace Valleysoft.DockerRegistry.Models
{
    public class ManifestHistory
    {
        /// <summary>
        /// V1Compatibility is the raw V1 compatibility information. This will contain the JSON object describing the V1 of this image.
        /// </summary>
        [JsonProperty("v1Compatibility")]
        public string? V1Compatibility { get; set; }
    }
}
