# AWS infrastructure (placeholder)

Production topology described in the architecture plan. CDK / Terraform to land in a follow-up.

## Target services
- **ECS Fargate** for `PublicApi`, `PrivateApi`, `Worker` (3 services, separate task definitions).
- **Aurora Postgres Serverless v2** for the `orchestrator`, `messaging`, `ops_read`, `customer_read` schemas (one cluster, four schemas; split clusters later if read load justifies).
- **API Gateway HTTP API** + internal **ALB** in front of `PublicApi`. Internal **ALB** only in front of `PrivateApi`. No public route to `PrivateApi`.
- **SQS** standard + FIFO queues per Wolverine-managed flow. **SNS** topic for cross-system fan-out. **EventBridge** for cross-account events later.
- **S3** for label PDFs and bulk export artifacts.
- **Secrets Manager** + **Parameter Store** for connection strings, OAuth credentials, per-carrier API keys.
- **KMS** customer-managed key for envelope encryption of OAuth tokens (replaces the local `AesEnvelopeEncryptor`).
- **Cognito** — two user pools (tenants, staff). Bearer JWT validated by both API hosts.
- **ECR** — three image repos: `public-api`, `private-api`, `worker`.
- **ADOT collector → CloudWatch / AMP / X-Ray** for OpenTelemetry export.

## Subnet layout
- Public subnets — API Gateway endpoints, internet-facing ALB.
- Private subnets — Fargate tasks, Aurora cluster, internal ALB.
- Isolated subnets — Aurora secondary AZ, KMS endpoints.

## Per-schema DB roles (defense-in-depth)
- `orchestrator_app` — owner of `orchestrator` + `messaging` schemas. Used by `Worker` and connection-string default.
- `customer_read_role` — `SELECT` on `customer_read` only. Used exclusively by `PublicApi`.
- `ops_read_role` — `SELECT` on `ops_read` only. Used exclusively by `PrivateApi` for read endpoints.

This file is intentionally a stub. The CDK stack will land here.
