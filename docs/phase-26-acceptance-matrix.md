# Phase 26 Acceptance Matrix

This is the Gate F evidence index for versioned API Destination Profile administration. Execution details and owner status are in [phase-26-gate-f-evidence.md](phase-26-gate-f-evidence.md).

| Acceptance ID | Evidence |
|---|---|
| AC-PRO-ID-01..02 | catalog migration constraints and concurrent Draft tests in `ApiDestinationProfileCatalogMigrationTests`; API has no rename/delete path |
| AC-PRO-LCY-01..03 | migration lifecycle/immutability tests and `ApiDestinationProfileAdminControllerTests` activation, disable, rollback and audit coverage |
| AC-PRO-AUT-01..02 | Admin controller unauthorized, policy projection and Production activation tests |
| AC-PRO-CON-01 | stale `If-Match` controller test returns `412` without overwrite |
| AC-PRO-VAL-01..02 | catalog validation, missing-secret mutation-free validation and validated-content-hash activation coverage |
| AC-PRO-SEC-01 | safe projection/controller/observability serialization tests; web E2E scans browser-visible flows without credential values |
| AC-PRO-SEC-02 | `ApiDestinationProfileNetworkPolicyTests`, catalog URL/template/header tests and `HttpContractSchemaFetcherTests`; redirects off and DNS is revalidated at connection time |
| AC-PRO-PIN-01..03 | PostgreSQL catalog contract, delivery scheduling/row normalization and cutover backfill tests preserve exact version identity after activation/disable |
| AC-PRO-CAT-01..02 | shared catalog contract, cache/outage/no-fallback and API/Worker activation visibility tests |
| AC-PRO-CUT-01..03 | `ApiDestinationProfileCutoverMigrationTests`, Compare canonical parity/mismatch and mutation-freeze tests |
| AC-PRO-ROLL-01 | Compare rollback rehearsal plus historical Disabled version resolution tests |
| AC-PRO-AUD-01 | append-only migration guard and lifecycle audit completeness test |
| AC-PRO-UI-01 | `web/e2e/api-destination-profiles-admin.spec.ts` create→validate→activate→select and stale edit recovery |
| AC-PRO-REG-01 | forced-PostgreSQL full .NET suite and full Playwright suite cover LegacyTable, ApiContract, JsonFile, retry/reprocess and historical jobs |

## Performance and operations evidence

- Admin list accepts bounded `limit`/`offset`; server clamps page size to 1–100.
- Admin list and runtime safe list use a fixed query count; neither performs per-profile lookups.
- Active profile resolution uses one database roundtrip. The PostgreSQL test runs 32 concurrent resolutions after warm-up and enforces p95 ≤250 ms.
- Metrics cover validation outcomes, successful activation/disable/rollback, resolution failure classes, cache hit/miss, latency and unauthorized activation.
- Required alerts and label redaction are asserted by `ApiDestinationProfileObservabilityTests`.
- Onboarding, rotation, rollback and incident procedures are in [phase-26-api-destination-profile-operations-runbook.md](phase-26-api-destination-profile-operations-runbook.md).

## Gate F verification

- PostgreSQL-required .NET suite: 471/471 passed, 0 skipped, 0 failed.
- Formatter, web lint, TypeScript and Next.js 16.4.3 production build passed.
- Full browser E2E: 16/16 passed with deterministic one-worker execution.
- npm and NuGet direct/transitive scans reported 0 vulnerable packages.
- Contract/Business, Platform/Operations and Security owner sign-off was confirmed by the user on 2026-11-02; Gate F is approved for controlled rollout.
