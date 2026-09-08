using System.Net;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class ReplayableStreamContentTests
{
    [Fact]
    public void Constructor_UsesNonDuplexStreamContentTransportContract()
    {
        using var content = new ReplayableStreamContent(new MemoryStream());

        Assert.IsAssignableFrom<StreamContent>(content);
    }

    [Fact]
    public async Task CopyToAsync_MultipleSerializationsReplayFromInitialPosition()
    {
        using var sourceStream = new MemoryStream([1, 2, 3, 4]);
        sourceStream.Position = 1;
        using var content = new ReplayableStreamContent(sourceStream);
        using var firstDestination = new MemoryStream();
        using var secondDestination = new MemoryStream();

        await content.CopyToAsync(firstDestination);
        await content.CopyToAsync(secondDestination);

        Assert.Equal(3, content.Headers.ContentLength);
        Assert.Equal([2, 3, 4], firstDestination.ToArray());
        Assert.Equal(firstDestination.ToArray(), secondDestination.ToArray());
    }

    [Fact]
    public async Task PrepareForReplay_UnconsumedNonSeekableStream_AllowsFirstSerialization()
    {
        using var sourceStream = new NonSeekableReadStream([1, 2, 3]);
        using var content = new ReplayableStreamContent(sourceStream);
        using var destination = new MemoryStream();

        content.PrepareForReplay();
        await content.CopyToAsync(destination);

        Assert.Equal([1, 2, 3], destination.ToArray());
    }

    [Fact]
    public async Task CopyToAsync_CanceledSerialization_InterruptsSourceRead()
    {
        using var sourceStream = new BlockingReadStream();
        using var content = new ReplayableStreamContent(sourceStream);
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
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => readStarted.Task;

        public void Release() => release.TrySetResult();

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => 1;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            readStarted.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            readStarted.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = offset;
            return Position;
        }

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
