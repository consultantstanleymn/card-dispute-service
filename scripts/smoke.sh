#!/usr/bin/env bash
set -euo pipefail

API_BASE="http://127.0.0.1:26600"
API_KEY="${CDS_SMOKE_API_KEY:-smoke-key-local-only}"
MINIO_HEALTH="http://127.0.0.1:26660/minio/health/live"

mkdir -p docs/evidence

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

wait_for_api() {
  local attempt
  for attempt in $(seq 1 60); do
    if curl -fsS -o /dev/null -H "X-Api-Key: $API_KEY" "$API_BASE/health/live" 2>/dev/null; then
      return 0
    fi
    sleep 1
  done

  echo "API did not become ready at $API_BASE" >&2
  exit 1
}

wait_for_api

suffix="$(date +%s%N)"
idempotency_key="smoke-$suffix"
transaction_id="smoke-tx-$suffix"

create_body="$(jq -n \
  --arg tx "$transaction_id" \
  --arg reason "GOODS_NOT_RECEIVED" \
  --arg currency "USD" \
  '{cardTxnRef:$tx, merchant:"Smoke Merchant", amountMinor:12345, currency:$currency, reason:$reason}')"

create_body_file="$TMP_DIR/create.json"
create_headers_file="$TMP_DIR/create.headers"
curl -fsS -X POST \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $idempotency_key" \
  -H "Content-Type: application/json" \
  -d "$create_body" \
  -D "$create_headers_file" \
  -o "$create_body_file" \
  "$API_BASE/disputes"

jq -e 'has("id")' "$create_body_file" >/dev/null

dispute_id="$(jq -r '.id // empty' "$create_body_file")"
if [ -z "$dispute_id" ]; then
  dispute_id="$(awk 'BEGIN { IGNORECASE = 1 } /^Location:/ { gsub("\r", ""); print $2 }' "$create_headers_file" | sed 's#^http://[^/]*##; s#.*/disputes/##')"
fi

if [ -z "$dispute_id" ]; then
  echo "Could not resolve dispute id" >&2
  exit 1
fi

get_body_file="$TMP_DIR/get.json"
curl -fsS -H "X-Api-Key: $API_KEY" "$API_BASE/disputes/$dispute_id" -o "$get_body_file"
jq -e '.version' "$get_body_file" >/dev/null

if curl -fsS -o /dev/null "$MINIO_HEALTH" 2>/dev/null; then
  full_flow=true
else
  full_flow=false
fi

if [ "$full_flow" = true ]; then
  pdf_file="$TMP_DIR/smoke-evidence.pdf"
  {
    printf '%%PDF-1.4\n'
    head -c 1024 /dev/zero | tr '\0' 'a'
    printf '\n%%%%EOF\n'
  } > "$pdf_file"

  evidence_body_file="$TMP_DIR/evidence.json"
  curl -fsS -X POST \
    -H "X-Api-Key: $API_KEY" \
    -F "file=@$pdf_file;type=application/pdf" \
    "$API_BASE/disputes/$dispute_id/evidence" \
    -o "$evidence_body_file"

  jq -e '.sha256 and .sizeBytes and .key' "$evidence_body_file" >/dev/null

  get_after_evidence="$TMP_DIR/get-after-evidence.json"
  curl -fsS -H "X-Api-Key: $API_KEY" "$API_BASE/disputes/$dispute_id" -o "$get_after_evidence"
  version="$(jq -r '.version' "$get_after_evidence")"
  [ -n "$version" ]

  submit_body_file="$TMP_DIR/submit.json"
  curl -fsS -X POST \
    -H "X-Api-Key: $API_KEY" \
    -H "Content-Type: application/json" \
    -d "{\"version\":$version}" \
    "$API_BASE/disputes/$dispute_id/submit" \
    -o "$submit_body_file"

  get_after_submit="$TMP_DIR/get-after-submit.json"
  curl -fsS -H "X-Api-Key: $API_KEY" "$API_BASE/disputes/$dispute_id" -o "$get_after_submit"
  jq -e '.status == "EVIDENCE_SUBMITTED"' "$get_after_submit" >/dev/null

  version="$(jq -r '.version' "$get_after_submit")"
  decide_body_file="$TMP_DIR/decide.json"
  curl -fsS -X POST \
    -H "X-Api-Key: $API_KEY" \
    -H "Content-Type: application/json" \
    -d "{\"outcome\":\"WON\",\"version\":$version}" \
    "$API_BASE/disputes/$dispute_id/decide" \
    -o "$decide_body_file"

  get_after_decision="$TMP_DIR/get-after-decision.json"
  curl -fsS -H "X-Api-Key: $API_KEY" "$API_BASE/disputes/$dispute_id" -o "$get_after_decision"
  jq -e '.status == "WON"' "$get_after_decision" >/dev/null
fi

list_body_file="$TMP_DIR/list.json"
curl -fsS -H "X-Api-Key: $API_KEY" "$API_BASE/disputes?limit=10" -o "$list_body_file"
jq -e '.items | type == "array"' "$list_body_file" >/dev/null
jq -e 'has("nextCursor")' "$list_body_file" >/dev/null

aging_body_file="$TMP_DIR/aging.json"
curl -fsS -H "X-Api-Key: $API_KEY" "$API_BASE/reports/aging" -o "$aging_body_file"
jq -e 'length >= 0' "$aging_body_file" >/dev/null

metrics_body_file="$TMP_DIR/metrics.txt"
curl -fsS "$API_BASE/metrics" -o "$metrics_body_file"
grep -Eq 'http_requests_received_total|process_cpu_seconds_total' "$metrics_body_file"

curl -fsS -o /dev/null "$API_BASE/health/live"

if [ "$full_flow" = true ]; then
  echo "SMOKE OK"
else
  echo "SMOKE OK (reduced: no MinIO)"
fi
