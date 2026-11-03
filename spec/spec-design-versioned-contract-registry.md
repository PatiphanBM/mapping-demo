---
title: Versioned Contract Registry Design
version: 1.0
date_created: 2026-11-02
last_updated: 2026-11-03
owner: Mapping Demo Contract, Platform and Security owners
tags: [design, contract-registry, json-schema, postgresql, phase-27]
---

# Introduction

This specification defines the Phase 27 versioned Contract Registry. It replaces bundled JSON Schema fixtures as the eventual production authority while preserving immutable Contract Snapshots already stored in Mapping Config Versions.

## 1. Purpose & Scope

The registry lets authorized operators create, validate, clone, publish, retire and resolve versioned JSON Schema contracts without editing application configuration or restarting runtime processes. Phase 27 implements the PostgreSQL catalog, shared Module/adapters, Admin HTTP/UI, seed/cutover and Gate G technical controls.

In scope:

- stable Contract identity and immutable Version identity;
- lifecycle `Draft → Published → Retired`;
- server-derived canonical schema, SHA-256 hash, typed path catalog and compatibility result;
- exact resolution for Published and Retired historical versions;
- profile allowlist linkage;
- fail-closed caching and source selection;
- optimistic concurrency and append-only audit.

Out of scope:

- production Admin publishing;
- automatic production enablement after Gate approval;
- remote/external JSON Schema references;
- hard delete, in-place edit of Published content, or automatic profile allowlist changes.

## 2. Definitions

- **Contract**: Stable logical identity, identified by immutable `contractKey`.
- **Contract Version**: Immutable identity `(contractKey, version)` with derived `reference` equal to `contractKey/version`.
- **Draft**: Mutable schema candidate. At most one Draft exists per Contract.
- **Published**: Immutable, canonicalized Version selectable for new work.
- **Retired**: Immutable historical Published Version excluded from new selection but resolvable by exact reference.
- **Contract Snapshot**: Canonical schema, hash and derived paths persisted inside a Mapping Config Version.
- **Compatibility**: Server-derived comparison with a selected Published base Version.
- **Breaking**: A change that can invalidate a payload valid under the base Version.
- **Module**: The deep Contract Registry behavior exposed through one interface.
- **Adapter**: Bundled or PostgreSQL implementation selected at the Module seam.
- **Gate G**: Final production publishing gate in Step 27.6. Design approval is not Gate G production approval.

## 3. Requirements, Constraints & Guidelines

### Identity and lifecycle

- **REG-001**: `contractKey` shall match `^[a-z][a-z0-9-]{1,62}$` and never change.
- **REG-002**: `version` shall match `^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$` and never change.
- **REG-003**: `reference` shall be server-derived as `contractKey/version`; clients cannot supply a different value.
- **REG-004**: `(contractKey, version)` and `reference` shall be globally unique.
- **REG-005**: A Contract shall have at most one Draft. Concurrent creation has one winner.
- **REG-006**: Allowed lifecycle transitions are `Draft → Published → Retired`. No reverse transition and no hard delete are allowed.
- **REG-007**: Published and Retired schema content, canonical schema, hash and identity are immutable.
- **REG-008**: Changing a Published or Retired schema requires clone to a new version label.

### Schema validation and compatibility

- **SCH-001**: Draft schema shall be UTF-8 JSON and at most 1 MiB.
- **SCH-002**: Validation and Publish shall reuse `ContractSchemaModule` for the supported JSON Schema subset, local reference resolution, canonicalization, hash and path derivation.
- **SCH-003**: The registry shall reject malformed JSON, remote or recursive references, ambiguous unions, executable content and unsupported schema features with safe diagnostics.
- **SCH-004**: Canonical schema and `sha256:<lowercase-hex>` are server outputs; client-supplied hashes are never authoritative.
- **SCH-005**: Validate shall have no persistence or lifecycle side effect.
- **SCH-006**: Publish shall compile again inside the operation and persist exactly the resulting canonical schema and hash.
- **CMP-001**: Compatibility shall compare against an explicitly selected Published/Retired base or the latest Published Version when present.
- **CMP-002**: Removed assignable paths, newly required paths, scalar type changes, nullable-to-non-nullable changes and stricter supported constraints are Breaking.
- **CMP-003**: New optional paths and equal or wider supported constraints are NonBreaking.
- **CMP-004**: A Breaking new version may publish only with explicit breaking-change acknowledgement and an audit reason. This does not mutate existing profiles or snapshots.
- **CMP-005**: First publication has compatibility `Initial` and requires no breaking acknowledgement.

