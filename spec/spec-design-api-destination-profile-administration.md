---
title: API Destination Profile Administration Design
version: 1.0
date_created: 2026-10-29
last_updated: 2026-10-29
owner: Mapping Demo Team
tags: [design, api-destination-profile, administration, versioning, security, operations]
status: Accepted for Phase 26 implementation
---

# Introduction

This specification defines the versioned, server-owned catalog and Admin control plane for API Destination Profiles. It replaces duplicated API/Worker application configuration as the production source of truth without changing persisted payload bytes, Contract Snapshots, idempotency keys, or the destination pinned by existing jobs.

The design is fail-closed. A browser can submit only typed profile data. It cannot submit credentials, arbitrary runtime URLs, raw headers, raw SQL, or payload data. Credentials remain in the secret provider and PostgreSQL stores only a logical secret reference.

This specification is the Phase 26 design addendum to [Contract API and Legacy Table Delivery Design](spec-design-contract-api-legacy-delivery.md). If the documents conflict for profile administration, profile lifecycle, profile version pinning, or profile catalog cutover, this specification is authoritative. The parent specification remains authoritative for Transformation, Contract Snapshot, payload construction, API delivery, retry, and idempotency semantics.

## 1. Purpose & Scope

### 1.1 Purpose

Phase 26 shall provide:

- a PostgreSQL catalog shared by API and Worker;
- immutable activated profile versions;
- a Draft → Active → Disabled lifecycle;
- Admin APIs and `/admin/api-destination-profiles`;
- typed validation for endpoints, authentication references, contracts, headers, and payload bindings;
- exact profile-version pinning for Mapping Config Versions and Delivery Jobs;
- audited activation, disable, and rollback;
- a controlled migration from configuration-backed profiles without split-brain resolution.

### 1.2 In scope

- `ApiDestinationProfile` only.
- Stable logical profile identity and monotonically increasing version identity.
- Draft creation and editing with optimistic concurrency.
- Validation, activation, disable, history, comparison, and rollback.
- PostgreSQL persistence, safe projections, authorization, and append-only audit.
- Profile resolution for Config Version creation, File Job intake, delivery, retry, reprocess, and resend.
- Seed/cutover of the current `pa-mtls`, `pa-mtls-inform`, and `claims-envelope` profiles.
- Cache consistency, mixed-deployment behavior, operational metrics, and rollback.

### 1.3 Out of scope

- `FileDestinationProfile` administration.
- Contract schema authoring or publishing; that belongs to Phase 27.
- Secret creation, editing, rotation, or display.
- Arbitrary custom authentication schemes, scripts, request bodies, header values, or URL templates.
- Hard deletion of a profile or version that has existed in the catalog.
- Changing a Mapping Config Version, Contract Snapshot, Delivery Job payload, or idempotency key in place.
- Automatic rerouting between `ApiContract`, `LegacyTable`, and `JsonFile`.

### 1.4 Confirmed identity decisions

| Decision | Required value |
|---|---|
| Logical profile identity | Immutable `profileKey`, using the existing logical ID such as `pa-mtls` |
| Rename policy | `profileKey` cannot be renamed; create a new profile and migrate through a new Mapping Config Version |
| Database identity | Internal `bigint` primary keys; clients use `profileKey` and `versionNo` |
| Version identity | Positive integer `versionNo`, monotonically allocated per profile and never reused |
| External version reference | `{profileKey}/{versionNo}`, for example `pa-mtls/2` |
| Active version policy | At most one Active version per profile |
| Draft policy | At most one mutable Draft version per profile |
| Activated content | Immutable; only lifecycle metadata may change |
| Rollback identity | Reactivate an immutable historical version through an audited rollback action; do not copy or edit it |
| Deletion | No hard-delete endpoint for profile or version |

## 2. Definitions

**API Destination Profile**: Stable logical identity used by Mapping Config authors to select a partner API destination.

**Profile Key**: Immutable lowercase logical identifier such as `pa-mtls`.

**Profile Version**: Versioned endpoint, method, authentication reference, timeout, headers, supported contracts, and payload bindings.

**Draft**: Mutable Profile Version not available to Mapping Config creation or runtime delivery.

**Active**: Immutable Profile Version selected for new Mapping Config Versions and new eligible File Jobs.

**Disabled**: Immutable Profile Version unavailable for new work. Historical pinned jobs can still resolve it according to Section 3.6.

**Pinned Resolution**: Loading a Profile Version by its immutable database/version identity rather than the currently Active version.

**Active Resolution**: Loading the one currently Active Profile Version for authoring or new-work admission.

**Safe Projection**: API response that excludes secret values, payloads, raw transport errors, and endpoint details not authorized for the caller.

**Content Hash**: Server-calculated SHA-256 over the canonical delivery-affecting Profile Version fields. Lifecycle and audit metadata are excluded.

**Concurrency Token**: Explicit positive `bigint` revision used by `ETag`/`If-Match`; it is not PostgreSQL `xmin`.

