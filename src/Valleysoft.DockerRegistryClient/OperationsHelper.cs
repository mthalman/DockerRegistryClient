using System.Net;

namespace Valleysoft.DockerRegistryClient;

internal static class OperationsHelper
{
    public static async Task<HttpOperationResponse<T>> HandleNotFoundErrorAsync<T>(string errorMessage, Func<Task<HttpOperationResponse<T>>> func)
    {
        try
        {
            return await func().ConfigureAwait(false);
        }
        catch (RegistryException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new RegistryException(errorMessage, ex);
        }
    }
}
