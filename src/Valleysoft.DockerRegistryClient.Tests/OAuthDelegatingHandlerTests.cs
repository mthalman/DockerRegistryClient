using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class OAuthDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_BearerChallenge_GetsTokenAndRetriesRequest()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Get &&
                request.RequestUri == new Uri("https://registry.example/v2/repo/tags/list") &&
                request.Headers.Authorization is null,
            unauthorizedResponse);

        AuthenticationHeaderValue? retryAuthorization = null;
        innerHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Get &&
                request.RequestUri?.Host == "auth.example" &&
                request.RequestUri.Query.Contains("service=registry.example") &&
                request.RequestUri.Query.Contains("scope=repository:repo:pull"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request =>
            {
                retryAuthorization = request.Headers.Authorization;
                return request.Method == HttpMethod.Get &&
                    request.RequestUri == new Uri("https://registry.example/v2/repo/tags/list");
            },
            new HttpResponseMessage(HttpStatusCode.OK));

        using var httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));

        using HttpResponseMessage response = await httpClient.GetAsync("https://registry.example/v2/repo/tags/list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", retryAuthorization?.Scheme);
        Assert.Equal("access-token", retryAuthorization?.Parameter);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_RefreshTokenAuthorization_PostsTokenRequest()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization?.Parameter == "refresh-token",
            unauthorizedResponse);

        string? tokenRequestBody = null;
        innerHandler.AddExpectedRequest(
            request =>
            {
                tokenRequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return request.Method == HttpMethod.Post &&
                    request.RequestUri == new Uri("https://auth.example/token");
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"token":"access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization?.Parameter == "access-token",
            new HttpResponseMessage(HttpStatusCode.OK));

        using var httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://registry.example/v2/repo/blobs/uploads/id");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "refresh-token");

        using HttpResponseMessage response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("grant_type=refresh_token", tokenRequestBody);
        Assert.Contains("refresh_token=refresh-token", tokenRequestBody);
        Assert.Contains("scope=repository%3Arepo%3Apush", tokenRequestBody);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_InvalidTokenResponse_ThrowsJsonException()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(
            "https://registry.example/v2/",
            unauthorizedResponse);
        innerHandler.AddExpectedRequest(
            request => request.RequestUri?.Host == "auth.example",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json")
            });

        using var httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));

        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            () => httpClient.GetAsync("https://registry.example/v2/"));

        Assert.Contains("Unable to deserialize the response", exception.Message);
    }

}