**Catalog Source Mode**: Explicit runtime mode `Configuration`, `Compare`, or `Postgres`. It never changes automatically after startup.

**Gate F**: Production release gate requiring profile catalog parity, security controls, operational rollback evidence, full regression, and owner approval.

## 3. Requirements, Constraints & Guidelines

### 3.1 Identity and lifecycle requirements

- **PRO-ID-001**: `profileKey` shall match `^[a-z][a-z0-9-]{1,62}$` and be unique using ordinal comparison.
- **PRO-ID-002**: `profileKey` shall be immutable. Rename shall be represented by a new profile.
- **PRO-ID-003**: `versionNo` shall start at 1, increase monotonically per profile, and never be reused after failure, disable, or rollback.
- **PRO-ID-004**: The catalog shall allow at most one Draft and one Active version per profile through database constraints and transactional locking.
- **PRO-LCY-001**: Normal lifecycle shall be `Draft → Active → Disabled`.
- **PRO-LCY-002**: Activating a Draft shall atomically mark the previous Active version Disabled and the Draft Active.
- **PRO-LCY-003**: Activated content shall be immutable. An endpoint, method, auth, timeout, contract, header, or binding change requires a new Draft version.
- **PRO-LCY-004**: An audited rollback may transition one historical Disabled version back to Active while atomically disabling the current Active version. It shall not change content or `versionNo`.
- **PRO-LCY-005**: Disabling a profile shall leave no Active version and shall not delete Draft, Disabled, audit, Mapping Config, File Job, or Delivery Job data.
- **PRO-LCY-006**: A Disabled version shall not be edited or cloned implicitly. Creating a new Draft from history shall be an explicit action.
- **PRO-LCY-007**: Profile display name is non-routing metadata. Changing it requires authorization, concurrency control, reason, and audit, but does not create a Profile Version.

### 3.2 Ownership and authorization requirements

| Policy | Allowed operations |
|---|---|
| `ProfileAdmin.Read` | list, get, history, safe validation results |
| `ProfileAdmin.EndpointRead` | view authorized schema/delivery host and logical secret reference |
| `ProfileAdmin.WriteDraft` | create profile, create/clone/update Draft, validate Draft |
| `ProfileAdmin.Activate` | activate Sandbox/Stub Draft or rollback Sandbox/Stub version |
| `ProfileAdmin.ActivateProduction` | activate or rollback Production version |
| `ProfileAdmin.Disable` | disable Active profile/version |

- **PRO-AUT-001**: Production authorization shall use an authenticated principal and server authorization policy. `X-Operator-Id` alone is not an authentication mechanism.
- **PRO-AUT-002**: Operator identity shall be derived from authenticated claims in production. Local/demo mode may use `X-Operator-Id` only behind an explicit non-production feature flag.
- **PRO-AUT-003**: Every mutation shall require a reason of 10–500 non-whitespace characters and a unique request/correlation ID.
- **PRO-AUT-004**: Contract owner approves supported contracts and payload bindings; Platform/Operations approves endpoints, timeout, environment, and rollout; Security approves authentication scheme/reference and network policy.
- **PRO-AUT-005**: Production activation requires `ProfileAdmin.ActivateProduction` and successful validation of the exact Draft content hash.
- **PRO-AUT-006**: API responses shall apply Safe Projection after authorization. Absence of endpoint-read permission shall redact hosts and logical secret references.
- **PRO-AUT-007**: Browser visibility shall not be treated as authorization. Every API action shall enforce policy independently.

### 3.3 Profile Version schema requirements

The delivery-affecting Profile Version content is:

```text
ApiDestinationProfileVersion
├─ profileVersionId: bigint
├─ profileKey: string
├─ versionNo: integer
├─ lifecycle: Draft | Active | Disabled
├─ environment: Stub | Sandbox | Production
├─ schemaBaseUrl: HTTPS origin
├─ schemaPathTemplate: string
├─ deliveryBaseUrl: HTTPS origin
├─ deliveryPathTemplate: string
├─ method: POST | PUT | PATCH
├─ authScheme: None | Mtls | OAuth2ClientCredentials
├─ authSecretReference: logical string | null
├─ networkPolicyReference: logical string
├─ timeoutSeconds: 1..120
├─ idempotencyHeaderName: string
├─ correlationHeaderName: string
├─ allowedHeaderNames[]
├─ supportedContracts[]
├─ payloadBindings[]
├─ contentHash: SHA-256
├─ concurrencyToken: bigint
└─ created/updated/validated/activated/disabled metadata
```

