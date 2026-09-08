using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// Represents the repository names returned by the registry catalog endpoint.
/// </summary>
public class Catalog
{
    /// <summary>
    /// Gets or sets the repository names in this catalog page.
    /// </summary>
    [JsonPropertyName("repositories")]
    public List<string> RepositoryNames { get; set; } = new List<string>();
}
