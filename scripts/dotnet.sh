#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE="$HOME/.cache/cds-nuget"
CLI_HOME="$HOME/.cache/cds-dotnet-home"

mkdir -p "$CACHE" "$CLI_HOME"

podman run --rm --network host --userns keep-id \
  -v "$ROOT":/src:Z \
  -v "$CACHE":/nuget:Z \
  -v "$CLI_HOME":/dotnet-home:Z \
  -e HOME=/dotnet-home \
  -e DOTNET_CLI_HOME=/dotnet-home \
  -e NUGET_PACKAGES=/nuget \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet "$@"
