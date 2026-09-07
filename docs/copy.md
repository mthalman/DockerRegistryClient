# Copy Images and Artifacts

Use `CopyAsync` on a source `RegistryClient` to copy an image or OCI artifact
and its dependencies to a destination repository and reference. The source
client must be able to pull the content, and the destination client must be able
to push it.

## Copy within a registry

```csharp
using RegistryClient client = new("registry.example.com", credentials);

ManifestPublishResult result = await client.CopyAsync(
    "development/service",
    "candidate",
    client,
    "production/service",
    "latest");

Console.WriteLine($"Published digest: {result.Digest}");
```

When both clients address the same registry origin (scheme, host, and port),
`CopyAsync` first attempts to mount each missing blob from the source repository
into the destination repository. If the registry does not support mounting, the
operation stages the blob in a temporary file before uploading it. Staging
prevents shared HTTP connection limits from blocking the transfer, but requires
enough temporary disk space for the largest blob. Temporary files are created
with owner-only access on Unix. Downloads are limited to the size declared by
the manifest descriptor and reject truncated or oversized content. Blobs that
already exist in the destination repository are skipped.

## Copy between registries

```csharp
using RegistryClient source = new("source.example.com", sourceCredentials);
using RegistryClient destination = new(
    "destination.example.com",
    destinationCredentials);

ManifestPublishResult result = await source.CopyAsync(
    "team/service",
    "candidate",
    destination,
    "archive/service",
    "release-2026",
    cancellationToken);

Console.WriteLine($"Published digest: {result.Digest}");
```

The source and destination references can differ. The source reference may be a
tag or digest. The root manifest is always published under the requested
destination tag or digest, even if its content already exists there.

Between different registry origins, blobs are streamed directly from the source
response to the destination upload without temporary-file staging. Uploads use
the descriptor size as their HTTP content length and reject source content whose
length does not match. If an upload is redirected while preserving its request
body, the source blob is downloaded again so the redirected request can be
replayed safely.

## Recursive behavior

The copy operation:

- Copies the config and distributable layers of Docker V2 and OCI image
  manifests.
- Recursively copies every manifest referenced by Docker manifest lists and OCI
  image indexes.
- Recursively copies the target of an OCI `subject` descriptor.
- Validates descriptor digests before using them in source or destination
  requests.
- Verifies supported manifest digests against the downloaded manifest bytes
  before traversing the manifest.
- Verifies each referenced manifest's size and media type against its
  descriptor and rejects conflicting descriptors for the same digest.
- Deduplicates repeated manifest and blob digests.
- Rejects conflicting sizes for descriptors that reference the same blob.
- Skips child manifests and blobs that already exist at the destination.
- Preserves Docker foreign-layer and OCI non-distributable-layer descriptors
  without uploading the referenced blobs.
- Publishes dependencies before the manifests that reference them.
- Preserves the original bytes and response media type of every published
  manifest.

The returned `ManifestPublishResult` describes publication of the root manifest
at the destination reference.

## Limitations

Unknown manifest media types are copied as opaque `RawManifest` leaves. Their
bytes and media type are preserved, but references in an unknown format cannot
be discovered or copied.

The operation copies only the graph reachable from the selected manifest.
Referrers are not enumerated or copied, and the operation does not continuously
synchronize later source changes. Publishing subject-bearing OCI content may
still maintain the registry's referrers tag-schema fallback as described in
[Manifest Operations](manifests.md).

Cancellation and registry failures are propagated to the caller. If the
operation fails, copied dependencies can remain at the destination. Blob upload
sessions created by the operation are canceled after a failed transfer when the
destination remains reachable. The root reference is published only after its
reachable dependencies have been copied.
