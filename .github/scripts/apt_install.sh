#!/bin/bash
set -euo pipefail

function usage {
  cat <<EOF
Usage: $(basename $0) [options]
Descriptions: Install apt package over ssh

Options:
  --tools string  command separated list of tools to install (default: "")
  --help          Show this help message

Examples:
  1. Install dotnet sdk version 8.0 over ssh
    $ ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@4.215.238.2 'bash -s -- --tools "libmsquic"' < ./scripts/apt_install.sh
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
  echo "$(date "+%Y-%m-%d %H:%M:%S") INFO(${FUNCNAME[1]:-unknown}): $*"
}
function title {
  echo ""
  echo "$(date "+%Y-%m-%d %H:%M:%S") INFO(${FUNCNAME[1]:-unknown}): # $*"
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
