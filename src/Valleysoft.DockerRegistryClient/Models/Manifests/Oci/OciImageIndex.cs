using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

/// <summary>
/// Represents an OCI image index or artifact index.
/// </summary>
public class OciImageIndex : Manifest, IManifestList
{
    private ManifestReference[] manifests = [];
    private IDictionary<string, string> annotations = new Dictionary<string, string>();

    /// <summary>
    /// Initializes an OCI image index.
    /// </summary>
    public OciImageIndex()
    {
        MediaType = ManifestMediaTypes.OciImageIndex1;
        SchemaVersion = 2;
    }

    /// <summary>
    /// The manifests field contains a list of manifests for specific platforms.
    /// </summary>
    [JsonPropertyName("manifests")]
    public ManifestReference[] Manifests
    {
        get => manifests;
        set => manifests = value ?? [];
    }

    IManifestReference[] IManifestList.Manifests => Manifests;

    /// <summary>
    /// Gets or sets the media type of the artifact represented by this index.
    /// </summary>
    [JsonPropertyName("artifactType")]
    public string? ArtifactType { get; set; }

    /// <summary>
    /// Gets or sets the descriptor of the manifest to which this artifact index refers.
    /// </summary>
    [JsonPropertyName("subject")]
    public OciDescriptor? Subject { get; set; }

    /// <summary>
    /// Gets or sets arbitrary metadata associated with the index. A <see langword="null"/> JSON value is normalized to an empty dictionary.
    /// </summary>
    [JsonPropertyName("annotations")]
    public IDictionary<string, string> Annotations
    {
        get => annotations;
        set => annotations = value ?? new Dictionary<string, string>();
    }
}
