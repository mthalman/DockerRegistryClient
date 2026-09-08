using System.Net;

namespace Valleysoft.DockerRegistryClient;

// StreamContent disables duplex request streaming so retries cannot overlap the original upload.
internal sealed class ReplayableStreamContent : StreamContent, IReplayableHttpContent
{
    private const int BufferSize = 81920;
    private readonly Stream stream;
    private readonly long? initialPosition;
    private readonly bool disposeStreamAfterCopy;
    private bool contentConsumed;

    public ReplayableStreamContent(Stream stream)
        : base(stream)
    {
        this.stream = stream;
        initialPosition = stream.CanSeek ? stream.Position : null;
        disposeStreamAfterCopy = !stream.CanSeek;
    }

    protected override Task SerializeToStreamAsync(
        Stream destination,
        TransportContext? context) =>
        SerializeToStreamAsync(destination, CancellationToken.None);

#if NET5_0_OR_GREATER
    protected override void SerializeToStream(
        Stream destination,
        TransportContext? context,
        CancellationToken cancellationToken)
    {
        PrepareForSerialization();
        try
        {
            stream.CopyTo(destination, BufferSize);
        }
        finally
        {
            DisposeStreamAfterCopy();
        }
    }

    protected override Task SerializeToStreamAsync(
        Stream destination,
        TransportContext? context,
        CancellationToken cancellationToken) =>
        SerializeToStreamAsync(destination, cancellationToken);
#endif

    private async Task SerializeToStreamAsync(
        Stream destination,
        CancellationToken cancellationToken)
    {
        PrepareForSerialization();

        try
        {
            await stream.CopyToAsync(
                destination,
                BufferSize,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DisposeStreamAfterCopy();
        }
    }

    private void PrepareForSerialization()
    {
        if (contentConsumed)
        {
            PrepareForReplay();
        }

        contentConsumed = true;
    }

    private void DisposeStreamAfterCopy()
    {
        if (disposeStreamAfterCopy)
        {
            stream.Dispose();
        }
    }

    public void PrepareForReplay()
    {
        if (!contentConsumed)
        {
            return;
        }

        if (initialPosition is null)
        {
            throw new InvalidOperationException(
                "Request content backed by a non-seekable stream cannot be safely replayed.");
        }

        stream.Position = initialPosition.Value;
    }
}
