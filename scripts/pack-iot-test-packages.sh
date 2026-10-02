#!/usr/bin/env bash
#
# Packs the Connect IoT packages listed in the "iotTestPackages" array of the
# root package.json into a local, git-ignored "IoTRepository" folder, then
# runs .rebuildDatabase.sh against it so the driver/controller engine
# packages can be exercised locally.
#
# Each entry - whether listed in "iotTestPackages" or passed as a script
# argument - is either:
#   - a registry package spec, e.g. "@criticalmanufacturing/connect-iot-manager"
#     (the registry behind .npmrc does not support reliably discovering
#     packages by scope/prefix, so these are curated explicitly rather than
#     resolved from a "@scope/prefix-*" pattern), or
#   - a path (relative to the current directory, or absolute) to local
#     `cmf pack` output, useful for exercising a driver you just built without
#     publishing it first:
#       - a .tgz is the npm tarball itself and is copied as-is.
#       - a .zip is a packed cmfpackage (e.g. Cmf.Custom.Tests.1.0.0.zip); the
#         .tgz file(s) under its "runtimePackages/" folder are extracted
#         instead, since the zip itself isn't an npm package.
#       - either may be a glob pattern (e.g. "./features/**/Package/*.zip"),
#         including "**" for recursive matching; every file it matches is
#         processed. A local entry (.tgz/.zip, literal or glob) that matches
#         no file only logs a warning and is skipped; a registry package spec
#         that fails to resolve is a hard error.
#
#   "iotTestPackages": [
#     "@criticalmanufacturing/connect-iot-controller-engine",
#     "@criticalmanufacturing/connect-iot-controller-engine-driver-<...>",
#     "@criticalmanufacturing/connect-iot-manager",
#     "./features/IoTMTConnect/IoTMTConnect.Tests/Package/Cmf.Custom.Tests.1.0.0.zip"
#   ]
#
# Usage:
#   npm run iot:test-packages
#   scripts/pack-iot-test-packages.sh
#   scripts/pack-iot-test-packages.sh ./features/IoTMTConnect/IoTMTConnect.Tests/Package/Cmf.Custom.Tests.1.0.0.zip
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_DIR="$ROOT_DIR/IoTRepository"
PACKAGE_JSON="$ROOT_DIR/package.json"

mapfile -t PACKAGES < <(jq -r '.iotTestPackages[]? // empty' "$PACKAGE_JSON")
PACKAGES+=("$@")

if [ "${#PACKAGES[@]}" -eq 0 ]; then
  echo "::error::No packages found in the 'iotTestPackages' array of $PACKAGE_JSON, and none were passed as arguments." >&2
  exit 1
fi

echo "Packages to add (${#PACKAGES[@]}):"
printf '  - %s\n' "${PACKAGES[@]}"

echo "Recreating $REPO_DIR ..."
rm -rf "$REPO_DIR"
mkdir -p "$REPO_DIR"

for pkg in "${PACKAGES[@]}"; do
  case "$pkg" in
    *.tgz | *.zip)
      # $pkg may be a literal path or a glob (possibly with "**"); expand it
      # to the set of files that actually exist. nullglob makes an unmatched
      # glob disappear instead of being kept as a literal pattern string; a
      # plain literal path with no wildcards is left untouched by the
      # expansion and is filtered out below if it doesn't exist.
      shopt -s nullglob globstar
      candidates=( $pkg )
      shopt -u nullglob globstar

      matches=()
      if [ "${#candidates[@]}" -gt 0 ]; then
        for candidate in "${candidates[@]}"; do
          [ -f "$candidate" ] && matches+=("$candidate")
        done
      fi

      if [ "${#matches[@]}" -eq 0 ]; then
        echo "::warning::Local package not found: $pkg" >&2
        continue
      fi

      for match in "${matches[@]}"; do
        if [[ "$match" == *.tgz ]]; then
          echo "Copying $match ..."
          cp "$match" "$REPO_DIR/"
        else
          echo "Extracting runtimePackages/*.tgz from $match ..."
          BEFORE=$(find "$REPO_DIR" -maxdepth 1 -name '*.tgz' | wc -l)
          unzip -oj "$match" 'runtimePackages/*.tgz' -d "$REPO_DIR"
          AFTER=$(find "$REPO_DIR" -maxdepth 1 -name '*.tgz' | wc -l)
          if [ "$AFTER" -eq "$BEFORE" ]; then
            echo "::error::No runtimePackages/*.tgz found inside $match" >&2
            exit 1
          fi
        fi
      done
      ;;
    *)
      echo "Packing $pkg ..."
      if ! npm pack "$pkg" --pack-destination "$REPO_DIR"; then
        echo "::error::Failed to resolve/pack remote package: $pkg" >&2
        exit 1
      fi
      ;;
  esac
done

echo "Rebuilding $REPO_DIR/.repositoryContent.json ..."
"$ROOT_DIR/scripts/.rebuildDatabase.sh" "$REPO_DIR"
