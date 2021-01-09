using System.Threading;
using System.Threading.Tasks;
using Valleysoft.DockerRegistry.Models;

namespace Valleysoft.DockerRegistry
{
    public static class ManifestOperationsExtensions
    {
        public static async Task<ManifestInfo> GetAsync(this IManifestOperations operations, string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
        {
            var response = await operations.GetWithHttpMessagesAsync(repositoryName, tagOrDigest, cancellationToken).ConfigureAwait(false);
            return response.GetBodyAndDispose();
        }

        public static async Task<string> GetDigestAsync(this IManifestOperations operations, string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
        {
            var response = await operations.GetDigestWithHttpMessagesAsync(repositoryName, tagOrDigest, cancellationToken).ConfigureAwait(false);
            return response.GetBodyAndDispose();
        }
    }
}
