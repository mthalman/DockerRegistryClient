using System.Net;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class ReplayableBlobContentTests
{
    [Fact]
    public async Task ReadAsStreamAsync_AllocatesOnlyOneBlobSizedBuffer()
    {
        const int size = 4 * 1024 * 1024;
        var sourceHandler = new MockHttpMessageHandler();
        using var sourceStream = new MemoryStream(new byte[size]);
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/blobs/custom:blob",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(sourceStream) });
        using var sourceClient = new RegistryClient("source.example", null, sourceHandler);
        using var content = new ReplayableBlobContent(
            (BlobOperations)sourceClient.Blobs, "source", "custom:blob", size, CancellationToken.None);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        Task<Stream> readTask = content.ReadAsStreamAsync();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.True(readTask.IsCompletedSuccessfully);
        using Stream result = await readTask;
        Assert.Equal(size, result.Length);
        Assert.False(result.CanWrite);
        Assert.InRange(allocated, size, size + 512 * 1024);
    }

    [Fact]
    public async Task ReadAsStreamAsync_BlobExceedsBufferLimit_RejectsBeforeDownloading()
    {
        var sourceHandler = new MockHttpMessageHandler();
        using var sourceClient = new RegistryClient("source.example", null, sourceHandler);
        using var content = new ReplayableBlobContent(
            (BlobOperations)sourceClient.Blobs, "source", "custom:blob",
            (long)int.MaxValue + 1, CancellationToken.None);

        await Assert.ThrowsAsync<HttpRequestException>(() => content.ReadAsStreamAsync());

        Assert.Equal(0, sourceHandler.RequestCount);
    }

    [Fact]
    public async Task ReadAsStreamAsync_CanceledSerialization_DisposesSource()
    {
        using var sourceStream = new BlockingReadStream([1, 2, 3, 4]);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/blobs/custom:blob",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(sourceStream) });
        using var sourceClient = new RegistryClient("source.example", null, sourceHandler);
        using var content = new ReplayableBlobContent(
            (BlobOperations)sourceClient.Blobs, "source", "custom:blob", 4, CancellationToken.None);
        using var cancellationSource = new CancellationTokenSource();

        Task<Stream> readTask = content.ReadAsStreamAsync(cancellationSource.Token);
        await sourceStream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => readTask.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            sourceStream.Release();
        }

        Assert.False(sourceStream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsStreamAsync_SerializesBlobInsteadOfBasePlaceholder(bool withCancellationToken)
    {
        var sourceHandler = new MockHttpMessageHandler();
        using var sourceStream = new MemoryStream([1, 2, 3, 4]);
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/blobs/custom:blob",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(sourceStream) });
        using var sourceClient = new RegistryClient("source.example", null, sourceHandler);
        using var content = new ReplayableBlobContent(
            (BlobOperations)sourceClient.Blobs, "source", "custom:blob", 4, CancellationToken.None);
        using var cancellationSource = new CancellationTokenSource();

        using Stream result = withCancellationToken
            ? await content.ReadAsStreamAsync(cancellationSource.Token)
            : await content.ReadAsStreamAsync();
        using var destination = new MemoryStream();
        await result.CopyToAsync(destination);
        await content.EnsureSerializationCompletedAsync();

        Assert.Equal([1, 2, 3, 4], destination.ToArray());
        Assert.False(result.CanWrite);
        Assert.False(sourceStream.CanRead);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task UnserializedContent_RejectsCompletionAndSynchronousPlaceholderReads()
    {
        var sourceHandler = new MockHttpMessageHandler();
        using var sourceClient = new RegistryClient("source.example", null, sourceHandler);
        using var content = new ReplayableBlobContent(
            (BlobOperations)sourceClient.Blobs, "source", "custom:blob", 4, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => content.EnsureSerializationCompletedAsync());
        Assert.Throws<NotSupportedException>(() => content.ReadAsStream());
        Assert.Throws<NotSupportedException>(() => content.CopyTo(Stream.Null, null, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_RedirectReplaysBlobFromBeginning()
    {
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/blobs/custom:blob",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream([1, 2, 3, 4]))
            });
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/blobs/custom:blob",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream([1, 2, 3, 4]))
            });
        using var sourceClient = new RegistryClient(
            "source.example",
            serviceClientCredentials: null,
            sourceHandler);
        using var content = new ReplayableBlobContent(
            (BlobOperations)sourceClient.Blobs,
            "source",
            "custom:blob",
            4,
            CancellationToken.None);
        var destinationHandler = new MockHttpMessageHandler();
        destinationHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3, 4 }),
            CreateRedirectResponse("/destination"));
        destinationHandler.AddExpectedRequest(
            request => ReadContent(request.Content!).SequenceEqual(new byte[] { 1, 2, 3, 4 }),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var destinationClient =
            new HttpClient(new RedirectDelegatingHandler(destinationHandler));
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://destination.example/source")
        {
            Content = content
        };

        using HttpResponseMessage response = await destinationClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, sourceHandler.RemainingRequestCount);
        Assert.Equal(0, destinationHandler.RemainingRequestCount);
    }

    [Fact]
    public async Task CopyToAsync_CanceledSerialization_InterruptsSourceRead()
    {
        var sourceStream = new BlockingReadStream([1, 2, 3, 4]);
        var sourceHandler = new MockHttpMessageHandler();
        sourceHandler.AddExpectedRequest(
            HttpMethod.Get,
            "https://source.example/v2/source/blobs/custom:blob",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(sourceStream)
            });
        using var sourceClient = new RegistryClient(
            "source.example",
            serviceClientCredentials: null,
            sourceHandler);
        using var content = new ReplayableBlobContent(
            (BlobOperations)sourceClient.Blobs,
            "source",
            "custom:blob",
            4,
            CancellationToken.None);
        using var cancellationSource = new CancellationTokenSource();

        Task copyTask = content.CopyToAsync(
            Stream.Null,
            context: null,
            cancellationSource.Token);
        await sourceStream.ReadStarted;
        cancellationSource.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => copyTask.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            sourceStream.Release();
        }

        Assert.Equal(0, sourceHandler.RemainingRequestCount);
    }

    private sealed class BlockingReadStream(byte[] buffer) : MemoryStream(buffer)
    {
        private readonly TaskCompletionSource readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => readStarted.Task;

        public void Release() => release.TrySetResult();

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            readStarted.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, offset, count, cancellationToken);
        }
    }

    private static byte[] ReadContent(HttpContent content)
    {
        using var stream = new MemoryStream();
        content.CopyToAsync(stream).GetAwaiter().GetResult();
        return stream.ToArray();
    }

    private static HttpResponseMessage CreateRedirectResponse(string location) =>
        new(HttpStatusCode.TemporaryRedirect)
        {
            Headers =
            {
                Location = new Uri(location, UriKind.RelativeOrAbsolute)
            }
        };
}
