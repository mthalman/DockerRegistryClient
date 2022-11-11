using Microsoft.Rest;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;
 
public static class ManifestOperationsExtensions
{
    public static async Task<ManifestInfo> GetAsync(this IManifestOperations operations, string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        var response = await operations.GetWithHttpMessagesAsync(repositoryName, tagOrDigest, cancellationToken).ConfigureAwait(false);
        return response.GetBodyAndDispose();
    }

    public static async Task<bool> ExistsAsync(this IManifestOperations operations, string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        HttpOperationResponse<bool> response =
            await operations.ExistsWithHttpMessagesAsync(repositoryName, tagOrDigest, cancellationToken).ConfigureAwait(false);
        return response.Body;
    }

    public static async Task<string> GetDigestAsync(this IManifestOperations operations, string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        var response = await operations.GetDigestWithHttpMessagesAsync(repositoryName, tagOrDigest, cancellationToken).ConfigureAwait(false);
        return response.GetBodyAndDispose();
    }
}
