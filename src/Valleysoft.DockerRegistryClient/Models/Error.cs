using System.Text.Json;
using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

// https://github.com/opencontainers/distribution-spec/blob/main/spec.md#error-codes
public class Error
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("detail")]
    public JsonElement? Detail { get; set; }
}
