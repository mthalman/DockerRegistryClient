using System.Globalization;

namespace Valleysoft.DockerRegistryClient;

internal static class RegistryUriBuilder
{
    public static Uri CreateOrigin(string registry)
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        string address = registry.Contains("://") ? registry : $"https://{registry}";
        int authorityStart = address.IndexOf("://", StringComparison.Ordinal) + 3;
        int pathStart = address.IndexOf('/', authorityStart);
        if (string.IsNullOrWhiteSpace(registry) ||
            registry.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            address.IndexOfAny(['?', '#', '\\']) >= 0 ||
            (pathStart >= 0 && pathStart != address.Length - 1) ||
            !Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host) ||
            uri.UserInfo.Length != 0)
        {
            throw new ArgumentException(
                "The registry must be an HTTP or HTTPS origin without user information, a non-root path, query, or fragment.",
                nameof(registry));
        }

        return new Uri(uri.GetLeftPart(UriPartial.Authority));
    }

    public static Uri Catalog(Uri origin, int? count) =>
        ApplyCount(new Uri(origin, "v2/_catalog"), count);

    public static Uri Tags(Uri origin, string repositoryName, int? count) =>
        ApplyCount(RepositoryPath(origin, repositoryName, "tags/list"), count);

    public static Uri Blob(Uri origin, string repositoryName, string digest)
    {
        RegistryReferenceValidator.ValidateDigest(digest, nameof(digest));
        return RepositoryPath(origin, repositoryName, $"blobs/{digest}");
    }

    public static Uri Upload(Uri origin, string repositoryName) =>
        RepositoryPath(origin, repositoryName, "blobs/uploads/");

    public static Uri Mount(Uri origin, string repositoryName, string digest, string sourceRepositoryName)
    {
        RegistryReferenceValidator.ValidateDigest(digest, nameof(digest));
        RegistryReferenceValidator.ValidateRepository(sourceRepositoryName, nameof(sourceRepositoryName));
        return AddQueryParameter(
            AddQueryParameter(Upload(origin, repositoryName), "mount", digest),
            "from", sourceRepositoryName);
    }

    public static Uri Manifest(Uri origin, string repositoryName, string tagOrDigest)
    {
        RegistryReferenceValidator.ValidateReference(tagOrDigest, nameof(tagOrDigest));
        return RepositoryPath(origin, repositoryName, $"manifests/{tagOrDigest}");
    }

    public static Uri Referrers(Uri origin, string repositoryName, string digest, string? artifactType = null)
    {
        RegistryReferenceValidator.ValidateDigest(digest, nameof(digest));
        Uri uri = RepositoryPath(origin, repositoryName, $"referrers/{digest}");
        return artifactType is null || artifactType.Length == 0
            ? uri
            : AddQueryParameter(uri, "artifactType", artifactType);
    }

    public static Uri AddQueryParameter(Uri uri, string name, string value)
    {
        // UriBuilder.Query handles a leading '?' differently across supported runtimes.
        string query = uri.Query.Length == 0 ? string.Empty : uri.Query.Substring(1);
        string parameter = $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
        string combinedQuery = query.Length == 0 ? parameter : $"{query}&{parameter}";
        return new Uri($"{uri.GetLeftPart(UriPartial.Path)}?{combinedQuery}{uri.Fragment}");
    }

    private static Uri ApplyCount(Uri uri, int? count) =>
        count is null ? uri : AddQueryParameter(uri, "n", count.Value.ToString(CultureInfo.InvariantCulture));

    private static Uri RepositoryPath(Uri origin, string repositoryName, string suffix)
    {
        RegistryReferenceValidator.ValidateRepository(repositoryName, nameof(repositoryName));
        return new Uri(origin, $"v2/{repositoryName}/{suffix}");
    }
}
