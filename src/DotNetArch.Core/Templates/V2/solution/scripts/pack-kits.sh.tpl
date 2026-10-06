#!/usr/bin/env bash
# Packs every kit under kits/ (each kit is built and versioned on its own) and pushes them when a feed is configured.
#   NUGET_SOURCE / NUGET_API_KEY   feed and token (CI secrets; never stored in files)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

shopt -s nullglob
found=0
for kit in kits/*/; do
  if [[ -x "${kit}scripts/pack.sh" ]]; then
    found=1
    echo "==> ${kit}"
    KIT_OUTPUT="$PWD/artifacts/kits" "${kit}scripts/pack.sh"
  fi
done

if [[ "$found" -eq 0 ]]; then
  echo "No kits found under kits/."
fi
