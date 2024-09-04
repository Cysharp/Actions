#!/bin/bash
set -euo pipefail
# Create benchmark argument from yaml config file, output can be used as github_output
# * dotnet-version
# * benchmark-expire-min
# * benchmark-client-run-script-path
# * benchmark-client-run-script-args
# * benchmark-server-run-script-path
# * benchmark-server-run-script-args
# * benchmark-server-stop-script-path

# #########################
# ### yaml file example ###
# #########################
# see: _template_benchmark_config.yaml
#
# #########################
# ### input sample      ###
# #########################
# bash .github/scripts/benchmark_config2args.sh --yaml-config-file ".github/scripts/_template_benchmark_config.yaml" --match-job "messagepack-h2c-linux-1"
#
# #########################
# ### output arg sample ###
# #########################
# dotnet-version=8.0
# benchmark-expire-min=10
# benchmark-client-run-script-path=.github/scripts/benchmark-client-run.sh
# benchmark-client-run-script-path=.github/scripts/benchmark-client-run.sh
# benchmark-server-stop-script-path=.github/scripts/benchmark-server-stop.sh
# benchmark-client-run-script-args=--args "-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 8 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c"
# benchmark-server-run-script-args=--args "-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c"

while [ $# -gt 0 ]; do
  case $1 in
    # required
    --match-job) _MATCH_JOB=$2; shift 2; ;;
    --yaml-config-file) _YAML_CONFIG_FILE=$2; shift 2; ;;
    # optional
    --benchmark-client-run-script-args-key) _BENCHMARK_CLIENT_RUN_SCRIPT_ARGS_KEY=$2; shift 2; ;;
    --benchmark-server-run-script-args-key) _BENCHMARK_SERVER_RUN_SCRIPT_ARGS_KEY=$2; shift 2; ;;
    *) shift ;;
  esac
done

function extract_placeholders {
  local template_string=$1
  echo "$template_string" | grep -oP '{{\s*\K\w+(?=\s*}})'
}

echo "Arguments:" >&2
echo "  --match-job=${_MATCH_JOB}"
echo "  --yaml-config-file=${_YAML_CONFIG_FILE}"

echo "Config:" >&2
echo "  dotnet_version_key=${dotnet_version_key:=.dotnet-version}"
echo "  benchmark_expire_min_key=${benchmark_expire_min_key:=.benchmark-expire-min}"
echo "  benchmark_client_run_script_path_key=${benchmark_client_run_script_path_key:=.benchmark-client-run-script-path}"
echo "  benchmark_server_run_script_path_key=${benchmark_server_run_script_path_key:=.benchmark-server-run-script-path}"
echo "  benchmark_server_stop_script_path_key=${benchmark_server_stop_script_path_key:=.benchmark-server-stop-script-path}"
echo "  benchmark_client_run_script_args_key=${benchmark_client_run_script_args_key:=.benchmark-client-run-script-args}"
echo "  benchmark_server_run_script_args_key=${benchmark_server_run_script_args_key:=.benchmark-server-run-script-args}"

if [[ "${CI:-""}" == "" ]]; then
  GITHUB_OUTPUT="/dev/null"
fi

if [[ ! -f "${_YAML_CONFIG_FILE}" ]]; then
  echo "File not found: ${_YAML_CONFIG_FILE}" >&2
  exit 1
fi

# Handle general values
general_keys=("$dotnet_version_key" "$benchmark_expire_min_key" "$benchmark_client_run_script_path_key" "$benchmark_server_run_script_path_key" "$benchmark_server_stop_script_path_key")
for general_key in "${general_keys[@]}"; do
  obtained_value=$(yq eval "$general_key" "$_YAML_CONFIG_FILE")
  echo "${general_key/\./}=$obtained_value" | tee -a "$GITHUB_OUTPUT"
done

# Handle server / client args by job
template_string_keys=("$benchmark_client_run_script_args_key" "$benchmark_server_run_script_args_key")
for template_string_key in "${template_string_keys[@]}"; do
  # find template string key
  jobs_json=$(yq eval -o=json '.jobs' "$_YAML_CONFIG_FILE")
  template_string=$(yq "$template_string_key" "$_YAML_CONFIG_FILE")

  if [[ "$template_string" == "" || "$template_string" == "null" ]]; then
    echo "Specified yaml not contains $template_string_key" >&2
    exit 1
  fi

  # find job to proceed
  filtered_jobs=$(echo "$jobs_json" | jq -c --arg match_value "$_MATCH_JOB" '.[] | select(.match == $match_value)')

  # check if any matching jobs found
  if [ -z "$filtered_jobs" ]; then
      echo "No matching job found for $_MATCH_JOB" >&2
      exit 1
  fi

  # handle filter job
  echo "$filtered_jobs" | while read -r job; do
    # initialize assembled string
    assembled_string="$template_string"

    # extract placeholders from the template string
    placeholders=$(extract_placeholders "$template_string")

    # extract values from the job, then replace the placeholder with the value
    # {{ foo }} will be replaced with the value of the key "foo" in the job
    for key in $placeholders; do
      value=$(echo "$job" | jq -r --arg key "$key" '.[$key]')
      assembled_string=$(echo "$assembled_string" | sed -e "s/{{\s*$key\s*}}/$value/g")
    done

    echo "${template_string_key/\./}=$assembled_string" | tee -a "$GITHUB_OUTPUT"
  done
done
