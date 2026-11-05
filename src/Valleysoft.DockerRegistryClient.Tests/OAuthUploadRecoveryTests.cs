using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Credentials;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

/// <summary>
/// Exercises upload authentication recovery and optional mount fallback through the built-in OAuth and redirect pipeline.
/// </summary>
public class OAuthUploadRecoveryTests
{
    private const string Registry = "https://registry.example";
    private const string Digest = "custom:blob";
    private const string UploadLocation = "/v2/destination/blobs/uploads/id";
    private static readonly byte[] Blob = [1, 2, 3, 4];

    public static IEnumerable<object[]> RecoveryCases()
    {
        foreach (string credentials in new[] { "basic", "refresh", "anonymous" })
        {
            foreach (HttpStatusCode status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
            {
                foreach (string operation in new[] { "chunk", "complete", "complete-body" })
                {
                    yield return [credentials, status, operation];
                }
            }
        }
    }

    /// <summary>
    /// Ensures recovery uses original credentials, preserves the body's initial position, and reuses renewed tokens across chunks.
    /// </summary>
    [Theory]
    [MemberData(nameof(RecoveryCases))]
    public async Task Upload_ExpiredAccessToken_RecoversUsingOriginalCredentials(
        string credentials, HttpStatusCode status, string operation)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(HttpMethod.Post, $"{Registry}/v2/destination/blobs/uploads/", Challenge());
        AddTokenRequest(handler, credentials, "initial-access");
        handler.AddExpectedRequest(request => request.Headers.Authorization?.Parameter == "initial-access", UploadResponse());

        HttpMethod method = operation == "chunk" ? HttpMethod.Patch : HttpMethod.Put;
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(method, request.Method);
            Assert.Equal("initial-access", request.Headers.Authorization?.Parameter);
            AssertUploadBody(request, operation);
            return true;
        }, status == HttpStatusCode.Forbidden ? new HttpResponseMessage(status) : Challenge());
        if (status == HttpStatusCode.Forbidden)
        {
            handler.AddExpectedRequest(request =>
            {
                Assert.Null(request.Headers.Authorization);
                AssertUploadBody(request, operation);
                return true;
            }, Challenge());
        }

        AddTokenRequest(handler, credentials, "renewed-access");
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(method, request.Method);
            Assert.Equal("renewed-access", request.Headers.Authorization?.Parameter);
            AssertUploadBody(request, operation);
            return true;
        }, UploadResponse());
        if (operation == "chunk")
        {
            handler.AddExpectedRequest(request =>
            {
                Assert.Equal(HttpMethod.Patch, request.Method);
                Assert.Equal("renewed-access", request.Headers.Authorization?.Parameter);
                AssertUploadBody(request, operation);
                return true;
            }, UploadResponse());
            handler.AddExpectedRequest(request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal("renewed-access", request.Headers.Authorization?.Parameter);
                Assert.Null(request.Content);
                return true;
            }, UploadResponse());
        }
        using var client = new RegistryClient(Registry, Credentials(credentials), handler);

        BlobUploadInitializationResult start = await client.Blobs.BeginUploadAsync("destination");
        using var stream = new MemoryStream([0, .. Blob]);
        stream.Position = 1;
        if (operation == "chunk")
        {
            BlobUploadStreamResult chunk = await client.Blobs.SendUploadStreamAsync(
                start.Location, stream, start.UploadContext);
            using var nextStream = new MemoryStream(Blob);
            chunk = await client.Blobs.SendUploadStreamAsync(chunk.Location, nextStream, start.UploadContext);
            await client.Blobs.EndUploadAsync(chunk.Location, Digest, start.UploadContext);
        }
        else
        {
            await client.Blobs.EndUploadAsync(
                start.Location, Digest, start.UploadContext, operation == "complete" ? null : stream);
        }

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Keeps valid registry authorization cached when a redirected chunk succeeds anonymously, regardless of the redirect's origin.
    /// </summary>
    [Theory]
    [InlineData("https://storage.example/upload")]
    [InlineData("https://registry.example/anonymous-upload")]
    public async Task Upload_AnonymousRedirect_DoesNotReplaceCachedRegistryAuthorization(string redirectLocation)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(_ => true, Challenge());
        AddTokenRequest(handler, "basic", "registry-access");
        handler.AddExpectedRequest(_ => true, UploadResponse());
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("registry-access", request.Headers.Authorization?.Parameter);
            Assert.Equal(Blob, Serialize(request.Content!));
            return true;
        }, new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
        {
            Headers = { Location = new Uri(redirectLocation) }
        });
        HttpResponseMessage externalResponse = UploadResponse();
        externalResponse.Headers.Location = new Uri($"{Registry}{UploadLocation}");
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(new Uri(redirectLocation), request.RequestUri);
            Assert.Null(request.Headers.Authorization);
            Assert.Equal(Blob, Serialize(request.Content!));
            return true;
        }, externalResponse);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("registry.example", request.RequestUri!.Host);
            Assert.Equal("registry-access", request.Headers.Authorization?.Parameter);
            Assert.Equal(Blob, Serialize(request.Content!));
            return true;
        }, UploadResponse());
        using var client = new RegistryClient(Registry, Credentials("basic"), handler);
        BlobUploadInitializationResult start = await client.Blobs.BeginUploadAsync("destination");

        using var firstStream = new MemoryStream(Blob);
        BlobUploadStreamResult chunk = await client.Blobs.SendUploadStreamAsync(
            start.Location, firstStream, start.UploadContext);
        using var secondStream = new MemoryStream(Blob);
        await client.Blobs.SendUploadStreamAsync(chunk.Location, secondStream, start.UploadContext);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Rejects missing or empty tokens before a mount retry can turn a malformed token response into optional fallback.
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"token":null}""")]
    [InlineData("""{"access_token":null}""")]
    [InlineData("""{"token":""}""")]
    [InlineData("""{"access_token":""}""")]
    [InlineData("""{"token":" "}""")]
    [InlineData("""{"access_token":" "}""")]
    public async Task Mount_InvalidTokenResponse_ThrowsBeforeRetry(string tokenPayload)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(request => request.RequestUri!.Query.Contains("mount="), Challenge());
        var tokenContent = new DisposalTrackingContent(tokenPayload);
        handler.AddExpectedRequest(request => request.RequestUri!.Host == "auth.example",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = tokenContent });
        handler.AddExpectedRequest(_ => true, new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = new RegistryClient(Registry, Credentials("basic"), handler);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            ((BlobOperations)client.Blobs).MountAsync("destination", Digest, "source"));

        Assert.True(tokenContent.IsDisposed);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(1, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Ensures external upload destinations cannot use cached registry credentials to satisfy their own bearer challenges.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upload_CachedAuthenticationState_DoesNotFollowExternalLocationOrRedirect(bool redirect)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(_ => true, Challenge());
        AddTokenRequest(handler, "basic", "initial-access");
        HttpResponseMessage started = UploadResponse();
        started.Headers.Location = new Uri(redirect ? UploadLocation : "https://storage.example/upload", UriKind.RelativeOrAbsolute);
        handler.AddExpectedRequest(_ => true, started);
        if (redirect)
        {
            handler.AddExpectedRequest(request => request.Headers.Authorization?.Parameter == "initial-access",
                new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
                {
                    Headers = { Location = new Uri("https://storage.example/upload") }
                });
        }

        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("storage.example", request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization);
            return true;
        }, Challenge());
        using var client = new RegistryClient(Registry, Credentials("basic"), handler);
        BlobUploadInitializationResult start = await client.Blobs.BeginUploadAsync("destination");

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(() =>
            client.Blobs.EndUploadAsync(start.Location, Digest, start.UploadContext));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    /// <summary>
    /// Allows denied optional mount scopes to fall back to separate source and destination credentials without hiding upload denial.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    public async Task Copy_MountTokenDenied_FallsBackButUploadTokenDenialPropagates(
        HttpStatusCode denial, bool denyUpload)
    {
        var sourceHandler = CreateCopySource(includeBlob: !denyUpload);
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(HttpMethod.Head, $"{Registry}/v2/destination/blobs/{Digest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(request => request.RequestUri!.Query.Contains("mount="),
            Challenge("repository:source:pull repository:destination:push"));
        var deniedContent = new DisposalTrackingContent();
        destinationHandler.AddExpectedRequest(request =>
        {
            Assert.Equal("auth.example", request.RequestUri!.Host);
            Assert.Contains(Uri.EscapeDataString("repository:source:pull"), request.RequestUri.Query);
            AssertBasicCredentials(request, "writer");
            return true;
        }, new HttpResponseMessage(denial) { Content = deniedContent });
        destinationHandler.AddExpectedRequest(HttpMethod.Post, $"{Registry}/v2/destination/blobs/uploads/",
            Challenge());
        if (denyUpload)
        {
            destinationHandler.AddExpectedRequest(request => request.RequestUri!.Host == "auth.example",
                new HttpResponseMessage(denial));
        }
        else
        {
            AddTokenRequest(destinationHandler, "basic", "upload-access");
            destinationHandler.AddExpectedRequest(_ => true, UploadResponse());
            destinationHandler.AddExpectedRequest(request =>
            {
                Assert.Equal("upload-access", request.Headers.Authorization?.Parameter);
                Assert.Equal(Blob, Serialize(request.Content!));
                return true;
            }, Challenge());
            AddTokenRequest(destinationHandler, "basic", "renewed-access");
            destinationHandler.AddExpectedRequest(request =>
            {
                Assert.Equal("renewed-access", request.Headers.Authorization?.Parameter);
                Assert.Equal(Blob, Serialize(request.Content!));
                return true;
            }, UploadResponse());
            var published = new HttpResponseMessage(HttpStatusCode.Created);
            published.Headers.Location = new Uri("/v2/destination/manifests/latest", UriKind.Relative);
            published.Headers.Add("Docker-Content-Digest", "custom:root");
            destinationHandler.AddExpectedRequest(HttpMethod.Put, $"{Registry}/v2/destination/manifests/latest", published);
        }

        using var source = new RegistryClient(Registry, new BasicAuthenticationCredentials("reader", "password"), sourceHandler);
        using var destination = new RegistryClient(Registry, Credentials("basic"), destinationHandler);

        if (denyUpload)
        {
            HttpRequestException exception = await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
                source.CopyAsync("source", "latest", destination, "destination", "latest"));
            Assert.Equal(denial, exception.StatusCode);
        }
        else
        {
            ManifestPublishResult result = await source.CopyAsync("source", "latest", destination, "destination", "latest");
            Assert.Equal("custom:root", result.Digest);
        }

        Assert.True(deniedContent.IsDisposed);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    /// <summary>
    /// Preserves the original refresh credential when an accepted mount supplies a session whose access token expires during staging.
    /// </summary>
    [Fact]
    public async Task Copy_MountStartsUpload_ExpiredAccessTokenRecoversUsingMountCredentials()
    {
        var sourceHandler = CreateCopySource();
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(HttpMethod.Head, $"{Registry}/v2/destination/blobs/{Digest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(request => request.RequestUri!.Query.Contains("mount="),
            Challenge("repository:source:pull repository:destination:push"));
        AddTokenRequest(destinationHandler, "refresh", "mount-access");
        destinationHandler.AddExpectedRequest(_ => true, UploadResponse());
        destinationHandler.AddExpectedRequest(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("mount-access", request.Headers.Authorization?.Parameter);
            Assert.Equal(Blob, Serialize(request.Content!));
            return true;
        }, Challenge());
        AddTokenRequest(destinationHandler, "refresh", "renewed-access");
        destinationHandler.AddExpectedRequest(request =>
        {
            Assert.Equal("renewed-access", request.Headers.Authorization?.Parameter);
            Assert.Equal(Blob, Serialize(request.Content!));
            return true;
        }, UploadResponse());
        var published = new HttpResponseMessage(HttpStatusCode.Created);
        published.Headers.Location = new Uri("/v2/destination/manifests/latest", UriKind.Relative);
        published.Headers.Add("Docker-Content-Digest", "custom:root");
        destinationHandler.AddExpectedRequest(HttpMethod.Put, $"{Registry}/v2/destination/manifests/latest", published);
        using var source = new RegistryClient(Registry, new BasicAuthenticationCredentials("reader", "password"), sourceHandler);
        using var destination = new RegistryClient(Registry, Credentials("refresh"), destinationHandler);

        await source.CopyAsync("source", "latest", destination, "destination", "latest");

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    /// <summary>
    /// Prevents mount fallback from masking cancellation, transport failures, server errors, or malformed authentication responses.
    /// </summary>
    [Theory]
    [InlineData("transport")]
    [InlineData("canceled")]
    [InlineData("server-error")]
    [InlineData("invalid-json")]
    [InlineData("invalid-challenge")]
    public async Task Copy_MountAuthenticationFailureOtherThanDenial_DoesNotFallback(string failure)
    {
        using var cancellationSource = new CancellationTokenSource();
        var sourceHandler = CreateCopySource(includeBlob: false);
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(HttpMethod.Head, $"{Registry}/v2/destination/blobs/{Digest}",
            new HttpResponseMessage(HttpStatusCode.NotFound));
        destinationHandler.AddExpectedRequest(request => request.RequestUri!.Query.Contains("mount="),
            failure == "invalid-challenge" ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Challenge());
        var transportException = new HttpRequestException("Transport failure", null, HttpStatusCode.Forbidden);
        if (failure != "invalid-challenge")
        {
            destinationHandler.AddExpectedRequest(request =>
            {
                Assert.Equal("auth.example", request.RequestUri!.Host);
                if (failure == "transport")
                {
                    throw transportException;
                }
                if (failure == "canceled")
                {
                    cancellationSource.Cancel();
                }
                return true;
            }, new HttpResponseMessage(failure switch
            {
                "server-error" => HttpStatusCode.InternalServerError,
                "canceled" => HttpStatusCode.Forbidden,
                _ => HttpStatusCode.OK
            }) { Content = new StringContent("not-json") });
        }
        using var source = new RegistryClient(Registry, new BasicAuthenticationCredentials("reader", "password"), sourceHandler);
        using var destination = new RegistryClient(Registry, Credentials("basic"), destinationHandler);
        Task Copy() => source.CopyAsync("source", "latest", destination, "destination", "latest", cancellationSource.Token);

        switch (failure)
        {
            case "transport":
                Assert.Same(transportException, await Assert.ThrowsAsync<HttpRequestException>(Copy));
                break;
            case "canceled":
                await Assert.ThrowsAnyAsync<OperationCanceledException>(Copy);
                break;
            case "server-error":
                Assert.Equal(HttpStatusCode.InternalServerError,
                    (await Assert.ThrowsAsync<HttpRequestException>(Copy)).StatusCode);
                break;
            case "invalid-json":
                await Assert.ThrowsAsync<JsonException>(Copy);
                break;
            case "invalid-challenge":
                await Assert.ThrowsAsync<AuthenticationException>(Copy);
                break;
        }
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    private static IRegistryClientCredentials? Credentials(string kind) => kind switch
    {
        "basic" => new BasicAuthenticationCredentials("writer", "password"),
        "refresh" => new TokenCredentials("original-refresh"),
        "anonymous" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void AddTokenRequest(MockHttpMessageHandler handler, string credentials, string accessToken) =>
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("auth.example", request.RequestUri!.Host);
            if (credentials == "refresh")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Null(request.Headers.Authorization);
                Assert.Contains("refresh_token=original-refresh",
                    request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            else
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Null(request.Content);
                if (credentials == "basic")
                {
                    AssertBasicCredentials(request, "writer");
                }
                else
                {
                    Assert.Null(request.Headers.Authorization);
                }
            }
            return true;
        }, new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"access_token":"{{accessToken}}"}""")
        });

    private static void AssertBasicCredentials(HttpRequestMessage request, string user)
    {
        Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:password")),
            request.Headers.Authorization?.Parameter);
    }

    private static HttpResponseMessage Challenge(string scope = "repository:destination:push")
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
            "Bearer", $"realm=\"https://auth.example/token\",scope=\"{scope}\""));
        return response;
    }

    private static HttpResponseMessage UploadResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Location = new Uri(UploadLocation, UriKind.Relative);
        response.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        response.Headers.Add("Range", "0-3");
        response.Headers.Add("Docker-Content-Digest", Digest);
        return response;
    }

    private static void AssertUploadBody(HttpRequestMessage request, string operation)
    {
        if (operation == "complete")
        {
            Assert.Null(request.Content);
        }
        else
        {
            Assert.Equal(Blob, Serialize(request.Content!));
        }
    }

    private static byte[] Serialize(HttpContent content)
    {
        using var destination = new MemoryStream();
        content.CopyToAsync(destination).GetAwaiter().GetResult();
        return destination.ToArray();
    }

    private static MockHttpMessageHandler CreateCopySource(bool includeBlob = true)
    {
        var handler = new MockHttpMessageHandler();
        var manifest = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"schemaVersion":2,"mediaType":"{{ManifestMediaTypes.OciManifestSchema1}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"{{Digest}}","size":4},"layers":[]}""",
                Encoding.UTF8, ManifestMediaTypes.OciManifestSchema1)
        };
        manifest.Headers.Add("Docker-Content-Digest", "custom:root");
        handler.AddExpectedRequest(request =>
        {
            AssertBasicCredentials(request, "reader");
            Assert.Equal($"{Registry}/v2/source/manifests/latest", request.RequestUri!.AbsoluteUri);
            return true;
        }, manifest);
        if (includeBlob)
        {
            handler.AddExpectedRequest(request =>
            {
                AssertBasicCredentials(request, "reader");
                Assert.Equal($"{Registry}/v2/source/blobs/{Digest}", request.RequestUri!.AbsoluteUri);
                return true;
            }, new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Blob) });
        }
        return handler;
    }

    private sealed class DisposalTrackingContent(string content = "denied") : StringContent(content)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
