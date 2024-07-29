using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public class JsonWebKey
{
    [JsonPropertyName("crv")]
    public string? Crv { get; set; }

    [JsonPropertyName("kid")]
    public string? Kid { get; set; }

    [JsonPropertyName("kty")]
    public string? Kty { get; set; }

    [JsonPropertyName("x")]
    public string? X { get; set; }

    [JsonPropertyName("y")]
    public string? Y { get; set; }
}
