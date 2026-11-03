# Phase 27 Gate G Evidence

Date: 2026-11-03  
Technical status: **PASS WITH DOCUMENTED DEV-TOOL ADVISORY**  
Gate G status: **APPROVED**  
Production Contract publishing: **DISABLED** (`ContractRegistry:PublishingEnabled=false`)

Gate G validates registry authority, immutable publication, exact pinning, safe Admin operation, cutover, observability and rollback. It does not rewrite a Contract Snapshot, alter an existing Delivery Job or enable Production publishing by itself.

## Verification record

PostgreSQL integration was forced with `MAPPING_REQUIRE_POSTGRES_INTEGRATION=true`; tests could not silently skip.

| Check | Result |
|---|---|
| Full .NET unit/integration/migration suite | 494/494 passed; 0 failed; 0 skipped |
| Phase 27 focused backend coverage | 23 Contract Registry/Admin/cutover/observability tests passed within the final suite |
| .NET formatter | passed |
| Web lint and TypeScript | passed |
| Next.js 16.4.3 production build | passed; `/admin/contracts` prerendered successfully |
| Full Playwright | 18/18 passed with deterministic one-worker execution |
| npm production vulnerability scan | 0 vulnerabilities |
| NuGet direct/transitive vulnerability scan | 0 vulnerable packages across Api, Worker, Shared and Tests |
| Local startup/cutover | migration `0017`, idempotent seed/parity check and Postgres-mode API startup passed |

The all-dependency npm audit reports 16 moderate findings in the development-only ESLint chain, rooted in `minimatch <11.0.1`. Current `eslint@9.39.5` and `eslint-config-next@16.4.3` resolve legacy `minimatch` majors and npm offers only incompatible/downgrade-style fixes. These packages are not shipped in the production Next.js runtime; `npm audit --omit=dev` reports zero. Gate G owners must accept this bounded toolchain exception or defer approval until upstream-compatible releases are available.

## Security review

- Read, Draft-write and Publish/Retire permissions are separate; production publish is additionally configuration-gated.
- Mutations require actor, unique request ID and 10–500 character reason; Update/Publish/Retire require `If-Match`.
- Request body/schema size is bounded. Remote/recursive references, ambiguous unions, unsupported constructs and malformed content fail validation.
- Content policy rejects credential/token/private-key values; browser/OpenAPI cannot supply authoritative hash, canonical bytes or path catalog.
- Published/Retired rows and append-only audit are protected by database triggers; no hard-delete route exists.
- Profile linkage resolves exact Published registry identity. Retired/Draft/unknown Versions cannot be newly attached; in-use Published Versions cannot retire.
- Registry outage and hash mismatch fail closed with no Bundled fallback.
- Metrics expose bounded action/result/failure/cache labels, never Contract identity, actor, schema, payload or secret.

## Cutover and regression

`proposal/1` and `claims/2` seed idempotently from bundled bootstrap inputs. Tests compare canonical bytes, SHA-256 and full path catalogs before accepting PostgreSQL authority. API snapshot creation now fetches through `IVersionedContractRegistry`; API and Worker register PostgreSQL adapters against the same database. Bundled mode is explicit rollback/local-test only and freezes mutation.

The 494-test backend run covers all three destinations, exact profile/Contract pinning, compatibility, audit, cache/outage, retry/reprocess, seed restart and historical Retired resolution. The 18-test browser run covers Contract Admin publication, malformed JSON, Published-only profile attachment, API Contract workbench, Profile Admin, delivery operations and JsonFile workflows.

## Performance and rollback

| Contract | Gate G proposal | Result |
|---|---:|---|
| Exact cache TTL | ≤5 minutes | Pass; constructor-enforced |
| Published-list cache TTL | ≤15 seconds | Pass; constructor-enforced |
| Cached exact resolution | p95 ≤25 ms over 64 calls | Pass; one source call enforced |

Rollback removes the new Version from active allowlists, restores the previous Profile Version and then Retires the new Contract without altering either Published artifact. Existing Mapping Config Versions and Delivery Jobs continue from immutable snapshots. Emergency Bundled source rollback is limited to the original bootstrap pair and never occurs automatically. Detailed procedures are in [the operations runbook](phase-27-contract-registry-operations-runbook.md).

## Owner sign-off

| Owner role | Required approval scope | Status | Approver/date |
|---|---|---|---|
| Contract / Business | lifecycle, compatibility/breaking acknowledgement, profile selection and rollback semantics | Approved | User confirmation / 2026-11-03 |
| Platform / Operations | Postgres authority, cache/p95 thresholds, metrics/alerts, source rollback and recovery | Approved | User confirmation / 2026-11-03 |
| Security | authorization/publish gate, schema content policy, redaction, audit and dev-tool advisory exception | Approved | User confirmation / 2026-11-03 |

On 2026-11-03, the user explicitly approved Gate G for all three owner roles and accepted the bounded 16-finding development-only ESLint/minimatch advisory. Approval authorizes a controlled publishing rollout; it does not toggle `PublishingEnabled` or publish any Contract automatically.
