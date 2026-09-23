using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

[CollectionDefinition("Default transport", DisableParallelization = true)]
public class DefaultTransportCollection;

[Collection("Default transport")]
public class DefaultTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpLoopback_SupportsSyncAndAsyncRequests(bool synchronous)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string origin = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        IWebProxy savedProxy = HttpClient.DefaultProxy;
        try
        {
            HttpClient.DefaultProxy = new WebProxy();
            using var client = new RegistryClient(origin);
            Task server = ServeAsync();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{origin}/v2/%41?state=%7E#client-fragment");

            using HttpResponseMessage response = synchronous
                ? client.HttpClient.Send(request, timeout.Token)
                : await client.HttpClient.SendAsync(request, timeout.Token);
            await server;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("ok", await response.Content.ReadAsStringAsync(timeout.Token));
        }
        finally
        {
            HttpClient.DefaultProxy = savedProxy;
        }

        async Task ServeAsync()
        {
            using TcpClient connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using NetworkStream stream = connection.GetStream();
            string headers = await ReadHeadersAsync(stream, timeout.Token);
            Assert.StartsWith("GET /v2/A?state=~ HTTP/1.1\r\n", headers);
            Assert.DoesNotContain("client-fragment", headers);
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"),
                timeout.Token);
        }
    }

    [Fact]
    public async Task DefaultProxy_ForwardsHostnameWithoutLocalDnsResolution()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        IWebProxy savedProxy = HttpClient.DefaultProxy;
        try
        {
            HttpClient.DefaultProxy = new WebProxy($"http://127.0.0.1:{port}");
            using var client = new RegistryClient("registry.example");
            Task server = ServeAsync();

            HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => client.HttpClient.GetAsync("https://proxy-only.invalid/v2/", timeout.Token));
            await server;

            Assert.Equal(HttpRequestError.ProxyTunnelError, exception.HttpRequestError);
        }
        finally
        {
            HttpClient.DefaultProxy = savedProxy;
        }

        async Task ServeAsync()
        {
            using TcpClient connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using NetworkStream stream = connection.GetStream();
            string headers = await ReadHeadersAsync(stream, timeout.Token);
            Assert.StartsWith("CONNECT proxy-only.invalid:443 HTTP/1.1\r\n", headers);
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"),
                timeout.Token);
        }
    }

    private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        byte[] buffer = new byte[1];
        while (bytes.Count < 32 * 1024)
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken);
            bytes.Add(buffer[0]);
            int count = bytes.Count;
            if (count >= 4 && bytes[count - 4] == '\r' && bytes[count - 3] == '\n' &&
                bytes[count - 2] == '\r' && bytes[count - 1] == '\n')
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }
        }

        throw new InvalidDataException("Request headers exceeded the test limit.");
    }
}
