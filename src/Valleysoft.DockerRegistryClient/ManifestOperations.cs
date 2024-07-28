using Newtonsoft.Json;
using System.Net.Http.Headers;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

internal class ManifestOperations : IManifestOperations
{
    private const string NotFoundMessage = "Manifest not found.";
    private const string DockerContentDigestHeader = "Docker-Content-Digest";

    public RegistryClient Client { get; }

    public ManifestOperations(RegistryClient client)
    {
        this.Client = client;
    }

    public async Task<ManifestInfo> GetAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Get);

        return await OperationsHelper.HandleNotFoundErrorAsync(
            NotFoundMessage,
            () => this.Client.SendRequestAsync(
                request,
                GetResult,
                cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Head);
        return await this.Client.SendExistsRequestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetDigestAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Head);
        return await OperationsHelper.HandleNotFoundErrorAsync(
            NotFoundMessage,
            () => this.Client.SendRequestAsync(
                request,
                (response, content) => GetDigest(response),
                cancellationToken)).ConfigureAwait(false);
    }

    public async Task<string> GetDigestWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Head);
        return await OperationsHelper.HandleNotFoundErrorAsync(
            NotFoundMessage,
            () => this.Client.SendRequestAsync(
                request,
                (response, content) => GetDigest(response),
                cancellationToken)).ConfigureAwait(false);
    }
    
    private Uri GetManifestUri(string repositoryName, string tagOrDigest) =>
        new(this.Client.BaseUri.AbsoluteUri + $"v2/{repositoryName}/manifests/{tagOrDigest}");

    private static HttpRequestMessage CreateGetRequestMessage(Uri requestUri, HttpMethod method)
    {
        HttpRequestMessage request = new(method, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.DockerManifestSchema1));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.DockerManifestSchema2));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.DockerManifestList));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.OciManifestSchema1));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.OciManifestList1));
        return request;
    }

    private static string GetDigest(HttpResponseMessage response) =>
        response.Headers.GetValues(DockerContentDigestHeader).First();

    private static ManifestInfo GetResult(HttpResponseMessage response, string content)
    {
        if (response.Content is null)
        {
            throw new InvalidOperationException($"Response content is null.");
        }

        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        string dockerContentDigest = GetDigest(response);

        return mediaType switch
        {
            ManifestMediaTypes.DockerManifestSchema1 or ManifestMediaTypes.DockerManifestSchema1Signed => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                JsonConvert.DeserializeObject<DockerManifestV1>(content) ?? throw new JsonSerializationException($"Unable to deserialize content:{Environment.NewLine}{content}")),
            ManifestMediaTypes.DockerManifestSchema2 => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                JsonConvert.DeserializeObject<DockerManifestV2>(content) ?? throw new JsonSerializationException($"Unable to deserialize content:{Environment.NewLine}{content}")),
            ManifestMediaTypes.DockerManifestList => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                JsonConvert.DeserializeObject<ManifestList>(content) ?? throw new JsonSerializationException($"Unable to deserialize content:{Environment.NewLine}{content}")),
            ManifestMediaTypes.OciManifestSchema1 => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                JsonConvert.DeserializeObject<OciManifest>(content) ?? throw new JsonSerializationException($"Unable to deserialize content:{Environment.NewLine}{content}")),
            ManifestMediaTypes.OciManifestList1 => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                JsonConvert.DeserializeObject<OciManifestList>(content) ?? throw new JsonSerializationException($"Unable to deserialize content:{Environment.NewLine}{content}")),
            _ => throw new NotSupportedException($"Content type '{mediaType}' not supported."),
        };
    }
}
