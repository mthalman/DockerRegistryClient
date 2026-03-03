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
