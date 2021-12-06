using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// The manifest list is the "fat manifest" which points to specific image manifests for one or more platforms. Its use is optional, and relatively few images will use one of these manifests. A client will distinguish a manifest list from an image manifest based on the Content-Type returned in the HTTP response.
/// </summary>
public class ManifestList : ManifestWithMediaType
{
    public ManifestList()
    {
        MediaType = ManifestMediaTypes.ManifestList;
        SchemaVersion = 2;
    }

    /// <summary>
    /// The manifests field contains a list of manifests for specific platforms.
    /// </summary>
    [JsonProperty("manifests")]
    public ManifestReference[] Manifests { get; set; } = Array.Empty<ManifestReference>();
}
