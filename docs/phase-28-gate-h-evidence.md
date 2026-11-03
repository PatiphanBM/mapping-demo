# Phase 28 Gate H Evidence

Date: 2026-11-03  
Technical status: **PASS WITH DOCUMENTED DEV-TOOL ADVISORY**  
Gate H status: **APPROVED**  
Production Table Admin mutations: **DISABLED** (`TableAdministration:MutationsEnabled=false`)

Gate H validates typed expand-only table administration, read-only physical verification, safe impact projection, authorization/audit, observability and rollback. It does not enable production mutation, auto-repair drift, alter immutable Config Versions or rewrite pinned jobs.

## Verification record

PostgreSQL integration was forced with `MAPPING_REQUIRE_POSTGRES_INTEGRATION=true`; tests could not silently skip.

| Check | Result |
|---|---|
| Full .NET/PostgreSQL regression | 514/514 passed, 0 failed/skipped |
| Table Inspector PostgreSQL integration | Healthy Source/Normalized, extra/missing/type/constraint drift, missing relation, injection-safe lookup, persisted history/audit passed |
| Full Playwright | 20/20 passed; includes Table create→redirect→Healthy, add→Healthy, drift and kill switch |
| .NET build/format | build 0 warnings/errors; `dotnet format --verify-no-changes` passed |
| Web quality | TypeScript, ESLint and Next.js 16.4.3 production build passed; detail route Partial Prerendered |
| Local startup | migration `0018`, Postgres-mode API startup, permissions and bounded list passed; migration `0019` append-only guards passed in forced PostgreSQL regression |
| npm production vulnerability scan | 0 vulnerabilities |
| NuGet direct/transitive vulnerability scan | 0 vulnerable packages across Api, Worker, Shared and Tests |

The all-dependency npm audit reports 16 moderate findings in the development-only ESLint chain rooted in `minimatch <11.0.1`. Current tooling has only a breaking/downgrade-style forced fix. These packages are not shipped in the production runtime; `npm audit --omit=dev` reports zero. Gate H owners must accept the existing bounded toolchain exception or defer approval.

## Security review

- Browser contracts contain typed definitions/table ID only; no raw SQL, schema/connection field or business-row operation.
- PostgreSQL catalog name is a parameter resolved from authoritative metadata. A malicious identifier fixture cannot execute SQL.
- Permissions separate read/create/add-column; production mutation is configuration-gated.
- Actor/reason/correlation and same-transaction success audit cover mutations.
- Verification is read-only, rate-limited and returns safe structural diagnostics.
- UI exposes no drop, rename, alter type/length, truncate, delete, preview, search or export of rows.

## Operational review

- Latest persisted status keeps list bounded and avoids N+1 catalog inspection.
- Per-table rate is 12/minute; catalog timeout is five seconds; alerts cover Drifted/Missing, repeated failure and unauthorized mutation.
- Kill switch disables create/add-column while list/detail/verify remains available.
- Runbook requires impact analysis and backup; drift is never auto-repaired.
- Existing Config Versions and File Jobs remain immutable/pinned through schema inspection and additive changes.

## Owner sign-off

| Owner role | Required approval scope | Status | Approver/date |
|---|---|---|---|
| Platform / Operations | expected schema/comparison, catalog access, rate/alerts, backup/restore and rollout | Approved | User confirmation / 2026-11-03 |
| Security | permissions/gate, parameterization, redaction, audit and dev-tool advisory | Approved | User confirmation / 2026-11-03 |
| Mapping | Source/Normalized rules, usage impact, additive compatibility and pinned-job behavior | Approved | User confirmation / 2026-11-03 |

On 2026-11-03, the user explicitly approved Gate H for all three owner roles and accepted the bounded 16-finding development-only ESLint/minimatch advisory. Approval authorizes a controlled mutation rollout; it does not toggle `MutationsEnabled` or mutate any table automatically.
