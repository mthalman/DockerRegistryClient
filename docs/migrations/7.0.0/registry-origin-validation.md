# Supply registry origins and valid resource references

**Version introduced:** 7.0.0

`RegistryClient` now rejects registry URLs with non-root paths, queries, or
fragments instead of silently discarding those components. Registry operations
also reject invalid repository names, tags, and digests locally. Update affected
configuration and resource arguments before upgrading.

## Previous behavior

In v6.2.0, `RegistryClient` parsed the `registry` argument as an absolute URI
(adding `https://` when no scheme was supplied) and used
`registryUri.GetLeftPart(UriPartial.Authority)` as its base URI. Paths,
queries, and fragments were silently discarded. For example,
`https://registry.example/v2/` constructed a client targeting the same base
as `https://registry.example`, not a registry under a custom path prefix.

Registry operations also interpolated repository names and references into
endpoint URLs without the current shared validation, so malformed references
could alter a request or reach the server rather than fail locally.

## New behavior

[PR #176](https://github.com/mthalman/DockerRegistryClient/pull/176) requires
`registry` to identify an HTTP or HTTPS origin. A host name without a scheme
still uses HTTPS. Explicit ports, Unicode/IDN hosts, bracketed IPv6 hosts, and
one trailing root slash remain supported.

Construction throws `ArgumentException` for invalid origins, including empty
or whitespace-only values, any whitespace or control character, backslashes,
non-HTTP(S) schemes, missing hosts, nonempty URI user information, non-root
paths, queries, or fragments. Even empty `?` or `#` suffixes are rejected.
Extra slashes and dot-segment paths are rejected rather than normalized away.
A null `registry` still throws `ArgumentNullException`.

Operations now validate repository names, tags, and digests before sending
requests. Required null references throw `ArgumentNullException`; malformed
values throw `ArgumentException`. Upload convenience methods and copy
operations validate their references before starting requests or consuming
upload content.

## Type of breaking change

This is a configuration and behavioral break, with unchanged public
signatures. Existing source still compiles, but configurations that relied on
silently removing URL components now fail during construction. Invalid
resource references fail locally instead of relying on registry responses.
This change does not itself add or remove interface members or introduce a
binary API break.

Ordinary `RegistryClient` consumers are affected, not just custom interface
implementers. Custom operations are not automatically validated by the internal
helpers, although public convenience methods can validate before delegating
to them.

## Reason for change

Explicit origin validation avoids silently accepting a URL different from the
one the caller intended. Shared reference validation and endpoint construction
keep caller-supplied values from changing path or query boundaries and provide
consistent argument errors across registry operations.

## Recommended action

Confirm that the intended registry serves its API at `/v2/` on the configured
host. If a reverse proxy exposes the registry only under a custom path prefix,
configure an origin that serves the API at `/v2/` first. The client does not
support a custom base-path prefix; the old behavior discarded that prefix.
Do not silently strip arbitrary URLs at runtime to bypass validation.

Then update registry configuration to store only the intended origin, removing
accidental path, query, and fragment suffixes. For example, replace
`https://registry.example/v2/` with `https://registry.example` or
`https://registry.example/`. The client adds `/v2/` to registry API requests:

```csharp
using Valleysoft.DockerRegistryClient;

using var client = new RegistryClient("https://registry.example");
string digest = await client.Manifests.GetDigestAsync(
    "team/image", "latest").ConfigureAwait(false);
```

Pass authentication through `IRegistryClientCredentials`, such as
`BasicAuthenticationCredentials` or `TokenCredentials`, using a
credential-taking constructor rather than embedding user information in a URL.
Retain an explicit `http://` only when that is the intended transport.

Pass repository names such as `team/image` separately from the registry origin.
Use valid tags and complete digests without URL-encoding them. Replace
abbreviated digest fixtures with digests computed from the fixture content;
do not pad abbreviated values such as `sha256:abc`.

For example, resolve a manifest tag with `GetDigestAsync` as above, then pass
the resulting digest to operations that require it. Blob operations need the
blob's own digest, not its parent manifest's digest. Update invalid fixtures
and configuration inputs rather than catching argument errors and continuing.
Keep server-issued pagination and upload links intact; they are URI references,
not constructor origins or repository-name arguments.

### Server-issued links

Pass a returned `Location` or `NextPageLink` back unchanged. These links can
contain percent-encoded information that the server needs for subsequent
requests. Absolute URLs, scheme-relative links (starting with `//`), and
relative links remain supported.

Upload results return `Location` as an absolute URL. Relative locations from
the server are resolved against the request that produced the response,
including after redirects, using standard .NET URI resolution.

Server-issued links are resolved with `System.Uri`, rather than a custom
parser or character policy. Dot segments and percent encodings receive
normal .NET canonicalization; the library does not promise byte-for-byte
preservation of the supplied URL. For example, `%41` can normalize to `A`.
Links are not validated as repository names. Fragments (`#...`) may remain
in URI metadata but are not sent in HTTP requests.

Pagination and upload links can point to another origin. For these links,
configured credentials are applied only to requests matching the registry's scheme,
host, and port. The built-in OAuth handler does not process authentication
challenges from a different origin, so those challenges cannot reuse
credentials saved from the original registry request.

`HttpClient.DefaultRequestHeaders` remains caller-controlled and applies to
cross-origin pagination and upload requests as well as registry requests.
The client does not reject or mutate these defaults. Configure registry
authentication through `IRegistryClientCredentials` instead of shared default
credential headers. Custom headers, including headers added by that callback,
are not removed on redirects; only `Authorization` is cleared.

Authentication policy is outside OCI's scope. The client retains its
normal .NET transport behavior: the built-in HTTP handler supports HTTP(S),
and HTTPS-to-HTTP redirects are not followed. URI parsing and normalization
are delegated to .NET, without additional server-link restrictions.
Upload-session locations may use HTTP or HTTPS, even when they differ from
the registry's scheme. There is no destination-IP filtering. See
[authentication](../../authentication.md) for the registry trust model and
the distinction between automatic redirects and session locations.

### Resource-reference formats

The accepted resource-reference formats are:

- Repository names are at most 255 characters and contain slash-separated
  lowercase alphanumeric components. Within a component, alphanumeric runs
  may be separated by one dot, one or two underscores, or one or more hyphens.
  Use `team/image`, not a full URL, leading slash, or percent-encoded path.
- Tags are 1 to 128 ASCII characters, start with a letter, digit, or underscore,
  and then contain only letters, digits, underscores, dots, or hyphens. Tags
  may contain uppercase letters.
- Digests contain an algorithm and encoded value separated by `:`.
  `sha256` and `blake3` require 64 lowercase hexadecimal characters, `sha384`
  requires 96, and `sha512` requires 128. Other algorithm names use lowercase
  alphanumeric runs separated by a single `+`, `.`, `_`, or `-`, with a
  nonempty encoded value containing only ASCII letters, digits, `=`, `_`, or
  `-`. Use a complete digest returned by the registry or computed from the
  actual content.

## Affected APIs

- All three public `RegistryClient` constructors: `(string registry)`,
  `(string registry, IRegistryClientCredentials? serviceClientCredentials)`,
  and `(string registry, IRegistryClientCredentials? serviceClientCredentials, HttpClient? httpClient, bool disposeHttpClient = false)`.
- `IBlobOperations.GetAsync`, `GetRangeAsync`, `ExistsAsync`, `DeleteAsync`,
  `BeginUploadAsync`, and `EndUploadAsync`; `BlobOperationsExtensions.UploadAsync`
  and, through blob retrieval, `GetImageAsync`.
- `IManifestOperations.GetAsync`, `ExistsAsync`, and `GetDigestAsync`;
  the repository-name overloads of `ITagOperations.GetAsync` and
  `IReferrerOperations.GetAsync`.
- The current manifest write APIs `IManifestWriteOperations.PublishAsync`,
  `DeleteAsync`, and `DeleteTagAsync`, their `ManifestOperationsExtensions`
  wrappers, and `RegistryClientExtensions.CopyAsync` also enforce these
  reference rules. These write and copy APIs were introduced after v6.2.0;
  their validation is not a separate removed capability from that release.
