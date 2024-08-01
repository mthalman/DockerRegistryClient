namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public interface IImageManifest : IManifest
{
    IDescriptor? Config { get; }
    IDescriptor[] Layers { get; }
}
