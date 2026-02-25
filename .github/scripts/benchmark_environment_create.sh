#!/bin/bash
set -euo pipefail

function usage {
  cat <<EOF
Usage: $(basename $0) [options]
Descriptions: Create Benchmark Environment by Azure Development Environment.

Required:
  --catalog-name                string The name of the catalog.
  --dev-center-name             string The name of the dev center.
  --environment-definition-name string The name of the environment definition.
  --environment-type            string The type of the environment.
  --name                        string The name of the environment.
  --project-name                string The name of the project.
Options:
  --expire-min                  int    The number of minutes until the environment expires. (default: 20)
  --debug                       bool   Show debug output pr not. (default: false)
  --dry-run                     bool   Show the command that would be run, but do not run it. (default: true)
  --location                    string The location of the environment. (default: "")
  --help                               Show this help message

Examples:
1. Dryrun create Benchmark Environment name foobar
   $ bash ./.github/scripts/$(basename $0) --catalog-name 'pulumi' --dev-center-name 'ade-devcenter-jp' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'foobar' --project-name 'ade-project-jp' --expire-min 15 --dry-run true
2. Dryrun create Benchmark Environment name foobar, with debug output
    $ bash ./.github/scripts/$(basename $0) --catalog-name 'pulumi' --dev-center-name 'ade-devcenter-jp' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'foobar' --project-name 'ade-project-jp' --expire-min 15 --dry-run true --debug true
3. Create Benchmark Environment name foobar
   $ bash ./.github/scripts/$(basename $0) --catalog-name 'pulumi' --dev-center-name 'ade-devcenter-jp' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'foobar' --project-name 'ade-project-jp' --expire-min 15 --dry-run false
4. Create Benchmark Environment name foobar-eastus with location eastus
   $ bash ./.github/scripts/$(basename $0) --catalog-name 'pulumi' --dev-center-name 'ade-devcenter-jp' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'foobar-eastus' --project-name 'ade-project-jp' --expire-min 15 --location eastus --dry-run false
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    # required
    --catalog-name) _CATALOG_NAME=$2; shift 2; ;;
    --dev-center-name) _DEVCENTER_NAME=$2; shift 2; ;;
    --environment-definition-name) _ENVIRONMENT_DEFINITION_NAME=$2; shift 2; ;;
    --environment-type) _ENVIRONMENT_TYPE=$2; shift 2; ;;
    --name) _NAME=$2; shift 2; ;;
    --project-name) _PROJECT_NAME=$2; shift 2; ;;
    # optional
    --expire-min) _MINUTES=$2; shift 2; ;;
    --dry-run) _DRYRUN=$2; shift 2; ;;
    --debug) _DEBUG=$2; shift 2; ;;
    --location) _LOCATION=$2; shift 2; ;;
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
function debug {
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
  new_expiration_epoch=$(date -d "$new_expiration_time" +%s)
}
# create environment
function create {
  $dryrun az devcenter dev environment create --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$_NAME" --catalog-name "$_CATALOG_NAME" --environment-definition-name "$_ENVIRONMENT_DEFINITION_NAME" --environment-type "$_ENVIRONMENT_TYPE" --parameters "$(jq -c -n --arg n "$_NAME" --arg l "$_LOCATION" '{name: $n, location: $l}')" --expiration-date "$new_expiration_time"
  github_output
}
# re-deploy environment (re-deploy)
function redeploy {
  $dryrun az devcenter dev environment deploy --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$_NAME" --parameters "$(jq -c -n --arg n "$_NAME" --arg l "${_LOCATION}" '{name: $n, location: $l}')" --expiration-date "$new_expiration_time"
}
# delete environment
function delete {
  $dryrun az devcenter dev environment delete --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$_NAME" --yes
}
# extend environment expiration date to requested time
function extend {
  $dryrun az devcenter dev environment update-expiration-date --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$_NAME" --expiration "$new_expiration_time"
  github_output
}
# list environment
function list {
  az devcenter dev environment list --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" | jq -c --arg name "$_NAME" '.[] | select(.name == $name)'
}
# show environment detail
function show {
  az devcenter dev environment show --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$_NAME"
}
# show environment error detail
function show_error_outputs {
  az devcenter dev environment show-outputs --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" --name "$_NAME" | jq -r ".outputs"
  az devcenter dev environment list --dev-center-name "$_DEVCENTER_NAME" --project-name "$_PROJECT_NAME" | jq -r --arg name "$_NAME" '.[] | select(.name == $name) | .error.message'
}
# output expiration date to GitHub Actions output
function github_output {
  echo "expiration=$new_expiration_time" | tee -a "$GITHUB_OUTPUT"
}
function main {
  print "Checking $_NAME is already exists or not."
  json=$(list)

  # NEW! -> create
  if [[ "$json" == "" ]]; then
    print "! $_NAME not found, creating new Deployment Environment. (expiration: ${_MINUTES}min)"
    reset_expiration_date "${create_timeout}" # 15 minutes for creation
    create

    reset_expiration_date "$_MINUTES"
    print "! $_NAME successfully create, extending expiration date to $new_expiration_time."
    extend

    print "Complete creating benchmark environment $_NAME, expire at $new_expiration_time"
    return
  fi

  # EXISTING! -> reuse/expand/delete
  print "$_NAME already existing, checking status..."
  provisioningState=$(echo "$json" | jq -r ".provisioningState")
  current_expiration_time=$(echo "$json" | jq -r ".expirationDate") # 2024-07-24T07:00:00+00:00
  current_expiration_epoch=$(date -d "$current_expiration_time" +%s)

  case "$provisioningState" in
    "Succeeded")
      # Let's reuse existing if possible. existing benchmark will process kill and re-run new bench.
      print "$_NAME status is $provisioningState, checking expiration limit status."

      reset_expiration_date "$_MINUTES"
      if [[ "$current_expiration_time" == "null" ]]; then
        print "! Expiration date is not set, setting $new_expiration_time"
        extend
      else
        # Check expirationDate is shorter than requested timeout
        time_diff=$((current_expiration_epoch - new_expiration_epoch))
        debug "current_expiration_time - new_expiration_time = $current_expiration_time - $new_expiration_time"
        debug "current_expiration_epoch - new_expiration_epoch = $current_expiration_epoch - $new_expiration_epoch = $time_diff"
        if [[ "$time_diff" -le 0 ]]; then
          print "! Current expirationDate is shorter than requested timeout ${_MINUTES}m, extending from $current_expiration_time to $new_expiration_time"
          extend
        else
          print "Current expirationDate is longer than requested timeout ${_MINUTES}m, no action required. Expired at $current_expiration_time"
        fi
      fi

      print "Complete creating benchmark environment $_NAME"
      ;;
    "Preparing" | "Creating" | "Updating")
      # Let's wait until succeeded
      print "! $_NAME status is $provisioningState, extend and wait to be succeeded. (expiration: ${_MINUTES}min)"
      reset_expiration_date "${create_timeout}"
      extend

      SECONDS=0
      while true; do
        # timeout
        now_epoch=$(date -u +%s)
        time_diff=$((new_expiration_epoch - now_epoch))
        debug "new_expiration_epoch - now_epoch = $new_expiration_epoch - $now_epoch = $time_diff"
        if [[ "$time_diff" -le 0 ]]; then
          print "Timeout reached, quitting. Final provisioningState: $provisioningState"
          exit 1 # no possibility of recover
        fi

        # watch
        current=$(show)
        provisioningState=$(echo "$current" | jq -r ".provisioningState")

        if [[ "$provisioningState" == "Succeeded" ]]; then
          break
        elif [[ "$provisioningState" == "Failed" ]]; then
          print "$_NAME creation was $provisioningState, quitting. There is no possibility of auto recover, should be Azure outage or Pulumi have pottential bug."
          exit 1 # no possibility of recover
        fi

        print "$_NAME is still $provisioningState, waiting... (elpased ${SECONDS}sec)"

        sleep 5
      done

      reset_expiration_date "$_MINUTES"
      print "! $_NAME successfully create, extending expiration date to $new_expiration_time."
      extend
      ;;
    "Failed")
      # Let's update first, then try to delete
      print "! $_NAME status is $provisioningState, showing error reason..."
      show_error_outputs

      # Let's re-deploy failed environment.
      print "$_NAME updating to re-deploy..."
      reset_expiration_date "${create_timeout}" # 15 minutes for creation
      if redeploy; then
        # re-run
        print "$_NAME succeessfully updated, automatically re-run from beginning."
      else
        print "$_NAME failed to update environment..."

        # Let's delete failed environment. We can do nothing.
        print "$_NAME deleting environment..."
        reset_expiration_date "1"
        delete
        print "$_NAME succeessfully deleted, automatically re-run from beginning."
      fi

      # re-run
      sleep 10 # sleep 10s to avoid Resource Group deletion error message when Create right after Deletion.
      main
      ;;
    "Deleting")
      # Let's wait until deletion complete. We can do nothing.
      print "$_NAME status is $provisioningState, wait for deletion..."
      reset_expiration_date "${delete_timeout}" # wait 10 minutes for deletion
      SECONDS=0
      while true; do
        # timeout
        now_epoch=$(date -u +%s)
        time_diff=$((new_expiration_epoch - now_epoch))
        debug "new_expiration_epoch - now_epoch = $new_expiration_epoch - $now_epoch = $time_diff"
        if [[ "$time_diff" -le 0 ]]; then
          print "Timeout reached, quitting. Final provisioningState: $provisioningState"
          exit 1 # no possibility of recover
        fi

        # watch
        deleted=$(list)
        if [[ "$deleted" == "" ]]; then
          # re-run
          print "$_NAME succeessfully deleted, automatically challenging recreation."
          break
        else
          if current=$(show); then
            provisioningState=$(echo "$current" | jq -r ".provisioningState")
            if [[ "$provisioningState" == "Failed" ]]; then
              # re-run
              print "$_NAME status is $provisioningState, automatically challenging recreation."
              break
            fi
          fi
        fi

        print "$_NAME is still $provisioningState, waiting... (elpased ${SECONDS}sec)"
        sleep 5
      done

      # re-run from beginning
      sleep 10 # sleep 10s to avoid Resource Group deletion error message when Create right after Deletion.
      main
      ;;
    *)
      print "provisioningState $provisioningState is not implemented, quitting, please check & delete on https://devportal.microsoft.com/ and re-run workflow to do benchmark..."
      exit 1 # no possibility of recover
      ;;
  esac
}

title "Arguments: "
print "  --catalog-name=${_CATALOG_NAME}"
print "  --dev-center-name=${_DEVCENTER_NAME}"
print "  --environment-definition-name=${_ENVIRONMENT_DEFINITION_NAME}"
print "  --environment-type=${_ENVIRONMENT_TYPE}"
print "  --location=${_LOCATION:=""}"
print "  --name=${_NAME}"
print "  --project-name=${_PROJECT_NAME}"
print "  --expire-min=${_MINUTES:=20}"
print "  --debug=${_DEBUG:=false}"
print "  --dry-run=${_DRYRUN:=true}"

readonly create_timeout=15
readonly delete_timeout=10
title "Constants:"
print "  * create_timeout=${create_timeout}"
print "  * delete_timeout=${delete_timeout}"

enable_debug_mode

dryrun=""
if [[ "$_DRYRUN" == "true" ]]; then
  dryrun="echo (dryrun) "
fi

if [[ "${CI:-""}" == "" ]]; then
  GITHUB_OUTPUT="/dev/null"
fi

title "Creating Benchmark Environment $_NAME"
main
