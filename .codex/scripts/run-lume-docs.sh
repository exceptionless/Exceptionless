#!/usr/bin/env bash

set -euo pipefail

worktree_path="${CODEX_WORKTREE_PATH:-$PWD}"
if [[ ! -d "$worktree_path/docs" ]]; then
  worktree_path="$(git -C "$worktree_path" rev-parse --show-toplevel 2>/dev/null || true)"
fi

if [[ -z "$worktree_path" || ! -d "$worktree_path/docs" ]]; then
  echo "Unable to locate the Exceptionless docs directory." >&2
  exit 1
fi

cd "$worktree_path/docs"

export PORT="${PORT:-7141}"

if ! command -v deno >/dev/null 2>&1; then
  echo "deno is not installed or not on PATH. The Lume docs site requires Deno." >&2
  exit 1
fi

echo "deno:"
deno --version

echo "Starting Lume docs at http://127.0.0.1:$PORT/"
exec deno task serve
