# Phase 19 Backend Config Handoff

Backend contract พร้อมเป็นฐานให้ Phase 20 สร้างหน้าเว็บตั้งค่า Mapping Config โดยไม่ต้องรู้จัก legacy payload หรือเรียก parser/evaluator คนละชุดกับ Worker

## Contract ที่ส่งต่อ

Config Version เป็น immutable pair ของ:

- `inputLayout`: `delimited` หรือ `fixedWidth`
- `transformationProgram`: typed/versioned AST ที่ใช้ `sqlserver-legacy-v1`

API, preview และ Worker เรียก `InputRecordModule` กับ `TransformationModule` ชุดเดียวกัน ไม่มี runtime adapter สำหรับ `fileToSource`, `sourceToNormalized`, `CsvRecordReader` หรือ `RowNormalizer`

Endpoint ที่หน้าเว็บใช้:

| งาน | Endpoint |
|---|---|
| capability catalog | `GET /mapping-capabilities` |
| validate draft | `POST /mapping-configs/{id}/versions/validate` |
| preview draft | `POST /mapping-configs/{id}/versions/preview` |
| create version | `POST /mapping-configs/{id}/versions` |
| activate version | `POST /mapping-configs/{id}/versions/{version}/activate` |

Capability identifiers เป็น contract แบบมีรุ่น ห้ามเปลี่ยน behavior ของ identifier เดิมแบบเงียบ ๆ หน้าเว็บต้องอ่าน catalog แทนการ hard-code format, operator, function หรือ lookup

## Runtime invariants

- File Job pin Config Version ตอนรับไฟล์; import และ initial normalization ใช้ version เดียวกัน
- Reprocess อ่าน Source row เดิม จึงใช้ Transformation Program ของ version ที่เลือก แต่ไม่อ่านไฟล์ใหม่ด้วย Input Layout ใหม่
- Fixed-width positions เป็น 1-based UTF-16 code units หลัง strict UTF-8 decode; Source เก็บ raw slice รวม padding
- Transformation หนึ่ง Source row ให้ผล `0..N` outputs ที่มี stable `outputKey`
- reprocess ที่สำเร็จ reconcile output set ทั้งชุด; `Filtered` ลบ outputs เดิม ส่วน `Invalid`/`Failed` รักษา Current Normalized Result เดิม
- named lookup ตัดเฉพาะผลของ Source row ปัจจุบันออกจาก anti-lookup เพื่อไม่ให้ retry/reprocess block ตัวเอง
- preview ทำงานใน memory, ไม่เขียน database/disk และไม่เปิด `fileName` เป็น path

## Preview limits

- `evaluationAt` ต้องเป็น ISO 8601 ที่มี `Z` หรือ UTC offset และ response echo instant ที่ใช้จริง
- UTF-8 text content ไม่เกิน 1,048,576 bytes
- parsed records หรือ sample Source rows ไม่เกิน 50
- request body ไม่เกิน 3 MiB และคืน HTTP 413 เมื่อเกิน
- request ที่เกิน limit ถูก reject ทั้งก้อน ไม่ truncate

## Coverage gate

[phase-19-golden-cases.md](phase-19-golden-cases.md) เป็น source of truth ของ semantics และ coverage matrix โดยใช้ fixtures ขนาดเล็กแทนการคัดลอกจำนวน rows/fields จาก workbook หลักฐานสำคัญประกอบด้วย:

- direct mapping, implicit trim/conversion, nested CASE, substring/convert, current date และ `formatRegNo@1`
- compact fixed-width `H/D/D/T`, multi-header/trailer, sequence/cardinality และ generated 115-field capacity case
- anti-lookup not found, other-source, self-only และ same-loan/different-product
- Worker/PostgreSQL integration สำหรับ retry, error paths, multi-output และ reprocess `2→1→0`
- direct CSV runtime acceptance: files 100/100 rows ให้ผล 98 Done + 2 Invalid และ 100 Done

รัน regression gate:

```powershell
$env:MAPPING_POSTGRES_INTEGRATION = "Host=localhost;Port=5432;Database=mapping;Username=mapping;Password=mapping"
dotnet test MappingDemo.sln --no-restore
dotnet format MappingDemo.sln --no-restore --verify-no-changes
```

## Clean reset

Phase 19 เป็น breaking cutover ของ demo หากฐานข้อมูลสร้างด้วย contract เก่า ให้หยุด API/Worker แล้วรัน:

```powershell
pwsh ./scripts/reset-demo.ps1
dotnet run --project src/MappingDemo.Api --launch-profile http
pwsh ./scripts/configure-demo.ps1
```

Rollback คือย้อน code ไป revision ก่อน Phase 19 แล้ว reset/reconfigure ใหม่ ไม่ใช่ migrate Config Version เก่ากลับ
