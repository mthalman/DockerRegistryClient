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
