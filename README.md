# Docker Registry Client

A .NET client library for the [OCI Distribution Spec](https://github.com/opencontainers/distribution-spec) / Docker Registry HTTP API V2.

[![NuGet](https://img.shields.io/nuget/v/Valleysoft.DockerRegistryClient)](https://www.nuget.org/packages/Valleysoft.DockerRegistryClient)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Installation

```
dotnet add package Valleysoft.DockerRegistryClient
```

## Quick Start

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

## API Overview

| Property | Operations | Docs |
|---|---|---|
| `client.Tags` | List tags | [Tags](docs/tags.md) |
| `client.Manifests` | Get, check existence, get digest | [Manifests](docs/manifests.md) |
| `client.Blobs` | Download, upload, check existence, delete | [Blobs](docs/blobs.md) |
| `client.Catalog` | List all repositories | [Catalog](docs/catalog.md) |
| `client.Referrers` | Get referrers by digest, filter by artifact type | [Referrers](docs/referrers.md) |

## Guides

- [Authentication](docs/authentication.md) — Anonymous, basic, token, and custom credentials
- [Error Handling](docs/error-handling.md) — `RegistryException` and error codes
- [Contributing](CONTRIBUTING.md)

## License

This project is licensed under the [MIT License](LICENSE).
