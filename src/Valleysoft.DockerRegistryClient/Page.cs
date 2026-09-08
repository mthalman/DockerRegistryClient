namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Represents one page of a paginated registry response.
/// </summary>
/// <typeparam name="T">Type of the page value.</typeparam>
public class Page<T>
{
    /// <summary>
    /// Initializes a page of results.
    /// </summary>
    /// <param name="value">Value returned for the current page.</param>
    /// <param name="nextPageLink">Registry URL for the next page, or <see langword="null"/> when this is the last page.</param>
    public Page(T value, string? nextPageLink)
    {
        Value = value;
        NextPageLink = nextPageLink;
    }

    /// <summary>
    /// Gets the registry URL for the next page, or <see langword="null"/> when this is the last page.
    /// </summary>
    public string? NextPageLink { get; }

    /// <summary>
    /// Gets the value returned for the current page.
    /// </summary>
    public T Value { get; }
}
