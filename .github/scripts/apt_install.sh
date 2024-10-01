#!/bin/bash
set -euo pipefail

# Install apt package over ssh
#
# Sample usage:
# $ ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@4.215.238.2 'bash -s -- --tools "libmsquic"' < ./scripts/apt_install.sh
# $ echo $?
#

function usage {
  cat <<EOF
usage: $(basename $0) [options]
Options:
  --tools string  command separated list of tools to install (default: "")
  --help          Show this help message

Examples:
  1. Install dotnet sdk version 8.0 over ssh
    $ ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@255.255.255.255 'bash -s -- --dotnet-version 8.0' < ./scripts/$(basename $0).sh
    $ echo \$?             # <- use \$? to get the exit code of the remote command
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    # optional
    --tools) _TOOLS=$2; shift 2; ;;
    --help) usage; exit 1; ;;
    *) shift ;;
  esac
done

function print {
  echo "$*"
}
function title {
  echo ""
  echo "# $*"
}

# parameter setup
title "Arguments:"
print "  --tools=${_TOOLS:=""}"

title "Constants:"
print "  * MACHINE_NAME=$(hostname)"

if [[ "$_TOOLS" == "" ]]; then
  print "No tools to install, done."
  exit 0
fi

# create array from comma separated string, by
mapfile -t tool_array < <(echo "$_TOOLS" | tr ',' '\n')

# install dotnet (dotnet-install.sh must be downloaded before running script)
title "Install tools: ${tool_array[*]}"
for tool in "${tool_array[@]}"; do
  print "Install $tool"
  sudo apt-get install -y "$tool"
done

print "Installation completed."
