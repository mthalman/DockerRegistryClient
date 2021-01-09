using System.Threading;
using System.Threading.Tasks;
using Valleysoft.DockerRegistry.Models;
using Microsoft.Rest;

namespace Valleysoft.DockerRegistry
{
    public interface IManifestOperations
    {
        Task<HttpOperationResponse<ManifestInfo>> GetWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
        Task<HttpOperationResponse<string>> GetDigestWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
    }
}
