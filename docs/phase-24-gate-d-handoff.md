# Phase 24 Gate D Handoff

## Delivered

- Durable encrypted API Delivery Jobs, attempts, retry leases and outbox dispatch.
- Delivery list/status UI with policy-aware manual retry and authoritative 409 refresh.
- SQL-backed operational metrics and threshold alerts without PII/high-cardinality labels.
- Audited global pause/API-activation controls and config-by-config Shadow/Canary/Expanded rollout.
- Shadow guard that rejects a Production profile, rollback pinning tests and retention cleanup.
- Acceptance traceability and an operator runbook.

## Migrations

- `0009_api_delivery.sql`: Delivery Job, attempts, resend audit, indexes and immutable delivery identity.
- `0010_api_delivery_operations.sql`: runtime controls, rollout/audit tables, metric indexes, nullable expired payload storage and safe retention foreign keys.

Both migrations are expand-only. Rollback disables API activation and pauses the Worker; it does not drop delivery/audit state or reroute pinned jobs.

## Known limits

- Demo authorization uses an explicit server flag plus `X-Operator-Id`; production must replace the hook with the organization IAM/role policy.
- The repository exposes a queryable operations snapshot and UI, but deployment-specific Prometheus/exporter and paging integration remain platform configuration.
- Production endpoint/certificate/OAuth material is intentionally absent. Secret rotation and certificate issuance belong to Security operations.
- Partner compensation after a Delivered side effect is contract-specific and cannot be automated by the generic adapter.
- LegacyTable retirement is not part of Phase 24; retain it until every partner completes a full successful release cycle and retention obligations close.

The `web/app/prototype/file-to-json` screens remain exploratory only. `spec/spec-design-file-to-json-export.md` is not authoritative for the Phase 21–24 delivery path; `spec/spec-design-contract-api-legacy-delivery.md` and `docs/web-config-plan.md` define the implemented target.
