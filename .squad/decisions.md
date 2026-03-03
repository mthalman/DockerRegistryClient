# Decisions

> Shared decision log. All agents read this before starting work.
> Scribe merges new decisions from `.squad/decisions/inbox/`.

<!-- Decisions will be appended below by Scribe -->

### 2026-03-02T18:26:27Z: User directive
**By:** Matt Thalman (via Copilot)
**What:** net10.0 is the latest LTS — keep net8.0 (previous LTS) and net10.0 (current LTS) as the non-netstandard TFMs.
**Why:** User request — captured for team memory

### 2026-03-03T02:47:00Z: Test Coverage Strategy & Component Priorities
**By:** Ripley (Test Coverage Lead)
**What:** Comprehensive unit test strategy for DockerRegistryClient with zero existing tests. 14 components prioritized into 4 tiers based on complexity and risk. Test framework: xUnit. Target frameworks: net8.0, net10.0. Mock strategy: Custom MockHttpMessageHandler (no heavy mocking frameworks).
**Why:** Establish baseline test coverage for production-ready library. Prioritization ensures critical HTTP and error handling paths are tested first. Estimated 100–120 tests across all components. Strategy includes detailed test cases for each component, edge case handling, and realistic error response testing.
**Outcome:** Ripley analysis delivered; Lambert implementation in progress (51 tests completed and passing).

### 2026-03-03T03:52:30Z: HTTP Registry Support (GitHub Issue #76)
**By:** Parker (Core Dev)
**What:** Modified `RegistryClient` constructor to detect and handle explicit URI schemes (http:// or https://) in the registry parameter, while maintaining backward compatibility for scheme-less inputs that default to https://.
**Why:** GitHub Issue #76: Client couldn't connect to HTTP registries. Many Kubernetes-based registries use HTTP internally with TLS termination at the service mesh. Constructor was hardcoded to prepend https://.
**Implementation:** Added scheme detection (case-insensitive), parses full URI when scheme present, extracts host for Registry property, uses provided scheme in BaseUri, defaults to https:// when absent, ensures BaseUri always has trailing slash.
**Impact:** Backward compatible with scheme-less inputs; new capability for explicit HTTP registries (e.g., `http://registry.local:5000`); Registry property remains as host[:port] format; all 57 existing tests pass without modification.
**Example:** `new RegistryClient("http://registry.local:5000")` → Registry="registry.local:5000", BaseUri="http://registry.local:5000/"
