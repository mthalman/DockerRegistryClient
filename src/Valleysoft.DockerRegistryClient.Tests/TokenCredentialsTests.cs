using Xunit;
using Valleysoft.DockerRegistryClient.Credentials;

namespace Valleysoft.DockerRegistryClient.Tests;

public class TokenCredentialsTests
{
    [Fact]
    public async Task ProcessHttpRequestAsync_SetsBearerTokenHeader()
    {
        var credentials = new TokenCredentials("mytoken123");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://registry.io/v2/");
        
        await credentials.ProcessHttpRequestAsync(request);
        
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal("mytoken123", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ProcessHttpRequestAsync_CustomTokenType_SetsCustomScheme()
    {
        var credentials = new TokenCredentials("customtoken", "Custom");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://registry.io/v2/");
        
        await credentials.ProcessHttpRequestAsync(request);
        
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Custom", request.Headers.Authorization.Scheme);
        Assert.Equal("customtoken", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var credentials = new TokenCredentials("token456", "MyTokenType");
        
        Assert.Equal("token456", credentials.Token);
        Assert.Equal("MyTokenType", credentials.TokenType);
    }

    [Fact]
    public void Constructor_DefaultTokenType_IsBearer()
    {
        var credentials = new TokenCredentials("token789");
        
        Assert.Equal("token789", credentials.Token);
        Assert.Equal("Bearer", credentials.TokenType);
    }
}
