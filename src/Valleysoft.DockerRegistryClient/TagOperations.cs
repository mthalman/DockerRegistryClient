using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

internal class TagOperations : ITagOperations
{
    public RegistryClient Client { get; }

    public TagOperations(RegistryClient client)
    {
        this.Client = client;
    }

    public async Task<Page<RepositoryTags>> GetAsync(string repositoryName, int? count = null, CancellationToken cancellationToken = default)
    {
        string url = UrlHelper.ApplyCount($"v2/{repositoryName}/tags/list", count);
        return await GetNextAsync(url, cancellationToken);
    }

    public async Task<Page<RepositoryTags>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            new Uri(UrlHelper.Concat(this.Client.BaseUri.AbsoluteUri, nextPageLink)));

        return await OperationsHelper.HandleNotFoundErrorAsync(
           "Repository not found.",
           () => this.Client.SendRequestAsync(
               request,
               RegistryClient.GetPageResult<RepositoryTags>,
               cancellationToken)).ConfigureAwait(false);
    }
}
