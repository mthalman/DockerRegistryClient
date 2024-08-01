using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

// https://docker-docs.uclv.cu/registry/spec/api/#listing-repositories
public class Catalog
{
    [JsonPropertyName("repositories")]
    public List<string> RepositoryNames { get; set; } = new List<string>();
}