- **PRO-SCH-001**: PostgreSQL shall contain at least `api_destination_profiles`, `api_destination_profile_versions`, `api_destination_profile_contracts`, `api_destination_profile_bindings`, and append-only `api_destination_profile_audit`.
- **PRO-SCH-002**: Profile Version children shall use foreign keys and deterministic ordinal/order where order affects canonical hash or display.
- **PRO-SCH-003**: `contentHash` shall be calculated server-side from canonical delivery-affecting fields. Browser-provided hashes are non-authoritative.
- **PRO-SCH-004**: The database shall prevent duplicate keys, duplicate version numbers, multiple Drafts, multiple Active versions, duplicate contracts, and duplicate binding targets.
- **PRO-SCH-005**: Concurrency shall use an explicit incrementing token exposed as `ETag`; mutation requires matching `If-Match` and stale mutation returns `412 Precondition Failed`.
- **PRO-SCH-006**: Profile/version/audit rows referenced by Mapping Config Versions or Delivery Jobs shall be retained at least as long as delivery audit retention and shall never be hard deleted through Admin APIs.
- **PRO-SCH-007**: Secret values, OAuth client IDs/secrets, certificates, tokens, payloads, and request/response bodies shall not be stored in profile catalog tables.

### 3.4 Endpoint, template, header, and SSRF requirements

- **PRO-NET-001**: Base URLs shall be absolute HTTPS origins with DNS hostnames. IP literals, loopback names, user information, path other than `/`, query, and fragment are forbidden.
- **PRO-NET-002**: Default port is 443. Any other port requires a server-owned network policy that explicitly allows it.
- **PRO-NET-003**: DNS A/AAAA results shall be checked against a Security-owned network policy at validation and connection time. Loopback, link-local, multicast, unspecified, and reserved ranges are always forbidden. Private ranges require an explicit approved policy.
- **PRO-NET-004**: Redirect following shall be disabled for schema, delivery, and OAuth token requests. A redirect response is a controlled failure.
- **PRO-NET-005**: DNS rebinding protection shall validate every resolved address used for the connection. A hostname passing Draft validation does not bypass connection-time checks.
- **PRO-NET-006**: `schemaPathTemplate` may contain only `{contractReference}`. `deliveryPathTemplate` may contain only `{contractVersion}` and `{contractReference}`. Values shall be encoded as path segments by server code.
- **PRO-NET-007**: Templates shall begin with one `/`, contain no scheme/authority, traversal, empty segment, query, fragment, backslash, control character, or unknown token.
- **PRO-NET-008**: Allowed methods are `POST`, `PUT`, and `PATCH`. A new method requires a specification change.
- **PRO-NET-009**: Header names shall be unique case-insensitively and valid HTTP token names. `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`, `Host`, `Content-Length`, `Transfer-Encoding`, and hop-by-hop headers are forbidden.
- **PRO-NET-010**: The configured idempotency and correlation headers shall be included in `allowedHeaderNames`. Header values remain server/runtime owned.
- **PRO-NET-011**: Profile validation shall not send a business payload. Schema checks use GET and delivery connectivity checks, if enabled, use an approved non-mutating mechanism or stub/sandbox endpoint.

### 3.5 Secret and authentication requirements

- **PRO-SEC-001**: Catalog and browser shall store/receive only `authSecretReference`, never the secret value.
- **PRO-SEC-002**: `None` requires a null/empty secret reference. `Mtls` and `OAuth2ClientCredentials` require a valid logical secret reference.
- **PRO-SEC-003**: Draft validation may call a secret-provider metadata operation that returns only `Present`, `Missing`, `InvalidShape`, or `Unchecked`; it shall not return secret content.
- **PRO-SEC-004**: Secret values shall be resolved at request time. Rotation behind the same reference shall not require a new Profile Version and shall not change pinned profile identity.
- **PRO-SEC-005**: Changing auth scheme or logical secret reference requires a new Draft version.
- **PRO-SEC-006**: OAuth token endpoints obtained from secret material shall pass the same HTTPS, redirect, DNS, and network-policy checks as delivery endpoints.
- **PRO-SEC-007**: Secret reference and endpoint host may appear only in authorized Admin responses. Logs, metrics, validation diagnostics, and audit summaries shall use redacted/safe values.

### 3.6 Version pinning and runtime requirements

- **PRO-PIN-001**: Creating an ApiContract Mapping Config Version shall resolve the currently Active Profile Version and persist `api_destination_profile_version_id` with the existing logical profile key, Contract Snapshot, schema hash, and payload binding snapshot.
- **PRO-PIN-002**: Creating a Delivery Job shall copy the pinned `api_destination_profile_version_id` from its Mapping Config Version. It shall not resolve the current Active profile.
- **PRO-PIN-003**: Delivery retry shall resolve the exact pinned Profile Version, encrypted payload bytes, hash, and idempotency key. Activation or disable of another version shall not change the request.
- **PRO-PIN-004**: Activating a new Profile Version shall affect only Mapping Config Versions created after activation. Existing Mapping Config Versions remain pinned.
- **PRO-PIN-005**: Disabling a profile shall block new Mapping Config creation/activation, new File Job admission, reprocess, and resend that would create new external work. It shall not make historical pinned resolution impossible.
- **PRO-PIN-006**: File Jobs admitted before disable and existing nonterminal Delivery Jobs may complete/retry with their pinned version unless an Operations delivery pause or security kill switch blocks dispatch.
- **PRO-PIN-007**: Runtime shall not substitute a current Active version when a pinned version is missing. Missing pinned data is a controlled retryable/blocked failure and an alert.
- **PRO-PIN-008**: Profile disable is not a substitute for the global delivery pause. Operations shall use existing delivery controls for immediate dispatch suspension.
- **PRO-PIN-009**: Contract Snapshot and payload binding snapshot remain authoritative for document shape. The pinned Profile Version remains authoritative for transport/auth settings.