### Authorization, audit and security

- **AUT-001**: Later Admin endpoints shall separate read, write and publish/retire permissions.
- **AUT-002**: Every mutation shall contain an authenticated actor, unique request ID and a 10–500 character reason.
- **AUT-003**: Draft update, Publish and Retire shall use optimistic concurrency.
- **AUD-001**: Every successful mutation shall append exactly one audit row in the same transaction.
- **AUD-002**: Audit is append-only and records identity, action, actor, reason, request ID, lifecycle/hash before and after, and resulting concurrency token.
- **SEC-001**: Schema, descriptions and examples shall not contain secrets or real customer data. Step 27.3 adds request content policy enforcement.
- **SEC-002**: Responses and diagnostics shall never expose filesystem paths, database details, stack traces or application configuration.
- **SEC-003**: No raw SQL, executable schema extension, remote URL fetch or dynamic code is allowed.

### Profile linkage, snapshots and runtime

- **LNK-001**: `api_destination_profile_contracts` shall support a nullable FK to `contract_versions` during expansion.
- **LNK-002**: When the FK is present, contract ID/version/reference values shall match the referenced registry Version.
- **LNK-003**: Existing profile rows remain unchanged through Step 27.1. Backfill and cutover occur in Step 27.5.
- **PIN-001**: Existing Mapping Config Version Contract Snapshots remain authoritative for runtime processing and are never rewritten.
- **PIN-002**: Retire blocks new selection but exact Retired resolution remains available for historical verification.
- **PIN-003**: Publish or Retire shall not automatically alter profile allowlists.

### Module, adapter and cache

- **MOD-001**: Callers shall use one registry interface for list, get/history, exact resolve, validate and typed mutation commands.
- **MOD-002**: The interface shall hide SQL, fixtures, canonicalization, compatibility, transactions and caching.
- **MOD-003**: Bundled and PostgreSQL adapters shall return equivalent canonical schema/hash/path results for the same schema.
- **MOD-004**: The Bundled adapter is read-only and returns `RegistryReadOnly` for mutation.
- **MOD-005**: Published/Retired exact results may cache for at most five minutes; the Published list may cache for at most fifteen seconds.
- **MOD-006**: Successful mutation invalidates affected exact and list cache entries locally.
- **MOD-007**: On cache miss or expiry, source outage shall return `ContractRegistryUnavailable`; it shall never fall back to another source.
- **MOD-008**: API and Worker instances configured for PostgreSQL shall observe the same Published identity and hash.

### Constraints

- **CON-001**: Postgres mode is authoritative after Step 27.5; Bundled mode is explicit bootstrap/local-test/emergency rollback only and never an automatic outage fallback.
- **CON-002**: No new third-party package is required.
- **CON-003**: Production publishing remains disabled until Gate G approval.
- **GUD-001**: Contract keys are business identities; version labels are opaque identifiers, not values to sort semantically.

## 4. Interfaces & Data Contracts

### 4.1 Module interface

```csharp
public interface IVersionedContractRegistry
{
    ValueTask<IReadOnlyList<ContractVersionSummary>> ListPublishedAsync(
        CancellationToken cancellationToken = default);

    ValueTask<ContractAggregateResult> GetAsync(
        string contractKey,
        CancellationToken cancellationToken = default);

    ValueTask<ContractVersionResult> ResolveExactAsync(
        string reference,
        CancellationToken cancellationToken = default);

    ValueTask<ContractValidationResult> ValidateAsync(
        long contractVersionId,
        CancellationToken cancellationToken = default);

    ValueTask<ContractMutationResult> ExecuteAsync(
        ContractRegistryCommand command,
        CancellationToken cancellationToken = default);
}
```

