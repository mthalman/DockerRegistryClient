using System.Net;
using System.Net.Sockets;
using System.Text;
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
        using var client = new RegistryClient(
            server.BaseUri.AbsoluteUri,
            serviceClientCredentials: null,
            new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 1,
                MaxResponseDrainSize = 0,
                UseProxy = false
            });
        using var cancellationSource = new CancellationTokenSource(TestTimeout);

        Page<Catalog> catalog = await client.Catalog.GetAsync(
            cancellationToken: cancellationSource.Token);
        await server.Completion.WaitAsync(TestTimeout);

        Assert.Equal(["authenticated/repo"], catalog.Value.RepositoryNames);
        Assert.Collection(
            server.Requests,
            request =>
            {
                Assert.Equal("GET", request.Method);
                Assert.Equal("/v2/_catalog", request.Target);
                Assert.False(request.Headers.ContainsKey("Authorization"));
            },
            request =>
            {
                Assert.Equal("GET", request.Method);
                Assert.StartsWith("/token?", request.Target);
                Assert.Contains("service=registry.example", request.Target);
                Assert.Contains("scope=registry:catalog:*", request.Target);
                Assert.False(request.Headers.ContainsKey("Authorization"));
            },
            request =>
            {
                Assert.Equal("GET", request.Method);
                Assert.Equal("/v2/_catalog", request.Target);
                Assert.Equal("Bearer access-token", request.Headers["Authorization"]);
            });
    }

    private sealed class OAuthLoopbackServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationSource = new();

        public OAuthLoopbackServer()
        {
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
                    """{"access_token":"access-token"}""",
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

            return new LoopbackRequest(requestParts[0], requestParts[1], headers);
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
        IReadOnlyDictionary<string, string> Headers);
}
