# Phase 25 Handoff

Phase 25 adds `JsonFile` as the third immutable destination. One File Job produces one deterministic JSON array artifact; it never writes a Normalized Table or creates an API Delivery Job. Gate E was approved on 2026-10-26, while runtime controls remain fail-closed until an operator explicitly opens them.

## Local acceptance walkthrough

Use only synthetic data. Start from a clean local state and do not run `generate-sample.ps1` before this walkthrough because duplicate detection is content-based.

1. Follow README setup through `reset-demo.ps1`, start API, run `configure-demo.ps1`, and start Web and Worker. Open `/configs`, select `orders-demo`, and note its config ID plus the active Legacy version number.
2. In Delivery, choose `Whole file · JSON file` with:
   - File destination profile: `local-json-export`
   - Contract: `proposal/1`
   - Relative folder: `shadow/orders`
   - File name template: `{sourceBaseName}.{artifactRevision}.json`
   - Formatting: `compact`
3. In contract mapping, map `proposal.insured.fullName` from `customer_name` and `proposal.insured.birthDate` from `order_date`. Under Array Group choose `proposal.beneficiaries`, sequence leaf `seq`, add source slot `1`, and map `customer_name` to relative item path `name`. System-bound `envelope.*` paths remain read-only.
4. Enter synthetic preview values such as `customer_name=Example Customer` and `order_date=2026-01-15`; click `Preview`. Verify the preview is one JSON array, reports object/byte counts, and states that it writes no file. Click `Create version` only after diagnostics are empty.
5. Open the approved local Shadow controls, replacing `<config-id>`:

   ```powershell
   pwsh ./scripts/configure-delivery-rollout.ps1 `
     -ConfigId <config-id> -Stage Shadow `
     -OperatorId "local-operator" `
     -Reason "Approved Gate E shadow verification" `
     -DisableApiActivation `
     -EnableJsonFileActivation `
     -ResumeJsonFileFinalizer
   ```

6. Activate the new JsonFile version in Version history. Create one valid synthetic input whose headers match the contract editor's generated layout:

   ```powershell
   @(
     "order_no,customer_name,order_date,amount,is_paid,note",
     "JF-001,Example Customer,2026-01-15,10.50,true,Synthetic acceptance row"
   ) | Set-Content -Encoding utf8NoBOM ./input/orders/json-ok.csv
   ```

7. Find the job and verify the published hash without reading the payload through the browser/API:

   ```powershell
   $jobs = Invoke-RestMethod http://localhost:5181/file-jobs
   $job = $jobs | Where-Object fileName -eq "json-ok.csv" | Sort-Object id -Descending | Select-Object -First 1
   $detail = Invoke-RestMethod "http://localhost:5181/file-jobs/$($job.id)"
   $history = Invoke-RestMethod "http://localhost:5181/json-exports?fileJobId=$($job.id)"
   $detail.export | Format-List status,relativeDisplayPath,objectCount,byteCount,sha256,publishedAt
   $history | Format-Table artifactRevision,status,relativeDisplayPath,sha256
   ```

   Expected: `Published`, object count `1`, a relative path only, and the current history SHA-256 equal to File Job metadata. The local artifact is under the server-owned `output/json` root; v1 deliberately has no download endpoint.

8. Simulate a whole-file block with a unique file containing an invalid required date:

   ```powershell
   @(
     "order_no,customer_name,order_date,amount,is_paid,note",
     "JF-BLOCK-001,Example Customer,2026-02-30,10.50,true,Synthetic invalid date"
   ) | Set-Content -Encoding utf8NoBOM ./input/orders/json-blocked.csv
   ```

   Expected: the File Job export becomes `Blocked`, the UI/API shows only a redacted diagnostic, and no final artifact exists for that job.

9. Rehearse rollback: disable new JSON activation, pause finalization, then activate the prior Legacy version for new files:

   ```powershell
   pwsh ./scripts/configure-delivery-rollout.ps1 `
     -ConfigId <config-id> -Stage Shadow `
     -OperatorId "local-operator" `
     -Reason "JSON rollback rehearsal" `
     -DisableApiActivation `
     -DisableJsonFileActivation `
     -PauseJsonFileFinalizer
   ```

   Submit a new uniquely valued file with the original Legacy headers. Verify it pins the Legacy version and writes Normalized rows, while `json-ok.csv` and `json-blocked.csv` retain their JsonFile Config Version, profile and artifact revision.

## Delivered files and seams

- Migrations `0011_json_file_destination.sql`, `0012_json_export_persistence.sql` and `0013_json_export_operations.sql` add destination shape, encrypted staging/export identity, controls, retry audit and operation events.
- Shared destination/contract modules own the discriminated union, schema snapshot, deterministic serialization and safe artifact path identity.
- Worker destination adapter, readiness coordinator, finalizer, publisher, recovery scheduler and cleanup service isolate row staging from file-level publication.
- API adds safe file-profile resolution, preview/status/history/retry, control/metrics/alerts and additive File Job export metadata.
- Web production Mapping workbench uses real capabilities/API/state for all three destinations. `/prototype/file-to-json` is decision evidence only and is not imported by production config components.

## Verification and known limits

The authoritative automated evidence is [phase-25-acceptance-matrix.md](phase-25-acceptance-matrix.md), with execution/sign-off in [phase-25-gate-e-evidence.md](phase-25-gate-e-evidence.md).

- Development profile paths are local-machine examples; production must inject server-owned profiles, ACLs, quotas and key references outside browser/config payloads.
- JsonFile activation is a global kill switch. Config-by-config rollout is an operator discipline enforced by activating only the approved Mapping Config version.
- v1 has no artifact download endpoint, HTTP batch delivery or dual delivery.
- Limits are 1 MiB per object, 320,000 objects and 256 MiB per artifact.
- Encrypted staged payloads and artifacts default to 30-day expiry. Audit expiry is recorded at 365 days, but deletion of audit rows requires a separately approved platform retention job.
- The UI's contract editor creates a UTF-8 CSV layout whose headers are Source column names. Preserve or deliberately recreate the prior Input Layout when switching destinations.
- Backup/restore must keep PostgreSQL, Output Root and encryption-key access at the same consistency point; see the operations runbook.

No commit is created by the agent. Review the complete dirty worktree, run the verification commands, and let the repository owner choose commit boundaries.
