using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

using var httpClient = new HttpClient(new SmokeRegistryHandler());
using var client = new RegistryClient(Constants.Registry, serviceClientCredentials: null, httpClient, disposeHttpClient: true);

Page<Valleysoft.DockerRegistryClient.Models.Catalog> catalog = await client.Catalog.GetAsync();
AssertEqual(Constants.Repository, catalog.Value.RepositoryNames.Single(), "catalog repository");

Page<Valleysoft.DockerRegistryClient.Models.RepositoryTags> tags = await client.Tags.GetAsync(Constants.Repository);
AssertEqual("latest", tags.Value.Tags.Single(), "repository tag");

ManifestInfo manifestInfo = await client.Manifests.GetAsync(Constants.Repository, "latest");
AssertEqual(Constants.ManifestDigest, manifestInfo.DockerContentDigest, "manifest digest");
if (manifestInfo.Manifest is not OciImageManifest { Subject.Digest: Constants.SubjectDigest })
{
    throw new InvalidOperationException("Expected an OCI image manifest with the configured subject.");
}

Page<OciImageIndex> referrers = await client.Referrers.GetAsync(Constants.Repository, Constants.SubjectDigest, Constants.ArtifactType);
Valleysoft.DockerRegistryClient.Models.Manifests.Oci.ManifestReference referrer = referrers.Value.Manifests.Single();
AssertEqual(Constants.ManifestDigest, referrer.Digest, "fallback referrer digest");
AssertEqual(Constants.ArtifactType, referrer.ArtifactType, "fallback referrer artifact type");

var publishedManifest = new OciImageManifest
{
    ArtifactType = Constants.ArtifactType,
    Config = new OciDescriptor
    {
        MediaType = Constants.ArtifactType,
        Digest = Constants.SubjectDigest,
        Size = 0
    },
    Subject = new OciDescriptor
    {
        MediaType = ManifestMediaTypes.OciManifestSchema1,
        Digest = Constants.SubjectDigest,
        Size = 1
    },
    Annotations = new Dictionary<string, string>
    {
        ["org.example.created-by"] = "aot-smoke"
    }
};

ManifestPublishResult publishResult = await client.Manifests.PublishAsync(Constants.Repository, "artifact", publishedManifest);
AssertEqual($"https://{Constants.Registry}/v2/{Constants.Repository}/manifests/artifact", publishResult.Location, "publish location");

var customManifest = new CustomManifest
{
    MediaType = "application/vnd.example.custom",
    CustomValue = "aot-safe"
};
ManifestPublishResult customPublishResult = await client.Manifests.PublishAsync(
    Constants.Repository,
    "custom",
    customManifest,
    SmokeJsonContext.Default.CustomManifest);
AssertEqual($"https://{Constants.Registry}/v2/{Constants.Repository}/manifests/custom", customPublishResult.Location, "custom publish location");

using var oauthServer = new CredentialedOAuthRegistryServer();
using var oauthClient = new RegistryClient(
    $"http://127.0.0.1:{oauthServer.Port}",
    new BasicAuthenticationCredentials("aot-user", "aot-password"));
Page<Valleysoft.DockerRegistryClient.Models.Catalog> authenticatedCatalog = await oauthClient.Catalog.GetAsync();
AssertEqual(Constants.Repository, authenticatedCatalog.Value.RepositoryNames.Single(), "authenticated catalog repository");
await oauthServer.Completion;
AssertEqualCount(3, oauthServer.RequestCount, "OAuth challenge, token, and retry request count");

static void AssertEqual(string expected, string? actual, string name)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected {name} '{expected}', but got '{actual}'.");
    }
}

static void AssertEqualCount(int expected, int actual, string name)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"Expected {name} '{expected}', but got '{actual}'.");
    }
}

