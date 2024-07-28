using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

public interface ITagOperations
{
    Task<Page<RepositoryTags>> GetAsync(
        string repositoryName, int? count = null, CancellationToken cancellationToken = default);

    Task<Page<RepositoryTags>> GetNextAsync(
        string nextPageLink, CancellationToken cancellationToken = default);
}
