using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

public class OciManifestList : ManifestList
{
    public OciManifestList()
    {
        MediaType = ManifestMediaTypes.OciManifestList1;
    }

    [JsonProperty("annotations")]
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
