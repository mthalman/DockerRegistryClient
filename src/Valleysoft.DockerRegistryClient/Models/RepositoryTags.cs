using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// Represents the tags returned for a repository.
/// </summary>
/// <remarks>
/// See the <see href="https://github.com/opencontainers/distribution-spec/blob/main/spec.md#listing-tags">OCI Distribution Specification tag listing</see>.
/// </remarks>
public class RepositoryTags
{
    private string[] tags = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the repository name returned by the registry.
    /// </summary>
    [JsonPropertyName("name")]
    public string? RepositoryName { get; set; }

    /// <summary>
    /// Gets or sets the repository's tags. A <see langword="null"/> JSON value is normalized to an empty array.
    /// </summary>
    [JsonPropertyName("tags")]
    public string[] Tags
    {
        get => tags;
        set => tags = value ?? Array.Empty<string>();
    }
}
