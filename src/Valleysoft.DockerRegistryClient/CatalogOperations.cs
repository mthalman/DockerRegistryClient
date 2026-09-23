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
        Uri uri = RegistryUriBuilder.Catalog(Client.BaseUri, count);
        return await GetNextAsync(uri.AbsoluteUri, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Page<Catalog>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default)
    {
        Uri requestUri = new(this.Client.BaseUri, nextPageLink);
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            requestUri);

        return await OperationsHelper.HandleNotFoundErrorAsync(
            "Catalog page not found.",
            () => this.Client.SendRequestAsync(
                request,
                (response, content) => GetPageResult(response, content, request.RequestUri!),
                cancellationToken,
                applyCredentials: RegistryUriBuilder.HasSameOrigin(Client.BaseUri, requestUri))).ConfigureAwait(false);
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

        Uri nextPageUri = new(
            response.RequestMessage?.RequestUri ?? requestUri,
            page.NextPageLink);
        return new Page<Catalog>(page.Value, nextPageUri.AbsoluteUri);
    }
}
