using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class RedirectDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_MarkedRequestFollowsSameOriginRedirect()
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            CreateRedirectResponse("/v2/repository/referrers/sha256:subject?last=first"));
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject?last=first",
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new RedirectDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject");
        RedirectDelegatingHandler.RequireSameOrigin(request, new Uri("https://registry.example"));

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_MarkedRequestRejectsCrossOriginRedirect()
    {
        var innerHandler = new MockHttpMessageHandler();
        HttpResponseMessage redirectResponse = CreateRedirectResponse(
            "https://attacker.example/continuation");
        redirectResponse.Content = new StringContent("redirect");
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject",
            redirectResponse);
        using var client = new HttpClient(new RedirectDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/repository/referrers/sha256:subject");
        RedirectDelegatingHandler.RequireSameOrigin(request, new Uri("https://registry.example"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request));

        Assert.Contains("outside the configured registry origin", exception.Message);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => redirectResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_UnmarkedRequestFollowsCrossOriginRedirect()
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repository/blobs/sha256:digest",
            CreateRedirectResponse("https://storage.example/blob"));
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://storage.example/blob",
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new RedirectDelegatingHandler(innerHandler));

        using HttpResponseMessage response = await client.GetAsync(
            "https://registry.example/v2/repository/blobs/sha256:digest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_RedirectWithoutFragment_InheritsOriginalFragment()
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/source#fragment",
            CreateRedirectResponse("/destination"));
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/destination#fragment",
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new RedirectDelegatingHandler(innerHandler));

        using HttpResponseMessage response = await client.GetAsync(
            "https://registry.example/source#fragment");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(300, "POST", "GET", false)]
    [InlineData(301, "POST", "GET", false)]
    [InlineData(302, "POST", "GET", false)]
    [InlineData(303, "PUT", "GET", false)]
    [InlineData(307, "POST", "POST", true)]
    [InlineData(308, "POST", "POST", true)]
    public async Task SendAsync_RedirectAppliesMethodContentAndAuthorizationRules(
        int statusCode,
        string originalMethod,
        string expectedMethod,
        bool expectsContent)
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            new HttpMethod(originalMethod),
            "https://registry.example/source",
            CreateRedirectResponse("/destination", (HttpStatusCode)statusCode));
        innerHandler.AddExpectedRequest(
            request =>
                request.RequestUri == new Uri("https://registry.example/destination") &&
                request.Method == new HttpMethod(expectedMethod) &&
                (request.Content is not null) == expectsContent &&
                request.Headers.Authorization is null,
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new RedirectDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            new HttpMethod(originalMethod),
            "https://registry.example/source")
        {
            Content = new StringContent("content")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("http://registry.example/destination")]
    [InlineData("ftp://registry.example/destination")]
    public async Task SendAsync_UnsafeOrUnsupportedRedirect_ReturnsRedirectResponse(string location)
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/source",
            CreateRedirectResponse(location));
        using var client = new HttpClient(new RedirectDelegatingHandler(innerHandler));

        using HttpResponseMessage response = await client.GetAsync("https://registry.example/source");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(new Uri(location), response.Headers.Location);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_MarkedRequestRejectsCrossOriginOnLaterRedirect()
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/source",
            CreateRedirectResponse("/second"));
        innerHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/second",
            CreateRedirectResponse("https://attacker.example/destination"));
        using var client = new HttpClient(new RedirectDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://registry.example/source");
        RedirectDelegatingHandler.RequireSameOrigin(request, new Uri("https://registry.example"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_StopsAfterMaximumRedirects()
    {
        var innerHandler = new MockHttpMessageHandler();
        for (int index = 0; index <= 50; index++)
        {
            innerHandler.AddExpectedRequest(
                HttpMethod.Get,
                $"https://registry.example/{index}",
                CreateRedirectResponse($"/{index + 1}"));
        }
        using var client = new HttpClient(new RedirectDelegatingHandler(innerHandler));

        using HttpResponseMessage response = await client.GetAsync("https://registry.example/0");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(new Uri("/51", UriKind.Relative), response.Headers.Location);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    private static HttpResponseMessage CreateRedirectResponse(
        string location,
        HttpStatusCode statusCode = HttpStatusCode.TemporaryRedirect) =>
        new(statusCode)
        {
            Headers =
            {
                Location = new Uri(location, UriKind.RelativeOrAbsolute)
            }
        };
}
