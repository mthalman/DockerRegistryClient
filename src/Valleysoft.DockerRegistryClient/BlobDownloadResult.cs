namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Represents the streamed response to a ranged blob download request.
/// </summary>
public class BlobDownloadResult
{
    /// <summary>
    /// Initializes a result for a blob download response.
    /// </summary>
    /// <param name="content">Response content. Disposing the stream also disposes the underlying HTTP response.</param>
    /// <param name="isRangeHonored">Whether the registry honored the requested range.</param>
    /// <param name="rangeStart">Inclusive starting offset of the returned content, when known.</param>
    /// <param name="rangeEnd">Inclusive ending offset of the returned content, when known.</param>
    /// <param name="totalLength">Total blob length, when provided by the registry.</param>
    public BlobDownloadResult(
        Stream content,
        bool isRangeHonored,
        long? rangeStart,
        long? rangeEnd,
        long? totalLength)
    {
        Content = content;
        IsRangeHonored = isRangeHonored;
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;
        TotalLength = totalLength;
    }

    /// <summary>
    /// Gets the response content. Disposing this stream also disposes the underlying HTTP response.
    /// </summary>
    public Stream Content { get; }

    /// <summary>
    /// Gets a value indicating whether the registry honored the requested range.
    /// </summary>
    public bool IsRangeHonored { get; }

    /// <summary>
    /// Gets the inclusive starting offset of the returned content, when known.
    /// </summary>
    public long? RangeStart { get; }

    /// <summary>
    /// Gets the inclusive ending offset of the returned content, when known.
    /// </summary>
    public long? RangeEnd { get; }

    /// <summary>
    /// Gets the total length of the blob, when provided by the registry.
    /// </summary>
    public long? TotalLength { get; }
}
