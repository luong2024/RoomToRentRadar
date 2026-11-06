#!/usr/bin/env bash
# Validate Git branch name, target restrictions, and commit message against RoomToRentRadar (Group-3) conventions

set -e

BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown")
COMMIT_MSG=$(git log -1 --pretty=%s 2>/dev/null || echo "")

echo "=== RoomToRentRadar Git Guardrail Check (.NET 10 LTS) ==="
echo "Active Branch: $BRANCH"

if [[ "$BRANCH" == "main" || "$BRANCH" == "master" ]]; then
  echo "[CRITICAL VIOLATION] Direct work or commits on '$BRANCH' are strictly forbidden!"
  echo "The main branch belongs to the instructor. Switch to a Group-3 feature branch immediately."
  exit 1
fi

if [[ "$BRANCH" == "Group-3" ]]; then
  echo "[CRITICAL VIOLATION] Direct commits on 'Group-3' are strictly forbidden!"
  echo "Group-3 is our protected parent branch. Create a feature branch (e.g., Group-3/feature/USxx-Txx.y-slug) and merge via PR."
  exit 1
fi

BRANCH_REGEX="^(Group-3/(feature|bugfix|release|hotfix)/US[0-9]{2}-T[0-9]{2}\.[0-9]-[a-z0-9-]+|Group-3-(feature|bugfix|release|hotfix)-US[0-9]{2}-T[0-9]{2}\.[0-9]-[a-z0-9-]+)$"
if [[ ! "$BRANCH" =~ $BRANCH_REGEX ]]; then
  echo "[ERROR] Invalid branch name: '$BRANCH'"
  echo "Expected format: Group-3/feature/USxx-Txx.y-short-slug (e.g., Group-3/feature/US01-T01.1-listing-api-pagination)"
  exit 1
fi
echo "[OK] Branch name is compliant for Group-3."

COMMIT_REGEX="^(feat|fix|refactor|test|docs|chore|perf)\(US[0-9]{2}/T[0-9]{2}\.[0-9]\): .{5,72}$"
if [[ -n "$COMMIT_MSG" ]]; then
  echo "Latest commit message: $COMMIT_MSG"
  if [[ ! "$COMMIT_MSG" =~ $COMMIT_REGEX ]]; then
    echo "[ERROR] Invalid commit message: '$COMMIT_MSG'"
    echo "Expected format: <type>(USxx/Txx.y): <summary> (e.g., feat(US01/T01.1): build listing pagination API)"
    exit 1
  fi
  echo "[OK] Commit message is compliant."
fi

echo "[SUCCESS] All Git guardrails passed."
