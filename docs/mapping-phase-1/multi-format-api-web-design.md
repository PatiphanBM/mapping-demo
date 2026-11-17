---
title: แบบ Mapping API และหน้าเว็บสำหรับหลายรูปแบบไฟล์
status: draft
last_updated: 2026-11-17
---

# แบบ Mapping API และหน้าเว็บสำหรับหลายรูปแบบไฟล์

ผู้ใช้ชี้แจงวันที่ 2026-11-16 ว่า MTLS เป็น **ตัวอย่างหนึ่ง** ของงานเข้า ไม่ใช่โครงหลักของ Mapping; รอบแรกต้องรองรับ **Excel, Delimited (CSV/TSV) และ Fixed-width** จริง และ **partner อื่นจะเปิดใช้ใน UAT** แบบนี้ขยาย [API contract](mapping-api-contract.md) และ [data/runtime](data-runtime-design.md) โดยยังส่ง `pa-inform/1` เข้า Informs ตาม M-21; การแก้ D-01/D-70 และวันส่งอยู่ใน Q9 ให้ Dev 1/PM ลงผล

## 1. หน่วยที่ผู้ใช้ตั้งค่า

หนึ่ง **Mapping Config** มี input route, Source Definition, Input Layout, Transformation Program, contract/profile snapshot และ version ที่ activate ได้ ไฟล์แต่ละงานเลือก config จาก path/extension ที่ active แล้ว pin version ก่อนอ่าน; UI และ API ไม่ผูกกับชื่อ MTLS, จำนวน 19 columns หรือ business field ของ partner ใด

| ส่วน | ใช้ร่วมทุก format | ต่างกันตาม format |
|---|---|---|
| Config/Version | identity, version, expected row version, validate/preview/activate/disable | `inputLayout.kind` และ options ของ adapter |
| Source | physical Source Table ต่อ config, raw columns เป็น `text NULL`, immutable rows และ definition version | วิธีจับตำแหน่ง/ชื่อ field จาก record |
| Processing | File Job → Source → Row Job → Delivery Job → Informs; status/attempts/counts เดียวกัน | parser และ diagnostics ของไฟล์ |
| Web | รายการ config, ฟอร์ม fields/mapping, preview, งาน/แถว/ผลส่ง | เฉพาะ panel Input Layout ที่เปลี่ยนตาม `kind` |

`GET /api/mapping/capabilities` คืน format/operator/limit ที่ runtime **ทำงานจริง** พร้อม `available` และ `reason`; UI แสดงเฉพาะ format ที่เปิดใช้ได้จริง ห้ามแสดง Excel/Delimited/Fixed-width ว่าพร้อมเพียงเพราะมีตัวเลือกใน form หรือ enum

## 2. Input Layout แบบ discriminated union

`inputLayout.kind` เป็น discriminator; validator ปฏิเสธ option ของอีก kind ด้วย field path ที่ชัดเจน แยก validation ของไฟล์กับ validation ของ Transformation Program; adapter แต่ละตัวคืน record รูปเดียวกัน: `sourceRowNo`, `physicalLineNumber`/`physicalRecordNumber`, `fields {fieldKey: rawText|null}`, diagnostics โดยไม่ทิ้ง record ที่มี business value ผิด (M-06, D-04)

ตัวอย่างต่อไปเป็น **ข้อมูลสมมติ** เพื่อแสดง structure; field keys และตัวเลขไม่ใช่กฎของ partner ใหม่

| `kind` | ตัวอย่าง `inputLayout` ใน immutable config version | แถวต้นทาง → Source fields |
|---|---|---|
| `Excel` | `{"kind":"Excel","extension":".xls","sheetIndex":0,"headerRow":1,"encoding":"cp874","fields":[{"key":"reference","header":"เลขอ้างอิง"},{"key":"amount","header":"จำนวนเงิน"}]}` | sheet row 2: `R001` / numeric `180` → row 1, physical 2, `{"reference":"R001","amount":"180"}`; MTLS fixture จริงเป็น BIFF5/`Book`, CODEPAGE 1252 แต่ text bytes cp874 |
| `Delimited` | `{"kind":"Delimited","extension":".csv","encoding":"utf-8","delimiter":",","quote":"\"","hasHeader":true,"fields":[{"key":"reference","header":"reference"},{"key":"amount","header":"amount"}]}` | `reference,amount` แล้ว `R001,180` → row 1, physical line 2, `{"reference":"R001","amount":"180"}`; TSV ใช้ delimiter `\t` ใน version อื่น |
| `FixedWidth` | `{"kind":"FixedWidth","extension":".txt","encoding":"utf-8","fields":[{"key":"reference","start":1,"length":4},{"key":"amount","start":6,"length":6}]}` | `R001 000180` → row 1, physical line 1, `{"reference":"R001","amount":"000180"}`; `start` เป็น 1-based ตาม [CONTEXT.md](../../CONTEXT.md), ช่วงห้ามซ้อนและต้องอยู่ในขนาด record ที่กำหนด |

ตัวอย่าง Source fields เป็น raw text ไม่ใช่ Delivery Payload; `amount` จะแปลง/ส่งที่ path ใดขึ้นกับ Transformation Program ของ config นั้น ไม่เติมค่าธุรกิจที่ผู้ใช้ยังไม่ส่งใน Q5

**ข้อบังคับของ adapters:** กำหนด encoding ต่อ config, ขนาดไฟล์/record/field/จำนวน records มี limit, header/blank row policy ระบุชัด, malformed file คืน file/row diagnostic, reader หลาย format ใช้เลขแถวข้อมูลเริ่ม 1 และเก็บตำแหน่งกายภาพแยก; signed amount/date/cp874 เป็นงานแปลงของ format/program ที่พิสูจน์ด้วย fixture ไม่ใช้ `ToString()` แบบ culture ของเครื่อง

