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
