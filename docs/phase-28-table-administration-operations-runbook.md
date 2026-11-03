# Phase 28 Table Administration Operations Runbook

This runbook covers create, additive column change, verification, drift response, rollback and recovery. Authoritative metadata is `table_definitions`/`table_columns`; verification never repairs physical schema or reads business rows.

## Access and rollout controls

- Read/detail/verify: `TableAdmin.Read`.
- Create: `TableAdmin.Create`, actor, reason and correlation ID.
- Add column: `TableAdmin.AddColumn`, actor, reason and correlation ID.
- Mutations additionally require `TableAdministration:MutationsEnabled=true`.
- Gate H is approved. Production keeps `MutationsEnabled=false` until a separate controlled rollout change; Gate approval does not toggle it.
- Verify limit: 12 requests per table per minute; catalog timeout: five seconds.

## Create table

1. Open `/admin/tables`, select **New table**, and enter a PostgreSQL-safe lower-case name.
2. Select Source or Normalized. Source fields are locked to optional Text. For Normalized, review type, length, precision, scale and mapping-required metadata.
3. Supply operator/change-ticket reason and create.
4. Confirm redirect to `/admin/tables/{id}` and a fresh verification result.
5. Continue only when status is Healthy. A 409 means the name won a concurrent create; refresh rather than renaming an existing physical object.

Create commits metadata, DDL and success audit together. Failure rolls all three back.

## Add a column

1. Review **Configuration impact**, including active/historical versions and pinned job count.
2. Confirm the change is additive and older Config Versions tolerate the new nullable physical column.
3. Select **Add column**, enter one typed column, operator and reason.
4. After 204, the UI reloads metadata and verifies. Confirm Healthy before using the column in a new Config Version.
5. A 409 is a duplicate/concurrent name; retain form values, refresh and reconcile.

Never use this flow to rename/drop/change type or length. Create a separately approved migration/restore plan for those operations.

## Verification outcomes

| Status | Meaning | Operator action |
|---|---|---|
| Healthy | Metadata, columns and expected constraints match | Continue; record checked time |
| Drifted | Extra/missing/changed column or constraint | Pause table mutation/use expansion; assess impact and backup |
| Missing | Metadata exists but physical relation does not | Pause affected intake; restore from approved backup or rebuild under incident plan |
| VerificationFailed | Catalog permission, timeout or availability problem | Keep mutations paused; restore catalog access and retry |
| Unknown/Verifying | No current result or inspection in progress | Wait/retry within rate limit; do not infer health |

Diagnostics are structural only. Do not paste row data, connection strings, catalog SQL or credentials into tickets.

## Drift response and recovery

1. Disable mutations with `TableAdministration:MutationsEnabled=false`; read/verify stays available.
2. Identify referenced Config Versions and pinned File Jobs on detail.
3. Stop new intake for affected configs if runtime safety is uncertain. Do not rewrite pinned jobs.
4. Capture database backup and catalog evidence outside application logs.
5. Classify drift:
   - unexpected additive object: determine owner and compatibility;
   - missing/changed object: restore approved physical schema/data;
   - metadata error: use a separately reviewed metadata/data migration.
6. Re-run Verify. Resume controlled mutation rollout only after Healthy and incident approval.

There is no auto-repair endpoint. Never edit Published Contract snapshots or immutable Mapping Config Versions to mask table drift.

## Metadata/DDL failure

Create/add-column uses one PostgreSQL transaction. On failure, confirm no partial metadata, physical object or success audit exists. If PostgreSQL outcome is uncertain, keep mutations disabled and verify both metadata and catalog before retrying.

## Rollback

- Application rollback: set `MutationsEnabled=false`; retain read/detail/verify.
- Additive column rollback: do not drop automatically. Stop referencing the column in new drafts and follow impact/backup approval for any physical reversal.
- Create rollback: unused erroneous tables require a separate DBA-approved removal workflow; v1 intentionally has no hard-delete endpoint.
- Configuration rollback affects new files only; historical/pinned jobs remain unchanged.

## Observability and alerts

Meter: `MappingDemo.TableAdministration`.

- `table_admin.mutation` — bounded `create|add_column` and `success|failure` labels.
- `table_admin.verification` — bounded status label.
- `table_admin.verification_duration` — milliseconds.
- `table_admin.verification_rate_limited` and `table_admin.unauthorized_mutation`.

Alerts:

- `TableSchemaDriftDetected`: Drifted or Missing observed; pause and follow drift response.
- `RepeatedTableVerificationFailure`: three consecutive catalog failures; inspect access/latency.
- `UnauthorizedTableMutationAttempt`: review identity and audit.

Metrics never label table/column/operator/reason. Operations snapshot is `GET /admin/table-operations` with `TableAdmin.Read`.

## Rollout checklist

1. Gate H owner sign-off recorded.
2. Backup/restore and kill-switch rehearsal completed.
3. Start with approved non-critical tables; verify create/add-column and audit.
4. Monitor failures, duration, rate limit and unauthorized attempts.
5. Expand only while verification remains Healthy. Keep production flag off if any acceptance evidence is stale.
