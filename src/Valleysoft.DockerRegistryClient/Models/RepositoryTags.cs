using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

// https://github.com/opencontainers/distribution-spec/blob/main/spec.md#listing-tags
public class RepositoryTags
{
    private string[] tags = Array.Empty<string>();

    [JsonPropertyName("name")]
    public string? RepositoryName { get; set; }

    [JsonPropertyName("tags")]
    public string[] Tags
    {
        get => tags;
        set => tags = value ?? Array.Empty<string>();
    }
}
