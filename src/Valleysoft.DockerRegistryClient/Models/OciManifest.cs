using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

public class OciManifest : DockerManifestV2
{
    public OciManifest()
    {
        MediaType = ManifestMediaTypes.OciManifestSchema1;
    }

    [JsonPropertyName("annotations")]
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
