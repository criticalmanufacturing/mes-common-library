#!/usr/bin/env bash
#
# Enforces the package versioning rule for cmfpackage.json files:
#
#   {MES_MAJOR}{MES_MINOR}{MES_PATCH}{MAJOR}.{MINOR}.{PATCH}
#
# Every feature is rooted at a cmfpackage.json with "packageType": "Root",
# which declares the CM MES version it targets via its "Cmf.Environment"
# dependency (e.g. "11.3.3"). The "version" of the root package and of every
# cmfpackage.json nested under it must have a major segment that starts with
# the MES version digits concatenated without dots (e.g. "1133"), followed by
# the package's own major version (e.g. "113310.0.0" is invalid, but
# "11331.0.0" and "113310" are consistent forms of MES 11.3.3 + package major
# 1 and 10 respectively).
set -euo pipefail

status=0

mapfile -t ALL_PACKAGES < <(find . -name cmfpackage.json -not -path "*/node_modules/*" | sort)

for root_file in "${ALL_PACKAGES[@]}"; do
  package_type=$(jq -r '.packageType // empty' "$root_file")
  [ "$package_type" = "Root" ] || continue

  root_dir=$(dirname "$root_file")
  mes_version=$(jq -r '[.dependencies[]? | select(.id == "Cmf.Environment")][0].version // empty' "$root_file")

  if [ -z "$mes_version" ]; then
    echo "::error file=$root_file::Root package is missing a 'Cmf.Environment' dependency; cannot determine the expected version prefix."
    status=1
    continue
  fi

  if [[ ! "$mes_version" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
    echo "::error file=$root_file::Cmf.Environment dependency version '$mes_version' is not in MAJOR.MINOR.PATCH format."
    status=1
    continue
  fi
  prefix="${BASH_REMATCH[1]}${BASH_REMATCH[2]}${BASH_REMATCH[3]}"

  mapfile -t GROUP_PACKAGES < <(find "$root_dir" -name cmfpackage.json -not -path "*/node_modules/*" | sort)

  for pkg_file in "${GROUP_PACKAGES[@]}"; do
    version=$(jq -r '.version // empty' "$pkg_file")

    if [[ ! "$version" =~ ^([0-9]+)\.[0-9]+\.[0-9]+$ ]]; then
      echo "::error file=$pkg_file::Version '$version' is not in MAJOR.MINOR.PATCH format."
      status=1
      continue
    fi
    major="${BASH_REMATCH[1]}"

    case "$major" in
      "$prefix"*) ;;
      *)
        echo "::error file=$pkg_file::Version '$version' must start with '$prefix' (derived from Cmf.Environment $mes_version declared in $root_file), e.g. '${prefix}0.0.0'."
        status=1
        continue
        ;;
    esac

    package_major="${major#"$prefix"}"
    if [[ ! "$package_major" =~ ^[0-9]+$ ]]; then
      echo "::error file=$pkg_file::Version '$version' major segment '$major' does not cleanly split into the MES prefix '$prefix' followed by a numeric package major version."
      status=1
    fi
  done
done

if [ "$status" -eq 0 ]; then
  echo "All cmfpackage.json versions conform to the {MES_MAJOR}{MES_MINOR}{MES_PATCH}{MAJOR}.{MINOR}.{PATCH} rule."
fi

exit "$status"
