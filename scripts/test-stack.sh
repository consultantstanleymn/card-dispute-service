#!/usr/bin/env bash
set -euo pipefail

PG_NAME="cds-postgres"
MINIO_NAME="cds-minio"

PG_IMAGE="docker.io/library/postgres:16-alpine"
MINIO_IMAGE="docker.io/minio/minio:RELEASE.2025-04-22T22-12-26Z"

PG_USER="${CDS_PG_USER:-disputes}"
PG_PASSWORD="${CDS_PG_PASSWORD:-disputes}"
PG_DATABASE="${CDS_PG_DATABASE:-disputes}"

MINIO_ROOT_USER="${CDS_MINIO_ROOT_USER:-cdsadmin}"
MINIO_ROOT_PASSWORD="${CDS_MINIO_ROOT_PASSWORD:-cdsadmin123456}"
MINIO_BUCKET="${CDS_MINIO_BUCKET:-cds-evidence}"

usage() {
  echo "usage: $0 up|down|status" >&2
  exit 2
}

container_state() {
  local name="$1"
  if ! podman container exists "$name"; then
    echo "absent"
    return
  fi

  local running
  running="$(podman container inspect --format '{{.State.Running}}' "$name" 2>/dev/null || echo false)"
  if [ "$running" = "true" ]; then
    echo "up"
  else
    echo "down"
  fi
}

start_postgres() {
  local state
  state="$(container_state "$PG_NAME")"

  if [ "$state" = "up" ]; then
    echo "$PG_NAME already up"
    return
  fi

  if [ "$state" = "down" ]; then
    podman start "$PG_NAME" >/dev/null
  else
    podman run -d \
      --name "$PG_NAME" \
      -p 127.0.0.1:26632:5432 \
      -e POSTGRES_USER="$PG_USER" \
      -e POSTGRES_PASSWORD="$PG_PASSWORD" \
      -e POSTGRES_DB="$PG_DATABASE" \
      "$PG_IMAGE" >/dev/null
  fi

  wait_for_postgres
}

wait_for_postgres() {
  local attempt
  for attempt in $(seq 1 60); do
    if podman exec "$PG_NAME" pg_isready -U "$PG_USER" -d "$PG_DATABASE" >/dev/null 2>&1; then
      echo "$PG_NAME ready"
      return
    fi
    sleep 1
  done

  echo "$PG_NAME did not become ready" >&2
  exit 1
}

start_minio() {
  if ! podman image exists "$MINIO_IMAGE"; then
    echo "minio image not local, skipping"
    return
  fi

  local state
  state="$(container_state "$MINIO_NAME")"

  if [ "$state" = "up" ]; then
    echo "$MINIO_NAME already up"
    ensure_bucket
    return
  fi

  if [ "$state" = "down" ]; then
    podman start "$MINIO_NAME" >/dev/null
  else
    podman run -d \
      --name "$MINIO_NAME" \
      -p 127.0.0.1:26660:9000 \
      -p 127.0.0.1:26661:9001 \
      -e MINIO_ROOT_USER="$MINIO_ROOT_USER" \
      -e MINIO_ROOT_PASSWORD="$MINIO_ROOT_PASSWORD" \
      "$MINIO_IMAGE" server /data --console-address :9001 >/dev/null
  fi

  wait_for_minio
  ensure_bucket
}

wait_for_minio() {
  local attempt
  for attempt in $(seq 1 60); do
    if curl -fsS http://127.0.0.1:26660/minio/health/live >/dev/null 2>&1; then
      echo "$MINIO_NAME ready"
      return
    fi
    sleep 1
  done

  echo "$MINIO_NAME did not become ready" >&2
  exit 1
}

ensure_bucket() {
  if ! podman exec "$MINIO_NAME" sh -c 'command -v mc' >/dev/null 2>&1; then
    echo "mc not in image; bucket $MINIO_BUCKET is created by the evidence test fixture"
    return
  fi

  podman exec "$MINIO_NAME" mc alias set local http://127.0.0.1:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null
  podman exec "$MINIO_NAME" mc mb --ignore-existing "local/$MINIO_BUCKET" >/dev/null
  echo "bucket $MINIO_BUCKET ready"
}

stop_container() {
  local name="$1"
  if ! podman container exists "$name"; then
    return
  fi

  if [ "$(container_state "$name")" = "up" ]; then
    podman stop "$name" >/dev/null
  fi
  podman rm "$name" >/dev/null
  echo "$name removed"
}

case "${1:-}" in
  up)
    start_postgres
    start_minio
    ;;
  down)
    stop_container "$MINIO_NAME"
    stop_container "$PG_NAME"
    ;;
  status)
    echo "$PG_NAME $(container_state "$PG_NAME")"
    echo "$MINIO_NAME $(container_state "$MINIO_NAME")"
    ;;
  *)
    usage
    ;;
esac
