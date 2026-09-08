using System.Text.Json.Serialization;
namespace Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

/// <summary>
/// Describes content-addressable data in an OCI manifest.
/// </summary>
/// <remarks>
/// See the <see href="https://github.com/opencontainers/image-spec/blob/v1.0/descriptor.md">OCI Image Format Specification descriptor</see>.
/// </remarks>
public class OciDescriptor : IDescriptor
{
    /// <inheritdoc />
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets fallback URLs from which the referenced content may be downloaded.
    /// </summary>
    [JsonPropertyName("urls")]
    public string[] Urls { get; set; } = [];

    /// <summary>
    /// Gets or sets arbitrary metadata associated with the descriptor.
    /// </summary>
    [JsonPropertyName("annotations")]
    public Dictionary<string, string> Annotations { get; set; } = [];

    /// <summary>
    /// Gets or sets base64-encoded embedded content for the descriptor.
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets the media type of the artifact represented by the descriptor.
    /// </summary>
    [JsonPropertyName("artifactType")]
    public string? ArtifactType { get; set; }
}
