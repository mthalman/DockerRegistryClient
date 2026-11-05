using System.Net;
using System.Net.Http.Headers;

namespace Valleysoft.DockerRegistryClient;

// StreamContent's non-duplex contract keeps SendAsync awaiting validation and source disposal,
// even when an HTTP/2 destination responds before the upload finishes.
internal sealed class ReplayableBlobContent : StreamContent, IReplayableHttpContent
{
    private const int BufferSize = 81920;
    private readonly BlobOperations source;
    private readonly string repositoryName;
    private readonly string digest;
    private readonly long size;
    private readonly CancellationToken copyCancellationToken;
    private Task? serializationTask;
    private byte[]? bufferedContent;

    public ReplayableBlobContent(
        BlobOperations source,
        string repositoryName,
        string digest,
        long size,
        CancellationToken cancellationToken)
        : base(Stream.Null)
    {
        this.source = source;
        this.repositoryName = repositoryName;
        this.digest = digest;
        this.size = size;
        copyCancellationToken = cancellationToken;
        Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = size;
        return true;
    }

    // The base stream only selects the transport contract; unbuffered copies open their own source.
    protected override void SerializeToStream(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken)
    {
        if (bufferedContent is null)
        {
            throw new NotSupportedException("Blob copy content requires asynchronous serialization.");
        }

        stream.Write(bufferedContent, 0, bufferedContent.Length);
    }

    protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Blob copy content requires asynchronous serialization.");

    protected override Task<Stream> CreateContentReadStreamAsync() =>
        CreateContentReadStreamAsync(CancellationToken.None);

    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
    {
        if (size > int.MaxValue)
        {
            throw new HttpRequestException("The blob is too large to buffer in memory.");
        }

        using var buffer = new MemoryStream((int)size);
        await CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        bufferedContent = buffer.GetBuffer();
        return new MemoryStream(bufferedContent, 0, (int)buffer.Length, writable: false);
    }

    protected override Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context) =>
        serializationTask = SerializeToStreamAsync(
            stream,
            copyCancellationToken);

#if NET5_0_OR_GREATER
    protected override Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken) =>
        serializationTask = SerializeWithLinkedCancellationAsync(stream, cancellationToken);

    private async Task SerializeWithLinkedCancellationAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linkedCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                copyCancellationToken,
                cancellationToken);
        await SerializeToStreamAsync(
            stream,
            linkedCancellationSource.Token).ConfigureAwait(false);
    }
#endif

    // HTTP/2 may suppress serialization errors after a successful response followed by RST_STREAM(NO_ERROR).
    public Task EnsureSerializationCompletedAsync() =>
        serializationTask ?? Task.FromException(
            new InvalidOperationException("The destination accepted the upload without serializing the blob."));

    private async Task SerializeToStreamAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (bufferedContent is not null)
        {
            await stream.WriteAsync(
                bufferedContent,
                0,
                bufferedContent.Length,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        using Stream sourceStream = await source.GetForCopyAsync(
            repositoryName,
            digest,
            cancellationToken).ConfigureAwait(false);
        byte[] buffer = new byte[BufferSize];
        long remaining = size;
        while (remaining > 0)
        {
            int bytesRead = await sourceStream.ReadAsync(
                buffer,
                0,
                (int)Math.Min(buffer.Length, remaining),
                cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw CreateSizeMismatchException(size - remaining);
            }

            await stream.WriteAsync(
                buffer,
                0,
                bytesRead,
                cancellationToken).ConfigureAwait(false);
            remaining -= bytesRead;
        }

        if (await sourceStream.ReadAsync(
            buffer,
            0,
            1,
            cancellationToken).ConfigureAwait(false) != 0)
        {
            throw new InvalidOperationException(
                $"Blob '{digest}' contains more data than its declared size of {size}.");
        }
    }

    private InvalidOperationException CreateSizeMismatchException(long actualSize) =>
        new($"Blob '{digest}' has size {actualSize}, but its descriptor declares {size}.");

    public void PrepareForReplay()
    {
    }
}
