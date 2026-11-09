# Tag Operations

Access tag operations via `client.Tags`.

All snippets below use the following setup code:

```csharp
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Models;

using RegistryClient client = new("mcr.microsoft.com");
```

## List tags

`GetAsync` returns one `Page<RepositoryTags>` for a repository.

```csharp
Page<RepositoryTags> tagsPage = await client.Tags.GetAsync("dotnet/sdk");
foreach (string tag in tagsPage.Value.Tags)
{
    Console.WriteLine(tag);
}
```

## Limit results

`count` specifies the requested page size:

```csharp
Page<RepositoryTags> tagsPage = await client.Tags.GetAsync("dotnet/sdk", count: 10);
```

## Stream every tag

`GetAllAsync` returns an `IAsyncEnumerable<string>` spanning all pages:

```csharp
await foreach (string tag in client.Tags.GetAllAsync("dotnet/sdk", count: 100))
{
    Console.WriteLine(tag);
}
```

Pages are requested as enumeration advances. The optional `CancellationToken`
applies to enumeration and active requests.

## Process complete pages

`GetAllPagesAsync` returns complete pages, retaining their metadata:

```csharp
await foreach (Page<RepositoryTags> page in
    client.Tags.GetAllPagesAsync("dotnet/sdk", count: 100))
{
    foreach (string tag in page.Value.Tags)
    {
        Console.WriteLine(tag);
    }
}
```

`GetNextAsync` accepts a page's `NextPageLink`. A null link marks the final
page:

```csharp
Page<RepositoryTags> page = await client.Tags.GetAsync(
    "dotnet/sdk",
    count: 100);

while (true)
{
    foreach (string tag in page.Value.Tags)
    {
        Console.WriteLine(tag);
    }

    if (page.NextPageLink is null)
    {
        break;
    }

    page = await client.Tags.GetNextAsync(page.NextPageLink);
}
```

The `Page<T>` pagination pattern is shared
across [Catalog](catalog.md) and [Referrers](referrers.md) operations.
