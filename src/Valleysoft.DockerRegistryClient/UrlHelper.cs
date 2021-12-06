namespace Valleysoft.DockerRegistryClient;
internal static class UrlHelper
{
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
