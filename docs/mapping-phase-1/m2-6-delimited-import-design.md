---
title: M2.6 — CSV/TSV reader และแกน import
status: review
last_updated: 2026-11-17
---

# M2.6 — CSV/TSV reader และแกน import

ข้อเสนอนี้ต่อจาก Delimited reader/validate/preview ที่มีแล้ว โดยใช้ M-06/M-17/M-24/M-25 และ transaction boundary ใน [data/runtime](data-runtime-design.md) §6/§8 ไม่เปิดเส้นทางบันทึก Source ที่ข้าม Row Job/outbox

**สถานะ 2026-11-17:** ผู้ใช้ยืนยัน source batch แล้ว; ส่วน reader/preview/import core/Source persistence ทำและทดสอบแล้วตาม M-29 ส่วน partner acceptance และ Q9 ยังรอ ไม่ปิด M2.6 ทั้งข้อ

## ผลที่ต้องได้

1. CSV/TSV อ่าน delimiter, quote, header, encoding และ field bindings จาก pinned Input Layout; ใช้ reader ตัวเดียวกับ preview
2. รักษาข้อความภายใน field รวมช่องว่าง/เลขศูนย์นำหน้า/CRLF ใน quoted field; แยกการ decode/แกะ quote ออกจาก business conversion
3. ตรวจ layout และ limits ก่อน/ระหว่างอ่าน: bytes, records, columns, field characters และ cells; headerless binding เป็นตำแหน่งเริ่ม 1; diagnostic มี field path และตำแหน่ง record
4. แกน import ทำงานกับ File Job ที่มี snapshot/hash และ pinned version อยู่แล้ว; ตรวจขนาด/hash ของ input ให้ตรงหลักฐานก่อนเขียน Source; ไม่อ่าน latest config แทน version เดิม
5. ต่อแถว commit registry + physical Source + Row Job + transform outbox + checkpoint ใน transaction เดียว; import ซ้ำ/resume ไม่เพิ่มแถวหรือ event ซ้ำ และ concurrent caller ไม่แย่ง lease
6. malformed file ไม่ถูกอ้างว่า import สำเร็จ; cancellation/restart คงข้อมูลที่ commit แล้วและ resume จาก checkpoint ได้

## ตัวอย่างข้อมูลสมมติ

Pinned layout ของ CSV:

```json
{
  "kind": "Delimited",
  "extension": ".csv",
  "encoding": "utf-8",
  "delimiter": ",",
  "quote": "\"",
  "hasHeader": true,
  "fields": [
    {"key": "reference", "header": "reference"},
    {"key": "amount", "header": "amount"},
    {"key": "note", "header": "note"}
  ]
}
```

```csv
reference,amount,note
R001,000180,"สมมติ, หนึ่ง"

R002,-293,"ข้อความสองบรรทัด
บรรทัดต่อเนื่อง"
```

| Source row number | Physical line เริ่มต้น | Raw fields |
|---|---|---|
| 1 | 2 | `{"reference":"R001","amount":"000180","note":"สมมติ, หนึ่ง"}` |
| 2 | 4 | `{"reference":"R002","amount":"-293","note":"ข้อความสองบรรทัด\nบรรทัดต่อเนื่อง"}` |

ถ้าไฟล์ใช้ CRLF ภายใน quote ให้ Source เก็บ `\r\n` ตามต้นทาง; ไม่เปลี่ยนเป็น LF ข้อมูล `-293` ยังเป็นข้อความดิบ ไม่แปลงที่ reader ตัวอย่าง TSV ใช้ fields เดียวกันแต่เปลี่ยน extension/delimiter เป็น `.tsv`/`\t`; แบบไม่มี header ให้ `header` เป็น `"1"`, `"2"`, `"3"`

ต่อ Source ที่ commit แล้วมี Row Job state `Pending` หนึ่งรายการ และ outbox หนึ่งรายการที่มีเฉพาะ job IDs/version ไม่มี raw values หรือ PII; Kafka topic ที่ใช้ใน local เป็น configuration ไม่ถือว่าตกลง topic บริษัทใน Q7 แล้ว

## ส่วนที่ใช้ร่วมกับ M3

