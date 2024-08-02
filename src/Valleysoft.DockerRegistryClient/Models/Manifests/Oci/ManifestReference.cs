using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

public class ManifestReference : OciDescriptor, IManifestReference
{
    /// <summary>
    /// The platform object describes the platform which the image in the manifest runs on. A full list of valid operating system and architecture values are listed in the Go language documentation for $GOOS and $GOARCH (https://golang.org/doc/install/source#environment).
    /// </summary>
    [JsonPropertyName("platform")]
    public ManifestPlatform? Platform { get; set; }
}
