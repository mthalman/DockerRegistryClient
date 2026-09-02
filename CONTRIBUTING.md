# Contributing

Contributions are welcome. Open an issue before making a large or
behavior-changing contribution so that maintainers can confirm the approach.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git
- Docker, when running the live-registry integration tests

The .NET 10 SDK builds all target frameworks: `netstandard2.0`, `net8.0`, and
`net10.0`.

## Build and test

Run these commands from the `src` directory:

```shell
dotnet restore
dotnet build
dotnet test --filter "Category!=Integration"
```

The build succeeds without warnings or errors, and `dotnet test` reports the
unit-test results for `Valleysoft.DockerRegistryClient.Tests` without requiring
Docker.

The integration tests start a temporary, authenticated Docker Registry
container through Testcontainers. Docker must be running; an unavailable Docker
engine fails the test run rather than skipping these tests.

```shell
dotnet test --filter "Category=Integration"
```

To run both the unit and integration tests:

```shell
dotnet test
```

## Versioning and releases

Package and assembly versions are derived from Git tags by
[MinVer](https://github.com/adamralph/minver). Create a tag on the commit to
release and push it to start the release workflow:

- Stable release: `v1.2.3`
- Prerelease: `v1.2.3-preview.1`

The `v` prefix is omitted from the resulting package version. For example,
`v1.2.3` produces `Valleysoft.DockerRegistryClient.1.2.3.nupkg`.

Untagged commits use MinVer's deterministic development version. After a stable
release, MinVer increments the patch version and adds an `alpha.0` prerelease
identifier and the Git commit height, so an untagged build cannot be mistaken
for a stable release.

[Release Drafter](https://github.com/release-drafter/release-drafter) collects
pull requests merged to `main` in an unpublished GitHub Release. Apply one
semantic-version label to each pull request:

| Label | Version change |
| --- | --- |
| `semver:major` | Breaking change |
| `semver:minor` | Backward-compatible feature |
| `semver:patch` | Backward-compatible fix or maintenance |

If none of these labels is present, Release Drafter proposes a patch release.
If more than one is present, the highest version change wins. Apply
`skip-changelog` only to internal changes that should not appear in the release
notes.

Release-note categories use the existing `enhancement` or `type:feature`,
`bug` or `type:bug`, and `documentation` or `type:docs` labels. Use
`dependencies` for dependency updates. Pull requests without a category label
appear under Maintenance.

To cut a release, review the accumulated draft on the GitHub Releases page and
publish it with its proposed `v*` tag. The tag starts the release workflow,
which builds and publishes the NuGet package. GitHub Releases are the permanent
release-note system of record; this repository does not maintain a
`CHANGELOG.md`.

## Submit a change

1. Create a branch from the repository's default branch.
2. Make a focused change and add or update tests when behavior changes.
3. Update the relevant documentation when the public API or behavior changes.
4. Run the build and tests.
5. Open a pull request that explains the problem and the solution.