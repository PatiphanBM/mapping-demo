# Phase 26 API Destination Profile Operations Runbook

Date: 2026-11-02  
Scope: versioned API Destination Profiles in PostgreSQL, Admin control plane, API/Worker resolution, secret rotation, rollback and incidents.

This runbook never authorizes exposing a secret value, changing a pinned job, or bypassing the destination rollout controls in the Phase 24 runbook.

## Preconditions and ownership

- PostgreSQL is the authoritative source: `ApiDestinationProfileCatalog:SourceMode=Postgres`.
- `Configuration` and `Compare` are compatibility/rehearsal modes only. Compare freezes mutations.
- Admin permissions are separate: `ProfileAdmin.Read`, `EndpointRead`, `WriteDraft`, `Activate`, `ActivateProduction`, and `Disable`.
- Every mutation needs the authenticated actor, a 10–500 character reason, request ID, and current concurrency token.
- Secret values live in the secret provider. PostgreSQL and the browser receive only a logical `AuthSecretReference`.
- Production activation requires both the normal activation permission and `ProfileAdmin.ActivateProduction`.

## Partner onboarding

1. Confirm the partner contract, owner, environment, egress policy, HTTPS DNS names, timeout, method, required headers, and logical secret reference.
2. Provision the credential in the secret provider. Do not paste its value into a ticket, profile field, log, or browser.
3. Open `/admin/api-destination-profiles`, create a stable `profileKey`, and create a Draft. A rename requires a new profile identity.
4. Use HTTPS origins on port 443 only. Origins cannot contain a path, query, fragment, user information, IP literal, loopback host, or private/reserved DNS answer. Redirects are forbidden.
5. Use only the supported template token for each path. Do not allow authorization, cookie, host, connection, transfer, or content-length headers.
6. Validate the Draft. Resolve every diagnostic and confirm the secret status without reading its value.
7. Have an independent authorized operator review the endpoint, contract allowlist, bindings, environment, and diff from the prior version.
8. Activate with the current `If-Match` token and an operational reason. For Production, require Gate F approval and the Phase 24 Shadow/Canary controls.
9. Verify the append-only audit record, the active version number, safe catalog projection, cache behavior, and operations snapshot.
10. Monitor validation, mutation, resolution, cache and delivery alerts through the entire canary window.

## Secret rotation

Use stable-reference rotation when only the credential value changes:

1. Pause new activation if an incident is active; do not edit the immutable Profile Version.
2. Add the new value/version behind the same logical secret reference.
3. Validate certificate/token shape, expiry, issuer/audience and partner trust out of band without logging the value.
4. Switch the provider alias atomically, run a non-business health check allowed by the partner, then monitor authentication failures.
5. Keep the prior provider version recoverable for the agreed overlap window; revoke it after successful observation.

Create and activate a new Profile Version when the logical secret reference, auth scheme, endpoint, headers, contract allowlist, bindings, timeout, or network policy changes. Historical jobs continue resolving their exact pinned Profile Version.

## Disable and rollback

### Disable

1. Pause dispatch first when payload delivery may be unsafe.
2. Call Disable with the current concurrency token and incident/change reason.
3. Verify the profile has no Active version and the audit action is `Disable`.
4. Confirm new Mapping Config activation and new external work are blocked.
5. Do not delete history. A historical Delivery Job may still resolve its pinned Disabled version; keep dispatch paused if retries are unsafe.

### Roll back active profile version

1. Identify the last known-good Disabled version by version ID and content hash.
2. Confirm its endpoint, contract, secret reference and network policy are still approved.
3. Call Rollback with the current profile concurrency token and reason.
4. Verify one Active version, the previous version Disabled, unchanged hashes, and one `Rollback` audit record.
5. Confirm existing Mapping Config Versions and Delivery Jobs retain their original pinned version IDs. Rollback affects new work only.

Automatic fallback to application configuration is forbidden after DB-only use.

## Metrics and alerts

The .NET meter is `MappingDemo.ApiDestinationProfiles`. Export it from every API and Worker instance through the deployment's OpenTelemetry pipeline. Authorized operators can inspect the process-local safe snapshot at `GET /admin/api-destination-profile-operations` with `ProfileAdmin.Read`.

Metric labels are bounded to environment, result, action, failure class, cache/result, and operation. Profile key, hostname, contract, operator, secret reference and payload are forbidden labels.

| Signal | Threshold | Severity | First response |
|---|---:|---|---|
| `PinnedVersionMissing` | any occurrence | Critical | pause affected delivery; verify migration/pin and catalog integrity |
| `ProfileCatalogUnavailable` | any cache-miss resolution failure | Critical | keep fail-closed; restore PostgreSQL; do not switch source |
| `ProfileCatalogSourceMismatch` | any occurrence | Critical | block cutover/mutation; compare canonical hashes |
| `UnauthorizedActivationAttempt` | any occurrence | Warning | review identity, claims, request ID and audit trail |
| `RepeatedValidationFailure` | 3 consecutive failures | Warning | stop activation; review safe diagnostics and secret status |
| pinned/safe-list cache miss ratio | >50% over 10 minutes with at least 20 reads | Warning | check churn, TTL, invalidation and database latency |
| active resolution latency | p95 >250 ms for 10 minutes at expected concurrency | Warning | inspect pool/database latency and query plan |

Successful validation resets the consecutive-validation counter. The process-local snapshot resets on restart; centralized Meter data is the operational record.

## Incident response

| Symptom | Containment | Diagnosis | Recovery |
|---|---|---|---|
| wrong partner/routing suspicion | pause dispatch and profile activation | compare pinned version ID, content hash, endpoint approval and audit | rollback only new work; preserve jobs/evidence |
| catalog unavailable | keep runtime fail-closed | PostgreSQL health, pool, migration and permissions | restore DB; retry durable work; never configuration-fallback |
| pinned version missing | pause affected jobs | verify `api_destination_profile_version_id`, FK, migration `0015`, audit | restore catalog/backup; never substitute Active version |
| source mismatch | freeze mutation/cutover | canonical Compare output for all seeded profiles | correct drift and re-run parity before Postgres authority |
| repeated validation failure | block activation | exact safe diagnostic, secret metadata, endpoint/network policy | update Draft or provider metadata, validate again |
| unauthorized activation | block caller and retain logs | identity claims, request ID, actor, audit and access logs | correct/revoke access; security approval before retry |
| authentication failure after secret rotation | pause dispatch if retries risk lockout | provider alias/version, certificate/token metadata, partner trust | restore prior provider value behind same reference |
| SSRF/DNS/redirect rejection | do not add an exception ad hoc | DNS answer set, port, redirect chain and egress policy | partner supplies approved public HTTPS origin; create reviewed Draft |

## Evidence capture and closure

For every production change/incident retain request ID, actor, reason, profile/version IDs, content hashes, audit rows, alert timeline, rollout control state and recovery verification. Never capture credentials or payload bodies. Close only after safe catalog resolution, pinned historical resolution, audit completeness, alert recovery and delivery controls are verified.
