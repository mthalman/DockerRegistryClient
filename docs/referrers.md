# Referrer Operations

Access referrer operations via `client.Referrers`.

The [OCI Referrers API](https://github.com/opencontainers/distribution-spec/blob/main/spec.md#listing-referrers) discovers artifacts that reference a given manifest digest — such as SBOMs, signatures, and attestations.

## Getting Referrers

```csharp
using RegistryClient client = new("myregistry.example.com", credentials);
Page<OciImageIndex> referrersPage = await client.Referrers.GetAsync(
    "myrepo", "sha256:abc123...");

foreach (var manifest in referrersPage.Value.Manifests)
{
    Console.WriteLine($"{manifest.Digest} ({manifest.MediaType})");
}
```

## Filtering by Artifact Type

Pass an `artifactType` to filter results to a specific kind of artifact:

```csharp
Page<OciImageIndex> sboms = await client.Referrers.GetAsync(
    "myrepo", "sha256:abc123...", artifactType: "application/spdx+json");
```

## Pagination

Pagination follows the same `Page<T>` pattern as [Tags](tags.md):

```csharp
Page<OciImageIndex> page = await client.Referrers.GetAsync("myrepo", "sha256:abc123...");

while (true)
{
    foreach (var manifest in page.Value.Manifests)
    {
        Console.WriteLine(manifest.Digest);
    }

    if (page.NextPageLink is null)
    {
        break;
    }

    page = await client.Referrers.GetNextAsync(page.NextPageLink);
}
```
