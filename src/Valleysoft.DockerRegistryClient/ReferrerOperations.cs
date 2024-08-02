using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

namespace Valleysoft.DockerRegistryClient;

internal class ReferrerOperations : IReferrerOperations
{
    public ReferrerOperations(RegistryClient client)
    {
        Client = client;
    }

    public RegistryClient Client { get; }

    public async Task<Page<OciImageIndex>> GetAsync(string repositoryName, string digest, string? artifactType = null, CancellationToken cancellationToken = default)
    {
        string url = $"v2/{repositoryName}/referrers/{digest}";
        if (!string.IsNullOrEmpty(artifactType))
        {
            url = $"{url}?artifactType={artifactType}";
        }

        return await GetNextAsync(url, cancellationToken);
    }

    public async Task<Page<OciImageIndex>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            new Uri(UrlHelper.Concat(this.Client.BaseUri.AbsoluteUri, nextPageLink)));

        return await OperationsHelper.HandleNotFoundErrorAsync(
            $"Manifest not found.",
            () => this.Client.SendRequestAsync(
                request,
                RegistryClient.GetPageResult<OciImageIndex>,
                cancellationToken)).ConfigureAwait(false);
    }
}
