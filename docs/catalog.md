# Catalog Operations

Access catalog operations via `client.Catalog`.

## Listing Repositories

```csharp
using RegistryClient client = new("myregistry.example.com", credentials);
Page<Catalog> catalogPage = await client.Catalog.GetAsync();
foreach (string repo in catalogPage.Value.RepositoryNames)
{
    Console.WriteLine(repo);
}
```

## Limiting Results

```csharp
Page<Catalog> catalogPage = await client.Catalog.GetAsync(count: 50);
```

## Pagination

Pagination follows the same `Page<T>` pattern as [Tags](tags.md):

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
