#!/usr/bin/env bash
# End-to-end check of the tool: build, unit tests, golden generation test, then generate a solution with the
# real CLI and build it. Needs the dotnet SDK and NuGet access.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cd "$root"

echo "==> build"
dotnet build DotNetArch.sln --nologo -v quiet

echo "==> unit tests"
dotnet test DotNetArch.sln --no-build --nologo --filter "Category!=Integration"

echo "==> integration tests (golden generation, MCP protocol)"
dotnet test DotNetArch.sln --no-build --nologo --filter "Category=Integration"

echo "==> generate with the CLI and build the result"
dotnet run --no-build --project src/DotNetArch.Cli -- new solution Smoke --output="$work" --database=SQLite --style=controller --no-git --no-docker </dev/null
dotnet build "$work/Smoke" -c Release --nologo -v quiet && dotnet test "$work/Smoke" -c Release --nologo -v quiet

echo "smoke: OK"
