#!/bin/bash
set -euo pipefail

# Create Benchmark Environment by Azure Development Environment.
#
# Sample usage:
# $ bash ./.github/scripts/create_benchmark_environment.sh --catalog-name 'pulumi' --dev-center-name 'cysharp-devcenter' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'magiconion-123' --project-name 'dve' --minutes 15 --dry-run true
#

function usage {
    echo "usage: $(basename $0) [options]"
    echo "Required:"
    echo "  --catalog-name                string The name of the catalog."
    echo "  --dev-center-name             string The name of the dev center."
    echo "  --environment-definition-name string The name of the environment definition."
    echo "  --environment-type            string The type of the environment."
    echo "  --name                        string The name of the environment."
    echo "  --project-name                string The name of the project."
    echo "Options:"
    echo "  --minutes                     int    The number of minutes until the environment expires. (default: 15)"
    echo "  --dry-run                     bool   Show the command that would be run, but do not run it. (default: true)"
    echo "  --help                               Show this help message"
    echo ""
    echo "Examples:"
    echo "  1. Dryrun create Benchmark Environment name foobar"
    echo "     bash ./.github/scripts/$(basename $0) --catalog-name 'pulumi' --dev-center-name 'cysharp-devcenter' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'foobar' --project-name 'dve' --minutes 15 --dry-run true"
    echo "  2. Create Benchmark Environment name foobar"
    echo "     bash ./.github/scripts/$(basename $0) --catalog-name 'pulumi' --dev-center-name 'cysharp-devcenter' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'foobar' --project-name 'dve' --minutes 15 --dry-run false"
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
    --minutes) _MINUTES=$2; shift 2; ;;
    --dry-run) _DRYRUN=$2; shift 2; ;;
    --help) usage; exit 1; ;;
    *) shift ;;
  esac
done

function print() {
  echo "$*"
}
function create() {
  $dryrun az devcenter dev environment create --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name" --catalog-name "$catalog_name" --environment-definition-name "$environment_definition_name" --environment-type "$environment_definition_name" --parameters "$(jq -c -n --arg n "$name" '{name: $n}')" --expiration-date "$expiration"
}
function delete() {
  $dryrun az devcenter dev environment delete --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name" --yes
}
function extend() {
  $dryrun az devcenter dev environment update-expiration-date --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name" --expiration "$expiration"
}
function list() {
  az devcenter dev environment list --dev-center-name "$dev_center_name" --project-name "$project_name" | jq -c ".[] | select(.name == \"$name\")"
}
function show() {
  az devcenter dev environment show --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name"
}
function debug() {
  az devcenter dev environment show-outputs --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name"
}

print "Arguments: "
print "  --catalog-name=${catalog_name="$_CATALOG_NAME"}"
print "  --dev-center-name=${dev_center_name="$_DEVCENTER_NAME"}"
print "  --environment-definition-name=${environment_definition_name="$_ENVIRONMENT_DEFINITION_NAME"}"
print "  --environment-type=${environment_type="$_ENVIRONMENT_TYPE"}"
print "  --name=${name="$_NAME"}"
print "  --project-name=${project_name="$_PROJECT_NAME"}"
print "  --minutes=${minutes="${_MINUTES:=20}"}"
print "  --dry-run=${_DRYRUN:=true}"

dryrun=""
if [[ "$_DRYRUN" == "true" ]]; then
  dryrun="echo (dryrun) "
fi

