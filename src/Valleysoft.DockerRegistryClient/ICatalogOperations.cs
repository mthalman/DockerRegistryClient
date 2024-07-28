using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;
 
public interface ICatalogOperations
{
    Task<HttpOperationResponse<Page<Catalog>>> GetWithHttpMessagesAsync(int? count = null, CancellationToken cancellationToken = default);
    Task<HttpOperationResponse<Page<Catalog>>> GetNextWithHttpMessagesAsync(string nextPageLink, CancellationToken cancellationToken = default);
}
