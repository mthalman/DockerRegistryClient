using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

internal class OAuthDelegatingHandler : DelegatingHandler
{
    private AuthenticationHeaderValue? authorization;

    public OAuthDelegatingHandler()
    {
    }

    public OAuthDelegatingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        authorization = request.Headers.Authorization;
        
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            request = await GetAuthenticatedRequestAsync(response, request, cancellationToken).ConfigureAwait(false);
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

    private async Task<HttpRequestMessage> GetAuthenticatedRequestAsync(HttpResponseMessage response, HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var authToken = await GetOAuthTokenAsync(response, request, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(HttpBearerChallenge.Bearer, authToken.AccessToken ?? authToken.Token);
        return request;
    }

    private async Task<OAuthToken> GetOAuthTokenAsync(HttpResponseMessage response, HttpRequestMessage unauthorizedRequest, CancellationToken cancellationToken = default)
    {
        AuthenticationHeaderValue? bearerHeader = response.Headers.WwwAuthenticate
            .AsEnumerable()
            .FirstOrDefault(header => header.Scheme == HttpBearerChallenge.Bearer) ?? throw new AuthenticationException($"Bearer header not contained in unauthorized response from {response.RequestMessage?.RequestUri}");
        HttpBearerChallenge challenge = HttpBearerChallenge.Parse(bearerHeader.Parameter);

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

        cancellationToken.ThrowIfCancellationRequested();
        response = await base.SendAsync(authenticateRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        cancellationToken.ThrowIfCancellationRequested();

#if NET5_0_OR_GREATER
        string tokenContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        string tokenContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
