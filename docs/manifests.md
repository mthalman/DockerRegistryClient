# Manifest Operations

Access manifest operations via `client.Manifests`.

## Getting a Manifest

`GetAsync` returns a `ManifestInfo` containing the media type, content digest, and deserialized manifest:

```csharp
using RegistryClient client = new("mcr.microsoft.com");
ManifestInfo info = await client.Manifests.GetAsync("dotnet/sdk", "latest");

Console.WriteLine($"Media type: {info.MediaType}");
Console.WriteLine($"Digest: {info.DockerContentDigest}");
```

## Checking Existence

```csharp
bool exists = await client.Manifests.ExistsAsync("dotnet/sdk", "sha256:abc123...");
```

## Getting a Digest

Retrieve the digest for a tag or digest reference without downloading the full manifest:

```csharp
string digest = await client.Manifests.GetDigestAsync("dotnet/sdk", "latest");
```

## Manifest Types

The `Manifest` property on `ManifestInfo` is typed as `IManifest`. The type hierarchy:

| Interface | Description |
|---|---|
| `IManifest` | Base — has `SchemaVersion` and `MediaType` |
| `IImageManifest` | Single image — has `Config` and `Layers` |
| `IManifestList` | Multi-arch index — has `Manifests` |

Concrete implementations:

| Class | Media Type Constant | Format |
|---|---|---|
| `DockerManifest` | `ManifestMediaTypes.DockerManifestSchema2` | Docker V2 image manifest |
| `ManifestList` | `ManifestMediaTypes.DockerManifestList` | Docker V2 manifest list |
| `OciImageManifest` | `ManifestMediaTypes.OciManifestSchema1` | OCI image manifest |
| `OciImageIndex` | `ManifestMediaTypes.OciImageIndex1` | OCI image index |

## Pattern Matching

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