- Application `FileImportService` และ repository boundary สำหรับอ่าน pinned job/context, claim/ตรวจ lease, commit row, บันทึก completion/diagnostics; เริ่มด้วย Delimited input ไม่สร้าง CSV-specific API/controller
- Domain `RowJobs` และ `OutboxMessages`/`TransformMessage`; outbox ใช้รูปแบบของ Informs (identity สำหรับลำดับ event, JSON payload, sent timestamp) โดย Mapping เป็นเจ้าของตารางเอง
- EF configurations/migration เพิ่มเฉพาะ schema `mapping`; ปรับ Source writer ให้สร้าง Row Job/outbox ร่วมกับ raw row และ checkpoint เสมอ; เก็บความคืบหน้า/ผล import ใน File Job
- import core รับ snapshot stream ที่ชั้นรับไฟล์เตรียมแล้ว; directory scanner, filesystem snapshot/archive, Kafka dispatcher/consumer และ transformation worker เป็นงาน M3 แยกต่อ ไม่ประกาศว่าทำงานครบจากการมี outbox ใน DB
- capability ของ Delimited ยังไม่เปิดใช้งาน production จน intake/processing ที่ต้องใช้พร้อม; ระบุ reason ตาม implementation จริง

## หลักฐานที่จะใช้

- reader tests: CSV/TSV/custom delimiter/quote, headerless/reordered headers, UTF-8/cp874, BOM, quote escape/multiline/CRLF, missing/duplicate header, malformed/short record, limits และ cancellation
- PostgreSQL tests: import CSV/TSV ผ่าน reader จริง; raw text/line numbers/pins ตรงไฟล์; Row Job/outbox/checkpoint ครบ; resume/concurrency/rollback/hash mismatch ไม่สร้างหลักฐานซ้ำหรือขาด
- API tests: validate และ preview-file ใช้ layout/diagnostics เดียวกัน; preview ไม่เปลี่ยน Source/jobs/outbox; regression Excel/Fixed-width และ M2.5 ต้องยังผ่าน
- `./scripts/check.ps1` ผ่าน build/tests/EF model; บันทึกผลจริงและไฟล์ที่แก้ใน `plan.md`

## ผลตรวจ 2026-11-17

`prjInsuranceValidationMapping` ผ่าน `./scripts/check.ps1` ด้วย .NET 10.0.404: build, unit **17/17**, integration **48/48** และ EF model check; tests เดิมของ MTLS full fixture และ M2.5 ยังผ่าน ไม่มีการ apply migration/แก้ DB บริษัท

| หลักฐานใหม่ | จำนวน | สิ่งที่ตรวจจริง |
|---|---|---|
| `DelimitedInputReaderTests` | 6 | CSV/TSV/custom delimiter/quote, reordered/headerless bindings, UTF-8 BOM/cp874, raw text/zero padding/CRLF, malformed/short/header errors, bytes/records/columns/field/cells limits, NUL ที่ PostgreSQL text เก็บไม่ได้ และ cancellation |
| `DelimitedFileImportTests` | 9 | PostgreSQL 15 + physical Source จริง, preview กับ pinned version เดียวกัน, version ใหม่ไม่เปลี่ยนงานเก่า, CSV/TSV/cp874, empty/raw/multiline, atomic outbox failure rollback, cancellation หลัง commit แรกแล้ว resume ด้วย Source ID เดิม, race/lease epoch/replay guard, hash mismatch/malformed/limits, legacy checkpoint ไม่มี Row Job และ immutable Row Job/outbox payload |
| `MappingPreviewApiTests` เพิ่ม | 1 | headerless preview-file ใช้ได้, validate/preview-file คืน diagnostic paths จาก shared validator; คง HTTP behavior เดิม (`validate` 200 พร้อม valid=false, invalid file preview 400) |

API read-only preview test เดิมเพิ่มการตรวจจำนวน `RowJobs`/outbox ว่าไม่เปลี่ยน; migration test ตรวจ **16 ตาราง** ใน schema `mapping` รวม EF history และ `public` ยังไม่มีตาราง

### Runtime ที่ใช้ได้ในรอบนี้

