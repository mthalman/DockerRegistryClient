using System.Net;
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

namespace Valleysoft.DockerRegistryClient;

internal class ReferrerOperations : IReferrerOperations
{
    private const string FiltersAppliedHeader = "OCI-Filters-Applied";

    public ReferrerOperations(RegistryClient client)
    {
        Client = client;
    }

    public RegistryClient Client { get; }

    public async Task<Page<OciImageIndex>> GetAsync(string repositoryName, string digest, string? artifactType = null, CancellationToken cancellationToken = default)
    {
        string url = $"v2/{repositoryName}/referrers/{digest}";
        if (!string.IsNullOrEmpty(artifactType))
        {
            url = $"{url}?artifactType={Uri.EscapeDataString(artifactType)}";
        }

        try
        {
            return await GetNextCoreAsync(url, artifactType, cancellationToken).ConfigureAwait(false);
        }
        catch (RegistryException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return await GetFallbackAsync(repositoryName, digest, artifactType, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<Page<OciImageIndex>> GetNextAsync(string nextPageLink, CancellationToken cancellationToken = default)
    {
        string? artifactType = GetArtifactType(nextPageLink);
        return await OperationsHelper.HandleNotFoundErrorAsync(
            $"Manifest not found.",
            () => GetNextCoreAsync(nextPageLink, artifactType, cancellationToken)).ConfigureAwait(false);
    }

    private async Task<Page<OciImageIndex>> GetNextCoreAsync(
        string nextPageLink,
        string? artifactType,
        CancellationToken cancellationToken)
    {
        Uri requestUri = ResolveRegistryUri(Client.BaseUri, nextPageLink);
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            requestUri);
        RedirectDelegatingHandler.RequireSameOrigin(request, Client.BaseUri);

        return await this.Client.SendRequestAsync(
            request,
            (response, content) => GetPageResult(response, content, artifactType, requestUri),
            cancellationToken).ConfigureAwait(false);
    }

    private Page<OciImageIndex> GetPageResult(
        HttpResponseMessage response,
        string content,
        string? artifactType,
        Uri requestUri)
    {
        Page<OciImageIndex> page = RegistryClient.GetPageResult<OciImageIndex>(response, content);
        string? nextPageLink = ResolveNextPageLink(
            response.RequestMessage?.RequestUri ?? requestUri,
            page.NextPageLink);
        if (string.IsNullOrEmpty(artifactType))
        {
            return new Page<OciImageIndex>(page.Value, nextPageLink);
        }

        if (!WasArtifactTypeFilterApplied(response))
        {
            page.Value.Manifests = page.Value.Manifests
                .Where(manifest => manifest.ArtifactType == artifactType)
                .ToArray();
        }

        if (nextPageLink is not null && GetArtifactType(nextPageLink) is null)
        {
            nextPageLink = AppendQueryParameter(
                nextPageLink,
                "artifactType",
                Uri.EscapeDataString(artifactType));
        }

        return new Page<OciImageIndex>(page.Value, nextPageLink);
    }

    private static bool WasArtifactTypeFilterApplied(HttpResponseMessage response) =>
        response.Headers.TryGetValues(FiltersAppliedHeader, out IEnumerable<string>? values) &&
        values
            .SelectMany(value => value.Split(','))
            .Any(value => string.Equals(value.Trim(), "artifactType", StringComparison.OrdinalIgnoreCase));

    private string? GetArtifactType(string nextPageLink)
    {
        Uri uri = new(Client.BaseUri, nextPageLink);
        foreach (string parameter in uri.Query.TrimStart('?').Split('&'))
        {
            int separatorIndex = parameter.IndexOf('=');
            if (separatorIndex > 0 &&
                Uri.UnescapeDataString(parameter.Substring(0, separatorIndex)) == "artifactType")
            {
                return Uri.UnescapeDataString(parameter.Substring(separatorIndex + 1));
            }
        }

        return null;
    }

    private string? ResolveNextPageLink(Uri requestUri, string? nextPageLink)
    {
        if (nextPageLink is null)
        {
            return null;
        }

        return ResolveRegistryUri(requestUri, nextPageLink).AbsoluteUri;
    }

    private Uri ResolveRegistryUri(Uri baseUri, string uriReference)
    {
        return UrlHelper.ResolveSameOrigin(Client.BaseUri, baseUri, uriReference);
    }

    private static string AppendQueryParameter(string url, string name, string value)
    {
        int fragmentIndex = url.IndexOf('#');
        string fragment = fragmentIndex >= 0 ? url.Substring(fragmentIndex) : string.Empty;
        string urlWithoutFragment = fragmentIndex >= 0 ? url.Substring(0, fragmentIndex) : url;
        char separator = urlWithoutFragment.Contains('?') ? '&' : '?';
        return $"{urlWithoutFragment}{separator}{name}={value}{fragment}";
    }

    private async Task<Page<OciImageIndex>> GetFallbackAsync(
        string repositoryName,
        string digest,
        string? artifactType,
        CancellationToken cancellationToken)
    {
        string fallbackTag = GetFallbackTag(digest);
        ManifestInfo manifestInfo;
        try
        {
            manifestInfo = await Client.Manifests.GetAsync(
                repositoryName,
                fallbackTag,
                cancellationToken).ConfigureAwait(false);
        }
        catch (RegistryException ex) when (IsNotFound(ex))
        {
            return CreateEmptyPage();
        }
        catch (JsonException)
        {
            return CreateEmptyPage();
        }
        catch (NotSupportedException)
        {
            return CreateEmptyPage();
        }

        if (manifestInfo.Manifest is not OciImageIndex
            {
                SchemaVersion: 2,
                MediaType: ManifestMediaTypes.OciImageIndex1,
                Manifests: not null
            } index)
        {
            return CreateEmptyPage();
        }

        if (!string.IsNullOrEmpty(artifactType))
        {
            index.Manifests = index.Manifests
                .Where(manifest => manifest.ArtifactType == artifactType)
                .ToArray();
        }

        return new Page<OciImageIndex>(index, nextPageLink: null);
    }

    internal static string GetFallbackTag(string digest)
    {
        int separatorIndex = digest.IndexOf(':');
        if (separatorIndex < 1 || separatorIndex == digest.Length - 1)
        {
            throw new ArgumentException("Digest must contain an algorithm and encoded value.", nameof(digest));
        }

        string algorithm = digest.Substring(0, Math.Min(separatorIndex, 32));
        string encoded = digest.Substring(separatorIndex + 1);
        encoded = encoded.Substring(0, Math.Min(encoded.Length, 64));

        return string.Concat($"{algorithm}-{encoded}".Select(character =>
            IsTagCharacter(character) ? character : '-'));
    }

    private static bool IsTagCharacter(char character) =>
        character is >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or
            >= '0' and <= '9' or
            '_' or '.' or '-';

    private static bool IsNotFound(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is RegistryException { StatusCode: HttpStatusCode.NotFound })
            {
                return true;
            }
        }

        return false;
    }

    private static Page<OciImageIndex> CreateEmptyPage() =>
        new(
            new OciImageIndex
            {
                SchemaVersion = 2
            },
            nextPageLink: null);
}
