using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class BlobOperationsTests
{
    private static readonly string Digest = RegistryFixture.GetDigest([1, 2, 3, 4]);

    [Theory]
    [InlineData(HttpStatusCode.OK, true, "")]
    [InlineData(HttpStatusCode.NoContent, true, "")]
    [InlineData(HttpStatusCode.NotFound, false, "")]
    [InlineData(HttpStatusCode.NotFound, false, null)]
    [InlineData(HttpStatusCode.NotFound, false, """{"errors":[{"code":"BLOB_UNKNOWN","message":"blob unknown"}]}""")]
    public async Task ExistsAsync_SuccessOrNotFound_ReturnsExpectedResult(
        HttpStatusCode statusCode, bool expected, string? content)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            new HttpResponseMessage(statusCode)
            {
                Content = content is null ? null : new StringContent(content)
            });
        using var client = CreateClient(handler);

        bool result = await client.Blobs.ExistsAsync("repo", Digest);

        Assert.Equal(expected, result);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ExistsAsync_Non404FailureWithoutBody_ThrowsRegistryException(HttpStatusCode statusCode)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            new HttpResponseMessage(statusCode));
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Blobs.ExistsAsync("repo", Digest));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Empty(exception.Errors);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task ExistsAsync_StructuredError_PreservesRegistryErrorDetails()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Head,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    """{"errors":[{"code":"DENIED","message":"access denied","detail":{"repository":"repo"}}]}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Blobs.ExistsAsync("repo", Digest));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Error error = Assert.Single(exception.Errors);
        Assert.Equal("DENIED", error.Code);
        Assert.Equal("access denied", error.Message);
        JsonElement detail = Assert.IsType<JsonElement>(error.Detail);
        Assert.Equal("repo", detail.GetProperty("repository").GetString());
    }

    [Fact]
    public async Task GetAsync_ReturnedStreamOwnsResponseLifetime()
    {
        var body = new MemoryStream([1, 2, 3]);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Get &&
                request.RequestUri == new Uri($"https://registry.example/v2/repo/blobs/{Digest}"),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
            });
        using var client = CreateClient(handler);

        Stream result = await client.Blobs.GetAsync("repo", Digest);

        Assert.True(body.CanRead);
        result.Dispose();
        Assert.False(body.CanRead);
    }

    [Fact]
    public async Task GetAsync_BuffersResponseBeforeReturning()
    {
        var body = new ThrowOnReadStream([1, 2, 3]);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
            });
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blobs.GetAsync("repo", Digest));
    }

    [Fact]
    public async Task GetRangeAsync_BoundedRange_ReturnsPartialContentMetadata()
    {
        var body = new MemoryStream([5, 6, 7]);
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new StreamContent(body)
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(5, 7, 10);
        response.Content.Headers.ContentLength = 3;
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Get &&
                request.RequestUri == new Uri($"https://registry.example/v2/repo/blobs/{Digest}") &&
                request.Headers.Range?.ToString() == "bytes=5-7" &&
                request.Headers.AcceptEncoding.Single().Value == "identity",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", Digest, 5, 3);

        Assert.True(result.IsRangeHonored);
        Assert.Equal(5, result.RangeStart);
        Assert.Equal(7, result.RangeEnd);
        Assert.Equal(10, result.TotalLength);
        Assert.Equal([5, 6, 7], await ReadAllBytesAsync(result.Content));
        result.Content.Dispose();
        Assert.False(body.CanRead);
    }

    [Fact]
    public async Task GetRangeAsync_OpenEndedRange_SendsRangeWithoutEnd()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent([8, 9])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(8, 9, 10);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Headers.Range?.ToString() == "bytes=8-",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", Digest, 8);

        Assert.True(result.IsRangeHonored);
        Assert.Equal(8, result.RangeStart);
        Assert.Equal(9, result.RangeEnd);
        Assert.Equal(10, result.TotalLength);
        result.Content.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetRangeAsync_ReturnedStreamMemoryRead_ForwardsBufferAndCancellationToken(bool cancel)
    {
        using var body = new TrackingMemoryStream([5, 6, 7]);
        using var response = CreateTrackingRangeResponse(body);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            response);
        using var client = CreateClient(handler);
        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", Digest, 0, 3);
        using Stream stream = result.Content;
        using var cancellationSource = new CancellationTokenSource();
        byte[] buffer = [9, 9, 9, 9, 9];

        Assert.True(body.CanRead);
        Assert.Equal(0, body.MemoryReadCount);
        if (cancel)
        {
            cancellationSource.Cancel();
            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => stream.ReadAsync(buffer.AsMemory(1, 3), cancellationSource.Token).AsTask());
            Assert.Equal(cancellationSource.Token, exception.CancellationToken);
            Assert.Equal([9, 9, 9, 9, 9], buffer);
            Assert.Equal(0, body.Position);
        }
        else
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(1, 3), cancellationSource.Token);

            Assert.Equal(3, bytesRead);
            Assert.Equal([9, 5, 6, 7, 9], buffer);
            Assert.Equal(3, body.Position);
        }

        Assert.Equal(1, body.MemoryReadCount);
        Assert.Equal(buffer.AsMemory(1, 3), body.LastReadBuffer);
        Assert.Equal(cancellationSource.Token, body.LastCancellationToken);
        Assert.True(body.CanRead);
        Assert.Equal(0, handler.RemainingRequestCount);
        stream.Dispose();
        Assert.False(body.CanRead);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => response.Content.ReadAsStreamAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetRangeAsync_ReturnedStreamMemoryWrite_ForwardsBufferAndCancellationToken(bool cancel)
    {
        using var body = new TrackingMemoryStream([5, 6, 7]);
        using var response = CreateTrackingRangeResponse(body);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            response);
        using var client = CreateClient(handler);
        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", Digest, 0, 3);
        using Stream stream = result.Content;
        using var cancellationSource = new CancellationTokenSource();
        byte[] buffer = [9, 1, 2, 9];
        ReadOnlyMemory<byte> payload = buffer.AsMemory(1, 2);

        Assert.True(stream.CanWrite);
        Assert.Equal(0, body.MemoryWriteCount);
        if (cancel)
        {
            cancellationSource.Cancel();
            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => stream.WriteAsync(payload, cancellationSource.Token).AsTask());
            Assert.Equal(cancellationSource.Token, exception.CancellationToken);
            Assert.Equal([5, 6, 7], body.ToArray());
            Assert.Equal(0, body.Position);
        }
        else
        {
            await stream.WriteAsync(payload, cancellationSource.Token);

            Assert.Equal([1, 2, 7], body.ToArray());
            Assert.Equal(2, body.Position);
        }

        Assert.Equal(1, body.MemoryWriteCount);
        Assert.Equal(payload, body.LastWriteBuffer);
        Assert.Equal(cancellationSource.Token, body.LastCancellationToken);
        Assert.True(body.CanWrite);
        Assert.Equal(0, handler.RemainingRequestCount);
        stream.Dispose();
        Assert.False(body.CanWrite);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => response.Content.ReadAsStreamAsync());
    }

    [Fact]
    public async Task GetRangeAsync_RangeIgnored_ReturnsFullContentMetadata()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3, 4])
        };
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Headers.Range?.ToString() == "bytes=2-3",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", Digest, 2, 2);

        Assert.False(result.IsRangeHonored);
        Assert.Equal(0, result.RangeStart);
        Assert.Equal(3, result.RangeEnd);
        Assert.Equal(4, result.TotalLength);
        Assert.Equal([1, 2, 3, 4], await ReadAllBytesAsync(result.Content));
        result.Content.Dispose();
    }

    [Theory]
    [InlineData(-1, null, "offset")]
    [InlineData(0L, 0L, "length")]
    [InlineData(0L, -1L, "length")]
    [InlineData(long.MaxValue, 2L, "length")]
    public async Task GetRangeAsync_InvalidRange_Throws(
        long offset,
        long? length,
        string parameterName)
    {
        using var client = CreateClient(new MockHttpMessageHandler());

        ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.Blobs.GetRangeAsync("repo", Digest, offset, length));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(long.MaxValue, 1L, "bytes=9223372036854775807-9223372036854775807")]
    [InlineData(long.MaxValue - 1, 2L, "bytes=9223372036854775806-9223372036854775807")]
    public async Task GetRangeAsync_MaximumValidEndOffset_SendsRequest(
        long offset,
        long length,
        string expectedRange)
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Headers.Range?.ToString() == expectedRange,
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            });
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", Digest, offset, length);

        result.Content.Dispose();
    }

    [Fact]
    public async Task GetRangeAsync_RangeNotSatisfiable_ThrowsRegistryException()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                Content = new StringContent("""{"errors":[{"code":"RANGE_INVALID","message":"invalid range"}]}""")
            });
        using var client = CreateClient(handler);

        RegistryException exception = await Assert.ThrowsAsync<RegistryException>(
            () => client.Blobs.GetRangeAsync("repo", Digest, 10));

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, exception.StatusCode);
    }

    [Fact]
    public async Task GetRangeAsync_MissingContentRange_ThrowsAndDisposesResponse()
    {
        var body = new MemoryStream([1, 2, 3]);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new StreamContent(body)
            });
        using var client = CreateClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blobs.GetRangeAsync("repo", Digest, 0, 3));

        Assert.Contains("Content-Range", exception.Message);
        Assert.False(body.CanRead);
    }

    [Fact]
    public async Task GetRangeAsync_InconsistentContentRange_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent([1, 2])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(1, 2, 10);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            response);
        using var client = CreateClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blobs.GetRangeAsync("repo", Digest, 0, 2));

        Assert.Contains("inconsistent", exception.Message);
    }

    [Theory]
    [InlineData(5, 9, 5, 7)]
    [InlineData(5, 9, 6, 7)]
    public async Task GetRangeAsync_ContainedPartialResponse_ReturnsActualRange(
        long requestedStart,
        long requestedEnd,
        long returnedStart,
        long returnedEnd)
    {
        int returnedLength = checked((int)(returnedEnd - returnedStart + 1));
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[returnedLength])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(returnedStart, returnedEnd, 20);
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request => request.Headers.Range?.ToString() == $"bytes={requestedStart}-{requestedEnd}",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync(
            "repo",
            Digest,
            requestedStart,
            requestedEnd - requestedStart + 1);

        Assert.True(result.IsRangeHonored);
        Assert.Equal(returnedStart, result.RangeStart);
        Assert.Equal(returnedEnd, result.RangeEnd);
        result.Content.Dispose();
    }

    [Fact]
    public async Task GetRangeAsync_PreCanceledToken_DoesNotSendRequest()
    {
        using var client = CreateClient(new MockHttpMessageHandler());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Blobs.GetRangeAsync("repo", Digest, 0, cancellationToken: cancellationSource.Token));
    }

    [Fact]
    public async Task GetRangeAsync_DoesNotBufferResponseBody()
    {
        var body = new ThrowOnReadStream([1, 2, 3]);
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new StreamContent(body)
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 2, 3);
        response.Content.Headers.ContentLength = 3;
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            $"https://registry.example/v2/repo/blobs/{Digest}",
            response);
        using var client = CreateClient(handler);

        BlobDownloadResult result = await client.Blobs.GetRangeAsync("repo", Digest, 0, 3);

        result.Content.Dispose();
    }

    [Fact]
    public async Task UploadPrimitives_ReuseAuthorizationAndParseResponseHeaders()
    {
        var uploadId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        var beginResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        beginResponse.Headers.Location = new Uri("/v2/repo/blobs/uploads/upload-id", UriKind.Relative);
        beginResponse.Headers.Add("Docker-Upload-UUID", uploadId.ToString());
        handler.AddExpectedRequest(
            request => request.Method == HttpMethod.Post &&
                request.RequestUri == new Uri("https://registry.example/v2/repo/blobs/uploads/") &&
                request.Headers.Authorization?.Parameter == "credential-token",
            beginResponse);

        var streamResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        streamResponse.Headers.Location = new Uri("/v2/repo/blobs/uploads/upload-id", UriKind.Relative);
        streamResponse.Headers.Add("Docker-Upload-UUID", uploadId.ToString());
        streamResponse.Headers.Add("Range", "0-2");
        handler.AddExpectedRequest(
            request =>
            {
                byte[] content = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return request.Method.Method == "PATCH" &&
                    request.RequestUri!.OriginalString == "https://registry.example/v2/repo/blobs/uploads/upload-id" &&
                    request.Headers.Authorization?.Parameter == "credential-token" &&
                    request.Content.Headers.ContentType?.MediaType == "application/octet-stream" &&
                    content.SequenceEqual(new byte[] { 1, 2, 3 });
            },
            streamResponse);

        var endResponse = new HttpResponseMessage(HttpStatusCode.Created);
        endResponse.Headers.Location = new Uri($"/v2/repo/blobs/{Digest}", UriKind.Relative);
        endResponse.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(
            request =>
            {
                byte[] content = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return request.Method == HttpMethod.Put &&
                    request.RequestUri!.OriginalString ==
                        $"https://registry.example/v2/repo/blobs/uploads/upload-id?digest={Uri.EscapeDataString(Digest)}" &&
                    request.Headers.Authorization?.Parameter == "credential-token" &&
                    request.Content.Headers.ContentType?.MediaType == "application/octet-stream" &&
                    content.SequenceEqual(new byte[] { 4 });
            },
            endResponse);

        using var client = new RegistryClient(
            "registry.example",
            new TokenCredentials("credential-token"),
            new HttpClient(handler),
            disposeHttpClient: true);

        BlobUploadInitializationResult initialization = await client.Blobs.BeginUploadAsync("repo");
        BlobUploadStreamResult streamResult = await client.Blobs.SendUploadStreamAsync(
            initialization.Location,
            new MemoryStream([1, 2, 3]),
            initialization.UploadContext);
        BlobUploadResult result = await client.Blobs.EndUploadAsync(
            streamResult.Location,
            Digest,
            initialization.UploadContext,
            new MemoryStream([4]));

        Assert.Equal(uploadId, initialization.UploadId);
        Assert.Equal(2, streamResult.RangeOffset);
        Assert.Equal($"https://registry.example/v2/repo/blobs/{Digest}", result.Location);
        Assert.Equal(Digest, result.Digest);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task GetUploadAsync_InvalidRangeHeader_ThrowsDescriptiveException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        response.Headers.Add("Docker-Upload-UUID", Guid.NewGuid().ToString());
        response.Headers.Add("Range", "invalid");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            HttpMethod.Get,
            "https://registry.example/v2/repo/blobs/uploads/upload-id",
            response);
        using var client = CreateClient(handler);

        Exception exception = await Assert.ThrowsAsync<Exception>(
            () => client.Blobs.GetUploadAsync("/v2/repo/blobs/uploads/upload-id"));

        Assert.Contains("Expected '0-<offset>'", exception.Message);
    }

    [Fact]
    public async Task SendUploadStreamAsync_CrossOriginLocationDoesNotForwardCredentials()
    {
        var uploadId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Location = new Uri("https://uploads.example/opaque/%41%7E%2F", UriKind.Absolute);
        response.Headers.Add("Docker-Upload-UUID", uploadId.ToString());
        response.Headers.Add("Range", "0-0");
        var handler = new MockHttpMessageHandler();
        handler.AddExpectedRequest(
            request =>
                request.RequestUri!.OriginalString == "https://uploads.example/opaque/%41%7E%2F" &&
                request.Headers.Authorization is null,
            response);
        using var client = new RegistryClient(
            "registry.example",
            new TokenCredentials("configured-token"),
            new HttpClient(handler),
            disposeHttpClient: true);
        var uploadContext = new BlobUploadContext(
            new AuthenticationHeaderValue("Bearer", "upload-token"), client.BaseUri);

        await client.Blobs.SendUploadStreamAsync(
            "https://uploads.example/opaque/%41%7E%2F",
            new MemoryStream([1]),
            uploadContext);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Fact]
    public async Task UploadPrimitives_UseNormalizedResponseLocationThroughCompletion()
    {
        const string Location = "/upload/%41%7E?state=hello%20world";
        const string AbsoluteLocation = "https://registry.example/upload/A~?state=hello%20world";
        var handler = new MockHttpMessageHandler();
        var beginResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        beginResponse.Headers.TryAddWithoutValidation("Location", Location);
        beginResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        handler.AddExpectedRequest(HttpMethod.Post, "https://registry.example/v2/repo/blobs/uploads/", beginResponse);
        var patchResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        patchResponse.Headers.TryAddWithoutValidation("Location", Location);
        patchResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        patchResponse.Headers.Add("Range", "0-0");
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("PATCH", request.Method.Method);
            Assert.Equal(AbsoluteLocation, request.RequestUri!.AbsoluteUri);
            return true;
        }, patchResponse);
        var endResponse = new HttpResponseMessage(HttpStatusCode.Created);
        endResponse.Headers.Location = new Uri($"/v2/repo/blobs/{Digest}", UriKind.Relative);
        endResponse.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal($"{AbsoluteLocation}&digest={Uri.EscapeDataString(Digest)}", request.RequestUri!.AbsoluteUri);
            return true;
        }, endResponse);
        using var client = new RegistryClient("registry.example", null, handler);
        using var stream = new MemoryStream([1]);

        BlobUploadInitializationResult initialization = await client.Blobs.BeginUploadAsync("repo");
        Assert.Equal(AbsoluteLocation, initialization.Location);
        BlobUploadStreamResult chunk = await client.Blobs.SendUploadStreamAsync(
            initialization.Location, stream, initialization.UploadContext);
        Assert.Equal(AbsoluteLocation, chunk.Location);
        await client.Blobs.EndUploadAsync(chunk.Location, Digest, initialization.UploadContext);

        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("session/%41?state=%2541", "https://registry.example/v2/team/repo/blobs/uploads/session/A?state=%2541")]
    [InlineData("?session=%41", "https://registry.example/v2/team/repo/blobs/uploads/?session=A")]
    public async Task UploadPrimitives_ResolveRelativeLocationsAgainstEachRequest(
        string location, string expectedUploadUri)
    {
        var handler = new MockHttpMessageHandler();
        var beginResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        beginResponse.Headers.TryAddWithoutValidation("Location", location);
        beginResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        handler.AddExpectedRequest(HttpMethod.Post, "https://registry.example/v2/team/repo/blobs/uploads/", beginResponse);

        string expectedChunkUri = expectedUploadUri.Substring(0, expectedUploadUri.IndexOf('?')) + "?state=~%252F";
        var chunkResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        chunkResponse.Headers.TryAddWithoutValidation("Location", "?state=%7E%252F");
        chunkResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        chunkResponse.Headers.Add("Range", "0-0");
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("PATCH", request.Method.Method);
            Assert.Equal(expectedUploadUri, request.RequestUri!.AbsoluteUri);
            return true;
        }, chunkResponse);

        var endResponse = new HttpResponseMessage(HttpStatusCode.Created);
        endResponse.Headers.TryAddWithoutValidation("Location", "?download=%41");
        endResponse.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal($"{expectedChunkUri}&digest={Uri.EscapeDataString(Digest)}", request.RequestUri!.AbsoluteUri);
            return true;
        }, endResponse);
        using var client = new RegistryClient("registry.example", null, handler);
        using var stream = new MemoryStream([1]);

        BlobUploadInitializationResult initialization = await client.Blobs.BeginUploadAsync("team/repo");
        Assert.Equal(expectedUploadUri, initialization.Location);
        BlobUploadStreamResult chunk = await client.Blobs.SendUploadStreamAsync(
            initialization.Location, stream, initialization.UploadContext);
        Assert.Equal(expectedChunkUri, chunk.Location);
        BlobUploadResult result = await client.Blobs.EndUploadAsync(chunk.Location, Digest, initialization.UploadContext);

        Assert.Equal(expectedUploadUri.Substring(0, expectedUploadUri.IndexOf('?')) + "?download=A", result.Location);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadPrimitives_ResolveLocationsAgainstRedirectedRequest(bool customTransport)
    {
        const string RedirectUri = "https://uploads.example/sessions/start";
        const string UploadUri = "https://uploads.example/session/A?state=~";
        var handler = new MockHttpMessageHandler();
        var beginResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        beginResponse.Headers.TryAddWithoutValidation("Location", "/session/%41?state=%7E");
        beginResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        using var effectiveRequest = new HttpRequestMessage(HttpMethod.Post, RedirectUri);
        if (customTransport)
        {
            beginResponse.RequestMessage = effectiveRequest;
        }
        else
        {
            var redirectResponse = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            redirectResponse.Headers.Location = new Uri(RedirectUri);
            handler.AddExpectedRequest(HttpMethod.Post, "https://registry.example/v2/repo/blobs/uploads/", redirectResponse);
        }

        handler.AddExpectedRequest(
            HttpMethod.Post,
            customTransport ? "https://registry.example/v2/repo/blobs/uploads/" : RedirectUri,
            beginResponse);
        var chunkResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        chunkResponse.Headers.TryAddWithoutValidation("Location", "?state=%2541");
        chunkResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        chunkResponse.Headers.Add("Range", "0-0");
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal("PATCH", request.Method.Method);
            Assert.Equal(UploadUri, request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return true;
        }, chunkResponse);
        using var client = customTransport
            ? new RegistryClient("registry.example", new TokenCredentials("registry-token"), new HttpClient(handler), disposeHttpClient: true)
            : new RegistryClient("registry.example", new TokenCredentials("registry-token"), handler);
        using var stream = new MemoryStream([1]);

        BlobUploadInitializationResult initialization = await client.Blobs.BeginUploadAsync("repo");
        Assert.Equal(UploadUri, initialization.Location);
        BlobUploadStreamResult chunk = await client.Blobs.SendUploadStreamAsync(
            initialization.Location, stream, initialization.UploadContext);

        Assert.Equal("https://uploads.example/session/A?state=%2541", chunk.Location);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    [Theory]
    [InlineData("copy", false)]
    [InlineData("mount", false)]
    [InlineData("convenience", false)]
    [InlineData("copy", true)]
    [InlineData("mount", true)]
    [InlineData("convenience", true)]
    public async Task UploadEntryPoints_UseResolvedResponseLocation(string entryPoint, bool externalUpload)
    {
        string uploadUri = externalUpload
            ? "https://storage.example/upload/A?state=~"
            : "https://registry.example/v2/repo/blobs/uploads/A?state=~";
        var handler = new MockHttpMessageHandler();
        var beginResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        beginResponse.Headers.TryAddWithoutValidation("Location", externalUpload ? uploadUri : "%41?state=%7E");
        beginResponse.Headers.Add("Docker-Upload-UUID", Guid.Empty.ToString());
        string beginUri = "https://registry.example/v2/repo/blobs/uploads/";
        if (entryPoint == "mount")
        {
            beginUri += $"?mount={Uri.EscapeDataString(Digest)}&from=source";
        }

        handler.AddExpectedRequest(HttpMethod.Post, beginUri, beginResponse);
        var endResponse = new HttpResponseMessage(HttpStatusCode.Created);
        endResponse.Headers.TryAddWithoutValidation("Location", "/blobs/%41");
        endResponse.Headers.Add("Docker-Content-Digest", Digest);
        handler.AddExpectedRequest(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal($"{uploadUri}&digest={Uri.EscapeDataString(Digest)}", request.RequestUri!.AbsoluteUri);
            Assert.Equal(externalUpload ? null : "registry-token", request.Headers.Authorization?.Parameter);
            Assert.Equal(new byte[] { 1, 2, 3, 4 },
                request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult());
            return true;
        }, endResponse);
        using var client = new RegistryClient("registry.example", new TokenCredentials("registry-token"), handler);
        using var stream = new MemoryStream([1, 2, 3, 4]);

        BlobUploadResult result;
        if (entryPoint == "convenience")
        {
            result = await client.Blobs.UploadAsync("repo", stream, Digest);
        }
        else
        {
            var operations = Assert.IsType<BlobOperations>(client.Blobs);
            BlobUploadSession upload = entryPoint == "copy"
                ? await operations.BeginUploadForCopyAsync("repo")
                : (await operations.MountAsync("repo", Digest, "source")).Upload!;
            Assert.Equal(uploadUri, upload.Location);
            result = await operations.EndUploadForCopyAsync(
                upload.Location, Digest, upload.UploadContext, new StreamContent(stream));
        }

        Assert.Equal(externalUpload ? "https://storage.example/blobs/A" : "https://registry.example/blobs/A",
            result.Location);
        Assert.Equal(0, handler.RemainingRequestCount);
    }

    private static RegistryClient CreateClient(HttpMessageHandler handler) =>
        new("registry.example", null, new HttpClient(handler), disposeHttpClient: true);

    private static HttpResponseMessage CreateTrackingRangeResponse(Stream body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new DirectStreamContent(body)
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 2, 3);
        response.Content.Headers.ContentLength = 3;
        return response;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var destination = new MemoryStream();
        await stream.CopyToAsync(destination);
        return destination.ToArray();
    }

    private sealed class ThrowOnReadStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("The response body was buffered.");

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The response body was buffered.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The response body was buffered.");
    }

    private sealed class DirectStreamContent : StreamContent
    {
        private readonly Stream stream;

        public DirectStreamContent(Stream stream)
            : base(stream)
        {
            this.stream = stream;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(stream);
    }

    private sealed class TrackingMemoryStream(byte[] buffer) : MemoryStream(buffer)
    {
        public int MemoryReadCount { get; private set; }

        public int MemoryWriteCount { get; private set; }

        public Memory<byte> LastReadBuffer { get; private set; }

        public ReadOnlyMemory<byte> LastWriteBuffer { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            MemoryReadCount++;
            LastReadBuffer = buffer;
            LastCancellationToken = cancellationToken;
            return base.ReadAsync(buffer, cancellationToken);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            MemoryWriteCount++;
            LastWriteBuffer = buffer;
            LastCancellationToken = cancellationToken;
            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
