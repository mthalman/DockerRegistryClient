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

## Stream every referrer

Use `GetAllAsync` to stream manifest references across every page:

```csharp
await foreach (ManifestReference manifest in client.Referrers.GetAllAsync(
    "myrepo",
    "sha256:abc123..."))
{
    Console.WriteLine(manifest.Digest);
}
```

The client requests each page only as the loop advances. Pass a
`CancellationToken` to stop enumeration and any active request.

## Process complete pages

Use `GetAllPagesAsync` when page boundaries or page metadata are needed:

```csharp
await foreach (Page<OciImageIndex> page in client.Referrers.GetAllPagesAsync(
    "myrepo",
    "sha256:abc123..."))
{
    foreach (ManifestReference manifest in page.Value.Manifests)
    {
        Console.WriteLine(manifest.Digest);
    }
}
```

For manual pagination, call `GetAsync`, inspect `NextPageLink`, and pass a
non-null link to `GetNextAsync`:

```csharp
Page<OciImageIndex> page = await client.Referrers.GetAsync(
    "myrepo",
    "sha256:abc123...");

while (true)
{
    foreach (ManifestReference manifest in page.Value.Manifests)
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