## 3. API เดียวสำหรับทุก format

คง prefix `/api/mapping` และ routes ใน [M1.2](mapping-api-contract.md) ไม่เพิ่ม `/mtls/*`, `/csv/*` หรือ `/fixed-width/*`; `POST /mapping-configs/{id}/versions` รับ `inputLayout` ของ 3 kinds พร้อม `sourceDefinitionVersionId`/program; `validate` ตรวจ kind/options/bindings/capability; `preview-file` รับ file + version/draft ตาม layout นั้น; `GET /file-jobs` และ detail คืน `inputLayoutKind`, pinned config/definition version และ diagnostics โดยไม่เปลี่ยน shape ตาม partner

การ activate ต้องตรวจว่า adapter สำหรับ `kind` พร้อม, extension/route ไม่ชน, source fields ตรง layout และ program, schema/profile ใช้ได้; ไม่ activate config ที่ parser หรือ dependency ยังไม่มีจริง ตัวอ่าน format ไม่ตัดสิน `transactionType`, product/package หรือ beneficiary แทน config/program; contract ยังเป็น `pa-inform/1` ตาม D-06

ตัวอย่าง `POST /mapping-configs/{id}/preview-file` สำหรับไฟล์ CSV ข้างบน หลังเลือก Delimited layout: `result.records[0]` มี `sourceRowNo:1`, `physicalLineNumber:2`, `raw:{"reference":"R001","amount":"180"}`; `result.outputs` เป็น JSON ตาม program ที่เลือก และ `diagnostics` ระบุ path/line เมื่อ parse หรือ map ผิด Preview ไม่สร้าง Source/Job/outbox หรือส่ง HTTP

## 4. หน้า Mapping

ภายใต้เมนู Mapping เดิมใน `features/mapping/` (D-78) ใช้หน้าหลักดังนี้ โดยหน้ารายการ/วันที่/409/error/การแบ่งหน้าใช้ [web.md](web.md):

1. **Configs:** รายการทุก config แสดงชื่อ, input format, input path, active version, enabled, last job status; filter ตาม format/status ไม่จำกัด partner MTLS; เพิ่ม config เริ่มจากเลือก format ตาม capabilities
2. **Config editor:** กำหนด Source fields และ Input Layout panel ตาม `kind` (Excel: sheet/header/encoding; Delimited: delimiter/quote/header/encoding; Fixed-width: start/length/encoding); Mapping Program, destination profile, validate และ preview ใช้ส่วนร่วม; save เป็น immutable version ใหม่ทั้งก้อน แล้วค่อย activate
3. **Jobs:** รายการ File Jobs ทุก format พร้อม filter config/format/status/date และ counts import/rows/outputs/attempts; detail เปิด Source ดิบรายแถว, diagnostics, Delivery Payload snapshot และ attempts แบบ read-only; retry แสดงเฉพาะ action/state ที่ API อนุญาต

Web ใช้ `core/mapping-api.ts` ไฟล์เดียวสำหรับ service; ไม่ hardcode 19 columns, P/C, cp874 หรือ MTLS default ใน component. ตาราง Source detail สร้าง columns จาก pinned Source Definition ของงานนั้น; ไม่ใช้ definition ล่าสุดแทนงานเก่า UI ไม่ให้พิมพ์ SQL/table name หรือ endpoint URL ปลายทาง

## 5. เกณฑ์ตรวจรับและขอบเขตที่ต้องตกลง

- ใช้ **สาม config สมมติ** จากตัวอย่างใน §2: Excel MTLS fixture 2,556 แถว, Delimited CSV/TSV, Fixed-width; validate/preview/activate/รับไฟล์/Source detail/job summary ทำงานด้วย route และ UI flow เดียวกัน ต้องมี fixture สมมติของ Delimited/Fixed-width ใน repo โค้ดเมื่อ implement tests; UAT ของ partner อื่นต้องเพิ่ม acceptance ตามไฟล์/fields ที่ผู้ใช้ส่งภายหลังโดยไม่ใช้ข้อมูลส่วนบุคคลจริงใน test
- เปลี่ยน version ของแต่ละ config แล้วงานเก่ายังคง layout/source/program/schema/profile ที่ pin ไว้; malformed record มี diagnostic และยอด ไม่หายเงียบ; test reader/preview ต้องตรวจ bytes/encoding/blank/header/physical line และไม่มี side effects
- `GET /capabilities` แสดงสถานะตาม implementation จริง; acceptance ของ `.xlsx`/encoding/layout เพิ่มเติมยังต้องตกลงและมี test ก่อนเปิดใช้ ห้ามรับ extension เพียงเพราะอยู่ใน allowlist เชิงเอกสาร
- Q9: ผู้ใช้ยืนยันให้ **partner อื่นเปิดใช้ใน UAT** ผ่าน runtime/API/Web สำหรับสาม format; D-01/D-70 ปัจจุบันกำหนด MTLS เป็น partner แรกและไม่ทำ partner อื่น จึงต้องให้ Dev 1/PM ลงผลขอบเขต/วันส่งก่อนเริ่ม source ส่วนที่ขยาย รายชื่อ partner, field bindings และตัวอย่างไฟล์สำหรับ acceptance ยังรอข้อมูลจากผู้ใช้ตาม M-24/Q5
- M1.2 review API/Web contract ยังเลื่อนตามคำขอผู้ใช้; เอกสารนี้เป็น draft ไม่ใช่การอนุมัติ contract หรือหลักฐานว่า runtime/UI ผ่านแล้ว