sealed class SmokeRegistryHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(Handle(request));

    private static HttpResponseMessage Handle(HttpRequestMessage request)
    {
        string pathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
        return (request.Method.Method, pathAndQuery) switch
        {
            ("GET", "/v2/_catalog") => Json(HttpStatusCode.OK, "{\"repositories\":[\"library/alpine\"]}"),
            ("GET", "/v2/library/alpine/tags/list") => Json(HttpStatusCode.OK, "{\"name\":\"library/alpine\",\"tags\":[\"latest\"]}"),
            ("GET", "/v2/library/alpine/manifests/latest") => Manifest(JsonManifest(), Constants.ManifestDigest),
            ("GET", "/v2/library/alpine/referrers/sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa?artifactType=application%2Fvnd.example.artifact") => Error(HttpStatusCode.NotFound),
            ("GET", "/v2/library/alpine/manifests/sha256-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") => Manifest(JsonReferrersIndex(), "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc", ManifestMediaTypes.OciImageIndex1),
            ("PUT", "/v2/library/alpine/manifests/artifact") => CreatedManifest(request, verifyReferrersFallback: false),
            ("GET", "/v2/library/alpine/referrers/sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") => Error(HttpStatusCode.NotFound),
            ("PUT", "/v2/library/alpine/manifests/sha256-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") => CreatedManifest(request, verifyReferrersFallback: true),
            ("PUT", "/v2/library/alpine/manifests/custom") => CreatedCustomManifest(request),
            _ => throw new InvalidOperationException($"Unexpected request: {request.Method} {pathAndQuery}")
        };
    }

    private static HttpResponseMessage CreatedManifest(HttpRequestMessage request, bool verifyReferrersFallback)
    {
        if (verifyReferrersFallback)
        {

            string body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
            if (!body.Contains("sha256:", StringComparison.Ordinal) || !body.Contains(Constants.ArtifactType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The referrers fallback index did not contain the published manifest descriptor.");
            }
        }

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri($"https://{Constants.Registry}/v2/{Constants.Repository}/manifests/artifact");
        return response;
    }

    private static HttpResponseMessage CreatedCustomManifest(HttpRequestMessage request)
    {
        string body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
        if (!body.Contains("\"customValue\":\"aot-safe\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The custom manifest was not serialized with its supplied JSON metadata.");
        }

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri($"https://{Constants.Registry}/v2/{Constants.Repository}/manifests/custom");
        return response;
    }

    private static HttpResponseMessage Manifest(string json, string digest, string mediaType = ManifestMediaTypes.OciManifestSchema1)
    {
        HttpResponseMessage response = Json(HttpStatusCode.OK, json, mediaType);
        response.Headers.Add("Docker-Content-Digest", digest);
        return response;
    }

    private static HttpResponseMessage Error(HttpStatusCode statusCode) =>
        Json(statusCode, "{\"errors\":[{\"code\":\"MANIFEST_UNKNOWN\",\"message\":\"manifest unknown\"}]}");

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json, string mediaType = "application/json") =>
        new(statusCode)
        {
            Content = Content(json, mediaType)
        };

    private static StringContent Content(string json, string mediaType)
    {
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return content;
    }

    private static string JsonManifest() =>
        """
        {
          "schemaVersion": 2,
          "mediaType": "application/vnd.oci.image.manifest.v1+json",
          "artifactType": "application/vnd.example.artifact",
          "config": {
            "mediaType": "application/vnd.example.artifact",
            "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "size": 0
          },
          "layers": [],
          "subject": {
            "mediaType": "application/vnd.oci.image.manifest.v1+json",
            "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "size": 1
          },
          "annotations": {
            "org.example.created-by": "aot-smoke"
          }
        }
        """;

    private static string JsonReferrersIndex() =>
        """
        {
          "schemaVersion": 2,
          "mediaType": "application/vnd.oci.image.index.v1+json",
          "manifests": [
            {
              "mediaType": "application/vnd.oci.image.manifest.v1+json",
              "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
              "size": 123,
              "artifactType": "application/vnd.example.artifact"
            }
          ]
        }
        """;
}

sealed class CredentialedOAuthRegistryServer : IDisposable
{
    private static readonly string BasicAuthorization =
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("aot-user:aot-password"));
    private const string BearerAuthorization = "Bearer aot-smoke-access-token";
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private int requestCount;

    public CredentialedOAuthRegistryServer()
    {
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Completion = ServeAsync();
    }

    public int Port { get; }

    public int RequestCount => requestCount;

    public Task Completion { get; }

    private async Task ServeAsync()
    {
        for (int i = 0; i < 3; i++)
        {
            using TcpClient connection = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

            string requestLine = await reader.ReadLineAsync() ??
                throw new InvalidOperationException("The local OAuth smoke server received an empty request.");
            string? authorization = null;
            string? header;
            while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync()))
            {
                if (header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    authorization = header["Authorization:".Length..].Trim();
                }
            }

            requestCount++;
            string[] requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestParts.Length != 3 || requestParts[0] != "GET")
            {
                throw new InvalidOperationException($"Unexpected local OAuth request: {requestLine}");
            }

            if (requestParts[1] == "/v2/_catalog" && authorization == BasicAuthorization)
            {
                await WriteResponseAsync(
                    stream,
                    HttpStatusCode.Unauthorized,
                    string.Empty,
                    $"WWW-Authenticate: Bearer realm=\"http://127.0.0.1:{Port}/token\",service=\"registry.example\",scope=\"registry:catalog:*\"\r\n");
            }
            else if (requestParts[1].StartsWith("/token?", StringComparison.Ordinal) &&
                Uri.UnescapeDataString(requestParts[1]) == "/token?service=registry.example&scope=registry:catalog:*" &&
                authorization == BasicAuthorization)
            {
                await WriteResponseAsync(
                    stream,
                    HttpStatusCode.OK,
                    "{\"access_token\":\"aot-smoke-access-token\"}",
                    "Content-Type: application/json\r\n");
            }
            else if (requestParts[1] == "/v2/_catalog" && authorization == BearerAuthorization)
            {
                await WriteResponseAsync(
                    stream,
                    HttpStatusCode.OK,
                    "{\"repositories\":[\"library/alpine\"]}",
                    "Content-Type: application/json\r\n");
            }
            else
            {
                throw new InvalidOperationException($"Unexpected local OAuth request or credentials: {requestLine}");
            }
        }
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        HttpStatusCode statusCode,
        string body,
        string additionalHeaders)
    {
        byte[] content = Encoding.UTF8.GetBytes(body);
        string reason = statusCode == HttpStatusCode.Unauthorized ? "Unauthorized" : "OK";
        byte[] headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(int)statusCode} {reason}\r\n" +
            $"Content-Length: {content.Length}\r\n" +
            "Connection: close\r\n" +
            additionalHeaders +
            "\r\n");
        await stream.WriteAsync(headers);
        await stream.WriteAsync(content);
    }

    public void Dispose() => listener.Stop();

}

static class Constants
{
    public const string Registry = "registry.example";
    public const string Repository = "library/alpine";
    public const string SubjectDigest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string ManifestDigest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string ArtifactType = "application/vnd.example.artifact";
}

sealed class CustomManifest : Manifest
{
    [JsonPropertyName("customValue")]
    public string? CustomValue { get; set; }
}

[JsonSerializable(typeof(CustomManifest))]
partial class SmokeJsonContext : JsonSerializerContext
{
}
