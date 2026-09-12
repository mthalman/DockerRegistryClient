# AGENTS

## Build

All commands run from the `src/` directory:

```shell
dotnet restore
dotnet build
```

The test project targets `net10.0` and exercises the `net8.0` library asset.
Run unit tests without Docker:

```shell
dotnet test -f net10.0 --filter "Category!=Integration"
```

Integration tests use `--filter "Category=Integration"` and require Docker.
See `CONTRIBUTING.md` for coverage collection commands.

## Architecture

This is a .NET client library for the [Docker Registry HTTP API](https://docs.docker.com/registry/spec/api/), published as NuGet package `Valleysoft.DockerRegistryClient`.

`RegistryClient` is the main entry point. It exposes registry operations through interface-backed properties:

- `Blobs` (`IBlobOperations`) – get, upload, delete blobs
- `Catalog` (`ICatalogOperations`) – list repositories
- `Tags` (`ITagOperations`) – list tags
- `Manifests` (`IManifestOperations`) – get/check manifests (Docker v2 and OCI)
- `Referrers` (`IReferrerOperations`) – OCI referrers API

Each operation group follows the pattern: public interface → `internal` implementation class → optional extension methods for convenience (e.g., `BlobOperationsExtensions.UploadAsync`).

Authentication is handled transparently by `OAuthDelegatingHandler`, which intercepts 401 responses, performs the OAuth2 bearer token flow, and retries the request. Callers supply credentials via `IRegistryClientCredentials` implementations (`BasicAuthenticationCredentials`, `TokenCredentials`).

Paginated results are wrapped in `Page<T>`, which carries a `NextPageLink` for cursor-based pagination.

## Conventions

- **Multi-targeting**: The library targets `netstandard2.0` and `net8.0`; tooling and tests use .NET 10. The `netstandard2.0` target is verified by compilation only. Use `#if NET5_0_OR_GREATER` / `#if NETSTANDARD2_0` preprocessor directives when APIs differ across targets.
- **Serialization**: Uses `System.Text.Json` exclusively (no Newtonsoft.Json).
- **Async pattern**: All public API methods are async, accept an optional `CancellationToken`, and use `.ConfigureAwait(false)`.
- **Error handling**: Unsuccessful HTTP responses throw `RegistryException` with `Errors` and `StatusCode` properties. Operations that check existence (e.g., `ExistsAsync`) return `true` for successful responses and `false` only for HTTP 404 Not Found; other unsuccessful responses throw `RegistryException`.
- **C# language version**: 12.0 with nullable reference types and implicit usings enabled. `CompilerServices.cs` provides an `IsExternalInit` shim for `init` properties on netstandard2.0.
- **Internal visibility**: Operation implementation classes are `internal`; only interfaces and models are public.

## Pull request labels

Every pull request must have exactly one semantic-version label, selected by the
highest-impact public change:

- `semver:major` for breaking public API or behavior
- `semver:minor` for backward-compatible public functionality
- `semver:patch` for fixes, documentation, dependencies, tests, build changes,
  or maintenance

Apply at most one canonical visible category:

- `enhancement` for features
- `bug` for fixes
- `documentation` for documentation-only changes
- `dependencies` for dependency updates
- No category for maintenance, refactoring, tests, or infrastructure

For mixed pull requests, classify by the highest-impact public change. A
test-heavy pull request that fixes a product bug is `bug`; a dependency pull
request spanning production and tooling dependencies remains `dependencies`.

Apply `skip-changelog` to internal-only test dependency updates, CI action
updates, build or tooling changes, and repository administration that are not
useful to package users. Do not apply it to production dependency updates,
user-facing fixes, features, or documentation, or significant release behavior
that users or maintainers should know about.

After creating a pull request, apply the labels on GitHub and verify them before
considering pull request creation complete.
