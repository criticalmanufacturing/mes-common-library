#!/usr/bin/env bash
#
# Builds a feature-scoped "What's Changed" release notes body. Unlike
# `gh api releases/generate-notes` (which scopes "previous release" only by
# tag lineage and then pulls in every commit/PR in that range with no path
# filtering), this only lists PRs that actually touched files under
# features/<feature>/ - so a PR that merged in between two of this feature's
# releases but only touched a *different* feature doesn't leak into these
# notes. On a feature's first-ever release (no previous tag), it bounds
# history to the feature folder's own lifetime instead of the whole repo.
#
# Usage: generate-feature-release-notes.sh <feature> <prevTag> <targetSha> <tagName>
#   <feature>   Feature folder name under features/ (e.g. "IoTMTConnect")
#   <prevTag>   Previous release tag for this feature, or "" for a first release
#   <targetSha> Commit to generate notes up to (usually $GITHUB_SHA)
#   <tagName>   The new tag being released, used only for the compare-link footer
#
# Requires GH_TOKEN and GITHUB_REPOSITORY in the environment, and a full
# clone (checkout with fetch-depth: 0) with tags fetched.
set -euo pipefail

FEATURE="${1:?Usage: generate-feature-release-notes.sh <feature> <prevTag> <targetSha> <tagName>}"
PREV_TAG="${2:-}"
TARGET_SHA="${3:?Usage: generate-feature-release-notes.sh <feature> <prevTag> <targetSha> <tagName>}"
TAG_NAME="${4:?Usage: generate-feature-release-notes.sh <feature> <prevTag> <targetSha> <tagName>}"
REPO="${GITHUB_REPOSITORY:?GITHUB_REPOSITORY must be set}"
FEATURE_PATH="features/$FEATURE/"

if [ -n "$PREV_TAG" ]; then
  BASE_SHA=$(git rev-list -n1 "$PREV_TAG")
else
  # First release for this feature: bound history to when the feature folder
  # was first added, so notes don't include ancient, unrelated commits that
  # merely predate any release of it.
  FIRST_COMMIT=$(git log --diff-filter=A --follow --format=%H -- "$FEATURE_PATH" | tail -n1)
  if [ -n "$FIRST_COMMIT" ] && git rev-parse --verify -q "$FIRST_COMMIT^" >/dev/null; then
    BASE_SHA="$FIRST_COMMIT^"
  else
    BASE_SHA=$(git hash-object -t tree /dev/null)
  fi
fi

mapfile -t COMMITS < <(git log --format=%H "$BASE_SHA..$TARGET_SHA")

declare -A SEEN_PRS
PR_NUMBERS=()
UNLINKED_COMMITS=()

for sha in "${COMMITS[@]}"; do
  # Commits/{sha}/pulls resolves a commit to the PR(s) it was merged through
  # (works for merge, squash and rebase-merge commits alike).
  mapfile -t PR_NUMS_FOR_COMMIT < <(gh api "repos/$REPO/commits/$sha/pulls" --jq '.[].number' 2>/dev/null || true)
  if [ ${#PR_NUMS_FOR_COMMIT[@]} -eq 0 ]; then
    UNLINKED_COMMITS+=("$sha")
    continue
  fi
  for num in "${PR_NUMS_FOR_COMMIT[@]}"; do
    if [ -z "${SEEN_PRS[$num]:-}" ]; then
      SEEN_PRS[$num]=1
      PR_NUMBERS+=("$num")
    fi
  done
done

FEATURE_PR_LINES=()
for num in "${PR_NUMBERS[@]}"; do
  line=$(gh pr view "$num" --repo "$REPO" --json number,title,author,url,files | \
    jq -r --arg prefix "$FEATURE_PATH" \
      'select(any(.files[]; .path | startswith($prefix))) | "- \(.title) by @\(.author.login) in #\(.number)"')
  [ -n "$line" ] && FEATURE_PR_LINES+=("$line")
done

FEATURE_COMMIT_LINES=()
for sha in "${UNLINKED_COMMITS[@]}"; do
  if git diff-tree --no-commit-id --name-only -r "$sha" | grep -q "^$FEATURE_PATH"; then
    FEATURE_COMMIT_LINES+=("- $(git log -1 --format=%s "$sha") ($(git rev-parse --short "$sha"))")
  fi
done

echo "## What's Changed"
if [ ${#FEATURE_PR_LINES[@]} -eq 0 ] && [ ${#FEATURE_COMMIT_LINES[@]} -eq 0 ]; then
  echo "_No changes for this feature since the previous release._"
else
  printf '%s\n' "${FEATURE_PR_LINES[@]}"
fi

if [ ${#FEATURE_COMMIT_LINES[@]} -gt 0 ]; then
  echo ""
  echo "### Other commits (no associated pull request)"
  printf '%s\n' "${FEATURE_COMMIT_LINES[@]}"
fi

if [ -n "$PREV_TAG" ]; then
  echo ""
  echo "**Full Changelog**: https://github.com/$REPO/compare/$PREV_TAG...$TAG_NAME"
fi
