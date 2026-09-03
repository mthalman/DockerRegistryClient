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

## Stream every repository

Use `GetAllAsync` to stream repository names across every page:

```csharp
await foreach (string repo in client.Catalog.GetAllAsync(count: 100))
{
    Console.WriteLine(repo);
}
```

The client requests each page only as the loop advances. Pass a
`CancellationToken` to stop enumeration and any active request.

## Process complete pages

Use `GetAllPagesAsync` when page boundaries or page metadata are needed:

```csharp
await foreach (Page<Catalog> page in client.Catalog.GetAllPagesAsync(count: 100))
{
    foreach (string repo in page.Value.RepositoryNames)
    {
        Console.WriteLine(repo);
    }
}
```

For manual pagination, call `GetAsync`, inspect `NextPageLink`, and pass a
non-null link to `GetNextAsync`:

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
