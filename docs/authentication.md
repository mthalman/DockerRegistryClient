# Authentication

`RegistryClient` supports anonymous access, basic credentials, bearer tokens,
and custom credentials.

When `RegistryClient` creates its own `HttpClient`, it also configures an
OAuth 2.0 bearer token exchange handler for registry authentication challenges
and explicit redirect handling over the standard .NET `HttpClientHandler`.
Both HTTP and HTTPS are supported, including private-network and loopback
destinations. HTTPS uses normal certificate validation.

The library trusts the configured registry to select token services, upload
locations, and redirect targets. It does not filter destination IP addresses
or provide an SSRF boundary. Applications that accept untrusted registry
addresses must enforce their own destination policy.

## Use anonymous access

Public registries such as `mcr.microsoft.com` may allow requests without
credentials:

```csharp
using Valleysoft.DockerRegistryClient;

using RegistryClient client = new("mcr.microsoft.com");
```

## Use basic credentials

```csharp
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Credentials;

BasicAuthenticationCredentials credentials = new("username", "password");
using RegistryClient client = new("myregistry.example.com", credentials);
```

The client sends the basic credentials with the registry request. If the
registry responds with a bearer challenge, the built-in OAuth handler uses the
credentials when it requests an access token.

## Use a token

Use a previously obtained token. The default authentication scheme is
`Bearer`:

```csharp
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Credentials;

TokenCredentials credentials = new("mytoken");
using RegistryClient client = new("myregistry.example.com", credentials);
```

Pass a second argument when the token uses a different authentication scheme:

```csharp
TokenCredentials credentials = new("mytoken", "CustomScheme");
```

## Provide custom credentials

Implement `IRegistryClientCredentials` to control how each request sets its
`Authorization` header:

```csharp
using System.Net.Http.Headers;
using Valleysoft.DockerRegistryClient.Credentials;

public sealed class MyCredentials : IRegistryClientCredentials
{
    public Task ProcessHttpRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", "my-dynamic-token");
        return Task.CompletedTask;
    }
}
```

`RegistryClient` calls `ProcessHttpRequestAsync` only when the request does not
already contain an `Authorization` header.

The callback is not invoked for direct pagination or upload requests to
another origin. Cached upload authorization is also scoped to the origin
where it was established.

Custom request and content headers are caller-controlled. Callback changes
are not rolled back on redirects, and headers other than `Authorization`
can be forwarded to another origin. This includes manually supplied cookies
and custom API-key headers. Do not assume arbitrary header names receive
credential protection.

`DefaultRequestHeaders` applies to every request made through the HTTP client,
including cross-origin continuation requests and direct calls through the
exposed `HttpClient`. Defaults are neither rejected nor mutated. Use
`IRegistryClientCredentials` to set `Authorization` instead of putting
registry-specific secrets in shared default headers.

## Control the HTTP pipeline

Pass an `HttpClient` when you need to configure transport behavior, proxies, or
additional handlers:

```csharp
using Valleysoft.DockerRegistryClient;

HttpClient httpClient = new();
using RegistryClient client = new(
    "myregistry.example.com",
    credentials,
    httpClient,
    disposeHttpClient: true);
```

When `RegistryClient` creates the `HttpClient`, it disposes the client with the
`RegistryClient`. For an injected `HttpClient`, `disposeHttpClient` defaults to
`false`.

An injected `HttpClient` does not include the library's internal OAuth handler.
The supplied credentials still set the initial `Authorization` header, but
automatic bearer challenge handling and the built-in redirect behavior
are unavailable. The supplied pipeline is responsible for applying an
appropriate redirect and SSRF policy. If the registry uses bearer challenges,
the supplied pipeline must provide token-handling behavior or use credentials
that the registry accepts without the built-in challenge flow. Use the default
`HttpClient` when you need the built-in OAuth token exchange and redirect
handling.

## Proxy support

The default pipeline uses .NET's standard proxy selection, bypass rules,
credentials, DNS resolution, and connection pooling. It honors
`HttpClient.DefaultProxy`; it does not resolve and pin proxy destinations to
IP addresses. Private corporate proxies and proxy-resolved hostnames use the
same transport as other requests.

## Token services and redirects

Bearer challenges are processed only for the configured registry origin
(scheme, host, and port), so a redirected storage endpoint cannot request a
token exchange with the original registry credentials. A registry's token
realm may be a different HTTP or HTTPS origin, including a private address.
Use trusted registries and HTTPS token services when sending credentials.

Token requests follow the same redirect rules as other requests; they are not
pinned to the realm's origin. The client retains its .NET-style behavior:
redirects clear `Authorization`, and HTTPS-to-HTTP redirects are not followed.
Body-preserving redirects can forward a refresh-token form to another origin.
Other headers are not filtered through an allowlist.

This transport trust model follows the registry clients in
[BuildKit v0.33.0](https://github.com/moby/buildkit/blob/v0.33.0/util/resolver/resolver.go)
and [Moby v29.8.1](https://github.com/moby/moby/blob/docker-v29.8.1/daemon/pkg/registry/registry.go),
with [containerd's token handling](https://github.com/moby/buildkit/blob/v0.33.0/vendor/github.com/containerd/containerd/v2/core/remotes/docker/auth/fetch.go).
It does not reproduce Go's redirect behavior: the library keeps its existing
.NET method-conversion, authorization-clearing, and HTTPS-downgrade rules.
