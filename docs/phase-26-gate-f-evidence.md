# Phase 26 Gate F Evidence

Date: 2026-11-02  
Technical status: **PASS**  
Gate F status: **APPROVED**  
Production Admin mutation status: **AUTHORIZED for controlled Shadow → Canary → Expanded rollout; not enabled by this approval alone**

Gate F proves PostgreSQL catalog authority, exact version pinning, safe Admin operation, security controls, rollback and full destination compatibility. It does not alter a pinned job, enable a Production profile, or bypass delivery rollout controls.

## Verification record

PostgreSQL integration was forced with `MAPPING_POSTGRES_INTEGRATION` and `MAPPING_REQUIRE_POSTGRES_INTEGRATION=true`, so integration tests could not silently skip.

| Check | Result |
|---|---|
| Full .NET unit/integration/migration suite | 471/471 passed; 0 failed; 0 skipped |
| .NET formatter | `dotnet format MappingDemo.sln --verify-no-changes --no-restore` passed |
| Web lint and TypeScript | `npm run lint` and `npx tsc --noEmit` passed |
| Production build | Next.js 16.4.3 build passed |
| Full browser E2E | 16/16 passed using the configured deterministic one worker |
| npm vulnerability scan | 0 vulnerabilities |
| NuGet direct/transitive vulnerability scan | 0 vulnerable packages in Api, Worker, Shared and Tests |

The first six-worker E2E run passed 12/16; four `/configs/11` cases timed out while the development server compiled/served the same dynamic route concurrently. The failed file then passed 6/6 serially. Playwright was fixed at one worker and the complete normal command passed 16/16. Assertions and product behavior were unchanged.

## Security review

- Origins are absolute HTTPS DNS origins on port 443 with no user info, path, query or fragment.
- IP literals, loopback, private, link-local, carrier NAT, multicast, documentation, benchmark and reserved ranges are rejected. A mixed public/private DNS answer rejects the whole endpoint.
- DNS is resolved and checked on every new socket connection; pooled connection lifetime is bounded and redirects are disabled.
- Schema redirects fail closed. Schema size remains bounded before and during streaming.
- Path tokens and headers use allowlists; duplicate header names are case-insensitive and transport/auth/cookie headers are forbidden.
- Admin authorization, Production activation permission, operator/reason, optimistic concurrency and append-only audit remain mandatory.
- Metrics/snapshots use bounded safe labels and contain no profile key, hostname, contract, operator, secret reference or payload.
- Package vulnerability scans were clean.

## Regression and pinning evidence

The 471-test backend run covers LegacyTable characterization, ApiContract exact bytes/idempotency, JsonFile deterministic finalization, migration/backfill, retries/reprocess, immutable Config Versions and historical Delivery Jobs. Catalog contract and cutover suites prove API/Worker parity, exact Disabled-version resolution, cache-miss fail-closed behavior and no configuration fallback.

The 16-test browser run covers API Contract editing/preview, Profile Admin create→validate→activate→select, stale concurrency recovery, delivery operations, JsonFile create/preview/activation and rollback to Legacy.

## Performance approval proposal

| Contract | Gate F threshold | Result |
|---|---:|---|
| Admin list page size | 1–100 | Pass; clamped server-side and offset-tested |
| Admin/runtime list query shape | fixed query count, no per-row profile lookup | Pass |
| Active resolution concurrency | 32 concurrent requests after warm-up | Pass |
| Active resolution latency | p95 ≤250 ms | Pass; enforced by PostgreSQL integration test |

These thresholds require Platform/Operations approval in the sign-off table.

## Observability and rollback

The `MappingDemo.ApiDestinationProfiles` Meter records validation result/environment, successful lifecycle mutations, resolution failure class, cache hit/miss and resolution latency. Any pinned-version missing, catalog outage or source mismatch is Critical; any unauthorized activation is Warning; three consecutive validation failures is Warning. The authorized operations endpoint exposes a safe process-local snapshot. Centralized Meter export is required because the local snapshot resets on restart.

Rollback rehearsal proves an immutable historical Disabled Profile Version can become Active without changing either content hash. Existing Mapping Config Versions and Delivery Jobs retain their exact pin. DB-only rollout never falls back automatically to configuration. Detailed steps are in [phase-26-api-destination-profile-operations-runbook.md](phase-26-api-destination-profile-operations-runbook.md).

## Owner sign-off

On 2026-11-02, the user explicitly confirmed Gate F on behalf of Contract/Business, Platform/Operations and Security, accepting the technical evidence, performance thresholds, security controls and rollback plan.

| Owner role | Required approval scope | Status | Approver/date |
|---|---|---|---|
| Contract / Business | identity, contracts/bindings, new-work blocking and rollback semantics | Approved | User-confirmed project authority / 2026-11-02 |
| Platform / Operations | p95/list thresholds, metrics/alerts, rollout and rollback runbook | Approved | User-confirmed project authority / 2026-11-02 |
| Security | authorization, secret handling, SSRF/DNS/redirect controls and redaction | Approved | User-confirmed project authority / 2026-11-02 |

Gate F authorizes controlled Shadow→Canary→Expanded rollout. It does not change runtime controls or enable a Production profile by itself. Any failed threshold, routing doubt, credential/payload exposure, catalog mismatch or missing pin requires pausing dispatch and Admin activation until resolved.
