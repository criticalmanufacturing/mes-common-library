#!/usr/bin/env bash
#
# Runs the test suite for a packageType "Tests" package: packs whatever IoT
# runtime dependencies it needs (scripts/pack-iot-test-packages.sh), then runs
# every MSTest/xUnit/NUnit test assembly under it via dotnet-test-rerun.
# Shared by _build-changed.yml's "build-tests" job and release.yml's sanity
# check, so both run the exact same test-execution logic.
#
# Assumes the package has already been restored, built (Release/*.dll exist)
# and packed before this runs - packing is what makes cmf stage the package's
# relatedPackages-declared local sibling dependencies (e.g. IoTMTConnect.IoT
# for IoTMTConnect.Tests) on disk via their preBuild/prePack hooks.
#
# Usage: run-package-tests.sh <packageDir>
set -euo pipefail

PACKAGE_DIR="${1:?Usage: run-package-tests.sh <packageDir>}"
DOTNET_TEST_RERUN_VERSION="${DOTNET_TEST_RERUN_VERSION:-4.2.0}"

dotnet tool install --global dotnet-test-rerun --version "$DOTNET_TEST_RERUN_VERSION"
export PATH="$PATH:$HOME/.dotnet/tools"

PACKAGE_ID=$(jq -r '.packageId' "$PACKAGE_DIR/cmfpackage.json")
RESULTS_DIR="$GITHUB_WORKSPACE/TestExecution/$PACKAGE_ID"
mkdir -p "$RESULTS_DIR"

./scripts/pack-iot-test-packages.sh

# Every .csproj under the package that references a known test framework is
# treated as a test project; its built assembly is expected at
# Release/<AssemblyName>.dll, matching these packages'
# `<OutputPath>..\Release</OutputPath>` convention (the same Release/** that
# cmfpackage.json's contentToPack ships).
mapfile -t TEST_PROJECTS < <(grep -lRE "MSTest\.TestFramework|xunit|NUnit3TestAdapter" --include="*.csproj" "$PACKAGE_DIR")

if [ ${#TEST_PROJECTS[@]} -eq 0 ]; then
  echo "::error::$PACKAGE_ID is packageType Tests but no test project (MSTest/xUnit/NUnit) was found under $PACKAGE_DIR"
  exit 1
fi

STATUS=0
for PROJ in "${TEST_PROJECTS[@]}"; do
  ASSEMBLY_NAME=$(basename "$PROJ" .csproj)
  TEST_ASSEMBLY="$PACKAGE_DIR/Release/$ASSEMBLY_NAME.dll"

  if [ ! -f "$TEST_ASSEMBLY" ]; then
    echo "::error::Expected test assembly not found: $TEST_ASSEMBLY"
    STATUS=1
    continue
  fi

  RUNSETTINGS_ARGS=()
  if [ -f "$PACKAGE_DIR/integration.runsettings" ]; then
    RUNSETTINGS_ARGS=(--settings "$PACKAGE_DIR/integration.runsettings")
  fi

  echo "::group::Running $ASSEMBLY_NAME"
  test-rerun "$TEST_ASSEMBLY" \
    --logger trx \
    --results-directory "$RESULTS_DIR" \
    "${RUNSETTINGS_ARGS[@]}" \
    --rerunMaxAttempts 3 \
  || STATUS=1
  echo "::endgroup::"
done

exit $STATUS
