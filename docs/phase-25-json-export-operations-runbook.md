# Phase 25 JSON Export Operations Runbook

This runbook covers metadata-only operations for `JsonFile`. Never paste payload bodies, credentials, absolute Output Roots, or source records into tickets, logs, metrics, or browser tools.

## Runtime controls

- `JsonFileActivationEnabled=false` blocks activation of new JSON Config Versions. It does not reroute pinned File Jobs.
- `JsonFileFinalizerPaused=true` prevents new claims. Pending/staged rows, artifact revision, relative path, formatting, profile, and hashes remain pinned.
- Resume finalization only after the triggering condition is resolved. Recovery will claim the same export identity; do not create a replacement job for a retry.
- Control and retry actions require an authorized operator ID plus a reason and are audited.

For an approved local Shadow run, enable only the intended Mapping Config and make both JSON state changes explicit:

```powershell
pwsh ./scripts/configure-delivery-rollout.ps1 `
  -ConfigId <config-id> -Stage Shadow `
  -OperatorId "local-operator" `
  -Reason "Approved Gate E shadow verification" `
  -DisableApiActivation `
  -EnableJsonFileActivation `
  -ResumeJsonFileFinalizer
```

`JsonFileActivationEnabled` is a global safety switch. JsonFile rollout remains config-by-config because Operations activates only the approved Mapping Config version; do not activate another JsonFile config merely because the switch is open. Read the authoritative state from `GET /delivery-operations` after every change.

## Alert response

| Signal | Immediate action | Safe investigation | Resume condition |
|---|---|---|---|
| `JsonExportQueueAge` (>10 minutes) | Pause JSON finalization if queue continues growing | Check finalizer control, worker/Kafka health and database connectivity using config/profile IDs only | Queue drains with the same job IDs and revisions |
| `JsonExportPublicationFailure` | Keep activation disabled; pause if failures repeat | Check volume free space, profile quota and service-account permissions; use only the relative display path | A retry succeeds with the same relative path/hash identity |
| `JsonExportCleanupFailure` | Do not remove audit rows manually | Check legal hold, volume availability and delete permission; never expose the absolute root in a ticket | Expired payload/artifact metadata shows purge/delete timestamps and the failure count stops increasing |
| `JsonExportHashConflict` | Pause finalization immediately | Preserve both bytes, verify the expected SHA-256 and investigate ownership/collision using job ID and relative path | Owner approves remediation; conflicting final file is never overwritten in place |
| `JsonExportBlocked` | Keep the job blocked | Inspect the redacted diagnostic and latest Row Job summary | Reprocess creates a new audited revision, or an approved retry uses the same identity where policy permits |

## Security checks

- The API and UI expose relative display paths, counts, sizes, hashes and timestamps only.
- v1 has no HTTP/browser download endpoint. Artifact access remains under Output Root ACLs for authorized service accounts and downstream consumers.
- Traversal, absolute paths and reparse-point traversal are rejected. Publish is create-no-overwrite and checks profile quota plus minimum free space before rename.
- Cleanup honors legal hold and records a redacted `CleanupFailure` event when storage deletion fails.

## Retention and cleanup

| Data | Default lifecycle | Cleanup behavior |
|---|---|---|
| encrypted staged JSON objects | 30 days after a terminal finalization outcome | ciphertext, nonce, tag and key reference are nulled; metadata/hash identity remains |
| published JSON artifact | 30 days after publish | artifact is deleted under the pinned Output Root and `artifact_deleted_at` is recorded |
| export/audit metadata | expiry recorded at 365 days | current worker retains the rows; production must use an approved audit-retention job before deleting them |

The cleanup worker scans hourly in batches of 100 and skips every job under legal hold. A hold requires non-empty reason, owner and expiry. Never bypass a hold or manually null encrypted payload columns. Resolve `JsonExportCleanupFailure` by fixing the pinned profile/volume permissions, then allow the same cleanup scan to retry.

## Backup and restore

A database dump alone is not a valid JSON export backup. The consistency set is PostgreSQL plus the pinned Output Root and access to every referenced encryption key. Production should use platform snapshots; the commands below are only a local-demo rehearsal.

1. Disable new JsonFile activation, pause the finalizer, wait for active finalizer leases to end, then stop API and Worker.
2. Create a database dump inside the PostgreSQL container and copy it out:

   ```powershell
   New-Item -ItemType Directory -Force ./backup/phase25 | Out-Null
   docker compose exec -T postgres pg_dump -U mapping -d mapping -Fc -f /tmp/mapping-phase25.dump
   docker compose cp postgres:/tmp/mapping-phase25.dump ./backup/phase25/mapping.dump
   Copy-Item -Recurse ./output/json ./backup/phase25/json
   ```

3. Record the backup time, database migration level, profile IDs and encryption-key references. Do not put key material or artifact contents in the manifest.
4. Restore only into an isolated environment first. Keep API/Worker stopped, copy the dump into the container, restore the matching Output Root snapshot, and restore access to the referenced keys:

   ```powershell
   docker compose cp ./backup/phase25/mapping.dump postgres:/tmp/mapping-phase25.dump
   docker compose exec -T postgres pg_restore -U mapping -d mapping --clean --if-exists /tmp/mapping-phase25.dump
   Copy-Item -Recurse -Force ./backup/phase25/json ./output/json
   ```

5. Start API with the finalizer still paused. Compare `GET /json-exports?fileJobId=<id>` metadata to restored files using relative path, byte count and SHA-256. Resume the Worker/finalizer only after the sample matches. If DB and storage snapshots are from different consistency points, keep finalization paused and restore a matching pair; never overwrite a conflicting final file.

## Rollback rehearsal

1. Disable new JSON activation and pause the JSON finalizer:

   ```powershell
   pwsh ./scripts/configure-delivery-rollout.ps1 `
     -ConfigId <config-id> -Stage Shadow `
     -OperatorId "local-operator" `
     -Reason "JSON rollback rehearsal" `
     -DisableApiActivation `
     -DisableJsonFileActivation `
     -PauseJsonFileFinalizer
   ```

2. Activate the prior Config Version only for new File Jobs.
3. Verify existing JSON File Jobs retain their config version, profile, artifact revision, relative path and staged payload identity.
4. Resolve the incident, record the reason, resume the finalizer, and verify recovery publishes or blocks each original identity without rerouting.

Do not delete or rewrite a staged/export job to make rollback look clean. Pinned jobs remain `JsonFile`; the rollback changes only which immutable version future files pin.
