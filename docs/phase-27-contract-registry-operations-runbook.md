# Phase 27 Contract Registry Operations Runbook

This runbook covers Contract Draft creation, validation, cloning, publication, retirement, rollback and Contract Registry incidents. PostgreSQL is authoritative when `ContractRegistry:SourceMode=Postgres`. Runtime never falls back automatically to bundled schemas.

## Access and safety rules

- Read requires `ContractAdmin.Read`.
- Draft create/update/validate/clone requires `ContractAdmin.WriteDraft`.
- Publish/Retire requires `ContractAdmin.Publish` and `ContractRegistry:PublishingEnabled=true`.
- Every mutation requires authenticated actor, unique request ID and a 10–500 character reason.
- Update, Publish and Retire require the current `If-Match` token.
- Published/Retired identity, canonical schema and hash are immutable. There is no hard delete.
- Schema is limited to 1 MiB, the supported local-reference JSON Schema subset and synthetic content without credentials/customer data.

Production configuration keeps `PublishingEnabled=false` until Gate G owner approval and the planned rollout change. Gate approval does not toggle configuration by itself.

## Create and publish

1. Open `/admin/contracts`; supply operator ID and change-ticket reason.
2. Create a stable lower-case Contract key and opaque Version label.
3. Paste or upload JSON Schema. Resolve malformed JSON and content-policy warnings locally.
4. Save Draft, then Validate. Review derived paths, required/nullability/constraints, canonical hash and compatibility.
5. For `Breaking`, obtain Contract owner approval and tick the explicit acknowledgement.
6. Publish after confirming the immutable reference. Verify:
   - lifecycle is `Published`;
   - canonical `sha256` is present;
   - exactly one `Publish` audit row exists;
   - the Version appears in the profile editor Published selector.
7. Attach the Published Version through a new API Destination Profile Draft, validate and activate under the Profile rollout policy.

Validate has no persistence/audit side effect. A `412` means another operator changed the Draft; preserve edits, refresh and reconcile before retrying.

## Clone

1. Select a Published or Retired Version.
2. Enter a new Version label and choose **Clone as new version**.
3. Edit only the new Draft. The source Version remains immutable.
4. Validate compatibility and publish using the normal approval path.

Only one Draft may exist per Contract. Finish or coordinate the existing Draft before cloning again.

## Retire

1. Remove the Version from active profile allowlists by creating/activating replacement Profile Versions.
2. Confirm no new Mapping Config creation selects it.
3. Retire with `ContractAdmin.Publish`, current `If-Match`, operator and reason.
4. Verify the Published list excludes it and exact historical resolution returns the unchanged canonical bytes/hash.

Retire fails with `ContractVersionInUse` while an Active API Destination Profile references the Version. Retire never rewrites Mapping Config snapshots or Delivery Jobs.

## Rollback

### Roll back a newly published Contract

1. Pause new Mapping Version activation and affected delivery rollout.
2. Activate the previous API Destination Profile Version/allowlist.
3. Retire the new Contract Version after it is no longer attached to an Active profile.
4. Do not edit the new or previous Published artifact.
5. Existing pinned Mapping Versions/Delivery Jobs continue with their stored snapshot/hash.

### Emergency source rollback

`ContractRegistry:SourceMode=Bundled` is an explicit emergency/local-test mode for the original `proposal/1` and `claims/2` bootstrap pair only. It freezes Admin mutation and does not cover later registry-created Versions. Use it only when those limitations are acceptable:

1. Pause publishing and new Mapping Version creation.
2. Confirm affected work uses only the bootstrap pair and their parity hashes.
3. Set API source mode to `Bundled` and restart the API.
4. Keep Worker delivery/retry running from pinned snapshots; do not treat Bundled as a DB outage fallback.
5. Repair PostgreSQL, compare canonical/hash/path parity, restore `Postgres` mode and restart.

If a later Contract Version is required, fail closed until PostgreSQL is restored.

## Seed and cutover verification

At API startup in Postgres mode, the bootstrapper:

- creates/publishes missing `proposal/1` and `claims/2` idempotently;
- refuses conflicting Draft/content;
- verifies canonical schema, SHA-256 and path catalog against bundled inputs;
- backfills exact nullable profile-contract FKs;
- never overwrites an existing Published Version.

Migration `0017_contract_registry_cutover.sql` auto-links new profile allowlist rows only to exact Published registry Versions. Unknown, Draft, Retired or mismatched references fail closed.

## Observability and alerts

Authorized operators can inspect `GET /admin/contract-registry-operations`. The `MappingDemo.ContractRegistry` Meter exposes safe bounded labels for:

- validation success/failure;
- create/update/clone/publish/retire success/failure;
- exact/published-list cache hit/miss;
- resolution failure class and latency;
- unauthorized publish attempts.

Alerts:

| Alert | Severity | Response |
|---|---|---|
| `ContractRegistryIntegrityOrAvailability` | Critical | Pause new config creation/publication; keep fail-closed; verify DB and canonical hash |
| `RepeatedContractValidationFailure` | Warning | Stop publication; inspect safe diagnostics and schema source |
| `UnauthorizedContractPublishAttempt` | Warning | Review identity, policy configuration and append-only audit |

Metrics never label Contract key/reference, actor, schema, endpoint, payload or secret. Process-local snapshots reset on restart; production needs centralized Meter export.

## Incident checks

- Registry unavailable: do not enable automatic Bundled fallback; restore PostgreSQL or use the bounded emergency rollback above.
- Hash drift/corruption: stop publication and new snapshot creation; compare stored canonical bytes/hash with server compilation; restore from backup rather than updating Published content.
- Suspected secret/PII: stop publication, preserve audit, remove the value from the Draft or clone a corrected Version; rotate any exposed credential outside this system.
- Unauthorized publish: keep `PublishingEnabled=false`, review claims/local-header settings and audit request IDs.
- Stale cache: successful mutation invalidates local caches; if another process is stale, wait at most 15 seconds for Published list or five minutes for immutable exact cache. Never change schema under an existing reference.

## Backup and recovery

Back up `contracts`, `contract_versions`, `contract_audit` and profile linkage with the rest of PostgreSQL. Recovery must preserve IDs, references, canonical bytes, hashes and audit rows. After restore, run bootstrap parity checks before enabling new Mapping Version creation or publication.
