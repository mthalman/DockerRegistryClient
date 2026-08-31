# Authentication

`RegistryClient` supports anonymous access, basic credentials, bearer tokens,
and custom credentials. When `RegistryClient` creates its own `HttpClient`, it
also configures the handler that performs an OAuth 2.0 bearer token exchange
after a registry authentication challenge.

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
automatic bearer challenge handling is unavailable. Use the default
`HttpClient` when you need the built-in OAuth token exchange.
