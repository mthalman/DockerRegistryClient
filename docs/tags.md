# Tag Operations

Access tag operations via `client.Tags`.

## List tags

```csharp
using RegistryClient client = new("mcr.microsoft.com");
Page<RepositoryTags> tagsPage = await client.Tags.GetAsync("dotnet/sdk");
foreach (string tag in tagsPage.Value.Tags)
{
    Console.WriteLine(tag);
}
```

## Limit results

Pass a `count` parameter to limit the number of tags returned per page:

```csharp
Page<RepositoryTags> tagsPage = await client.Tags.GetAsync("dotnet/sdk", count: 10);
```

## Stream every tag

Use `GetAllAsync` to stream tags across every page:

```csharp
using RegistryClient client = new("mcr.microsoft.com");

await foreach (string tag in client.Tags.GetAllAsync("dotnet/sdk", count: 100))
{
    Console.WriteLine(tag);
}
```

The client requests each page only as the loop advances. Pass a
`CancellationToken` to stop enumeration and any active request.

## Process complete pages

Use `GetAllPagesAsync` when page boundaries or page metadata are needed:

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

For manual pagination, call `GetAsync`, inspect `NextPageLink`, and pass a
non-null link to `GetNextAsync`:

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
