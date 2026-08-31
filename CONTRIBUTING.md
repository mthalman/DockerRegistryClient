# Contributing

Contributions are welcome. Open an issue before making a large or
behavior-changing contribution so that maintainers can confirm the approach.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

The .NET 10 SDK builds all target frameworks: `netstandard2.0`, `net8.0`, and
`net10.0`.

## Build and test

Run these commands from the `src` directory:

```shell
dotnet restore
dotnet build
dotnet test
```

The build succeeds without warnings or errors, and `dotnet test` reports the
test results for `Valleysoft.DockerRegistryClient.Tests`.

## Submit a change

1. Create a branch from the repository's default branch.
2. Make a focused change and add or update tests when behavior changes.
3. Update the relevant documentation when the public API or behavior changes.
4. Run the build and tests.
5. Open a pull request that explains the problem and the solution.