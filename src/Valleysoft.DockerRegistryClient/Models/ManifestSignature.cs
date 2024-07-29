using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public class ManifestSignature
{
    /// <summary>
    /// A JSON Web Signature (http://self-issued.info/docs/draft-ietf-jose-json-web-signature.html).
    /// </summary>
    [JsonPropertyName("header")]
    public SignatureHeader? Header { get; set; }

    /// <summary>
    /// A signature for the image manifest, signed by a libtrust private key.
    /// </summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    /// <summary>
    /// The signed protected header.
    /// </summary>
    [JsonPropertyName("protected")]
    public string? Protected { get; set; }
}
