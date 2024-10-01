#!/bin/bash
set -euo pipefail
# Create GitHub Matrix JSON from yaml config file, output can be used as matrix strategy in GitHub Actions.
#
# # values in each matrix
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
# bash .github/scripts/benchmark_config2matrix.sh --benchmark-config-path ".github/scripts/_template_benchmark_config.yaml"
#
# #########################
# ### output arg sample ###
# #########################
# dotnet-version=8.0
# benchmark-expire-min=15
# benchmark-timeout-min=10
# benchmark-client-run-script-path=.github/scripts/benchmark-client-run.sh
# benchmark-server-run-script-path=.github/scripts/benchmark-server-run.sh
# benchmark-server-stop-script-path=.github/scripts/benchmark-server-stop.sh
# benchmark-client-run-script-args=--args "-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 8 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c"
# benchmark-server-run-script-args=--args "-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c"
# benchmark-client-run-script-args=--args "-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 8 --channels 1 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c"
# benchmark-server-run-script-args=--args "-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c"
# matrix={"include":[{"dotnet-version":"8.0","benchmark-expire-min":"10","benchmark-client-run-script-path":".github/scripts/benchmark-client-run.sh","benchmark-server-run-script-path":".github/scripts/benchmark-server-run.sh","benchmark-server-stop-script-path":".github/scripts/benchmark-server-stop.sh","benchmark-client-run-script-args":"--args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 8 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\"","benchmark-server-run-script-args":"--args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\""},{"dotnet-version":"8.0","benchmark-expire-min":"10","benchmark-client-run-script-path":".github/scripts/benchmark-client-run.sh","benchmark-server-run-script-path":".github/scripts/benchmark-server-run.sh","benchmark-server-stop-script-path":".github/scripts/benchmark-server-stop.sh","benchmark-client-run-script-args":"--args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 8 --channels 1 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c\"","benchmark-server-run-script-args":"--args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c\""}]}

while [ $# -gt 0 ]; do
  case $1 in
    # required
    --benchmark-config-path) _BENCHMARK_CONFIG_FILE=$2; shift 2; ;;
    *) shift ;;
  esac
done

function print {
  echo "$*"
}
function error {
  echo "$*" >&2
}
function title {
  echo ""
  echo "# $*"
}
function extract_placeholders {
  local template_string=$1
  echo "$template_string" | grep -oP '{{\s*\K\w+(?=\s*}})'
}
function is_number {
  local value=$1
  if [[ "$value" =~ ^[0-9]+$ ]]; then
    echo "1"
  else
      echo "0"
  fi
}

title "Arguments:" >&2
print "  --benchmark-config-path=${_BENCHMARK_CONFIG_FILE}"

title "Config:" >&2
print "  dotnet_version_key=${dotnet_version_key:=.dotnet-version}"
print "  benchmark_timeout_min_key=${benchmark_timeout_min_key:=.benchmark-timeout-min}"
print "  benchmark_expire_min_key=${benchmark_expire_min_key:=.benchmark-expire-min}"
print "  benchmark_client_run_script_path_key=${benchmark_client_run_script_path_key:=.benchmark-client-run-script-path}"
print "  benchmark_server_run_script_path_key=${benchmark_server_run_script_path_key:=.benchmark-server-run-script-path}"
print "  benchmark_server_stop_script_path_key=${benchmark_server_stop_script_path_key:=.benchmark-server-stop-script-path}"
print "  benchmark_client_run_script_args_key=${benchmark_client_run_script_args_key:=.benchmark-client-run-script-args}"
print "  benchmark_server_run_script_args_key=${benchmark_server_run_script_args_key:=.benchmark-server-run-script-args}"

general_json_elements=()
matrix_includes_json_array="["
general_keys=("$dotnet_version_key" "$benchmark_timeout_min_key" "$benchmark_expire_min_key" "$benchmark_client_run_script_path_key" "$benchmark_server_run_script_path_key" "$benchmark_server_stop_script_path_key")
template_string_keys=("$benchmark_client_run_script_args_key" "$benchmark_server_run_script_args_key")

if [[ "${CI:-""}" == "" ]]; then
  GITHUB_OUTPUT="/dev/null"
fi

if [[ ! -f "${_BENCHMARK_CONFIG_FILE}" ]]; then
  error "File not found: ${_BENCHMARK_CONFIG_FILE}"
  exit 1
fi

# Handle general values
for general_key in "${general_keys[@]}"; do
  obtained_value=$(yq eval "$general_key" "$_BENCHMARK_CONFIG_FILE")
  k=${general_key/\./}
  echo "${k}=$obtained_value" | tee -a "$GITHUB_OUTPUT"
  if [[ "$(is_number "$obtained_value")" == "1" ]]; then
    general_json=$(jq -c -n --arg key "$k" --argjson value "$obtained_value" '{
      ($key): $value
    }')
  else
    general_json=$(jq -c -n --arg key "$k" --arg value "$obtained_value" '{
      ($key): $value
    }')
  fi
  # echo "$general_json" # debug
  general_json_elements+=("$general_json")
done

# find job to proceed
jobs_json=$(yq eval -o=json '.jobs' "$_BENCHMARK_CONFIG_FILE")
filtered_jobs=($(echo "$jobs_json" | jq -c '.[]'))

# handle filter job
for job in "${filtered_jobs[@]}"; do
  json_elements=()
  args_json_elements=()

  # Handle server / client args by job
  for template_string_key in "${template_string_keys[@]}"; do
    # find template string key
    template_string=$(yq "$template_string_key" "$_BENCHMARK_CONFIG_FILE")

    if [[ "$template_string" == "" || "$template_string" == "null" ]]; then
      error "Specified yaml not contains $template_string_key"
      exit 1
    fi


    # check if any matching jobs found
    if [ -z "$filtered_jobs" ]; then
        error "No matching job found for $_MATCH_JOB"
        exit 1
    fi

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

      k=${template_string_key/\./}
      echo "${k}=$assembled_string" | tee -a "$GITHUB_OUTPUT"
      args_json=$(jq -c -n --arg key "$k" --arg value "$assembled_string" '{
        ($key): $value
      }')
      args_json_elements+=("$args_json")
  done
  json_elements=("${general_json_elements[@]}" "${args_json_elements[@]}")
  matrix_includes_json_array+=$(jq -c -s add <<< "${json_elements[@]}")
  matrix_includes_json_array+=","
done

matrix_includes_json_array="${matrix_includes_json_array%,}"
matrix_includes_json_array+=']'
json_output=$(jq -c -n --argjson matrix_includes "$matrix_includes_json_array" '{
  include: $matrix_includes
}')
echo "matrix=$json_output" | tee -a "$GITHUB_OUTPUT"
