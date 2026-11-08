# Catalog Operations

Access catalog operations via `client.Catalog`.

All snippets below use the following setup code:

```csharp
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models;

BasicAuthenticationCredentials credentials = new("username", "password");
using RegistryClient client = new("myregistry.example.com", credentials);
```

## List repositories

`GetAsync` returns one `Page<Catalog>`. Catalog access depends on the
registry's authorization policy.

```csharp
Page<Catalog> catalogPage = await client.Catalog.GetAsync();
foreach (string repo in catalogPage.Value.RepositoryNames)
{
    Console.WriteLine(repo);
}
```

## Limit results

`count` specifies the requested page size:

```csharp
Page<Catalog> catalogPage = await client.Catalog.GetAsync(count: 50);
```

## Stream every repository

`GetAllAsync` returns an `IAsyncEnumerable<string>` spanning all pages:

```csharp
await foreach (string repo in client.Catalog.GetAllAsync(count: 100))
{
    Console.WriteLine(repo);
}
```

Pages are requested as enumeration advances. The optional `CancellationToken`
applies to enumeration and active requests.

## Process complete pages

`GetAllPagesAsync` returns complete pages, retaining their metadata:

```csharp
await foreach (Page<Catalog> page in client.Catalog.GetAllPagesAsync(count: 100))
{
    foreach (string repo in page.Value.RepositoryNames)
    {
        Console.WriteLine(repo);
    }
}
```

`GetNextAsync` accepts a page's `NextPageLink`. A null link marks the final
page:

```csharp
Page<Catalog> page = await client.Catalog.GetAsync(count: 100);

while (true)
{
    foreach (string repo in page.Value.RepositoryNames)
    {
        Console.WriteLine(repo);
    }

    if (page.NextPageLink is null)
    {
        break;
    }

    page = await client.Catalog.GetNextAsync(page.NextPageLink);
}
```

Continuation links are opaque and must remain unchanged.
Continuation links and redirects can point to another server. The credential
provider is not invoked for direct continuation requests to a different origin
(scheme, host, or port). Redirects clear `Authorization`; shared default headers
and custom headers remain caller-controlled. See
[server-issued links](authentication.md#server-issued-links)
for continuation-link and credential behavior.
