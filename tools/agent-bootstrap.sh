#!/usr/bin/env bash
set -euo pipefail

origin_configured() {
  git remote get-url origin >/dev/null 2>&1
}

try_add_origin() {
  if origin_configured; then
    return 0
  fi

  if [[ -n "${GITHUB_REPOSITORY:-}" ]]; then
    remote_url="https://github.com/${GITHUB_REPOSITORY}.git"
    echo "Configuring origin from GITHUB_REPOSITORY: ${remote_url}"
    git remote add origin "${remote_url}"
    origin_configured && return 0
  fi

  if command -v gh >/dev/null 2>&1; then
    repo_name="$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null || true)"
    if [[ -n "${repo_name}" ]]; then
      remote_url="https://github.com/${repo_name}.git"
      echo "Configuring origin from GitHub CLI context: ${remote_url}"
      git remote add origin "${remote_url}"
      origin_configured && return 0
    fi
  fi

  return 1
}

echo "Running cloud-agent preflight for IHeartFiction..."
echo "Detected .NET SDK: $(dotnet --version)"

if ! try_add_origin; then
  echo "WARNING: Could not infer origin remote automatically."
  echo "Run: git remote add origin https://github.com/SheepReaper/IHeartFiction.git"
else
  echo "origin remote: $(git remote get-url origin)"
fi

echo "Restoring dependencies..."
dotnet restore

echo "Preflight complete."
