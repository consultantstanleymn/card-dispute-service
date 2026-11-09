#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE="$HOME/.cache/cds-nuget"

mkdir -p "$CACHE"

podman run --rm --network host --userns keep-id \
  -v "$ROOT":/src:Z \
  -v "$CACHE":/nuget:Z \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/nuget \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet "$@"