### 3.7 Catalog module and cache requirements

The Profile Catalog Module shall expose three distinct operations:

```csharp
ValueTask<SafeProfileList> ListActiveSafeAsync(
    ProfileReadContext context,
    CancellationToken cancellationToken);

ValueTask<ProfileResolution> ResolveActiveAsync(
    string profileKey,
    CancellationToken cancellationToken);

ValueTask<ProfileResolution> ResolvePinnedAsync(
    long profileVersionId,
    CancellationToken cancellationToken);
```

- **PRO-CAT-001**: API and Worker shall call the same module contract and validation rules.
- **PRO-CAT-002**: `ResolveActiveAsync` shall query authoritative PostgreSQL state in v1; correctness shall not depend on an eventually consistent active-pointer cache.
- **PRO-CAT-003**: `ResolvePinnedAsync` may cache immutable Active/Disabled version content for at most five minutes, keyed by `profileVersionId` and `contentHash`. A cache entry shall never be replaced with different content for the same key/hash.
- **PRO-CAT-004**: A database outage may use an already-loaded exact immutable pinned-cache entry. Cache miss shall fail closed and shall not fall back to a different Active or configuration profile.
- **PRO-CAT-005**: List/search cache may be stale for at most 15 seconds and shall be invalidated locally after mutation. List cache shall never be used for runtime pinned resolution.
- **PRO-CAT-006**: Once `CatalogSourceMode=Postgres`, there is no automatic fallback to application configuration. `Configuration` remains an explicit bootstrap/local-test mode only.
- **PRO-CAT-007**: `Compare` mode shall read configuration and PostgreSQL, compare canonical hashes, emit safe mismatch metrics, and use the explicitly configured authoritative side. Mismatch shall block cutover.
- **PRO-CAT-008**: Startup shall fail when source mode is invalid, required adapter is unavailable, or duplicate profile identity/version exists.

### 3.8 Audit and observability requirements

- **PRO-AUD-001**: Audit actions shall include `CreateProfile`, `UpdateDisplayName`, `CreateDraft`, `UpdateDraft`, `ValidateDraft`, `Activate`, `Disable`, and `Rollback`.
- **PRO-AUD-002**: Audit row shall include profile/version identity, action, authenticated actor, reason, request ID, timestamp, before/after lifecycle, before/after content hash, and concurrency token.
- **PRO-AUD-003**: Audit is append-only. Application roles shall not update/delete audit rows.
- **PRO-AUD-004**: Audit shall not include secret values, payloads, raw response bodies, access tokens, certificates, or unredacted transport errors.
- **PRO-OBS-001**: Metrics shall include validation result, activation/disable count, catalog resolution failure, cache hit/miss, source mismatch, and resolve latency.
- **PRO-OBS-002**: Metric labels may include environment and lifecycle but shall not include profile key, endpoint host, secret reference, contract reference, payload, operator, or customer identifiers.
- **PRO-OBS-003**: Alerts shall cover pinned-version missing, PostgreSQL catalog unavailable, configuration/PostgreSQL parity mismatch, unauthorized activation attempt, and repeated validation failure.

### 3.9 Migration, cutover, and rollback requirements

- **PRO-MIG-001**: Migration shall be expand-only and shall not rewrite Contract Snapshots, payload binding snapshots, payload bytes, idempotency keys, or existing destination JSON.
- **PRO-MIG-002**: Seed shall be idempotent and include all current profiles: `pa-mtls`, `pa-mtls-inform`, and `claims-envelope`.
- **PRO-MIG-003**: Seed canonical content hashes shall match API and Worker configuration definitions before Compare mode can pass.
- **PRO-MIG-004**: Existing ApiContract Mapping Config Versions and Delivery Jobs shall be backfilled to the exact seeded version through deterministic logical profile key mapping. Ambiguous/missing mapping blocks cutover.
- **PRO-MIG-005**: API and Worker shall support `Configuration`, `Compare`, and `Postgres` modes during the compatibility window. Both processes shall use the same declared authoritative mode before Admin mutation is enabled.
- **PRO-MIG-006**: Profile mutation shall remain disabled during mixed-binary deployment and until every API/Worker instance reads PostgreSQL authoritatively.
- **PRO-MIG-007**: Configuration definitions may be removed as an authoritative source only after parity, restart, mixed-deployment, pinned retry, and rollback rehearsal pass.
- **PRO-MIG-008**: Before the first DB-only profile/version is used, source rollback to configuration is allowed only when parity hashes match. After DB-only use, rollback shall keep PostgreSQL authoritative, disable Admin mutation, and reactivate a historical Profile Version as needed.
- **PRO-MIG-009**: Application rollback shall tolerate the expanded schema. Database rollback shall not drop profile/version/audit rows while any Mapping Config Version, File Job, Delivery Job, or audit retention reference exists.
- **PRO-MIG-010**: Activation/disable/rollback shall use a transaction and profile-level lock so concurrent requests produce one deterministic winner and one conflict.

