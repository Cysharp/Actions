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
    echo "  --debug                       bool   Show debug output pr not. (default: false)"
    echo "  --dry-run                     bool   Show the command that would be run, but do not run it. (default: true)"
    echo "  --help                               Show this help message"
    echo ""
    echo "Examples:"
    echo "  1. Dryrun create Benchmark Environment name foobar"
    echo "     bash ./.github/scripts/$(basename $0) --catalog-name 'pulumi' --dev-center-name 'cysharp-devcenter' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'foobar' --project-name 'dve' --minutes 15 --dry-run true"
    echo "  2. Dryrun create Benchmark Environment name foobar, with debug output"
    echo "     bash ./.github/scripts/$(basename $0) --catalog-name 'pulumi' --dev-center-name 'cysharp-devcenter' --environment-definition-name 'Benchmark' --environment-type 'benchmark' --name 'foobar' --project-name 'dve' --minutes 15 --dry-run true --debug true"
    echo "  3. Create Benchmark Environment name foobar"
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
    --debug) _DEBUG=$2; shift 2; ;;
    --help) usage; exit 1; ;;
    *) shift ;;
  esac
done

function print() {
  echo "$*"
}
function debug() {
  if [[ "${debug}" == "true" ]]; then
    echo "DEBUG: $*"
  fi
}
function create() {
  $dryrun az devcenter dev environment create --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name" --catalog-name "$catalog_name" --environment-definition-name "$environment_definition_name" --environment-type "$environment_definition_name" --parameters "$(jq -c -n --arg n "$name" '{name: $n}')" --expiration-date "$new_expiration_time"
  output_expiration=$new_expiration_time
  github_output
}
function delete() {
  $dryrun az devcenter dev environment delete --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name" --yes
}
function extend() {
  $dryrun az devcenter dev environment update-expiration-date --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name" --expiration "$new_expiration_time"
  output_expiration=$new_expiration_time
  github_output
}
function list() {
  az devcenter dev environment list --dev-center-name "$dev_center_name" --project-name "$project_name" | jq -c ".[] | select(.name == \"$name\")"
}
function show() {
  az devcenter dev environment show --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name"
}
function devcenter_output() {
  az devcenter dev environment show-outputs --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name"
}
function github_output() {
  if [[ "${CI:=""}" != "" ]]; then
    echo "expiration=$output_expiration" | tee -a "$GITHUB_OUTPUT"
  fi
}

print "Arguments: "
print "  --catalog-name=${catalog_name="$_CATALOG_NAME"}"
print "  --dev-center-name=${dev_center_name="$_DEVCENTER_NAME"}"
print "  --environment-definition-name=${environment_definition_name="$_ENVIRONMENT_DEFINITION_NAME"}"
print "  --environment-type=${environment_type="$_ENVIRONMENT_TYPE"}"
print "  --name=${name="$_NAME"}"
print "  --project-name=${project_name="$_PROJECT_NAME"}"
print "  --minutes=${minutes="${_MINUTES:=20}"}"
print "  --debug=${debug=${_DEBUG:=false}}"
print "  --dry-run=${_DRYRUN:=true}"

dryrun=""
if [[ "$_DRYRUN" == "true" ]]; then
  dryrun="echo (dryrun) "
fi

function main() {
  # set expiration date from now
  new_expiration_time=$(date -ud "$minutes minutes" +"%Y-%m-%dT%H:%M:%SZ") # 2024-07-24T05:31:52Z
  new_expiration_epoch=$(date -d "$new_expiration_time" +%s)

  print "Checking $name is already exists or not."
  json=$(list)

  # NEW! -> create
  if [[ "$json" == "" ]]; then
    print "! No existing $name found, creating new Deployment Environment."
    create

    print "Complete creating benchmark environment $name"
  else
    # EXISTING! -> reuse/expand or delete
    print "Existing $name found, checking status..."
    provisioningState=$(echo "$json" | jq -r ".provisioningState")
    name=$(echo "$json" | jq -r ".name")
    current_expiration_time=$(echo "$json" | jq -r ".expirationDate") # 2024-07-24T07:00:00+00:00
    current_expiration_epoch=$(date -d "$current_expiration_time" +%s)
    output_expiration=$current_expiration_time

    case "$provisioningState" in
      "Succeeded")
        # Let's reuse existing if possible. existing benchmark will process kill and re-run new bench.
        print "$name status is $provisioningState, checking expiration limit status."
        if [[ "$current_expiration_time" == "null" ]]; then
          print "! Expiration date is not set, setting $new_expiration_time"
          extend
        else
          # Check expirationDate is shorter than requested timeout
          new_expiration_epoch=$(date -ud "$new_expiration_time" +%s)
          time_diff=$((current_expiration_epoch - new_expiration_epoch))
          debug "current_expiration_time - new_expiration_time = $current_expiration_time - $new_expiration_time"
          debug "current_expiration_epoch - new_expiration_epoch = $current_expiration_epoch - $new_expiration_epoch = $time_diff"
          if [[ "$time_diff" -le 0 ]]; then
            print "! Current expirationDate is shorter than requested timeout ${minutes}m, extending from $current_expiration_time to $new_expiration_time"
            extend
          else
            print "Current expirationDate is longer than requested timeout ${minutes}m, no action required. Expired at $current_expiration_time"
          fi
        fi

        print "Complete creating benchmark environment $name"
        ;;
      "Preparing" | "Creating" | "Updating")
        # Let's wait until succeeded
        print "$name status is $provisioningState, waiting to be succeeded."
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
            print "$name creation was $provisioningState, quitting. There is no possibility of auto recover, should be Azure outage or Pulumi have pottential bug."
            exit 1 # no possibility of recover
          fi

          print "$name is still $provisioningState, waiting... (elpased ${SECONDS}sec)"

          sleep 5
        done

        print "$name successfully create."
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
            print "$name succeessfully deleted, automatically re-run from beginning."
            break
          else
            if current=$(show); then
              provisioningState=$(echo "$current" | jq -r ".provisioningState")
              if [[ "$provisioningState" == "Failed" ]]; then
                # re-run
                print "$name status is $provisioningState, automatically re-run from beginning"
                break
              fi
            fi
          fi

          print "$name is still $provisioningState, waiting... (elpased ${SECONDS}sec)"
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
  fi
}

main
