# Phase 25 Gate E Evidence

Date: 2026-10-26  
Technical status: **PASS**  
Gate E status: **APPROVED**  
Production activation status: **NOT ENABLED; authorized only for controlled config-by-config rollout**

Gate E validates the three immutable destination paths together. It does not authorize rerouting a pinned File Job, dual delivery, or enabling a Production File Destination Profile by itself.

## AC-JF-E2E-01 trace

| Required behavior | Automated evidence |
|---|---|
| `.txt`, `.csv`, `.xlsx`, `.xls` import to Source with no partial invalid import | `FileImportHandlerInputMatrixIntegrationTests.Four_extensions_import_and_invalid_or_rollback_excel_write_no_partial_rows` |
| Nested documents, arrays and system bindings use the shared contract pipeline | `JsonFileContractParityTests.Api_and_json_file_build_equal_documents_from_same_contract`, contract fixture/path/assembler suites |
| Typed `add` and `concat`, Unicode, null and overflow semantics | `AddConcatExpressionTests` compiler/evaluator suites |
| Zero output publishes deterministic `[]` | `JsonFileFinalizerIntegrationTests.Publishes_empty_array_when_every_row_was_filtered` |
| One output preserves pinned formatting and typed JSON scalars | `JsonFileFinalizerIntegrationTests.Publishes_pinned_indented_format_with_unicode_and_scalars` |
| Many outputs have deterministic bytes, order, count and hash | `JsonFileFinalizerIntegrationTests.Publishes_ordered_compact_utf8_and_records_metadata`, `Streams_large_ordered_artifact_with_exact_byte_hash` |
| Parallel row completion and finalizer claims create one export/artifact | `JsonFileDestinationAdapterIntegrationTests.Parallel_last_rows_create_one_export_job_and_one_finalization`, `JsonFileFinalizerIntegrationTests.Final_path_is_invisible_until_atomic_publish_and_parallel_claim_is_single` |
| Retry and crash recovery do not leave partial or duplicate artifacts | `Retries_failure_before_rename_without_partial_artifact`, `Reconciles_crash_after_rename_without_duplicate_artifact`, `Reclaims_stale_finalization_lease_with_same_staged_payload` |
| Reprocess reconciles `2 → 1 → 0` and allocates a monotonic audited revision | `JsonFileDestinationAdapterIntegrationTests.Reprocess_reconciles_one_source_row_from_two_to_one_to_zero_outputs`, `JsonFileReprocessIntegrationTests.New_config_version_allocates_new_revision_and_audit_link` |
| Invalid/failed/oversize/collision/storage failures publish no partial artifact | JsonFile finalizer failure-injection suite plus `JsonExportPublisherTests` traversal, reparse-point, collision, quota and low-disk coverage |

The acceptance chain is composed at stable public seams: Input Module → Source persistence, Transformation/Contract Module → mapped documents, Destination Adapter → encrypted staging, and Finalizer → atomic artifact. This keeps each failure local while exercising the same production interfaces used by the complete flow.

## AC-JF-E2E-02 trace

| Compatibility requirement | Automated evidence |
|---|---|
| Missing/Legacy/API config JSON and side effects remain at the Phase 25 baseline | `Phase25CompatibilityBaselineTests`, destination contract/migration suites |
| LegacyTable SQL row/status parity | `RowNormalizeHandlerIntegrationTests.Legacy_table_preserves_golden_sql_effects`, `NormalizedRowWriterIntegrationTests` |
| ApiContract exact bytes and idempotency remain stable | `DeliveryHttpAdapterTests.Sends_exact_bytes_and_approved_headers`, `Commit_before_timeout_preserves_the_idempotency_key`, `ApiContractSchedulingIntegrationTests.Scheduling_is_atomic_encrypted_and_idempotent` |
| JsonFile produces no Normalized write, Delivery Job or outbound HTTP | `JsonFileDestinationAdapterIntegrationTests.Row_completion_and_encrypted_stage_commit_atomically` and destination-router suites |
| Config Versions remain immutable across clone/activate/intake | `Phase25CompatibilityBaselineTests.Clone_activation_and_intake_keep_versions_pinned` |
| Variant C create/clone/activate/rollback uses production UI/API shapes | `web/e2e/json-file-workbench.spec.ts`, including JSON activation followed by rollback to the prior Legacy version |
| Operations metadata and controls reveal no payload/root/credential | `JsonExportOperationsIntegrationTests`, `JsonExportPublisherTests`, `web/e2e/delivery-operations.spec.ts` |

## Verification record

The PostgreSQL container was healthy and integration tests were forced on with both `MAPPING_POSTGRES_INTEGRATION` and `MAPPING_REQUIRE_POSTGRES_INTEGRATION=true`.

| Check | Result |
|---|---|
| Full .NET unit/integration/migration suite | 404/404 passed; 0 failed; PostgreSQL tests could not short-circuit |
| .NET formatter | `dotnet format MappingDemo.sln --no-restore --verify-no-changes` passed |
| Web lint and TypeScript | `npm run lint` and `npx tsc --noEmit` passed |
| Production build | Next.js 16.4.3 build passed |
| Full browser E2E | 9/9 passed with `--workers=1`; the first six-worker run exhausted the local Node heap before assertions completed |
| npm vulnerability scan | 0 vulnerabilities |
| NuGet direct/transitive vulnerability scan | 0 vulnerable packages in Api, Worker, Shared and Tests |

Security remediation applied during the gate:

- Next.js and `eslint-config-next` upgraded from 16.4.0 to 16.4.3; `experimental.agentUpgrade` remains set to `security`.
- Worker pins patched `System.Text.Json` 8.0.5.
- Tests pin patched `System.Net.Http` 4.3.4 and `System.Text.RegularExpressions` 4.3.1 to replace vulnerable legacy transitive versions.

## Rollback rehearsal result

The browser acceptance test activates the new JsonFile version and then activates the prior LegacyTable version. Existing integration coverage verifies File Jobs keep their pinned Config Version, Json export identity and staged bytes. Runtime controls remain fail-closed: new JsonFile activation defaults off and the finalizer defaults paused. This sign-off records authorization but does not change either runtime control.

## Owner sign-off

On 2026-10-26, the user explicitly confirmed Gate E on behalf of Business/Contract, Platform/Operations and Security, approving the test evidence and rollback plan. No Production File Destination Profile or kill switch was enabled as part of this sign-off.

| Owner role | Approval | Name/date |
|---|---|---|
| Business / Contract | Approved | User-confirmed project authority / 2026-10-26 |
| Platform / Operations | Approved | User-confirmed project authority / 2026-10-26 |
| Security | Approved | User-confirmed project authority / 2026-10-26 |

After approval, Platform/Operations may enable JsonFile activation config-by-config in the Shadow root, then Canary and Expanded. A failed threshold or any hash/path/payload leak requires disabling new activation, pausing the finalizer and activating the prior Config Version only for new File Jobs.
