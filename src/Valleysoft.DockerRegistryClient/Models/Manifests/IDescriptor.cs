namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public interface IDescriptor
{
    string MediaType { get; }
    long Size { get; }
    string Digest { get; }
}
