#!/bin/bash
# Auto-commit script for Claude Code Stop hook
# Runs when Claude finishes a response; commits if there are changes

cd "$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0

# Check for any changes (staged + unstaged + untracked, respecting .gitignore)
if git diff --quiet HEAD 2>/dev/null && [ -z "$(git ls-files --others --exclude-standard)" ]; then
  exit 0
fi

# Stage all changes (respects .gitignore)
git add -A

# Generate commit message from changed files
CHANGED=$(git diff --cached --name-only | head -10)
FILE_COUNT=$(git diff --cached --name-only | wc -l | tr -d ' ')

if [ "$FILE_COUNT" -eq 0 ]; then
  exit 0
fi

# Build a short summary
if [ "$FILE_COUNT" -le 3 ]; then
  MSG="auto: update $(git diff --cached --name-only | sed 's|.*/||' | paste -sd ', ' -)"
else
  MSG="auto: update ${FILE_COUNT} files"
fi

git commit -m "$MSG" --no-verify >/dev/null 2>&1

# Auto-push to origin if tracking branch exists
if git rev-parse --abbrev-ref --symbolic-full-name @{u} >/dev/null 2>&1; then
  git push --quiet 2>/dev/null &
fi
