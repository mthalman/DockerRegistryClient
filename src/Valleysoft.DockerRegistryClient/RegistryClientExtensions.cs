using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Provides high-level operations that coordinate multiple registry API requests.
/// </summary>
public static class RegistryClientExtensions
{
    private static readonly HashSet<string> NonDistributableLayerMediaTypes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip",
        "application/vnd.oci.image.layer.nondistributable.v1.tar",
        "application/vnd.oci.image.layer.nondistributable.v1.tar+gzip",
        "application/vnd.oci.image.layer.nondistributable.v1.tar+zstd"
    };

    /// <summary>
    /// Copies an image or OCI artifact and its dependencies to a destination repository and reference.
    /// </summary>
    /// <param name="sourceClient">Client used to pull the source content.</param>
    /// <param name="sourceRepositoryName">Name of the source repository.</param>
    /// <param name="sourceReference">Tag or digest identifying the source manifest.</param>
    /// <param name="destinationClient">Client used to push the destination content.</param>
    /// <param name="destinationRepositoryName">Name of the destination repository.</param>
    /// <param name="destinationReference">Tag or digest to assign to the copied root manifest.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The result of publishing the root manifest under <paramref name="destinationReference"/>.</returns>
    /// <remarks>
    /// Referenced manifests, OCI subjects, configs, and distributable layers are copied recursively.
    /// Existing destination dependencies are skipped. Docker foreign layers and OCI
    /// non-distributable layers remain referenced by the manifest but are not uploaded.
    /// </remarks>
    public static Task<ManifestPublishResult> CopyAsync(
        this RegistryClient sourceClient,
        string sourceRepositoryName,
        string sourceReference,
        RegistryClient destinationClient,
        string destinationRepositoryName,
        string destinationReference,
        CancellationToken cancellationToken = default)
    {
        if (sourceClient is null)
        {
            throw new ArgumentNullException(nameof(sourceClient));
        }

        if (destinationClient is null)
        {
            throw new ArgumentNullException(nameof(destinationClient));
        }
        ValidateRequired(sourceRepositoryName, nameof(sourceRepositoryName));
        ValidateRequired(sourceReference, nameof(sourceReference));
        ValidateRequired(destinationRepositoryName, nameof(destinationRepositoryName));
        ValidateRequired(destinationReference, nameof(destinationReference));
        ValidateReference(sourceReference, nameof(sourceReference));
        ValidateReference(destinationReference, nameof(destinationReference));

        return new CopyContext(
            sourceClient,
            sourceRepositoryName,
            destinationClient,
            destinationRepositoryName).CopyAsync(
                sourceReference,
                destinationReference,
                cancellationToken);
    }

    private static void ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName);
        }
    }

    private static void ValidateReference(string value, string parameterName)
    {
        if (!ManifestOperations.IsValidReference(value))
        {
            throw new ArgumentException(
                "The value must be a valid manifest tag or digest.",
                parameterName);
        }
    }

    private sealed class CopyContext
    {
        private readonly RegistryClient sourceClient;
        private readonly string sourceRepositoryName;
        private readonly RegistryClient destinationClient;
        private readonly string destinationRepositoryName;
        private readonly HashSet<string> visitedManifestDigests = new(StringComparer.Ordinal);
        private readonly HashSet<string> visitedBlobDigests = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> blobDescriptorSizes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ManifestDescriptorMetadata> manifestDescriptors =
            new(StringComparer.Ordinal);

        public CopyContext(
            RegistryClient sourceClient,
            string sourceRepositoryName,
            RegistryClient destinationClient,
            string destinationRepositoryName)
        {
            this.sourceClient = sourceClient;
            this.sourceRepositoryName = sourceRepositoryName;
            this.destinationClient = destinationClient;
            this.destinationRepositoryName = destinationRepositoryName;
        }

        public async Task<ManifestPublishResult> CopyAsync(
            string sourceReference,
            string destinationReference,
            CancellationToken cancellationToken)
        {
            ManifestInfo root = await sourceClient.Manifests.GetAsync(
                sourceRepositoryName,
                sourceReference,
                cancellationToken).ConfigureAwait(false);

            AddVerifiedManifestIdentity(root.DockerContentDigest, root.Content);
            if (ManifestOperations.IsValidDigest(sourceReference))
            {
                AddVerifiedManifestIdentity(sourceReference, root.Content);
            }
            await CopyDependenciesAsync(root.Manifest, cancellationToken).ConfigureAwait(false);

            return await destinationClient.Manifests.PublishAsync(
                destinationRepositoryName,
                destinationReference,
                root.Content,
                root.MediaType,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task CopyManifestAsync(
            IDescriptor? descriptor,
            CancellationToken cancellationToken)
        {
            if (descriptor is null)
            {
                throw new InvalidOperationException(
                    "Source manifest contains a null manifest descriptor.");
            }

            ValidateManifestDescriptor(descriptor);
            if (!visitedManifestDigests.Add(descriptor.Digest))
            {
                return;
            }

            bool exists = await destinationClient.Manifests.ExistsAsync(
                destinationRepositoryName,
                descriptor.Digest,
                cancellationToken).ConfigureAwait(false);
            ManifestInfo manifest = await sourceClient.Manifests.GetAsync(
                sourceRepositoryName,
                descriptor.Digest,
                cancellationToken).ConfigureAwait(false);
            if (manifest.Content.Length != descriptor.Size)
            {
                throw new InvalidOperationException(
                    $"Manifest '{descriptor.Digest}' has size {manifest.Content.Length}, but its descriptor declares {descriptor.Size}.");
            }
            if (!manifest.MediaType.Equals(
                descriptor.MediaType,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Manifest '{descriptor.Digest}' has media type '{manifest.MediaType}', but its descriptor declares '{descriptor.MediaType}'.");
            }

            ManifestOperations.VerifyDigestIfSupported(descriptor.Digest, manifest.Content);
            ManifestOperations.VerifyDigestIfSupported(
                manifest.DockerContentDigest,
                manifest.Content);
            await CopyDependenciesAsync(manifest.Manifest, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                await destinationClient.Manifests.PublishAsync(
                    destinationRepositoryName,
                    descriptor.Digest,
                    manifest.Content,
                    manifest.MediaType,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task CopyDependenciesAsync(IManifest manifest, CancellationToken cancellationToken)
        {
            if (manifest is OciImageManifest ociManifest && ociManifest.Subject is not null)
            {
                await CopyManifestAsync(ociManifest.Subject, cancellationToken).ConfigureAwait(false);
            }
            else if (manifest is OciImageIndex ociIndex && ociIndex.Subject is not null)
            {
                await CopyManifestAsync(ociIndex.Subject, cancellationToken).ConfigureAwait(false);
            }

            if (manifest is IManifestList manifestList)
            {
                foreach (IManifestReference? reference in manifestList.Manifests ?? [])
                {
                    await CopyManifestAsync(reference, cancellationToken).ConfigureAwait(false);
                }
            }

            if (manifest is IImageManifest imageManifest)
            {
                if (imageManifest.Config is not null)
                {
                    await CopyBlobAsync(
                        imageManifest.Config.Digest,
                        imageManifest.Config.Size,
                        cancellationToken).ConfigureAwait(false);
                }

                foreach (IDescriptor? layer in imageManifest.Layers ?? [])
                {
                    if (layer is null)
                    {
                        throw new InvalidOperationException(
                            "Source manifest contains a null layer descriptor.");
                    }

                    if (!NonDistributableLayerMediaTypes.Contains(layer.MediaType))
                    {
                        await CopyBlobAsync(
                            layer.Digest,
                            layer.Size,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task CopyBlobAsync(
            string digest,
            long size,
            CancellationToken cancellationToken)
        {
            ValidateDescriptorDigest(digest);
            if (size < 0)
            {
                throw new InvalidOperationException(
                    $"Source manifest contains a negative size for blob '{digest}'.");
            }
            if (blobDescriptorSizes.TryGetValue(digest, out long existingSize) &&
                existingSize != size)
            {
                throw new InvalidOperationException(
                    $"Source manifest contains conflicting sizes for blob '{digest}'.");
            }

            blobDescriptorSizes[digest] = size;
            if (!visitedBlobDigests.Add(digest))
            {
                return;
            }

            (bool exists, long? destinationSize) =
                await ((BlobOperations)destinationClient.Blobs).GetExistenceForCopyAsync(
                    destinationRepositoryName,
                    digest,
                    cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                await ValidateAvailableBlobSizeAsync(
                    digest,
                    size,
                    destinationSize,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            bool isSameRegistry = IsSameRegistry(sourceClient.BaseUri, destinationClient.BaseUri);
            BlobUploadSession? upload = null;
            if (isSameRegistry)
            {
                BlobMountResult mount = await ((BlobOperations)destinationClient.Blobs).MountAsync(
                    destinationRepositoryName,
                    digest,
                    sourceRepositoryName,
                    cancellationToken).ConfigureAwait(false);
                if (mount.IsMounted)
                {
                    (bool mountedExists, long? mountedSize) =
                        await ((BlobOperations)destinationClient.Blobs).GetExistenceForCopyAsync(
                            destinationRepositoryName,
                            digest,
                            cancellationToken).ConfigureAwait(false);
                    if (!mountedExists)
                    {
                        throw new InvalidOperationException(
                            $"Registry reported that blob '{digest}' was mounted, but it does not exist in the destination repository.");
                    }

                    await ValidateAvailableBlobSizeAsync(
                        digest,
                        size,
                        mountedSize,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                upload = mount.Upload;
            }

            upload ??= await ((BlobOperations)destinationClient.Blobs).BeginUploadForCopyAsync(
                destinationRepositoryName,
                cancellationToken).ConfigureAwait(false);
            try
            {
                if (isSameRegistry)
                {
                    using Stream content = await DownloadToTemporaryFileAsync(
                        digest,
                        size,
                        cancellationToken).ConfigureAwait(false);
                    await destinationClient.Blobs.EndUploadAsync(
                        upload.Location,
                        digest,
                        upload.UploadContext,
                        content,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    using var content = new ReplayableBlobContent(
                        (BlobOperations)sourceClient.Blobs,
                        sourceRepositoryName,
                        digest,
                        size,
                        cancellationToken);
                    await ((BlobOperations)destinationClient.Blobs).EndUploadForCopyAsync(
                        upload.Location,
                        digest,
                        upload.UploadContext,
                        content,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                await CancelUploadAsync(upload.Location, exception).ConfigureAwait(false);
                throw;
            }
        }

        private async Task CancelUploadAsync(string uploadLocation, Exception originalException)
        {
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await destinationClient.Blobs.DeleteUploadAsync(
                    uploadLocation,
                    cancellationSource.Token).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                originalException.Data["UploadCleanupException"] = cleanupException;
            }
        }

        private async Task<Stream> DownloadToTemporaryFileAsync(
            string digest,
            long expectedSize,
            CancellationToken cancellationToken)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"docker-registry-copy-{Guid.NewGuid():N}.tmp");
            FileStream temporaryFile = TemporaryFile.Create(path);
            try
            {
                using Stream source = await ((BlobOperations)sourceClient.Blobs).GetForCopyAsync(
                    sourceRepositoryName,
                    digest,
                    cancellationToken).ConfigureAwait(false);
                await CopyAndValidateBlobSizeAsync(
                    source,
                    temporaryFile,
                    digest,
                    expectedSize,
                    cancellationToken).ConfigureAwait(false);

                temporaryFile.Position = 0;
                return temporaryFile;
            }
            catch
            {
                temporaryFile.Dispose();
                throw;
            }
        }

        private async Task ValidateAvailableBlobSizeAsync(
            string digest,
            long expectedSize,
            long? availableSize,
            CancellationToken cancellationToken)
        {
            if (availableSize is long size)
            {
                if (size != expectedSize)
                {
                    throw new InvalidOperationException(
                        $"Blob '{digest}' has size {size}, but its descriptor declares {expectedSize}.");
                }

                return;
            }

            using Stream source = await ((BlobOperations)sourceClient.Blobs).GetForCopyAsync(
                sourceRepositoryName,
                digest,
                cancellationToken).ConfigureAwait(false);
            await CopyAndValidateBlobSizeAsync(
                source,
                destination: null,
                digest,
                expectedSize,
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task CopyAndValidateBlobSizeAsync(
            Stream source,
            Stream? destination,
            string digest,
            long expectedSize,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[81920];
            long remaining = expectedSize;
            while (remaining > 0)
            {
                int bytesRead = await source.ReadAsync(
                    buffer,
                    0,
                    (int)Math.Min(buffer.Length, remaining),
                    cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException(
                        $"Blob '{digest}' has size {expectedSize - remaining}, but its descriptor declares {expectedSize}.");
                }

                if (destination is not null)
                {
                    await destination.WriteAsync(
                        buffer,
                        0,
                        bytesRead,
                        cancellationToken).ConfigureAwait(false);
                }
                remaining -= bytesRead;
            }

            if (await source.ReadAsync(
                buffer,
                0,
                1,
                cancellationToken).ConfigureAwait(false) != 0)
            {
                throw new InvalidOperationException(
                    $"Blob '{digest}' contains more data than its declared size of {expectedSize}.");
            }
        }

        private static bool IsSameRegistry(Uri source, Uri destination) =>
            source.Scheme.Equals(destination.Scheme, StringComparison.OrdinalIgnoreCase) &&
            source.IdnHost.Equals(destination.IdnHost, StringComparison.OrdinalIgnoreCase) &&
            source.Port == destination.Port;

        private static void ValidateDescriptorDigest(string digest)
        {
            if (!ManifestOperations.IsValidDigest(digest))
            {
                throw new InvalidOperationException(
                    $"Source manifest contains invalid descriptor digest '{digest}'.");
            }
        }

        private void ValidateManifestDescriptor(IDescriptor descriptor)
        {
            ValidateDescriptorDigest(descriptor.Digest);
            if (descriptor.Size < 0)
            {
                throw new InvalidOperationException(
                    $"Source manifest contains a negative size for manifest '{descriptor.Digest}'.");
            }
            if (string.IsNullOrWhiteSpace(descriptor.MediaType))
            {
                throw new InvalidOperationException(
                    $"Source manifest contains an empty media type for manifest '{descriptor.Digest}'.");
            }

            var metadata = new ManifestDescriptorMetadata(
                descriptor.MediaType,
                descriptor.Size);
            if (manifestDescriptors.TryGetValue(
                descriptor.Digest,
                out ManifestDescriptorMetadata? existing) &&
                (existing.Size != metadata.Size ||
                    !existing.MediaType.Equals(
                        metadata.MediaType,
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Source manifest contains conflicting descriptors for manifest '{descriptor.Digest}'.");
            }

            manifestDescriptors[descriptor.Digest] = metadata;
        }

        private void AddVerifiedManifestIdentity(
            string digest,
            ReadOnlyMemory<byte> content)
        {
            if (ManifestOperations.VerifyDigestIfSupported(digest, content))
            {
                visitedManifestDigests.Add(digest);
            }
        }

        private sealed record ManifestDescriptorMetadata(string MediaType, long Size);
    }
}
