using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;
 
public interface IManifestOperations
{
    Task<HttpOperationResponse<ManifestInfo>> GetWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
    Task<HttpOperationResponse<bool>> ExistsWithHttpMessagesAsync(string repositoryName, string digest, CancellationToken cancellationToken = default);
    Task<HttpOperationResponse<string>> GetDigestWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
}
