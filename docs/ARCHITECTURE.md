# Architecture

## Purpose

Card Dispute Service is an ASP.NET Core Web API. Dispute analysts use it to:

- open a dispute on a card transaction;
- attach evidence files;
- submit the case;
- record the final outcome.

The service enforces a strict state machine and keeps an append-only event history.

## Domain model

### Money

Money uses a `long` value for minor units and an ISO 4217 `char(3)` currency code. API field names are `amountMinor` and `currency`.

Rules:

- amountMinor must be from 1 to 10,000,000 minor units.
- The code never uses `decimal`, `double`, or `float` for money.

### Dispute

A dispute tracks:

- `id`
- `cardTxnRef`
- `merchant`
- `amountMinor`
- `currency`
- `reason`
- `status`
- `openedAt`
- `deadline`
- `version`

`version` comes from PostgreSQL `xmin` and is used for optimistic concurrency.

### Reason codes

- `FRAUD_CARD_ABSENT`
- `GOODS_NOT_RECEIVED`
- `DUPLICATE_CHARGE`
- `CANCELLED_RECURRING`
- `CREDIT_NOT_PROCESSED`

### Response deadlines

Dates are stored in UTC.

- `FRAUD_CARD_ABSENT`: 30 calendar days from `openedAt`.
- All other reason codes: 20 calendar days from `openedAt`.

Submitting after the deadline is illegal.

## State machine

States:

- `OPENED`
- `EVIDENCE_SUBMITTED`
- `WON`
- `LOST`
- `WITHDRAWN`

Allowed transitions:

- `OPENED` to `EVIDENCE_SUBMITTED`, requires at least one evidence file.
- `OPENED` to `WITHDRAWN`.
- `EVIDENCE_SUBMITTED` to `WON`.
- `EVIDENCE_SUBMITTED` to `LOST`.
- `EVIDENCE_SUBMITTED` to `WITHDRAWN`.

All other transitions are illegal.

## API contract

All JSON error responses use RFC 7807 `application/problem+json`.

Header `X-Api-Key` is required on every route except `/health/*` and `/metrics`. Keys map to a client id in configuration. The development key `dev-key-local-only` exists only in `appsettings.Development.json`.

### Create dispute

`POST /disputes`

Request headers:

- `X-Api-Key`
- `Idempotency-Key`: required, 1 to 64 characters.

Request body:

```
{
  "cardTxnRef": "string",
  "merchant": "string",
  "amountMinor": 12345,
  "currency": "USD",
  "reason": "GOODS_NOT_RECEIVED"
}
```

Behavior:

- Same idempotency key and same body: return the same status and body.
- Same idempotency key and different body: return 409.
- Success: return 201 with a `Location` header.

### Get dispute

`GET /disputes/{id}`

- Return 200 or 404.
- The response includes `version` from PostgreSQL `xmin`.

### State changes

`POST /disputes/{id}/submit`

Request body:

```
{"version": n}
```

`POST /disputes/{id}/decide`

Request body:

```
{"outcome":"WON"|"LOST","version":n}
```

`POST /disputes/{id}/withdraw`

Request body:

```
{"version":n}
```

Errors:

- Illegal transition: 409 with type `/problems/illegal-transition`.
- Stale version: 409 with type `/problems/concurrency`.

### Evidence upload

`POST /disputes/{id}/evidence`

Multipart form:

- one file;
- maximum size 5 MB;
- allowed types: `application/pdf`, `image/png`, `image/jpeg`;
- content is checked by magic bytes, not only by the `Content-Type` header.

Response:

- 201 with `sha256`, `sizeBytes`, and `key`.

## Paging

`GET /disputes?status=&reason=&limit=&cursor=`

- Keyset order: `(opened_at DESC, id DESC)`.
- `limit`: 1 to 100, default 25.
- `cursor`: opaque base64url string.
- Bad cursor: 400.
- Response: `{ "items": [...], "nextCursor": "..." | null }`.

## Aging report

`GET /reports/aging`

Returns open disputes by reason code and days-to-deadline bucket.

Buckets:

- `overdue`
- `0-3`
- `4-7`
- `8+`

Open disputes have status `OPENED` or `EVIDENCE_SUBMITTED`.

The query uses raw SQL through `Database.SqlQuery<T>`.

## Data model

### Disputes table

Stores the current dispute state.

### Dispute events table

The `dispute_events` table is append-only.

Every state change inserts one row in the same transaction:

- dispute id;
- previous state;
- new state;
- client id;
- UTC event time.

The service never updates or deletes rows in `dispute_events`.

### Optimistic concurrency

Optimistic concurrency uses PostgreSQL `xmin`. The API returns the current `version`. State change requests must include that version. A stale version returns 409 with type `/problems/concurrency`.

## Evidence storage

Evidence files are stored through the AWS SDK for .NET S3 API.

Local testing uses MinIO, an S3-compatible server.

The local MinIO settings use:

- service URL: `http://127.0.0.1:26660`
- bucket: `disputes`
- region: `us-east-1`

## Metrics and health

- `/metrics`: Prometheus metrics.
- `/health/live`: liveness check.
- `/health/ready`: readiness check, including PostgreSQL and S3 health.

## Infrastructure

The repository includes:

- a Dockerfile that builds a .NET 10 runtime image;
- a compose file with PostgreSQL, MinIO, and the API;
- a CloudFormation template for ECS Fargate, RDS, and S3.

The CloudFormation template is linted locally but never deployed in this repository.
