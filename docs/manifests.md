# Manifest Operations

Access manifest operations via `client.Manifests`.

All snippets below use the following setup code:

```csharp
using System.Security.Cryptography;
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Docker;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

using RegistryClient client = new("mcr.microsoft.com");
BasicAuthenticationCredentials credentials = new("username", "password");
using RegistryClient destinationClient = new("myregistry.example.com", credentials);
```

## Get a manifest

`GetAsync` returns a `ManifestInfo` containing the response media type, canonical
content digest, original content bytes, and manifest:

```csharp
ManifestInfo info = await client.Manifests.GetAsync("dotnet/sdk", "latest");

Console.WriteLine($"Media type: {info.MediaType}");
Console.WriteLine($"Digest: {info.DockerContentDigest}");
await File.WriteAllBytesAsync("manifest.json", info.Content.ToArray());
```

`Content` preserves the response body exactly, so it can be used for operations
such as copying a manifest without reserializing it. Manifest content is buffered
in memory and does not require disposal.

## Publish a manifest

`PublishAsync` publishes manifest content under a tag or digest reference and
returns a `ManifestPublishResult`. Referenced config and layer blobs must
exist in the destination repository. The example publishes an OCI image
manifest with a configuration blob and no layers:

```csharp
byte[] config = """{"architecture":"amd64","os":"linux","rootfs":{"type":"layers","diff_ids":[]}}"""u8.ToArray();
string configDigest = $"sha256:{Convert.ToHexString(SHA256.HashData(config)).ToLowerInvariant()}";
using Stream configStream = new MemoryStream(config);
await destinationClient.Blobs.UploadAsync("example/image", configStream, configDigest);

var manifest = new OciImageManifest
{
    Config = new OciDescriptor
    {
        MediaType = "application/vnd.oci.image.config.v1+json",
        Digest = configDigest,
        Size = config.LongLength
    },
    Layers = []
};

ManifestPublishResult result =
    await destinationClient.Manifests.PublishAsync("example/image", "latest", manifest);

Console.WriteLine($"Digest: {result.Digest}");
Console.WriteLine($"Location: {result.Location}");
```

The model's `MediaType` is sent as the request content type. `Location` contains
the registry's `Location` response header. `Digest` contains the
`Docker-Content-Digest` response header when the registry provides it; otherwise,
it is `null`.

The byte-oriented overload accepts the original bytes and media type.
`PublishAsync` does not copy referenced blobs or child manifests;
[CopyAsync](copy.md) copies the reachable content graph.

```csharp
ManifestInfo source = await client.Manifests.GetAsync("dotnet/sdk", "latest");

ManifestPublishResult copied = await destinationClient.Manifests.PublishAsync(
    "example/image",
    "copied",
    source.Content,
    source.MediaType);
```

This overload sends the provided bytes unchanged and supports vendor-specific
manifest formats. Publishing a `RawManifest` through the model overload also
preserves its original bytes.

The model overload without `JsonTypeInfo` is trim- and Native AOT-safe for
`RawManifest` and the built-in Docker and OCI manifest models, which use
source-generated JSON metadata. It does not fall back to reflection: publishing
a custom `IManifest` implementation, including a type derived from a built-in
model, throws `NotSupportedException`.

