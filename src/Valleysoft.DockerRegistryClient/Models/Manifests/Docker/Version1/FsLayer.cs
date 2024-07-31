using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests.Docker.Version1;

public class FsLayer
{
    /// <summary>
    /// blobSum is the digest of the referenced filesystem image layer.
    /// </summary>
    [JsonPropertyName("blobSum")]
    public string? BlobSum { get; set; }
}
