using System.Text.Json.Serialization;
using Valleysoft.DockerRegistryClient.Models.Manifests;

namespace Valleysoft.DockerRegistryClient.Models.Manifest.Oci;

public class OciManifestList : ManifestList
{
    public OciManifestList()
    {
        MediaType = ManifestMediaTypes.OciManifestList1;
    }

    [JsonPropertyName("annotations")]
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
