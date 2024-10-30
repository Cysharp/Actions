#!/bin/bash
set -euo pipefail

function usage {
  cat <<EOF
Usage: $(basename $0) [options]
Descriptions: Create GitHub Matrix JSON, output can be used as matrix strategy in GitHub Actions. This script can be used to generate matrix from loader config or branch name.

Options:
  --branch            string      Branch name to run the benchmark, required when --enable-loader is false (default: "")
  --config-path       string      The name of the dev center.
  --debug             bool        Show debug output or not. (default: false)
  --help                          Show this help message

Sample benchmark-config:
  see: .github/scripts/tests/template_benchmark_config.yaml

Examples:
  1. Generate GitHub Matrix JSON from loader
      $ bash ./.github/scripts/$(basename $0) --config-path ".github/scripts/tests/template_schedule_loader.yaml"
  2. Generate GitHub Matrix JSON from branch name
      $ bash ./.github/scripts/benchmark_loader2matrix.sh --branch main --config-path ./.github/scripts/tests/template_benchmark_config.yaml
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    # optional
    --branch) _BRANCH=$2; shift 2; ;;
    --config-path) _BENCHMARK_CONFIG_FILE=$2; shift 2; ;;
    --debug) _DEBUG=$2; shift 2; ;;
    --help) usage; exit 1; ;;
    *) shift ;;
  esac
done

function print {
  echo "$(date "+%Y-%m-%d %H:%M:%S") INFO(${FUNCNAME[1]:-main}): $*"
}
function title {
  echo ""
  echo "$(date "+%Y-%m-%d %H:%M:%S") INFO(${FUNCNAME[1]:-main}): # $*"
}
function error {
  echo "$(date "+%Y-%m-%d %H:%M:%S") ERRO(${FUNCNAME[1]:-main}): ERROR $*" >&2
}
function debug {
  if [[ "${_DEBUG}" == "true" ]]; then
    echo "$(date "+%Y-%m-%d %H:%M:%S") DEBG(${FUNCNAME[1]:-main}): $*"
  fi
}
function validate_config {
  # check yaml contains branch-configs key
  if [[ "$(yq e 'has("branch-configs")' "${_BENCHMARK_CONFIG_FILE}")" != "true" ]]; then
    error "${_BENCHMARK_CONFIG_FILE} does not contain branch-configs key"
    exit 1
  fi

  yq -o json eval '.branch-configs[]' "$_BENCHMARK_CONFIG_FILE" | jq -c | while read -r item; do
    # Check for branch key
    branch_exists=$(echo "$item" | jq 'has("branch")')
    # Check for config key
    config_exists=$(echo "$item" | yq 'has("config")')

    if [ "$branch_exists" != "true" ] || [ "$config_exists" != "true" ]; then
      error "An item in branch-configs does not have both 'branch' and 'config' keys. item: $item"
      exit 1
    fi
  done
}

title "Arguments:"
print "  --branch=${_BRANCH:=""}"
print "  --config-path=${_BENCHMARK_CONFIG_FILE:=""}"
print "  --debug=${_DEBUG:=false}"

if [[ "${CI:-""}" == "" ]]; then
  GITHUB_OUTPUT="/dev/null"
fi

title "Validating arguments:"
# branch or config must be specified
if [[ "${_BRANCH}" == "" && "${_BENCHMARK_CONFIG_FILE}" == "" ]]; then
  error "Loader config not specified, please use --config-path CONFIG_PATH to specify config."
  exit 1
fi
# config path is specified but not found
if [[ "${_BENCHMARK_CONFIG_FILE}" != "" && ! -f "${_BENCHMARK_CONFIG_FILE}" ]]; then
  error "Loader config specified but config not found. Please check the path. ${_BENCHMARK_CONFIG_FILE}"
  exit 1
fi
# bracnh is specified but config is not
if [[ "${_BRANCH}" != "" && "${_BENCHMARK_CONFIG_FILE}" == "" ]]; then
  error "Branch specified but config not found. Please use --config-path CONFIG_PATH to specify config."
  exit 1
fi

if [[ "${_BRANCH}" == "" ]]; then
  # config mode
  title "Loader config found, creating matrix json from config."

  title "Validate config"
  validate_config

  title "Scan config and obtain json elements (General keys)"
  mapfile -t matrix_includes_json_array < <(yq -o json eval '.branch-configs' "$_BENCHMARK_CONFIG_FILE" | jq -c)

  title "Output Matrix json"
  json_output=$(jq -c -n --argjson matrix_includes "${matrix_includes_json_array[@]}" '{
    include: $matrix_includes
  }')
else
  # branch mode
  title "Branch specified, creating matrix via current config and branch."
  json_output=$(jq -c -n --arg branch "${_BRANCH}" --arg config "$_BENCHMARK_CONFIG_FILE" '{
    include: [
      {
        branch: $branch,
        config: $config,
      }
    ]
  }')

fi

  print "Pretty print Matrix json for debug"
  echo "$json_output" | jq

  print "Output for GITHUB_OUTPUT"
  echo "matrix=$json_output" | tee -a "$GITHUB_OUTPUT"
