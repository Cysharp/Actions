[![cysharp actions lint](https://github.com/Cysharp/Actions/actions/workflows/_cysharp-actions-lint.yaml/badge.svg)](https://github.com/Cysharp/Actions/actions/workflows/_cysharp-actions-lint.yaml)

[![Test benchmark-runnable](https://github.com/Cysharp/Actions/actions/workflows/_test-benchmark-runnable.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-benchmark-runnable.yaml)
[![Test check-metas](https://github.com/Cysharp/Actions/actions/workflows/_test-check-metas.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-check-metas.yaml)
[![Test checkout](https://github.com/Cysharp/Actions/actions/workflows/_test-checkout.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-checkout.yaml)
[![Test clean-packagejson-branch](https://github.com/Cysharp/Actions/actions/workflows/_test-clean-packagejson-branch.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-clean-packagejson-branch.yaml)
[![Test create-release](https://github.com/Cysharp/Actions/actions/workflows/_test-create-release.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-create-release.yaml)
[![Test setup-dotnet](https://github.com/Cysharp/Actions/actions/workflows/_test-setup-dotnet.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-setup-dotnet.yaml)
[![Test update-packagejson](https://github.com/Cysharp/Actions/actions/workflows/_test-update-packagejson.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-update-packagejson.yaml)

# Actions

Reusable workflows and composite actions maintained for Cysharp repositories.

This README reflects the current public definitions under `.github/workflows` and `.github/actions`.
Test and maintenance workflows prefixed with `_` are intentionally omitted here.

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
# 📖 Table of Contents

- [Reusable workflows](#reusable-workflows)
  - [Usage examples](#usage-examples)
    - [actions-timeline](#actions-timeline)
    - [benchmark-loader + benchmark-execute](#benchmark-loader--benchmark-execute)
    - [benchmark-cleanup](#benchmark-cleanup)
    - [clean-packagejson-branch](#clean-packagejson-branch)
    - [create-release](#create-release)
    - [dd-event-post](#dd-event-post)
    - [increment-version](#increment-version)
    - [prevent-github-change](#prevent-github-change)
    - [pr-harness](#pr-harness)
    - [stale-issue](#stale-issue)
    - [update-packagejson](#update-packagejson)
    - [validate-release](#validate-release)
- [Composite actions](#composite-actions)
  - [Action examples](#action-examples)
    - [benchmark-progress-comment](#benchmark-progress-comment)
    - [benchmark-runnable](#benchmark-runnable)
    - [checkout](#checkout)
    - [check-metas](#check-metas)
    - [setup-dotnet](#setup-dotnet)
    - [upload-artifact + download-artifact](#upload-artifact--download-artifact)
    - [unity-builder](#unity-builder)
- [Notes](#notes)
- [Release CLI migration](#release-cli-migration)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

## Reusable workflows

| Workflow | Purpose | Key inputs / notes |
| --- | --- | --- |
| `actions-timeline` | Post workflow timing information using `Kesin11/actions-timeline`. | Requires `secrets.github-token`. |
| `benchmark-loader` | Authorize benchmark requests and generate a benchmark execution matrix from a loader config. | Inputs: `benchmark-name-prefix`, `benchmark-config-path`. Outputs: `is-benchmarkable`, `matrix`. |
| `benchmark-execute` | Provision benchmark infrastructure, execute benchmark matrix entries, and update PR/issue progress comments. | Inputs include `benchmark-name`, `benchmark-config-path`, `branch`. Requires the `benchmark` environment and 1Password/Azure secrets. |
| `benchmark-cleanup` | Clean benchmark environments on schedule or on demand. | Inputs: `state`, `try-redeploy`, `no-delete`. Scheduled hourly in this repo. |
| `clean-packagejson-branch` | Delete a temporary branch created by release/update automation. | Only deletes non-default branches created by `github-actions[bot]`. Input: `branch`. |
| `create-release` | Validate a tag, create a GitHub release and optionally upload release assets. | Inputs include `commit-id`, `tag`, `dry-run`, `release-upload`, `release-asset-path`, `download-run-id`. |
| `dd-event-post` | Post an event to Datadog, typically for PR merge notifications. | Inputs include `title`, `text`, `event`, `additional-tags`, `alert-type`. |
| `increment-version` | Increment a semantic version string and expose the computed version. | Inputs: `tag`, `type`, optional `prefix`, `suffix`, `ref`. Output: `version`. |
| `prevent-github-change` | Fail PRs from forks when they modify `.github/**/*.yml` or `.github/**/*.yaml`. | Intended for policy enforcement around GitHub configuration changes. |
| `pr-harness` | Apply shared PR security checks, including protected workflow files, dependency review, and forbidden Unicode scanning. | Trigger on `edited` as well as code-changing PR events so title/body edits are rescanned. |
| `stale-issue` | Mark and close stale issues and PRs using `actions/stale`. | Current defaults: stale after 180 days, close 30 days later. |
| `update-packagejson` | Normalize a release tag, update version-bearing files, optionally run project-specific `dotnet run -- --version {tag}`, and push the result. | Supports `package.json`, `plugin.cfg`, and `Directory.Build.props`. Outputs: `branch-name`, `is-branch-created`, `sha`. |
| `validate-release` | Run the existing release tag validation before version updates or builds, without creating tags or releases. | Input: `tag`. Outputs: original `tag` and `version` with the leading `v` removed. Requires only `contents: read`; see validation limitations below. |

### Usage examples

#### actions-timeline

```yaml
jobs:
  timeline:
    uses: Cysharp/Actions/.github/workflows/actions-timeline.yaml@main
    secrets:
      # actions-timeline.yaml requires this exact secret name.
      github-token: ${{ secrets.GITHUB_TOKEN }}
```

#### benchmark-loader + benchmark-execute

```yaml
jobs:
  loader:
    uses: Cysharp/Actions/.github/workflows/benchmark-loader.yaml@main
    with:
      # Prefix is used to construct benchmark environment names.
      # MagicOnion uses issue/run context to avoid name collisions.
      benchmark-name-prefix: myrepo-pr-${{ github.event.number }}
      # Loader config path consumed by benchmark-loader2matrix.
      benchmark-config-path: .github/benchmark-loader.yaml

  benchmark:
    needs: [loader]
    # loader output is a string ('true'/'false'), compare explicitly.
    if: ${{ needs.loader.outputs.is-benchmarkable == 'true' }}
    strategy:
      fail-fast: false
      # Matrix JSON is produced by benchmark-loader output.
      matrix: ${{ fromJson(needs.loader.outputs.matrix) }}
    uses: Cysharp/Actions/.github/workflows/benchmark-execute.yaml@main
    with:
      # Key names come from your loader config output schema.
      benchmark-name: ${{ matrix.benchmark-name }}
      benchmark-config-path: ${{ matrix.benchmark-config-path }}
      branch: ${{ matrix.branch }}
    # benchmark-execute needs Azure/1Password-related secrets.
    secrets: inherit
```

#### benchmark-cleanup

```yaml
jobs:
  cleanup:
    uses: Cysharp/Actions/.github/workflows/benchmark-cleanup.yaml@main
    with:
      # Failed/Succeeded/All depending on your cleanup policy.
      state: Failed
      # Scheduled runs can optionally redeploy before cleanup.
      try-redeploy: false
      # Keep false for normal cleanup (true means dry-maintenance mode).
      no-delete: false
```

#### clean-packagejson-branch

```yaml
jobs:
  cleanup:
    permissions:
      # Required because the workflow deletes remote branches.
      contents: write
    uses: Cysharp/Actions/.github/workflows/clean-packagejson-branch.yaml@main
    with:
      # Usually pass update-packagejson output branch-name.
      branch: test-release/1.2.3
```

#### create-release

```yaml
jobs:
  create-release:
    uses: Cysharp/Actions/.github/workflows/create-release.yaml@main
    with:
      # Empty means current checked out commit.
      commit-id: ""
      # Raw tag like 1.2.3 (workflow validates/normalizes internally).
      tag: ${{ inputs.tag }}
      # true keeps dry-run behavior and cleanup path.
      dry-run: ${{ inputs.dry-run }}
      # Guard to prevent accidentally releasing older tags.
      require-validation: true
      # If true, release-asset-path must be provided.
      release-upload: true
      release-asset-path: |
        ./MyUnityPackage/MyUnityPackage.unitypackage
      # v{0} -> v1.2.3, {0} -> 1.2.3.
      release-format: v{0}
      # Empty means download artifacts from current run.
      download-run-id: ""
    # Reusable workflow reads org/repo secrets (1Password/NuGet).
    secrets: inherit
```

#### dd-event-post

```yaml
jobs:
  post-dd-event:
    # Typical trigger pattern: only post when PR was actually merged.
    if: ${{ github.event.pull_request.merged == true }}
    uses: Cysharp/Actions/.github/workflows/dd-event-post.yaml@main
    with:
      # Keep consistent with dashboard aggregation keys/tags.
      event: pr-merged
      alert-type: info
    # Required because workflow loads DD_API_KEY via 1Password.
    secrets: inherit
```

#### increment-version

```yaml
jobs:
  new-version:
    uses: Cysharp/Actions/.github/workflows/increment-version.yaml@main
    with:
      # Use default branch when you want to continue development after release.
      ref: ${{ github.event.repository.default_branch }}
      # Released tag to increment from.
      tag: 1.2.3
      # major | minor | patch
      type: patch
      prefix: ""
      # Common post-release convention.
      suffix: -dev
```

#### prevent-github-change

```yaml
on:
  pull_request:
    paths:
      # Run only when GitHub config files are touched.
      - ".github/**/*.yaml"
      - ".github/**/*.yml"

jobs:
  detect:
    # Reusable workflow blocks fork PR changes to .github files.
    uses: Cysharp/Actions/.github/workflows/prevent-github-change.yaml@main
```

#### pr-harness

```yaml
on:
  pull_request:
    # `edited` is required to rescan PR title/body changes.
    types: [opened, synchronize, reopened, edited]

jobs:
  pr-harness:
    permissions:
      contents: read
      pull-requests: read
    uses: Cysharp/Actions/.github/workflows/pr-harness.yaml@main
```

The Unicode check is implemented by the `CysharpActions scan-pr-unicode` CLI command and invoked directly from `pr-harness`; there is no standalone Unicode workflow or composite action. The checked-in Linux binary is updated only by the release workflow. It scans the PR title, PR body, changed file names, and the complete contents of changed `.cs` and `.csx` files in the checked-out working tree. Changed paths are determined by diffing GitHub's test merge commit (`refs/pull/<number>/merge`) against its first parent, the current base branch tip, so only the merge commit and its two parents (`fetch-depth: 2`) are required even after the base branch advances. The checkout must be that merge commit, and its second parent must match the event's PR head. File contents are read from the checked-out merge result. C# source rejects symbolic links, raw Unicode format/default-ignorable characters, controls, forbidden `\uXXXX` / `\UXXXXXXXX` escapes, and non-ASCII spaces regardless of whether they occur in code, comments, strings, or test data. In addition, every tracked `.cs` and `.csx` path is checked for Git symbolic-link mode, including paths not changed by the PR. Other file contents are not scanned. Tests that intentionally need these values should construct them numerically, for example with `char.ConvertFromUtf32(0x200B)`.

#### stale-issue

```yaml
on:
  schedule:
    - cron: "0 0 * * *"

jobs:
  stale:
    permissions:
      # actions/stale needs write perms for labels/comments/close.
      contents: read
      pull-requests: write
      issues: write
    uses: Cysharp/Actions/.github/workflows/stale-issue.yaml@main
```

#### update-packagejson

```yaml
jobs:
  update-packagejson:
    permissions:
      actions: read
      # Required because this workflow can commit/push version updates.
      contents: write
    uses: Cysharp/Actions/.github/workflows/update-packagejson.yaml@main
    with:
      # Keep checkout target explicit (MagicOnion uses github.ref/default branch).
      ref: ${{ github.ref }}
      file-path: |
        # Supported: package.json / plugin.cfg / Directory.Build.props
        ./src/MyUnityProject/package.json
        ./addons/MyPlugin/plugin.cfg
        ./Directory.Build.props
      # Release tag to propagate into version files.
      tag: ${{ inputs.tag }}
      require-validation: true
      # true if your default branch requires GitHub App authentication.
      use-bot-token: false
      # true writes to test-release/{tag} branch instead of target ref.
      dry-run: false
      dotnet-run-path: |
        # Optional hook; workflow always passes: -- --version {tag}
        ./tools/VersionOutput/VersionOutput.csproj
      additional-commit-path: |
        # Explicit allow-list for files generated outside file-path by dotnet-run-path.
        # Files outside file-path and this list are never committed.
        ./tools/VersionOutput/version.txt
```

```yaml
jobs:
  cleanup:
    # Branch cleanup is only needed when dry-run created a temp branch.
    if: ${{ needs.update-packagejson.outputs.is-branch-created == 'true' }}
    needs: [update-packagejson]
    permissions:
      contents: write
    uses: Cysharp/Actions/.github/workflows/clean-packagejson-branch.yaml@main
    with:
      # Output of update-packagejson workflow.
      branch: ${{ needs.update-packagejson.outputs.branch-name }}
```

#### validate-release

```yaml
jobs:
  validate-release:
    permissions:
      contents: read
    uses: Cysharp/Actions/.github/workflows/validate-release.yaml@main
    with:
      tag: ${{ inputs.tag }}

  build:
    needs: [validate-release]
    permissions:
      contents: read
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    steps:
      - uses: Cysharp/Actions/.github/actions/checkout@main
      - uses: Cysharp/Actions/.github/actions/setup-dotnet@main
      - run: dotnet build -c Release -p:Version="$VERSION"
        env:
          VERSION: ${{ needs.validate-release.outputs.version }}
```

This workflow calls the same `validate-tag --require-validation` command used by `update-packagejson` and `create-release`. It exposes `tag` unchanged and maps the CLI's `normalized-tag` output to `version` (for example, `v1.2.3` becomes `1.2.3`). It queries releases in the calling repository with its `GITHUB_TOKEN`; no inherited secrets or write permissions are needed. Validation failures fail the job and block dependent jobs.

It rejects an empty normalized tag and versions older than the latest stable release. It is **not** a strict Git-tag or package-version syntax validator.

## Composite actions

| Action | Purpose | Key inputs / notes |
| --- | --- | --- |
| `benchmark-progress-comment` | Post or update a benchmark progress comment on an issue or PR. | Inputs: `comment`, `state`, `title`, `update`. No-op when the event has no issue number. |
| `benchmark-runnable` | Authorize whether a GitHub user may trigger benchmark execution. | Input: `username`. Output: `authorized`. Current allowlist is maintained in the action itself. |
| `check-metas` | Fail or report when untracked Unity `.meta` files exist. | Inputs: `directory`, optional `exit-on-error`. Output: `meta-exists`. |
| `checkout` | SHA-pinned wrapper around `actions/checkout`. | Mirrors most `actions/checkout` inputs while centralizing the pinned version. |
| `download-artifact` | SHA-pinned wrapper around `actions/download-artifact`. | Supports `name`, `path`, `pattern`, `merge-multiple`, `github-token`, `repository`, `run-id`. |
| `setup-dotnet` | Install one or more .NET SDKs and configure CI-friendly environment variables. | Defaults to .NET `6.0.x` through `10.0.x`. Optional `dotnet-quality`, `skip-env`. |
| `unity-builder` | SHA-pinned wrapper around `game-ci/unity-builder`. | Inputs include `projectPath`, `unityVersion`, `targetPlatform`, `buildMethod`, `customParameters`, `versioning`. |
| `upload-artifact` | SHA-pinned wrapper around `actions/upload-artifact`. | Default `if-no-files-found` is `error`, not `warn`. |

### Action examples

#### benchmark-progress-comment

```yaml
steps:
  # This action posts comments only for issue/PR events with issue.number.
  - uses: Cysharp/Actions/.github/actions/benchmark-progress-comment@main
    with:
      comment: "Benchmark started"
      state: running
      title: "Benchmark"
      # "true" edits latest benchmark comment; "false" appends a new one.
      update: "true"
```

#### benchmark-runnable

```yaml
steps:
  - id: auth
    uses: Cysharp/Actions/.github/actions/benchmark-runnable@main
    with:
      # Usually github.actor
      username: ${{ github.actor }}

  # authorized output is 'true' or 'false'.
  - run: echo "authorized=${{ steps.auth.outputs.authorized }}"
```

#### checkout

```yaml
steps:
  - id: co
    uses: Cysharp/Actions/.github/actions/checkout@main
    with:
      repository: ${{ github.repository }}
      ref: ${{ github.ref_name }}
      # 0 to fetch full history/tags when versioning logic needs it.
      fetch-depth: 0

  # Wrapper exposes the same useful outputs as actions/checkout.
  - run: echo "checked out ${{ steps.co.outputs.ref }} @ ${{ steps.co.outputs.commit }}"
```

#### check-metas

```yaml
steps:
  - uses: Cysharp/Actions/.github/actions/check-metas@main
    with:
      directory: ./sandbox/Sandbox.Unity
      # Keep true in CI to fail immediately on untracked .meta files.
      exit-on-error: "true"
```

#### setup-dotnet

```yaml
steps:
  - uses: Cysharp/Actions/.github/actions/setup-dotnet@main
    with:
      dotnet-version: |
        10.0.x
      # false also sets CI-friendly env vars (telemetry off, etc.).
      skip-env: "false"
```

#### upload-artifact + download-artifact

```yaml
steps:
  - uses: Cysharp/Actions/.github/actions/upload-artifact@main
    with:
      name: my-artifact
      path: ./artifacts/**
      # Default is already error, but explicit value is clearer in docs.
      if-no-files-found: error

  - uses: Cysharp/Actions/.github/actions/download-artifact@main
    with:
      name: my-artifact
      path: ./downloaded
      # false keeps each artifact in its own directory.
      merge-multiple: "false"
```

#### unity-builder

```yaml
steps:
  # UNITY_* env vars must be set from secrets before this step.
  - uses: Cysharp/Actions/.github/actions/unity-builder@main
    with:
      projectPath: ./sandbox/Sandbox.Unity
      unityVersion: "2022.3.62f1"
      targetPlatform: StandaloneLinux64
      buildMethod: PackageExporter.Export
      customParameters: ""
      # Pass-through to game-ci/unity-builder versioning option.
      versioning: None
```

## Notes

- `secure-checkout` and `secure-setup-dotnet` directories currently exist but do not contain action definitions.
- `validate-tag` is implemented by the `CysharpActions` CLI and used internally by reusable workflows such as `create-release` and `update-packagejson`; it is not exposed as a standalone reusable workflow.
- The repo also contains internal test and maintenance workflows such as `_test-*`, `_toc-generator.yaml`, and `_update-actions-binaries.yaml`.

## Release CLI migration

The following commands prepare the next release-workflow migration. The existing
`validate-tag` and `create-release` commands are **deprecated**, but remain available
with their existing behavior until all callers migrate. Their C# entry points are
marked `Obsolete` (warnings, not errors).
The `validate-release.yaml` wrapper still uses `validate-tag` until the updated CLI
binaries have been published and the workflow integration is migrated.

| Command | Contract |
| --- | --- |
| `validate-release --tag v1.2.3` | Validate Git ref syntax and NuGet version syntax/order. Emit original `tag` and `version` without the leading `v`. MagicOnion skips only the ordering check. |
| `validate-package-versions --directory ./nuget --version 1.2.3` | Require at least one nupkg and check all nupkg/snupkg nuspec versions. Use NuGet version normalization and ignore build metadata for identity comparison. No authentication or push. |
| `prepare-release --tag v1.2.3 --release-title v1.2.3 --state-path ./release-state.json` | Check an existing tag against checked-out HEAD (including annotated tags), reject an existing release, create a tag if missing and a draft release, and journal only resources this invocation created. Optional `--release-asset-path-string` uploads assets after journaling. |
| `cleanup-release --state-path ./release-state.json` | Delete the recorded draft by ID and delete an owned tag only with a lease on its recorded object ID. Preserve preexisting tags and refuse changed or published resources. |

Run `validate-release` before version updates/builds and check out the exact build
commit before `prepare-release`. Its syntax/SHA checks do not repeat the latest
release version-order check. GitHub commands use the caller's `GH_REPO` and
`GH_TOKEN`; set `GITHUB_REPOSITORY` consistently for the validation policy.

The journal path must be new for each invocation (for example, a file in
`RUNNER_TEMP` containing the run ID and attempt). Keep it local to the same job;
do not download ownership journals from external artifacts. Cleanup with missing,
corrupt or inconsistent state fails without guessing ownership. Each successful
remote mutation is journaled immediately. If a remote operation succeeds but its
response or journal write is lost, manual recovery may be necessary; an uncertain
resource must not be adopted for automatic deletion.

Use cleanup for dry runs and failures during preparation/asset upload. Leave the
draft, tag and package artifacts intact if the subsequent publication job fails.
Do not combine `prepare-release` with the old workflow's grep-based cleanup.
Read/check/delete of a GitHub draft is not an atomic transaction: avoid manual
publication or editing during preparation/cleanup. Tag deletion uses an explicit
Git lease to reject a concurrent move.

Deployment order: merge the backward-compatible CLI changes, publish and confirm
the generated `actions/Linux-*` binaries on `main`, then migrate the workflows.
Keep duplicate legacy validation during the caller migration; remove it only
after all callers, including maintenance/manual entry points, have migrated.
No validation-skip option is introduced.

After all workflow and direct CLI callers have migrated, remove the legacy
`validate-tag` / `create-release` commands, their obsolete implementations/options,
and legacy-only tests. Preserve shared code still used by the replacements: move
`CreateReleaseCommand.UploadAssetFilesAsync` into a dedicated uploader before
removing that class, and retain the release-query interface/implementation used by
`ValidateReleaseCommand`. Publish the cleaned CLI binaries and update CLI contract
tests, smoke tests and documentation as a separate final migration phase. This
deprecation applies to the CLI commands, not to `create-release.yaml` itself.
