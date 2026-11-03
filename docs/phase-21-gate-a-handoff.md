# Phase 21 Gate A — Legacy Parity Handoff

Date: 2026-10-19 (Asia/Bangkok)  
Decision: **PASS — engineering sign-off**

## Scope and dependency map

```text
File watcher / File Intake
  -> File Job pins Config Version
  -> File Import Handler -> Input Record Module -> Source Row Writer
  -> Row Job -> RowNormalizeHandler
       -> Transformation Module (pinned program + target schema)
       -> Destination Module
            -> LegacyTableDestinationAdapter
                 -> NormalizedRowWriter -> PostgreSQL normalized table
```

The Config Version `destination_definition` is the only runtime routing source.
`mapping_configs.normalized_table_id` remains a nullable compatibility field and
is not consulted by `RowNormalizeHandler`. Routing is never based on the active
version or a random percentage.

Active dependencies in Gate A are PostgreSQL, Kafka, Source/Normalized dynamic
tables, the existing API, and the web proxy. Partner schema/delivery endpoints,
secret providers, Delivery Jobs, outbox delivery messages, and HTTP delivery
clients are intentionally absent until later phases.

## Verification evidence

| Check | Result |
|---|---|
| Full .NET regression with PostgreSQL | 263/263 passed; 0 failed; 15 s |
| Legacy direct/adapter golden SQL effects | Both feature-flag values passed the same expected rows, attempts, statuses, errors, and reconciliation assertions |
| Critical-path coverage | `RowNormalizeHandler` 82.97% line; its process state machine 95.86% line / 88.46% branch; adapter delivery state machine 88.57% line / 87.5% branch; writer reconciliation state machine 94.33% line / 83.33% branch |
| Formatter | `dotnet format MappingDemo.sln --verify-no-changes --no-restore` passed |
| Web | ESLint passed; production build passed; `/configs/1` returned 200; same-origin `/api/mapping-configs/1` returned the pinned `legacyTable` destination |
| Fixed-width/draft preview | `InputRecordModuleTests` 12/12 and `MappingDraftServiceTests` 12/12 passed |
| Reset/configure demo | Clean reset completed; Orders and PMIB configs/version 1 created and activated |
| CSV runtime through adapter | 200 rows imported: 198 Done, 2 Invalid, 0 Failed; 198 normalized rows |
| Reprocess through adapter | API accepted 100 row jobs; final file status remained 98 Done, 2 Invalid, 0 Failed with stale-output reconciliation intact |
| External side effects | No `delivery_jobs`, Delivery Job type, or `HttpClient` exists in runtime/migrations; no partner API call is possible in Phase 21 |

Coverage was collected with `tests/gate-a-coverage.runsettings`. Guard-only
adapter compile branches are below 80% as a class aggregate; they are not part
of the delivery critical path. The supported destination and unsupported-kind
diagnostics have contract tests, while delivery/reconciliation exceeds the Gate
A threshold.

## Risk register

| Risk | Owner | Mitigation | Rollback trigger |
|---|---|---|---|
| Legacy SQL/status drift | Mapping runtime owner | Direct and adapter paths share golden SQL-effect assertions | Any row/value/status/error/attempt mismatch |
| Transaction split causes locks or partial completion | Worker/DB owner | Evaluate transaction commits before adapter-owned delivery transaction; retry remains bounded | Lock timeout, duplicate output, or incomplete Row Job completion |
| Destination pin is bypassed | Config API owner | Version JSON drives both target table lookup and adapter compile; compatibility field is nullable | Job routes to a table not named by its pinned version |
| ApiContract becomes creatable too early | API/Web owner | API flag defaults false; UI option is hidden or disabled with dependency text | Any ApiContract version created before Phase 23 gate |
| Old clients omit destination | API owner | Boundary/service normalization maps legacy `normalizedTableId` to `legacyTable` | Old create/version fixture fails or returns a destination-less version |
| Rollback binary cannot read expanded schema | Release owner | Migration is expand-only; legacy compatibility field/FK retained | Previous application fails against schema version 7 |

## Rollback

Set the worker environment override and restart only the Worker:

```powershell
$env:Features__UseDestinationModule = "false"
dotnet run --project src/MappingDemo.Worker/MappingDemo.Worker.csproj
```

For a deployed service, set `Features__UseDestinationModule=false` in its
environment and restart/roll the Worker instances. This restores the direct
`NormalizedRowWriter` path. It does not change a job's pinned Config Version or
destination. The expand-only schema remains in place.

To return to the adapter after investigation, set the value to `true` (or
remove the override, because the default is true) and restart the Worker.

Gate A is signed off for beginning Phase 22; this handoff does not authorize
ApiContract creation or delivery.
