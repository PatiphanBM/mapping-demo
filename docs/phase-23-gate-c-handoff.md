# Phase 23 Gate C Handoff

## Result

Engineering verification and contract-owner acknowledgement passed on
2026-10-20. Runtime API delivery remains out of scope and is not wired: Phase
23 resolves schemas only while editing/creating a version and previews payloads
without scheduling a job or calling a partner endpoint.

## Dependency map

```text
API Destination Profile Catalog
  -> Contract Registry Module -> canonical schema/hash/path catalog
  -> immutable Config Version snapshot
      -> Target Schema Module
          -> Transformation v2 -> Document Assembler
              -> Payload Binding Module -> Delivery Payload preview

LegacyTable -> LegacyTableTargetSchemaAdapter -> Transformation v1 (unchanged)
```

The delivery base URL, authentication reference, and secret value never enter
Mapping Config JSON or safe browser metadata. Browser input contains logical
profile and contract IDs only; Create Version resolves and hashes the schema
again on the server.

## Feature flags and rollback

- Server creation gate: `Features:ApiContractDestinationCreation`.
- Browser visibility gate:
  `NEXT_PUBLIC_API_CONTRACT_DESTINATION_VISIBLE=false` hides the API option.
- Development enables the server gate and ships two bundled illustrative
  schemas. Production defaults to disabled with an empty profile catalog.
- Rollback: disable both gates and activate the previous immutable
  `LegacyTable` version for new files. Existing API versions keep their pinned
  destination/snapshot and are never rewritten or rerouted.
- Phase 24 must introduce delivery/outbox kill switches separately; Phase 23
  has no outbound delivery side effect to pause.

## Requirement trace

| Requirement | Automated evidence |
|---|---|
| R1 | API Config contracts reject a Legacy Normalized Table field; Phase 23 has no Delivery Job, API delivery adapter, or Normalized Table write path for `ApiContract` |
| R2 | Destination union/compatibility tests plus both direct and adapter-path Legacy SQL characterization tests |
| R3 | `ContractSchemaModuleTests`, server-authoritative path catalog, UI path browser, and two-profile E2E |
| R4 | `ApiContractSnapshotIntegrationTests` persists authoritative snapshot/hash and proves later schema drift does not change the pinned version |
| R26–R28 | `TransformationV2Tests` covers nested object, fixed index, type/path validation, container rejection, and array gap diagnostics |
| R29–R31 | `TransformationV2Tests` covers Array Group empty-item handling, compaction, retained `false`, and source-slot `seq` |
| PAY-001–008 | `PayloadBindingModuleTests` covers two path vocabularies, mapped/system conflict, final constraints, deterministic JSON/hash; UI E2E covers read-only system metadata and final payload |

## Verification on 2026-10-20

- Full .NET suite with `MAPPING_POSTGRES_INTEGRATION` set to the local test
  database: 290 passed, 0 failed. PostgreSQL tests executed rather than taking
  their no-connection early-return path.
- Contract snapshot transaction and drift pinning integration passed against
  PostgreSQL.
- Legacy direct path and Destination Module adapter path produced the same
  golden SQL effects; atomic `2→1→0` reconciliation/rollback passed.
- `.txt/.csv/.xlsx/.xls`, migrations, watcher/intake, lookup, and row-output
  regression suites passed in the same full run.
- `dotnet format MappingDemo.sln --no-restore --verify-no-changes`: passed.
- `npm run lint`: passed.
- `npm run build`: passed, including TypeScript and all Next.js 16.4 routes.
- `npm run test:e2e`: 4 passed, 0 failed. PA MTLS and Claims Envelope previews
  matched their golden JSON; five preview tabs and version compare passed; no
  send button, partner request, Delivery Job, or outbox request occurred.
- Development API smoke returned only safe metadata for `pa-mtls` and
  `claims-envelope`. Resolved catalogs contained 21 and 15 typed paths with
  different authoritative SHA-256 hashes and envelope vocabularies.
- API log scan found no configured secret reference, internal schema/delivery
  host, or preview sample PII. Phase 23 contract/assembler/binding modules have
  no logging surface for payload bodies.
- PostgreSQL container may be stopped after review; named volume is preserved.

## Contract-owner review

Supported in v1:

- Closed root/nested objects, homogeneous arrays, scalar
  string/integer/number/boolean, date/time/date-time/uuid formats.
- `required`, nullable `[type, "null"]`, descriptions, supported constraints,
  local non-recursive JSON Pointer `$ref`, fixed indices, and Array Groups.

Explicitly unsupported and rejected before Config Version persistence:

- External `$ref` → `ExternalReferenceNotSupported`.
- Recursive local `$ref` → `RecursiveReferenceNotSupported`.
- `oneOf`, `anyOf`, `allOf` → `AmbiguousUnionNotSupported`.
- Unconstrained `additionalProperties: true` →
  `UnconstrainedAdditionalPropertiesNotSupported`.
- Multiple non-null types, tuple arrays, conditionals, dynamic references,
  unevaluated properties/items, and schema-generated delivery headers.

The contract owner confirmed on 2026-10-20 that the intended `pa-proposal/1`
and `claims/2` contracts fit this subset and that an unsupported feature will
require a new design/version rather than bypassing validation.

## Sign-off status

- Engineering gate: **Passed** on 2026-10-20.
- Contract-owner subset acknowledgement: **Confirmed** on 2026-10-20.
- Gate C / checklist 23.14: **Passed** on 2026-10-20.

Phase 23 Gate C is complete. Phase 24 may begin only when separately requested;
its required side-effect decisions remain governed by step 24.0.

