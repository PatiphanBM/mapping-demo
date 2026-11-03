# Phase 24 API Delivery Operations Runbook

Owner roles: Platform / Operations for delivery controls, Contract for partner behavior, Security for secrets and resend authorization, Business for compensation.

## Safety invariants

- A Mapping Config Version selects exactly one destination. Never send the same production business document to both `LegacyTable` and a partner API for comparison.
- Retry keeps the persisted encrypted payload, payload hash and idempotency key. Reprocess and business resend are separate operations.
- Pausing delivery leaves pinned API jobs durable. It never reroutes them to `LegacyTable`.
- Activating a previous `LegacyTable` version affects only new File Jobs. Delivered partner side effects need partner-defined compensation.

## Server-owned profile and secret setup

Profiles live under `ApiDestinationProfiles:Profiles`; browser requests contain only logical profile and contract references. Set `Environment` to `Stub`, `Sandbox` or `Production`. A `Shadow` rollout cannot activate a profile marked `Production`.

The Worker resolves logical secret references from environment variables. Replace punctuation with `_` and uppercase the reference:

| Reference | Environment variable | Value shape |
|---|---|---|
| `delivery-payload-v1` | `MAPPING_SECRET__DELIVERY_PAYLOAD_V1` | Base64 of exactly 32 random bytes |
| `pa-mtls-client-certificate` | `MAPPING_SECRET__PA_MTLS_CLIENT_CERTIFICATE` | JSON `{ "pfxBase64": "...", "password": "..." }` |
| `claims-oauth-client` | `MAPPING_SECRET__CLAIMS_OAUTH_CLIENT` | JSON `{ "tokenEndpoint": "https://...", "clientId": "...", "clientSecret": "...", "scope": "..." }` |

Do not put real values in source control, command history, screenshots or tickets.

## Local shadow validation

The repeatable local stub flow is automated and has no external business side effect:

```powershell
docker compose up -d postgres
$env:MAPPING_POSTGRES_INTEGRATION = "Host=localhost;Port=5432;Database=mapping;Username=mapping;Password=mapping"
$env:MAPPING_REQUIRE_POSTGRES_INTEGRATION = "true"
dotnet test tests/MappingDemo.Tests/MappingDemo.Tests.csproj --filter "FullyQualifiedName~Delivery"
Set-Location web
npm run test:e2e
```

The suite covers deterministic 2xx/400/408/429/5xx/timeout/idempotency scenarios, exact request bytes and headers, retry identity, two output branches, schema/path fixtures, no Normalized write in API mode, UI stale-action refresh and credential/body absence. It also keeps the second contract profile in the same core path.

Before activating a real config, register its rollout and set the global controls:

```powershell
pwsh ./scripts/configure-delivery-rollout.ps1 `
  -ConfigId 3 `
  -Stage Shadow `
  -OperatorId "operator-id" `
  -Reason "Approved non-mutating sandbox validation"
```

`Shadow` requires a `Stub` or `Sandbox` profile. Move to `Canary` only after contract validation is 100%, duplicate/idempotency conflicts and schema validation failures are zero, logs contain no credential or payload body, and latency/queue thresholds remain green.

## Dashboard and alerts

- UI: `http://localhost:3000/delivery`
- API snapshot: `GET /delivery-operations`
- File delivery detail: `GET /delivery-jobs?fileJobId={id}`

Metrics are grouped by Config Version and Contract Reference; labels do not contain file, row, correlation or customer identifiers. The snapshot includes status distribution, success/failure rates, p50/p95/p99 latency, queue age, retry count, HTTP 409 conflicts, API row validation failures and p95 source-to-delivered duration.

| Trigger | Required action |
|---|---|
| duplicate/idempotency conflict > 0 | immediate rollback and incident review |
| schema validation failure > 0 | immediate rollback and contract owner review |
| permanent failure > 1% | pause delivery |
| retryable failure > 5% | pause delivery |
| p95 latency > 5 seconds | pause delivery |
| oldest queue item > 10 minutes | pause delivery |
| credential or PII body in logs | immediate rollback, rotate affected secret and follow security incident process |

## Pause, resume and activation kill switch

Use an authorized operator identity and an audit reason:

```powershell
$headers = @{ "X-Operator-Id" = "operator-id" }
$body = @{
  deliveryPaused = $true
  apiContractActivationEnabled = $false
  reason = "Queue age exceeded the approved threshold"
} | ConvertTo-Json
Invoke-RestMethod -Method Put `
  -Uri "http://localhost:5181/delivery-operations/control" `
  -Headers $headers -ContentType "application/json" -Body $body
```

When paused, the scheduler creates no new delivery dispatch and a queued message returns its job to the durable queue without changing payload/hash/key. Resume by setting `deliveryPaused = false`; the scheduler continues due work with the original identity.

Disable one config without affecting others by updating `PUT /delivery-operations/rollouts/{configId}` with `enabled = false`. The control and rollout tables both keep audit rows.

## Canary and rollback rehearsal

1. Select one low-risk Mapping Config and partner; approve `Canary` for that config only.
2. Activate its ApiContract version for new files and observe one full release cycle.
3. Monitor delivery and business duplicate controls. Do not percentage-route inside the config.
4. Rehearse pause, then activate the previous LegacyTable version.
5. Verify new File Jobs pin the Legacy version and existing API File/Delivery Jobs retain their API version and remain paused or continue under the runbook decision.
6. Record the rollback duration and compensation owner. A delivered external side effect is not undone by config rollback.

## Retention

Terminal time resets payload expiry to 30 days and audit expiry to 365 days. The Worker runs hourly cleanup: it first nulls encrypted payload bytes after payload expiry, then removes attempts, resend audit and terminal Delivery Jobs after audit expiry. Stop the cleanup Worker during legal hold and record the exception; do not drop delivery tables during the retention window.

## Recovery checks

After resume or rollback verify:

- no pinned API job changed destination or Config Version;
- retries retained payload hash and idempotency key;
- new File Jobs use the active version;
- queue age decreases and no new critical alert appears;
- partner compensation is owned and audited for any already Delivered side effect.
