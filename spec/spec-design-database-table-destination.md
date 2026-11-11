---
title: Database Table Destination Design
version: 1.0
date_created: 2026-11-09
last_updated: 2026-11-09
owner: Mapping Demo Mapping, Platform/Operations, Security and Target Database owners
tags: [design, database-destination, postgresql, sql-server, schema-drift, delivery]
---

# Introduction

This specification defines the `DatabaseTable` destination for delivering mapped outputs to existing PostgreSQL and SQL Server tables. It defines server-owned connection profiles, target-schema discovery, immutable schema snapshots, compatibility rules, external delivery semantics, duplicate reconciliation, ambiguous outcomes, authorization, security, observability and rollout.

The design is accepted for Phase 29 implementation on 2026-11-09. This acceptance is not Gate I production approval. Every production profile remains read-only or write-disabled until the Gate I evidence and owner sign-off are complete.

## 1. Purpose & Scope

The system shall let an authorized user select an allowlisted target database profile, schema and existing table, inspect writable fields, map Transformation Program outputs to those fields and deliver each mapped output through a parameterized `INSERT`.

Phase 29 v1 is limited to:

- PostgreSQL and SQL Server targets.
- Existing target tables. The system does not create, alter, drop or repair target objects.
- One destination per Config Version. No dual delivery.
- `INSERT` only. Generic `UPDATE`, `UPSERT`, `DELETE`, stored procedures and browser-supplied SQL are out of scope.
- Server-owned profiles and secrets.
- Existing target unique constraints for optional reconciliation.
- Manual resolution when an outcome cannot be proven and no usable unique key exists.

`LegacyTable` remains the internal Mapping PostgreSQL Normalized Table destination. `DatabaseTable` is a separate destination because external database writes cannot share the Mapping Database transaction.

## 2. Definitions

- **DatabaseTable Destination**: An immutable Destination Definition that selects one server-owned database profile and one existing target table.
- **Database Destination Profile**: Server-owned provider, connection-string reference, schema allowlist, timeout, cache and write-gate configuration.
- **Target Table Schema**: Current canonical catalog projection of a target table, including columns, writeability, type constraints and unique constraints.
- **Target Schema Snapshot**: Immutable canonical Target Table Schema stored with a Config Version.
- **Schema Hash**: SHA-256 of the canonical structural snapshot. It excludes inspection time and non-contract formatting.
- **Mapped Field**: Writable target column referenced by an output assignment.
- **Required Target Field**: Writable `NOT NULL` target column without a database default.
- **Compatible Change**: Target change that does not invalidate existing inserts, such as adding a nullable/defaulted/generated column.
- **Breaking Change**: Target change that invalidates a mapped field, required-field coverage or selected unique key.
- **Existing Unique Key**: A target unique constraint selected from inspected catalog metadata. It identifies one business row before insert and excludes generated identity values unavailable to the sender.
- **AlreadyDelivered**: A row with the selected unique key exists and all canonical mapped values equal the persisted delivery values.
- **DeliveryConflict**: A row with the selected unique key exists but one or more canonical mapped values differ.
- **UnknownOutcome**: The system cannot prove whether the target committed an insert. Automatic retry is prohibited without reconciliation.
- **Database Delivery Job**: Persisted external-delivery intent for one mapped output, pinned to exact Config Version, target snapshot, values and duplicate policy.
- **Adapter**: PostgreSQL or SQL Server implementation behind the Database Destination Module seam.

## 3. Requirements, Constraints & Guidelines

### Destination and configuration

- **DST-001**: `DatabaseTable` shall be a new Destination Definition and shall not change `LegacyTable`, `ApiContract` or `JsonFile` wire contracts.
- **DST-002**: A Config Version shall select exactly one destination and shall not dual-deliver the same mapped output.
- **DST-003**: v1 shall support parameterized `INSERT` only.
- **DST-004**: A Config Version shall pin profile ID, schema, table, write mode, duplicate policy, Target Schema Snapshot and Schema Hash.
- **DST-005**: Active and historical Config Versions shall remain immutable. A newly discovered field requires a new Config Version before it can be mapped.

