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
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            new Uri(UrlHelper.Concat(this.Client.BaseUri.AbsoluteUri, nextPageLink)));

        return await OperationsHelper.HandleNotFoundErrorAsync(
            "Catalog page not found.",
            () => this.Client.SendRequestAsync(
                request,
                GetResult,
                cancellationToken)).ConfigureAwait(false);
    }
        
    private static Page<Catalog> GetResult(HttpResponseMessage response, string content)
    {
        string? nextLink = RegistryClient.GetNextLinkUrl(response);
        return new Page<Catalog>(RegistryClient.GetResult<Catalog>(response, content), nextLink);
    }
}
