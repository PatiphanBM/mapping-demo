# Phase 22 Gate B — File-to-Source Handoff

Date: 2026-10-20 (Asia/Bangkok)  
Decision: **PASS — engineering sign-off**

## Delivered boundary

Config Versions now persist normalized `acceptedExtensions` and an immutable
Input Layout. The watcher derives candidates from the active version, disables
ambiguous folder/extension ownership, and File Intake pins the detected Config
Version. `InputRecordModule` remains the single parser used by Worker and API
preview; ExcelDataReader stays internal. Phase 22 ends at the existing Source,
Row Job, transformation, and `LegacyTable` path and introduces no partner API
side effect.

## Gate evidence

| Check | Result |
|---|---|
| Golden workbook matrix | `.xlsx` and BIFF `.xls` passed with multi-sheet/Thai sheet, non-first header, separate data start, blank row, string/number/boolean/date/time/cached formula/error/merged-cell coverage |
| Actual PostgreSQL FileImportHandler matrix | `.txt`, `.csv`, `.xlsx`, `.xls` created 5 Source Rows, 5 Row Jobs, and 5 row-normalize outbox messages |
| Failure atomicity | Corrupt `.xlsx` ended `ImportFailed` with 0 Source Rows; error-cell and structural failures return no records |
| Rollback rehearsal | Activating the prior delimited version rejects a new `.xlsx` with `FileExtensionNotAccepted` and 0 Source Rows |
| Version pinning | Intake passes the detected version ID into File Job creation; later activation cannot change the job layout/extensions |
| Legacy parity and migrations | Full PostgreSQL suite passed 270/270, including destination adapter parity and migration 0008 backfill |
| Preview | Multipart endpoint uses shared parser, signature sniffing, limits, and returns sheet/header/source metadata without persistence |
| Web | ESLint and Next.js 16.4.0 production build passed; browser sends binary file and never parses Excel as source of truth |
| Formatting | `dotnet format --verify-no-changes --no-restore` passed before final documentation pass |

## Rollback and operations

Rollback is version activation, not schema removal: activate a known delimited
or fixed-width Config Version whose accepted extensions exclude Excel. New
Excel candidates are then ignored by the watcher; already-created File Jobs keep
their pinned version. Migration 0008 is expand-only and existing versions are
backfilled with `[".txt", ".csv"]` to preserve prior behavior.

Monitor import-status distribution and stable input error codes. Limits and the
exact cell policy are recorded in `phase-22-workbook-reader-decision.md`.
PostgreSQL was stopped after verification; its named volume was preserved.

