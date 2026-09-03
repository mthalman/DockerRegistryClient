using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Docker;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

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
            async () =>
            {
                using HttpResponseMessage response = await this.Client.SendRequestCoreAsync(
                    request,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

#if NET5_0_OR_GREATER
                byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#else
                byte[] content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif

                return GetResult(response, content);
            }).ConfigureAwait(false);
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
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.DockerManifestSchema2));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.DockerManifestList));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.OciManifestSchema1));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.OciImageIndex1));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return request;
    }

    private static string GetDigest(HttpResponseMessage response) =>
        response.Headers.GetValues(DockerContentDigestHeader).First();

    private static ManifestInfo GetResult(HttpResponseMessage response, byte[] content)
    {
        if (response.Content is null)
        {
            throw new InvalidOperationException($"Response content is null.");
        }

        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        string dockerContentDigest = GetDigest(response);

        if (mediaType is null)
        {
            throw new InvalidOperationException("Response content type is not set.");
        }

        IManifest manifest;
        if (mediaType.Equals(ManifestMediaTypes.DockerManifestSchema2, StringComparison.OrdinalIgnoreCase))
        {
            manifest = Deserialize<DockerManifest>(content);
        }
        else if (mediaType.Equals(ManifestMediaTypes.DockerManifestList, StringComparison.OrdinalIgnoreCase))
        {
            manifest = Deserialize<ManifestList>(content);
        }
        else if (mediaType.Equals(ManifestMediaTypes.OciManifestSchema1, StringComparison.OrdinalIgnoreCase))
        {
            manifest = Deserialize<OciImageManifest>(content);
        }
        else if (mediaType.Equals(ManifestMediaTypes.OciImageIndex1, StringComparison.OrdinalIgnoreCase))
        {
            manifest = Deserialize<OciImageIndex>(content);
        }
        else
        {
            manifest = new RawManifest(mediaType, content);
        }

        return new ManifestInfo(mediaType, dockerContentDigest, manifest, content);
    }

    private static T Deserialize<T>(byte[] content)
        where T : IManifest
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content) ??
                throw new JsonException($"Unable to deserialize content:{Environment.NewLine}{Encoding.UTF8.GetString(content)}");
        }
        catch (JsonException exception)
        {
            throw new JsonException(
                $"Unable to deserialize the response:{Environment.NewLine}{Encoding.UTF8.GetString(content)}",
                exception);
        }
    }
}
