using System.Net;

namespace Valleysoft.DockerRegistryClient;

internal sealed class RedirectDelegatingHandler : DelegatingHandler
{
    private const int MaxAutomaticRedirects = 50;
#if NET5_0_OR_GREATER
    private static readonly HttpRequestOptionsKey<Uri> s_restrictedOriginKey =
        new("Valleysoft.DockerRegistryClient.RestrictedRedirectOrigin");
#else
    private const string RestrictedOriginKey =
        "Valleysoft.DockerRegistryClient.RestrictedRedirectOrigin";
#endif

    public RedirectDelegatingHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    internal static void RequireSameOrigin(HttpRequestMessage request, Uri origin)
    {
#if NET5_0_OR_GREATER
        request.Options.Set(s_restrictedOriginKey, origin);
#else
        request.Properties[RestrictedOriginKey] = origin;
#endif
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Uri? redirectOrigin = GetRestrictedOrigin(request);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        for (int redirectCount = 0; redirectCount < MaxAutomaticRedirects; redirectCount++)
        {
            Uri? redirectUri;
            try
            {
                redirectUri = GetRedirectUri(request.RequestUri!, response);
                if (redirectUri is not null && redirectOrigin is not null)
                {
                    redirectUri = UrlHelper.ResolveSameOrigin(redirectOrigin, redirectUri.AbsoluteUri);
                }
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

            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private static Uri? GetRestrictedOrigin(HttpRequestMessage request)
    {
#if NET5_0_OR_GREATER
        return request.Options.TryGetValue(s_restrictedOriginKey, out Uri? origin)
            ? origin
            : null;
#else
        if (request.Properties.TryGetValue(RestrictedOriginKey, out object? value) &&
            value is Uri restrictedOrigin)
        {
            return restrictedOrigin;
        }

        return null;
#endif
    }

    private static Uri? GetRedirectUri(Uri requestUri, HttpResponseMessage response)
    {
        if (!IsRedirectStatusCode(response.StatusCode) ||
            response.Headers.Location is null)
        {
            return null;
        }

        Uri redirectUri = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location
            : new Uri(requestUri, response.Headers.Location);

        if (!string.IsNullOrEmpty(requestUri.Fragment) && string.IsNullOrEmpty(redirectUri.Fragment))
        {
            redirectUri = new UriBuilder(redirectUri)
            {
                Fragment = requestUri.Fragment
            }.Uri;
        }

        if (requestUri.Scheme == Uri.UriSchemeHttps && redirectUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return redirectUri.Scheme == Uri.UriSchemeHttp || redirectUri.Scheme == Uri.UriSchemeHttps
            ? redirectUri
            : null;
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
