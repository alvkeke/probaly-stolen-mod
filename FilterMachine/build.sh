#!/usr/bin/env bash
# Builds the mod and installs it into <game>/Mods/FilterMachine (the csproj does the copy).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
exec dotnet build -c Release "$@"
