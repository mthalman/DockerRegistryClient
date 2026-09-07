using System.Net;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class ReplayableBlobContentTests
{
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
}
