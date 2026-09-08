# Docker Registry Client

A .NET client library for the [OCI Distribution Spec](https://github.com/opencontainers/distribution-spec) / Docker Registry HTTP API V2.

[![NuGet](https://img.shields.io/nuget/v/Valleysoft.DockerRegistryClient)](https://www.nuget.org/packages/Valleysoft.DockerRegistryClient)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/mthalman/DockerRegistryClient/blob/main/LICENSE)

## Install the package

```shell
dotnet add package Valleysoft.DockerRegistryClient
```

The package provides assets for `netstandard2.0`, `net8.0`, and `net10.0`.

## List repository tags

```csharp
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Models;

using RegistryClient client = new("mcr.microsoft.com");
Page<RepositoryTags> tagsPage = await client.Tags.GetAsync("dotnet/sdk");
foreach (string tag in tagsPage.Value.Tags)
{
    Console.WriteLine(tag);
}
```

`RegistryClient` uses HTTPS when the registry name does not include a scheme. Pass
a complete URI, such as `http://localhost:5000`, when the registry uses HTTP or
a nondefault scheme.

All operation methods accept an optional `CancellationToken`.

## API overview

| Property | Operations | Docs |
| --- | --- | --- |
| `client.Tags` | List tags | [Tags](https://github.com/mthalman/DockerRegistryClient/blob/main/docs/tags.md) |
| `client.Manifests` | Get, publish, delete, check existence, get digest | [Manifests](https://github.com/mthalman/DockerRegistryClient/blob/main/docs/manifests.md) |
| `client.Blobs` | Download, deserialize image configuration, upload, check existence, delete | [Blobs](https://github.com/mthalman/DockerRegistryClient/blob/main/docs/blobs.md) |
| `client.Catalog` | List repositories | [Catalog](https://github.com/mthalman/DockerRegistryClient/blob/main/docs/catalog.md) |
| `client.Referrers` | Get referrers by digest, filter by artifact type | [Referrers](https://github.com/mthalman/DockerRegistryClient/blob/main/docs/referrers.md) |

## Guides

- [Copy images and artifacts](https://github.com/mthalman/DockerRegistryClient/blob/main/docs/copy.md) - Copy complete content graphs within or between registries
- [Authentication](https://github.com/mthalman/DockerRegistryClient/blob/main/docs/authentication.md) - Configure anonymous, basic, token, or custom credentials
- [Error handling](https://github.com/mthalman/DockerRegistryClient/blob/main/docs/error-handling.md) - Handle `RegistryException` and registry error details
- [Contributing](https://github.com/mthalman/DockerRegistryClient/blob/main/CONTRIBUTING.md)
- [Maintainer guide](https://github.com/mthalman/DockerRegistryClient/blob/main/MAINTAINERS.md) - Review, label, and release changes

## License

This project is licensed under the [MIT License](https://github.com/mthalman/DockerRegistryClient/blob/main/LICENSE).
