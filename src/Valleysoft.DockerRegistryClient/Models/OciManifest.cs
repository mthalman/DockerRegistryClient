using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

public class OciManifest : DockerManifestV2
{
    public OciManifest()
    {
        MediaType = ManifestMediaTypes.OciManifestSchema1;
    }

    [JsonProperty("annotations")]
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
