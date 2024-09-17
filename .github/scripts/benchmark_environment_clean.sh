#!/bin/bash
set -euo pipefail

# Clean up Failed Benchmark Environment created by Azure Development Environment.
#
# Sample usage:
# $ bash ./.github/scripts/benchmark_environment_clean.sh --dev-center-name 'cysharp-devcenter'--project-name 'dve' --dry-run true
#

function usage {
  cat <<EOF
usage: $(basename $0) [options]
Required:
  --dev-center-name             string      The name of the dev center.
  --project-name                string      The name of the project.
Options:
  --state                       string      State of the environment. (default: Failed)
  --try-redeploy                true|false  Try to redeploy the environment before delete, this is useful when deletion failed due to stack not exists. (default: false)
  --debug                       bool        Show debug output pr not. (default: false)
  --dry-run                     bool        Show the command that would be run, but do not run it. (default: true)
  --help                                    Show this help message

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
    --try-redeploy) _TRYREDEPLOY=$2; shift 2; ;;
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
# re-deploy environment (re-deploy)
function redeploy {
  local n=$1
  $dryrun az devcenter dev environment deploy --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$n" --parameters "$(jq -c -n --arg n "$n" '{name: $n}')" --expiration-date "$new_expiration_time"
  github_output
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

print "Arguments: "
print "  --dev-center-name=${_DEVCENTER_NAME}"
print "  --project-name=${_PROJECT_NAME}"
print "  --state=${_STATE:="Failed"}"
print "  --try-redeploy=${_TRYREDEPLOY:="false"}"
print "  --debug=${_DEBUG:="false"}"
print "  --dry-run=${_DRYRUN:="true"}"

enable_debug_mode

dryrun=""
if [[ "$_DRYRUN" == "true" ]]; then
  dryrun="echo (dryrun) "
fi

print "Checking Failed environments are exists or not."
readarray -t jsons < <(list)

if [[ "${#jsons[@]}" == "0" ]]; then
  print "! No environment found, exiting..."
  exit
fi

# delete
print "Deployment environments are found, try deleting each..."
for environment in "${jsons[@]}"; do
  provisioningState=$(echo "$environment" | jq -r ".provisioningState")
  name=$(echo "$environment" | jq -r ".name")

  case "$provisioningState" in
    "Failed")
      print "! $name status is $provisioningState, showing error reason..."
      show_error_outputs "$name"

      if [[ "${_TRYREDEPLOY}" == "true" ]]; then
        print "! $name try redeploying..."
        reset_expiration_date "15"
        extend "$name"
        if redeploy "$name"; then
          print "  - $name redeploy success."
        else
          print "  - $name redeploy failed."
        fi
      fi
      ;;
    "Deleting")
      print "$name status is $provisioningState, skipping..."
      continue
      ;;
    *)
      print "$name status is $provisioningState, deleting..."
      ;;
  esac

  print "! $name set expire and delete existing..."
  reset_expiration_date "1"
  extend "$name"
  delete "$name"
done
