using System.Net;
using System.Net.Http.Headers;
using Valleysoft.DockerRegistryClient.Credentials;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class RequestHeaderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalContinuation_PreservesExplicitDefaultHeaders(bool injected)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("trace-value", request.Headers.GetValues("X-Custom").Single());
            Assert.Equal("explicit-token", request.Headers.Authorization?.Parameter);
            return true;
        }, new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"repositories":[]}""")
        });
        using var client = injected
            ? new RegistryClient("registry.example", null, new HttpClient(handler), disposeHttpClient: true)
            : new RegistryClient("registry.example", null, handler);
        client.HttpClient.DefaultRequestHeaders.Add("X-Custom", "trace-value");
        client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "explicit-token");

        await client.Catalog.GetNextAsync("https://storage.example/page");

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("explicit-token", client.HttpClient.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Redirect_PreservesCustomHeadersAndClearsAuthorization(bool synchronous)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(_ => true, new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
        {
            Headers = { Location = new Uri("https://storage.example/upload") }
        });
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(new Uri("https://storage.example/upload"), request.RequestUri);
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("request-value", request.Headers.GetValues("X-Custom").Single());
            Assert.Equal("content-value", request.Content!.Headers.GetValues("X-Content").Single());
            Assert.Equal("payload", request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return true;
        }, new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new RegistryClient("registry.example", null, handler);
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://registry.example/upload")
        {
            Content = new StringContent("payload")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "registry-token");
        request.Headers.Add("X-Custom", "request-value");
        request.Content.Headers.Add("X-Content", "content-value");

        using HttpResponseMessage response = synchronous
            ? client.HttpClient.Send(request)
            : await client.HttpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CredentialCallback_CustomHeaderChangesAreNotRolledBackOnRedirect()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(_ => true, new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
        {
            Headers = { Location = new Uri("https://storage.example/upload") }
        });
        handler.AddExpectedRequest(request =>
        {
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("custom-value", request.Headers.GetValues("X-Custom").Single());
            Assert.Equal("application/custom", request.Content!.Headers.ContentType!.MediaType);
            return true;
        }, new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new RegistryClient("registry.example", new CustomCredentials(), handler);
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://registry.example/upload")
        {
            Content = new StringContent("payload")
        };

        await client.SendRequestAsync(request);

        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class CustomCredentials : IRegistryClientCredentials
    {
        public Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "registry-token");
            request.Headers.Add("X-Custom", "custom-value");
            request.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/custom");
            return Task.CompletedTask;
        }
    }
}
