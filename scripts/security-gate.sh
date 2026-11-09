#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "Running secret scan"
gitleaks detect --source . --no-banner

echo "Running filesystem scan"
SKIP_DIRS="$(find . -type d \( -name bin -o -name obj \) -not -path './.git/*' -printf '%P,' 2>/dev/null | sed 's/,$//')"
if [ -n "$SKIP_DIRS" ]; then
  echo "Skipping build output directories: ${SKIP_DIRS}"
  trivy fs --skip-version-check --skip-dirs "$SKIP_DIRS" --severity HIGH,CRITICAL --exit-code 1 .
else
  trivy fs --skip-version-check --severity HIGH,CRITICAL --exit-code 1 .
fi

echo "Checking package vulnerabilities"
if ! VULN_OUTPUT="$(scripts/dotnet.sh list package --vulnerable --include-transitive 2>&1)"; then
  printf '%s\n' "$VULN_OUTPUT" >&2
  echo "Package vulnerability scan failed" >&2
  exit 1
fi

printf '%s\n' "$VULN_OUTPUT"
if printf '%s\n' "$VULN_OUTPUT" | grep -E 'High|Critical' >/dev/null; then
  echo "Vulnerable packages found" >&2
  exit 1
fi

echo "Checking workflow syntax"
actionlint .github/workflows/ci.yml

echo "Security gate passed"
