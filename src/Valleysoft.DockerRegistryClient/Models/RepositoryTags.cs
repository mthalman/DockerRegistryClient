using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

// https://github.com/opencontainers/distribution-spec/blob/main/spec.md#listing-tags
public class RepositoryTags
{
    [JsonPropertyName("name")]
    public string? RepositoryName { get; set; }

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}
