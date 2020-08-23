using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;
using Microsoft.Rest;
using Microsoft.Rest.Serialization;

namespace DockerRegistry
{
    internal class ManifestOperations : IServiceOperations<DockerRegistryClient>, IManifestOperations
    {
        private const string DockerContentDigestHeader = "Docker-Content-Digest";

        public DockerRegistryClient Client { get; }

        public ManifestOperations(DockerRegistryClient client)
        {
            this.Client = client;
        }

        public Task<HttpOperationResponse<ManifestInfo>> GetWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
        {
            Uri requestUri = new Uri(this.Client.BaseUri.AbsoluteUri + $"v2/{repositoryName}/manifests/{tagOrDigest}");
            return this.Client.SendRequestAsync(CreateGetRequestMessage(requestUri, HttpMethod.Get), GetResult, cancellationToken);
        }

        public Task<HttpOperationResponse<string>> GetDigestWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
        {
            Uri requestUri = new Uri(this.Client.BaseUri.AbsoluteUri + $"v2/{repositoryName}/manifests/{tagOrDigest}");

            return this.Client.SendRequestAsync(
                CreateGetRequestMessage(requestUri, HttpMethod.Head),
                (response, content) => GetDigest(response),
                cancellationToken);
        }

        private static HttpRequestMessage CreateGetRequestMessage(Uri requestUri, HttpMethod method)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, requestUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.ManifestSchema1));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.ManifestSchema2));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.ManifestList));
            return request;
        }

        private static string GetDigest(HttpResponseMessage response) =>
            response.Headers.GetValues(DockerContentDigestHeader).First();

        private static ManifestInfo GetResult(HttpResponseMessage response, string content)
        {
            string mediaType = response.Content.Headers.ContentType.MediaType;
            string dockerContentDigest = GetDigest(response);

            switch (mediaType)
            {
                case ManifestMediaTypes.ManifestSchema1:
                case ManifestMediaTypes.ManifestSchema1Signed:
                    return new ManifestInfo(
                        mediaType,
                        dockerContentDigest,
                        SafeJsonConvert.DeserializeObject<Manifest_Schema1>(content));
                case ManifestMediaTypes.ManifestSchema2:
                    return new ManifestInfo(
                        mediaType,
                        dockerContentDigest,
                        SafeJsonConvert.DeserializeObject<Manifest_Schema2>(content));
                case ManifestMediaTypes.ManifestList:
                    return new ManifestInfo(
                        mediaType,
                        dockerContentDigest,
                        SafeJsonConvert.DeserializeObject<ManifestList>(content));
                default:
                    throw new NotSupportedException($"Content type '{mediaType}' not supported.");
            }
        }
    }
}
