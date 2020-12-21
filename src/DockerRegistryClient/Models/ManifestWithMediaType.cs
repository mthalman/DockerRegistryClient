using Newtonsoft.Json;

namespace DockerRegistry.Models
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
