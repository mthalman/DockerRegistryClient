using System;
using Newtonsoft.Json;

namespace DockerRegistry.Models
{
    /// <summary>
    /// The image manifest provides a configuration and a set of layers for a container image. It’s the direct replacement for the schema-1 manifest.
    /// </summary>
    public class Manifest_Schema2 : ManifestWithMediaType
    {
        public Manifest_Schema2()
        {
            MediaType = ManifestMediaTypes.ManifestSchema2;
            SchemaVersion = 2;
        }

        /// <summary>
        /// The config field references a configuration object for a container, by digest. This configuration item is a JSON blob that the runtime uses to set up the container. This new schema uses a tweaked version of this configuration to allow image content-addressability on the daemon side.
        /// </summary>
        [JsonProperty("config")]
        public ManifestConfig? Config { get; set; }

        /// <summary>
        /// The layer list is ordered starting from the base image (opposite order of schema1).
        /// </summary>
        [JsonProperty("layers")]
        public ManifestLayer[] Layers { get; set; } = Array.Empty<ManifestLayer>();
    }
}
