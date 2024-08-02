namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public interface IManifestReference : IDescriptor
{
    ManifestPlatform? Platform { get; }
}
