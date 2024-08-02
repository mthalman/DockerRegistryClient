namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public interface IManifestList : IManifest
{
    IManifestReference[] Manifests { get; }
}
