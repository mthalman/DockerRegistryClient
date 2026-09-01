using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

public class OciImageIndex : Manifest, IManifestList
{
    private ManifestReference[] manifests = [];
    private IDictionary<string, string> annotations = new Dictionary<string, string>();

    public OciImageIndex()
    {
        MediaType = ManifestMediaTypes.OciImageIndex1;
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

    [JsonPropertyName("annotations")]
    public IDictionary<string, string> Annotations
    {
        get => annotations;
        set => annotations = value ?? new Dictionary<string, string>();
    }
}
