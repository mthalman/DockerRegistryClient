using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;
using Microsoft.Rest;

namespace DockerRegistry
{
    internal class TagOperations : IServiceOperations<DockerRegistryClient>, ITagOperations
    {
        public DockerRegistryClient Client { get; }

        public TagOperations(DockerRegistryClient client)
        {
            this.Client = client;
        }

        public Task<HttpOperationResponse<Page<RepositoryTags>>> GetWithHttpMessagesAsync(
            string repositoryName, int? count = null, CancellationToken cancellationToken = default)
        {
            string url = UrlHelper.ApplyCount($"v2/{repositoryName}/tags/list", count);
            return GetNextWithHttpMessagesAsync(url, cancellationToken);
        }

        public Task<HttpOperationResponse<Page<RepositoryTags>>> GetNextWithHttpMessagesAsync(
            string nextPageLink, CancellationToken cancellationToken = default)
        {
            return this.Client.SendRequestAsync(
                new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(UrlHelper.Concat(this.Client.BaseUri.AbsoluteUri, nextPageLink))),
                GetResult,
                cancellationToken);
        }

        private static Page<RepositoryTags> GetResult(HttpResponseMessage response, string content)
        {
            string? nextLink = DockerRegistryClient.GetNextLinkUrl(response);
            return new Page<RepositoryTags>(DockerRegistryClient.GetResult<RepositoryTags>(response, content), nextLink);
        }
    }
}