## 4. Interfaces & Data Contracts

### 4.1 PostgreSQL aggregate

```text
api_destination_profiles
├─ id bigint PK
├─ profile_key text UNIQUE NOT NULL
├─ display_name text NOT NULL
├─ active_version_id bigint NULL
├─ concurrency_token bigint NOT NULL
├─ created_at / created_by
└─ updated_at / updated_by

api_destination_profile_versions
├─ id bigint PK
├─ profile_id bigint FK
├─ version_no integer NOT NULL
├─ lifecycle text: Draft | Active | Disabled
├─ environment / endpoint / template / method fields
├─ auth scheme / auth secret reference / network policy reference
├─ timeout / idempotency header / correlation header / allowed headers
├─ content_hash text NOT NULL
├─ concurrency_token bigint NOT NULL
├─ validated_at / validated_content_hash
├─ created_at / created_by / updated_at / updated_by
├─ activated_at / activated_by
├─ disabled_at / disabled_by
└─ UNIQUE(profile_id, version_no)

api_destination_profile_contracts
├─ profile_version_id bigint FK
├─ ordinal integer
├─ contract_id / contract_version / contract_reference
└─ UNIQUE(profile_version_id, contract_reference)

api_destination_profile_bindings
├─ profile_version_id bigint FK
├─ ordinal integer
├─ target_path text
├─ runtime_value_source text
└─ UNIQUE(profile_version_id, target_path)

api_destination_profile_audit
├─ id bigint PK
├─ profile_id / profile_version_id
├─ action / actor / reason / request_id / occurred_at
├─ before_lifecycle / after_lifecycle
├─ before_content_hash / after_content_hash
└─ concurrency_token
```

Required partial uniqueness:

```text
UNIQUE(profile_id) WHERE lifecycle = 'Draft'
UNIQUE(profile_id) WHERE lifecycle = 'Active'
```

`active_version_id` is a fast authoritative pointer and shall reference the same profile and an Active version through transactional application validation plus database constraints/triggers selected in Step 26.1. The migration design shall prove no state where pointer and lifecycle disagree can commit.

### 4.2 Admin endpoints

| Method and path | Policy | Purpose |
|---|---|---|
| `GET /admin/api-destination-profiles` | Read | bounded list/search/filter |
| `POST /admin/api-destination-profiles` | WriteDraft | create stable profile plus version 1 Draft |
| `GET /admin/api-destination-profiles/{profileKey}` | Read | profile, active/draft summary, safe details |
| `GET /admin/api-destination-profiles/{profileKey}/versions` | Read | immutable version history |
| `POST /admin/api-destination-profiles/{profileKey}/versions` | WriteDraft | create Draft from Active or selected historical version |
| `PUT /admin/api-destination-profiles/{profileKey}/versions/{versionNo}` | WriteDraft | update Draft with `If-Match` |
| `POST /admin/api-destination-profiles/{profileKey}/versions/{versionNo}/validate` | WriteDraft | validate exact Draft without lifecycle change |
| `POST /admin/api-destination-profiles/{profileKey}/versions/{versionNo}/activate` | Activate | activate validated Draft |
| `POST /admin/api-destination-profiles/{profileKey}/versions/{versionNo}/rollback` | Activate | reactivate immutable historical version |
| `POST /admin/api-destination-profiles/{profileKey}/disable` | Disable | disable current Active version/profile |

No endpoint shall delete a profile/version or accept a secret value.

### 4.3 Create request example

```json
{
  "profileKey": "partner-proposals",
  "displayName": "Partner Proposals",
  "draft": {
    "environment": "Sandbox",
    "schemaBaseUrl": "https://contracts.partner.example",
    "schemaPathTemplate": "/schemas/{contractReference}",
    "deliveryBaseUrl": "https://delivery.partner.example",
    "deliveryPathTemplate": "/proposals/{contractVersion}",
    "method": "POST",
    "authScheme": "Mtls",
    "authSecretReference": "partner-proposals-mtls",
    "networkPolicyReference": "partner-egress-default",
    "timeoutSeconds": 10,
    "idempotencyHeaderName": "Idempotency-Key",
    "correlationHeaderName": "X-Correlation-Id",
    "allowedHeaderNames": ["Idempotency-Key", "X-Correlation-Id"],
    "supportedContracts": [
      { "id": "proposal", "version": "1", "reference": "proposal/1" }
    ],
    "payloadBindings": [
      { "targetPath": "meta.fileJobId", "source": "FileJobId" }
    ]
  },
  "reason": "Onboard approved sandbox partner profile"
}
```

### 4.4 Safe response example

