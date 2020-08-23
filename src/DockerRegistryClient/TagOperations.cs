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

        public Task<HttpOperationResponse<RepositoryTags>> GetWithHttpMessagesAsync(string repositoryName, CancellationToken cancellationToken = default)
        {
            Uri requestUri = new Uri(this.Client.BaseUri.AbsoluteUri + $"v2/{repositoryName}/tags/list");
            return this.Client.SendRequestAsync<RepositoryTags>(new HttpRequestMessage(HttpMethod.Get, requestUri), cancellationToken);
        }
    }
}