### Profiles and secrets

- **PRF-001**: Profiles shall be loaded from the `DestinationDatabases:Profiles` server configuration section.
- **PRF-002**: Each profile shall have an immutable lowercase logical ID, provider, connection-string name, non-empty allowed-schema list, command timeout from 1–300 seconds, schema-cache duration from 0–3,600 seconds and write gate.
- **PRF-003**: Supported providers shall be exactly `PostgreSql` and `SqlServer` in v1.
- **PRF-004**: Duplicate profile IDs, missing connection strings, invalid providers, empty schema allowlists and invalid limits shall fail startup.
- **PRF-005**: Connection values shall never be stored in Mapping Config, returned by an API or written to logs, metrics, diagnostics or audit.
- **PRF-006**: Development may use appsettings/User Secrets. Production shall use an approved secret provider or environment-backed configuration.
- **PRF-007**: `WriteEnabled=false` shall prevent new external writes while allowing profile/schema inspection.
- **PRF-008**: Target credentials shall use least privilege: catalog read, required business-row read for reconciliation and `INSERT` only on allowlisted targets. DDL privilege is prohibited.

### Schema inspection

- **SCH-001**: PostgreSQL inspection shall use `information_schema` plus `pg_catalog` where portable metadata is insufficient.
- **SCH-002**: SQL Server inspection shall use `INFORMATION_SCHEMA` plus `sys.*` where portable metadata is insufficient.
- **SCH-003**: Adapters shall project provider metadata into one canonical Target Table Schema.
- **SCH-004**: The schema model shall include provider type, canonical scalar type, ordinal, nullability, default presence, identity, generated/computed state, writeability, length, precision, scale and unique constraints.
- **SCH-005**: Identity, generated, computed and SQL Server `rowversion` columns shall not be assignment targets.
- **SCH-006**: Every Required Target Field shall be assigned in every emitted output branch before activation.
- **SCH-007**: An unmapped column with a database default shall be omitted from the `INSERT`, not supplied as explicit `NULL`.
- **SCH-008**: Profile/schema/table identifiers shall be resolved through the allowlisted inspected model and provider-quoted. Browser strings shall never be interpolated directly into SQL.
- **SCH-009**: Inspection shall occur during browsing, validation/activation, before delivery and bounded scheduled monitoring. Per-field catalog queries are prohibited.

### Schema compatibility

- **CMP-001**: No structural difference shall produce `Healthy`.
- **CMP-002**: Adding a nullable, defaulted, identity, generated or computed column shall produce `CompatibleChange` and shall not block the pinned Config Version.
- **CMP-003**: Adding a writable `NOT NULL` column without a default shall produce `BreakingChange`.
- **CMP-004**: Removing, renaming or changing type, length, precision, scale, nullability or writeability of a mapped column shall produce `BreakingChange`.
- **CMP-005**: Removing an unmapped non-dependent column may produce `CompatibleChange`.
- **CMP-006**: Changing an unmapped column into a required no-default column shall produce `BreakingChange`.
- **CMP-007**: Removing or changing the selected Existing Unique Key shall produce `BreakingChange` for reconciliation-enabled delivery.
- **CMP-008**: Missing table/profile, catalog permission failure, timeout or outage shall produce `Unavailable` with safe diagnostics.
- **CMP-009**: Compatible changes shall notify the user. Breaking changes shall block delivery and shall never auto-edit a Config Version or target schema.

### External delivery and consistency

