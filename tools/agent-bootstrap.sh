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
source_generator_package_file_name="${source_generator_package_id}.${source_generator_package_version}.nupkg"

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

packages_have_same_content() {
  local first_package_path="$1"
  local second_package_path="$2"

  if [[ ! -f "${first_package_path}" || ! -f "${second_package_path}" ]]; then
    return 1
  fi

  if command -v python3 >/dev/null 2>&1; then
    python3 - "$first_package_path" "$second_package_path" <<'PY'
import hashlib
import sys
import zipfile

def entries(path):
    with zipfile.ZipFile(path) as package:
        result = []
        for info in sorted(package.infolist(), key=lambda item: item.filename):
            if (
                info.filename == "_rels/.rels"
                or info.filename.startswith("package/services/metadata/core-properties/")
                and info.filename.endswith(".psmdcp")
            ):
                continue

            with package.open(info) as stream:
                digest = hashlib.sha256(stream.read()).hexdigest().upper()
            result.append((info.filename, info.file_size, digest))
        return result

sys.exit(0 if entries(sys.argv[1]) == entries(sys.argv[2]) else 1)
PY
    return $?
  fi

  cmp -s "${first_package_path}" "${second_package_path}"
}

publish_local_source_generator_package() {
  mkdir -p "${local_package_feed}"

  echo "Stopping .NET build servers before repacking local analyzers..."
  dotnet build-server shutdown

  echo "Restoring source generator package dependencies..."
  dotnet restore "${source_generator_project}"

  echo "Packing source generator for local restore..."
  staging_feed="$(mktemp -d)"
  staged_package="${staging_feed}/${source_generator_package_file_name}"
  published_package="${local_package_feed}/${source_generator_package_file_name}"
  trap 'rm -rf "${staging_feed}"' RETURN

  dotnet pack "${source_generator_project}" --no-restore -p:PackageOutputPath="${staging_feed}"

  if packages_have_same_content "${published_package}" "${staged_package}"; then
    echo "Local source generator package content is unchanged; keeping existing package."
  else
    rm -f "${local_package_feed}/${source_generator_package_id}".*.nupkg
    cp "${staged_package}" "${published_package}"
  fi

  rm -rf "${staging_feed}"
  trap - RETURN

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
