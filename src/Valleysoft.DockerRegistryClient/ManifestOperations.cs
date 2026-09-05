using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Docker;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

namespace Valleysoft.DockerRegistryClient;

internal class ManifestOperations : IManifestWriteOperations
{
    private const string NotFoundMessage = "Manifest not found.";
    private const string DockerContentDigestHeader = "Docker-Content-Digest";
    private const string OciSubjectHeader = "OCI-Subject";
    private static readonly Regex DigestRegex = new(
        @"\A[a-z0-9]+(?:[+._-][a-z0-9]+)*:[A-Za-z0-9=_-]+\z",
        RegexOptions.CultureInvariant);
    private readonly SemaphoreSlim[] referrersFallbackLocks = Enumerable.Range(0, 32)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public RegistryClient Client { get; }

    public ManifestOperations(RegistryClient client)
    {
        this.Client = client;
    }

    public async Task<ManifestInfo> GetAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Get);

        return await OperationsHelper.HandleNotFoundErrorAsync(
            NotFoundMessage,
            async () =>
            {
                using HttpResponseMessage response = await this.Client.SendRequestCoreAsync(
                    request,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

#if NET5_0_OR_GREATER
                byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#else
                byte[] content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif

                return GetResult(response, content);
            }).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Head);
        return await this.Client.SendExistsRequestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetDigestAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Head);
        return await OperationsHelper.HandleNotFoundErrorAsync(
            NotFoundMessage,
            () => this.Client.SendRequestAsync(
                request,
                (response, content) => GetDigest(response),
                cancellationToken)).ConfigureAwait(false);
    }

    public async Task<string> GetDigestWithHttpMessagesAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(GetManifestUri(repositoryName, tagOrDigest), HttpMethod.Head);
        return await OperationsHelper.HandleNotFoundErrorAsync(
            NotFoundMessage,
            () => this.Client.SendRequestAsync(
                request,
                (response, content) => GetDigest(response),
                cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ManifestPublishResult> PublishAsync(
        string repositoryName,
        string tagOrDigest,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException("The manifest media type must be set.", nameof(mediaType));
        }

        byte[] contentBytes = content.ToArray();
        return await PublishCoreAsync(
            repositoryName,
            tagOrDigest,
            contentBytes,
            mediaType,
            maintainReferrersFallback: true,
            expectedEntityTag: null,
            requireMissing: false,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ManifestPublishResult> PublishCoreAsync(
        string repositoryName,
        string tagOrDigest,
        byte[] content,
        string mediaType,
        bool maintainReferrersFallback,
        EntityTagHeaderValue? expectedEntityTag,
        bool requireMissing,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, GetManifestUri(repositoryName, tagOrDigest))
        {
            Content = new ByteArrayContent(content)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        if (expectedEntityTag is not null)
        {
            request.Headers.IfMatch.Add(expectedEntityTag);
        }
        else if (requireMissing)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
        }

        using HttpResponseMessage response = await this.Client.SendRequestCoreAsync(
            request,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        string? responseDigest = GetOptionalDigest(response);
        if (responseDigest is not null)
        {
            VerifyDigestIfSupported(responseDigest, content);
        }

        var result = new ManifestPublishResult(GetLocation(response), responseDigest);

        SubjectManifestMetadata? subjectMetadata = maintainReferrersFallback
            ? GetSubjectMetadata(mediaType, content)
            : null;
        if (subjectMetadata is not null && !response.Headers.Contains(OciSubjectHeader))
        {
            string manifestDigest = GetPublishedDigest(tagOrDigest, responseDigest, content);
            await UpdateReferrersFallbackAsync(
                repositoryName,
                manifestDigest,
                mediaType,
                content.LongLength,
                subjectMetadata,
                addReference: true,
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task DeleteAsync(
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default)
    {
        if (digest is null)
        {
            throw new ArgumentNullException(nameof(digest));
        }

        if (!IsValidDigest(digest))
        {
            throw new ArgumentException("A valid manifest digest is required. Tags cannot be deleted.", nameof(digest));
        }

        StoredManifest manifest = await GetStoredManifestAsync(
            repositoryName,
            digest,
            cancellationToken).ConfigureAwait(false);
        SubjectManifestMetadata? subjectMetadata = GetSubjectMetadata(manifest.MediaType, manifest.Content);
        bool updateFallback = subjectMetadata is not null &&
            !await SupportsNativeReferrersAsync(
                repositoryName,
                subjectMetadata.SubjectDigest,
                cancellationToken).ConfigureAwait(false);

        using HttpRequestMessage request = new(HttpMethod.Delete, GetManifestUri(repositoryName, digest));
        await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (updateFallback && subjectMetadata is not null)
        {
            await UpdateReferrersFallbackAsync(
                repositoryName,
                digest,
                manifest.MediaType,
                manifest.Content.LongLength,
                subjectMetadata,
                addReference: false,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private Uri GetManifestUri(string repositoryName, string tagOrDigest) =>
        new(this.Client.BaseUri.AbsoluteUri + $"v2/{repositoryName}/manifests/{tagOrDigest}");

    private static HttpRequestMessage CreateGetRequestMessage(Uri requestUri, HttpMethod method)
    {
        HttpRequestMessage request = new(method, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.DockerManifestSchema2));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.DockerManifestList));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.OciManifestSchema1));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaTypes.OciImageIndex1));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", quality: 0.1));
        return request;
    }

    private static string GetDigest(HttpResponseMessage response) =>
        response.Headers.GetValues(DockerContentDigestHeader).First();

    private static string? GetOptionalDigest(HttpResponseMessage response) =>
        response.Headers.TryGetValues(DockerContentDigestHeader, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    private static string GetLocation(HttpResponseMessage response) =>
        response.Headers.Location?.ToString() ??
            throw new InvalidOperationException("Location header not set.");

    private async Task UpdateReferrersFallbackAsync(
        string repositoryName,
        string manifestDigest,
        string mediaType,
        long manifestSize,
        SubjectManifestMetadata metadata,
        bool addReference,
        CancellationToken cancellationToken)
    {
        string fallbackTag = ReferrerOperations.GetFallbackTag(metadata.SubjectDigest);
        string lockKey = $"{repositoryName}\n{fallbackTag}";
        SemaphoreSlim fallbackLock = referrersFallbackLocks[
            (lockKey.GetHashCode() & int.MaxValue) % referrersFallbackLocks.Length];
        await fallbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                StoredManifest? storedIndex = await TryGetStoredManifestAsync(
                    repositoryName,
                    fallbackTag,
                    cancellationToken).ConfigureAwait(false);
                OciImageIndex index = storedIndex is null
                    ? new OciImageIndex()
                    : DeserializeFallbackIndex(storedIndex, fallbackTag);

                bool changed = addReference
                    ? AddReferrer(index, manifestDigest, mediaType, manifestSize, metadata)
                    : RemoveReferrer(index, manifestDigest);
                if (!changed)
                {
                    return;
                }

                byte[] indexContent = JsonSerializer.SerializeToUtf8Bytes(index);
                try
                {
                    await PublishCoreAsync(
                        repositoryName,
                        fallbackTag,
                        indexContent,
                        ManifestMediaTypes.OciImageIndex1,
                        maintainReferrersFallback: false,
                        storedIndex?.EntityTag,
                        requireMissing: storedIndex is null,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (RegistryException ex)
                    when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed && attempt < 2)
                {
                }
            }
        }
        finally
        {
            fallbackLock.Release();
        }
    }

    private static bool AddReferrer(
        OciImageIndex index,
        string manifestDigest,
        string mediaType,
        long manifestSize,
        SubjectManifestMetadata metadata)
    {
        if (index.Manifests.Any(reference => reference.Digest == manifestDigest))
        {
            return false;
        }

        index.Manifests =
        [
            .. index.Manifests,
            new Models.Manifests.Oci.ManifestReference
            {
                MediaType = mediaType,
                Digest = manifestDigest,
                Size = manifestSize,
                ArtifactType = metadata.ArtifactType,
                Annotations = new Dictionary<string, string>(metadata.Annotations)
            }
        ];
        return true;
    }

    private static bool RemoveReferrer(OciImageIndex index, string manifestDigest)
    {
        Models.Manifests.Oci.ManifestReference[] remaining = index.Manifests
            .Where(reference => reference.Digest != manifestDigest)
            .ToArray();
        if (remaining.Length == index.Manifests.Length)
        {
            return false;
        }

        index.Manifests = remaining;
        return true;
    }

    private async Task<StoredManifest> GetStoredManifestAsync(
        string repositoryName,
        string tagOrDigest,
        CancellationToken cancellationToken) =>
        await TryGetStoredManifestAsync(repositoryName, tagOrDigest, cancellationToken).ConfigureAwait(false) ??
            throw new RegistryException(NotFoundMessage)
            {
                StatusCode = System.Net.HttpStatusCode.NotFound
            };

    private async Task<StoredManifest?> TryGetStoredManifestAsync(
        string repositoryName,
        string tagOrDigest,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateGetRequestMessage(
            GetManifestUri(repositoryName, tagOrDigest),
            HttpMethod.Get);
        try
        {
            using HttpResponseMessage response = await this.Client.SendRequestCoreAsync(
                request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
            byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#else
            byte[] content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif
            string mediaType = response.Content.Headers.ContentType?.MediaType ??
                throw new InvalidOperationException("Response content type is not set.");
            return new StoredManifest(content, mediaType, response.Headers.ETag);
        }
        catch (RegistryException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<bool> SupportsNativeReferrersAsync(
        string repositoryName,
        string subjectDigest,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"{Client.BaseUri.AbsoluteUri}v2/{repositoryName}/referrers/{subjectDigest}");
        try
        {
            using HttpResponseMessage response = await Client.SendRequestCoreAsync(
                request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RegistryException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static OciImageIndex DeserializeFallbackIndex(
        StoredManifest storedIndex,
        string fallbackTag)
    {
        using JsonDocument document = JsonDocument.Parse(storedIndex.Content);
        JsonElement root = document.RootElement;
        bool isValid = storedIndex.MediaType.Equals(
                ManifestMediaTypes.OciImageIndex1,
                StringComparison.OrdinalIgnoreCase) &&
            root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("schemaVersion", out JsonElement schemaVersion) &&
            schemaVersion.ValueKind == JsonValueKind.Number &&
            schemaVersion.TryGetInt32(out int schemaVersionValue) &&
            schemaVersionValue == 2 &&
            root.TryGetProperty("mediaType", out JsonElement mediaType) &&
            mediaType.ValueKind == JsonValueKind.String &&
            mediaType.GetString() == ManifestMediaTypes.OciImageIndex1 &&
            root.TryGetProperty("manifests", out JsonElement manifests) &&
            manifests.ValueKind == JsonValueKind.Array &&
            manifests.EnumerateArray().All(IsValidDescriptor);
        if (!isValid)
        {
            throw new InvalidOperationException(
                $"The referrers fallback tag '{fallbackTag}' does not contain a valid OCI image index.");
        }

        return Deserialize<OciImageIndex>(storedIndex.Content);
    }

    private static bool IsValidDescriptor(JsonElement descriptor) =>
        descriptor.ValueKind == JsonValueKind.Object &&
        descriptor.TryGetProperty("mediaType", out JsonElement mediaType) &&
        mediaType.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(mediaType.GetString()) &&
        descriptor.TryGetProperty("digest", out JsonElement digest) &&
        digest.ValueKind == JsonValueKind.String &&
        digest.GetString() is string digestValue &&
        IsValidDigest(digestValue) &&
        descriptor.TryGetProperty("size", out JsonElement size) &&
        size.ValueKind == JsonValueKind.Number &&
        size.TryGetInt64(out long sizeValue) &&
        sizeValue >= 0;

    private static SubjectManifestMetadata? GetSubjectMetadata(string mediaType, byte[] content)
    {
        if (mediaType.Equals(ManifestMediaTypes.OciManifestSchema1, StringComparison.OrdinalIgnoreCase))
        {
            OciImageManifest manifest = Deserialize<OciImageManifest>(content);
            if (manifest.Subject is null)
            {
                return null;
            }

            string? artifactType = string.IsNullOrWhiteSpace(manifest.ArtifactType)
                ? manifest.Config.MediaType
                : manifest.ArtifactType;
            return new SubjectManifestMetadata(
                manifest.Subject.Digest,
                artifactType,
                manifest.Annotations ?? new Dictionary<string, string>());
        }

        if (mediaType.Equals(ManifestMediaTypes.OciImageIndex1, StringComparison.OrdinalIgnoreCase))
        {
            OciImageIndex index = Deserialize<OciImageIndex>(content);
            return index.Subject is null
                ? null
                : new SubjectManifestMetadata(
                    index.Subject.Digest,
                    index.ArtifactType,
                    index.Annotations);
        }

        return null;
    }

    private static string GetPublishedDigest(
        string tagOrDigest,
        string? responseDigest,
        byte[] content)
    {
        if (responseDigest is not null && responseDigest.Length > 0)
        {
            return responseDigest;
        }

        if (IsValidDigest(tagOrDigest))
        {
            VerifyDigestIfSupported(tagOrDigest, content);
            return tagOrDigest;
        }

        using SHA256 sha256 = SHA256.Create();
        string encoded = BitConverter.ToString(sha256.ComputeHash(content))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
        return $"sha256:{encoded}";
    }

    private static void VerifyDigestIfSupported(string digest, byte[] content)
    {
        if (!IsValidDigest(digest))
        {
            throw new InvalidOperationException($"Registry returned an invalid manifest digest '{digest}'.");
        }

        int separatorIndex = digest.IndexOf(':');
        string algorithm = digest.Substring(0, separatorIndex);
        using HashAlgorithm? hashAlgorithm = algorithm switch
        {
            "sha256" => SHA256.Create(),
            "sha384" => SHA384.Create(),
            "sha512" => SHA512.Create(),
            _ => null
        };
        if (hashAlgorithm is null)
        {
            return;
        }

        string actual = BitConverter.ToString(hashAlgorithm.ComputeHash(content))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
        string expected = digest.Substring(separatorIndex + 1);
        if (!actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Registry returned manifest digest '{digest}', but the published content has digest '{algorithm}:{actual}'.");
        }
    }

    private static bool IsValidDigest(string digest)
    {
        if (!DigestRegex.IsMatch(digest))
        {
            return false;
        }

        int separatorIndex = digest.IndexOf(':');
        string algorithm = digest.Substring(0, separatorIndex);
        string encoded = digest.Substring(separatorIndex + 1);
        return algorithm switch
        {
            "sha256" => IsLowerHex(encoded, 64),
            "sha384" => IsLowerHex(encoded, 96),
            "sha512" => IsLowerHex(encoded, 128),
            "blake3" => IsLowerHex(encoded, 64),
            _ => true
        };
    }

    private static bool IsLowerHex(string value, int expectedLength) =>
        value.Length == expectedLength &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static ManifestInfo GetResult(HttpResponseMessage response, byte[] content)
    {
        if (response.Content is null)
        {
            throw new InvalidOperationException($"Response content is null.");
        }

        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        string dockerContentDigest = GetDigest(response);

        if (mediaType is null)
        {
            throw new InvalidOperationException("Response content type is not set.");
        }

        IManifest manifest;
        if (mediaType.Equals(ManifestMediaTypes.DockerManifestSchema2, StringComparison.OrdinalIgnoreCase))
        {
            manifest = Deserialize<DockerManifest>(content);
        }
        else if (mediaType.Equals(ManifestMediaTypes.DockerManifestList, StringComparison.OrdinalIgnoreCase))
        {
            manifest = Deserialize<ManifestList>(content);
        }
        else if (mediaType.Equals(ManifestMediaTypes.OciManifestSchema1, StringComparison.OrdinalIgnoreCase))
        {
            manifest = Deserialize<OciImageManifest>(content);
        }
        else if (mediaType.Equals(ManifestMediaTypes.OciImageIndex1, StringComparison.OrdinalIgnoreCase))
        {
            manifest = Deserialize<OciImageIndex>(content);
        }
        else
        {
            manifest = new RawManifest(mediaType, content);
        }

        return new ManifestInfo(mediaType, dockerContentDigest, manifest, content);
    }

    private static T Deserialize<T>(byte[] content)
        where T : IManifest
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content) ??
                throw new JsonException($"Unable to deserialize content:{Environment.NewLine}{Encoding.UTF8.GetString(content)}");
        }
        catch (JsonException exception)
        {
            throw new JsonException(
                $"Unable to deserialize the response:{Environment.NewLine}{Encoding.UTF8.GetString(content)}",
                exception);
        }
    }

    private sealed class SubjectManifestMetadata
    {
        public SubjectManifestMetadata(
            string subjectDigest,
            string? artifactType,
            IDictionary<string, string> annotations)
        {
            SubjectDigest = subjectDigest;
            ArtifactType = artifactType;
            Annotations = annotations;
        }

        public string SubjectDigest { get; }
        public string? ArtifactType { get; }
        public IDictionary<string, string> Annotations { get; }
    }

    private sealed class StoredManifest
    {
        public StoredManifest(
            byte[] content,
            string mediaType,
            EntityTagHeaderValue? entityTag)
        {
            Content = content;
            MediaType = mediaType;
            EntityTag = entityTag;
        }

        public byte[] Content { get; }
        public string MediaType { get; }
        public EntityTagHeaderValue? EntityTag { get; }
    }
}
