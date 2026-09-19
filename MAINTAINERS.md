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

The shared [release automation](https://github.com/mthalman/release-automation/tree/90551757fe8b061d4dff1a4cab12f10e58f07201)
uses these labels to organize the unpublished release and select its next
version. The workflows pin v1.0.1 and use its default configuration.
`semver:major` changes appear under Breaking Changes and require a new
[migration fragment](CONTRIBUTING.md#document-breaking-changes).
Never combine `semver:major` and `skip-changelog`.

Policy enforces the breaking-fragment requirement and the major/skip conflict,
not the general label counts. Review labels yourself: missing version labels
fall back to patch, conflicting version labels select the largest bump, and
`skip-changelog` excludes a PR from both notes and version resolution.

## Understand package versions

[MinVer](https://github.com/adamralph/minver) derives package and assembly
versions from Git tags. The release workflow accepts only stable tags in the
exact form `vMAJOR.MINOR.PATCH`, such as `v1.2.3`. Prerelease tags such as
`v1.2.3-preview.1` are rejected before build or publication.

MinVer removes the `v` prefix from the package version. For example, `v1.2.3`
produces `Valleysoft.DockerRegistryClient.1.2.3.nupkg`.

Untagged commits use MinVer's deterministic development version. After a stable
release, MinVer increments the patch version and adds an `alpha.0` prerelease
identifier and the Git commit height.

## Publish a release

Complete the [repository setup](#configure-release-automation) and
[trusted publishing setup](#configure-trusted-publishing) first.
Release drafting never publishes packages or creates tags.

1. Confirm that **Release draft** succeeded on the default branch. If the run
   creates a migration-guide PR, a human must mark it ready for review to start
   CI, review it, and merge it. Automated updates return it to draft, requiring
   another human readiness action. A run that fails at **Wait for merged
   migration guides** leaves the old release draft unchanged; rerun after merge
   if no new draft run starts automatically.

2. Inspect the prepared draft:

   ```shell
   gh api repos/mthalman/DockerRegistryClient/releases --paginate --jq '.[] | select(.draft) | {id, tag_name, target_commitish, html_url}'
   ```

   Expect exactly one draft. Review its version-only title, notes, migration
   links, and source commit. Preserve its preparation metadata and migration
   links when editing prose. A successful draft run is required on first
   adoption and after toolkit upgrades; an older draft is not ready to publish.

3. Create the draft's exact tag at its full `target_commitish` SHA, not at
   whichever commit happens to be checked out:

   ```shell
   git fetch origin main --tags
   git tag v1.2.3 <prepared-commit-sha>
   ```

   Substitute the draft's actual tag and commit in these commands.

4. Push that new tag as a human:

   ```shell
   git push origin v1.2.3
   ```

5. If GitHub requests approval, approve the deployment to the protected
   `nuget.org` environment.

6. Confirm that **Release** publishes the same version to NuGet.org and GitHub
   Releases, with the package assets attached and reviewed release notes intact.

The workflow prepares the release before building and testing the validated
tagged commit. It checks that the MinVer package matches the prepared version.
After NuGet publication and asset upload succeed, finalization rechecks the
prepared context and publishes the existing GitHub Release without rewriting
its notes. The entire workflow shares the toolkit's `release-drafter` concurrency
queue, including time waiting for environment approval.

Rerun the original tag-creation workflow after investigating failures; do not
move, delete, or recreate a tag to bypass validation. An already-published
prepared release skips build and publication on rerun. Partial publication has
no rollback: NuGet push uses `--skip-duplicate`, and asset uploads use
`--clobber`. Inspect NuGet and GitHub state before retrying. Preparation and
finalization are not atomic with external publication or other GitHub writers.
See the pinned [publication guide](https://github.com/mthalman/release-automation/blob/90551757fe8b061d4dff1a4cab12f10e58f07201/docs/tag-publishing.md)
for provenance checks, race limitations, and recovery.

GitHub Releases are the release-note system of record. The repository does not
maintain a `CHANGELOG.md`.

## Configure release automation

1. In **Settings > Actions > General**, allow the shared workflows and their
   pinned actions. Enable **Allow GitHub Actions to create and approve pull
   requests**. The toolkit creates draft documentation PRs; it does not approve
   or merge them.
2. Ensure all eight default labels exist: `semver:major`, `semver:minor`,
   `semver:patch`, `skip-changelog`, `enhancement`, `bug`, `documentation`, and
   `dependencies`.
3. Merge the caller workflows through normal review. Exercise migration policy
   with a test PR, including rejection of a major change without a fragment and
   of a major/skip conflict. Discover the actual nested **Validate migration
   notes** check name from a deployed run before requiring it in a ruleset.
4. Run **Release draft**, review any generated migration-guide PR, and confirm a
   successful post-merge draft run. The generated guides and state must be
   committed exactly; do not hand-author preparation metadata or edit the
   automation branch. Before the first new-system release, check that breaking
   changes merged before onboarding have the migration guidance users need.
5. Exercise publication and rerun behavior in a test repository before relying
   on it here. Confirm draft visibility, environment approvals, and shared queue
   behavior. Committed workflow files do not establish live deployment success.

The callers use `GITHUB_TOKEN` without an external secret. Publication jobs
need draft visibility and `contents: write`; only the NuGet publication job
gets `id-token: write`. If the default branch advances with workflow changes
after preparation, GitHub may require Workflows write authorization to publish
the older target, which `GITHUB_TOKEN` cannot receive. Investigate 403/404 errors
instead of bypassing checks. Any additional narrowly scoped credential needs
maintainer approval; see the shared
[credential requirements](https://github.com/mthalman/release-automation/blob/90551757fe8b061d4dff1a4cab12f10e58f07201/docs/tag-publishing.md#choose-credentials-and-verify-draft-visibility).

## Upgrade release automation

Renovate groups the shared workflow and Action pins. Review all four
entrypoints together: migration policy, release drafting, prepare, and finalize.
Verify the published stable tag resolves to the proposed full commit SHA and
keep all refs and version comments identical. Update pinned documentation links
in `AGENTS.md`, `CONTRIBUTING.md`, and this guide in the same change; Renovate
does not synchronize those links.

Keep workflow-level `release-drafter` concurrency with `queue: max` and
`cancel-in-progress: false` on the tag workflow. Do not add that group to the
draft caller; the reusable callee already owns it. Follow the pinned
[upgrade guide](https://github.com/mthalman/release-automation/blob/90551757fe8b061d4dff1a4cab12f10e58f07201/docs/upgrading.md)
and refresh the draft successfully before tagging.

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