`ContractRegistryCommand` is a closed typed set: `CreateDraft`, `UpdateDraft`, `CloneVersion`, `PublishVersion`, and `RetireVersion`. Mutation results return safe diagnostics and a new concurrency token; they do not throw for domain conflicts.

### 4.2 Persistence model

```text
contracts
├─ id, contract_key (immutable), display_name
├─ concurrency_token
└─ created/updated timestamps and actors

contract_versions
├─ id, contract_id, version, reference (immutable)
├─ lifecycle: Draft | Published | Retired
├─ draft_schema
├─ canonical_schema, schema_hash
├─ concurrency_token
└─ created/published/retired metadata

contract_audit
├─ contract_id, contract_version_id
├─ action, actor, reason, request_id
├─ before/after lifecycle and hash
└─ concurrency_token, occurred_at
```

### 4.3 Safe result rules

- Published list excludes Draft and Retired.
- Aggregate history includes schema only for authorized internal/module callers through Step 27.2; Step 27.3 defines safe HTTP projection.
- Exact resolve returns Published or Retired only and rejects Draft.
- Diagnostics use stable codes and schema paths.

## 5. Acceptance Criteria

- **AC-REG-ID-01**: Given duplicate key, version or reference, when committed concurrently, then one mutation wins and the other conflicts.
- **AC-REG-LCY-01**: Given a Draft, when Published, then canonical schema/hash are persisted atomically and content becomes immutable.
- **AC-REG-LCY-02**: Given Published, when Retired, then new list excludes it while exact resolution returns unchanged canonical content/hash.
- **AC-REG-LCY-03**: Given Published or Retired content update/delete, when committed, then PostgreSQL rejects it.
- **AC-REG-VAL-01**: Given invalid or oversized schema, when Validate/Publish runs, then safe diagnostics return and lifecycle/audit do not change.
- **AC-REG-CMP-01**: Given Breaking change without acknowledgement, when Publish runs, then it is rejected without side effect.
- **AC-REG-CMP-02**: Given acknowledged Breaking or NonBreaking change, when Publish succeeds, then compatibility and audit are recorded without altering older Versions.
- **AC-REG-AUD-01**: Given any successful mutation, then one append-only audit record commits with actor/reason/request/hash/lifecycle/token.
- **AC-REG-LNK-01**: Given a profile contract FK, when ID/version/reference differs from the linked Version, then PostgreSQL rejects it.
- **AC-REG-PIN-01**: Given existing Mapping Config snapshots, when migration runs, then no snapshot row is rewritten.
- **AC-REG-MOD-01**: Given identical bundled and PostgreSQL schema, when resolved, then canonical schema/hash/path catalog are equal.
- **AC-REG-MOD-02**: Given a Retired Version, when exact historical resolution runs, then it succeeds; Published list excludes it.
- **AC-REG-MOD-03**: Given source outage and cache miss, when resolving, then `ContractRegistryUnavailable` returns with no source fallback.
- **AC-REG-CACHE-01**: Given successful Publish/Retire, when the same process lists/resolves, then stale local cache is invalidated.

## 6. Test Automation Strategy

- **Unit**: identity validation, canonical compile reuse, compatibility classification, cache keys and fail-closed behavior.
- **PostgreSQL integration**: constraints, concurrent Draft, lifecycle, immutability, optimistic concurrency, atomic audit, profile linkage and migration non-rewrite.
- **Module contract**: identical list/resolve/validate behavior through Bundled and PostgreSQL adapters.
- **Outage**: expired/missing cache returns unavailable without bundled fallback.
- **Regression**: all existing LegacyTable, ApiContract and JsonFile tests remain green.
- **Frameworks**: existing xUnit, Dapper and Npgsql fixtures; no new dependency.
- **Performance**: exact cached resolution p95 and bounded Published list are approved in Gate G evidence.

