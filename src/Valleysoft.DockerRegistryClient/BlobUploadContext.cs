using System.Net.Http.Headers;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// State associated with the initialization of the blob upload.
/// </summary>
public class BlobUploadContext
{
    internal BlobUploadContext(
        AuthenticationHeaderValue? authorization,
        Uri authorizationOrigin,
        OAuthDelegatingHandler.AuthenticationState? authenticationState = null)
    {
        Authorization = authorization;
        AuthorizationOrigin = authorizationOrigin;
        AuthenticationState = authenticationState;
    }

    internal AuthenticationHeaderValue? Authorization { get; }

    internal Uri AuthorizationOrigin { get; }

    internal OAuthDelegatingHandler.AuthenticationState? AuthenticationState { get; }
}
