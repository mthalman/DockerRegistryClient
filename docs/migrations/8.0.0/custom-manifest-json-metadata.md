# Require JSON metadata to publish custom manifest types

**Version introduced:** 8.0.0

`IManifestOperations.PublishAsync(string, string, IManifest, CancellationToken)`
no longer serializes arbitrary manifest implementations through reflection. It
now throws `NotSupportedException` for manifest types other than the library's
built-in Docker and OCI models and `RawManifest`.

## Previous behavior

In v7.0.0, the model overload serialized any `IManifest` implementation using
its runtime type. Custom manifest types, including types derived from
`DockerManifest`, `ManifestList`, `OciImageManifest`, and `OciImageIndex`, were
serialized with reflection-based `System.Text.Json` APIs.

## New behavior

The model overload serializes the built-in manifest models with
source-generated JSON metadata and publishes `RawManifest` content unchanged.
Any other manifest type, including a type derived from a built-in model, throws
`NotSupportedException`. A new overload accepts a `JsonTypeInfo<TManifest>` for
publishing custom manifest types.

## Type of breaking change

This is a behavioral break for callers that publish custom `IManifest`
implementations through the model overload. Method signatures of existing
overloads are unchanged, so the change does not itself require source edits to
compile.

## Reason for change

Reflection-based serialization inside the library produced trim and Native AOT
analysis warnings (IL2026 and IL3050) that consumers had to suppress, and it
could silently lose manifest properties in trimmed applications. Removing the
reflection path makes the package safe for trimmed and Native AOT applications.

## Recommended action

Publish custom manifest types with the overload that accepts source-generated
JSON metadata:

```csharp
using System.Text.Json.Serialization;
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Models.Manifests;

sealed class CustomManifest : Manifest
{
    [JsonPropertyName("customValue")]
    public string? CustomValue { get; set; }
}

[JsonSerializable(typeof(CustomManifest))]
partial class AppJsonContext : JsonSerializerContext
{
}

await client.Manifests.PublishAsync(
    "example/image",
    "latest",
    customManifest,
    AppJsonContext.Default.CustomManifest);
```

Alternatively, serialize the manifest in the application and publish the bytes
with the content overload or as a `RawManifest`.

## Affected APIs

- `ManifestOperationsExtensions.PublishAsync(IManifestOperations, string, string, IManifest, CancellationToken)`,
  accessed through `RegistryClient.Manifests`.
- New: `ManifestOperationsExtensions.PublishAsync<TManifest>(IManifestOperations, string, string, TManifest, JsonTypeInfo<TManifest>, CancellationToken)`.
- Unchanged: `ManifestOperationsExtensions.PublishAsync(IManifestOperations, string, string, ReadOnlyMemory<byte>, string, CancellationToken)`.
