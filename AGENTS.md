# AGENTS

## Build

All commands run from the `src/` directory:

```shell
dotnet restore
dotnet build
```

There is no test project in this repository.

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

- **Multi-targeting**: The project targets `netstandard2.0`, `net8.0`, and `net9.0`. Use `#if NET5_0_OR_GREATER` / `#if NETSTANDARD2_0` preprocessor directives when APIs differ across targets.
- **Serialization**: Uses `System.Text.Json` exclusively (no Newtonsoft.Json).
- **Async pattern**: All public API methods are async, accept an optional `CancellationToken`, and use `.ConfigureAwait(false)`.
- **Error handling**: Unsuccessful HTTP responses throw `RegistryException` with `Errors` and `StatusCode` properties. Operations that check existence (e.g., `ExistsAsync`) return `bool` instead of throwing.
- **C# language version**: 12.0 with nullable reference types and implicit usings enabled. `CompilerServices.cs` provides an `IsExternalInit` shim for `init` properties on netstandard2.0.
- **Internal visibility**: Operation implementation classes are `internal`; only interfaces and models are public.
