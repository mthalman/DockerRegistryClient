using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public class Catalog
{
    [JsonPropertyName("repositories")]
    public List<string> RepositoryNames { get; set; } = new List<string>();
}
