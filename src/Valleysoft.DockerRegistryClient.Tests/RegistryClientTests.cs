using System.Net;
using System.Text.Json;
using Xunit;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient.Tests;

public class RegistryClientTests
{
    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RegistryClient(null!));
    }

    [Fact]
    public void Constructor_ValidRegistry_SetsProperties()
    {
        var client = new RegistryClient("myregistry.io");
        
        Assert.Equal("myregistry.io", client.Registry);
        Assert.Equal("https://myregistry.io/", client.BaseUri.ToString());
        Assert.NotNull(client.Blobs);
        Assert.NotNull(client.Catalog);
        Assert.NotNull(client.Tags);
        Assert.NotNull(client.Manifests);
        Assert.NotNull(client.Referrers);
    }

    [Fact]
    public void Constructor_WithCustomHttpClient_UsesProvidedClient()
    {
        var mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler);
        
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        Assert.Same(httpClient, client.HttpClient);
    }

    [Fact]
    public void Dispose_DefaultHttpClient_DisposesClient()
    {
        var client = new RegistryClient("myregistry.io");
        
        client.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => client.HttpClient.GetAsync("http://example.com").GetAwaiter().GetResult());
    }

    [Fact]
    public void Dispose_CustomHttpClientNotDisposable_DoesNotDisposeClient()
    {
        var mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient, disposeHttpClient: false);
        
        client.Dispose();
        
        // HttpClient should still be usable
        mockHandler.AddExpectedRequest(HttpMethod.Get, "http://example.com/", new HttpResponseMessage(HttpStatusCode.OK));
        var task = httpClient.GetAsync("http://example.com/");
        task.Wait();
    }

    [Fact]
    public async Task SendRequestCoreAsync_SuccessResponse_ReturnsResponse()
    {
        var mockHandler = new MockHttpMessageHandler();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("success")
        };
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/");
        var result = await client.SendRequestCoreAsync(request);
        
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task SendRequestCoreAsync_ErrorWithJsonBody_ThrowsRegistryException()
    {
        var mockHandler = new MockHttpMessageHandler();
        var errorBody = JsonSerializer.Serialize(new ErrorResult
        {
            Errors = new[]
            {
                new Error { Code = "UNAUTHORIZED", Message = "authentication required" }
            }
        });
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(errorBody),
            ReasonPhrase = "Unauthorized"
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/");
        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.SendRequestCoreAsync(request));
        
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Single(ex.Errors);
        Assert.Equal("UNAUTHORIZED", ex.Errors.First().Code);
        Assert.Equal("authentication required", ex.Errors.First().Message);
    }

    [Fact]
    public async Task SendRequestCoreAsync_ErrorWithXmlBodySingleError_ThrowsRegistryException()
    {
        var mockHandler = new MockHttpMessageHandler();
        var errorBody = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Error><Code>BLOB_UNKNOWN</Code><Message>blob unknown to registry</Message></Error>";
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(errorBody),
            ReasonPhrase = "Not Found"
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/");
        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.SendRequestCoreAsync(request));
        
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Single(ex.Errors);
        Assert.Equal("BLOB_UNKNOWN", ex.Errors.First().Code);
        Assert.Equal("blob unknown to registry", ex.Errors.First().Message);
    }

    [Fact]
    public async Task SendRequestCoreAsync_ErrorWithXmlBodyMultipleErrors_ThrowsRegistryException()
    {
        var mockHandler = new MockHttpMessageHandler();
        var errorBody = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Errors><Error><Code>ERROR1</Code><Message>first error</Message></Error><Error><Code>ERROR2</Code><Message>second error</Message></Error></Errors>";
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(errorBody),
            ReasonPhrase = "Bad Request"
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/");
        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.SendRequestCoreAsync(request));
        
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(2, ex.Errors.Count());
        Assert.Equal("ERROR1", ex.Errors.First().Code);
        Assert.Equal("first error", ex.Errors.First().Message);
        Assert.Equal("ERROR2", ex.Errors.Last().Code);
        Assert.Equal("second error", ex.Errors.Last().Message);
    }

    [Fact]
    public async Task SendRequestCoreAsync_ErrorWithEmptyContent_ThrowsRegistryException()
    {
        var mockHandler = new MockHttpMessageHandler();
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("")
        };
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/");
        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.SendRequestCoreAsync(request));
        
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Empty(ex.Errors);
    }

    [Fact]
    public async Task GetPageResult_WithLinkHeader_ReturnsPageWithNextLink()
    {
        var mockHandler = new MockHttpMessageHandler();
        var responseBody = JsonSerializer.Serialize(new { repositories = new[] { "repo1", "repo2" } });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        response.Headers.Add("Link", "</v2/_catalog?n=2&last=repo2>; rel=\"next\"");
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/_catalog", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/_catalog");
        var result = await client.SendRequestAsync<Page<Catalog>>(request, RegistryClient.GetPageResult<Catalog>);
        
        Assert.NotNull(result.Value);
        Assert.Equal("/v2/_catalog?n=2&last=repo2", result.NextPageLink);
    }

    [Fact]
    public async Task GetPageResult_WithoutLinkHeader_ReturnsPageWithoutNextLink()
    {
        var mockHandler = new MockHttpMessageHandler();
        var responseBody = JsonSerializer.Serialize(new { repositories = new[] { "repo1", "repo2" } });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/_catalog", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/_catalog");
        var result = await client.SendRequestAsync<Page<Catalog>>(request, RegistryClient.GetPageResult<Catalog>);
        
        Assert.NotNull(result.Value);
        Assert.Null(result.NextPageLink);
    }

    [Fact]
    public async Task GetResult_ValidJson_DeserializesCorrectly()
    {
        var mockHandler = new MockHttpMessageHandler();
        var responseBody = JsonSerializer.Serialize(new { repositories = new[] { "repo1", "repo2" } });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/_catalog", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/_catalog");
        var result = await client.SendRequestAsync<Catalog>(request);
        
        Assert.NotNull(result);
        Assert.Equal(2, result.RepositoryNames.Count);
    }

    [Fact]
    public async Task GetResult_NullDeserialization_ThrowsJsonException()
    {
        var mockHandler = new MockHttpMessageHandler();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null")
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/test", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/test");
        await Assert.ThrowsAsync<JsonException>(() => client.SendRequestAsync<Catalog>(request));
    }

    [Fact]
    public async Task GetResult_InvalidJson_ThrowsJsonException()
    {
        var mockHandler = new MockHttpMessageHandler();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json")
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        mockHandler.AddExpectedRequest(HttpMethod.Get, "https://myregistry.io/v2/test", response);
        
        var httpClient = new HttpClient(mockHandler);
        var client = new RegistryClient("myregistry.io", null, httpClient);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://myregistry.io/v2/test");
        await Assert.ThrowsAsync<JsonException>(() => client.SendRequestAsync<Catalog>(request));
    }
}
