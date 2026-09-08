using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

[Trait("Category", "Integration")]
public sealed class OAuthDelegatingHandlerIntegrationTests
{
    [Fact]
    public async Task SendAsync_EarlyUnauthorizedResponse_RetriesUntouchedNonSeekableContent()
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var contentBytes = new byte[] { 1, 2, 3, 4 };
            using var contentStream = new NonSeekableReadStream(contentBytes);
            Task serverTask = RunServerAsync(
                listener,
                port,
                contentStream,
                contentBytes,
                cancellationSource.Token);
            using var transport = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                MaxConnectionsPerServer = 1,
                UseProxy = false
            };
            using var client = new HttpClient(
                new OAuthDelegatingHandler(transport));
            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"http://127.0.0.1:{port}/upload")
            {
                Content = new ReplayableStreamContent(contentStream)
            };
            request.Headers.ExpectContinue = true;

            Task<HttpResponseMessage> responseTask = client.SendAsync(
                request,
                cancellationSource.Token);
            await Task.WhenAll(responseTask, serverTask);
            using HttpResponseMessage response = await responseTask;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(contentBytes.LongLength, contentStream.BytesRead);
        }
        finally
        {
            cancellationSource.Cancel();
            listener.Stop();
        }
    }

    private static async Task RunServerAsync(
        TcpListener listener,
        int port,
        NonSeekableReadStream contentStream,
        byte[] expectedContent,
        CancellationToken cancellationToken)
    {
        using TcpClient firstConnection =
            await listener.AcceptTcpClientAsync(cancellationToken);
        await using (NetworkStream firstStream = firstConnection.GetStream())
        {
            string initialHeaders = await ReadHeadersAsync(
                firstStream,
                cancellationToken);
            Assert.StartsWith("PUT /upload HTTP/1.1", initialHeaders);
            Assert.Contains(
                "Expect: 100-continue",
                initialHeaders,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, contentStream.BytesRead);

            await WriteResponseAsync(
                firstStream,
                "HTTP/1.1 401 Unauthorized\r\n" +
                $"WWW-Authenticate: {HttpBearerChallenge.Bearer} realm=\"http://127.0.0.1:{port}/token\",service=\"registry.example\",scope=\"repository:repo:push\"\r\n" +
                "Content-Length: 1\r\n\r\n",
                cancellationToken);

            byte[] buffer = new byte[1];
            Assert.Equal(
                0,
                await firstStream.ReadAsync(buffer, cancellationToken));
        }

        using TcpClient secondConnection =
            await listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream secondStream = secondConnection.GetStream();

        string tokenHeaders = await ReadHeadersAsync(
            secondStream,
            cancellationToken);
        Assert.StartsWith("GET /token?", tokenHeaders);
        const string tokenResponse = """{"access_token":"access-token"}""";
        await WriteResponseAsync(
            secondStream,
            "HTTP/1.1 200 OK\r\n" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(tokenResponse)}\r\n\r\n" +
            tokenResponse,
            cancellationToken);

        string retryHeaders = await ReadHeadersAsync(
            secondStream,
            cancellationToken);
        Assert.StartsWith("PUT /upload HTTP/1.1", retryHeaders);
        Assert.Contains(
            "Authorization: Bearer access-token",
            retryHeaders,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Transfer-Encoding: chunked",
            retryHeaders,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, contentStream.BytesRead);

        await WriteResponseAsync(
            secondStream,
            "HTTP/1.1 100 Continue\r\n\r\n",
            cancellationToken);
        byte[] actualContent = await ReadChunkedBodyAsync(
            secondStream,
            cancellationToken);
        Assert.Equal(expectedContent, actualContent);

        await WriteResponseAsync(
            secondStream,
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n",
            cancellationToken);
    }

    private static async Task<string> ReadHeadersAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        byte[] buffer = new byte[1];
        while (bytes.Count < 64 * 1024)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                throw new IOException("Connection closed before the HTTP headers were complete.");
            }

            bytes.Add(buffer[0]);
            int count = bytes.Count;
            if (count >= 4 &&
                bytes[count - 4] == '\r' &&
                bytes[count - 3] == '\n' &&
                bytes[count - 2] == '\r' &&
                bytes[count - 1] == '\n')
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }
        }

        throw new InvalidOperationException("HTTP headers exceeded the test server limit.");
    }

    private static async Task<byte[]> ReadChunkedBodyAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        while (true)
        {
            string sizeLine = await ReadLineAsync(stream, cancellationToken);
            int extensionIndex = sizeLine.IndexOf(';');
            string sizeText = extensionIndex < 0
                ? sizeLine
                : sizeLine[..extensionIndex];
            int size = int.Parse(
                sizeText,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            if (size == 0)
            {
                Assert.Equal(string.Empty, await ReadLineAsync(stream, cancellationToken));
                return content.ToArray();
            }

            byte[] chunk = new byte[size];
            await stream.ReadExactlyAsync(chunk, cancellationToken);
            await content.WriteAsync(chunk, cancellationToken);
            Assert.Equal(string.Empty, await ReadLineAsync(stream, cancellationToken));
        }
    }

    private static async Task<string> ReadLineAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        byte[] buffer = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                throw new IOException("Connection closed before the HTTP line was complete.");
            }

            if (buffer[0] == '\n')
            {
                if (bytes.Count == 0 || bytes[^1] != '\r')
                {
                    throw new InvalidOperationException("HTTP line did not end with CRLF.");
                }

                bytes.RemoveAt(bytes.Count - 1);
                return Encoding.ASCII.GetString(bytes.ToArray());
            }

            bytes.Add(buffer[0]);
        }
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        string response,
        CancellationToken cancellationToken)
    {
        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(responseBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
