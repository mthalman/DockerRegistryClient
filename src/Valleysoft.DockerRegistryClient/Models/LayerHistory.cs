using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

public class LayerHistory
{
    /// <summary>
    /// A combined date and time at which the layer was created.
    /// </summary>
    [JsonProperty("created")]
    public DateTime? Created { get; set; }

    /// <summary>
    /// The command which created the layer.
    /// </summary>
    [JsonProperty("created_by")]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// The author of the build point.
    /// </summary>
    [JsonProperty("author")]
    public string? Author { get; set; }

    /// <summary>
    /// A custom message set when creating the layer.
    /// </summary>
    [JsonProperty("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// This field is used to mark if the history item created a filesystem diff. It is set to true if this history item doesn't correspond to an actual layer in the rootfs section.
    /// </summary>
    [JsonProperty("empty_layer")]
    public bool IsEmptyLayer { get; set; }
}