- **DEL-001**: Row normalization shall persist a Database Delivery Job, encrypted deterministic mapped values and outbox event in the Mapping Database transaction. It shall not open a target connection.
- **DEL-002**: A separate Database Delivery Worker shall claim jobs by lease and perform target I/O.
- **DEL-003**: Retry shall use persisted values, payload hash, target identity, duplicate policy and Target Schema Snapshot. It shall not rebuild from the active config.
- **DEL-004**: Target values shall use provider parameters. Raw value interpolation is prohibited.
- **DEL-005**: Target `INSERT` shall execute in a target-local transaction.
- **DEL-006**: Provider-specific errors shall map to typed results without exposing raw SQL, connection information, stack traces or business values.
- **DEL-007**: Confirmed pre-execution/transient failures may be retried within policy. Ambiguous commit results require reconciliation or `UnknownOutcome`.
- **DEL-008**: Disabling a profile shall pause its new writes and shall not reroute pinned jobs to another destination.

### Duplicate reconciliation

- **DUP-001**: The UI shall offer only unique constraints returned by the Inspector as Existing Unique Key choices.
- **DUP-002**: Every selected key value shall be known deterministically before insert. Database-generated identity values are ineligible.
- **DUP-003**: A database unique constraint, not a select-before-insert check, shall be the concurrency guard.
- **DUP-004**: After a unique violation or ambiguous result, the Adapter may query by the full selected key and compare canonical mapped values.
- **DUP-005**: Existing key plus equal mapped values shall produce `AlreadyDelivered`.
- **DUP-006**: Existing key plus different mapped values shall produce `DeliveryConflict`; automatic overwrite is prohibited.
- **DUP-007**: Missing row after a confirmed rollback or safe pre-execution failure may be retried.
- **DUP-008**: Without a usable unique key, an ambiguous network/timeout/lease-expiry result shall produce `UnknownOutcome` and shall not be retried automatically.
- **DUP-009**: `UnknownOutcome` manual actions shall require authorization, operator, reason, correlation ID and append-only audit.
- **DUP-010**: Manual retry without a unique key shall display and record duplicate risk.

### Security and operations

- **SEC-001**: Browser contracts shall contain logical IDs and safe schema summaries only.
- **SEC-002**: SQL text, target host, connection string, credentials and payload values shall not appear in normal logs or ProblemDetails.
- **SEC-003**: Metrics labels shall be bounded provider/status/result values and shall not contain table, column, credential or payload values.
- **SEC-004**: SQL Server production transport shall use owner-approved encryption and certificate validation.
- **SEC-005**: Profile write gates, global DatabaseTable creation/activation gate and Worker pause control shall be independently operable.
- **SEC-006**: Production rollout shall progress through inspect-only, sandbox, canary and expanded stages.
- **OPS-001**: Operations shall provide runbooks for drift, outage, conflict, unknown outcome, credential rotation, queue recovery and rollback.
- **OPS-002**: Gate I requires Mapping, Platform/Operations, Security and Target Database owner approval before production write enablement.

### Constraints

- **CON-001**: No target business column, receipt table or target DDL is required or created by Phase 29.
- **CON-002**: Exactly-once delivery cannot be guaranteed when no usable target unique key or target-side idempotency record exists.
- **CON-003**: Mapping Database state alone cannot prove an external commit after process/network failure.
- **CON-004**: PostgreSQL and SQL Server catalog/write integration tests are mandatory before each provider is enabled.

## 4. Interfaces & Data Contracts

### Module seam

```csharp
public interface IDatabaseDestinationModule
{
    Task<TargetTableSchema> InspectAsync(
        TargetTableReference target,
        CancellationToken cancellationToken = default);

    SchemaCompatibilityResult Compare(
        TargetSchemaSnapshot configured,
        TargetTableSchema current,
        IReadOnlySet<string> mappedColumns);

    Task<DatabaseDeliveryResult> InsertAsync(
        DatabaseInsertRequest request,
        CancellationToken cancellationToken = default);
}
```

The Interface is the caller and test surface. Provider catalog queries, quoting, parameters and error codes are implementation details behind PostgreSQL and SQL Server Adapters.

### Profile model

```csharp
public sealed record DatabaseDestinationProfile(
    string Id,
    DatabaseProvider Provider,
    string ConnectionStringName,
    IReadOnlyList<string> AllowedSchemas,
    int CommandTimeoutSeconds,
    int SchemaCacheSeconds,
    bool WriteEnabled,
    string ConfigurationHash);
```

