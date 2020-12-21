using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;
using Microsoft.Rest;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;

namespace DockerRegistry
{
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
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                request = await GetAuthenticatedRequestAsync(response, request, cancellationToken).ConfigureAwait(false);
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }

        private async Task<HttpRequestMessage> GetAuthenticatedRequestAsync(HttpResponseMessage response, HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var authToken = await GetOAuthTokenAsync(response, request, cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue(HttpBearerChallenge.Bearer, authToken.Token);
            return request;
        }

        private async Task<OAuthToken> GetOAuthTokenAsync(HttpResponseMessage response, HttpRequestMessage unauthorizedRequest, CancellationToken cancellationToken = default)
        {
            AuthenticationHeaderValue? bearerHeader = response.Headers.WwwAuthenticate
                .AsEnumerable()
                .FirstOrDefault(header => header.Scheme == HttpBearerChallenge.Bearer);

            if (bearerHeader is null)
            {
                throw new AuthenticationException($"Bearer header not contained in unauthorized response from {response.RequestMessage?.RequestUri}");
            }

            HttpBearerChallenge challenge = HttpBearerChallenge.Parse(bearerHeader.Parameter);

            Uri authenticateUri = new Uri($"{challenge.Realm}?service={challenge.Service}&scope={challenge.Scope}");
            HttpRequestMessage authenticateRequest = new HttpRequestMessage(HttpMethod.Get, authenticateUri);
            authenticateRequest.Headers.Authorization = unauthorizedRequest.Headers.Authorization;

            cancellationToken.ThrowIfCancellationRequested();
            response = await base.SendAsync(authenticateRequest, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            cancellationToken.ThrowIfCancellationRequested();
            string tokenContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            try
            {
                return SafeJsonConvert.DeserializeObject<OAuthToken>(tokenContent);
            }
            catch (JsonException e)
            {
                throw new SerializationException("Unable to deserialize the response.", tokenContent, e);
            }
        }
    }
}
