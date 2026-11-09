# Decisions

## 1. EF Core with raw SQL for reports

Use EF Core for normal data access. The aging report uses `Database.SqlQuery<T>` raw SQL because the report is a small read-only aggregation and avoids adding a second data access library.

## 2. PostgreSQL xmin for optimistic concurrency

Use PostgreSQL `xmin` as the entity version. The API returns it as `version`. State change requests require the current version. This avoids an extra mapped rowversion column.

## 3. Keyset paging instead of offset paging

Use keyset paging on `(opened_at DESC, id DESC)`. Keyset paging stays stable as rows are inserted and does not scan and discard rows the way offset paging does.

## 4. Magic-byte file validation

Check file content by magic bytes, not only by the multipart `Content-Type` header. The header is attacker-controlled. Magic-byte checks reject disguised files before they reach object storage.

## 5. .NET SDK in a container

The host has no dotnet SDK. Every dotnet command runs through `scripts/dotnet.sh`, which uses the official .NET SDK container image. This keeps the host clean and pins the SDK through `global.json`.

## 6. CloudFormation is linted but not deployed

Keep the CloudFormation template in the repository. Run cfn-lint and a local template check script. Do not deploy it in this repository because local acceptance runs on one laptop with podman.
