using Valleysoft.DockerRegistryClient.Models.Manifests;

namespace Valleysoft.DockerRegistryClient;

public interface IManifestOperations
{
    Task<ManifestInfo> GetAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken = default);
    Task<string> GetDigestAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
}
