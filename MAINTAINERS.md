# Maintainer guide

This guide describes how maintainers review changes and publish releases.

## Label pull requests

Apply exactly one semantic-version label based on the highest-impact public
change:

- `semver:major` for breaking public API or behavior
- `semver:minor` for backward-compatible public functionality
- `semver:patch` for fixes, documentation, dependencies, tests, build changes,
  or maintenance

Apply at most one release-note category:

- `enhancement` for features
- `bug` for fixes
- `documentation` for documentation-only changes
- `dependencies` for dependency updates
- No category for maintenance, refactoring, tests, or infrastructure

For mixed pull requests, classify by the highest-impact public change. For
example, a test-heavy pull request that fixes a product bug is a `bug`. A
dependency pull request that updates production and tooling dependencies remains
a `dependencies` change.

Apply `skip-changelog` to internal-only test dependency updates, CI action
updates, build or tooling changes, and repository administration that package
users do not need to know about. Do not apply `skip-changelog` to production
dependency updates, user-facing fixes, features, documentation, or significant
release behavior.

[Release Drafter](https://github.com/release-drafter/release-drafter) uses these
labels to organize the unpublished release and select its next version. If a
pull request has no semantic-version label, Release Drafter proposes a patch
release.

## Understand package versions

[MinVer](https://github.com/adamralph/minver) derives package and assembly
versions from Git tags:

- Stable release: `v1.2.3`
- Prerelease: `v1.2.3-preview.1`

MinVer removes the `v` prefix from the package version. For example, `v1.2.3`
produces `Valleysoft.DockerRegistryClient.1.2.3.nupkg`.

Untagged commits use MinVer's deterministic development version. After a stable
release, MinVer increments the patch version and adds an `alpha.0` prerelease
identifier and the Git commit height.

## Publish a release

Before publishing, confirm that the Release Drafter draft contains the intended
changes and proposes the correct version.

1. Create the proposed tag on the commit to release:

   ```shell
   git tag v1.2.3
   ```

2. Push the tag:

   ```shell
   git push origin v1.2.3
   ```

3. If GitHub requests approval, approve the deployment to the protected
   `nuget.org` environment.

4. Confirm that the workflow publishes the same version to NuGet.org and GitHub
   Releases and attaches the package to the GitHub Release.

The workflow builds and tests the tagged commit before publication. It fails if
the tag does not exactly match the MinVer package version. Tags with a
prerelease suffix produce prerelease packages and prerelease GitHub Releases.

GitHub Releases are the release-note system of record. The repository does not
maintain a `CHANGELOG.md`.

## Configure trusted publishing

Complete this one-time setup before the first release.

1. Create a protected GitHub Actions environment named `nuget.org`.

2. Configure required reviewers or other deployment protection rules for the
   environment.

3. Add a Trusted Publishing policy to the NuGet.org account `thalman` with these
   values:

   | Setting | Value |
   | --- | --- |
   | Repository owner | `mthalman` |
   | Repository | `DockerRegistryClient` |
   | Workflow file | `release.yml` |
   | Environment | `nuget.org` |

The environment name in NuGet.org must exactly match the GitHub environment.
The workflow exchanges its GitHub OIDC token for a short-lived NuGet API key, so
the repository does not need a `NUGET_ORG_API_KEY` secret.
