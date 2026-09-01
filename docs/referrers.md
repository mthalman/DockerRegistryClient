# Referrer Operations

Access referrer operations via `client.Referrers`.

The [OCI Referrers API](https://github.com/opencontainers/distribution-spec/blob/main/spec.md#listing-referrers)
discovers artifacts that reference a given manifest digest, such as SBOMs,
signatures, and attestations. If a registry does not implement the referrers
endpoint, the client automatically queries the OCI referrers tag fallback.

## Get referrers

```csharp
using RegistryClient client = new("myregistry.example.com", credentials);
Page<OciImageIndex> referrersPage = await client.Referrers.GetAsync(
    "myrepo", "sha256:abc123...");

foreach (var manifest in referrersPage.Value.Manifests)
{
    Console.WriteLine($"{manifest.Digest} ({manifest.MediaType})");
}
```

## Filter by artifact type

Pass an `artifactType` to filter results to a specific kind of artifact:

```csharp
Page<OciImageIndex> sboms = await client.Referrers.GetAsync(
    "myrepo", "sha256:abc123...", artifactType: "application/spdx+json");
```

## Retrieve every page

When the registry returns another page, `NextPageLink` contains the URL to pass
to `GetNextAsync`:

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
