using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class PageTests
{
    [Fact]
    public void Constructor_WithNextPageLink_SetsProperties()
    {
        var value = new { Name = "test" };
        string nextLink = "https://registry.io/v2/repo/tags/list?n=10&last=tag1";
        
        var page = new Page<object>(value, nextLink);
        
        Assert.Same(value, page.Value);
        Assert.Equal(nextLink, page.NextPageLink);
    }

    [Fact]
    public void Constructor_WithoutNextPageLink_SetsPropertiesToNull()
    {
        var value = new { Name = "test" };
        
        var page = new Page<object>(value, null);
        
        Assert.Same(value, page.Value);
        Assert.Null(page.NextPageLink);
    }

    [Fact]
    public void Constructor_WithNullValue_AllowsNull()
    {
        var page = new Page<string?>(null, "https://example.com/next");
        
        Assert.Null(page.Value);
        Assert.Equal("https://example.com/next", page.NextPageLink);
    }
}
