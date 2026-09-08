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

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            HttpBearerChallenge challenge;
            try
            {
                challenge = GetBearerChallenge(response);
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
        else if (authorization is not null && response.StatusCode == HttpStatusCode.Forbidden)
        {
            // Not all registries will return 401 if Authorization is provided which requires an OAuth challenge.
            // For example, ghcr.io will return a 403 in that case and won't return a challenge. In such cases,
            // set the Authorization header to null and attempt again.
            request.Headers.Authorization = null;
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

    private static HttpBearerChallenge GetBearerChallenge(HttpResponseMessage response)
    {
        AuthenticationHeaderValue? bearerHeader = response.Headers.WwwAuthenticate
            .AsEnumerable()
            .FirstOrDefault(header => header.Scheme == HttpBearerChallenge.Bearer);
        if (bearerHeader is null)
        {
            throw new AuthenticationException(
                $"Bearer challenge not contained in unauthorized response from {response.RequestMessage?.RequestUri}");
        }

        return HttpBearerChallenge.Parse(bearerHeader.Parameter);
    }

    private async Task<OAuthToken> GetOAuthTokenAsync(
        HttpBearerChallenge challenge,
        AuthenticationHeaderValue? authorization,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage authenticateRequest;
        if (authorization is not null && authorization.Scheme == "Bearer")
        {
            authenticateRequest = new(HttpMethod.Post, challenge.Realm)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", "registry-client" },
                    { "grant_type", "refresh_token" },
                    { "refresh_token", authorization.Parameter ?? string.Empty },
                    { "scope", challenge.Scope },
                    { "service", challenge.Service },
                })
            };
        }
        else
        {
            Uri authenticateUri = new($"{challenge.Realm}?service={challenge.Service}&scope={challenge.Scope}");
            authenticateRequest = new(HttpMethod.Get, authenticateUri);
            authenticateRequest.Headers.Authorization = authorization;
        }

        using HttpRequestMessage requestToDispose = authenticateRequest;
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
