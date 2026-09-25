using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text.Json;

namespace Valleysoft.DockerRegistryClient;

internal class OAuthDelegatingHandler : DelegatingHandler
{
    private readonly Uri? registryUri;

    public OAuthDelegatingHandler()
    {
    }

    public OAuthDelegatingHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    public OAuthDelegatingHandler(Uri registryUri, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        this.registryUri = registryUri;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SendAsync(request, cancellationToken).GetAwaiter().GetResult();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        AuthenticationHeaderValue? authorization = request.Headers.Authorization;
        Uri requestRegistryUri = registryUri ?? request.RequestUri ??
            throw new InvalidOperationException("The request URI must be set before authentication.");

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (request.Headers.Authorization is not null &&
            response.StatusCode == HttpStatusCode.Forbidden)
        {
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
            Uri? challengeUri = response.RequestMessage?.RequestUri ?? request.RequestUri;
            if (!RegistryUriBuilder.HasSameOrigin(requestRegistryUri, challengeUri ?? requestRegistryUri))
            {
                return response;
            }

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
        request.Headers.Authorization = new AuthenticationHeaderValue(
            HttpBearerChallenge.Bearer,
            authToken.AccessToken ?? authToken.Token);
        return request;
    }

    private async Task<OAuthToken> GetOAuthTokenAsync(
        HttpBearerChallenge challenge,
        AuthenticationHeaderValue? authorization,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri realmUri = HttpBearerChallenge.ValidateRealmUri(challenge.Realm);

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

            authenticateRequest = new(HttpMethod.Post, realmUri)
            {
                Content = new FormUrlEncodedContent(formValues)
            };
        }
        else
        {
            Uri authenticateUri = CreateAuthenticationUri(realmUri, challenge);
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
                return DockerRegistryClientJson.Deserialize<OAuthToken>(tokenContent);
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
        if (value is not null && value.Length > 0)
        {
            parameters.Add(name, value);
        }
    }

    private static Uri CreateAuthenticationUri(
        Uri realmUri,
        HttpBearerChallenge challenge)
    {
        var builder = new UriBuilder(realmUri);
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
        if (value is not null && value.Length > 0)
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
                    $"The unauthorized response from {response.RequestMessage?.RequestUri} does not contain an OAuth authentication challenge.");
        return HttpBearerChallenge.Parse(bearerHeader.Parameter);
    }
}
