# Contributing

Contributions are welcome. Open an issue before making a large or
behavior-changing contribution so that maintainers can confirm the approach.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git
- Docker, when running the live-registry integration tests

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

## Submit a change

1. Create a branch from the repository's default branch.
2. Make a focused change and add or update tests when behavior changes.
3. Update the relevant documentation when the public API or behavior changes.
4. Run the build and tests.
5. Open a pull request that explains the problem and the solution.

## Document breaking changes

Follow the pinned [release-automation author guide](https://github.com/mthalman/release-automation/blob/90551757fe8b061d4dff1a4cab12f10e58f07201/docs/author-guide.md)
and the [labeling rules](MAINTAINERS.md#label-pull-requests). The workflows use
the toolkit's default labels and paths.

For each breaking change, add a new file named
`.changes/+short-kebab-slug.breaking.md` and apply `semver:major`.
Use the [fragment template](https://github.com/mthalman/release-automation/blob/90551757fe8b061d4dff1a4cab12f10e58f07201/docs/fragment-template.md):
one H3 title followed by these H4 sections, in order:

- Previous behavior
- New behavior
- Type of breaking change
- Reason for change
- Recommended action
- Affected APIs

Each section needs concrete guidance, not placeholders. Never combine
`semver:major` with `skip-changelog`. Retain fragments after publication; a new
breaking change requires a new fragment, not an edit to an existing one.
Non-breaking changes do not need fragments.

Repository maintainers should follow the [maintainer guide](MAINTAINERS.md) for
pull-request labeling and releases.