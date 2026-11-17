---
title: M2.7 — Fixed-width reader และ import
status: review
last_updated: 2026-11-17
---

# M2.7 — Fixed-width reader และ import

ข้อเสนอนี้ต่อ Fixed-width เข้ากับ [import core ของ M2.6](m2-6-delimited-import-design.md) ตาม M-29 โดยใช้ Source/Row Job/outbox/checkpoint transaction และ lease เดิม ไม่เพิ่มตารางหรือ migration

**สถานะ 2026-11-17:** ผู้ใช้ยืนยัน source batch แล้ว; reader/shared validator/preview/import/Source raw ทำและทดสอบแล้วตาม M-30 ส่วน fields/fixture ของ partner และ Q9 ยังรอ ไม่ติ๊กปิด M2.7 ทั้งข้อ

## พฤติกรรมที่เสนอ

- `kind=FixedWidth`, extension `.txt`; `start` เริ่ม 1 และ `length` นับตำแหน่งในข้อความหลัง decode ตาม encoding ตาม Fixed-width Slice ใน `CONTEXT.md` และ reader เดิม ไม่ใช่ byte offset
- เก็บ raw slice ตามต้นทาง รวมช่องว่าง/เลขศูนย์นำหน้า/ค่าที่เป็นข้อความผิดทางธุรกิจ; reader ไม่ trim หรือแปลงชนิดธุรกิจ
- ใช้ validator เดียวกันใน reader/validate/preview/import: field keys ไม่ซ้ำ, start/length เป็นบวก, ตรวจ endpoint ด้วยเลข 64-bit เพื่อกัน overflow, ranges ไม่ซ้อน และอยู่ใน limits
- รองรับ UTF-8/BOM และ cp874; short record, invalid encoding/characters/slice, limits และ cancellation มีผลชัดเจน ไม่มีการสร้าง Source ของไฟล์ที่ parse ไม่ผ่าน
- bounds ของ positions อิงข้อความ UTF-16 ของ .NET ที่ decode แล้ว; slice ที่ตัดคู่ surrogate กลางตัวอักษรต้องแจ้ง error แทนการส่งข้อความที่ encode กลับไม่ได้
- line terminators อยู่นอก slice; blank lines ไม่นับเป็น Source row แต่ยังนับ physical line; cells สำหรับ Fixed-width นับจำนวน slices ที่อ่านจาก data records
- import dispatch เลือก Delimited หรือ FixedWidth จาก pinned Input Layout; hash/size/lease/read count/atomic row writes/resume/recovery ใช้ส่วนกลางเดิม
- API routes/envelope เดิม; FixedWidth capability ยังปิดจน intake/queued processing พร้อม ตามสถานะจริงของ M3

## ตัวอย่างสมมติ

```json
{
  "kind": "FixedWidth", "extension": ".txt", "encoding": "utf-8",
  "fields": [
    {"key": "reference", "start": 1, "length": 4},
    {"key": "amount", "start": 6, "length": 6},
    {"key": "note", "start": 12, "length": 6}
  ]
}
```

แถวสมมติ `R001 000180ไทย   ` ได้ Source `{"reference":"R001","amount":"000180","note":"ไทย   "}` ตำแหน่ง `note` เริ่มที่ 12 ของข้อความที่ decode แล้ว แม้ข้อความไทยใน UTF-8 ใช้หลาย bytes; ถ้ามีแถวว่างคั่น แถวข้อมูลถัดไปยังเป็น source row 2 และ physical line 3

## ไฟล์ source ที่เพิ่ม/แก้แล้ว

ทุก path อยู่ภายใต้ `prjInsuranceValidationMapping`:

- เพิ่ม `src/Mapping/Mapping.Application/Input/FixedWidthInputLayoutValidator.cs`
- แก้ `src/Mapping/Mapping.Infrastructure/Files/FixedWidthInputReader.cs`
- แก้ `src/Mapping/Mapping.Application/Preview/PreviewService.cs`
- แก้ `src/Mapping/Mapping.Application/Imports/FileImportService.cs`
- แก้ `src/Mapping/Mapping.Application/Capabilities/Queries/GetCapabilitiesQuery.cs`
- เพิ่ม `tests/Mapping.IntegrationTests/Input/FixedWidthInputReaderTests.cs`
- เพิ่ม `tests/Mapping.IntegrationTests/Imports/FixedWidthFileImportTests.cs`
- แก้ `tests/Mapping.IntegrationTests/Imports/DelimitedFileImportTests.cs` เฉพาะ constructor ที่เพิ่ม reader อีกชนิดและ regression ที่เกี่ยวข้อง
- แก้ `tests/Mapping.IntegrationTests/Api/MappingPreviewApiTests.cs` สำหรับ Fixed-width validation/preview-file ที่ใช้กฎเดียวกับ import

## เกณฑ์ตรวจ

