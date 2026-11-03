# Phase 28 Acceptance Matrix

This is the Gate H traceability index for Table Administration and physical schema verification. Execution and owner status are in [phase-28-gate-h-evidence.md](phase-28-gate-h-evidence.md).

| Acceptance ID / scope | Evidence |
|---|---|
| TBL-001..005 | `TablePhysicalSchema`, existing `DdlBuilder`/validator tests and PostgreSQL healthy Source/Normalized coverage |
| AC-TBL-HEALTH-01 | `TableSchemaInspectorIntegrationTests.Healthy_source_and_normalized_tables_match_metadata` |
| AC-TBL-DRIFT-01..02 | integration fixtures alter extra/missing/type/unique constraint and assert field-level codes |
| AC-TBL-MISSING-01 | dropped physical table returns Missing/TableMissing without DDL |
| AC-TBL-FAIL-01 | unreachable catalog returns safe VerificationFailed/CatalogUnavailable without host/credential text |
| AC-TBL-CREATE-01 | existing transaction tests plus `table-admin.spec.ts` create→redirect→auto-verify Healthy |
| AC-TBL-ADD-01 | row-lock concurrent add-column integration test, transactional audit/status reset and E2E add→verify |
| AC-TBL-AUTH-01 | authorizer/kill-switch unit tests and E2E mutation-hidden state; production flag false |
| AC-TBL-IMPACT-01 | bounded references query projects Config/Version active state and pinned job count; detail E2E renders it |
| AC-TBL-SEC-01 | parameterized malicious name test, typed HTTP contracts and E2E absence of destructive/data/SQL controls |
| List/rate limit | fixed-query search/kind/status, limit 1–100; per-table 12/minute limiter unit coverage |
| Audit | migrations `0018`/`0019`, same-transaction create/add audit, safe failure audit, persisted verification history and database append-only guards |
| Observability | safe-label unit test and drift/repeated-failure/unauthorized alerts; operations endpoint |
| Rollout/rollback | mutation kill switch retains read/verify; procedures in operations runbook |
| Full regression | PostgreSQL-required .NET suite and Playwright cover LegacyTable, ApiContract, JsonFile, Profile/Contract Admin and Table Admin |

## Security and performance contracts

- Catalog lookup is parameterized and resolves table name from server metadata; no browser schema/query/connection field.
- Catalog command timeout is five seconds; verification is explicit and rate-limited rather than polled.
- List uses persisted latest status and does not issue per-row catalog queries.
- Metrics have bounded labels and diagnostics contain structural values only.
- Gate H is approved; Production Table Admin mutation remains disabled until a separate controlled rollout change.
