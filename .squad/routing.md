# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, API design, scope decisions | Ripley | Interface design, breaking changes, new operations, trade-offs |
| C# implementation, HTTP operations, registry protocol | Parker | New endpoints, bug fixes, refactoring, OCI spec compliance |
| Code review, PR review | Ripley | Review PRs, check quality, approve/reject changes |
| Testing, quality, edge cases | Lambert | Unit tests, integration tests, mocking HTTP, error scenarios |
| Scope & priorities | Ripley | What to build next, trade-offs, decisions |
| Session logging | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Ripley |
| `squad:ripley` | Architecture, design, review tasks | Ripley |
| `squad:parker` | Implementation and bug fix tasks | Parker |
| `squad:lambert` | Testing and quality tasks | Lambert |

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn Lambert to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. Ripley handles all `squad` (base label) triage.
