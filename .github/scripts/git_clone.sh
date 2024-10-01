#!/bin/bash
set -euo pipefail

# git clone over ssh
#
# ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@4.215.238.2 'bash -s -- --branch benchmark --owner Cysharp --repo MagicOnion' < ./scripts/git_clone.sh
# $ echo $?

function usage {
  cat <<EOF
usage: $(basename $0) --build-csproj <string> --repo <string> [options]
Required:
  --repo          string  Repository name to clone
Options:
  --branch        string  Branch name to checkout (default: main)
  --owner         string  Repository owner (default: Cysharp)
  --help                  Show this help message

Examples:
  1. git clone Cysharp/MagicOnion main branch
    $ ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@255.255.255.255 'bash -s -- --branch main --owner Cysharp --repo MagicOnion' < ./scripts/$(basename $0).sh"
    $ echo \$?            # <- use \$? to get the exit code of the remote command'
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    # required
    --repo) _REPO=$2; shift 2; ;;
    # optional
    --branch) _BRANCH=$2; shift 2; ;;
    --owner) _OWNER=$2; shift 2; ;;
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
print "  --owner=${_OWNER:="Cysharp"}"
print "  --repo=${_REPO}"
print "  --branch=${_BRANCH:="main"}"

readonly clone_path="$HOME/github/$_REPO"
title "Constants:"
print "  * clone_path=${clone_path}"
print "  * MACHINE_NAME=$(hostname)"

# git clone cysharp repo
title "# git clone $_OWNER/$_REPO"
if [[ -d "$clone_path" && ! -d "$clone_path/.git" ]]; then
  rm -rf "$clone_path" # remove non-git directory
fi
mkdir -p "$(dirname "$clone_path")"
if [[ ! -d "$clone_path" ]]; then
  git clone "https://github.com/$_OWNER/$_REPO" "$clone_path"
fi

# list files
title "# List cloned files"
ls "$clone_path"

# git pull
title "# git pull $_BRANCH"
pushd "$clone_path"
  git merge --abort || true
  git fetch # get remote info first
  git switch "$_BRANCH"
  git reset --hard "origin/$_BRANCH" # reset to origin to avoid conflicts
  git clean -fdx # clean all untracked files
  git pull --ff-only # pull latest changes, fast-forwad and error if not possible
  git reset --hard HEAD
popd
