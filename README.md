# Card Dispute Service

Fictional customer. Any match with a real company is not intended.

Card Dispute Service is an ASP.NET Core Web API for dispute analysts. Analysts open disputes on card transactions, attach evidence files, submit cases, and record outcomes. Every state change is stored as an append-only event.

## Status

Verified locally:

| Claim | Evidence |
| --- | --- |
| 30 unit tests and 32 integration tests pass (1 skipped) | docs/evidence/full-gate-local.txt |
| Container image is non-root and the Trivy scan is clean | docs/evidence/image-scan.txt |
| Reduced smoke flow passes against the compose stack | docs/evidence/smoke-local.txt |
| CloudFormation passes cfn-lint and the template checks | docs/evidence/cfn-lint.txt |
| Local security gate passes | docs/evidence/security-gate-local.txt |

Authored, not run:

| Item | Reason |
| --- | --- |
| Live evidence upload test and the full smoke flow | No MinIO container image was available on the machine, so the test is skipped |
| CloudFormation deploy | No AWS account was used |
| GitHub Actions workflow | Checked with actionlint only |

## Features

- Strict dispute state machine: OPENED, EVIDENCE_SUBMITTED, WON, LOST, WITHDRAWN.
- Idempotent create with an Idempotency-Key header.
- Optimistic concurrency with PostgreSQL xmin.
- Keyset paged dispute list.
- Raw SQL aging report.
- Evidence upload to S3-compatible storage with magic-byte validation.
- Prometheus metrics and health checks.
- Container image and CloudFormation template for ECS Fargate, RDS, and S3.

## Tech stack

- .NET 10 LTS
- ASP.NET Core Web API
- EF Core with PostgreSQL
- AWS SDK for .NET S3
- Prometheus metrics
- PostgreSQL 16
- MinIO for local S3-compatible tests

## Local ports

- API: http://127.0.0.1:26600
- PostgreSQL: 127.0.0.1:26632
- MinIO S3: 127.0.0.1:26660
- MinIO console: 127.0.0.1:26661

## Development

The host has no dotnet SDK. Every dotnet command runs through `scripts/dotnet.sh`.

Build the solution:

```
scripts/dotnet.sh build CardDisputes.sln
```

Start the local PostgreSQL and MinIO containers:

```
scripts/test-stack.sh up
```

Run unit and integration tests:

```
scripts/test.sh
```

Run the local security gate:

```
scripts/security-gate.sh
```

Stop the local containers:

```
scripts/test-stack.sh down
```

Run the smoke test against the compose stack:

```
scripts/smoke.sh
```

## Documentation

- `docs/ARCHITECTURE.md`: domain rules, API contract, data model, infrastructure.
- `docs/DECISIONS.md`: short architecture decision records.
- `docs/RUNBOOK.md`: operational procedures.
- `docs/ACCEPTANCE.md`: local verification table.

## License

MIT. See `LICENSE`.
