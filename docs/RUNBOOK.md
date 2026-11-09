# Runbook

## Useful commands

Build:

```
scripts/dotnet.sh build CardDisputes.sln
```

Start local services:

```
scripts/test-stack.sh up
```

Run tests:

```
scripts/test.sh
```

Run smoke test:

```
scripts/smoke.sh
```

Security gate:

```
scripts/security-gate.sh
```

Stop local services:

```
scripts/test-stack.sh down
```

Show container status:

```
scripts/test-stack.sh status
```

Read container logs:

```
podman logs cds-postgres
podman logs cds-minio
podman logs cds-api
```

## Health endpoints

- Liveness: `GET http://127.0.0.1:26600/health/live`
- Readiness: `GET http://127.0.0.1:26600/health/ready`
- Metrics: `GET http://127.0.0.1:26600/metrics`

## Incident: dispute stuck near deadline

Symptoms:

- An analyst says a dispute is still `OPENED` and the deadline is close.
- The analyst cannot submit because no evidence is attached.

Steps:

1. Find the dispute with `GET /disputes/{id}`.
2. Check status, `openedAt`, and deadline.
3. If no evidence is attached, ask the analyst to attach evidence or withdraw the dispute.
4. If evidence exists and the deadline has not passed, submit with the current `version`.
5. If the deadline has passed, do not attempt to submit. Route the dispute to the team lead.
6. Check `dispute_events` only through application endpoints. Never edit that table.

## Incident: S3 is down

Symptoms:

- Evidence uploads return 503.
- Readiness is red for the S3 health check.
- `scripts/smoke.sh` reports the reduced flow.

Steps:

1. Check MinIO status:

```
podman ps --filter name=cds-minio
podman logs cds-minio
```

2. Check the S3 health endpoint:

```
curl -fsS http://127.0.0.1:26660/minio/health/live
```

3. Check bucket configuration and credentials.
4. Restart MinIO if needed:

```
scripts/test-stack.sh down
scripts/test-stack.sh up
```

5. Run the smoke test again:

```
scripts/smoke.sh
```

If the upload still fails, keep readiness red. Do not write a partial evidence row.

## Incident: concurrency 409 spikes

Symptoms:

- Many state change requests return 409 with type `/problems/concurrency`.
- Analysts may have duplicate tabs or stale screens.

Steps:

1. Confirm the problem type is `/problems/concurrency`.
2. Ask the analyst to reload the dispute with `GET /disputes/{id}`.
3. Use the latest `version` for the retry.
4. Check metrics for a high rate of 409 responses.
5. If retries fail repeatedly, treat it as a workflow problem, not a database outage.

## Rotating API keys

1. Add the new key and client id to configuration.
2. Deploy or restart the API.
3. Update callers with the new key.
4. Remove the old key from configuration.
5. Restart the API again.
6. Verify `/metrics` and `/health/ready` are healthy.

The development key `dev-key-local-only` exists only in development configuration. Do not use it in non-development environments.
