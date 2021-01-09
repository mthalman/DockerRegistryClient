using System.Threading;
using System.Threading.Tasks;
using Valleysoft.DockerRegistry.Models;
using Microsoft.Rest;

namespace Valleysoft.DockerRegistry
{
    public interface ICatalogOperations
    {
        Task<HttpOperationResponse<Page<Catalog>>> GetWithHttpMessagesAsync(int? count = null, CancellationToken cancellationToken = default);
        Task<HttpOperationResponse<Page<Catalog>>> GetNextWithHttpMessagesAsync(string nextPageLink, CancellationToken cancellationToken = default);
    }
}
