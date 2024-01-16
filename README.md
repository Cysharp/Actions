Actions

Cysharp GitHub Actions "reusable workflows" and "composite actions".
Cysharp OSS repository uses and maintain for this purpose.

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

Clean up update-packagejson job's dry-run branch.

> [!TIP]
> Use this workflow with update-packagejson workflow.
> See [#update-packagejson](#update-packagejson) for sample usage.

## create-release

> [See workflow](https://github.com/Cysharp/Actions/blob/main/.github/workflows/create-release.yaml)

Create GitHub Release, upload NuGet and upload Unity AssetBundle to release assets. Mainly used for NuGet and Unity release workflow.

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
      file-path: ./src/Foo.Unity/Assets/Plugins/Foo/package.json
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }}
      push-tag: false # recommend push tag on create-release job.

  dotnet-build:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v4
      - uses: Cysharp/Actions/.github/actions/setup-dotnet@main
      - run: dotnet build Foo.sln -c Release -p:Version=${{ inputs.tag }}
      - run: dotnet pack Foo.sln -c Release --no-build -p:Version=${{ inputs.tag }} -o ./publish
      # Store artifacts.
      - uses: actions/upload-artifact@v3
        with:
          name: nuget
          path: ./publish/

  build-unity:
    name: "Build Unity package"
    strategy:
      matrix:
        unity: ["2020.3.33f1"]
        include:
          - unity: 2020.3.33f1
            license: UNITY_LICENSE_2020
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v4
      - name: Export unitypackage
        uses: game-ci/unity-builder@v2
        env:
          UNITY_LICENSE: ${{ secrets[matrix.license] }}
        with:
          projectPath: src/Foo.Unity
          unityVersion: ${{ matrix.unity }}
          targetPlatform: StandaloneLinux64
          buildMethod: PackageExporter.Export
          versioning: None
      - uses: actions/upload-artifact@v3
        with:
          name: Foo.${{ inputs.tag }}.unitypackage
          path: ./src/Foo.Unity/Foo.${{ inputs.tag }}.unitypackage

  create-release:
    needs: [update-packagejson, build-dotnet, build-unity]
    uses: Cysharp/Actions/.github/workflows/create-release.yaml@main
    with:
      dry-run: ${{ inputs.dry-run }}
      commit-id: ${{ needs.update-packagejson.outputs.sha }}
      tag: ${{ inputs.tag }}
      push-tag: true
      nuget-push: true
      unitypackage-upload: true
      unitypackage-name: Foo.${{ inputs.tag }}.unitypackage
      unitypackage-path: ./Foo.${{ inputs.tag }}.unitypackage/Foo.${{ inputs.tag }}.unitypackage
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
  # reusable workflow caller
  update-packagejson:
    uses: Cysharp/Actions/.github/workflows/update-packagejson.yaml@main
    with:
      # you can write multi path.
      file-path: |
        ./Sandbox/Sandbox.Unity/Assets/Plugins/Foo/package.json
        ./Sandbox/Sandbox.Godot/addons/Foo/plguin.cfg
      tag: ${{ inputs.tag }}
      dry-run: ${{ inputs.dry-run }}
      push-tag: false # recommend push tag on create-release job.

  # any job using updated package.json or plugin.cfg
  build-unity:
    needs: [update-packagejson]
    runs-on: ubuntu-latest
    steps:
      - run: echo ${{ needs.update-packagejson.outputs.sha }}
      - uses: actions/checkout@v4
        with:
          ref: ${{ needs.update-packagejson.outputs.sha }}  # use updated package.json
      # do anything

  # delete remote branches created by update-packagejson (dry-run only)
  cleanup:
    # you can trigger with update-packagejson.outputs.branch-created to determine branch created.
    if: needs.update-packagejson.outputs.is-branch-created == 'true'
    needs: [update-packagejson]
    uses: Cysharp/Actions/.github/workflows/clean-packagejson-branch.yaml@main
    with:
      branch: ${{ needs.update-packagejson.outputs.branch-name }}

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
  pull_request:
    branches:
      - main

jobs:
  build-unity:
    name: "Build Unity package"
    strategy:
      matrix:
        unity: ["2020.3.33f1"]
        include:
          - unity: 2020.3.33f1
            license: UNITY_LICENSE_2020
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v4
      - name: Build Unity (.unitypacakge)
        uses: Cysharp/Actions/.github/actions/unity-builder@main
        with:
          projectPath: src/MyProject.Unity
          unityVersion: "${{ matrix.unity }}"
          targetPlatform: StandaloneLinux64
          buildMethod: PackageExporter.Export
          versioning: None

      - name: Check all .meta is comitted
        uses: Cysharp/Actions/.github/actions/check-metas@main # check meta files
        with:
          directory: src/MyProject.Unity
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
  pull_request:
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
  pull_request:
    branches:
      - main

jobs:
  dotnet-build:
    strategy:
      matrix:
        unity: ["2020.3.33f1"]
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
          unityVersion: "${{ matrix.unity }}"
          targetPlatform: StandaloneLinux64
          buildMethod: PackageExporter.Export
          versioning: None
```
