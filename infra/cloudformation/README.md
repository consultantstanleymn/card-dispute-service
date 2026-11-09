# Card Dispute Service CloudFormation

Label: VERIFIED LOCALLY (lint and checks); AUTHORED, NOT RUN (deploy).

`dispute-service.yaml` defines the AWS resources for the Card Dispute Service.

## Resources

- ECS Fargate cluster and service.
- Task definition with a read-only root file system.
- CloudWatch log group with 30-day retention.
- Application Load Balancer with a `/health/ready` health check.
- RDS PostgreSQL 16 with encryption and deletion protection.
- S3 evidence bucket with SSE, versioning, public access block, and TLS-only bucket policy.
- Task role with `s3:PutObject` and `s3:GetObject` on `disputes/*` only.
- Application Auto Scaling target tracking on CPU at 60 percent.
- CloudWatch alarms for ALB 5xx count, p95 target response time, and RDS CPU.

## Parameters

- `VpcId`
- `PrivateSubnetIds`
- `PublicSubnetIds`
- `ImageUri`
- `DesiredCount`
- `MultiAZ`
- `DbInstanceClass`
- `DatabaseName`
- `DatabaseConnectionStringSecretArn`
- `ApiKeySecretArn`

## Local verification

Run from the repository root:

```
TV=<path to a Python virtual environment with cfn-lint installed>
$TV/bin/pip show cfn-lint >/dev/null || $TV/bin/pip install cfn-lint
$TV/bin/cfn-lint infra/cloudformation/dispute-service.yaml | tee docs/evidence/cfn-lint.txt
test ${PIPESTATUS[0]} -eq 0
$TV/bin/python infra/cloudformation/check_template.py infra/cloudformation/dispute-service.yaml
```

## Deployment status

The template is authored and checked locally. It is not deployed from this repository.
