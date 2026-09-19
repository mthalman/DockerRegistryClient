# Implement ranged downloads on custom blob operations

**Version introduced:** 7.0.0

`IBlobOperations` now requires `GetRangeAsync`. Custom implementations,
decorators, and test doubles must add the member and be rebuilt. Callers using
the built-in `RegistryClient.Blobs.GetAsync` need no changes for this addition.

## Previous behavior

In v6.2.0, `IBlobOperations` required `GetAsync` for full blob downloads but
had no `GetRangeAsync` member. Custom implementations, decorators, and test
doubles could implement the interface without providing ranged downloads.

## New behavior

[PR #155](https://github.com/mthalman/DockerRegistryClient/pull/155) adds a
required interface member, with no default implementation:

```csharp
Task<BlobDownloadResult> GetRangeAsync(
    string repositoryName,
    string digest,
    long offset,
    long? length = null,
    CancellationToken cancellationToken = default);
```

`offset` is zero-based; `length` is a byte count, not an ending offset.
Omitting `length` requests the remaining bytes. The built-in implementation
streams the response after headers arrive and returns the stream with range
metadata in `BlobDownloadResult`. A registry can return a contained portion
of the requested range or ignore the range and return the full blob.

## Type of breaking change

This is a source and binary compatibility break for custom
`IBlobOperations` implementations. Existing concrete implementations must add
the member to compile, and previously compiled implementations must be updated
and rebuilt before use with the new interface.

Ordinary callers using `RegistryClient.Blobs` do not need to switch download
methods: the built-in implementation provides the new member, and `GetAsync`
retains its existing full-download, buffered behavior.

## Reason for change

Ranged downloads allow callers to fetch selected bytes from large blobs and
build resumable transfers without downloading every blob from the beginning.
The result metadata makes it possible to detect when a registry ignores or
only partially satisfies a requested range.

## Recommended action

Update and rebuild custom implementations and decorators; regenerate or update
test doubles. A decorator whose `inner` field is an `IBlobOperations` supplied
by `RegistryClient.Blobs` can forward the new member:

```csharp
public Task<BlobDownloadResult> GetRangeAsync(
    string repositoryName,
    string digest,
    long offset,
    long? length = null,
    CancellationToken cancellationToken = default) =>
    inner.GetRangeAsync(
        repositoryName, digest, offset, length, cancellationToken);
```

For an independent implementation, preserve the range contract: reject negative
offsets, nonpositive lengths, and ranges whose inclusive end overflows `long`
with `ArgumentOutOfRangeException`. Propagate cancellation and report the
actual returned bytes through `IsRangeHonored`, inclusive `RangeStart` and
`RangeEnd`, and nullable `TotalLength`. Do not report a full-body fallback as
an honored partial range. The built-in implementation validates HTTP 206
`Content-Range` metadata and throws `RegistryException` for unsuccessful HTTP
responses, including HTTP 416.

Configure test results with matching content and metadata. Callers own and must
dispose `BlobDownloadResult.Content`; the result object itself is not
`IDisposable`. When implementing resume logic, append only if `IsRangeHonored`
is true and `RangeStart` equals the next required offset. If the registry returns
the full blob, restart rather than append it. Account for short partial ranges
and verify the completed blob against its expected digest.

## Affected APIs

- Added required member: `Valleysoft.DockerRegistryClient.IBlobOperations.GetRangeAsync(string repositoryName, string digest, long offset, long? length = null, CancellationToken cancellationToken = default)`.
- Built-in implementation entry point: `RegistryClient.Blobs`.
- Result contract: `BlobDownloadResult.Content`, `IsRangeHonored`, `RangeStart`,
  `RangeEnd`, and `TotalLength`.
- Unchanged full-download API: `IBlobOperations.GetAsync(string repositoryName, string digest, CancellationToken cancellationToken = default)`.
