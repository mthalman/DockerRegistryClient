using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient;
 
public interface IBlobOperations
{
    Task<HttpOperationResponse<Stream>> GetWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);

    Task<HttpOperationResponse<bool>> ExistsWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default);
}
