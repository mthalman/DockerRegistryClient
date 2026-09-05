# Manifest Operations

Access manifest operations via `client.Manifests`.

## Get a manifest

`GetAsync` returns a `ManifestInfo` containing the response media type, canonical
content digest, original content bytes, and manifest:

```csharp
using RegistryClient client = new("mcr.microsoft.com");
ManifestInfo info = await client.Manifests.GetAsync("dotnet/sdk", "latest");

Console.WriteLine($"Media type: {info.MediaType}");
Console.WriteLine($"Digest: {info.DockerContentDigest}");
await File.WriteAllBytesAsync("manifest.json", info.Content.ToArray());
```

`Content` preserves the response body exactly, so it can be used for operations
such as copying a manifest without reserializing it. Manifest content is buffered
in memory and does not require disposal.

## Publish a manifest

Publish a Docker or OCI model under a tag or digest reference:

```csharp
var manifest = new OciImageManifest
{
    Config = new OciDescriptor
    {
        MediaType = "application/vnd.oci.image.config.v1+json",
        Digest = configDigest,
        Size = configSize
    },
    Layers = []
};

ManifestPublishResult result =
    await client.Manifests.PublishAsync("example/image", "latest", manifest);

Console.WriteLine($"Digest: {result.Digest}");
Console.WriteLine($"Location: {result.Location}");
```

The model's `MediaType` is sent as the request content type. `Location` contains
the registry's `Location` response header. `Digest` contains the
`Docker-Content-Digest` response header when the registry provides it; otherwise,
it is `null`.

Use the byte-oriented overload when copying a retrieved manifest or when its
exact representation matters:

```csharp
ManifestInfo source = await sourceClient.Manifests.GetAsync("example/image", "latest");

ManifestPublishResult copied = await destinationClient.Manifests.PublishAsync(
    "example/image",
    "copied",
    source.Content,
    source.MediaType);
```

This overload sends the provided bytes unchanged and supports vendor-specific
manifest formats. Publishing a `RawManifest` through the model overload also
preserves its original bytes.

When an OCI manifest or index includes a `subject`, publishing maintains the
referrers tag-schema fallback if the registry does not acknowledge native
referrers support with an `OCI-Subject` response header. Deleting a
subject-bearing manifest also removes it from that fallback index when the
native referrers API is unavailable.

The built-in manifest operations implement `IManifestWriteOperations`. Custom
`IManifestOperations` implementations must also implement that capability
interface to support the publishing and deletion extension methods.

## Delete a manifest

Resolve a tag to its canonical digest and delete the manifest by digest:

```csharp
string digest = await client.Manifests.GetDigestAsync("example/image", "latest");
await client.Manifests.DeleteAsync("example/image", digest);
```

## Check whether a manifest exists

```csharp
bool exists = await client.Manifests.ExistsAsync("dotnet/sdk", "latest");
```

`ExistsAsync` accepts a tag or digest and returns `false` for any non-success
HTTP status.

## Get a digest

Retrieve the digest for a tag or digest reference without downloading the full manifest:

```csharp
string digest = await client.Manifests.GetDigestAsync("dotnet/sdk", "latest");
```

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

Use pattern matching to handle different manifest types:

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

You can also match on `MediaType`:

```csharp
if (info.MediaType == ManifestMediaTypes.DockerManifestList)
{
    var list = (ManifestList)info.Manifest;
    // ...
}
```

`ManifestInfo.MediaType` and `DockerContentDigest` come from the registry's HTTP
response headers. `RawManifest.MediaType` uses that same response media type;
the JSON document does not need to contain its own `mediaType` field.
