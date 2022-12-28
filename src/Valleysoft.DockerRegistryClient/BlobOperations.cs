using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient;

internal class BlobOperations : IServiceOperations<RegistryClient>, IBlobOperations
{
    public RegistryClient Client { get; }

    public BlobOperations(RegistryClient client)
    {
        this.Client = client;
    }

    public async Task<HttpOperationResponse<Stream>> GetWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{this.Client.BaseUri.AbsoluteUri}/v2/{repositoryName}/blobs/{digest}");
        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await RegistryClient.GetStreamContentAsync(request, response).ConfigureAwait(false);
    }

    public async Task<HttpOperationResponse<bool>> ExistsWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Head, $"{this.Client.BaseUri.AbsoluteUri}/v2/{repositoryName}/blobs/{digest}");
        HttpResponseMessage response = await this.Client.SendRequestAsync(request, ignoreUnsuccessfulResponse: true, cancellationToken).ConfigureAwait(false);
        return new HttpOperationResponse<bool>
        {
            Body = response.IsSuccessStatusCode,
            Request = request,
            Response = response
        };
    }
}
