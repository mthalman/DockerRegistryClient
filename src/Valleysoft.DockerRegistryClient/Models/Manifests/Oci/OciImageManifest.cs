using System.Text.Json.Serialization;
using Valleysoft.DockerRegistryClient.Models.Manifests;

namespace Valleysoft.DockerRegistryClient.Models.Manifest.Oci;

// https://github.com/opencontainers/image-spec/blob/v1.0/manifest.md
public class OciImageManifest : Manifests.Manifest, IImageManifest
{
    public OciImageManifest()
    {
        MediaType = ManifestMediaTypes.OciManifestSchema1;
        SchemaVersion = 2;
    }

    [JsonPropertyName("artifactType")]
    public string? ArtifactType { get; set; }

    [JsonPropertyName("config")]
    public OciDescriptor Config { get; set; } = new();

    IDescriptor? IImageManifest.Config => Config;

    [JsonPropertyName("layers")]
    public OciDescriptor[] Layers { get; set; } = [];

    IDescriptor[] IImageManifest.Layers => Layers;

    [JsonPropertyName("subject")]
    public OciDescriptor? Subject { get; set; }

    [JsonPropertyName("annotations")]
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
