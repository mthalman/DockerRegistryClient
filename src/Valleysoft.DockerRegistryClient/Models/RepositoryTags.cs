using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;
 
public class RepositoryTags
{
    [JsonPropertyName("name")]
    public string? RepositoryName { get; set; }

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}
