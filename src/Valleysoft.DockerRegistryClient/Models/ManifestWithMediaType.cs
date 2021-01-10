using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models
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
