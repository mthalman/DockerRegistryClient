# Project Context

- **Owner:** Matt Thalman
- **Project:** Valleysoft.DockerRegistryClient — a .NET client library for Docker registry HTTP API operations
- **Stack:** C#, .NET 9, NuGet package, Docker Registry HTTP API v2
- **Created:** 2026-03-02T14:28:00Z

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- Project multi-targets: netstandard2.0, net8.0, and net10.0 (previously net9.0)
- Solution file is at `src/Valleysoft.DockerRegistryClient.sln` — must specify it for dotnet CLI commands from repo root
- global.json uses `rollForward: latestFeature` and `allowPrerelease: true` to accommodate available SDK versions
- System.Net.Http.Json is the only external NuGet dependency (currently 10.0.3)
- RegistryClient constructor now supports both http:// and https:// schemes in the registry parameter (GitHub Issue #76). When a scheme is provided, it parses the full URI and extracts the host for the Registry property while preserving the scheme in BaseUri. When no scheme is provided, it defaults to https:// for backward compatibility. BaseUri always has a trailing slash for consistent URL building.

## Completed Work Sessions

📌 Team update (2026-03-03T03:52:30Z): HTTP Registry Support (Issue #76) implemented and tested — 57 tests passing, backward compatible, new http:// capability enabled — decided by Parker
