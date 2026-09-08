using System.Net;
using System.Net.Http.Headers;

namespace Valleysoft.DockerRegistryClient;

internal sealed class ReplayableBlobContent : HttpContent, IReplayableHttpContent
{
    private const int BufferSize = 81920;
    private readonly BlobOperations source;
    private readonly string repositoryName;
    private readonly string digest;
    private readonly long size;
    private readonly CancellationToken copyCancellationToken;

    public ReplayableBlobContent(
        BlobOperations source,
        string repositoryName,
        string digest,
        long size,
        CancellationToken cancellationToken)
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

    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context)
    {
        await SerializeToStreamAsync(
            stream,
            copyCancellationToken).ConfigureAwait(false);
    }

#if NET5_0_OR_GREATER
    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
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

    private async Task SerializeToStreamAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
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
