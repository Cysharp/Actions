#!/bin/bash
set -euo pipefail

while [ $# -gt 0 ]; do
  case $1 in
    # required
    --benchmark-config-path) _BENCHMARK_CONFIG_FILE=$2; shift 2; ;;
    # optional
    --branch) _BRANCH=$2; shift 2; ;;
    --enable-loader) _ENABLE_LOADER=$2; shift 2; ;;
    --debug) _DEBUG=$2; shift 2; ;;
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
  echo "$(date "+%Y-%m-%d %H:%M:%S") ERRO(${FUNCNAME[1]:-main}): # $*" >&2
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
print "  --benchmark-loader-path=${_BENCHMARK_CONFIG_FILE}"
print "  --branch=${_BRANCH:=""}"
print "  --enable-loader=${_ENABLE_LOADER:=false}"
print "  --debug=${_DEBUG:=false}"

if [[ "${CI:-""}" == "" ]]; then
  GITHUB_OUTPUT="/dev/null"
fi

if [[ "${_ENABLE_LOADER}" == "true" && ! -f "${_BENCHMARK_CONFIG_FILE}" ]]; then
  error "Loader config not found: ${_BENCHMARK_CONFIG_FILE}"
  exit 1
fi

if [[ "${_ENABLE_LOADER}" == "true" && -f "${_BENCHMARK_CONFIG_FILE}" ]]; then
  title "Loader config found, creating matrix json from config."

  title "Validate config"
  validate_config

  title "Scan config and obtain json elements (General keys)"
  mapfile -t matrix_includes_json_array < <(yq -o json eval '.branch-configs' "$_BENCHMARK_CONFIG_FILE" | jq -c)

  title "Output Matrix json"
  json_output=$(jq -c -n --argjson matrix_includes "${matrix_includes_json_array[@]}" '{
    include: $matrix_includes
  }')
elif [[ "${_ENABLE_LOADER}" == "false" ]]; then
  title "Loader is disabled, creating matrix via current config and branch."
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
