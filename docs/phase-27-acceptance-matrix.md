# Phase 27 Acceptance Matrix

This is the Gate G traceability index for the Versioned Contract Registry. Execution and owner status are in [phase-27-gate-g-evidence.md](phase-27-gate-g-evidence.md).

| Acceptance ID / scope | Evidence |
|---|---|
| REG-001..008 / AC-REG-ID-01 | migration constraints/triggers and concurrent Draft coverage in `ContractRegistryMigrationTests`; no rename/delete endpoint |
| AC-REG-LCY-01..03 | `ContractRegistryMigrationTests` and `VersionedContractRegistryTests` prove atomic Publish, immutable content/audit and Retired exact history |
| AC-REG-VAL-01 | invalid/oversized/sensitive/unsupported schema tests; Validate and rejected mutations leave lifecycle/audit unchanged |
| AC-REG-CMP-01..02 | compatibility narrowing/widening and breaking acknowledgement tests in `VersionedContractRegistryTests` |
| AC-REG-AUD-01 | one same-transaction audit per successful command, unique request ID and append-only trigger coverage |
| AC-REG-LNK-01 | migrations `0016`/`0017`, exact auto-link test and `ContractVersionInUse` retirement coverage |
| AC-REG-PIN-01 | migration `xmin` assertion plus full historical Mapping/retry regression; no snapshot update in cutover code |
| AC-REG-MOD-01..03 | bundled/PostgreSQL parity, API/Worker-equivalent adapters, cache expiry/outage no-fallback tests |
| AC-REG-CACHE-01 | publish/retire invalidation, exact cache and bounded p95 test |
| AUT/SEC | `ContractAdminControllerTests` covers 401/403, policy split, publish gate, `If-Match`, safe response and OpenAPI fields; schema content policy blocks credentials/private keys |
| Admin lifecycle | typed `ContractAdminController` endpoints cover list/get/history/create/update/validate/clone/publish/retire with safe diagnostics |
| Admin UI | `contract-registry-admin.spec.ts` covers create→validate→publish and malformed JSON; Profile Admin E2E asserts Published-only Contract selection |
| Cutover | `ContractRegistryCutoverTests` proves idempotent two-Contract seed, canonical/hash/path parity, FK backfill, exact history and no runtime fallback |
| Observability | `ContractRegistryObservabilityTests` proves safe labels and availability/integrity, repeated validation and unauthorized publish alerts |
| Full regression | forced-PostgreSQL .NET suite and full Playwright suite cover LegacyTable, ApiContract, JsonFile, profile Admin, retry/reprocess and historical jobs |

## Performance and operations evidence

- Exact immutable resolution caches for at most five minutes; Published list caches for at most fifteen seconds.
- The automated cached exact test performs 64 resolutions, requires one source call and enforces p95 ≤25 ms.
- Metrics and alerts are documented in [the operations runbook](phase-27-contract-registry-operations-runbook.md).
- Production publishing remains disabled until owner approval and an explicit configuration rollout.
