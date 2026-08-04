#!/usr/bin/env bash
#
# Promotes this run's freshly-built, tested packages into the persistent,
# cross-run CI package store: a hidden/prerelease GitHub Release
# ("internal/ci-latest") holding one zip asset per package. Only call this
# after the Tests flow has actually run and passed for this run - see
# _build-changed.yml's "promote" job condition.
#
# Usage: promote-to-ci-store.sh <downloadedArtifactsDir>
#   <downloadedArtifactsDir> must contain one subdirectory per package,
#   named after its packageId (i.e. artifacts downloaded WITHOUT
#   merge-multiple), each holding that package's single "<packageId>.<version>.zip".
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
  zip_files=("$package_dir"*.zip)

  if [ ${#zip_files[@]} -eq 0 ]; then
    echo "::warning::No zip found under $package_dir for packageId '$package_id', skipping."
    continue
  fi

  zip_file="${zip_files[0]}"
  zip_name="$(basename "$zip_file")"

  # Clean up any stale asset for the same packageId at a different version, so
  # the store doesn't grow unbounded across version bumps. Assets are named
  # "<packageId>.<version>.zip" - match that shape exactly for this packageId.
  mapfile -t existing_assets < <(gh release view "$CI_STORE_TAG" --json assets --jq '.assets[].name')
  escaped_id="$(printf '%s' "$package_id" | sed 's/[.[\*^$]/\\&/g')"
  for asset in "${existing_assets[@]}"; do
    if [[ "$asset" =~ ^${escaped_id}\.[0-9]+\.[0-9]+\.[0-9]+\.zip$ ]] && [ "$asset" != "$zip_name" ]; then
      echo "Removing stale CI store asset $asset (superseded by $zip_name)"
      gh release delete-asset "$CI_STORE_TAG" "$asset" --yes
    fi
  done

  echo "Promoting $zip_name to the CI package store..."
  gh release upload "$CI_STORE_TAG" "$zip_file" --clobber
done