## 7. Rationale & Context

Mapping Config creation needs a deterministic schema snapshot, but bundled fixtures require code deployment and create split ownership. Stable version identity plus immutable publication lets authoring change independently while already-pinned Config Versions retain exact bytes and semantics. Retire is selection policy, not deletion.

The Module is deep: its small interface hides lifecycle, compilation, compatibility, persistence, cache and fail-closed behavior. Bundled and PostgreSQL adapters make the seam real and keep cutover reversible until DB-only use.

## 8. Dependencies & External Integrations

### External Systems

- **EXT-001**: PostgreSQL stores authoritative registry state after Step 27.5.
- **EXT-002**: Identity provider supplies later Admin authorization claims.

### Infrastructure Dependencies

- **INF-001**: API and Worker share the same PostgreSQL catalog.
- **INF-002**: Existing migration advisory lock serializes schema changes.

### Data Dependencies

- **DAT-001**: Bundled fixtures are migration inputs in Step 27.5, not authority after cutover.
- **DAT-002**: Mapping Config Versions retain immutable Contract Snapshots.
- **DAT-003**: API Destination Profile allowlists retain string identity during nullable-FK expansion.

### Technology Platform Dependencies

- **PLT-001**: .NET 8, existing JSON compiler, Npgsql and Dapper.

### Compliance Dependencies

- **COM-001**: Schema content must use synthetic examples and contain no credential or customer payload.

## 9. Examples & Edge Cases

```text
proposal/1 Published
clone → proposal/2 Draft
validate proposal/2 → Breaking: proposal.insured.fullName removed
publish without acknowledgement → rejected
publish with acknowledgement + reason → proposal/2 Published
retire proposal/1 → excluded from new selection; exact historical resolve still succeeds
```

Edge cases:

- Two Draft creates for one Contract race: one commits.
- Same version label with different case conflicts because identity comparison is ordinal/case-sensitive at the Module and database C collation semantics are not assumed; exact duplicates are always rejected.
- Draft changes after validation: Publish recompiles current schema; no stale validation artifact is trusted.
- Cache contains proposal/1 while proposal/2 publishes: proposal/1 remains valid immutable cache; Published list is invalidated.
- PostgreSQL unavailable with uncached reference: fail closed; never read bundled fixture.
- Retired Version referenced by a historical snapshot: exact resolve succeeds, new profile attachment does not list it.

## 10. Validation Criteria

Phase 27 technical implementation is complete when:

- this specification has no unresolved identity, lifecycle, compatibility, cache or rollback default;
- migration tests prove constraints, immutability, audit, nullable profile linkage and non-rewrite;
- one Module contract passes for Bundled and PostgreSQL adapters;
- mutation/cache/outage behavior passes through the Module interface;
- existing full backend regression remains green;
- Admin API/UI, idempotent parity-checked cutover, observability, operations evidence and full regression pass;
- production Admin publishing remains disabled pending explicit Gate G owner sign-off and rollout configuration.

### 10.1 Design approval record

The user instruction to implement through Step 27.2 on 2026-11-02 authorizes this design for implementation. It is not final Gate G production approval.

| Owner role | Scope | Approver | Status | Date |
|---|---|---|---|---|
| Contract / Business | identity, version labels, lifecycle and compatibility | Project owner (user instruction) | Approved for implementation | 2026-11-02 |
| Platform / Operations | PostgreSQL model, cache, cutover and rollback | Project owner (user instruction) | Approved for implementation | 2026-11-02 |
| Security | schema content policy, authorization model, audit and redaction | Project owner (user instruction) | Approved for implementation | 2026-11-02 |

## 11. Related Specifications / Further Reading

- [Contract API and Legacy Table Delivery Design](spec-design-contract-api-legacy-delivery.md)
- [API Destination Profile Administration Design](spec-design-api-destination-profile-administration.md)
- [Implementation Plan](../docs/web-config-plan.md)
- [Current Mapping Demo Workflow](../docs/current-mapping-demo-workflow.md)
- [Domain Context](../CONTEXT.md)
