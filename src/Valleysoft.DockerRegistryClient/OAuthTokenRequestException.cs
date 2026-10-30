using System.Net;

namespace Valleysoft.DockerRegistryClient;

internal sealed class OAuthTokenRequestException(HttpStatusCode statusCode)
    : HttpRequestException($"OAuth token request was denied: {statusCode}.", null, statusCode);