```json
{
  "profileKey": "partner-proposals",
  "displayName": "Partner Proposals",
  "lifecycle": "Draft",
  "activeVersionNo": null,
  "draft": {
    "versionNo": 1,
    "reference": "partner-proposals/1",
    "environment": "Sandbox",
    "method": "POST",
    "deliveryPathTemplate": "/proposals/{contractVersion}",
    "endpointVisibility": "Redacted",
    "schemaBaseUrl": null,
    "deliveryBaseUrl": null,
    "authScheme": "Mtls",
    "authSecretReference": null,
    "secretStatus": "Present",
    "contentHash": "sha256:...",
    "concurrencyToken": 1
  }
}
```

Caller with `ProfileAdmin.EndpointRead` may receive approved endpoint hosts and logical secret reference. Secret value is never returned.

### 4.5 Mutation concurrency and audit contract

- Successful GET of mutable profile/Draft returns `ETag: "{concurrencyToken}"`.
- PUT/activate/disable/rollback requires `If-Match`.
- Missing `If-Match` returns `428 Precondition Required`.
- Stale `If-Match` returns `412 Precondition Failed` with safe current token metadata.
- Duplicate identity or lifecycle race returns `409 Conflict`.
- Validation errors return `400` with stable code and exact field path.
- Missing profile/version returns `404`.
- Unauthorized/forbidden returns `401`/`403` without revealing existence beyond policy.
- Audit insert and state mutation commit in one transaction.

### 4.6 Validation result contract

```text
ProfileValidationResult
├─ contentHash
├─ validatedAt
├─ isValid
├─ secretStatus: Present | Missing | InvalidShape | Unchecked
├─ endpointStatus: Valid | Invalid | Unreachable | Unchecked
├─ contractStatus[]
└─ diagnostics[]
   ├─ path
   ├─ code
   └─ safe message
```

Activation shall recompute canonical content hash and reject a validation result from different content. Production activation shall re-run all security-critical static checks inside the activation transaction/workflow.

### 4.7 Runtime resolution contract

```text
New Mapping Config Version
  logical profileKey
    → ResolveActive(profileKey)
    → pin profileVersionId + Profile Version reference
    → fetch/canonicalize Contract Snapshot
    → pin payload binding snapshot

Delivery Job creation
  Mapping Config Version.profileVersionId
    → copy profileVersionId to Delivery Job

Delivery retry
  Delivery Job.profileVersionId
    → ResolvePinned(profileVersionId)
    → use persisted payload/hash/idempotency key
```

## 5. Acceptance Criteria

- **AC-PRO-ID-01**: Given profile `pa-mtls`, when rename is requested, then the API rejects it and requires a new profile key.
- **AC-PRO-ID-02**: Given concurrent Draft creation, when both commit, then one Draft is created and the other receives a conflict; no version number is reused.
- **AC-PRO-LCY-01**: Given Draft v2 and Active v1, when v2 activates, then v2 is Active and v1 is Disabled in one transaction.
- **AC-PRO-LCY-02**: Given Active or Disabled version, when content update is attempted, then the API rejects mutation.
- **AC-PRO-LCY-03**: Given historical v1 and Active v2, when authorized rollback selects v1, then v1 becomes Active, v2 becomes Disabled, content hashes remain unchanged, and audit records the transition.
- **AC-PRO-AUT-01**: Given an unauthenticated or unauthorized caller, when any Admin mutation is requested, then it returns `401`/`403` and no state/audit mutation occurs.
- **AC-PRO-AUT-02**: Given a production Draft, when caller lacks `ProfileAdmin.ActivateProduction`, then activation is forbidden.
- **AC-PRO-CON-01**: Given two editors use the same token, when one saves first, then the second receives `412` and cannot overwrite the Draft.
- **AC-PRO-VAL-01**: Given invalid URL/template/header/auth/timeout/binding, when validation runs, then exact-path diagnostics are returned with no lifecycle change or secret disclosure.
- **AC-PRO-VAL-02**: Given content changes after validation, when activation runs, then stale validation is rejected by content hash.
- **AC-PRO-SEC-01**: Given Admin list/detail/history/audit responses and logs, when scanned, then no secret value, certificate, token, payload, or body is present.
- **AC-PRO-SEC-02**: Given loopback/IP/private-unapproved/rebinding/redirect endpoint behavior, when validation or connection occurs, then it fails closed before sending business payload.
- **AC-PRO-PIN-01**: Given Mapping Config v10 pins profile v1 and profile v2 later activates, when a new File Job uses Mapping Config v10, then transport still resolves profile v1.
- **AC-PRO-PIN-02**: Given Delivery Job pinned to disabled profile v1, when retry occurs, then exact v1 endpoint/auth reference is resolved and payload/hash/idempotency key remain unchanged.
- **AC-PRO-PIN-03**: Given a profile is disabled, when new Mapping Config activation, File Job intake, reprocess, or resend is attempted, then new external work is blocked while historical pinned retry remains resolvable.
- **AC-PRO-CAT-01**: Given API and Worker resolve the same Active/Pinned identity from PostgreSQL, then canonical hashes and safe behavior match.
- **AC-PRO-CAT-02**: Given PostgreSQL outage and exact pinned version is cached, then retry may use that immutable cache entry; given cache miss, resolution fails closed without configuration fallback.
- **AC-PRO-CUT-01**: Given seeded config and PostgreSQL catalogs, when Compare mode runs, then all three current profiles have equal canonical hashes before cutover.
- **AC-PRO-CUT-02**: Given a mismatch or unmapped historical profile key, when cutover is attempted, then Postgres activation/Admin mutation remains blocked.
- **AC-PRO-CUT-03**: Given mixed API/Worker deployment, when mutation is disabled, then no instance resolves a profile version not present in both authoritative sources.
- **AC-PRO-ROLL-01**: Given rollback before DB-only use, then configuration source may be restored only with parity; given DB-only use, rollback keeps PostgreSQL authoritative and reactivates historical version.
- **AC-PRO-AUD-01**: Given any successful mutation, then exactly one append-only audit record commits with actor/reason/request/hash/lifecycle and no sensitive value.
- **AC-PRO-UI-01**: Given an authorized keyboard/screen-reader/mobile user, when creating, validating, activating, disabling, comparing, or viewing history, then the flow is accessible and stale concurrency is visible without losing Draft input.
- **AC-PRO-REG-01**: Given Phase 26 is enabled, when LegacyTable, ApiContract, and JsonFile full regression runs, then destination selection, Contract Snapshot, payload bytes, retry identity, and historical jobs are unchanged.

