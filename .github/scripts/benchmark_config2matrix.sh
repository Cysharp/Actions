#!/bin/bash
set -euo pipefail
# Create GitHub Matrix JSON from yaml config file, output can be used as matrix strategy in GitHub Actions.
#
# # values in each matrix
# * apt-tools
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
# ### output sample ###
# #########################
# 2024-10-09 16:01:09 INFO(main): # Arguments:
# 2024-10-09 16:01:09 INFO(main):   --benchmark-config-path=./.github/scripts/_template_benchmark_config.yaml
# 2024-10-09 16:01:09 INFO(main):   --debug=true

# 2024-10-09 16:01:09 INFO(main): # Parameters:
# 2024-10-09 16:01:09 INFO(main):   * template_key_endswith=run-script-args

# 2024-10-09 16:01:09 INFO(main): # Gathering config keys

# 2024-10-09 16:01:09 INFO(main): # Validate config
#
# 2024-10-09 15:58:34 INFO(main): # Scan config and obtain json elements (General keys)
# 2024-10-09 15:58:34 INFO(main): apt-tools=libmsquic
# 2024-10-09 15:58:34 INFO(main): dotnet-version=8.0
# 2024-10-09 15:58:34 INFO(main): benchmark-expire-min=15
# 2024-10-09 15:58:35 INFO(main): benchmark-timeout-min=10
# 2024-10-09 15:58:35 INFO(main): benchmark-client-run-script-path=.github/scripts/benchmark-client-run.sh
# 2024-10-09 15:58:35 INFO(main): benchmark-server-run-script-path=.github/scripts/benchmark-server-run.sh
# 2024-10-09 15:58:35 INFO(main): benchmark-server-stop-script-path=.github/scripts/benchmark-server-stop.sh

# 2024-10-09 15:58:35 INFO(main): # Reflect the values defined in jobs into the placeholders for xxxx-run-script-args.
# 2024-10-09 15:58:35 INFO(main): benchmark-client-run-script-args=--run-args "-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c" --build-args ""
# 2024-10-09 15:58:35 INFO(main): benchmark-server-run-script-args=--run-args "-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c"
# 2024-10-09 15:58:35 INFO(main): benchmark-client-run-script-args=--run-args "-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 1 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c" --build-args ""
# 2024-10-09 15:58:36 INFO(main): benchmark-server-run-script-args=--run-args "-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c"
# 2024-10-09 15:58:36 INFO(main): benchmark-client-run-script-args=--run-args "-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c" --build-args "--p:UseNuGetClient=6.14"
# 2024-10-09 15:58:36 INFO(main): benchmark-server-run-script-args=--run-args "-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c"
# 2024-10-09 15:58:36 INFO(main): benchmark-client-run-script-args=--run-args "-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c" --build-args ""
# 2024-10-09 15:58:36 INFO(main): benchmark-server-run-script-args=--run-args "-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c"

# 2024-10-09 15:58:36 INFO(main): # Output Matrix json
# 2024-10-09 15:58:36 INFO(main): Output for GITHUB_OUTPUT
# matrix={"include":[{"apt-tools":"libmsquic","dotnet-version":"8.0","benchmark-expire-min":15,"benchmark-timeout-min":10,"benchmark-client-run-script-path":".github/scripts/benchmark-client-run.sh","benchmark-server-run-script-path":".github/scripts/benchmark-server-run.sh","benchmark-server-stop-script-path":".github/scripts/benchmark-server-stop.sh","benchmark-client-run-script-args":"--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"\"","benchmark-server-run-script-args":"--run-args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\""},{"apt-tools":"libmsquic","dotnet-version":"8.0","benchmark-expire-min":15,"benchmark-timeout-min":10,"benchmark-client-run-script-path":".github/scripts/benchmark-client-run.sh","benchmark-server-run-script-path":".github/scripts/benchmark-server-run.sh","benchmark-server-stop-script-path":".github/scripts/benchmark-server-stop.sh","benchmark-client-run-script-args":"--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 1 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c\" --build-args \"\"","benchmark-server-run-script-args":"--run-args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1x1,protocol:h2c\""},{"apt-tools":"libmsquic","dotnet-version":"8.0","benchmark-expire-min":15,"benchmark-timeout-min":10,"benchmark-client-run-script-path":".github/scripts/benchmark-client-run.sh","benchmark-server-run-script-path":".github/scripts/benchmark-server-run.sh","benchmark-server-stop-script-path":".github/scripts/benchmark-server-stop.sh","benchmark-client-run-script-args":"--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"--p:UseNuGetClient=6.14\"","benchmark-server-run-script-args":"--run-args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\""},{"apt-tools":"libmsquic","dotnet-version":"8.0","benchmark-expire-min":15,"benchmark-timeout-min":10,"benchmark-client-run-script-path":".github/scripts/benchmark-client-run.sh","benchmark-server-run-script-path":".github/scripts/benchmark-server-run.sh","benchmark-server-stop-script-path":".github/scripts/benchmark-server-stop.sh","benchmark-client-run-script-args":"--run-args \"-u http://${BENCHMARK_SERVER_NAME}:5000 --protocol h2c -s CI --rounds 3 --channels 28 --streams 1 --serialization messagepack --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\" --build-args \"\"","benchmark-server-run-script-args":"--run-args \"-u http://0.0.0.0:5000 --protocol h2c --validate true --tags legend:messagepack-h2c-linux,streams:1,protocol:h2c\""}]}

while [ $# -gt 0 ]; do
  case $1 in
    # required
    --benchmark-config-path) _BENCHMARK_CONFIG_FILE=$2; shift 2; ;;
    # optional
    --debug) _DEBUG=$2; shift 2; ;;
    *) shift ;;
  esac
