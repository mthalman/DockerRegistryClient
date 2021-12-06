using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

public class FsLayer
{
    /// <summary>
    /// blobSum is the digest of the referenced filesystem image layer.
    /// </summary>
    [JsonProperty("blobSum")]
    public string? BlobSum { get; set; }
}
