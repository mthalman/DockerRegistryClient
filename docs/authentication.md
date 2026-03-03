# Authentication

`RegistryClient` supports several authentication modes. OAuth token exchange is handled automatically via the built-in `OAuthDelegatingHandler` — you only need to supply the initial credentials.

## Anonymous Access

No credentials required. Works with public registries like `mcr.microsoft.com`:

```csharp
using RegistryClient client = new("mcr.microsoft.com");
```

## Basic Auth

```csharp
using Valleysoft.DockerRegistryClient.Credentials;

var credentials = new BasicAuthenticationCredentials("username", "password");
using RegistryClient client = new("myregistry.example.com", credentials);
```

## Token Auth

Use a pre-obtained token. The default token type is `Bearer`:

```csharp
using Valleysoft.DockerRegistryClient.Credentials;

var credentials = new TokenCredentials("mytoken");
using RegistryClient client = new("myregistry.example.com", credentials);
```

You can specify a custom token type:

```csharp
var credentials = new TokenCredentials("mytoken", "CustomScheme");
```

## Custom Credentials

Implement `IRegistryClientCredentials` to fully control how the `Authorization` header is set:

```csharp
using Valleysoft.DockerRegistryClient.Credentials;

public class MyCredentials : IRegistryClientCredentials
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

## HttpClient Control

For advanced scenarios, you can inject your own `HttpClient`:

```csharp
var httpClient = new HttpClient();
using RegistryClient client = new("myregistry.example.com", credentials, httpClient, disposeHttpClient: true);
```

When no `HttpClient` is provided, `RegistryClient` creates one internally with an `OAuthDelegatingHandler` and disposes it automatically. When you inject your own, set `disposeHttpClient: true` if you want the client to dispose it.
