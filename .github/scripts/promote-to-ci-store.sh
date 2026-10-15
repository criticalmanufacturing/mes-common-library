#!/usr/bin/env bash
#
# Promotes this run's freshly-built, tested packages into the persistent,
# cross-run CI package store: a hidden/prerelease GitHub Release
# ("internal/ci-latest") holding one asset per package: a cmf package zip, or
# an npm-packed CLI plugin tarball (utils/plugins/). Only call this
# after the Tests flow has actually run and passed for this run - see
# _build-changed.yml's "promote" job condition.
#
# Usage: promote-to-ci-store.sh <downloadedArtifactsDir>
#   <downloadedArtifactsDir> must contain one subdirectory per package
#   (i.e. artifacts downloaded WITHOUT merge-multiple), each holding either:
#     - a cmf package's single "<packageId>.<version>.zip", in a directory
#       named after its packageId, or
#     - a plugin's single "<prefix>-<version>.tgz" (npm pack output), in a
#       directory named after that tarball prefix.
set -euo pipefail

ARTIFACTS_DIR="${1:?Usage: promote-to-ci-store.sh <downloadedArtifactsDir>}"
CI_STORE_TAG="internal/ci-latest"

if ! gh release view "$CI_STORE_TAG" >/dev/null 2>&1; then
  echo "Creating CI package store release ($CI_STORE_TAG)..."
  gh release create "$CI_STORE_TAG" \
    --prerelease \
    --title "CI package store (internal - do not use)" \
    --notes "Internal store of the last known-good, tested build of every package. Not a real release - do not reference from documentation, downstream tooling, or customers. Managed entirely by .github/workflows/_build-changed.yml."
fi

shopt -s nullglob
for package_dir in "$ARTIFACTS_DIR"/*/; do
  package_id="$(basename "$package_dir")"
  package_files=("$package_dir"*.zip "$package_dir"*.tgz)

  if [ ${#package_files[@]} -eq 0 ]; then
    echo "::warning::No zip or tgz found under $package_dir for package '$package_id', skipping."
    continue
  fi

  package_file="${package_files[0]}"
  package_name="$(basename "$package_file")"

  # Clean up any stale asset for the same package at a different version, so
  # the store doesn't grow unbounded across version bumps. Assets are named
  # "<packageId>.<version>.zip" or "<prefix>-<version>.tgz" (npm versions may
  # carry a prerelease suffix) - match that shape exactly for this package.
  escaped_id="$(printf '%s' "$package_id" | sed 's/[.[\*^$]/\\&/g')"
  case "$package_name" in
    *.tgz) stale_pattern="^${escaped_id}-[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?\.tgz$" ;;
    *)     stale_pattern="^${escaped_id}\.[0-9]+\.[0-9]+\.[0-9]+\.zip$" ;;
  esac
  mapfile -t existing_assets < <(gh release view "$CI_STORE_TAG" --json assets --jq '.assets[].name')
  for asset in "${existing_assets[@]}"; do
    if [[ "$asset" =~ $stale_pattern ]] && [ "$asset" != "$package_name" ]; then
      echo "Removing stale CI store asset $asset (superseded by $package_name)"
      gh release delete-asset "$CI_STORE_TAG" "$asset" --yes
    fi
  done

  echo "Promoting $package_name to the CI package store..."
  gh release upload "$CI_STORE_TAG" "$package_file" --clobber
done
