#!/bin/bash
set -euo pipefail

# Clean up Failed Benchmark Environment created by Azure Development Environment.
#
# Sample usage:
# $ bash ./.github/scripts/clean_benchmark_environment.sh --dev-center-name 'cysharp-devcenter'--project-name 'dve' --dry-run true
#

function usage {
    echo "usage: $(basename $0) [options]"
    echo "Required:"
    echo "  --dev-center-name             string The name of the dev center."
    echo "  --project-name                string The name of the project."
    echo "Options:"
    echo "  --debug                       bool   Show debug output pr not. (default: false)"
    echo "  --dry-run                     bool   Show the command that would be run, but do not run it. (default: true)"
    echo "  --help                               Show this help message"
    echo ""
    echo "Examples:"
    echo "  1. Dryrun clean up Benchmark Environment"
    echo "     bash ./.github/scripts/$(basename $0) --dev-center-name 'cysharp-devcenter' --project-name 'dve' --dry-run true"
    echo "  3. Clean up Benchmark Environment"
    echo "     bash ./.github/scripts/$(basename $0) --dev-center-name 'cysharp-devcenter' --project-name 'dve' --dry-run false"
}

while [ $# -gt 0 ]; do
  case $1 in
    # required
    --dev-center-name) _DEVCENTER_NAME=$2; shift 2; ;;
    --project-name) _PROJECT_NAME=$2; shift 2; ;;
    # optional
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
function delete() {
  $dryrun az devcenter dev environment delete --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name" --yes --no-wait
}
function list() {
  az devcenter dev environment list --dev-center-name "$dev_center_name" --project-name "$project_name" | jq -c ".[] | select(.provisioningState == \"Failed\")"
}
function show_error_outputs() {
  az devcenter dev environment show-outputs --dev-center-name "$dev_center_name" --project-name "$project_name" --name "$name" | jq -r ".outputs"
  az devcenter dev environment list --dev-center-name "$dev_center_name" --project-name "$project_name" | jq -r ".[] | select(.name == \"$name\") | .error"
}

print "Arguments: "
print "  --dev-center-name=${dev_center_name="$_DEVCENTER_NAME"}"
print "  --project-name=${project_name="$_PROJECT_NAME"}"
print "  --debug=${debug=${_DEBUG:=false}}"
print "  --dry-run=${_DRYRUN:=true}"

dryrun=""
if [[ "$_DRYRUN" == "true" ]]; then
  dryrun="echo (dryrun) "
fi

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

    print "! $name status is $provisioningState, showing error reason and delete existing..."
    show_error_outputs
    delete

  done
}

main
