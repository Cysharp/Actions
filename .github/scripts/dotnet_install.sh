#!/bin/bash
set -euo pipefail

# Install .NET SDK over ssh
#
# Sample usage:
# $ ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@4.215.238.2 'bash -s -- --dotnet-version 8.0' < ./scripts/dotnet_install.sh
# $ ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@4.215.237.255 'bash -s -- --dotnet-version 8.0' < ./scripts/dotnet_install.sh
# $ echo $?
#

function usage {
  cat <<EOF
usage: $(basename $0) [options]
Options:
  --dotnet-version string  Version of dotnet sdk to install (default: 8.0)
  --help                   Show this help message

Examples:
  1. Install dotnet sdk version 8.0 over ssh
    $ ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@255.255.255.255 'bash -s -- --dotnet-version 8.0' < ./scripts/$(basename $0).sh
    $ echo \$?             # <- use \$? to get the exit code of the remote command
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    # optional
    --dotnet-version) _DOTNET_VERSION=$2; shift 2; ;;
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
print "--dotnet-version=${_DOTNET_VERSION:="8.0"}"

title "Constants:"
print "  * MACHINE_NAME=$(hostname)"

# install dotnet (dotnet-install.sh must be downloaded before running script)
title "Install dotnet sdk version: ${_DOTNET_VERSION}"
sudo bash /opt/dotnet-install.sh --channel "${_DOTNET_VERSION}" --install-dir /usr/share/dotnet

# link dotnet to /usr/local/bin
title "Link to /usr/local/bin/dotnet"
if [[ ! -h "/usr/local/bin/dotnet" ]]; then
  sudo ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet
fi

# show dotnet verison
title "Show installed dotnet sdk versions"
print "dotnet sdk versions (list): $(dotnet --list-sdks)"
print "dotnet sdk version (default): $(dotnet --version)"
