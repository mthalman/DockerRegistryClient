# Remove netstandard2.0 support

**Version introduced:** 7.0.0

`Valleysoft.DockerRegistryClient` now targets `net8.0` only. Applications or
libraries that still target `netstandard2.0` must retarget to `net8.0` or newer
before upgrading to this package version.

## Previous behavior

In earlier releases, the package multi-targeted `netstandard2.0` and `net8.0`.
Consumers could reference the library from a `netstandard2.0` application or a
compatible runtime that still depended on the older target.

## New behavior

This release removes the `netstandard2.0` target and the compatibility shims that
were only needed for that runtime. The library no longer publishes a
`netstandard2.0` asset, so older frameworks and consumers pinned to that target
must move to a supported .NET runtime before using the newer version.

## Type of breaking change

This is a binary and compatibility break for consumers that still target
`netstandard2.0`. Existing downstream projects must retarget or multi-target to
`net8.0+` before consuming the updated package.

## Reason for change

The project is consolidating on a single supported .NET target, simplifying the
implementation and removing compatibility branches that exist only to support a
runtime the library no longer intends to claim.

## Recommended action

Retarget consuming applications and libraries to `net8.0` or newer before
upgrading to this package version. Build and test the downstream project against
the newer target to catch any compatibility issues introduced by the runtime
change.

## Affected APIs

- Package target framework: `Valleysoft.DockerRegistryClient` now targets
  `net8.0` only.
- Removed compatibility paths: netstandard-specific upload and temporary-file
  handling logic.
