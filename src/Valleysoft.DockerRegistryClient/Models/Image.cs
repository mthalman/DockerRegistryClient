using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// An image is an ordered collection of root filesystem changes and the corresponding execution parameters for use within a container runtime.
/// </summary>
public class Image
{
    /// <summary>
    /// An combined date and time at which the image was created, formatted as defined by RFC 3339, section 5.6.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    /// <summary>
    /// Gives the name and/or email address of the person or entity which created and is responsible for maintaining the image.
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>
    /// The CPU architecture which the binaries in this image are built to run on.
    /// </summary>
    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// The name of the operating system which the image is built to run on.
    /// </summary>
    [JsonPropertyName("os")]
    public string Os { get; set; } = string.Empty;

    /// <summary>
    /// Specifies the version of the operating system targeted by the referenced blob.
    /// </summary>
    [JsonPropertyName("os.version")]
    public string? OsVersion { get; set; }

    /// <summary>
    /// This OPTIONAL property specifies an array of strings, each specifying a mandatory OS feature
    /// </summary>
    [JsonPropertyName("os.features")]
    public string[] OsFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The variant of the specified CPU architecture.
    /// </summary>
    [JsonPropertyName("variant")]
    public string? Variant { get; set; }

    /// <summary>
    /// The execution parameters which should be used as a base when running a container using the image.
    /// </summary>
    [JsonPropertyName("config")]
    public ImageConfig? Config { get; set; }

    /// <summary>
    /// References the layer content addresses used by the image.
    /// </summary>
    [JsonPropertyName("rootfs")]
    public RootFilesystem RootFilesystem { get; set; } = new RootFilesystem();

    /// <summary>
    /// Describes the history of each layer. The array is ordered from first to last.
    /// </summary>
    [JsonPropertyName("history")]
    public LayerHistory[] History { get; set; } = Array.Empty<LayerHistory>();
}
