using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

internal class CatalogOperations : ICatalogOperations
{
    public RegistryClient Client { get; }

    public CatalogOperations(RegistryClient client)
    {
        this.Client = client;
    }

    public async Task<Page<Catalog>> GetAsync(int? count = null, CancellationToken cancellationToken = default)
    {
        string url = UrlHelper.ApplyCount($"v2/_catalog", count);
        return await GetNextAsync(url, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Page<Catalog>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default)
    {
        Uri requestUri = UrlHelper.ResolveSameOrigin(this.Client.BaseUri, nextPageLink);
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            requestUri);
        RedirectDelegatingHandler.RequireSameOrigin(request, this.Client.BaseUri);

        return await OperationsHelper.HandleNotFoundErrorAsync(
            "Catalog page not found.",
            () => this.Client.SendRequestAsync(
                request,
                (response, content) => GetPageResult(response, content, requestUri),
                cancellationToken)).ConfigureAwait(false);
    }

    private Page<Catalog> GetPageResult(
        HttpResponseMessage response,
        string content,
        Uri requestUri)
    {
        Page<Catalog> page = RegistryClient.GetPageResult<Catalog>(response, content);
        if (page.NextPageLink is null)
        {
            return page;
        }

        Uri nextPageUri = UrlHelper.ResolveSameOrigin(
            this.Client.BaseUri,
            response.RequestMessage?.RequestUri ?? requestUri,
            page.NextPageLink);
        return new Page<Catalog>(page.Value, nextPageUri.AbsoluteUri);
    }
}
