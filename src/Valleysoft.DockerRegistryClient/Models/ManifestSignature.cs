using Newtonsoft.Json;

namespace Valleysoft.DockerRegistry.Models
{
    public class ManifestSignature
    {
        /// <summary>
        /// A JSON Web Signature (http://self-issued.info/docs/draft-ietf-jose-json-web-signature.html).
        /// </summary>
        [JsonProperty("header")]
        public SignatureHeader? Header { get; set; }

        /// <summary>
        /// A signature for the image manifest, signed by a libtrust private key.
        /// </summary>
        [JsonProperty("signature")]
        public string? Signature { get; set; }

        /// <summary>
        /// The signed protected header.
        /// </summary>
        [JsonProperty("protected")]
        public string? Protected { get; set; }
    }
}
