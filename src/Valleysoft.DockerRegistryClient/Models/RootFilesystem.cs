using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// References the layer content addresses used by the image.
/// </summary>
public class RootFilesystem
{
    /// <summary>
    /// Type of the content.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    // <summary>
    /// An array of layer content hashes, in order from first to last.
    /// </summary>
    [JsonProperty("diff_ids")]
    public string[] DiffIds { get; set; } = Array.Empty<string>();
}
