namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public class ManifestInfo
{
    public ManifestInfo(string mediaType, string dockerContentDigest, IManifest manifest)
        : this(mediaType, dockerContentDigest, manifest, ReadOnlyMemory<byte>.Empty)
    {
    }

    public ManifestInfo(
        string mediaType,
        string dockerContentDigest,
        IManifest manifest,
        ReadOnlyMemory<byte> content)
    {
        MediaType = mediaType;
        DockerContentDigest = dockerContentDigest;
        Manifest = manifest;
        Content = content;
    }

    public string MediaType { get; }
    public string DockerContentDigest { get; }
    public IManifest Manifest { get; }
    public ReadOnlyMemory<byte> Content { get; }
}
