using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Microsoft.Rest.Serialization;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient
{
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
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.ManifestSchema1));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.ManifestSchema2));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.ManifestList));
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
                ManifestMediaTypes.ManifestSchema1 or ManifestMediaTypes.ManifestSchema1Signed => new ManifestInfo(
                    mediaType,
                    dockerContentDigest,
                    SafeJsonConvert.DeserializeObject<Manifest_Schema1>(content)),
                ManifestMediaTypes.ManifestSchema2 => new ManifestInfo(
                    mediaType,
                    dockerContentDigest,
                    SafeJsonConvert.DeserializeObject<Manifest_Schema2>(content)),
                ManifestMediaTypes.ManifestList => new ManifestInfo(
                    mediaType,
                    dockerContentDigest,
                    SafeJsonConvert.DeserializeObject<ManifestList>(content)),
                _ => throw new NotSupportedException($"Content type '{mediaType}' not supported."),
            };
        }
    }
}
