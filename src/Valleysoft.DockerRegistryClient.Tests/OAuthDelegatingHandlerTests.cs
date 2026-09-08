using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class OAuthDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_AuthorizedForbidden_RetriesAnonymouslyAndCompletesBearerChallenge()
    {
        var innerHandler = new MockHttpMessageHandler();
        var forbiddenContent = new TrackingContent("forbidden");
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization?.Scheme == "Basic" &&
                request.Headers.Authorization?.Parameter == "credentials",
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = forbiddenContent
            });
        var challengeContent = new TrackingContent("challenge");
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = challengeContent
        };
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(
            request => forbiddenContent.IsDisposed &&
                request.Headers.Authorization is null,
            unauthorizedResponse);
        innerHandler.AddExpectedRequest(
            request => challengeContent.IsDisposed &&
                request.RequestUri?.Host == "auth.example" &&
                request.Headers.Authorization?.Scheme == "Basic" &&
                request.Headers.Authorization?.Parameter == "credentials",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization?.Scheme == "Bearer" &&
                request.Headers.Authorization?.Parameter == "access-token",
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/repo/tags/list");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(forbiddenContent.IsDisposed);
        Assert.True(challengeContent.IsDisposed);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_BearerChallengeWithOnlyRealm_OmitsOptionalQueryParameters()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "bearer",
            "REALM=\"https://auth.example/token\""));
        innerHandler.AddExpectedRequest(_ => true, unauthorizedResponse);
        innerHandler.AddExpectedRequest(
            request =>
                request.Method == HttpMethod.Get &&
                request.RequestUri == new Uri("https://auth.example/token") &&
                request.Headers.Authorization?.Scheme == "Basic",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"token":"access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request =>
                request.Headers.Authorization?.Scheme == "Bearer" &&
                request.Headers.Authorization?.Parameter == "access-token",
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/repo/tags/list");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_AuthorizedForbiddenTwice_ReturnsSecondForbidden()
    {
        var innerHandler = new MockHttpMessageHandler();
        var firstContent = new TrackingContent("first");
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization is not null,
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = firstContent
            });
        var secondContent = new TrackingContent("second");
        innerHandler.AddExpectedRequest(
            request => firstContent.IsDisposed &&
                request.Headers.Authorization is null,
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = secondContent
            });
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/repo/tags/list");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(firstContent.IsDisposed);
        Assert.False(secondContent.IsDisposed);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_AnonymousForbidden_DoesNotRetry()
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization is null,
            new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));

        using HttpResponseMessage response =
            await client.GetAsync("https://registry.example/v2/repo/tags/list");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_RedirectedAnonymousForbidden_DoesNotRetry()
    {
        var innerHandler = new MockHttpMessageHandler();
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.Redirect);
        redirectResponse.Headers.Location =
            new Uri("https://storage.example/blob");
        innerHandler.AddExpectedRequest(
            request =>
                request.RequestUri == new Uri("https://registry.example/v2/blob") &&
                request.Headers.Authorization is not null,
            redirectResponse);
        var forbiddenContent = new TrackingContent("forbidden");
        innerHandler.AddExpectedRequest(
            request =>
                request.RequestUri == new Uri("https://storage.example/blob") &&
                request.Headers.Authorization is null,
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = forbiddenContent
            });
        using var client = new HttpClient(
            new OAuthDelegatingHandler(
                new RedirectDelegatingHandler(innerHandler)));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/blob");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(forbiddenContent.IsDisposed);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_CanceledAfterAuthorizedForbidden_DoesNotRetry()
    {
        var innerHandler = new MockHttpMessageHandler();
        using var cancellationSource = new CancellationTokenSource();
        var forbiddenContent = new TrackingContent("forbidden");
        innerHandler.AddExpectedRequest(
            request =>
            {
                cancellationSource.Cancel();
                return request.Headers.Authorization is not null;
            },
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = forbiddenContent
            });
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://registry.example/v2/repo/tags/list");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendAsync(request, cancellationSource.Token));

        Assert.True(forbiddenContent.IsDisposed);
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_CanceledAfterAuthorizedForbiddenWithConsumedNonSeekableContent_ThrowsCancellation()
    {
        var innerHandler = new MockHttpMessageHandler();
        using var cancellationSource = new CancellationTokenSource();
        var forbiddenContent = new TrackingContent("forbidden");
        innerHandler.AddExpectedRequest(
            request =>
            {
                _ = ReadContent(request.Content!);
                cancellationSource.Cancel();
                return true;
            },
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = forbiddenContent
            });
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var stream = new NonSeekableReadStream([1, 2, 3]);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/v2/repo/blobs/uploads/id")
        {
            Content = new ReplayableStreamContent(stream)
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendAsync(request, cancellationSource.Token));

        Assert.True(forbiddenContent.IsDisposed);
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_AuthorizedForbiddenWithNonReplayableContent_ThrowsBeforeRetry()
    {
        var innerHandler = new MockHttpMessageHandler();
        var forbiddenContent = new TrackingContent("forbidden");
        innerHandler.AddExpectedRequest(
            _ => true,
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = forbiddenContent
            });
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/v2/repo/blobs/uploads/id")
        {
            Content = new StreamContent(new MemoryStream([1, 2, 3]))
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request));

        Assert.Contains("cannot be safely replayed", exception.Message);
        Assert.True(forbiddenContent.IsDisposed);
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_BearerChallenge_GetsTokenAndRetriesRequest()
    {
        var innerHandler = new MockHttpMessageHandler();
        var challengeContent = new TrackingContent("challenge");
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = challengeContent
        };
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(
            request => request.Method == HttpMethod.Get &&
                request.RequestUri == new Uri("https://registry.example/v2/repo/tags/list") &&
                request.Headers.Authorization is null,
            unauthorizedResponse);

        AuthenticationHeaderValue? retryAuthorization = null;
        var tokenContent = new TrackingContent("""{"access_token":"access-token"}""");
        innerHandler.AddExpectedRequest(
            request => challengeContent.IsDisposed &&
                request.Method == HttpMethod.Get &&
                request.RequestUri?.Host == "auth.example" &&
                request.RequestUri.Query.Contains("service=registry.example") &&
                request.RequestUri.Query.Contains("scope=repository%3Arepo%3Apull"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = tokenContent
            });
        var finalContent = new TrackingContent("final");
        innerHandler.AddExpectedRequest(
            request =>
            {
                retryAuthorization = request.Headers.Authorization;
                return request.Method == HttpMethod.Get &&
                    request.RequestUri == new Uri("https://registry.example/v2/repo/tags/list");
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = finalContent
            });

        using var httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));

        using HttpResponseMessage response = await httpClient.GetAsync("https://registry.example/v2/repo/tags/list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", retryAuthorization?.Scheme);
        Assert.Equal("access-token", retryAuthorization?.Parameter);
        Assert.True(challengeContent.IsDisposed);
        Assert.True(tokenContent.IsDisposed);
        Assert.False(finalContent.IsDisposed);
        Assert.Equal("final", await response.Content.ReadAsStringAsync());
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

        HttpRequestMessage? tokenRequest = null;
        string? tokenRequestBody = null;
        innerHandler.AddExpectedRequest(
            request =>
            {
                tokenRequest = request;
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
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => tokenRequest!.Content!.ReadAsStringAsync());
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_InvalidTokenResponse_ThrowsJsonException()
    {
        var innerHandler = new MockHttpMessageHandler();
        var challengeContent = new TrackingContent("challenge");
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = challengeContent
        };
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(
            "https://registry.example/v2/",
            unauthorizedResponse);
        var tokenContent = new TrackingContent("not-json");
        var tokenRequestContent = new TrackingContent("token-request");
        innerHandler.AddExpectedRequest(
            request =>
            {
                request.Content = tokenRequestContent;
                return request.RequestUri?.Host == "auth.example";
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = tokenContent
            });

        using var httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));

        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            () => httpClient.GetAsync("https://registry.example/v2/"));

        Assert.Contains("Unable to deserialize the response", exception.Message);
        Assert.True(challengeContent.IsDisposed);
        Assert.True(tokenRequestContent.IsDisposed);
        Assert.True(tokenContent.IsDisposed);
    }

    [Fact]
    public async Task SendAsync_UnsuccessfulTokenResponse_DisposesIntermediateResponses()
    {
        var innerHandler = new MockHttpMessageHandler();
        var challengeContent = new TrackingContent("challenge");
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = challengeContent
        };
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:pull\""));
        innerHandler.AddExpectedRequest(
            "https://registry.example/v2/",
            unauthorizedResponse);
        var tokenContent = new TrackingContent("error");
        var tokenRequestContent = new TrackingContent("token-request");
        innerHandler.AddExpectedRequest(
            request =>
            {
                request.Content = tokenRequestContent;
                return request.RequestUri?.Host == "auth.example";
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = tokenContent
            });

        using var httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => httpClient.GetAsync("https://registry.example/v2/"));

        Assert.True(challengeContent.IsDisposed);
        Assert.True(tokenRequestContent.IsDisposed);
        Assert.True(tokenContent.IsDisposed);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_CanceledBeforeTokenRequest_DoesNotCreateTokenRequest()
    {
        var innerHandler = new MockHttpMessageHandler();
        using var cancellationSource = new CancellationTokenSource();
        var challengeContent = new TrackingContent("challenge");
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = challengeContent
        };
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"http://[\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(
            request =>
            {
                cancellationSource.Cancel();
                return request.Headers.Authorization?.Parameter == "refresh-token";
            },
            unauthorizedResponse);

        using var httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://registry.example/v2/repo/blobs/uploads/id");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "refresh-token");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => httpClient.SendAsync(request, cancellationSource.Token));

        Assert.True(challengeContent.IsDisposed);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_CanceledAfterUnauthorizedWithConsumedNonSeekableContent_ThrowsCancellation()
    {
        var innerHandler = new MockHttpMessageHandler();
        using var cancellationSource = new CancellationTokenSource();
        var challengeContent = new TrackingContent("challenge");
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = challengeContent
        };
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(
            request =>
            {
                _ = ReadContent(request.Content!);
                cancellationSource.Cancel();
                return true;
            },
            unauthorizedResponse);
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var stream = new NonSeekableReadStream([1, 2, 3]);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/v2/repo/blobs/uploads/id")
        {
            Content = new ReplayableStreamContent(stream)
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", "credentials");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendAsync(request, cancellationSource.Token));

        Assert.True(challengeContent.IsDisposed);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_CanceledTokenRead_DisposesIntermediateMessages()
    {
        var innerHandler = new MockHttpMessageHandler();
        var challengeContent = new TrackingContent("challenge");
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = challengeContent
        };
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization?.Parameter == "refresh-token",
            unauthorizedResponse);

        using var cancellationSource = new CancellationTokenSource();
        HttpRequestMessage? tokenRequest = null;
        HttpContent? tokenRequestContent = null;
        var tokenContent = new CancelingContent(cancellationSource);
        innerHandler.AddExpectedRequest(
            request =>
            {
                tokenRequest = request;
                tokenRequestContent = request.Content;
                return request.Method == HttpMethod.Post &&
                    request.RequestUri == new Uri("https://auth.example/token");
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = tokenContent
            });

        using var httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://registry.example/v2/repo/blobs/uploads/id");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "refresh-token");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => httpClient.SendAsync(request, cancellationSource.Token));

        Assert.True(challengeContent.IsDisposed);
        Assert.True(tokenContent.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => tokenRequestContent!.ReadAsStringAsync());
        Assert.Same(tokenRequestContent, tokenRequest!.Content);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_NestedRequest_DoesNotReplaceOuterAuthorization()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"destination.example\",scope=\"repository:repo:push\""));
        HttpClient? httpClient = null;
        using var content = new NestedRequestContent(async () =>
        {
            using var nestedRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "https://source.example/blob");
            nestedRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", "source-refresh-token");
            using HttpResponseMessage response = await httpClient!.SendAsync(nestedRequest);
        });
        innerHandler.AddExpectedRequest(
            request =>
            {
                request.Content!.CopyToAsync(Stream.Null).GetAwaiter().GetResult();
                return request.Headers.Authorization?.Parameter == "destination-refresh-token";
            },
            unauthorizedResponse);
        innerHandler.AddExpectedRequest(
            request =>
                request.RequestUri == new Uri("https://source.example/blob") &&
                request.Headers.Authorization?.Parameter == "source-refresh-token",
            new HttpResponseMessage(HttpStatusCode.OK));
        string? tokenRequestBody = null;
        innerHandler.AddExpectedRequest(
            request =>
            {
                tokenRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return request.RequestUri == new Uri("https://auth.example/token");
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"destination-access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request => request.Headers.Authorization?.Parameter == "destination-access-token",
            new HttpResponseMessage(HttpStatusCode.OK));
        httpClient = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using (httpClient)
        using (var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://destination.example/upload"))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", "destination-refresh-token");
            request.Content = content;

            using HttpResponseMessage response = await httpClient.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Contains(
            "refresh_token=destination-refresh-token",
            tokenRequestBody);
        Assert.DoesNotContain("source-refresh-token", tokenRequestBody);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_SeekableContentReplaysFromInitialPosition()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 2, 3, 4 }),
            unauthorizedResponse);
        innerHandler.AddExpectedRequest(
            request => request.RequestUri?.Host == "auth.example",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 2, 3, 4 }),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var stream = new MemoryStream([1, 2, 3, 4]);
        stream.Position = 1;
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/upload")
        {
            Content = new ReplayableStreamContent(stream)
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_NonSeekableContentThrowsBeforeRetry()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3 }),
            unauthorizedResponse);
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var stream = new NonSeekableReadStream([1, 2, 3]);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/upload")
        {
            Content = new ReplayableStreamContent(stream)
        };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request));

        Assert.Contains("non-seekable", exception.Message);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_UnconsumedNonSeekableContentIsSentOnRetry()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(_ => true, unauthorizedResponse);
        innerHandler.AddExpectedRequest(
            request => request.RequestUri?.Host == "auth.example",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3 }),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var stream = new NonSeekableReadStream([1, 2, 3]);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/upload")
        {
            Content = new ReplayableStreamContent(stream)
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_UnknownContentThrowsBeforeRetry()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Content = new StringContent("unauthorized");
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(_ => true, unauthorizedResponse);
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/upload")
        {
            Content = new StreamContent(new MemoryStream([1, 2, 3]))
        };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request));

        Assert.Contains("cannot be safely replayed", exception.Message);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => unauthorizedResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_BufferedContentIsRetried()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3 }),
            unauthorizedResponse);
        innerHandler.AddExpectedRequest(
            request => request.RequestUri?.Host == "auth.example",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3 }),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/upload")
        {
            Content = new ByteArrayContent([1, 2, 3])
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_ReadOnlyMemoryContentIsRetried()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3 }),
            unauthorizedResponse);
        innerHandler.AddExpectedRequest(
            request => request.RequestUri?.Host == "auth.example",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"access-token"}""")
            });
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3 }),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/upload")
        {
            Content = new ReadOnlyMemoryContent(new byte[] { 1, 2, 3 })
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_ByteArrayContentSubclassThrowsBeforeRetry()
    {
        var innerHandler = new MockHttpMessageHandler();
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        unauthorizedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer",
            "realm=\"https://auth.example/token\",service=\"registry.example\",scope=\"repository:repo:push\""));
        innerHandler.AddExpectedRequest(_ => true, unauthorizedResponse);
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/upload")
        {
            Content = new DerivedByteArrayContent([1, 2, 3])
        };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request));

        Assert.Contains("cannot be safely replayed", exception.Message);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task SendAsync_NonSeekableContentWithoutRetrySucceeds()
    {
        var innerHandler = new MockHttpMessageHandler();
        innerHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3 }),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new OAuthDelegatingHandler(innerHandler));
        using var stream = new NonSeekableReadStream([1, 2, 3]);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://registry.example/upload")
        {
            Content = new ReplayableStreamContent(stream)
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, innerHandler.RemainingRequestCount);
    }

    private static byte[] ReadContent(HttpContent content)
    {
        using var stream = new MemoryStream();
        content.CopyToAsync(stream).GetAwaiter().GetResult();
        return stream.ToArray();
    }

    private sealed class NestedRequestContent(Func<Task> sendNestedRequest) : HttpContent, IReplayableHttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            await sendNestedRequest();
        }

        public void PrepareForReplay()
        {
        }
    }

    private sealed class DerivedByteArrayContent(byte[] content) : ByteArrayContent(content);

    private sealed class DisposalTrackingContent : HttpContent
    {
        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = disposing;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingContent(string content) : HttpContent
    {
        private readonly byte[] _content = Encoding.UTF8.GetBytes(content);

        public bool IsDisposed { get; private set; }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            stream.WriteAsync(_content, 0, _content.Length);

        protected override void Dispose(bool disposing)
        {
            IsDisposed = disposing;
            base.Dispose(disposing);
        }
    }

    private sealed class CancelingContent(CancellationTokenSource cancellationSource) : HttpContent
    {
        public bool IsDisposed { get; private set; }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            cancellationSource.Cancel();
            return Task.FromCanceled(cancellationSource.Token);
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = disposing;
            base.Dispose(disposing);
        }
    }
}
