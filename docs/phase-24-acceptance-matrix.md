# Phase 24 Acceptance Matrix

This matrix is the Gate D evidence index. One large scenario is intentionally split into deterministic tests so failures identify the broken boundary.

| Requirement / gate | Automated evidence |
|---|---|
| `.txt`, `.csv`, `.xlsx`, `.xls` import atomically to Source | `FileImportHandlerInputMatrixIntegrationTests` |
| nested object, fixed array and grouped arrays; empty-slot/seq semantics | `TransformationV2Tests`, `TransformationCompilerTests`, contract fixture suites |
| system-bound and mapped values coexist; final contract validation | `PayloadBindingModuleTests`, `ApiContractSnapshotIntegrationTests` |
| one request per output and no Normalized writes in API mode | `ApiContractRowNormalizeIntegrationTests`, `ApiContractSchedulingIntegrationTests` |
| retry keeps exact bytes/hash/key; 400 permanent; 408/429/5xx retry | `ApiContractSchedulingIntegrationTests`, `DeliveryHttpAdapterTests`, `DeliveryRetryPolicyTests` |
| reprocess/resend separation and resend audit | `ApiContractSchedulingIntegrationTests` |
| second profile without core changes | `ApiDestinationProfileCatalogTests`, `api-contract.spec.ts` |
| LegacyTable parity remains green in parallel | existing Worker/Transformation/NormalizedRowWriter characterization suites |
| Shadow has no production side effect | stub scenario fixture, `DeliveryStubFixtureTests`, non-production profile gate in `DeliveryOperationsIntegrationTests` |
| pause/activation switches and config-by-config canary | `DeliveryOperationsIntegrationTests` |
| rollback keeps old jobs pinned and routes new jobs to Legacy | `DeliveryOperationsIntegrationTests.Rollback_pins_existing_api_job_and_routes_new_file_to_legacy_version` |
| operations UI mixed states, safe retry, stale 409 refresh, no body/credential | `delivery-operations.spec.ts` |
| payload/audit retention | `DeliveryOperationsIntegrationTests` plus hourly `DeliveryRetentionService` |

## Failure and security coverage

- Stub fixture covers 2xx, 400, 408, 429, 5xx, timeout, commit-before-timeout and idempotency conflict.
- HTTP adapter never reads or persists partner response bodies; UI/API detail exposes hashes and redacted errors, not payload bytes or secret references.
- Profile URLs and headers are server allowlisted; authentication values come from the secret provider.
- API runtime uses the pinned schema/bindings and never fetches the latest schema.

## Gate D result

Gate D passes only when backend tests (with PostgreSQL integration required), web lint/build/E2E, formatting, JSON validation and the log/security scan are green. Production promotion still needs the named owners to observe one release cycle; completing implementation does not claim that external observation has elapsed.
