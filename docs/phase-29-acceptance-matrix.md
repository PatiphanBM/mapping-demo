# Phase 29 Acceptance Matrix

| Requirement | Implementation evidence | Automated evidence | Gate I status |
|---|---|---|---|
| PostgreSQL/SQL Server catalog discovery | `DatabaseTargetSchemaInspector`, canonical snapshot/hash | `DatabaseTargetSchemaInspectorIntegrationTests` | Forced live run pending |
| No Entity/raw SQL config | immutable `DatabaseTableDestination`, logical profile | destination validation/OpenAPI tests | Pass |
| Parameterized INSERT and quoting | provider `IDatabaseWriteAdapter` implementations | `DatabaseWriteAdapterIntegrationTests` | Forced live run pending |
| Typed values/default/generated omission | snapshot-driven write plan/typed `DbParameter` | provider write integration fixtures | Forced live run pending |
| Existing-key reconciliation | composite key query + canonical value compare | same-row/conflict provider fixtures | Forced live run pending |
| No-key ambiguity | stale lease/ambiguous commit → `UnknownOutcome` | persistence/operations integration test | Mapping DB fixture pending live PostgreSQL |
| Durable intent/outbox/encryption | `database_delivery_jobs`, partial-unsent unique outbox | atomic rollback/encryption/retry outbox fixture | Mapping DB fixture pending live PostgreSQL |
| Manual resolution security/audit | gated controller, reason/correlation, row lock, append-only audit | operations integration fixture | Mapping DB fixture pending live PostgreSQL |
| Schema drift notification | bounded monitor, latest checks, UI alerts | compatibility + monitor integration fixture | Mapping DB fixture pending live PostgreSQL |
| Kill switches | feature/profile/runtime activation and pause gates | activation/control tests | Pass; rollout stays off |
| Safe operations UI/API | hashes/redacted errors only; no payload/connection/raw SQL | Playwright + API contracts | Pass |
| Regression/security | backend/web suites, format, dependency scan | Gate I evidence | In progress |
| Owner approval | Mapping, Platform/Security, PostgreSQL and SQL Server owners | signed Gate I record | Pending |

ผล `Pass` ใน matrix ไม่เปิด production write อัตโนมัติ ต้องครบ forced provider tests, controlled canary observation และ owner sign-off ใน Gate I ก่อน
