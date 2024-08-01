namespace Valleysoft.DockerRegistryClient.Models.Manifests;

public interface IManifest
{
    int SchemaVersion { get; }
    string? MediaType { get; }
}
