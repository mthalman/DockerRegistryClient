using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Valleysoft.DockerRegistry.Models;
using Microsoft.Rest;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;

namespace Valleysoft.DockerRegistry
{
    public class DockerRegistryClient : ServiceClient<DockerRegistryClient>
    {
        public string Registry { get; }
        public Uri BaseUri { get; }
        public ICatalogOperations Catalog { get; }
        public ITagOperations Tags { get; }
        public IManifestOperations Manifests { get; }

        private readonly ServiceClientCredentials? credentials;

        public DockerRegistryClient(string registry)
            : this(registry, null)
        {
        }

        public DockerRegistryClient(string registry, ServiceClientCredentials? serviceClientCredentials)
            : this(registry, serviceClientCredentials, (HttpClientHandler?)null)
        {
            
        }

        public DockerRegistryClient(string registry, ServiceClientCredentials? serviceClientCredentials, HttpClient httpClient, bool disposeHttpClient = true)
            : this(registry, serviceClientCredentials, httpClient, disposeHttpClient, null)
        {
        }

        public DockerRegistryClient(string registry, ServiceClientCredentials? serviceClientCredentials, params DelegatingHandler[] handlers)
            : this(registry, serviceClientCredentials, null, handlers)
        {
        }

        public DockerRegistryClient(string registry, ServiceClientCredentials? serviceClientCredentials, HttpClientHandler? rootHandler, params DelegatingHandler[] handlers)
            : this(registry, serviceClientCredentials, null, true, rootHandler, handlers)
        {
        }

        private DockerRegistryClient(string registry, ServiceClientCredentials? serviceClientCredentials, HttpClient? httpClient, bool disposeHttpClient, HttpClientHandler? rootHandler, params DelegatingHandler[] handlers)
            : base(httpClient, disposeHttpClient)
        {
            this.InitializeHttpClient(httpClient, rootHandler, handlers.Append(new OAuthDelegatingHandler()).ToArray());

            this.Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.BaseUri = new Uri($"https://{this.Registry}");

            this.credentials = serviceClientCredentials;
            serviceClientCredentials?.InitializeServiceClient(this);

            this.Catalog = new CatalogOperations(this);
            this.Tags = new TagOperations(this);
            this.Manifests = new ManifestOperations(this);
        }

        internal Task<HttpOperationResponse<T>> SendRequestAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
            this.SendRequestAsync<T>(request, null, cancellationToken);

        internal async Task<HttpOperationResponse<T>> SendRequestAsync<T>(HttpRequestMessage request,
            Func<HttpResponseMessage, string, T>? getResult, CancellationToken cancellationToken = default)
        {
            if (this.credentials != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await this.credentials.ProcessHttpRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            HttpResponseMessage response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (request.Content is null)
                {
                    throw new InvalidOperationException("Request content is null.");
                }

                if (response.Content is null)
                {
                    throw new InvalidOperationException($"Response content is null.");
                }

                string requestContent = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                ErrorResult errorResult = SafeJsonConvert.DeserializeObject<ErrorResult>(errorContent);

                throw new DockerRegistryException(
                    $"Response status code does not indicate success: {response.StatusCode}. See {nameof(DockerRegistryException.Errors)} property for more detail. ({response.ReasonPhrase})")
                {
                    Errors = errorResult.Errors,
                    Body = errorContent,
                    Request = new HttpRequestMessageWrapper(request, requestContent),
                    Response = new HttpResponseMessageWrapper(response, errorContent)
                };
            }
            response.EnsureSuccessStatusCode();

            cancellationToken.ThrowIfCancellationRequested();
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            getResult = getResult ?? GetResult<T>;

            try
            {
                return new HttpOperationResponse<T>
                {
                    Body = getResult(response, content),
                    Request = request,
                    Response = response
                };
            }
            catch (JsonException e)
            {
                throw new SerializationException("Unable to deserialize the response.", content, e);
            }
        }

        internal static T GetResult<T>(HttpResponseMessage response, string content) =>
            SafeJsonConvert.DeserializeObject<T>(content);

        internal static string? GetNextLinkUrl(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Link", out IEnumerable<string>? linkValues))
            {
                HttpLink? nextLink = linkValues
                    .Select(linkValue =>
                    {
                        HttpLink.TryParse(linkValue, out HttpLink? httpLink);
                        return httpLink;
                    })
                    .FirstOrDefault(link => link?.Relationship == "next");

                if (nextLink is null)
                {
                    throw new InvalidOperationException(
                        $"Unable to parse link header '{string.Join(", ", linkValues.ToArray())}'");
                }
                
                return nextLink.Url;
            }

            return null;
        }
    }
}
