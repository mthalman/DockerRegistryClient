### Read schema versions from typed manifests

#### Previous behavior

In v6.2.0, `IManifest` exposed `int SchemaVersion { get; }`. Callers could
read `manifestInfo.Manifest.SchemaVersion` without checking the manifest type.
`IManifestOperations.GetAsync` returned a `ManifestInfo` whose `Manifest`
property contained a built-in Docker or OCI model. It rejected unrecognized
response media types with `NotSupportedException`.

#### New behavior

[PR #151](https://github.com/mthalman/DockerRegistryClient/pull/151) removes
`SchemaVersion` from `IManifest`. The interface retains `MediaType`.
The existing typed models still inherit the public, read/write
`Manifest.SchemaVersion` property.

For unrecognized response media types, `GetAsync` now returns a `ManifestInfo`
whose `Manifest` property contains a `RawManifest`, instead of throwing
`NotSupportedException`. `RawManifest` implements `IManifest`, not `Manifest`,
and exposes `MediaType` and `Content`, with no schema-version property.
Results retrieved through `GetAsync` also preserve the original bytes in
`ManifestInfo.Content`, including for recognized formats.

#### Type of breaking change

This is a source and binary compatibility break for code that references
`IManifest.SchemaVersion`. Recompilation requires changing those references;
existing binaries that call the removed interface getter must be rebuilt.
Code accessing `SchemaVersion` through `Manifest` or a built-in concrete model
does not lose that property.

Custom implementations with an explicit `IManifest.SchemaVersion` implementation
must remove or replace it before recompiling. An ordinary public
`SchemaVersion` property may remain on a custom type, but is no longer part of
the interface contract. Callers must also handle the newly possible raw result.

#### Reason for change

A schema-version field is not universal to vendor-specific or emerging manifest
formats. Raw support lets callers inspect and preserve those formats without
requiring a new library model or changing the original serialized bytes.

#### Recommended action

Rebuild code that uses the removed interface getter. If your operation requires
a supported typed schema, pattern-match before reading it and explicitly reject
unsupported formats rather than assuming every `IManifest` derives from
`Manifest`:

```csharp
using Valleysoft.DockerRegistryClient.Models.Manifests;

static int GetSchemaVersion(IManifest manifest) =>
    manifest is Manifest typedManifest
        ? typedManifest.SchemaVersion
        : throw new NotSupportedException(
            $"A typed schema is required for media type '{manifest.MediaType}'.");
```

If your operation instead preserves or forwards arbitrary manifests, use
`ManifestInfo.MediaType` and `ManifestInfo.Content` from the retrieval result,
or `RawManifest.MediaType` and `RawManifest.Content`. Do not reserialize a typed
model when byte identity matters. Parse raw bytes only according to the
specific format you support; do not assume that they are JSON or contain a
`schemaVersion` field. A manually constructed `ManifestInfo` using the
three-argument constructor has no original bytes; use the content-taking
constructor when supplying them yourself.

For a custom manifest with an explicit getter, remove the obsolete
`IManifest.SchemaVersion` declaration. If your own consumers need the value,
expose it on your concrete type or your own interface and update those callers.

#### Affected APIs

- Removed: `Valleysoft.DockerRegistryClient.Models.Manifests.IManifest.SchemaVersion`.
- Retrieval: `IManifestOperations.GetAsync(string repositoryName, string tagOrDigest, CancellationToken cancellationToken = default)`
  and its `ManifestInfo.Manifest` result.
- Migration alternatives: `Manifest.SchemaVersion`, `ManifestInfo.Content`,
  `ManifestInfo.MediaType`, `RawManifest.Content`, and `RawManifest.MediaType`.