done

function print {
  echo "$(date "+%Y-%m-%d %H:%M:%S") INFO(${FUNCNAME[1]:-unknown}): $*"
}
function title {
  echo ""
  echo "$(date "+%Y-%m-%d %H:%M:%S") INFO(${FUNCNAME[1]:-unknown}): # $*"
}
function error {
  echo "$(date "+%Y-%m-%d %H:%M:%S") ERRO(${FUNCNAME[1]:-unknown}): # $*" >&2
}
function debug {
  if [[ "${_DEBUG}" == "true" ]]; then
    echo "$(date "+%Y-%m-%d %H:%M:%S") DEBG(${FUNCNAME[1]:-unknown}): $*"
  fi
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
function validate_config {
  # Check if the general entry exists in the specified yaml file.
  if [[ "${general_keys[*]}" == "" ]]; then
    error "No general entry found in the specified yaml file."
    exit 1
  fi

  # Check if the template_string_keys entry exists in the specified yaml file.
  if [[ "${template_string_keys[*]}" == "" ]]; then
    error "No  entry found in the specified yaml file."
    exit 1
  fi

  # Check if the jobs entry exists in the specified yaml file.
  if [[ "${filtered_jobs[*]}" == "" ]]; then
    error "No jobs entry found in the specified yaml file."
    exit 1
  fi
}

title "Arguments:"
print "  --benchmark-config-path=${_BENCHMARK_CONFIG_FILE}"
print "  --debug=${_DEBUG:=false}"

title "Parameters:"
readonly template_key_endswith=run-script-args
print "  * template_key_endswith=${template_key_endswith}"

if [[ "${CI:-""}" == "" ]]; then
  GITHUB_OUTPUT="/dev/null"
fi

if [[ ! -f "${_BENCHMARK_CONFIG_FILE}" ]]; then
  error "File not found: ${_BENCHMARK_CONFIG_FILE}"
  exit 1
fi

title "Gathering config keys"
general_json_elements=()
matrix_includes_json_array="["
keys_json=$(yq -o json eval 'keys | map(select(. != "jobs"))' "$_BENCHMARK_CONFIG_FILE" | jq -c)
mapfile -t general_keys < <(echo "$keys_json" | jq --arg template_string_key "$template_key_endswith" -r '.[] | select(. | endswith($template_string_key) | not)')
mapfile -t template_string_keys < <(echo "$keys_json" | jq --arg template_string_key "$template_key_endswith" -r '.[] | select(. | endswith($template_string_key))')
jobs_json=$(yq eval -o=json '.jobs' "$_BENCHMARK_CONFIG_FILE")
mapfile -t filtered_jobs < <(echo "$jobs_json" | jq -c '.[]')

title "Validate config"
validate_config

title "Scan config and obtain json elements (General keys)"
for general_key in "${general_keys[@]}"; do
  obtained_value=$(yq eval ".$general_key" "$_BENCHMARK_CONFIG_FILE")
  print "${general_key}=$obtained_value"
  if [[ "$(is_number "$obtained_value")" == "1" ]]; then
    general_json=$(jq -c -n --arg key "$general_key" --argjson value "$obtained_value" '{
      ($key): $value
    }')
  else
    general_json=$(jq -c -n --arg key "$general_key" --arg value "$obtained_value" '{
      ($key): $value
    }')
  fi
  # echo "$general_json" # debug
  general_json_elements+=("$general_json")
done

title "Reflect the values defined in jobs into the placeholders for xxxx-run-script-args."
for job in "${filtered_jobs[@]}"; do
  json_elements=()
  args_json_elements=()

  debug "  job: ${job}"

  # Get server / client args by job
  for template_string_key in "${template_string_keys[@]}"; do
    # find template string key
    template_string_value=$(yq ".$template_string_key" "$_BENCHMARK_CONFIG_FILE")

    debug "  template_string_key=template_string_value: ${template_string_key}=$template_string_value"

    if [[ "$template_string_value" == "" || "$template_string_value" == "null" ]]; then
      error "Specified yaml not contains ${template_string_key}, please add ${template_string_key} key."
      exit 1
    fi

    # initialize assembled string
    assembled_string="$template_string_value"

    # extract placeholders from the template string
    placeholders=$(extract_placeholders "$template_string_value")

    debug "  placeholders: ${placeholders}"

    # extract values from the job, then replace the placeholder with the value.
    # {{ foo }} will be replaced with the value of the key "foo" in the job
    # If job missing key for the placeholder, it will be replaced with empty string.
    for key in $placeholders; do
      # get value from the job, set empty string if not found
      value=$(echo "$job" | jq -r ".$key // \"\"")
      debug "  templateKey=jobValue: ${key}=${value}"
      assembled_string=$(echo "$assembled_string" | sed -e "s/{{\s*$key\s*}}/$value/g")
    done

    print "${template_string_key}=$assembled_string"

    args_json=$(jq -c -n --arg key "$template_string_key" --arg value "$assembled_string" '{
      ($key): $value
    }')
    args_json_elements+=("$args_json")
  done
  json_elements=("${general_json_elements[@]}" "${args_json_elements[@]}")
  matrix_includes_json_array+=$(jq -c -s add <<< "${json_elements[@]}")
  matrix_includes_json_array+=","
done

title "Output Matrix json"
matrix_includes_json_array="${matrix_includes_json_array%,}"
matrix_includes_json_array+=']'
json_output=$(jq -c -n --argjson matrix_includes "$matrix_includes_json_array" '{
  include: $matrix_includes
}')

print "Pretty print Matrix json for debug"
echo "$json_output" | jq

print "Output for GITHUB_OUTPUT"
echo "matrix=$json_output" | tee -a "$GITHUB_OUTPUT"
