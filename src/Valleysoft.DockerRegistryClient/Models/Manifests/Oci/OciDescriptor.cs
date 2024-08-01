using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifest.Oci;

// https://github.com/opencontainers/image-spec/blob/v1.0/descriptor.md
public class OciDescriptor
{
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("urls")]
    public string[] Urls { get; set; } = [];

    [JsonPropertyName("annotations")]
    public Dictionary<string, string> Annotations { get; set; } = [];

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}
