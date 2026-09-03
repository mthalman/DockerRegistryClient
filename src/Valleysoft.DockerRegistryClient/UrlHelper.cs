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

    public static string ApplyCount(string url, int? count)
    {
        if (count is not null)
        {
            return url + $"?n={count}";
        }

        return url;
    }

    public static string Concat(string url1, string url2)
    {
        if (url1.Last() == '/' && url2.First() == '/')
        {
#if NET5_0_OR_GREATER
            return url1 + url2[1..];
#else
            return url1 + url2.Substring(1);
#endif
        }

        return url1 + url2;
    }
}
