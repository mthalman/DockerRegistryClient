using Microsoft.Rest;
using Microsoft.Rest.Serialization;
using System.Net.Http.Headers;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;
 
internal class ManifestOperations : IServiceOperations<DockerRegistryClient>, IManifestOperations
{
    private const string DockerContentDigestHeader = "Docker-Content-Digest";

    public DockerRegistryClient Client { get; }

    public ManifestOperations(DockerRegistryClient client)
    {
        this.Client = client;
    }

    public Task<HttpOperationResponse<ManifestInfo>> GetWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default) =>
        this.Client.SendRequestAsync(
            CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Get),
            GetResult,
            cancellationToken);

    public Task<HttpOperationResponse<string>> GetDigestWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default) =>
        this.Client.SendRequestAsync(
            CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Head),
            (response, content) => GetDigest(response),
            cancellationToken);

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
                SafeJsonConvert.DeserializeObject<DockerManifestV1>(content)),
            ManifestMediaTypes.DockerManifestSchema2 => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                SafeJsonConvert.DeserializeObject<DockerManifestV2>(content)),
            ManifestMediaTypes.DockerManifestList => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                SafeJsonConvert.DeserializeObject<ManifestList>(content)),
            ManifestMediaTypes.OciManifestSchema1 => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                SafeJsonConvert.DeserializeObject<OciManifest>(content)),
            ManifestMediaTypes.OciManifestList1 => new ManifestInfo(
                mediaType,
                dockerContentDigest,
                SafeJsonConvert.DeserializeObject<OciManifestList>(content)),
            _ => throw new NotSupportedException($"Content type '{mediaType}' not supported."),
        };
    }
}
