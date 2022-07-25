Actions

Cysharp GitHub Actions "reusable workflows" and "composite actions".
Cysharp OSS repository uses and maintain for this purpose.

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
# 📖 Table of Contents

- [Reusable workflows](#reusable-workflows)
  - [update-packagejson.yaml](#update-packagejsonyaml)
  - [clean-packagejson-branch.yaml](#clean-packagejson-branchyaml)
- [Actions](#actions)
  - [check-metas](#check-metas)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

# Reusable workflows

## update-packagejson.yaml

> [See workflow]((https://github.com/Cysharp/Actions/blob/main/.github/workflows/update-packagejson.yaml))

Update specified Unity's package.json version with tag version.
Mainly used for UPM release workflow.

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
      file-path: ./src/Foo.Unity/Assets/Plugins/Foo/package.json
      tag: ${{ github.event.inputs.tag }}
      dry-run: ${{ fromJson(github.event.inputs.dry-run) }}

  # dotnet build
  build-dotnet:
    needs: [update-packagejson]
    runs-on: ubuntu-latest
    steps:
      - run: echo ${{ needs.update-packagejson.outputs.sha }}
      - uses: actions/checkout@v3
        with:
          ref: ${{ needs.update-packagejson.outputs.sha }} # use updated commit sha

  # unity build
  build-unity:
    needs: [update-packagejson]
    runs-on: ubuntu-latest
    steps:
      - run: echo ${{ needs.update-packagejson.outputs.sha }}
      - uses: actions/checkout@v3
        with:
          ref: ${{ needs.update-packagejson.outputs.sha }}  # use updated commit sha
```

## clean-packagejson-branch.yaml

> [See workflow]((https://github.com/Cysharp/Actions/blob/main/.github/workflows/clean-packagejson-branch.yaml)

Clean up update-poackagejson job's dry-run branch.
Mainly used for UPM release workflow.

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
      tag: ${{ github.event.inputs.tag }}
      dry-run: ${{ fromJson(github.event.inputs.dry-run) }}

  cleanup:
    if: github.event.inputs.dry-run == 'true'
    needs: [update-packagejson]
    uses: Cysharp/Actions/.github/workflows/clean-packagejson-branch.yaml@main
    with:
      branch: ${{ needs.update-packagejson.outputs.branch-name }}
```


# Actions

## check-metas

> [See action]((https://github.com/Cysharp/Actions/blob/main/.github/actions/check-meta/action.yaml))

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
      # execute scripts/Export Package
      # /opt/Unity/Editor/Unity -quit -batchmode -nographics -silent-crashes -logFile -projectPath . -executeMethod PackageExporter.Export
      - name: Export unitypackage
        uses: game-ci/unity-builder@v2
        env:
          UNITY_LICENSE: ${{ secrets[matrix.license] }}
        with:
          buildMethod: PackageExporter.Export
          versioning: None

      - uses: Cysharp/Actions/.github/actions/check-metas # check meta files
        with:
          directory: src/MyProject.Unity
```
