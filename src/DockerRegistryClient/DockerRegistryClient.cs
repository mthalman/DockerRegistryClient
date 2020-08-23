using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;

namespace DockerRegistry
{
    public class DockerRegistryClient : ServiceClient<DockerRegistryClient>
    {
        public string Registry { get; }
        public Uri BaseUri { get; }
        public ICatalogOperations Catalog { get; }
        public ITagOperations Tags { get; }
        public IManifestOperations Manifests { get; }

        private readonly ServiceClientCredentials credentials;

        public DockerRegistryClient(string registry)
            : this(registry, null)
        {
        }

        public DockerRegistryClient(string registry, ServiceClientCredentials serviceClientCredentials)
            : this(registry, serviceClientCredentials, (HttpClientHandler)null)
        {
            
        }

        public DockerRegistryClient(string registry, ServiceClientCredentials serviceClientCredentials, HttpClient httpClient, bool disposeHttpClient = true)
            : this(registry, serviceClientCredentials, httpClient, disposeHttpClient, null)
        {
        }

        public DockerRegistryClient(string registry, ServiceClientCredentials serviceClientCredentials, params DelegatingHandler[] handlers)
            : this(registry, serviceClientCredentials, null, handlers)
        {
        }

        public DockerRegistryClient(string registry, ServiceClientCredentials serviceClientCredentials, HttpClientHandler rootHandler, params DelegatingHandler[] handlers)
            : this(registry, serviceClientCredentials, null, true, rootHandler, handlers)
        {
        }

        private DockerRegistryClient(string registry, ServiceClientCredentials serviceClientCredentials, HttpClient httpClient, bool disposeHttpClient, HttpClientHandler rootHandler, params DelegatingHandler[] handlers)
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

        internal Task<HttpOperationResponse<T>> SendRequestAsync<T>(HttpRequestMessage request, Func<HttpResponseMessage, string, T> getResult, CancellationToken cancellationToken = default) =>
            this.SendRequestAsync(request, getResult, HttpMethod.Get, cancellationToken);

        internal async Task<HttpOperationResponse<T>> SendRequestAsync<T>(HttpRequestMessage request, Func<HttpResponseMessage, string, T> getResult, HttpMethod method, CancellationToken cancellationToken = default)
        {
            if (this.credentials != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await this.credentials.ProcessHttpRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            HttpResponseMessage response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            cancellationToken.ThrowIfCancellationRequested();
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (getResult is null)
            {
                getResult = GetResult<T>;
            }

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

        private static T GetResult<T>(HttpResponseMessage response, string content) =>
            SafeJsonConvert.DeserializeObject<T>(content);
    }
}
