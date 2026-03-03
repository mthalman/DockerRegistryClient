# Tag Operations

Access tag operations via `client.Tags`.

## Listing Tags

```csharp
using RegistryClient client = new("mcr.microsoft.com");
Page<RepositoryTags> tagsPage = await client.Tags.GetAsync("dotnet/sdk");
foreach (string tag in tagsPage.Value.Tags)
{
    Console.WriteLine(tag);
}
```

## Limiting Results

Pass a `count` parameter to limit the number of tags returned per page:

```csharp
Page<RepositoryTags> tagsPage = await client.Tags.GetAsync("dotnet/sdk", count: 10);
```

## Pagination

Results are returned as `Page<RepositoryTags>`. When more results are available, `NextPageLink` is non-null. Use `GetNextAsync` to retrieve subsequent pages:

```csharp
using RegistryClient client = new("mcr.microsoft.com");

Page<RepositoryTags> page = await client.Tags.GetAsync("dotnet/sdk", count: 100);

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

The `Page<T>` pagination pattern is shared across [Catalog](catalog.md) and [Referrers](referrers.md) operations.