### 5.1 Phase 26 traceability

| Step | Requirements | Acceptance IDs |
|---|---|---|
| 26.0 | All design requirements and Gate F contract | All AC-PRO-* |
| 26.1 | PRO-ID, PRO-LCY, PRO-SCH, PRO-AUD, PRO-MIG-001..004 | AC-PRO-ID-01..02, AC-PRO-LCY-01..03, AC-PRO-CON-01, AC-PRO-AUD-01 |
| 26.2 | PRO-PIN, PRO-CAT, PRO-MIG-005..010 | AC-PRO-PIN-01..03, AC-PRO-CAT-01..02, AC-PRO-CUT-01..03, AC-PRO-ROLL-01 |
| 26.3 | PRO-AUT, PRO-NET, PRO-SEC, API contracts | AC-PRO-AUT-01..02, AC-PRO-VAL-01..02, AC-PRO-SEC-01..02, AC-PRO-AUD-01 |
| 26.4 | Safe Projection and Admin UI | AC-PRO-CON-01, AC-PRO-SEC-01, AC-PRO-UI-01 |
| 26.5 | PRO-MIG and runtime pinning | AC-PRO-PIN-01..03, AC-PRO-CUT-01..03, AC-PRO-ROLL-01 |
| 26.6 | PRO-OBS, full security/operations/regression | AC-PRO-SEC-01..02, AC-PRO-AUD-01, AC-PRO-REG-01 |

## 6. Test Automation Strategy

| Level | Required coverage |
|---|---|
| Unit | identifier rules, canonical hash, lifecycle transitions, typed validation, safe projection, header/template/network rules, cache keys |
| PostgreSQL integration | constraints, partial uniqueness, locks, optimistic concurrency, atomic activate/disable/rollback/audit, immutable rows, backfill |
| Module contract | identical active/pinned results from configuration and PostgreSQL adapters for seeded fixtures |
| API integration | authentication/authorization, `ETag`/`If-Match`, validation errors, audit, redaction, no delete endpoint |
| Worker integration | exact pinned resolution, retry after disable, cache hit/miss, database outage, no wrong-source fallback |
| Security | SSRF/DNS rebinding/redirect, secret redaction, malformed headers/templates, privilege escalation, oversized request |
| Mixed deployment | Configuration → Compare → Postgres with mutation freeze, restart, rolling API/Worker instances, rollback rehearsal |
| End-to-End | create Draft → validate → activate → select in Mapping Workbench → create Config Version → schedule/deliver with pinned version |
| Regression | LegacyTable, ApiContract exact bytes/idempotency, JsonFile, reprocess/resend, historical Config/File/Delivery Jobs |

Tests use the repository's existing xUnit, PostgreSQL integration fixture, ASP.NET controller/integration patterns, and Playwright setup. New third-party packages are not required by this design.

Performance validation shall prove bounded list pagination, no per-row profile lookup, and acceptable p95 resolution latency under expected Delivery Worker concurrency. Exact thresholds are approved by Platform/Operations in Gate F evidence.

## 7. Rationale & Context

Current profiles are duplicated in API and Worker application settings. This creates drift risk and requires restart for onboarding. Delivery Jobs persist only logical profile ID, so current retry can accidentally resolve changed current configuration. Phase 26 adds immutable Profile Version identity to make retry transport deterministic while keeping credential rotation possible through a stable secret reference.

Profile key rename is forbidden because existing Config Versions, Delivery Jobs, audit, dashboards, and operations procedures use the logical key. A new identity plus explicit migration is safer and observable.

