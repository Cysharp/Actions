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
    - [stale-issue](#stale-issue)
    - [update-packagejson](#update-packagejson)
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

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

## Reusable workflows

| Workflow | Purpose | Key inputs / notes |
| --- | --- | --- |
| `actions-timeline` | Post workflow timing information using `Kesin11/actions-timeline`. | Requires `secrets.github-token`. |
| `benchmark-loader` | Authorize benchmark requests and generate a benchmark execution matrix from a loader config. | Inputs: `benchmark-name-prefix`, `benchmark-config-path`. Outputs: `is-benchmarkable`, `matrix`. |
| `benchmark-execute` | Provision benchmark infrastructure, execute benchmark matrix entries, and update PR/issue progress comments. | Inputs include `benchmark-name`, `benchmark-config-path`, `branch`. Requires the `benchmark` environment and 1Password/Azure secrets. |
| `benchmark-cleanup` | Clean benchmark environments on schedule or on demand. | Inputs: `state`, `try-redeploy`, `no-delete`. Scheduled hourly in this repo. |
| `clean-packagejson-branch` | Delete a temporary branch created by release/update automation. | Only deletes non-default branches created by `github-actions[bot]`. Input: `branch`. |
| `create-release` | Validate a tag, create a GitHub release, optionally push NuGet packages, and optionally upload release assets. | Inputs include `commit-id`, `tag`, `dry-run`, `nuget-push`, `release-upload`, `release-asset-path`, `download-run-id`. Uses 1Password to load `NUGET_KEY` when NuGet push is enabled. |
| `dd-event-post` | Post an event to Datadog, typically for PR merge notifications. | Inputs include `title`, `text`, `event`, `additional-tags`, `alert-type`. |
| `increment-version` | Increment a semantic version string and expose the computed version. | Inputs: `tag`, `type`, optional `prefix`, `suffix`, `ref`. Output: `version`. |
| `prevent-github-change` | Fail PRs from forks when they modify `.github/**/*.yml` or `.github/**/*.yaml`. | Intended for policy enforcement around GitHub configuration changes. |
| `stale-issue` | Mark and close stale issues and PRs using `actions/stale`. | Current defaults: stale after 180 days, close 30 days later. |
| `update-packagejson` | Normalize a release tag, update version-bearing files, optionally run project-specific `dotnet run -- --version {tag}`, and push the result. | Supports `package.json`, `plugin.cfg`, and `Directory.Build.props`. Outputs: `branch-name`, `is-branch-created`, `sha`. |

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
      # If true, NuGet push runs and NUGET_KEY is required.
      nuget-push: false
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
      directory: ./Sandbox/Sandbox.Unity
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
      projectPath: ./Sandbox/Sandbox.Unity
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
