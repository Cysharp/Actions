#!/bin/bash
set -euo pipefail

# Clean up Failed Benchmark Environment created by Azure Development Environment.
#
# Sample usage:
# $ bash ./.github/scripts/clean_benchmark_environment.sh --dev-center-name 'cysharp-devcenter'--project-name 'dve' --dry-run true
#

function usage {
  cat <<EOF
usage: $(basename $0) [options]
Required:
  --dev-center-name             string The name of the dev center.
  --project-name                string The name of the project.
Options:
  --state                       string State of the environment. (default: Failed)
  --debug                       bool   Show debug output pr not. (default: false)
  --dry-run                     bool   Show the command that would be run, but do not run it. (default: true)
  --help                               Show this help message

Examples:
  1. Dryrun clean up Benchmark Environment
      bash ./.github/scripts/$(basename $0) --dev-center-name 'cysharp-devcenter' --project-name 'dve' --state Failed --dry-run true
  3. Clean up Benchmark Environment
      bash ./.github/scripts/$(basename $0) --dev-center-name 'cysharp-devcenter' --project-name 'dve' --state Failed --dry-run false
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    # required
    --dev-center-name) _DEVCENTER_NAME=$2; shift 2; ;;
    --project-name) _PROJECT_NAME=$2; shift 2; ;;
    # optional
    --state) _STATE=$2; shift 2; ;; # Failed, Succeeded, All
    --dry-run) _DRYRUN=$2; shift 2; ;;
    --debug) _DEBUG=$2; shift 2; ;;
    --help) usage; exit 1; ;;
    *) shift ;;
  esac
done

function print() {
  echo "$*"
}
function debug() {
  if [[ "${_DEBUG}" == "true" ]]; then
    echo "DEBUG: $*"
  fi
}
function enable_debug_mode {
  if [[ "${_DEBUG}" == "true" ]]; then
    set -x
  fi
}
# reset expiration date from now
function reset_expiration_date {
  local minutes=$1
  new_expiration_time=$(date -ud "$minutes minutes" +"%Y-%m-%dT%H:%M:%SZ") # 2024-07-24T05:31:52Z
}
# delete environment
function delete() {
  local n=$1
  $dryrun az devcenter dev environment delete --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$n" --yes --no-wait
}
# extend environment expiration date to requested time
function extend() {
  local n=$1
  $dryrun az devcenter dev environment update-expiration-date --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$n" --expiration "$new_expiration_time"
  output_expiration=$new_expiration_time
  github_output
}
# list environment
function list() {
  if [[ "${_STATE}" == "All" ]]; then
    az devcenter dev environment list --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" | jq -c ".[]"
  else
    az devcenter dev environment list --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" | jq -c ".[] | select(.provisioningState == \"${_STATE}\")"
  fi
}
# show environment error detail
function show_error_outputs() {
  local n=$1
  az devcenter dev environment show-outputs --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$n" | jq -r ".outputs"
  az devcenter dev environment list --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" | jq -r ".[] | select(.name == \"$n\") | .error"
}
# output expiration date to GitHub Actions output
function github_output() {
  if [[ "${CI:=""}" != "" ]]; then
    echo "expiration=$output_expiration" | tee -a "$GITHUB_OUTPUT"
  fi
}
function main() {
  print "Checking Failed environments are exists or not."
  readarray -t jsons < <(list)

  if [[ "${#jsons[@]}" == "0" ]]; then
    print "! No failed environment found, exiting..."
    exit
  fi

  # delete
  print "Failed environments are found, deleting..."
  for environment in "${jsons[@]}"; do
    provisioningState=$(echo "$environment" | jq -r ".provisioningState")
    name=$(echo "$environment" | jq -r ".name")

    print "! $name status is $provisioningState, showing error reason, set auto-expire and delete existing..."
    show_error_outputs "$name"
    reset_expiration_date "1"
    extend "$name"
    delete "$name"
  done
}

print "Arguments: "
print "  --dev-center-name=${_DEVCENTER_NAME}"
print "  --project-name=${_PROJECT_NAME}"
print "  --state=${_STATE:="Failed"}"
print "  --debug=${_DEBUG:="false"}"
print "  --dry-run=${_DRYRUN:="true"}"

enable_debug_mode

dryrun=""
if [[ "$_DRYRUN" == "true" ]]; then
  dryrun="echo (dryrun) "
fi

main
