#!/bin/bash
set -euo pipefail

# git clone over ssh
#
# ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@4.215.238.2 'bash -s -- --branch benchmark --owner Cysharp --repo MagicOnion' < ./scripts/git_clone.sh
# $ echo $?

function usage {
    echo "usage: $(basename $0) --build-csproj <string> --repo <string> [options]"
    echo "Required:"
    echo "  --repo          string  Repository name to clone"
    echo "Options:"
    echo "  --branch        string  Branch name to checkout (default: main)"
    echo "  --owner         string  Repository owner (default: Cysharp)"
    echo "  --help                  Show this help message"
    echo ""
    echo "Examples:"
    echo "  1. git clone Cysharp/MagicOnion main branch"
    echo "    ssh -o StrictHostKeyChecking=accept-new -i ~/.ssh/id_ed25519 azure-user@255.255.255.255 'bash -s -- --branch main --owner Cysharp --repo MagicOnion' < ./scripts/$(basename $0).sh"
    echo '    echo $? # use $? to get the exit code of the remote command'
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

function print() {
  echo ""
  echo "$*"
}

# parameter setup
owner="${_OWNER:="Cysharp"}"
repo="${_REPO}"
branch="${_BRANCH:="main"}"
clone_path="$HOME/github/$repo"

# show machine name
print "MACHINE_NAME: $(hostname)"

# git clone cysharp repo
print "# git clone $owner/$repo"
if [[ -d "$clone_path" && ! -d "$clone_path/.git" ]]; then
  rm -rf "$clone_path" # remove non-git directory
fi
mkdir -p "$(dirname "$clone_path")"
if [[ ! -d "$clone_path" ]]; then
  git clone "https://github.com/$owner/$repo" "$clone_path"
fi

# list files
print "# List cloned files"
ls "$clone_path"

# git pull
print "# git pull $branch"
pushd "$clone_path"
  git merge --abort || true
  git switch "$branch"
  git fetch
  git reset --hard "origin/$branch" # reset to origin to avoid conflicts
  git clean -fdx # clean all untracked files
  git pull --ff-only # pull latest changes, fast-forwad and error if not possible
  git reset --hard HEAD
popd
