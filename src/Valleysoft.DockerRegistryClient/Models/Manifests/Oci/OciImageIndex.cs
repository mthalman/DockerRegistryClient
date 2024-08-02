using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

public class OciImageIndex : Manifest, IManifestList
{
    public OciImageIndex()
    {
        MediaType = ManifestMediaTypes.OciImageIndex1;
    }

    /// <summary>
    /// The manifests field contains a list of manifests for specific platforms.
    /// </summary>
    [JsonPropertyName("manifests")]
    public ManifestReference[] Manifests { get; set; } = [];

    IManifestReference[] IManifestList.Manifests => Manifests;

    [JsonPropertyName("annotations")]
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
