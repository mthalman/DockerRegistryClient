namespace Valleysoft.DockerRegistryClient;
internal static class UrlHelper
{
    public static Uri ResolveSameOrigin(Uri baseUri, string location)
    {
        Uri resolvedUri = new(baseUri, location);
        if (!string.Equals(baseUri.Scheme, resolvedUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(baseUri.IdnHost, resolvedUri.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            baseUri.Port != resolvedUri.Port)
        {
            throw new InvalidOperationException(
                $"Location '{location}' resolves outside the configured registry origin '{baseUri.GetLeftPart(UriPartial.Authority)}'.");
        }

        return resolvedUri;
    }

    public static Uri ResolveSameOrigin(Uri origin, Uri requestUri, string location) =>
        ResolveSameOrigin(origin, new Uri(requestUri, location).AbsoluteUri);
}
