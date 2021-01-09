using Newtonsoft.Json;

namespace Valleysoft.DockerRegistry.Models
{
    public abstract class ManifestWithMediaType : Manifest
    {
        /// <summary>
        /// The MIME type of the manifest.
        /// </summary>
        [JsonProperty("mediaType")]
        public string? MediaType { get; set; }
    }
}
