[![actionlint cysharp](https://github.com/Cysharp/Actions/actions/workflows/_actionlint-cysharp.yaml/badge.svg)](https://github.com/Cysharp/Actions/actions/workflows/_actionlint-cysharp.yaml)

[![Test benchmark scripts](https://github.com/Cysharp/Actions/actions/workflows/_test-benchmark_scripts.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-benchmark_scripts.yaml)
[![Test benchmark-runnable](https://github.com/Cysharp/Actions/actions/workflows/_test-benchmark-runnable.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-benchmark-runnable.yaml)
[![Test check-metas](https://github.com/Cysharp/Actions/actions/workflows/_test-check-metas.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-check-metas.yaml)
[![Test checkout](https://github.com/Cysharp/Actions/actions/workflows/_test-checkout.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-checkout.yaml)
[![Test clean-packagejson-branch](https://github.com/Cysharp/Actions/actions/workflows/_test-clean-packagejson-branch.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-clean-packagejson-branch.yaml)
[![Test create-release](https://github.com/Cysharp/Actions/actions/workflows/_test-create-release.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-create-release.yaml)
[![Test setup-dotnet](https://github.com/Cysharp/Actions/actions/workflows/_test-setup-dotnet.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-setup-dotnet.yaml)
[![Test update-packagejson](https://github.com/Cysharp/Actions/actions/workflows/_test-update-packagejson.yaml/badge.svg?event=pull_request)](https://github.com/Cysharp/Actions/actions/workflows/_test-update-packagejson.yaml)

# Actions

Cysharp OSS repository uses and maintained GitHub Actions "reusable workflows" and "composite actions".

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
# 📖 Table of Contents

- [♻️ Reusable workflows](#-reusable-workflows)
  - [clean-packagejson-branch](#clean-packagejson-branch)
  - [create-release](#create-release)
  - [dd-event-post](#dd-event-post)
  - [increment-version](#increment-version)
  - [prevent-github-change](#prevent-github-change)
  - [stale-issue](#stale-issue)
  - [update-packagejson](#update-packagejson)
  - [validate-tag](#validate-tag)
- [🎬 Actions](#-actions)
  - [check-benchmarkable](#check-benchmarkable)
  - [check-metas](#check-metas)
  - [checkout](#checkout)
  - [download-artifact](#download-artifact)
  - [setup-dotnet](#setup-dotnet)
  - [unity-builder](#unity-builder)
  - [upload-artifact](#upload-artifact)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

# ♻️ Reusable workflows

## clean-packagejson-branch

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/clean-packagejson-branch.yaml)

Delete specic github branch. Mainly used for cleanup branch created by [update-packagejson](#update-packagejson) workflow. Action has following limitation to prevent accidental deletion.

1. Branch is NOT default branch.
2. Branch is created & commited by github-actions[bot].

**Sample usage**

```yaml
name: Build-Release

on:
  workflow_dispatch:

jobs:
  cleanup:
    permissions:
      contents: write
    uses: Cysharp/Actions/.github/workflows/clean-packagejson-branch.yaml@main
    with:
      branch: branch_name_to_delete
```

## create-release

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/create-release.yaml)

Create GitHub Release, upload NuGet and upload artifact to release assets. Mainly used for NuGet and Unity release workflow.

Required secrets.

| SecretKey | When | Description |
| ---- | ---- | ---- |
| `NUGET_KEY` | `with.nuget-push` is true | This secret is required to push nupkg, snupkg to NuGet.org |

**Sample usage**

Create release only.

```yaml
name: Build-Release

on:
  workflow_dispatch:
    inputs:
      tag:
        description: "tag: git tag you want create. (sample 1.0.0)"
        required: true
      dry-run:
        description: "dry_run: true will never create release/nuget."
        required: true
        default: false
        type: boolean

jobs:
  create-release:
    uses: Cysharp/Actions/.github/workflows/create-release.yaml@main
    with:
      commit-id: ''
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }} # if true, delete tag after Release creation & 60s later.
      nuget-push: false
      release-upload: false
    secrets: inherit
```

Change release name not to use `Ver.` prefix.

```yaml
name: Build-Release

on:
  workflow_dispatch:
    inputs:
      tag:
        description: "tag: git tag you want create. (sample 1.0.0)"
        required: true
      dry-run:
        description: "dry_run: true will never create release/nuget."
        required: true
        default: false
        type: boolean

jobs:
  create-release:
    uses: Cysharp/Actions/.github/workflows/create-release.yaml@main
    with:
      commit-id: ''
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }} # if true, delete tag after Release creation & 60s later.
      nuget-push: false
      release-upload: false
      release-format: '{0}'
    secrets: inherit
```

Build .NET then create release. `create-release` will push nuget packages.

```yaml
name: Build-Release

on:
  workflow_dispatch:
    inputs:
      tag:
        description: "tag: git tag you want create. (sample 1.0.0)"
        required: true
      dry-run:
        description: "dry_run: true will never create release/nuget."
        required: true
        default: false
        type: boolean

jobs:
  build-dotnet:
    runs-on: ubuntu-24.04
    timeout-minutes: 3
    defaults:
      run:
        working-directory: ./Sandbox
    steps:
      - uses: actions/checkout@v4
      - uses: Cysharp/Actions/.github/actions/setup-dotnet@main
      - run: dotnet build -c Release -p:Version=${{ inputs.tag }}
      - run: dotnet pack --no-build -c Release -p:Version=${{ inputs.tag }} -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -o ./publish
      - name: upload artifacts
        uses: Cysharp/Actions/.github/actionsupload-artifact@main
        with:
          name: nuget
          path: ./Sandbox/publish
          retention-days: 1

  create-release:
    needs: [build-dotnet]
    uses: Cysharp/Actions/.github/workflows/create-release.yaml@main
    with:
      commit-id: ''
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }} # if true, delete tag after Release creation & 60s later.
      nuget-push: true
      release-upload: false
    secrets: inherit                 # to allow workflow to access NUGET_KEY secret
```

Build .NET and Unity, then create release. `create-release` will push nuget packages and upload unitypackage to release assets.

```yaml
name: Build-Release

on:
  workflow_dispatch:
    inputs:
      tag:
        description: "tag: git tag you want create. (sample 1.0.0)"
        required: true
      dry-run:
        description: "dry_run: true will never create release/nuget."
        required: true
        default: false
        type: boolean

jobs:
  update-packagejson:
    if: ${{ github.actor != 'dependabot[bot]' }}
    permissions:
      actions: read
      contents: write
    uses: Cysharp/Actions/.github/workflows/update-packagejson.yaml@main
    with:
      file-path: |
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo/package.json
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo.Plugin/package.json
        ./Sandbox/Sandbox.Godot/addons/Foo/plugin.cfg
        ./Sandbox/Directory.Build.props
      tag: ${{ inputs.tag }}
      dry-run: false

  build-dotnet:
    runs-on: ubuntu-24.04
    timeout-minutes: 3
    defaults:
      run:
        working-directory: ./Sandbox
    steps:
      - uses: actions/checkout@v4
      - uses: Cysharp/Actions/.github/actions/setup-dotnet@main
      - run: dotnet build -c Release -p:Version=${{ inputs.tag }}
      - run: dotnet pack --no-build -c Release -p:Version=${{ inputs.tag }} -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -o ./publish
      - name: upload artifacts
        uses: Cysharp/Actions/.github/actionsupload-artifact@main
        with:
          name: nuget
          path: ./Sandbox/publish
          retention-days: 1

  build-unity:
    needs: [update-packagejson]
    runs-on: ubuntu-24.04
    timeout-minutes: 15
    steps:
      - run: echo ${{ needs.update-packagejson.outputs.sha }}
      - uses: actions/checkout@v4
        with:
          ref: ${{ needs.update-packagejson.outputs.sha }}
      # Store artifacts.
      - uses: Cysharp/Actions/.github/actions/upload-artifact@main
        with:
          name: Sandbox.Unity.unitypackage
          path: ./Sandbox/Sandbox.Unity/output/Sandbox.Unity.unitypackage
          if-no-files-found: error
      - uses: Cysharp/Actions/.github/actions/upload-artifact@main
        with:
          name: Sandbox.Unity.Plugin.unitypackage
          path: ./Sandbox/Sandbox.Unity/output/Sandbox.Unity.Plugin.unitypackage
          if-no-files-found: error

  create-release:
    needs: [update-packagejson, build-dotnet, build-unity]
    uses: Cysharp/Actions/.github/workflows/create-release.yaml@main
    with:
      commit-id: ${{ needs.update-packagejson.outputs.sha }}
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }} # if true, delete tag after Release creation & 60s later.
      nuget-push: true
      release-upload: true
      release-asset-path: |
        ./Sandbox.Unity.unitypackage/Sandbox.Unity.unitypackage
        ./Sandbox.Unity.Plugin.unitypackage/Sandbox.Unity.Plugin.unitypackage
        ./nuget/ClassLibrary.${{ inputs.tag }}.nupkg
        ./nuget/ClassLibrary.${{ inputs.tag }}.snupkg
    secrets: inherit                 # to allow workflow to access NUGET_KEY secret

  cleanup:
    if: ${{ needs.update-packagejson.outputs.is-branch-created == 'true' }}
    needs: [update-packagejson]
    permissions:
      contents: write
    uses: Cysharp/Actions/.github/workflows/clean-packagejson-branch.yaml@main
    with:
      branch: ${{ needs.update-packagejson.outputs.branch-name }}
```


## dd-event-post

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/dd-event-post.yaml)

Post Datadog event.

1. Use for Pull Request Merge event.

**Sample usage**

```yaml
name: PR Merged

on:
  pull_request:
    types: [closed]

jobs:
  post:
    if: ${{ github.event.pull_request.merged == true }}
    uses: Cysharp/Actions/.github/workflows/dd-event-post.yaml@main
    secrets: inherit
```

## increment-version

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/increment-version.yaml)

Update specified version file with incremented version. Mainly used for [post-release workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/post-release.yaml).

**Sample usage**

Following workflow will increment patch version from released tag and update specified package.json, plugin.cfg and Directory.Build.props files with new version with `-dev` suffix.

```yaml
name: Post Release

on:
  release:
    types: [published]

jobs:
  new-version:
    permissions:
      actions: read
      contents: read
    uses: Cysharp/Actions/.github/workflows/increment-version.yaml@main
    with:
      ref: ${{ github.event.repository.default_branch }}
      tag: ${{ github.ref_name }} # tag value will here. 1.2.1
      type: patch
      suffix: "-dev"

  update-packagejson:
    needs: [new-version]
    permissions:
      actions: read
      contents: write
    uses: Cysharp/Actions/.github/workflows/update-packagejson.yaml@main
    with:
      ref: ${{ github.event.repository.default_branch }}
      file-path: |
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo/package.json
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo.Plugin/package.json
        ./Sandbox/Sandbox.Godot/addons/Foo/plugin.cfg
        ./Sandbox/Directory.Build.props
      tag: ${{ needs.new-version.outputs.version }}
      dry-run: false

```

## prevent-github-change

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/prevent-github-change.yaml)

Prevent fork users to change files triggered by. Only Organization contributors can change these files.

**Sample usage**

```yaml
name: Prevent github change
on:
  pull_request:
    paths:
      - ".github/**/*.yaml"
      - ".github/**/*.yml"

jobs:
  detect:
    permissions:
      contents: read
    uses: Cysharp/Actions/.github/workflows/prevent-github-change.yaml@main
```


## stale-issue

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/stale-issue.yaml)

Stale issue and PRs.
Mainly used for Issue/PR management.

**Sample usage**


```yaml
name: "Close stale issues"

on:
  schedule:
    - cron: "0 0 * * *"

jobs:
  stale:
    permissions:
      contents: read
      pull-requests: write
      issues: write
    uses: Cysharp/Actions/.github/workflows/stale-issue.yaml@main
```

## update-packagejson

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/update-packagejson.yaml)

Update specified `Unity package.json` and `Godot plugin.cfg` version with tag version. Mainly used for UPM and Godot plugin release workflow.

**Sample usage**

```yaml
name: Build-Release

on:
  workflow_dispatch:
    inputs:
      tag:
        description: "tag: git tag you want create. (sample 1.0.0)"
        required: true
      dry-run:
        description: "dry_run: true will never create release/nuget."
        required: true
        default: false
        type: boolean

jobs:
  update-packagejson:
    permissions:
      actions: read
      contents: write
    uses: Cysharp/Actions/.github/workflows/update-packagejson.yaml@main
    with:
      # you can write multi path.
      file-path: |
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo/package.json
        ./Sandbox/Sandbox.Godot/addons/Foo/plguin.cfg
        ./Sandbox/Directory.Build.props
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }}

  build-unity:
    needs: [update-packagejson]
    runs-on: ubuntu-24.04
    steps:
      - uses: actions/checkout@v4
        with:
          ref: ${{ needs.update-packagejson.outputs.sha }}  # use updated package.json

  # use clean-packagejson-branch.yaml to delete dry-run branch.
  cleanup:
    if: ${{ needs.update-packagejson.outputs.is-branch-created == 'true' }}
    needs: [update-packagejson]
    permissions:
      contents: write
    uses: Cysharp/Actions/.github/workflows/clean-packagejson-branch.yaml@main
    with:
      branch: ${{ needs.update-packagejson.outputs.branch-name }}
```

**Execute dotnet run**

Use `dotnet-run-path` to run `dotnet run --project` after update package.json.

```yaml
name: Build-Release

on:
  workflow_dispatch:
    inputs:
      tag:
        description: "tag: git tag you want create. (sample 1.0.0)"
        required: true
      dry-run:
        description: "dry_run: true will never create release/nuget."
        required: true
        default: false
        type: boolean

jobs:
  update-packagejson:
    permissions:
      actions: read
      contents: write
    uses: Cysharp/Actions/.github/workflows/update-packagejson.yaml@main
    with:
      # you can write multi path.
      file-path: |
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo/package.json
      # you can write multi path.
      dotnet-run-path: |
        ./Sandbox/Sandbox.Console/Sandbox.Console.csproj
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }}
```

## validate-tag

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/validate-tag.yaml)

Validate tag is newer than latest release tag.

**Sample usage**

```yaml
name: "Validate release tag"

on:
  workflow_dispatch:
    inputs:
      tag:
        description: "tag: git tag you want create. (sample 1.0.0)"
        required: true
      require-validation:
        description: "require-validation: true require validation must pass, false to keep going even validation failed."
        required: false
        type: boolean
        default: true

jobs:
  validate:
    uses: Cysharp/Actions/.github/workflows/validate-tag.yaml@main
    with:
      tag: ${{ inputs.tag }}
      require-validation: ${{ inputs.require-validation }} # true = exit 1 if tag is older than current release. false = keep going even failed.

  test:
    needs: [validate]
    runs-on: ubuntu-24.04
    steps:
      - run: echo "${{ needs.validate.outputs.validated }}" # true or false

```

# 🎬 Actions

## check-benchmarkable

> [See action](https://github.com/Cysharp/Actions/blob/main/.github/actions/check-benchmarkable/action.yaml)

Check if GitHub User is allow to run benchmark.
Mainly used for benchmark CI workflow.

> [!NOTE]
> This action is workaround for current `github.event.comment.author_association` inconsistence behavior.
> `github.event.comment.author_association` should return `OWNER`, `MEMBER` or `CORABORATOR` for organization member, however currently it returns `CONTRIBUTOR` even actor is Org member.
> It means `github.event.comment.author_association` can't be used to check if actor is Org member == "benchmark command allowed user" or not.
> This action checks if actor is Benchmark allowd by statically defined list, lol.

**sample usage**

```yaml
name: benchmark

jobs:
  # is actor is benchmarkable
  verify:
    if: ${{ github.event_name == 'workflow_dispatch' || contains(github.event.comment.body, '/benchmark') }}
    outputs:
      is-benchmarkable: ${{ steps.is-benchmarkable.outputs.authorized }} # true or false
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    steps:
      - name: Check actor is benchmarkable
        id: is-benchmarkable
        uses: Cysharp/Actions/.github/actions/check-benchmarkable@main
        with:
          username: ${{ github.actor }}

  # run benchmark
  benchmark:
    needs: [verify]
    if: ${{ needs.verify.outputs.is-benchmarkable == 'true' }}
    environment: benchmark # required for Azure login
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    steps:
      - run: echo "run benchmark"
```

## check-metas

> [See action](https://github.com/Cysharp/Actions/blob/main/.github/actions/check-metas/action.yaml)

Check Unity .meta files are not generated.
Mainly used for Unity CI workflow.

**Sample usage**

```yaml
name: build-debug

on:
  push:
    branches:
      - main

jobs:
  build-unity:
    name: "Build Unity package"
    runs-on: ubuntu-24.04
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v4
      # Any actions that create .meta when it was not comitted.
      - name: Unity Build
        run: touch ./Sandbox/Sandbox.Unity/Assets/Scene1.unity.meta
      - name: Check all .meta is comitted
        uses: Cysharp/Actions/.github/actions/check-metas@main
        with:
          directory: ./Sandbox/Sandbox.Unity
```

## checkout

> [See action](https://github.com/Cysharp/Actions/blob/main/.github/actions/checkout/action.yaml)

Wrapper of [actions/checkout](https://github.com/actions/checkout/tree/main) to offer centrlral managed checkout by sha pinning.

**Sample usage**

```yaml
name: build-debug

on:
  push:
    branches:
      - main

jobs:
  build-unity:
    name: "Build Unity package"
    runs-on: ubuntu-24.04
    timeout-minutes: 15
    steps:
      # - uses: actions/checkout@v4
      - use: Cysharp/Actions/.github/actions/checkout@main
      # Any actions that create .meta when it was not comitted.
      - name: Unity Build
        run: touch ./Sandbox/Sandbox.Unity/Assets/Scene1.unity.meta
      - name: Check all .meta is comitted
        uses: Cysharp/Actions/.github/actions/check-metas@main
        with:
          directory: ./Sandbox/Sandbox.Unity
```


## download-artifact

> [See action](https://github.com/Cysharp/Actions/blob/main/.github/actions/download-artifact/action.yaml)

Wrapper of [actions/download-artifact](https://github.com/actions/download-artifact/tree/main) to offer default value and consistent action versioning. Mainly used for Release artifact.

> [!TIP]
> See [upload-artifact](#upload-artifact) for upload.

**Sample usage**

```yaml
name: build-debug

on:
  push:
    branches:
      - main

jobs:
  # must prepare upload-artifact

  download-artifact:
    needs: [upload-artifact]
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v4
      - uses: Cysharp/Actions/.github/actions/download-artifact@main
        with:
          name: my-artifact
      - name: Display structure of downloaded files
        run: ls -R
```


## setup-dotnet

> [See action](https://github.com/Cysharp/Actions/blob/main/.github/actions/setup-dotnet/action.yaml)

Wrapper of [actions/setup-dotnet](https://github.com/actions/setup-dotnet) to offer default value and consistent action versioning and Environment variables. Mainly used for .NET CI workflow.

**Sample usage**

```yaml
name: build-debug

on:
  push:
    branches:
      - main

jobs:
  dotnet-build:
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v4
      - uses: Cysharp/Actions/.github/actions/setup-dotnet@main
```

## unity-builder

> [See action](https://github.com/Cysharp/Actions/blob/main/.github/actions/unity-builder/action.yaml)

Build Unity projects for different platforms.

**Sample usage**

```yaml
name: build-debug

on:
  push:
    branches:
      - main

jobs:
  dotnet-build:
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v4
      # execute scripts/Export Package
      # /opt/Unity/Editor/Unity -quit -batchmode -nographics -silent-crashes -logFile -projectPath . -executeMethod PackageExporter.Export
      - name: Build Unity (.unitypacakge)
        uses: Cysharp/Actions/.github/actions/unity-builder@main
        with:
          projectPath: src/MyProject.Unity
          unityVersion: "2020.3.33f1"
          targetPlatform: StandaloneLinux64
          buildMethod: PackageExporter.Export
          versioning: None
```

## upload-artifact

> [See action](https://github.com/Cysharp/Actions/blob/main/.github/actions/upload-artifact/action.yaml)

Wrapper of [actions/upload-artifact](https://github.com/actions/upload-artifact/tree/main) to offer default value and consistent action versioning. Mainly used for Release artifact.

> [!TIP]
> See [download-artifact](#download-artifact) for download.

**Sample usage**

```yaml
name: build-debug

on:
  push:
    branches:
      - main

jobs:
  upload-artifact:
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v4
      - run: mkdir -p path/to/artifact
      - run: echo hello > path/to/artifact/world.txt
      - uses: Cysharp/Actions/.github/actions/upload-artifact@main
        with:
          name: my-artifact
          path: path/to/artifact/world.txt
```
