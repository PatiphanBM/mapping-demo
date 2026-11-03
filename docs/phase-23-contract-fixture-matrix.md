# Phase 23 Contract Fixture Matrix

## Profiles

| Profile | Fixture | Envelope | Business root | Repeating item | Delivery policy |
|---|---|---|---|---|---|
| `pa-mtls` | `src/MappingDemo.Api/ContractFixtures/pa-proposal-v1.json` | `envelope.*` | `proposal.*` | `proposal.beneficiaries[]` | `POST /proposals/{contractVersion}` |
| `claims-envelope` | `src/MappingDemo.Api/ContractFixtures/claims-envelope-v2.json` | `meta.*` | `case.*` | `case.parties[]` | `PUT /cases/{contractVersion}` |

The second profile deliberately does not use `proposal`, `fileJobId`, or
`beneficiaries`. Runtime bindings are configuration owned; no PA path is coded
inside the Contract Schema, Transformation, Document Assembler, or Payload
Binding modules.

## Supported JSON Schema subset

- Root and nested `object`, closed with `additionalProperties: false`.
- `array` with one `items` schema.
- `string`, `integer`, `number`, `boolean`.
- String formats `date`, `time`, `date-time`, and `uuid`.
- `required`, nullable `[type, "null"]`, descriptions, `maxLength`, `pattern`,
  `minimum`, and `maximum`.
- Local JSON Pointer `$ref`, including escaped pointer tokens.
- Direct scalar paths, fixed non-negative array indices, and Array Group
  wildcard paths. Fixed indices must be contiguous from zero.

Explicitly unsupported with stable diagnostics:

- External `$ref` → `ExternalReferenceNotSupported`.
- Recursive local `$ref` → `RecursiveReferenceNotSupported`.
- `oneOf`, `anyOf`, and `allOf` → `AmbiguousUnionNotSupported`.
- Unconstrained `additionalProperties: true` →
  `UnconstrainedAdditionalPropertiesNotSupported`.
- Multiple non-null types, tuple arrays, conditionals, dynamic references,
  unevaluated properties/items, and schema-generated delivery headers.

Negative fixtures are `negative-external-ref.json`,
`negative-recursive-ref.json`, and `negative-ambiguous-union.json` in the same
fixture directory.

## Requirement and test trace

| Requirement | Fixture / automated evidence |
|---|---|
| R3, R4 | `ContractSchemaModuleTests`; both logical contract references resolve through `ContractRegistryModule.ResolveAsync` |
| R26–R28 | `ContractSchemaModuleTests`, `TransformationV2Tests`; nested paths, scalar types, nullable, local refs, fixed index and gap rejection |
| R29–R31 | `TransformationV2Tests`; `-`/blank skip, `false` retained, source slot injected as `seq`, compact array |
| CTR-001–007 | `ContractSchemaModuleTests`, `ApiContractSnapshotIntegrationTests`; canonical hash, drift, authoritative immutable snapshot |
| PATH-001–007 | `TransformationV2Tests`; unknown/container/conflicting paths and v1 parity suite |
| PAY-001–008 | `PayloadBindingModuleTests`; two path vocabularies, conflict, conversion, required/type/constraint validation, deterministic payload hash |
| CON preview | `MappingConfigsController` builds Delivery Payload with `FileJobId = 0`, `sendsRequest = false`, safe method/path/header names, and no delivery adapter |

