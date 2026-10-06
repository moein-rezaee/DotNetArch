#!/usr/bin/env bash
# Fails when .env.example or appsettings.example.json drift from the code (see ConfigurationContract).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet test --filter "Category=Configuration" --nologo
