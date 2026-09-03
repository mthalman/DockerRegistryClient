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
        Uri requestUri = UrlHelper.ResolveSameOrigin(this.Client.BaseUri, nextPageLink);
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            requestUri);
        RedirectDelegatingHandler.RequireSameOrigin(request, this.Client.BaseUri);

        return await OperationsHelper.HandleNotFoundErrorAsync(
           "Repository not found.",
           () => this.Client.SendRequestAsync(
               request,
               (response, content) => GetPageResult(response, content, requestUri),
               cancellationToken)).ConfigureAwait(false);
    }

    private Page<RepositoryTags> GetPageResult(
        HttpResponseMessage response,
        string content,
        Uri requestUri)
    {
        Page<RepositoryTags> page =
            RegistryClient.GetPageResult<RepositoryTags>(response, content);
        if (page.NextPageLink is null)
        {
            return page;
        }

        Uri nextPageUri = UrlHelper.ResolveSameOrigin(
            this.Client.BaseUri,
            response.RequestMessage?.RequestUri ?? requestUri,
            page.NextPageLink);
        return new Page<RepositoryTags>(page.Value, nextPageUri.AbsoluteUri);
    }
}
