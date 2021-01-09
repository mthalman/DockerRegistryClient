using System.Threading;
using System.Threading.Tasks;
using Valleysoft.DockerRegistryClient.Models;
using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient
{
    public interface IManifestOperations
    {
        Task<HttpOperationResponse<ManifestInfo>> GetWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
        Task<HttpOperationResponse<string>> GetDigestWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default);
    }
}
