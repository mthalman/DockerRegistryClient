using System.Text.Json;
using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;
 
public class Error
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("detail")]
    public JsonElement? Detail { get; set; }
}
