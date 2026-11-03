# Phase 25 Acceptance Matrix

This matrix is the Gate E evidence index for the three immutable destination paths. The detailed execution record and owner approval are in [phase-25-gate-e-evidence.md](phase-25-gate-e-evidence.md).

| Acceptance requirement | Automated evidence |
|---|---|
| `.txt`, `.csv`, `.xlsx`, `.xls` import atomically to Source | `FileImportHandlerInputMatrixIntegrationTests` |
| nested objects, fixed arrays, Array Groups and system bindings use the shared contract path | `TransformationV2Tests`, `PayloadBindingModuleTests`, contract schema fixture suites |
| typed `add`/`concat`, Unicode, null and numeric overflow semantics | `AddConcatExpressionTests` |
| zero output publishes deterministic `[]` | `JsonFileFinalizerIntegrationTests.Publishes_empty_array_when_every_row_was_filtered` |
| one/many output bytes, order, UTF-8, formatting, count and SHA-256 are deterministic | `JsonFileFinalizerIntegrationTests` exact-byte, ordering and large-stream scenarios |
| parallel completion creates one export/artifact | `JsonFileDestinationAdapterIntegrationTests.Parallel_last_rows_create_one_export_job_and_one_finalization`, finalizer parallel-claim coverage |
| retry/crash recovery creates no partial or duplicate artifact | finalizer failure-before-rename, crash-after-rename, stale-lease and collision/hash tests |
| reprocess `2 → 1 → 0` reconciles outputs and allocates an audited revision | `JsonFileDestinationAdapterIntegrationTests`, `JsonFileReprocessIntegrationTests` |
| invalid/failed latest row blocks the whole artifact | readiness/finalizer blocked-export integration tests |
| JsonFile creates no Normalized row, Delivery Job or outbound HTTP | `JsonFileDestinationAdapterIntegrationTests.Row_completion_and_encrypted_stage_commit_atomically`, destination-router tests |
| LegacyTable and ApiContract remain at the compatibility baseline | `Phase25CompatibilityBaselineTests`, legacy characterization, `DeliveryHttpAdapterTests`, `ApiContractSchedulingIntegrationTests` |
| create/clone/activate JsonFile and rollback Legacy use production UI/API shapes | `web/e2e/json-file-workbench.spec.ts` |
| relative-path-only operations, retry authorization and no payload/root/credential exposure | `JsonExportOperationsIntegrationTests`, `JsonExportPublisherTests`, `web/e2e/delivery-operations.spec.ts` |
| traversal/reparse/collision/quota/disk/permission/oversize fail safely | `JsonExportPublisherTests` and finalizer failure-injection suite |
| cleanup honors retention and legal hold | cleanup scenarios in `JsonExportOperationsIntegrationTests` |

## Gate E verification

- PostgreSQL-required .NET suite: 404/404 passed, 0 failed.
- Formatter, web lint, TypeScript and Next.js 16.4.3 production build passed.
- Full browser E2E: 9/9 passed with one worker; the initial six-worker attempt exhausted the local Node heap before assertions completed.
- npm and NuGet direct/transitive vulnerability scans reported zero vulnerabilities.
- Business/Contract, Platform/Operations and Security sign-off was recorded on 2026-10-26.

Gate E authorizes controlled rollout; it does not itself enable the Production File Destination Profile, JsonFile activation switch or finalizer.
