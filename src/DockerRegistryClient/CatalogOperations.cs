using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;
using Microsoft.Rest;

namespace DockerRegistry
{
    internal class CatalogOperations : IServiceOperations<DockerRegistryClient>, ICatalogOperations
    {
        public DockerRegistryClient Client { get; }

        public CatalogOperations(DockerRegistryClient client)
        {
            this.Client = client;
        }

        public Task<HttpOperationResponse<Catalog>> GetWithHttpMessagesAsync(CancellationToken cancellationToken = default)
        {
            Uri requestUri = new Uri(this.Client.BaseUri.AbsoluteUri + "v2/_catalog");
            return this.Client.SendRequestAsync<Catalog>(new HttpRequestMessage(HttpMethod.Get, requestUri), cancellationToken);
        }
    }
}