1. Reader: raw padding/zeroes/Thai, UTF-8/cp874/BOM, physical lines/blank lines, positions/overflow/overlap/short record, limits bytes/records/columns/field/cells, invalid encoding/characters/surrogate boundary และ cancellation
2. API: shared validation paths ของ validate/preview-file, preview เป็น read-only และ CSV/TSV/Excel เดิมยังผ่าน
3. PostgreSQL: Fixed-width preview/import ใช้ pinned reader/layout เดียวกัน; Source raw/Row Jobs/outbox/checkpoint ตรงกัน, resume/replay/concurrent calls/lease guards/hash/parse failures ไม่ทิ้งแถวหรือสร้างงานซ้ำ
4. `./scripts/check.ps1`: build, unit/integration regression และ EF model; บันทึกผลจริงใน `plan.md`

## ผลตรวจ 2026-11-17

`prjInsuranceValidationMapping` ผ่าน `./scripts/check.ps1` ด้วย .NET 10.0.404: build, unit **17/17**, integration **60/60** และ EF model check; ไม่มี migration ใหม่ ไม่มีการเปลี่ยน DB บริษัทหรือเขียน HTTP delivery

| หลักฐานใหม่ | จำนวน | สิ่งที่ตรวจ |
|---|---|---|
| `FixedWidthInputReaderTests` | 6 | UTF-8 BOM/cp874, decoded Thai positions, raw spaces/zero padding, field order/gaps, CR/LF/CRLF และ blank line positions; invalid/null/duplicate/overflow/overlap/short layout; bytes/records/columns/field/cells limits; invalid encoding/NUL/surrogate slice; cancellation/I/O failure |
| `FixedWidthFileImportTests` | 5 | PostgreSQL 15/physical Source จริง; preview/import ใช้ pinned layout เดียวกันแม้มี config version ใหม่; raw slices และ line positions ตรงกัน; cp874; Source/Row Job/outbox/checkpoint atomic; replay/resume/concurrent calls และ stale lease; malformed/limits/hash mismatch ไม่สร้างแถว; outbox insert ล้มเหลวแล้ว rollback และ resume ได้ |
| `MappingPreviewApiTests` เพิ่ม | 1 | UTF-8 ไทยก่อนช่วง field ถูกนับเป็นข้อความหลัง decode; overlap/overflow คืน code/path ร่วมกันใน validate และ preview-file; คง HTTP behavior เดิม |

tests ใหม่ **12 รายการ**; regression CSV/TSV, MTLS full fixture, Transformation Program และ Named Lookup ของ M2.5 ผ่านทั้งหมด ไม่เปิด capability จากผล synthetic tests เพียงอย่างเดียว

### ขอบเขต runtime หลัง M2.7

- `FileImportService` เลือก `IInputReader<DelimitedInputLayoutDto>` หรือ `IInputReader<FixedWidthInputLayoutDto>` จาก pinned `inputLayout.kind`; hash/size/lease/read count/row transaction เป็นทางเดียวกัน ไม่สร้าง Fixed-width writer อีกชุด
- preview กับ import ใช้ `FixedWidthInputLayoutValidator` และ `FixedWidthInputReader` ตัวเดียวกัน; invalid layout คืน diagnostics ก่อนเข้า slice/Source writer
- bounds ใช้เลข 64-bit ก่อนทำ `Substring`; คงช่องว่างตาม slice และไม่ตัด surrogate pair; record ที่สั้นคืน `fields.<key>` พร้อม physical record/line number และตำแหน่งเริ่ม field ใน `Position` ของ reader diagnostic
- Fixed-width cells budget นับ `fields.Count` ต่อ data record; blank lines นับเป็น physical lines แต่ไม่สร้าง Source และไม่นับ cells
- source commit ใช้ writer เดิมของ M2.6 พร้อม Row Job/outbox/checkpoint; tests ฉีด failure ใน outbox และยืนยัน rollback ทั้งชุด
- constructor ของ FileImportService รับ readers ทั้งสองชนิด; ใช้ DI registrations ที่มีอยู่แล้ว ปรับเฉพาะจุดสร้าง service ใน CSV/TSV tests ให้ตรง constructor
- FixedWidth capability ยังคง `available=false`; reason ระบุว่า reader/preview/sealed-snapshot import ใช้ได้แล้ว แต่ intake และ queued processing ยังรอ M3
- ยังไม่มี Excel import dispatch, scanner, Kafka dispatcher/consumer, transform worker หรือ archive ใน batch นี้; M2.3 ยังรอ Excel import และการเทียบ compiler/evaluator กับ transform worker ตาม M3.3 รวมถึง Q9

Q5/M-24 ยังรอ fields/fixture ของ partner; Q9 รอ Dev 1/PM ลงผล scope/วันส่ง จึงยังไม่ติ๊กปิด M2.7 ทั้งข้อจาก synthetic tests เพียงอย่างเดียว ไม่เดา business bindings เพิ่ม
