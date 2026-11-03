# Phase 22 Workbook Reader Decision

Date: 2026-10-20 (Asia/Bangkok)  
Decision: use `ExcelDataReader` 3.9.0 behind `InputRecordModule`.

## Decision

The production reader is `ExcelDataReader` 3.9.0 with
`System.Text.Encoding.CodePages` 8.0.0 for legacy BIFF code pages. Both package
versions are pinned in `MappingDemo.Shared`. The dependency is read-only and
its types do not cross the Input Record Module boundary.

It was selected because one low-level, forward-only API reads Open XML `.xlsx`
and binary BIFF `.xls`, is MIT-licensed, and supports single-pass mode. ClosedXML
was not selected because it does not read `.xls`. NPOI was not selected because
the larger read/write API is unnecessary here and current binary distributions
have an additional maintenance-fee EULA.

## Frozen workbook semantics

- Sheet names and headers match ordinally and case-sensitively.
- `headerRow`, `dataStartRow`, physical worksheet rows, and error columns are
  one-based. Imported `rowNumber` is contiguous and excludes skipped blank rows.
- Strings are unchanged; booleans are lowercase `true`/`false`; numeric CLR
  values use invariant round-trip text; CLR dates use `yyyy-MM-dd` at midnight
  or `yyyy-MM-ddTHH:mm:ss.fffffff`; ISO Open XML date cells returned as strings
  remain exact ISO source text.
- Formula expressions are never evaluated. The reader consumes only the cached
  value exposed by the workbook; a missing cached value is an empty cell.
- Excel error cells reject the entire file with `WorkbookCellError`. Merged
  cells follow workbook storage: only the anchor cell contains a value.
- Structural failures are accumulated before Source persistence begins, so a
  rejected workbook produces no Source Row, Row Job, or row-normalize outbox.

Stable boundary codes include `InvalidWorkbookSignature`,
`WorkbookSignatureMismatch`, `EncryptedWorkbook`, `CorruptWorkbook`,
`WorkbookSheetNotFound`, `WorkbookHeaderRowNotFound`,
`DuplicateWorkbookHeader`, `RequiredWorkbookHeaderMissing`,
`WorkbookCellError`, `BlankWorkbookRow`, `WorkbookTooLarge`,
`TooManyWorkbookRows`, `TooManyWorkbookColumns`, `CellTooLong`, and
`WorkbookReadTimeLimitExceeded`. Library exception messages are not returned.

## Limits

Worker defaults: 10 MiB, 10,000 imported rows, 256 columns, and 32,768
characters per cell. Multipart preview: 10 MiB, 50 imported rows, 256 columns,
32,768 characters per cell, and 10 seconds. Preview streams into the same Input
Record Module and never persists the uploaded file.

