# Diagrams

## Benchmark Config to dispatch benchmark for multiple configurations

`type: config` config defines `job` array. The `benchmark_config2matrix.sh` will convert the config to GitHub Actions matrix.

```mermaid
sequenceDiagram
    participant gha as GitHub Actions
    participant config2matrix as Config2Matrix
    participant benchmark as Benchmark

    gha->>config2matrix: Convert config to matrix
    config2matrix->>config2matrix: Checkout current branch
    config2matrix->>benchmark: Create matrix
    benchmark->>benchmark: Run benchmark on current branch
```

## Schedule Loader to dispatch benchmark for multiple branches

GitHub Actions schedule only invoke on default branch, this means that we need to dispatch the benchmark for multiple branches. This sequence diagram indicate how to dispatch the benchmark for multiple branches.

`type: loader` config defines `branch & config path` array. The loader will load the config for each branch and dispatch the benchmark.

```mermaid
sequenceDiagram
    participant gha as GitHub Actions
    participant loader as ScheduleLoader
    participant config2matrix as Config2Matrix
    participant benchmark as Benchmark

    gha->>loader: Load benchmark config
    loader->>config2matrix: Convert config to matrix
    par BranchA
      config2matrix->>config2matrix: Checkout BranchA
      config2matrix->>benchmark: Create matrix
      benchmark->>benchmark: Run benchmark on BranchA
    and BranchB
      config2matrix->>config2matrix: Checkout BranchB
      config2matrix->>benchmark: Create matrix
      benchmark->>benchmark: Run benchmark on BranchB
    end
```
