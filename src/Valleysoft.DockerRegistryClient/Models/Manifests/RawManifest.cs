namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public sealed class RawManifest : IManifest
{
    public RawManifest(string mediaType, ReadOnlyMemory<byte> content)
    {
        MediaType = mediaType;
        Content = content;
    }

    public string MediaType { get; }
    public ReadOnlyMemory<byte> Content { get; }
}
