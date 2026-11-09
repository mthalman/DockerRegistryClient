# Referrer Operations

Access referrer operations via `client.Referrers`.

The [OCI Referrers API](https://github.com/opencontainers/distribution-spec/blob/main/spec.md#listing-referrers)
discovers artifacts that reference a given manifest digest, such as SBOMs,
signatures, and attestations. When the initial referrers request returns
`404 Not Found`, the client queries the OCI referrers tag fallback. Other
unsuccessful responses propagate rather than triggering that fallback.
A missing fallback tag or invalid fallback index produces an empty page with
no continuation link.

All snippets below use the following setup code:

```csharp
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

BasicAuthenticationCredentials credentials = new("username", "password");
using RegistryClient client = new("myregistry.example.com", credentials);
string subjectDigest = await client.Manifests.GetDigestAsync("myrepo", "latest");
```

`subjectDigest` identifies the subject manifest, not a blob.

## Get referrers

`GetAsync` returns a `Page<OciImageIndex>` of descriptors referencing the
subject digest.

```csharp
Page<OciImageIndex> referrersPage = await client.Referrers.GetAsync(
    "myrepo", subjectDigest);

foreach (var manifest in referrersPage.Value.Manifests)
{
    Console.WriteLine($"{manifest.Digest} ({manifest.MediaType})");
}
```

## Filter by artifact type

`artifactType` filters the returned descriptors by artifact media type:

```csharp
Page<OciImageIndex> sboms = await client.Referrers.GetAsync(
    "myrepo", subjectDigest, artifactType: "application/spdx+json");
```

## Stream every referrer

`GetAllAsync` returns an `IAsyncEnumerable<ManifestReference>` spanning all
pages:

```csharp
await foreach (ManifestReference manifest in client.Referrers.GetAllAsync(
    "myrepo",
    subjectDigest))
{
    Console.WriteLine(manifest.Digest);
}
```

Pages are requested as enumeration advances. The optional `CancellationToken`
applies to enumeration and active requests.

## Process complete pages

`GetAllPagesAsync` returns complete pages, retaining their metadata:

```csharp
await foreach (Page<OciImageIndex> page in client.Referrers.GetAllPagesAsync(
    "myrepo",
    subjectDigest))
{
    foreach (ManifestReference manifest in page.Value.Manifests)
    {
        Console.WriteLine(manifest.Digest);
    }
}
```

`GetNextAsync` accepts a page's `NextPageLink`. A null link marks the final
page:

```csharp
Page<OciImageIndex> page = await client.Referrers.GetAsync(
    "myrepo",
    subjectDigest);

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
