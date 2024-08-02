namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public class ManifestInfo
{
    public ManifestInfo(string mediaType, string dockerContentDigest, IManifest manifest)
    {
        MediaType = mediaType;
        DockerContentDigest = dockerContentDigest;
        Manifest = manifest;
    }

    public string MediaType { get; }
    public string DockerContentDigest { get; }
    public IManifest Manifest { get; }
}
