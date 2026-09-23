using System.Net;

namespace Valleysoft.DockerRegistryClient;

internal sealed class RedirectDelegatingHandler : DelegatingHandler
{
    private const int MaxAutomaticRedirects = 50;

    public RedirectDelegatingHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SendAsync(request, cancellationToken).GetAwaiter().GetResult();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        for (int redirectCount = 0; redirectCount < MaxAutomaticRedirects; redirectCount++)
        {
            Uri? redirectUri;
            try
            {
                redirectUri = GetRedirectUri(request.RequestUri!, response);
            }
            catch
            {
                response.Dispose();
                throw;
            }

            if (redirectUri is null)
            {
                return response;
            }

            HttpStatusCode redirectStatusCode = response.StatusCode;
            response.Dispose();
            request.Headers.Authorization = null;
            request.RequestUri = redirectUri;

            if (RequiresGet(redirectStatusCode, request.Method))
            {
                request.Method = HttpMethod.Get;
                request.Content = null;
                request.Headers.TransferEncodingChunked = false;
            }
            else
            {
                HttpRequestReplayPolicy.PrepareForReplay(request);
            }

            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private static Uri? GetRedirectUri(Uri requestUri, HttpResponseMessage response)
    {
        if (!IsRedirectStatusCode(response.StatusCode) ||
            response.Headers.Location is null)
        {
            return null;
        }

        string location = response.Headers.Location.OriginalString;
        Uri redirectUri = new(requestUri, location);

        if (requestUri.Scheme == Uri.UriSchemeHttps && redirectUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (redirectUri.Scheme != Uri.UriSchemeHttp && redirectUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(requestUri.Fragment) && string.IsNullOrEmpty(redirectUri.Fragment))
        {
            redirectUri = new UriBuilder(redirectUri)
            {
                Fragment = requestUri.Fragment
            }.Uri;
        }

        return redirectUri;
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode) =>
        (int)statusCode is 300 or 301 or 302 or 303 or 307 or 308;

    private static bool RequiresGet(HttpStatusCode statusCode, HttpMethod method) =>
        (int)statusCode switch
        {
            300 or 301 or 302 => method == HttpMethod.Post,
            303 => method != HttpMethod.Get && method != HttpMethod.Head,
            _ => false
        };
}
