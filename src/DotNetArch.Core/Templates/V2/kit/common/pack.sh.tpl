#!/usr/bin/env bash
# Packs this kit and optionally pushes it to a NuGet feed.
#   NUGET_SOURCE   feed URL (push is skipped when empty)
#   NUGET_API_KEY  API key / token for the feed (never stored in files)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

out="${KIT_OUTPUT:-artifacts}"
dotnet build -c Release --nologo
if compgen -G "tests/*/*.csproj" > /dev/null; then
  dotnet test -c Release --no-build --nologo
fi
dotnet pack -c Release --no-build --nologo -o "$out"

if [[ -n "${NUGET_SOURCE:-}" ]]; then
  dotnet nuget push "$out/*.nupkg" --source "$NUGET_SOURCE" --api-key "${NUGET_API_KEY:-}" --skip-duplicate
else
  echo "NUGET_SOURCE not set: packed to $out, push skipped."
fi
