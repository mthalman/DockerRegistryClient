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

## Collect unit-test coverage

From `src`, build in Release mode and run the same coverage command as CI:

```shell
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-restore --no-build --filter "Category!=Integration" --collect:"XPlat Code Coverage" --settings coverage.runsettings --logger "trx;LogFileName=unit-tests.trx" --results-directory TestResults/unit
```

`coverage.runsettings` selects Cobertura output for the production assembly only.
The report is written to `TestResults/unit/<run-id>/coverage.cobertura.xml`;
test results are written to `TestResults/unit/unit-tests.trx`. Use an empty
results directory for each measurement to avoid mixing reports from different
runs.

CI runs the build and unit tests separately from the integration-test job, so a
unit-test failure does not prevent integration-test execution. Each job uploads
its TRX results, including on fork pull requests where test-report checks cannot
be created. Integration tests do not contribute to the coverage report.

Download the unit-test coverage artifact from the workflow run for the Cobertura
report and `coverage-summary.md`. The workflow summary records line and branch
coverage counts and percentages, the tested revision, runner OS, and test outcome.
Artifacts are retained for 14 days. Available reports are also uploaded after
test failures, but only successful runs establish a coverage baseline.

No coverage threshold is enforced. Use successful CI measurements to establish
an observed repository baseline before proposing a gate.

## Submit a change

1. Create a branch from the repository's default branch.
2. Make a focused change and add or update tests when behavior changes.
3. Update the relevant documentation when the public API or behavior changes.
4. Run the build and tests.
5. Open a pull request that explains the problem and the solution.

Repository maintainers should follow the [maintainer guide](MAINTAINERS.md) for
pull-request labeling and releases.