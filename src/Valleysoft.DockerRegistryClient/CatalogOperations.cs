using Microsoft.Rest;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;
 
internal class CatalogOperations : IServiceOperations<RegistryClient>, ICatalogOperations
{
    public RegistryClient Client { get; }

    public CatalogOperations(RegistryClient client)
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
        string? nextLink = RegistryClient.GetNextLinkUrl(response);
        return new Page<Catalog>(RegistryClient.GetResult<Catalog>(response, content), nextLink);
    }
}
