#!/usr/bin/env bash

set -euo pipefail

worktree_path="${CODEX_WORKTREE_PATH:-$PWD}"
if [[ ! -f "$worktree_path/src/Exceptionless.Web/ClientApp/package.json" ]]; then
  worktree_path="$(git -C "$worktree_path" rev-parse --show-toplevel 2>/dev/null || true)"
fi

client_path="$worktree_path/src/Exceptionless.Web/ClientApp"
if [[ -z "$worktree_path" || ! -f "$client_path/package.json" ]]; then
  echo "Unable to locate the Exceptionless Svelte client." >&2
  exit 1
fi

cd "$client_path"

storybook_bin="./node_modules/.bin/storybook"
storybook_port="${STORYBOOK_PORT:-6006}"
storybook_path="${STORYBOOK_PATH:-/story/components-shared-taglist--clickable-long-tags-in-a-summary-cell}"

if [[ ! -x "$storybook_bin" ]]; then
  echo "Storybook dependencies are missing. Run setup first." >&2
  exit 1
fi

echo "Starting Storybook at http://127.0.0.1:$storybook_port/?path=$storybook_path"
exec "$storybook_bin" dev --host 127.0.0.1 --port "$storybook_port" --initial-path "$storybook_path" --exact-port
