[![Test check-metas](https://github.com/Cysharp/Actions/actions/workflows/test-check-metas.yaml/badge.svg)](https://github.com/Cysharp/Actions/actions/workflows/test-check-metas.yaml)
[![Test clean-packagejson-branch](https://github.com/Cysharp/Actions/actions/workflows/test-clean-packagejson-branch.yaml/badge.svg)](https://github.com/Cysharp/Actions/actions/workflows/test-clean-packagejson-branch.yaml)
[![Test create-release](https://github.com/Cysharp/Actions/actions/workflows/test-create-release.yaml/badge.svg)](https://github.com/Cysharp/Actions/actions/workflows/test-create-release.yaml)
[![Test setup-dotnet](https://github.com/Cysharp/Actions/actions/workflows/test-setup-dotnet.yaml/badge.svg)](https://github.com/Cysharp/Actions/actions/workflows/test-setup-dotnet.yaml)
[![Test update-packagejson](https://github.com/Cysharp/Actions/actions/workflows/test-update-packagejson.yaml/badge.svg)](https://github.com/Cysharp/Actions/actions/workflows/test-update-packagejson.yaml)

# Actions

Cysharp OSS repository uses and maintained GitHub Actions "reusable workflows" and "composite actions".

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
# 📖 Table of Contents

- [Reusable workflows](#reusable-workflows)
  - [clean-packagejson-branch](#clean-packagejson-branch)
  - [create-release](#create-release)
  - [prevent-github-change](#prevent-github-change)
  - [stale-issue](#stale-issue)
  - [update-packagejson](#update-packagejson)
- [Actions](#actions)
  - [check-metas](#check-metas)
  - [setup-dotnet](#setup-dotnet)
  - [unity-builder](#unity-builder)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

# Reusable workflows

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
    runs-on: ubuntu-latest
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
    uses: Cysharp/Actions/.github/workflows/update-packagejson.yaml@main
    with:
      file-path: |
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo/package.json
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo.Plugin/package.json
        ./Sandbox/Sandbox.Godot/addons/Foo/plugin.cfg
        ./Sandbox/Directory.Build.props
      tag: ${{ inputs.tag }}
      dry-run: false
      push-tag: false # tag push is done by create-release job

  build-dotnet:
    runs-on: ubuntu-latest
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
    runs-on: ubuntu-latest
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
    uses: Cysharp/Actions/.github/workflows/clean-packagejson-branch.yaml@main
    with:
      branch: ${{ needs.update-packagejson.outputs.branch-name }}
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
    uses: Cysharp/Actions/.github/workflows/update-packagejson.yaml@main
    with:
      # you can write multi path.
      file-path: |
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo/package.json
        ./Sandbox/Sandbox.Godot/addons/Foo/plguin.cfg
        ./Sandbox/Directory.Build.props
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }}
      push-tag: false # recommend push tag on create-release job.

  build-unity:
    needs: [update-packagejson]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          ref: ${{ needs.update-packagejson.outputs.sha }}  # use updated package.json

  # use clean-packagejson-branch.yaml to delete dry-run branch.
  cleanup:
    if: ${{ needs.update-packagejson.outputs.is-branch-created == 'true' }}
    needs: [update-packagejson]
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
      push-tag: false # recommend push tag on create-release job.
```


# Actions

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
    runs-on: ubuntu-latest
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


## setup-dotnet

> [See action](https://github.com/Cysharp/Actions/blob/main/.github/actions/setup-dotnet/action.yaml)

Setup .NET SDK and Environment variables.
Mainly used for .NET CI workflow.

**Sample usage**

```yaml
name: build-debug

on:
  push:
    branches:
      - main

jobs:
  dotnet-build:
    runs-on: ubuntu-latest
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
    runs-on: ubuntu-latest
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
