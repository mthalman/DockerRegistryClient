using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient;
 
internal class BlobOperations : IServiceOperations<DockerRegistryClient>, IBlobOperations
{
    public DockerRegistryClient Client { get; }

    public BlobOperations(DockerRegistryClient client)
    {
        this.Client = client;
    }

    public async Task<HttpOperationResponse<Stream>> GetWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{this.Client.BaseUri.AbsoluteUri}/v2/{repositoryName}/blobs/{digest}");
        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return await DockerRegistryClient.GetStreamContentAsync(request, response).ConfigureAwait(false);
    }
}
