# Acceptance

Each row is one claim. The commit is the code commit that holds the work.

Labels:

- VERIFIED LOCALLY: the command ran on this laptop and passed.
- AUTHORED, NOT RUN: the code or template exists, but it was not run, or the check was skipped because a service was missing.

| Claim | Command | Evidence file | Commit | Label |
| --- | --- | --- | --- | --- |
| Domain rules: test-first commit with failing tests before the code | git show --stat fd31f40 85a5ef7 | - | fd31f40, 85a5ef7 | VERIFIED LOCALLY |
| Domain unit tests pass (23 at that commit, 30 now) | scripts/test.sh | docs/evidence/full-gate-local.txt | 85a5ef7 | VERIFIED LOCALLY |
| EF Core model, initial migration, xmin concurrency token, append-only events trigger | scripts/test.sh (MigrationTests, ConcurrencyTests, EventsAppendOnlyTests) | docs/evidence/full-gate-local.txt | f46002b | VERIFIED LOCALLY |
| API: idempotent create, transitions, auth, problem details, parallel decide (one wins) | scripts/test.sh (DisputesApiTests, IdempotencyTests, AuthTests) | docs/evidence/full-gate-local.txt | 2c96e60 | VERIFIED LOCALLY |
| Keyset paging (ties, insert, delete between pages) and raw-SQL aging report | scripts/test.sh (PagingTests, AgingReportTests, CursorTests) | docs/evidence/full-gate-local.txt | 7e289a0 | VERIFIED LOCALLY |
| Evidence: magic-byte checks, 5 MB limit, closed dispute 409, S3 down gives 503 and no row | scripts/test.sh (EvidenceTests, FileSignatureTests) | docs/evidence/full-gate-local.txt | 80fe869 | VERIFIED LOCALLY |
| Evidence stored in S3 API and read back with SHA-256 (MinIO) | scripts/test.sh (Upload_Stores_Object_And_Row_With_Sha256) | docs/evidence/full-gate-local.txt | 80fe869 | AUTHORED, NOT RUN (test skipped: no MinIO image on this machine) |
| Prometheus metrics, JSON logs without API key, readiness check for database | scripts/test.sh (ObservabilityTests) | docs/evidence/full-gate-local.txt | 80657f7 | VERIFIED LOCALLY |
| Container image builds, runs as user app, Trivy HIGH and CRITICAL clean | podman build -t card-dispute-service:local . ; trivy image --severity HIGH,CRITICAL --ignore-unfixed card-dispute-service:local | docs/evidence/image-scan.txt | af4078d | VERIFIED LOCALLY |
| Reduced smoke flow against the compose stack (create, get, list, aging, metrics, liveness) | podman-compose up -d cds-postgres cds-api ; scripts/smoke.sh | docs/evidence/smoke-local.txt | cff5e6c | VERIFIED LOCALLY (reduced: no MinIO) |
| Full smoke flow with upload, submit and decide | scripts/smoke.sh with MinIO running | - | af4078d | AUTHORED, NOT RUN |
| CloudFormation lint and template checks | cfn-lint infra/cloudformation/dispute-service.yaml ; python infra/cloudformation/check_template.py infra/cloudformation/dispute-service.yaml | docs/evidence/cfn-lint.txt | 75c4335 | VERIFIED LOCALLY (lint and checks) |
| CloudFormation deploy | aws cloudformation deploy | - | 75c4335 | AUTHORED, NOT RUN |
| Local security gate (gitleaks, trivy fs, vulnerable packages, actionlint) | scripts/security-gate.sh | docs/evidence/security-gate-local.txt | 6ec94b9 | VERIFIED LOCALLY |
| GitHub Actions workflow | push and run the workflow | - | 6ec94b9 | AUTHORED, NOT RUN (lint with actionlint only) |
| Full local gate with test counts (30 unit, 32 passed and 1 skipped integration) | scripts/test-stack.sh up && scripts/test.sh && scripts/security-gate.sh && scripts/test-stack.sh down | docs/evidence/full-gate-local.txt | edadf8c | VERIFIED LOCALLY |
