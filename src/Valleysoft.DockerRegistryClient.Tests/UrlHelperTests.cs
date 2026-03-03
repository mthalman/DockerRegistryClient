using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class UrlHelperTests
{
    [Fact]
    public void ApplyCount_WithCount_AppendsQueryParameter()
    {
        string url = "https://registry.io/v2/repo/tags/list";
        
        string result = UrlHelper.ApplyCount(url, 50);
        
        Assert.Equal("https://registry.io/v2/repo/tags/list?n=50", result);
    }

    [Fact]
    public void ApplyCount_WithoutCount_ReturnsUnchangedUrl()
    {
        string url = "https://registry.io/v2/repo/tags/list";
        
        string result = UrlHelper.ApplyCount(url, null);
        
        Assert.Equal(url, result);
    }

    [Fact]
    public void ApplyCount_WithZeroCount_AppendsZero()
    {
        string url = "https://registry.io/v2/repo/tags/list";
        
        string result = UrlHelper.ApplyCount(url, 0);
        
        Assert.Equal("https://registry.io/v2/repo/tags/list?n=0", result);
    }

    [Fact]
    public void Concat_BothWithSlash_RemovesDuplicateSlash()
    {
        string url1 = "https://registry.io/";
        string url2 = "/v2/repo/tags/list";
        
        string result = UrlHelper.Concat(url1, url2);
        
        Assert.Equal("https://registry.io/v2/repo/tags/list", result);
    }

    [Fact]
    public void Concat_NeitherWithSlash_ConcatenatesDirectly()
    {
        string url1 = "https://registry.io";
        string url2 = "v2/repo/tags/list";
        
        string result = UrlHelper.Concat(url1, url2);
        
        Assert.Equal("https://registry.iov2/repo/tags/list", result);
    }

    [Fact]
    public void Concat_OnlyFirstWithSlash_ConcatenatesDirectly()
    {
        string url1 = "https://registry.io/";
        string url2 = "v2/repo/tags/list";
        
        string result = UrlHelper.Concat(url1, url2);
        
        Assert.Equal("https://registry.io/v2/repo/tags/list", result);
    }

    [Fact]
    public void Concat_OnlySecondWithSlash_ConcatenatesDirectly()
    {
        string url1 = "https://registry.io";
        string url2 = "/v2/repo/tags/list";
        
        string result = UrlHelper.Concat(url1, url2);
        
        Assert.Equal("https://registry.io/v2/repo/tags/list", result);
    }
}
