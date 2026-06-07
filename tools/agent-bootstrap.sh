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

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_generator_project="${repo_root}/src/lib/IHFiction.SourceGenerators/IHFiction.SourceGenerators.csproj"
local_package_feed="${repo_root}/.artifacts/packages"
source_generator_package_id="IHFiction.SourceGenerators"
source_generator_package_version="0.1.0-local"

remove_source_generator_package_from_global_cache() {
  global_packages_path="$(dotnet nuget locals global-packages --list | sed -n 's/^global-packages:[[:space:]]*//p' | head -n 1)"
  if [[ -z "${global_packages_path}" ]]; then
    return 0
  fi

  cached_package_path="${global_packages_path}/$(printf '%s' "${source_generator_package_id}" | tr '[:upper:]' '[:lower:]')/${source_generator_package_version}"
  if [[ -d "${cached_package_path}" ]]; then
    echo "Removing cached ${source_generator_package_id} ${source_generator_package_version} package..."
    rm -rf "${cached_package_path}"
  fi
}

publish_local_source_generator_package() {
  mkdir -p "${local_package_feed}"
  rm -f "${local_package_feed}/${source_generator_package_id}".*.nupkg

  echo "Stopping .NET build servers before repacking local analyzers..."
  dotnet build-server shutdown

  echo "Restoring source generator package dependencies..."
  dotnet restore "${source_generator_project}"

  echo "Packing source generator for local restore..."
  dotnet pack "${source_generator_project}" --no-restore

  remove_source_generator_package_from_global_cache
}

echo "Running cloud-agent preflight for IHeartFiction..."
echo "Detected .NET SDK: $(dotnet --version)"

if ! try_add_origin; then
  echo "WARNING: Could not infer origin remote automatically."
  echo "Run: git remote add origin https://github.com/SheepReaper/IHeartFiction.git"
else
  echo "origin remote: $(git remote get-url origin)"
fi

publish_local_source_generator_package

echo "Restoring dependencies..."
dotnet restore

echo "Preflight complete."
