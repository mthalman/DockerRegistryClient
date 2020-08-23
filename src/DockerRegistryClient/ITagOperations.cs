using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;
using Microsoft.Rest;

namespace DockerRegistry
{
    public interface ITagOperations
    {
        Task<HttpOperationResponse<RepositoryTags>> GetWithHttpMessagesAsync(string repositoryName, CancellationToken cancellationToken = default);
    }
}
