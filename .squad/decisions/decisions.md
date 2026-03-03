# Decisions

### Decision: .NET 10 Upgrade

**By:** Parker (Core Dev)  
**Date:** 2026-03-02  

## Context

Upgraded the project from .NET 9 to .NET 10. The target framework changed from `net9.0` to `net10.0`, dropping net9.0 from the multi-target list (netstandard2.0 and net8.0 retained).

## Changes

- `global.json`: SDK version set to `10.0.100`, `rollForward` changed from `latestPatch` to `latestFeature`, `allowPrerelease` set to `true` — necessary because stable 10.0.1xx SDKs on this machine were incomplete installations. The installed working SDK is `10.0.200-preview.0.26103.119`.
- `csproj`: Target frameworks now `netstandard2.0;net8.0;net10.0`. `System.Net.Http.Json` bumped from `9.0.8` to `10.0.3`.

## Notes

- `rollForward` and `allowPrerelease` changes were required to get a working build with available SDKs. Once a stable 10.0.1xx SDK is properly installed, these could be reverted to `latestPatch` / `false`.
- LangVersion left at 12.0 — could be bumped to 14.0 for C# 14 features if desired.
- Build verified: all three target frameworks compile cleanly.
