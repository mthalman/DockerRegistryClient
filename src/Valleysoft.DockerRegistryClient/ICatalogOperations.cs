using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

public interface ICatalogOperations
{
    Task<Page<Catalog>> GetAsync(int? count = null, CancellationToken cancellationToken = default);
    Task<Page<Catalog>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default);
}
