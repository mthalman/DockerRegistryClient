namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Represents the streamed response to a ranged blob download request.
/// </summary>
public class BlobDownloadResult
{
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