### Destination definition

```json
{
  "kind": "databaseTable",
  "profileId": "legacy-sqlserver",
  "schema": "dbo",
  "table": "customer",
  "writeMode": "insert",
  "duplicatePolicy": {
    "kind": "existingUniqueKey",
    "columns": ["customer_code"]
  },
  "schemaHash": "sha256:..."
}
```

`duplicatePolicy.kind` is either `existingUniqueKey` or `manualUnknownOutcome`.

### Delivery states

```text
Pending
Delivering
Delivered
AlreadyDelivered
RetryableFailed
PermanentFailed
DeliveryConflict
BlockedSchemaDrift
UnknownOutcome
```

### Safe profile projection

Safe projections may contain profile ID, provider, enabled/write-enabled state and allowed-schema summaries. They shall not contain `ConnectionStringName` or its resolved value.

## 5. Acceptance Criteria

- **AC-DST-01**: Given existing LegacyTable, ApiContract and JsonFile definitions, when Phase 29 code is deployed, then their JSON and runtime effects remain unchanged.
- **AC-PRF-01**: Given duplicate profile IDs, when API or Worker starts, then startup fails before accepting work.
- **AC-PRF-02**: Given a missing named connection string, when configuration is validated, then startup fails without logging another connection value.
- **AC-PRF-03**: Given PostgreSQL and SQL Server profiles, when resolved by ID, then the factory creates the matching provider connection without exposing the value through a profile projection.
- **AC-SCH-01**: Given a new table in an allowed schema, when the user refreshes table discovery, then it appears without a code change.
- **AC-SCH-02**: Given a table outside allowed schemas, when requested by a browser, then inspection is rejected before catalog-derived SQL execution.
- **AC-CMP-01**: Given a new nullable/defaulted/generated column, when compared with a pinned snapshot, then the result is CompatibleChange and existing delivery may continue.
- **AC-CMP-02**: Given a new no-default required column or mapped-column drift, when compared, then the result is BreakingChange and insert is blocked.
- **AC-CMP-03**: Given a field added after activation, when a user wants to map it, then the user creates a new Config Version and the historical version remains unchanged.
- **AC-DEL-01**: Given a valid mapped output, when normalization commits, then one Database Delivery Job and outbox event exist before target network I/O.
- **AC-DEL-02**: Given a target defaulted column with no assignment, when SQL is built, then that column is omitted from the `INSERT`.
- **AC-DUP-01**: Given a selected unique key and an existing row with equal mapped values, when reconciliation runs, then the result is AlreadyDelivered.
- **AC-DUP-02**: Given the same key with different mapped values, when reconciliation runs, then the result is DeliveryConflict and no update occurs.
- **AC-DUP-03**: Given no selected key and an ambiguous insert result, when recovery runs, then the job is UnknownOutcome and is not retried automatically.
- **AC-SEC-01**: Given any list/detail/error/log path, when inspected, then it contains no connection value, raw SQL or business payload.
- **AC-OPS-01**: Given `WriteEnabled=false`, when delivery is due, then no target write occurs and the pinned destination is not changed.

## 6. Test Automation Strategy

- **Unit**: profile/options validation, deterministic configuration/schema hashes, compatibility matrix, identifier rules, type canonicalization, error classification and state transitions.
- **Contract**: existing destination JSON golden fixtures, DatabaseTable request/response strict JSON and safe profile projection.
- **PostgreSQL integration**: catalog discovery, type/default/identity/unique metadata, parameterized insert, drift, unique reconciliation, timeout and transaction behavior.
- **SQL Server integration**: equivalent provider behavior using an explicit test instance; tests shall not silently skip when provider integration is required.
- **Mapping PostgreSQL integration**: migration idempotence, job/outbox atomicity, lease recovery, encrypted payload and append-only audit.
- **End-to-end**: profile → schema/table selection → mapping → activation → import → delivery → status, plus compatible/breaking drift and UnknownOutcome resolution.
- **Regression**: all existing LegacyTable, ApiContract, JsonFile, retry, reprocess and immutable-version suites.
- **Security**: identifier injection, secret/log redaction, allowlist bypass, authorization, unsafe retry and TLS/profile misconfiguration.

