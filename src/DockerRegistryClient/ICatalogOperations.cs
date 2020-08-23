using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;
using Microsoft.Rest;

namespace DockerRegistry
{
    public interface ICatalogOperations
    {
        Task<HttpOperationResponse<Catalog>> GetWithHttpMessagesAsync(CancellationToken cancellationToken = default);
    }
}
