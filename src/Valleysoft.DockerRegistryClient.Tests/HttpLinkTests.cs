using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class HttpLinkTests
{
    [Fact]
    public void TryParse_ValidLinkHeader_ReturnsTrue()
    {
        string linkHeader = "<https://registry.io/v2/repo/tags/list?n=10&last=tag1>; rel=\"next\"";
        
        bool success = HttpLink.TryParse(linkHeader, out HttpLink? link);
        
        Assert.True(success);
        Assert.NotNull(link);
        Assert.Equal("https://registry.io/v2/repo/tags/list?n=10&last=tag1", link.Url);
        Assert.Equal("next", link.Relationship);
    }

    [Fact]
    public void TryParse_LinkWithDifferentRelationship_ReturnsTrue()
    {
        string linkHeader = "<https://example.com/page2>; rel=\"prev\"";
        
        bool success = HttpLink.TryParse(linkHeader, out HttpLink? link);
        
        Assert.True(success);
        Assert.NotNull(link);
        Assert.Equal("https://example.com/page2", link.Url);
        Assert.Equal("prev", link.Relationship);
    }

    [Fact]
    public void TryParse_RelativeUrl_ReturnsTrue()
    {
        string linkHeader = "</v2/repo/tags/list?n=10&last=tag1>; rel=\"next\"";
        
        bool success = HttpLink.TryParse(linkHeader, out HttpLink? link);
        
        Assert.True(success);
        Assert.NotNull(link);
        Assert.Equal("/v2/repo/tags/list?n=10&last=tag1", link.Url);
        Assert.Equal("next", link.Relationship);
    }

    [Fact]
    public void TryParse_InvalidFormat_ReturnsFalse()
    {
        string linkHeader = "not a valid link header";
        
        bool success = HttpLink.TryParse(linkHeader, out HttpLink? link);
        
        Assert.False(success);
        Assert.Null(link);
    }

    [Fact]
    public void TryParse_MissingAngleBrackets_ReturnsFalse()
    {
        string linkHeader = "https://example.com/page2; rel=\"next\"";
        
        bool success = HttpLink.TryParse(linkHeader, out HttpLink? link);
        
        Assert.False(success);
        Assert.Null(link);
    }

    [Fact]
    public void TryParse_MissingRelAttribute_ReturnsFalse()
    {
        string linkHeader = "<https://example.com/page2>";
        
        bool success = HttpLink.TryParse(linkHeader, out HttpLink? link);
        
        Assert.False(success);
        Assert.Null(link);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        bool success = HttpLink.TryParse(string.Empty, out HttpLink? link);
        
        Assert.False(success);
        Assert.Null(link);
    }
}