Tests shall use public Module/profile/config interfaces. Provider databases are system boundaries and use real integration fixtures rather than mocks.

## 7. Rationale & Context

External databases vary in catalog vocabulary, identifier quoting, parameters and errors. A deep Database Destination Module keeps these details behind one small Interface and uses two real Adapters. Callers learn target identity and typed results, not provider dialect.

Schema is inspected dynamically so new tables and columns do not require code-generated entities. Immutable snapshots preserve deterministic Config Versions. Compatibility comparison permits safe additive database evolution while blocking changes that invalidate the SQL contract.

External target commits cannot be atomic with Mapping PostgreSQL commits. Persisted delivery jobs and outbox events prevent intent loss. Existing unique constraints permit effective reconciliation. Where no unique identity exists, `UnknownOutcome` makes uncertainty explicit instead of creating silent duplicates through automatic retry.

## 8. Dependencies & External Integrations

### External Systems

- **EXT-001**: PostgreSQL target databases with catalog read and allowlisted table read/insert permissions.
- **EXT-002**: SQL Server target databases with catalog read and allowlisted table read/insert permissions.

### Infrastructure Dependencies

- **INF-001**: Mapping PostgreSQL stores Config Versions, delivery jobs, attempts, audit and outbox.
- **INF-002**: Kafka transports Database Delivery Job identifiers.
- **INF-003**: Approved secret configuration supplies named target connection strings.

### Technology Platform Dependencies

- **PLT-001**: .NET 8, Npgsql, Dapper and Microsoft SQL Server client support.
- **PLT-002**: Existing Transformation and Destination Modules provide typed mapped outputs and destination routing.

### Compliance Dependencies

- **COM-001**: Target data classification controls whether reconciliation may read mapped values.
- **COM-002**: Retention and encryption policy applies to persisted Database Delivery payloads and attempt metadata.

## 9. Examples & Edge Cases

### Compatible addition

```text
Configured: customer_code NOT NULL, customer_name NULL
Current:    customer_code NOT NULL, customer_name NULL, note NULL
Result:     CompatibleChange; omit note; notify user
```

### Breaking required addition

```text
Configured: customer_code NOT NULL
Current:    customer_code NOT NULL, branch_code NOT NULL without default
Result:     BreakingChange; block delivery
```

### Existing-key reconciliation

```text
Unique key: (policy_number, effective_date)
Existing values equal persisted mapped values  -> AlreadyDelivered
Existing values differ                         -> DeliveryConflict
No row after confirmed rollback                -> retry allowed
```

### No key and timeout

```text
INSERT sent
Connection reset before commit acknowledgement
No usable unique key
Result: UnknownOutcome; no automatic retry
```

## 10. Validation Criteria

- Specification requirements trace to Phase 29 steps and acceptance tests.
- Existing destination golden/regression tests pass before and after profile foundation changes.
- API and Worker fail closed for invalid profiles or missing connection strings.
- Provider connections are selected by logical profile ID and secrets are not exposed in public profile objects.
- No schema inspection, external insert or migration for delivery jobs is implemented before its assigned Phase 29 step.
- Gate I evidence includes forced PostgreSQL and SQL Server integration results, web E2E, security review, runbook rehearsal and owner approval.

## 11. Related Specifications / Further Reading

- [Approved Multi-table Database Delivery Design Addendum](spec-design-multi-table-database-delivery.md)
- [Phase 29 implementation plan](../docs/web-config-plan.md#phase-29--external-database-table-destination-สำหรับ-postgresql-และ-sql-server)
- [Current Mapping Demo workflow](../docs/current-mapping-demo-workflow.md)
- [Contract API and Legacy Table Delivery Design](spec-design-contract-api-legacy-delivery.md)
- [Table Administration and Schema Verification Design](spec-design-table-administration-schema-verification.md)
