using Microsoft.Rest;

namespace DockerRegistry
{
    internal static class HttpOperationResponseExtensions
    {
        public static T GetBodyAndDispose<T>(this HttpOperationResponse<T> response)
        {
            T body = response.Body;
            response.Dispose();
            return body;
        }
    }
}
