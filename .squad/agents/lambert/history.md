# Project Context

- **Owner:** Matt Thalman
- **Project:** Valleysoft.DockerRegistryClient — a .NET client library for Docker registry HTTP API operations
- **Stack:** C#, .NET 9, NuGet package, Docker Registry HTTP API v2
- **Created:** 2026-03-02T14:28:00Z

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-02: Initial Test Suite Created

Created comprehensive test suite from scratch for DockerRegistryClient:

**Test Project Structure:**
- Location: `src/Valleysoft.DockerRegistryClient.Tests/`
- Framework: xUnit, targeting net10.0 only
- Packages: xUnit 2.9.2, Moq 4.20.72, Microsoft.NET.Test.Sdk 17.12.0
- Added to solution: `src/Valleysoft.DockerRegistryClient.sln`
- InternalsVisibleTo configured for testing internal classes

**MockHttpMessageHandler Pattern:**
- Created `MockHttpMessageHandler.cs` helper class for mocking HTTP requests
- Allows setting up expected requests/responses without real registry
- Supports matching by URI, HTTP method, or custom predicates
- All operations tests use this pattern: `new RegistryClient(registry, null, new HttpClient(mockHandler))`

**Test Coverage (51 tests total):**

*Tier 1 - Pure Logic (no HTTP):*
- `HttpBearerChallengeTests`: Parse valid/invalid challenges, missing fields, null/empty input
- `HttpLinkTests`: TryParse for link headers with various formats and relationships
- `UrlHelperTests`: ApplyCount, Concat with different slash combinations
- `PageTests`: Constructor with/without nextPageLink

*Tier 2 - Core HTTP Handling:*
- `RegistryClientTests`: Constructor, Dispose, SendRequestCoreAsync with success/error/JSON/XML responses
- `RegistryClientTests`: GetPageResult with/without Link header, GetResult for deserialization
- Error handling: JSON errors, XML errors (single and multiple), empty content

*Tier 3 - Operations:*
- `OperationsHelperTests`: HandleNotFoundErrorAsync wrapping 404s vs other errors

*Tier 4 - Auth & Credentials:*
- `BasicAuthenticationCredentialsTests`: Basic auth header encoding
- `TokenCredentialsTests`: Bearer and custom token type headers

**Key Testing Patterns Established:**
1. Mock HTTP responses with `MockHttpMessageHandler` for all HTTP-dependent tests
2. Test both success and error paths for all operations
3. Validate JSON/XML error response parsing
4. Test null/edge cases systematically
5. All async tests properly await (no blocking operations)

**Build & Run:**
- Build: `dotnet build src/Valleysoft.DockerRegistryClient.Tests/`
- Run: `dotnet test src/Valleysoft.DockerRegistryClient.Tests/`
- All 51 tests pass successfully

### 2026-03-03: HTTP Registry Support Tests (Issue #76)

Added comprehensive tests for HTTP/HTTPS scheme handling in `RegistryClient` constructor:

**Test Coverage:**
- Used `[Theory]` with `[InlineData]` to parameterize 6 test cases covering all scheme variations
- Test cases verify both `BaseUri` (includes scheme) and `Registry` (host only) properties
- Scenarios tested:
  1. Explicit `http://` scheme → BaseUri uses HTTP
  2. Explicit `https://` scheme → BaseUri uses HTTPS
  3. No scheme (backward compat) → BaseUri defaults to HTTPS
  4. HTTP with port → BaseUri preserves port in HTTP scheme
  5. HTTPS with port → BaseUri preserves port in HTTPS scheme
  6. No scheme with port → BaseUri defaults to HTTPS with port

**Pattern Observations:**
- Kept existing `Constructor_ValidRegistry_SetsProperties` test unchanged to ensure backward compatibility
- Added new parameterized test `Constructor_WithSchemeVariations_SetsCorrectBaseUriAndRegistry`
- This pattern is efficient: 6 scenarios tested with single method vs 6 separate [Fact] methods
- All assertions verify both BaseUri (full URL with scheme) and Registry (host:port only)

**Test Results:**
- Total tests: 57 (51 existing + 6 new scheme variations)
- All tests pass successfully
- Build time: ~4s, Test execution: ~1.4s

## Completed Work Sessions

📌 Team update (2026-03-03T03:52:30Z): HTTP Registry Support (Issue #76) test coverage completed — 6 new parameterized tests added covering http://, https://, and no-scheme scenarios with ports — decided by Parker
