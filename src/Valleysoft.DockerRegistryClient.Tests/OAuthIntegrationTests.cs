using System.Net;
using System.Net.Sockets;
using System.Text;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

[Trait("Category", "Integration")]
public sealed class OAuthIntegrationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task CatalogRequest_CompletesBearerTokenFlow()
    {
        await using var server = new OAuthLoopbackServer();
        using RegistryClient client = CreateClient(server);
        using var cancellationSource = new CancellationTokenSource(TestTimeout);

        Page<Catalog> catalog = await GetCatalogAsync(
            client,
            server,
            cancellationSource.Token);

        Assert.Equal(["authenticated/repo"], catalog.Value.RepositoryNames);
        Assert.Collection(
            server.Requests,
            request => AssertCatalogRequest(request, expectedAuthorization: null),
            request =>
            {
                Assert.Equal("GET", request.Method);
                Assert.StartsWith("/token?", request.Target);
                Assert.Contains("service=registry.example", request.Target);
                Assert.Contains("scope=registry%3Acatalog%3A%2A", request.Target);
                Assert.False(request.Headers.ContainsKey("Authorization"));
            },
            request => AssertCatalogRequest(request, "Bearer access-token"));
    }

    [Fact]
    public async Task CatalogRequest_ForwardsBasicCredentialsToTokenEndpoint()
    {
        await using var server = new OAuthLoopbackServer();
        using RegistryClient client = CreateClient(
            server,
            new BasicAuthenticationCredentials("registry-user", "registry-password"));
        using var cancellationSource = new CancellationTokenSource(TestTimeout);

        Page<Catalog> catalog = await GetCatalogAsync(
            client,
            server,
            cancellationSource.Token);

        string basicCredentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("registry-user:registry-password"));
        Assert.Equal(["authenticated/repo"], catalog.Value.RepositoryNames);
        Assert.Collection(
            server.Requests,
            request => AssertCatalogRequest(request, $"Basic {basicCredentials}"),
            request =>
            {
                Assert.Equal("GET", request.Method);
                Assert.StartsWith("/token?", request.Target);
                Assert.Equal($"Basic {basicCredentials}", request.Headers["Authorization"]);
                Assert.Empty(request.Body);
            },
            request => AssertCatalogRequest(request, "Bearer access-token"));
    }

    [Fact]
    public async Task CatalogRequest_ExchangesRefreshTokenWithPost()
    {
        await using var server = new OAuthLoopbackServer("""{"token":"access-token"}""");
        using RegistryClient client = CreateClient(
            server,
            new TokenCredentials("refresh-token"));
        using var cancellationSource = new CancellationTokenSource(TestTimeout);

        Page<Catalog> catalog = await GetCatalogAsync(
            client,
            server,
            cancellationSource.Token);

        Assert.Equal(["authenticated/repo"], catalog.Value.RepositoryNames);
        Assert.Collection(
            server.Requests,
            request => AssertCatalogRequest(request, "Bearer refresh-token"),
            request =>
            {
                Assert.Equal("POST", request.Method);
                Assert.Equal("/token", request.Target);
                Assert.Equal(
                    "application/x-www-form-urlencoded",
                    request.Headers["Content-Type"]);
                Dictionary<string, string> form = ParseForm(request.Body);
                Assert.Equal("registry-client", form["client_id"]);
                Assert.Equal("refresh_token", form["grant_type"]);
                Assert.Equal("refresh-token", form["refresh_token"]);
                Assert.Equal("registry:catalog:*", form["scope"]);
                Assert.Equal("registry.example", form["service"]);
            },
            request => AssertCatalogRequest(request, "Bearer access-token"));
    }

    private static void AssertCatalogRequest(
        LoopbackRequest request,
        string? expectedAuthorization)
    {
        Assert.Equal("GET", request.Method);
        Assert.Equal("/v2/_catalog", request.Target);

        if (expectedAuthorization is null)
        {
            Assert.False(request.Headers.ContainsKey("Authorization"));
        }
        else
        {
            Assert.Equal(expectedAuthorization, request.Headers["Authorization"]);
        }
    }

    private static RegistryClient CreateClient(
        OAuthLoopbackServer server,
        IRegistryClientCredentials? credentials = null) =>
        new(
            server.BaseUri.AbsoluteUri,
            credentials,
            new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 1,
                MaxResponseDrainSize = 0,
                UseProxy = false
            });

    private static async Task<Page<Catalog>> GetCatalogAsync(
        RegistryClient client,
        OAuthLoopbackServer server,
        CancellationToken cancellationToken)
    {
        Page<Catalog> catalog = await client.Catalog.GetAsync(
            cancellationToken: cancellationToken);
        await server.Completion.WaitAsync(TestTimeout);
        return catalog;
    }

    private static Dictionary<string, string> ParseForm(string content) =>
        content.Split('&').ToDictionary(
            pair => Uri.UnescapeDataString(pair[..pair.IndexOf('=')]),
            pair => Uri.UnescapeDataString(pair[(pair.IndexOf('=') + 1)..]));

    private sealed class OAuthLoopbackServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationSource = new();
        private readonly string _tokenResponse;

        public OAuthLoopbackServer(
            string tokenResponse = """{"access_token":"access-token"}""")
        {
            _tokenResponse = tokenResponse;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUri = new Uri($"http://127.0.0.1:{port}/");
            Completion = ServeAsync(_cancellationSource.Token);
        }

        public Uri BaseUri { get; }

        public Task Completion { get; }

        public List<LoopbackRequest> Requests { get; } = [];

        public async ValueTask DisposeAsync()
        {
            await _cancellationSource.CancelAsync();

            try
            {
                await Completion;
            }
            catch (OperationCanceledException) when (_cancellationSource.IsCancellationRequested)
            {
            }
            finally
            {
                _listener.Stop();
                _cancellationSource.Dispose();
            }
        }

        private async Task ServeAsync(CancellationToken cancellationToken)
        {
            for (int requestIndex = 0; requestIndex < 3; requestIndex++)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                NetworkStream stream = client.GetStream();
                Requests.Add(await ReadRequestAsync(stream, cancellationToken));

                if (requestIndex == 0)
                {
                    await WriteChallengeAsync(stream, cancellationToken);
                    await WaitForDisconnectAsync(stream, cancellationToken);
                }
                else
                {
                    await WriteResponseAsync(stream, GetResponse(requestIndex), cancellationToken);
                }
            }
        }

        private string GetResponse(int requestIndex) =>
            requestIndex switch
            {
                1 => CreateResponse(
                    HttpStatusCode.OK,
                    _tokenResponse,
                    "Content-Type: application/json\r\n"),
                2 => CreateResponse(
                    HttpStatusCode.OK,
                    """{"repositories":["authenticated/repo"]}""",
                    "Content-Type: application/json\r\n"),
                _ => throw new InvalidOperationException($"Unexpected request index: {requestIndex}.")
            };

        private async Task WriteChallengeAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            string response =
                "HTTP/1.1 401 Unauthorized\r\n" +
                $"WWW-Authenticate: Bearer realm=\"{BaseUri}token\",service=\"registry.example\",scope=\"registry:catalog:*\"\r\n" +
                "Content-Length: 1024\r\n" +
                "\r\n" +
                "x";
            await WriteResponseAsync(stream, response, cancellationToken);
        }

        private static async Task WaitForDisconnectAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[1];
            int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead != 0)
            {
                throw new InvalidOperationException("Unexpected request data after the OAuth challenge.");
            }
        }

        private static async Task<LoopbackRequest> ReadRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            string requestLine = await reader.ReadLineAsync(cancellationToken) ??
                throw new InvalidOperationException("The request line was not received.");
            string[] requestParts = requestLine.Split(' ', 3);
            if (requestParts.Length != 3)
            {
                throw new InvalidOperationException($"Invalid request line: {requestLine}");
            }

            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } headerLine)
            {
                int separatorIndex = headerLine.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    throw new InvalidOperationException($"Invalid request header: {headerLine}");
                }

                headers[headerLine[..separatorIndex]] = headerLine[(separatorIndex + 1)..].Trim();
            }

            string body = string.Empty;
            if (headers.TryGetValue("Content-Length", out string? contentLength))
            {
                char[] content = new char[int.Parse(contentLength)];
                int offset = 0;
                while (offset < content.Length)
                {
                    int charsRead = await reader.ReadAsync(
                        content.AsMemory(offset),
                        cancellationToken);
                    if (charsRead == 0)
                    {
                        throw new InvalidOperationException("The request body ended unexpectedly.");
                    }

                    offset += charsRead;
                }

                body = new string(content);
            }

            return new LoopbackRequest(requestParts[0], requestParts[1], headers, body);
        }

        private static async Task WriteResponseAsync(
            NetworkStream stream,
            string response,
            CancellationToken cancellationToken)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, cancellationToken);
        }

        private static string CreateResponse(
            HttpStatusCode statusCode,
            string content,
            string additionalHeaders)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            return $"HTTP/1.1 {(int)statusCode} {statusCode}\r\n" +
                additionalHeaders +
                $"Content-Length: {contentBytes.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n" +
                content;
        }
    }

    private sealed record LoopbackRequest(
        string Method,
        string Target,
        IReadOnlyDictionary<string, string> Headers,
        string Body);
}
