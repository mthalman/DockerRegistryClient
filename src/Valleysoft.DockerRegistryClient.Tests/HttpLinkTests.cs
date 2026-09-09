using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class HttpLinkTests
{
    [Theory]
    [InlineData(
        "<https://registry.io/v2/repo/tags/list?n=10&last=tag1>; rel=\"next\"",
        "https://registry.io/v2/repo/tags/list?n=10&last=tag1",
        "next")]
    [InlineData(
        "</v2/repo/tags/list?n=10&last=tag1>; title=\"Page 2\"; REL=\"previous NEXT\"; type=\"application/json\"",
        "/v2/repo/tags/list?n=10&last=tag1",
        "previous NEXT")]
    [InlineData(
        "<https://example.com/page2>; title=\"a comma, and an escaped \\\"quote\\\"\"; rel=prev",
        "https://example.com/page2",
        "prev")]
    [InlineData(
        "<https://example.com/page2>; rel=next; extension; hreflang=en; hreflang=de",
        "https://example.com/page2",
        "next")]
    [InlineData(
        "<https://example.com/page2>; rel=next; rel=prev",
        "https://example.com/page2",
        "next")]
    [InlineData(
        "<https://example.com/page2>; anchor=\"\"; rel=next",
        "https://example.com/page2",
        "next")]
    public void TryParse_ValidLinkHeader_ReturnsLink(
        string linkHeader,
        string expectedUrl,
        string expectedRelationship)
    {
        bool success = HttpLink.TryParse(linkHeader, out HttpLink? link);

        Assert.True(success);
        Assert.NotNull(link);
        Assert.Equal(expectedUrl, link.Url);
        Assert.Equal(expectedRelationship, link.Relationship);
    }

    [Fact]
    public void TryParseList_CommaSeparatedLinks_ReturnsEveryLink()
    {
        const string LinkHeader =
            ", <https://example.com/page1>; rel=\"prev\",, </page3>; rel=\"alternate next\"; title=\"Page 3\",";

        bool success = HttpLink.TryParseList(LinkHeader, out IReadOnlyList<HttpLink>? links);

        Assert.True(success);
        Assert.NotNull(links);
        Assert.Collection(
            links,
            link =>
            {
                Assert.Equal("https://example.com/page1", link.Url);
                Assert.True(link.HasRelationship("PREV"));
            },
            link =>
            {
                Assert.Equal("/page3", link.Url);
                Assert.True(link.HasRelationship("next"));
            });
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a valid link header")]
    [InlineData("https://example.com/page2; rel=\"next\"")]
    [InlineData("<https://example.com/page2>")]
    [InlineData("<https://example.com/page2>; title=\"Page 2\"")]
    [InlineData("<https://example.com/page2>; rel=\"next\", malformed")]
    [InlineData("<https://example.com/page 2>; rel=\"next\"")]
    [InlineData("<https://example.com/%ZZ>; rel=\"next\"")]
    [InlineData("<https://example.com/page2>; rel=\"next")]
    [InlineData("<https://example.com/page2>; rel=\"next\" trailing")]
    [InlineData("<https://example.com/page2>; rel=\"next\", </bad>; rel=\"@\"")]
    [InlineData("<https://example.com/page2>; rel=\"next\", </bad>; rel=\"https://example.com/%ZZ\"")]
    [InlineData("<https://example.com/page2>; rel=\"next\"; anchor=\"https://example.com/%ZZ\"")]
    public void TryParseList_InvalidHeader_ReturnsFalse(string linkHeader)
    {
        bool success = HttpLink.TryParseList(linkHeader, out IReadOnlyList<HttpLink>? links);

        Assert.False(success);
        Assert.Null(links);
    }

    [Fact]
    public void TryParse_AnchoredLink_PreservesAnchor()
    {
        const string LinkHeader =
            "</page2>; anchor=\"https://other.example/list\"; rel=\"next\"";

        bool success = HttpLink.TryParse(LinkHeader, out HttpLink? link);

        Assert.True(success);
        Assert.Equal("https://other.example/list", link?.Anchor);
    }
}
