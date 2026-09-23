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
        Uri uri = RegistryUriBuilder.Tags(Client.BaseUri, repositoryName, count);
        return await GetNextAsync(uri.AbsoluteUri, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Page<RepositoryTags>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default)
    {
        Uri requestUri = new(this.Client.BaseUri, nextPageLink);
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            requestUri);

        return await OperationsHelper.HandleNotFoundErrorAsync(
           "Repository not found.",
           () => this.Client.SendRequestAsync(
               request,
               (response, content) => GetPageResult(response, content, request.RequestUri!),
               cancellationToken,
               applyCredentials: RegistryUriBuilder.HasSameOrigin(Client.BaseUri, requestUri))).ConfigureAwait(false);
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

        Uri nextPageUri = new(
            response.RequestMessage?.RequestUri ?? requestUri,
            page.NextPageLink);
        return new Page<RepositoryTags>(page.Value, nextPageUri.AbsoluteUri);
    }
}
