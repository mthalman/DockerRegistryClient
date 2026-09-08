using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text.Json;

namespace Valleysoft.DockerRegistryClient;

internal class OAuthDelegatingHandler : DelegatingHandler
{
    public OAuthDelegatingHandler()
    {
    }

    public OAuthDelegatingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AuthenticationHeaderValue? authorization = request.Headers.Authorization;

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (request.Headers.Authorization is not null &&
            response.StatusCode == HttpStatusCode.Forbidden)
        {
            // Some registries only return a bearer challenge after an anonymous request.
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                HttpRequestReplayPolicy.PrepareForReplay(request);
            }
            finally
            {
                response.Dispose();
            }

            request.Headers.Authorization = null;
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            HttpBearerChallenge challenge;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                challenge = GetOAuthChallenge(response);
                HttpRequestReplayPolicy.PrepareForReplay(request);
            }
            finally
            {
                response.Dispose();
            }

            request = await GetAuthenticatedRequestAsync(
                challenge,
                request,
                authorization,
                cancellationToken).ConfigureAwait(false);
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private async Task<HttpRequestMessage> GetAuthenticatedRequestAsync(
        HttpBearerChallenge challenge,
        HttpRequestMessage request,
        AuthenticationHeaderValue? authorization,
        CancellationToken cancellationToken = default)
    {
        var authToken = await GetOAuthTokenAsync(
            challenge,
            authorization,
            cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(HttpBearerChallenge.Bearer, authToken.AccessToken ?? authToken.Token);
        return request;
    }

    private async Task<OAuthToken> GetOAuthTokenAsync(
        HttpBearerChallenge challenge,
        AuthenticationHeaderValue? authorization,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HttpRequestMessage authenticateRequest;
        if (authorization is not null &&
            authorization.Scheme.Equals(HttpBearerChallenge.Bearer, StringComparison.OrdinalIgnoreCase))
        {
            var formValues = new Dictionary<string, string>
            {
                { "client_id", "registry-client" },
                { "grant_type", "refresh_token" },
                { "refresh_token", authorization.Parameter ?? string.Empty },
            };
            AddOptionalParameter(formValues, "scope", challenge.Scope);
            AddOptionalParameter(formValues, "service", challenge.Service);

            authenticateRequest = new(HttpMethod.Post, challenge.Realm)
            {
                Content = new FormUrlEncodedContent(formValues)
            };
        }
        else
        {
            Uri authenticateUri = CreateAuthenticationUri(challenge);
            authenticateRequest = new(HttpMethod.Get, authenticateUri);
            authenticateRequest.Headers.Authorization = authorization;
        }

        using (authenticateRequest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using HttpResponseMessage authenticateResponse =
                await base.SendAsync(authenticateRequest, cancellationToken).ConfigureAwait(false);
            authenticateResponse.EnsureSuccessStatusCode();

            cancellationToken.ThrowIfCancellationRequested();

#if NET5_0_OR_GREATER
            string tokenContent = await authenticateResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            string tokenContent = await authenticateResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif

            try
            {
                return JsonSerializer.Deserialize<OAuthToken>(tokenContent) ?? throw new JsonException($"Unable to deserialize response:{Environment.NewLine}{tokenContent}");
            }
            catch (JsonException e)
            {
                throw new JsonException($"Unable to deserialize the response:{Environment.NewLine}{tokenContent}", e);
            }
        }
    }

    private static void AddOptionalParameter(
        IDictionary<string, string> parameters,
        string name,
        string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parameters.Add(name, value);
        }
    }

    private static Uri CreateAuthenticationUri(HttpBearerChallenge challenge)
    {
        var builder = new UriBuilder(challenge.Realm);
        var queryParts = new List<string>();

        if (builder.Query.Length > 1)
        {
            queryParts.Add(builder.Query.Substring(1));
        }

        AddOptionalQueryParameter(queryParts, "service", challenge.Service);
        AddOptionalQueryParameter(queryParts, "scope", challenge.Scope);
        builder.Query = string.Join("&", queryParts);
        return builder.Uri;
    }

    private static void AddOptionalQueryParameter(
        ICollection<string> queryParts,
        string name,
        string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            queryParts.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }
    }

    private static HttpBearerChallenge GetOAuthChallenge(HttpResponseMessage response)
    {
        AuthenticationHeaderValue? bearerHeader = response.Headers.WwwAuthenticate
            .AsEnumerable()
            .FirstOrDefault(header =>
                header.Scheme.Equals(HttpBearerChallenge.Bearer, StringComparison.OrdinalIgnoreCase)) ??
                throw new AuthenticationException(
                    $"****** not contained in unauthorized response from {response.RequestMessage?.RequestUri}");
        return HttpBearerChallenge.Parse(bearerHeader.Parameter);
    }
}
