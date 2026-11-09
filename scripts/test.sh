#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

scripts/dotnet.sh test tests/CardDisputes.UnitTests -v q

status="$(scripts/test-stack.sh status 2>/dev/null || true)"

if printf '%s\n' "$status" | grep -q '^cds-postgres up'; then
  export CDS_PG="${CDS_PG:-Host=127.0.0.1;Port=26632;Database=disputes;Username=disputes;Password=disputes}"
  scripts/dotnet.sh test tests/CardDisputes.IntegrationTests -v q
else
  echo "integration skipped: stack down"
fi
