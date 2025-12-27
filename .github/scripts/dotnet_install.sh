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
  echo "$(date "+%Y-%m-%d %H:%M:%S") INFO(${FUNCNAME[1]:-main}): $*"
}
function error {
  echo "$(date "+%Y-%m-%d %H:%M:%S") ERRO(${FUNCNAME[1]:-main}): ERROR $*" >&2
}
function title {
  echo ""
  echo "$(date "+%Y-%m-%d %H:%M:%S") INFO(${FUNCNAME[1]:-main}): # $*"
}
function download_url {
  local retry_count=0
  while [[ $retry_count -lt $max_retries ]]; do
    if curl -L -s --fail-with-body --retry 3 --retry-delay 10 --retry-max-time 60 -o "$HOME/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh; then
      print "Download succeeded."
      break
    fi

    retry_count=$((retry_count + 1))
    if [[ $retry_count -ge $max_retries ]]; then
      error "Failed to download after $max_retries attempts."
      exit 1
    else
      print "Download failed, retrying in $retry_delay seconds... (Attempt: $retry_count)"
      sleep "$retry_delay"
    fi
  done
}

# parameter setup
title "Arguments:"
print "--dotnet-version=${_DOTNET_VERSION:="8.0"}"

title "Constants:"
print "  * MACHINE_NAME=$(hostname)"
print "  * MAX_RETRIES=${max_retries:=3}"
print "  * RETRY_DELAY=${retry_delay:=10}"

# check required dotnet sdk is already installed
title "Check existing dotnet installation"
if command -v dotnet &> /dev/null; then
  installed_versions=$(dotnet --list-sdks | awk '{print $1}' | cut -d'.' -f1,2 | sort -u)
  print "Existing installed dotnet sdk versions: ${installed_versions}"
  for ver in $installed_versions; do
    if [[ "$ver" == "${_DOTNET_VERSION}"* ]]; then
      print "Required dotnet sdk version ${_DOTNET_VERSION} is already installed. Skipping installation."
      exit 0
    fi
  done
fi
print "required dotnet sdk version ${_DOTNET_VERSION} is not installed. Proceeding with installation."

# download dotnet-install.sh if not exists
title "Download dotnet-install script if not exists"
if [[ ! -f "$HOME/dotnet-install.sh" ]]; then
  print "dotnet installer now found, downloading..."
  download_url
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
