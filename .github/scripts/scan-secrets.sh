#!/usr/bin/env bash
#
# Scans staged changes for secrets (tokens, passwords, API keys, ...) using
# secretlint (https://github.com/secretlint/secretlint), a pure npm/JS
# scanner. Blocks the commit if anything is found.
#
# secretlint is installed as a regular devDependency (see package.json), so
# "npm install" is the only setup step required - no binary download.
set -euo pipefail

mapfile -t STAGED_FILES < <(git diff --cached --name-only --diff-filter=ACMR)

if [ "${#STAGED_FILES[@]}" -eq 0 ]; then
  exit 0
fi

npx --no -- secretlint "${STAGED_FILES[@]}"
