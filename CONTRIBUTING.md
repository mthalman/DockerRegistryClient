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

## Submit a change

1. Create a branch from the repository's default branch.
2. Make a focused change and add or update tests when behavior changes.
3. Update the relevant documentation when the public API or behavior changes.
4. Run the build and tests.
5. Open a pull request that explains the problem and the solution.