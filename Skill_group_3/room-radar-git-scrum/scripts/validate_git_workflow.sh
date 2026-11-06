#!/usr/bin/env bash
# Validate Git branch name and latest commit message against RoomToRentRadar conventions

set -e

BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown")
COMMIT_MSG=$(git log -1 --pretty=%s 2>/dev/null || echo "")

BRANCH_REGEX="^(main|develop|feature/US[0-9]{2}-T[0-9]{2}\.[0-9]-[a-z0-9-]+|bugfix/US[0-9]{2}-T[0-9]{2}\.[0-9]-[a-z0-9-]+|release/sprint-[1-4]|hotfix/[a-z0-9-]+)$"
COMMIT_REGEX="^(feat|fix|refactor|test|docs|chore|perf)\(US[0-9]{2}/T[0-9]{2}\.[0-9]\): .{5,72}$"

echo "Checking branch: $BRANCH"
if [[ ! "$BRANCH" =~ $BRANCH_REGEX ]]; then
  echo "[ERROR] Invalid branch name: '$BRANCH'"
  echo "Expected format: feature/USxx-Txx.y-short-slug (e.g., feature/US01-T01.1-listing-api-pagination)"
  exit 1
fi
echo "[OK] Branch name is compliant."

if [[ -n "$COMMIT_MSG" && "$BRANCH" != "main" && "$BRANCH" != "develop" ]]; then
  echo "Checking latest commit message: $COMMIT_MSG"
  if [[ ! "$COMMIT_MSG" =~ $COMMIT_REGEX ]]; then
    echo "[ERROR] Invalid commit message: '$COMMIT_MSG'"
    echo "Expected format: <type>(USxx/Txx.y): <summary> (e.g., feat(US01/T01.1): build listing pagination API)"
    exit 1
  fi
  echo "[OK] Commit message is compliant."
fi
