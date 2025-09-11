#!/usr/bin/env bash
set -euo pipefail

# Detect preferred TFM based on installed SDK
major=$(dotnet --version | cut -d. -f1)
tfm="net8.0"
if [[ "$major" -ge 9 ]]; then
  tfm="net9.0"
fi

exec dotnet run -f "$tfm" -- "$@"

