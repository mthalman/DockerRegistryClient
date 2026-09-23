using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class OAuthRealmSecurityTests
{
    [Theory]
    [InlineData("file:///token")]
    public async Task SendAsync_InvalidRealm_DoesNotForwardCredentials(
        string realm)
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse =
            new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(
            new AuthenticationHeaderValue(
                "Bearer",
                $"realm=\"{realm}\",service=\"registry.example\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(
            request =>
                request.Headers.Authorization?.Parameter == "credentials",
            unauthorizedResponse);

        using var httpClient = CreateClient(
            new Uri("https://registry.example"),
            innerHandler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        await Assert.ThrowsAsync<AuthenticationException>(
            () => httpClient.SendAsync(request));

        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("http", "Basic")]
    [InlineData("https", "Basic")]
    [InlineData("http", "Bearer")]
    [InlineData("https", "Bearer")]
    public async Task SendAsync_HttpRealm_UsesNormalTransport(string registryScheme, string scheme)
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse =
            new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(
            new AuthenticationHeaderValue(
                "Bearer",
                "realm=\"http://192.168.1.5:5001/token\",service=\"registry\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(_ => true, unauthorizedResponse);
        innerHandler.AddExpectedRequest(request =>
        {
            Assert.Equal("http://192.168.1.5:5001/token", request.RequestUri!.GetLeftPart(UriPartial.Path));
            if (scheme == "Basic")
            {
                Assert.Equal("credentials", request.Headers.Authorization?.Parameter);
            }
            else
            {
                Assert.Contains("refresh_token=credentials",
                    request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            return true;
        }, new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"token":"registry-token"}""")
        });
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization?.Parameter == "registry-token",
            new HttpResponseMessage(HttpStatusCode.OK));

        using var httpClient = CreateClient(
            new Uri($"{registryScheme}://192.168.1.5:5000"),
            innerHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{registryScheme}://192.168.1.5:5000/v2/");
        request.Headers.Authorization = new AuthenticationHeaderValue(scheme, "credentials");

        using HttpResponseMessage response = await httpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("Basic", "basic-credentials")]
    [InlineData("Bearer", "refresh-token")]
    public async Task SendAsync_CrossOriginChallenge_DoesNotForwardAuthorization(
        string authorizationScheme,
        string authorizationParameter)
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse =
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://attacker.example/redirected")
            };
        unauthorizedResponse.Headers.WwwAuthenticate.Add(
            new AuthenticationHeaderValue(
                "Bearer",
                "realm=\"https://auth.attacker.example/token\",service=\"registry.example\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(_ => true, unauthorizedResponse);

        using var httpClient = CreateClient(
            new Uri("https://registry.example"),
            innerHandler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            authorizationScheme,
            authorizationParameter);

        using HttpResponseMessage response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Bearer")]
    public async Task SendAsync_TokenRealmRedirect_UsesNormalRedirectRules(string scheme)
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse =
            new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(
            new AuthenticationHeaderValue(
                "Bearer",
                "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(_ => true, unauthorizedResponse);
        var redirectResponse =
            new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
        redirectResponse.Headers.Location =
            new Uri("https://other-auth.example/token");
        innerHandler.AddExpectedRequest(
            request =>
                request.RequestUri!.GetLeftPart(UriPartial.Path) == "https://auth.example/token",
            redirectResponse);
        innerHandler.AddExpectedRequest(request =>
        {
            Assert.Equal(new Uri("https://other-auth.example/token"), request.RequestUri);
            Assert.Null(request.Headers.Authorization);
            Assert.Equal(scheme == "Bearer" ? HttpMethod.Post : HttpMethod.Get, request.Method);
            if (scheme == "Bearer")
            {
                Assert.Contains("refresh_token=refresh-token",
                    request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            return true;
        }, new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"token":"registry-token"}""")
        });
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization?.Parameter == "registry-token",
            new HttpResponseMessage(HttpStatusCode.OK));

        using var httpClient = CreateClient(
            new Uri("https://registry.example"),
            innerHandler);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/v2/repo");
        request.Headers.Authorization =
            new AuthenticationHeaderValue(scheme, "refresh-token");

        using HttpResponseMessage response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    private static HttpClient CreateClient(
        Uri registryUri,
        HttpMessageHandler innerHandler) =>
        new(
            new OAuthDelegatingHandler(
                registryUri,
                new RedirectDelegatingHandler(innerHandler)));
}
