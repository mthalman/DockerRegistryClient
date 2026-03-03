# Project Context

- **Owner:** Matt Thalman
- **Project:** Valleysoft.DockerRegistryClient — a .NET client library for Docker registry HTTP API operations
- **Stack:** C#, .NET 9, NuGet package, Docker Registry HTTP API v2
- **Created:** 2026-03-02T14:28:00Z

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-02: Test Priority Analysis
Analyzed entire codebase to identify testing priorities. Key findings:
- **Highest risk areas:** RegistryClient error parsing (JSON/XML), OAuthDelegatingHandler OAuth flow, BlobOperations multi-stage upload
- **Critical parsers:** HttpBearerChallenge (regex-based), HttpLink (pagination), Range/UUID headers in BlobOperations
- **Complex dispatch:** ManifestOperations media type switching (4 manifest formats)
- **Platform quirks:** netstandard2.0 vs net5.0+ differences (PATCH method, string slicing, async overloads)
- **Recommended approach:** xUnit + MockHttpMessageHandler, ~100-120 tests total
- Documented priorities and structure in `.squad/decisions/inbox/ripley-test-priorities.md` for Lambert
