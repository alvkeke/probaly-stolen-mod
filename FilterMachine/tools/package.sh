#!/usr/bin/env bash
# Build the mod and produce dist/FilterMachine.zip for a GitHub release.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
python3 tools/generate_icon.py
dotnet build -c Release
python3 tools/package.py
