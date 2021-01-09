using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Valleysoft.DockerRegistryClient.Models;
using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient
{
    internal class CatalogOperations : IServiceOperations<DockerRegistryClient>, ICatalogOperations
    {
        public DockerRegistryClient Client { get; }

        public CatalogOperations(DockerRegistryClient client)
        {
            this.Client = client;
        }

        public Task<HttpOperationResponse<Page<Catalog>>> GetWithHttpMessagesAsync(int? count = null, CancellationToken cancellationToken = default)
        {
            string url = UrlHelper.ApplyCount($"v2/_catalog", count);
            return GetNextWithHttpMessagesAsync(url, cancellationToken);
        }

        public Task<HttpOperationResponse<Page<Catalog>>> GetNextWithHttpMessagesAsync(string nextPageLink, CancellationToken cancellationToken = default)
        {
            return this.Client.SendRequestAsync(
                new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(UrlHelper.Concat(this.Client.BaseUri.AbsoluteUri, nextPageLink))),
                GetResult,
                cancellationToken);
        }

        private static Page<Catalog> GetResult(HttpResponseMessage response, string content)
        {
            string? nextLink = DockerRegistryClient.GetNextLinkUrl(response);
            return new Page<Catalog>(DockerRegistryClient.GetResult<Catalog>(response, content), nextLink);
        }
    }
}