- `FileImportService.ImportAsync(fileJobId, snapshot, cancellationToken)` เป็น entry point ภายใน service; ทำงานกับ sealed File Job และเริ่มด้วย `inputLayout.kind=Delimited` ใน M2.6; [M2.7](m2-7-fixed-width-import-design.md) เพิ่ม `FixedWidth` ใน entry point เดียวกันแล้ว ยังไม่มี import controller หรือ scanner มาเรียก
- ตรวจ hash/size ของ snapshot ก่อนบันทึก Source; ใช้ `InputReadLimitsDto` แบบไม่อนุญาต truncate สำหรับ import; preview คง limit ของตัวเองและไม่สร้าง job
- claim/renew/finish ใช้ row lock และเวลา DB; owner+epoch ที่เก่าหรือหมดอายุเขียนต่อไม่ได้ การอ่าน stream ที่ช้าจน lease หมดจะไม่สามารถ renew/commit เป็นเจ้าของเดิมได้ และต้อง claim/resume ใหม่
- `ReadCount` บันทึกจำนวน data records หลัง parse สำเร็จ ส่วน `CommittedCount`/checkpoint เพิ่มตาม transaction ของแต่ละแถว เช่นอ่านพบ 2 แต่ยกเลิกหลังแถวแรกได้ read=2, committed=1 และ resume ต่อแถว 2
- registry/Row Job count ต้องตรง checkpoint ก่อน claim; หาก Source เก่าจากก่อน migration ไม่มี Row Job คืน `RecoveryRequired` โดยไม่ซ่อม/สร้าง outbox ย้อนหลังเอง ผลนี้ต้องนำไปจัดการใน recovery/operations ของ M3 ต่อ
- Row Job และ outbox เริ่มต้นต่อ Source สร้างพร้อม raw row/checkpoint; replay เดิมต้องตรง physical line และ raw fields; outbox ถูกบันทึกแต่ยังไม่ publish/mark sent ในงานนี้
- layout/file/limit/hash ที่ใช้ต่อไม่ได้บันทึก `ImportDiagnosticsJson` และ state `Blocked`; system/database exception ย้อนกลับ transaction แล้วส่ง exception ให้ caller, cancellation ส่งต่อและปล่อย lease ของผู้ถือเดิม
- จำกัด cells รวม header และ blank records ที่ parser พบ; `PhysicalRecordNumber` ของ diagnostic เป็นลำดับ record รวม header/blank ไม่ใช่ physical line ซึ่งต่างกันเมื่อ quote มีหลายบรรทัด
- configuration section `FileImport`: `MaxBytes`, `MaxRecords`, `MaxCells`, `LeaseSeconds`, `HeartbeatSeconds`, `TransformTopic`; default ตาม M-29 และรับผ่าน environment ได้ เช่น `FileImport__MaxRecords` โดยไม่อ้าง path ของ workspace เอกสารจากโค้ด

### ไฟล์ที่เพิ่ม/แก้ใน repo Mapping

| กลุ่ม | ไฟล์ |
|---|---|
| Reader/preview/capability | เพิ่ม `Application/Input/DelimitedInputLayoutValidator.cs`; แก้ `Infrastructure/Files/DelimitedInputReader.cs`, `Application/Preview/PreviewService.cs`, `Application/Capabilities/Queries/GetCapabilitiesQuery.cs` |
| Import | เพิ่ม `Application/Imports/FileImportService.cs`, `FileImportOptions.cs`, `DTOs/FileImportResultDto.cs`, `Domain/Repositories/FileJobs/IFileImportRepository.cs`, `Infrastructure/Repositories/FileImportRepository.cs` |
| Durable jobs | เพิ่ม `Domain/Entities/RowJobs/RowJobs.cs`, `Domain/Entities/Outbox/{OutboxMessages,TransformMessage}.cs`; แก้ `Domain/Entities/FileJobs/FileJobs.cs`, `Domain/Repositories/SourceDefinitions/ISourceStorageRepository.cs`, `Infrastructure/Repositories/SourceStorageRepository.cs` |
| EF/DI | เพิ่ม configurations ของ RowJobs/Outbox; แก้ FileJobs configuration, `MappingDbContext.cs` และ DI ของ Application/Infrastructure; เพิ่ม `20261117024834_AddFileImportJobs.cs`/Designer และอัปเดต model snapshot |
| Tests | เพิ่ม `Input/DelimitedInputReaderTests.cs`, `Imports/DelimitedFileImportTests.cs`; แก้ `Api/MappingPreviewApiTests.cs`, `Database/MappingCatalogTests.cs` ใน `Mapping.IntegrationTests` |

## สิ่งที่ยังรอรับตรวจ

Q5/M-24 ยังรอ fields/fixture ของ partner ที่ใช้ Delimited; Q9 รอ Dev 1/PM ลงขอบเขต/วันส่ง; ไม่สร้าง business bindings เอง ไม่ติ๊กปิด M2.6 ทั้งข้อจนมีหลักฐานตรง acceptance ที่เหลือ
