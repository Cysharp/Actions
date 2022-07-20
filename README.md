Actions

Cysharp GitHub Actions "reusable workflows" and "composite actions".
Cysharp OSS repository uses and maintain for this purpose.

# 📖 Table of contents

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->

- [Reusable workflows](#reusable-workflows)
  - [update-packagejson.yaml](#update-packagejsonyaml)

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

env:
  GIT_TAG: ${{ github.event.inputs.tag }}
  DRY_RUN: ${{ github.event.inputs.dry-run }}

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
