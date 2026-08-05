#!/usr/bin/env bash

set -euo pipefail

worktree_path="${CODEX_WORKTREE_PATH:-$PWD}"
if [[ ! -f "$worktree_path/src/Exceptionless.AppHost/Exceptionless.AppHost.csproj" ]]; then
  worktree_path="$(git -C "$worktree_path" rev-parse --show-toplevel 2>/dev/null || true)"
fi

if [[ -z "$worktree_path" || ! -f "$worktree_path/src/Exceptionless.AppHost/Exceptionless.AppHost.csproj" ]]; then
  echo "Unable to locate the Exceptionless worktree or AppHost project." >&2
  exit 1
fi

cd "$worktree_path"
worktree_path="$PWD"

export AppMode=Development
export DOTNET_ENVIRONMENT=Development
export ASPNETCORE_ENVIRONMENT=Development
export ASPIRE_HOME="$worktree_path/.aspire"

mkdir -p "$ASPIRE_HOME"

echo "Starting Exceptionless Aspire from $worktree_path"
echo "Aspire logs: $ASPIRE_HOME/logs"

if command -v dotnet >/dev/null 2>&1; then
  echo "Building the AppHost from the repository root..."
  if ! dotnet build src/Exceptionless.AppHost/Exceptionless.AppHost.csproj --no-restore --nologo --verbosity minimal -m:1 /p:UseSharedCompilation=false; then
    echo "The AppHost build could not complete from the restored repository state. Run environment setup first to restore dependencies." >&2
    exit 1
  fi
fi

if command -v aspire >/dev/null 2>&1; then
  exec aspire run --apphost src/Exceptionless.AppHost --no-build --nologo
elif command -v dotnet >/dev/null 2>&1; then
  echo "Aspire CLI not found; starting the AppHost with dotnet."
  exec dotnet run --project src/Exceptionless.AppHost/Exceptionless.AppHost.csproj --no-build
fi

echo "Neither the Aspire CLI nor dotnet is installed or available on PATH." >&2
exit 1
