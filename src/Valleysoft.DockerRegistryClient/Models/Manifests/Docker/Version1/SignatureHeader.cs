using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests.Docker.Version1;

public class SignatureHeader
{
    [JsonPropertyName("jwk")]
    public JsonWebKey? Jwk { get; set; }

    [JsonPropertyName("alg")]
    public string? Algorithm { get; set; }
}
