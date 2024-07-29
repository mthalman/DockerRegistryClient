using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public class DockerManifestV1 : Manifest
{
    public DockerManifestV1()
    {
        SchemaVersion = 1;
    }

    /// <summary>
    /// Architecture is the host architecture on which this image is intended to run. This is for information purposes and not currently used by the engine.
    /// </summary>
    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    /// <summary>
    /// Name is the name of the image’s repository.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// History is a list of unstructured historical data for v1 compatibility. It contains ID of the image layer and ID of the layer’s parent layers.
    /// </summary>
    [JsonPropertyName("history")]
    public ManifestHistory[] History { get; set; } = Array.Empty<ManifestHistory>();

    /// <summary>
    /// Rag is the tag of the image.
    /// </summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    /// <summary>
    /// fsLayers is a list of filesystem layer blob sums contained in this image.
    /// </summary>
    [JsonPropertyName("fsLayers")]
    public FsLayer[] FsLayers { get; set; } = Array.Empty<FsLayer>();

    /// <summary>
    /// Signed manifests provides an envelope for a signed image manifest. A signed manifest consists of an image manifest along with an additional field containing the signature of the manifest.
    /// </summary>
    [JsonPropertyName("signatures")]
    public ManifestSignature[] Signatures { get; set; } = Array.Empty<ManifestSignature>();
}
