using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Image;

public class LayerHistory
{
    /// <summary>
    /// A combined date and time at which the layer was created.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    /// <summary>
    /// The command which created the layer.
    /// </summary>
    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// The author of the build point.
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>
    /// A custom message set when creating the layer.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// This field is used to mark if the history item created a filesystem diff. It is set to true if this history item doesn't correspond to an actual layer in the rootfs section.
    /// </summary>
    [JsonPropertyName("empty_layer")]
    public bool IsEmptyLayer { get; set; }
}
