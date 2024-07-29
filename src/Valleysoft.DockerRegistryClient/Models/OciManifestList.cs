using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public class OciManifestList : ManifestList
{
    public OciManifestList()
    {
        MediaType = ManifestMediaTypes.OciManifestList1;
    }

    [JsonPropertyName("annotations")]
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