function main() {
  # set expiration date from now
  expiration=$(date -u -d "$minutes minutes" +"%Y-%m-%dT%H:%M:%SZ")

  print "Checking $name is already exists or not."
  json=$(list)

  # NEW! -> create
  if [[ "$json" == "" ]]; then
    print "! No existing $name found, creating new Deployment Environment."
    create

    print "Complete creating benchmark environment $name"
    exit
  fi

  # EXISTING! -> reuse/expand or delete
  print "Existing $name found, checking status..."
  provisioningState=$(echo "$json" | jq -r ".provisioningState")
  name=$(echo "$json" | jq -r ".name")
  input_time=$(echo "$json" | jq -r ".expirationDate")
  current_time=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

  case "$provisioningState" in
    "Succeeded")
      # Let's reuse existing if possible. existing benchmark will process kill and re-run new bench.
      print "$name status is $provisioningState, checking expiration limit status."
      if [[ "$input_time" == "null" ]]; then
        print "! Expiration date is not set, setting $expiration"
        extend
      else
        # Check expirationDate and expand if less than 300s
        current_epoch=$(date -ud "$current_time" +%s)
        expiration_epoch=$(date -ud "$expiration" +%s)
        time_diff=$((expiration_epoch - current_epoch))
        if [[ "$time_diff" -le 300 ]]; then
          print "! Limit to expirationDate $time_diff is less than 300s. Extending to ${minutes}m. $input_time -> $expiration"
          extend
        else
          print "Limit to expirationDate $time_diff is more than 300s, no action required. $input_time -> $expiration"
        fi
      fi

      print "Complete creating benchmark environment $name"
      exit
      ;;
    "Preparing" | "Creating" | "Updating")
      # Let's wait until succeeded
      print "$name status is $provisioningState, waiting to be succeeded."
      SECONDS=0
      while true; do
        current=$(show)
        provisioningState=$(echo "$current" | jq -r ".provisioningState")

        if [[ "$provisioningState" == "Succeeded" ]]; then
          print "$name succeessfully created."
          break
        elif [[ "$provisioningState" == "Failed" ]]; then
          # no possibility of recover
          print "$name creation was $provisioningState, quitting. There is no possibility of auto recover, should be Azure outage or Pulumi have pottential bug."
          exit 1
        fi

        print "$name is still $provisioningState, waiting... (elpased ${SECONDS}sec)"

        # timeout
        current_time=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
        current_epoch=$(date -ud "$current_time" +%s)
        expiration_epoch=$(date -ud "$expiration" +%s)
        time_diff=$((expiration_epoch - current_epoch))
        if [[ "$time_diff" -le 0 ]]; then
          # no possibility of recover
          print "Timeout reached, quitting. Final provisioningState: $provisioningState"
          exit 1
        fi

        sleep 5
      done

      print "Complete creating benchmark environment $name"
      exit
      ;;
    "Failed")
      # Let's delete failed environment. We can do nothing.
      print "! $name status is $provisioningState, deleting..."
      delete

      # re-run
      print "$name succeessfully deleted, automatically re-run from beginning."
      sleep 10 # sleep 10s to avoid Resource Group deletion error message when Create right after Deletion.
      main
      ;;
    "Deleting")
      # Let's wait until deletion complete. We can do nothing.
      print "$name status is $provisioningState, wait for deletion..."
      while true; do
        deleted=$(list)

        if [[ "$deleted" == "" ]]; then
          # re-run
          print "$name succeessfully deleted, automatically re-run from beginning."
          sleep 10 # sleep 10s to avoid Resource Group deletion error message when Create right after Deletion.
          main
          exit 1
        else
          if current=$(show); then
            provisioningState=$(echo "$current" | jq -r ".provisioningState")
            if [[ "$provisioningState" == "Failed" ]]; then
              # re-run
              print "$name status is $provisioningState, automatically re-run from beginning"
              sleep 10 # sleep 10s to avoid Resource Group deletion error message when Create right after Deletion.
              main
              exit 1
            fi
          fi
        fi

        print "$name is still $provisioningState, waiting... (elpased ${SECONDS}sec)"

        # timeout
        current_time=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
        current_epoch=$(date -ud "$current_time" +%s)
        expiration_epoch=$(date -ud "$expiration" +%s)
        time_diff=$((expiration_epoch - current_epoch))
        if [[ "$time_diff" -le 0 ]]; then
          # no possibility of recover
          print "Timeout reached, quitting. Final provisioningState: $provisioningState"
          exit 1
        fi

        sleep 5
      done
      ;;
    *)
      print "provisioningState $provisioningState is not implemented, quitting, please check & delete on https://devportal.microsoft.com/ and re-run workflow to do benchmark..."
      exit 1
      ;;
  esac
}

main

if [[ "$CI" != "" ]]; then
  echo "expiration=$expiration" | tee -a "$GITHUB_OUTPUT"
fi
