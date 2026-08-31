# Catalog Operations

Access catalog operations via `client.Catalog`.

## List repositories

```csharp
using RegistryClient client = new("myregistry.example.com", credentials);
Page<Catalog> catalogPage = await client.Catalog.GetAsync();
foreach (string repo in catalogPage.Value.RepositoryNames)
{
    Console.WriteLine(repo);
}
```

## Limit results

```csharp
Page<Catalog> catalogPage = await client.Catalog.GetAsync(count: 50);
```

## Retrieve every page

When the registry returns another page, `NextPageLink` contains the URL to pass
to `GetNextAsync`:

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
