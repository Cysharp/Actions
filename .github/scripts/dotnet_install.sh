#!/bin/bash
set -euo pipefail

function usage {
  cat <<EOF
Usage: $(basename $0) [options]
Descriptions: Install .NET SDK over ssh

Options:
  --dotnet-version string  Version of dotnet sdk to install (default: 8.0)
  --help                   Show this help message

Examples:
  1. Install dotnet sdk version 8.0 over ssh
    $ ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@4.215.238.2 'bash -s -- --dotnet-version 8.0' < ./scripts/dotnet_install.sh
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

# download dotnet-install.sh if not exists
title "Download dotnet-install script if not exists"
if [[ ! -f "$HOME/dotnet-install.sh" ]]; then
  print "dotnet installer now found, downloading..."
  curl -L -s --fail-with-body --retry 3 --retry-delay 10 --retry-max-time 60 -o "$HOME/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
fi

# install dotnet (dotnet-install.sh must be downloaded before running script)
title "Install dotnet sdk version: ${_DOTNET_VERSION}"
sudo bash "$HOME/dotnet-install.sh" --channel "${_DOTNET_VERSION}" --install-dir /usr/share/dotnet

# link dotnet to /usr/local/bin
title "Link to /usr/local/bin/dotnet"
if [[ ! -h "/usr/local/bin/dotnet" ]]; then
  sudo ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet
fi

# show dotnet verison
title "Show installed dotnet sdk versions"
print "dotnet sdk versions (list): $(dotnet --list-sdks)"
print "dotnet sdk version (default): $(dotnet --version)"