Custom manifests, including in trimmed or Native AOT applications, require
the overload accepting `JsonTypeInfo` for their concrete type. The
[custom-manifest migration example](migrations/8.0.0/custom-manifest-json-metadata.md#recommended-action)
shows the manifest class, source-generated context, and publication together.

Publishing custom serialized content as a `RawManifest` or through the
byte-oriented overload is also supported.

When an OCI manifest or index includes a `subject`, publishing maintains the
referrers tag-schema fallback if the registry does not acknowledge native
referrers support with an `OCI-Subject` response header. Deleting a
subject-bearing manifest also removes it from that fallback index when the
native referrers API is unavailable.

The built-in manifest operations implement `IManifestWriteOperations`. Custom
`IManifestOperations` implementations must also implement that capability
interface to support the publishing and deletion extension methods.

## Delete a manifest

`DeleteAsync` accepts a digest, not a tag. Deleting a manifest makes it
unavailable through every tag that references it in that repository.
Deletion requires registry support and authorization.
[DeleteTagAsync](#delete-a-tag) removes only a tag association.

```csharp
string digest = await destinationClient.Manifests.GetDigestAsync("example/image", "latest");
await destinationClient.Manifests.DeleteAsync("example/image", digest);
```

## Delete a tag

`DeleteTagAsync` removes a tag association while preserving the referenced
manifest and other tags that point to it:

```csharp
await destinationClient.Manifests.DeleteTagAsync("example/image", "old-tag");
```

`DeleteTagAsync` does not accept digest references. Tag deletion is optional in
the OCI Distribution specification, so registries that do not support it may
return `400 Bad Request` or `405 Method Not Allowed`. These responses are
reported as `RegistryException` instances like other unsuccessful registry
responses.

## Check whether a manifest exists

```csharp
bool exists = await client.Manifests.ExistsAsync("dotnet/sdk", "latest");
```

`ExistsAsync` accepts a tag or digest. It returns `true` for successful HTTP
responses and `false` only for `404 Not Found`. Other non-success responses
throw `RegistryException`; see [Error Handling](error-handling.md).

## Get a digest

`GetDigestAsync` resolves a tag or digest reference to a manifest digest:

```csharp
string digest = await client.Manifests.GetDigestAsync("dotnet/sdk", "latest");
```

`GetDigestAsync` first sends a HEAD request. If the response includes
`Docker-Content-Digest`, that value is returned. Without that header, a
digest-pinned request returns its requested digest after a successful HEAD.
A tag lookup instead downloads the manifest with GET to resolve its digest.

## Manifest types

The `Manifest` property on `ManifestInfo` is typed as `IManifest`. Known Docker
and OCI media types are deserialized to their concrete models. Other media types
are returned as `RawManifest` so new and vendor-specific manifest formats remain
accessible without a library update.

The type hierarchy:

| Interface | Description |
| --- | --- |
| `IManifest` | Base type with the manifest media type |
| `IImageManifest` | Single image with `Config` and `Layers` |
| `IManifestList` | Multi-platform index with `Manifests` |

Concrete implementations:

| Class | Media Type Constant | Format |
| --- | --- | --- |
| `DockerManifest` | `ManifestMediaTypes.DockerManifestSchema2` | Docker V2 image manifest |
| `ManifestList` | `ManifestMediaTypes.DockerManifestList` | Docker V2 manifest list |
| `OciImageManifest` | `ManifestMediaTypes.OciManifestSchema1` | OCI image manifest |
| `OciImageIndex` | `ManifestMediaTypes.OciImageIndex1` | OCI image index |
| `RawManifest` | Any other response media type | Original manifest content |

## Handle manifest types

`ManifestInfo.Manifest` supports pattern matching on its concrete type:

```csharp
ManifestInfo info = await client.Manifests.GetAsync("dotnet/sdk", "latest");

switch (info.Manifest)
{
    case DockerManifest dockerManifest:
        Console.WriteLine($"Docker image with {dockerManifest.Layers.Length} layers");
        break;
    case ManifestList manifestList:
        Console.WriteLine($"Docker manifest list with {manifestList.Manifests.Length} entries");
        break;
    case OciImageManifest ociManifest:
        Console.WriteLine($"OCI image with {ociManifest.Layers.Length} layers");
        break;
    case OciImageIndex ociIndex:
        Console.WriteLine($"OCI index with {ociIndex.Manifests.Length} entries");
        break;
    case RawManifest raw:
        Console.WriteLine($"Raw manifest with {raw.Content.Length} bytes");
        break;
}
```

`MediaType` also identifies the manifest format:

```csharp
if (info.MediaType == ManifestMediaTypes.DockerManifestList)
{
    var list = (ManifestList)info.Manifest;
    // ...
}
```

`ManifestInfo.MediaType` comes from the response's `Content-Type` header.
`DockerContentDigest` uses `Docker-Content-Digest` when present. Otherwise,
retrieval uses the requested digest or calculates SHA-256 over the original
response bytes for a tag lookup. Supported SHA-256, SHA-384, and SHA-512
digests are verified against retrieved bytes.

`RawManifest.MediaType` uses the response media type; the JSON document does
not need to contain its own `mediaType` field.
