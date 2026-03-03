using System.Net.Http.Headers;
using Xunit;
using Valleysoft.DockerRegistryClient.Credentials;

namespace Valleysoft.DockerRegistryClient.Tests;

public class BasicAuthenticationCredentialsTests
{
    [Fact]
    public async Task ProcessHttpRequestAsync_SetsBasicAuthHeader()
    {
        var credentials = new BasicAuthenticationCredentials("user", "pass");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://registry.io/v2/");
        
        await credentials.ProcessHttpRequestAsync(request);
        
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Basic", request.Headers.Authorization.Scheme);
        // "user:pass" in Base64 is "dXNlcjpwYXNz"
        Assert.Equal("dXNlcjpwYXNz", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ProcessHttpRequestAsync_WithNullValues_SetsHeaderWithEmptyValues()
    {
        var credentials = new BasicAuthenticationCredentials(null, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://registry.io/v2/");
        
        await credentials.ProcessHttpRequestAsync(request);
        
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Basic", request.Headers.Authorization.Scheme);
        // ":" in Base64 is "Og=="
        Assert.Equal("Og==", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var credentials = new BasicAuthenticationCredentials("myuser", "mypassword");
        
        Assert.Equal("myuser", credentials.UserName);
        Assert.Equal("mypassword", credentials.Password);
    }
}