The design separates Active resolution from Pinned resolution. Active state controls new authoring/work admission. Pinned state preserves historical execution. This prevents profile disable or new activation from silently changing a queued retry.

## 8. Dependencies & External Integrations

### External systems

- **EXT-001**: PostgreSQL — authoritative versioned profile catalog, references, and audit.
- **EXT-002**: Partner schema endpoints — read-only JSON Schema retrieval through approved network policy.
- **EXT-003**: Partner delivery endpoints — one request per persisted Delivery Job.
- **EXT-004**: Secret provider — logical-reference lookup and metadata/shape validation without secret disclosure.
- **EXT-005**: Identity provider — authenticated Admin principal and role/policy claims.

### Infrastructure dependencies

- **INF-001**: API and Worker use the same PostgreSQL database/catalog.
- **INF-002**: Egress/DNS enforcement can validate resolved IP addresses and disable redirects.
- **INF-003**: Existing delivery pause, activation controls, rollout stages, alerts, and retention remain available.

### Data dependencies

- **DAT-001**: Existing application profile definitions are migration input only and must canonicalize identically before cutover.
- **DAT-002**: Mapping Config Versions retain Contract Snapshot and payload binding snapshot.
- **DAT-003**: Delivery Jobs retain encrypted payload, hash, idempotency key, logical profile key, and new pinned Profile Version ID.

## 9. Examples & Edge Cases

### 9.1 New version does not mutate old Config Version

```text
pa-mtls/1 Active
Mapping Config 10 pins pa-mtls/1

Admin creates and activates pa-mtls/2
pa-mtls/1 Disabled; pa-mtls/2 Active

Mapping Config 10 still resolves pa-mtls/1
New Mapping Config 11 resolves pa-mtls/2
```

### 9.2 Disable and retry

```text
Delivery Job 500 pins pa-mtls/1 and fails with HTTP 503
Admin disables pa-mtls
New Config activation, File Job intake, reprocess, and resend are blocked
Delivery Job 500 retry may resolve pa-mtls/1 unless Operations pauses dispatch
```

### 9.3 Secret rotation

```text
pa-mtls/1 pins authSecretReference = pa-mtls-client-certificate
Security rotates value behind that reference
pa-mtls/1 identity and content hash remain unchanged
Next request resolves rotated secret value
```

### 9.4 Catalog outage

```text
Pinned pa-mtls/1 exists in immutable cache → exact retry may continue
Pinned pa-mtls/1 absent from cache → retry remains durable and fails closed
System never substitutes pa-mtls/2 or application configuration
```

### 9.5 Edge cases

- Activation races with disable: profile lock allows one winner; loser receives conflict.
- Draft changes after validation: activation rejects stale `validatedContentHash`.
- Same header with different casing: duplicate validation error.
- DNS returns one approved and one forbidden address: entire endpoint validation fails.
- OAuth secret contains loopback token endpoint: validation/transport fails without exposing endpoint or secret.
- Historical version references retired Contract Version: pinned Contract Snapshot still works; new Draft validation follows current Contract Registry policy.
- Configuration and PostgreSQL differ by binding order/content: Compare mode blocks cutover.
- A DB-only profile has been selected by a Mapping Config: automatic rollback to configuration is forbidden.

## 10. Validation Criteria

Step 26.0 is design-complete when:

- all identity decisions in Section 1.4 are accepted;
- lifecycle, persistence, API, authorization, endpoint/secret, pinning, cache, migration, and rollback contracts contain no unresolved default;
- every Phase 26 implementation step maps to requirements and acceptance IDs in Section 5.1;
- current profile inventory includes `pa-mtls`, `pa-mtls-inform`, and `claims-envelope`;
- Contract, Platform/Operations, and Security owners approve the decision record below.

### 10.1 Gate F design approval record

| Owner role | Required approval scope | Approver | Status | Date |
|---|---|---|---|---|
| Contract / Business | identity, contracts/bindings, lifecycle, new-work blocking, rollback semantics | Project owner (user approval) | Approved | 2026-10-29 |
| Platform / Operations | PostgreSQL ownership, cache, endpoint/timeout, cutover, observability, rollback | Project owner (user approval) | Approved | 2026-10-29 |
| Security | authorization, secret policy, SSRF/DNS policy, audit/redaction | Project owner (user approval) | Approved | 2026-10-29 |

Implementation may prepare code only after the required owner approvals are recorded. Production Admin mutation remains disabled until final Gate F evidence in Step 26.6.

## 11. Related Specifications / Further Reading

- [Contract API and Legacy Table Delivery Design](spec-design-contract-api-legacy-delivery.md)
- [Web Config Plan](../docs/web-config-plan.md)
- [Phase 24 Delivery Decisions](../docs/phase-24-delivery-decisions.md)
- [Phase 24 Operations Runbook](../docs/phase-24-operations-runbook.md)
- [Domain Context](../CONTEXT.md)
- [Current Program Workflow](../current-program-workflow.md)
