using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class HttpBearerChallengeTests
{
    [Fact]
    public void Parse_ValidChallenge_ReturnsChallenge()
    {
        string challenge = "realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\",scope=\"repository:library/hello-world:pull\"";
        
        var result = HttpBearerChallenge.Parse(challenge);
        
        Assert.Equal("https://auth.docker.io/token", result.Realm);
        Assert.Equal("registry.docker.io", result.Service);
        Assert.Equal("repository:library/hello-world:pull", result.Scope);
    }

    [Fact]
    public void Parse_DifferentParameterOrder_ReturnsChallenge()
    {
        string challenge = "scope=\"repository:library/ubuntu:pull\",realm=\"https://auth.example.com\",service=\"example.io\"";
        
        var result = HttpBearerChallenge.Parse(challenge);
        
        Assert.Equal("https://auth.example.com", result.Realm);
        Assert.Equal("example.io", result.Service);
        Assert.Equal("repository:library/ubuntu:pull", result.Scope);
    }

    [Fact]
    public void Parse_MissingRealm_ThrowsArgumentException()
    {
        string challenge = "service=\"registry.docker.io\",scope=\"repository:library/hello-world:pull\"";
        
        var ex = Assert.Throws<ArgumentException>(() => HttpBearerChallenge.Parse(challenge));
        Assert.Contains("realm", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MissingService_ThrowsArgumentException()
    {
        string challenge = "realm=\"https://auth.docker.io/token\",scope=\"repository:library/hello-world:pull\"";
        
        var ex = Assert.Throws<ArgumentException>(() => HttpBearerChallenge.Parse(challenge));
        Assert.Contains("service", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MissingScope_ThrowsArgumentException()
    {
        string challenge = "realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\"";
        
        var ex = Assert.Throws<ArgumentException>(() => HttpBearerChallenge.Parse(challenge));
        Assert.Contains("scope", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NullChallenge_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => HttpBearerChallenge.Parse(null));
    }

    [Fact]
    public void Parse_EmptyChallenge_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => HttpBearerChallenge.Parse(string.Empty));
    }

    [Fact]
    public void Parse_MalformedChallenge_ThrowsArgumentException()
    {
        string challenge = "not a valid challenge format";
        
        Assert.Throws<ArgumentException>(() => HttpBearerChallenge.Parse(challenge));
    }

    [Fact]
    public void Parse_PartiallyMalformed_ThrowsArgumentException()
    {
        string challenge = "realm=\"https://auth.docker.io/token\",service=registry.docker.io,scope=\"repository:library/hello-world:pull\"";
        
        Assert.Throws<ArgumentException>(() => HttpBearerChallenge.Parse(challenge));
    }
}
