# Review: web-config-plan.md (Phase 19)

วันที่ review: 2026-10-08

เอกสารนี้ review [web-config-plan.md](web-config-plan.md) เทียบกับ [demo-plan.md](demo-plan.md) หัวข้อ "ข้อสรุปรอบที่ 7–9", [CONTEXT.md](../CONTEXT.md) และโค้ดปัจจุบัน ได้แก่ migrations, `FileImportHandler`, `SourceRowWriter`, `NormalizedRowWriter`, `DdlBuilder` และ `NormalizationStatusCalculator`

แบ่งเป็นสามกลุ่ม:

- **A. ต้องแก้**: เอกสารขัดกันเองหรือขัดกับโค้ด
- **B. ต้องตัดสินใจ**: ทุกข้อมีคำแนะนำแนบไว้
- **C. เรื่องเล็กที่ควรเขียนให้ชัด**

ข้อที่ควรปิดก่อนเริ่ม 19.0 คือ **B1, B4, B6 และ B7** เพราะมีผลต่อ golden fixture โดยตรง

---

## A. ต้องแก้

### A1. ลำดับ step ใช้ทำตามจริงไม่ได้

ใน 19.3 Worker ต้องอ่าน `inputLayout` จาก Config Version และ 19.11 ต้องอ่าน `transformationProgram` แต่คอลัมน์ `input_layout jsonb` และ `transformation_program jsonb` เพิ่งถูกสร้างใน 19.12

**แก้:** ย้าย persistence ของ `input_layout` ไปไว้ก่อน 19.3 และของ `transformation_program` ไปไว้ก่อน 19.11 หรือแยกออกมาเป็น step ของตัวเอง

### A2. ไม่มี `trim` ใน AST

Source เก็บ raw slice ที่มี padding (หัวข้อ "ทิศทาง Input Layout ที่เลือก") และ CONTEXT ก็บอกว่า Normalized Table ต้องตัดช่องว่าง แต่ `ValueExpression` รุ่นแรกไม่มี `trim` ผลคือ Direct field ทุกตัวของ fixed-width จะติด padding ไปด้วย

**แก้:** เลือกระหว่างให้ `source` trim อัตโนมัติ หรือเพิ่ม node `trim` (ดู B4)

### A3. ตัวอย่าง JSON ขัดกับ contract ที่ประกาศไว้

- `literal` ไม่ได้ระบุ type แต่รายการ `ValueExpression` บอกว่า "ค่าคงที่ที่ระบุ type ชัดเจน"
- `eq` ใช้ shorthand `field` + `value` แต่ UI ข้อ 7 ("operand เลือก Source field, metadata หรือ literal") และ multi-field condition ต้องการ operand ที่เป็น expression

**แก้:** เขียนตัวอย่างให้ตรงกับ AST จริง เช่น

```json
{
  "kind": "eq",
  "left": { "kind": "source", "field": "GarageType" },
  "right": { "kind": "literal", "type": "text", "value": "อู่ในเครือ" }
}
```

### A4. UI ตั้งค่าได้ไม่ครบตามที่ acceptance 19.23 ต้องการ

Value kind ใน UI มีแค่ Direct field, Constant, Current date, CASE และ Named transform แต่ 19.23 กำหนดให้สร้างทุกอย่างจาก UI ให้ coverage matrix ครบ ซึ่งรวม `substring`, `convert`, `coalesce`/`defaultIfBlank` และ `metadata` ด้วย

**แก้:** เลือกว่าจะเพิ่มชนิดเหล่านี้ใน UI หรือลดขอบเขตของ 19.23 ให้ครอบคลุมเฉพาะชนิดที่ UI ตั้งค่าได้

### A5. Input Row Error ไม่มีที่เก็บ

`row_errors.row_job_id` เป็น `NOT NULL` (`0004_row_jobs.sql`) แต่ Detail record ที่สั้นหรือยาวผิดจะไม่มี Source row และไม่มี Row Job ไม่มี step ไหนเพิ่มตารางหรือคอลัมน์สำหรับ error ระดับ import

**แก้:** ขึ้นกับการตัดสินใจใน B1/B2 ถ้าเลือกให้ fail ทั้งไฟล์ ใช้ `file_jobs.last_error` ได้เลยโดยไม่ต้องสร้างตารางใหม่

### A6. ไม่มี migration รองรับสิ่งที่แผนอ้างถึง

| สิ่งที่แผนอ้างถึง | สถานะในโค้ด | อ้างใน step |
|---|---|---|
| `row_errors.output_key`, `expression_path` | ยังไม่มี | ตาราง "ชนิด error", 19.10 |
| `row_jobs.evaluation_at` | ยังไม่มี | 19.8 |
| physical line number ของ Source row | Source table มีแค่ `row_number` | 19.3 |

**แก้:** ระบุว่า step ไหนเป็นคนเพิ่มคอลัมน์เหล่านี้ และใส่การตรวจไว้ในส่วน "ตรวจ" ของ step นั้น

### A7. `Filtered` ยังไม่อยู่ในการคำนวณสถานะ

`NormalizationStatusCalculator.Calculate` รับแค่ pending/done/invalid/failed

**แก้:** ระบุว่า `Filtered` นับเป็น Completed (ไม่ทำให้เป็น `CompletedWithErrors`) และแสดงยอดแยกใน `GET /file-jobs/{id}`

### A8. อ้างอิงที่ไม่มีอยู่จริงหรือคลาดเคลื่อน

- 19.24 สั่ง "ติ๊ก Phase 19 ใน learning-plan" แต่ learning-plan จบที่ Phase 18
- demo-plan รอบ 7 ยังเขียนว่า "แผนทีละ step อยู่ที่ web-config-plan" โดยไม่ได้บอกว่าขอบเขตขยายไปแล้ว (เรื่องเล็ก)
- 19.7 และ 19.22 อ้าง "ตัวอย่าง 1–2" และ "ตัวอย่าง 3–4" แต่ตาราง "ความหมายของตัวอย่างที่ต้องรองรับ" ไม่ได้ใส่เลขกำกับ และมี 5 แถว

**แก้:** ใส่เลขกำกับในตารางตัวอย่าง และเพิ่ม Phase 19 ใน learning-plan หรือเปลี่ยนข้อความใน 19.24

---

## B. ต้องตัดสินใจ

### B1. การนำเข้าแบบ commit ทีละ row ไปด้วยกันไม่ได้กับ file-level error

`FileImportHandler.ImportRecordAsync` commit Source row พร้อม outbox ทีละแถว ทำให้ normalize เริ่มก่อนอ่านไฟล์จบ ถ้าเจอ invalid UTF-8 หรือ record type ที่ไม่รู้จักกลางไฟล์ แถวก่อนหน้าก็เข้า Source และ normalize ไปแล้ว ซึ่งขัดกับ 19.3 ที่บอกว่า "error ก่อน import ไม่ทิ้ง partial success ที่รายงานผิด"

**แนะนำ:** ทำ pre-scan pass ทั้งไฟล์ (decode + classify + ตรวจ length) บน snapshot ก่อนเขียนแถวแรก เพราะไฟล์มีขนาดเล็ก และทำได้ต่อจากขั้นที่คำนวณ content hash อยู่แล้ว

### B2. Input Row Error ส่งผลกับไฟล์อย่างไร

มีสองทาง:

1. fail ทั้งไฟล์
2. ให้ไฟล์เป็น `Imported` แล้วนับแถวเสียแยกไว้

**แนะนำ:** ถ้าใช้ pre-scan ตาม B1 ให้ fail ทั้งไฟล์ไปเลย จะง่ายที่สุด และไม่ต้องสร้างตาราง import error เพิ่ม (ปิด A5 ไปด้วย)

### B3. ความเข้มงวดของ H/D/T

- H/T ความยาวผิดถือเป็น error ไหม (ตอนนี้ action เป็น `Ignore` แต่มี `expectedLength` อยู่)
- บังคับไหมว่า H ต้องมีหนึ่งตัวอยู่ต้นไฟล์ และ T หนึ่งตัวอยู่ท้ายไฟล์
- ต้องตรวจจำนวน record ใน Trailer ไหม

**แนะนำ:** สำหรับ v1 ตรวจ length ทุก record type และบังคับ H ต้น/T ท้ายอย่างละหนึ่ง ส่วน trailer count เลื่อนไปทำทีหลัง

### B4. Trim และ implicit conversion

- Direct field ที่ปลายทางเป็น Date/Decimal จะ convert ให้อัตโนมัติ (ใช้ format แบบ `SourceToNormalizedRule.Format` เดิม) หรือผู้ใช้ต้องใส่ `convert` เอง
- `source` ควร trim เองไหม

**แนะนำ:** ให้ `source` คืนค่า raw เสมอ และให้ compiler แทรก trim + convert ตาม type ปลายทางเมื่อ assign ลง Normalized column ส่วน UI ให้แค่เลือก format

### B5. Reprocess ที่ได้ `Filtered` (0 outputs) ต้องลบผลเดิมไหม

ถ้าถือว่า Filtered คือ "สำเร็จ" การ reconcile จะลบ output ทั้งหมดของแถวนั้น คำนิยาม Current Normalized Result ใน CONTEXT ยังไม่ได้ครอบคลุมกรณีนี้

**แนะนำ:** ลบ เพราะผลนี้ถูกต้องตาม config ใหม่ แล้วอัปเดต CONTEXT ให้ชัด

### B6. Anti-lookup กับ idempotency

หัวข้อ "Invariants และ semantics" พูดถึง idempotency key แต่ไม่มี step ไหน implement ต้องตอบให้ได้ก่อนว่า `MTI_ORIGINAL` คือ Normalized table ของ config นี้เองหรือเป็นตารางอื่น

- ถ้าเป็นตารางเดียวกัน ตอน retry ตัว lookup จะเจอ output ของตัวเองแล้วกรองตัวเองทิ้ง จึงต้องกำหนดให้ exclude `source_row_id` เดิม
- ต้องระบุด้วยว่าใน demo ตาราง lookup อยู่ที่ไหนใน Postgres และใครเป็นคนสร้าง

### B7. `formatRegNo@1` ยังไม่มี source of truth

ใน repo ยังไม่มีโค้ดหรือ behavior ของ `dbo.FormatRegNo` ทำให้ 19.0 และ 19.8 ทำ parity test ไม่ได้

**ต้องตัดสินใจ:** ไปขอ source ของ function จริงมา หรือนิยาม behavior เองสำหรับ demo แล้วเขียนกำกับไว้ว่าเป็น stand-in

### B8. `currentDate` ใช้ timezone ไหน

`GETDATE()` ของระบบเดิมใช้เวลาของ server (น่าจะเป็นเวลาไทย) แต่ `evaluation_at` เป็น timestamptz

**แนะนำ:** กำหนด `Asia/Bangkok` ไว้ใน `semanticsProfile`

### B9. ชื่อและเนื้อหาของ `semanticsProfile`

ตอนนี้ชื่อ `"pmib-v1"` แต่ TIB ก็ต้องใช้ profile นี้ด้วย

**แนะนำ:** ตั้งชื่อตาม semantics เช่น `sqlserver-legacy-v1` แล้วระบุใน 19.0 ว่า profile กำหนดอะไรบ้าง:

- null กับ empty string
- LIKE แบบ case-insensitive และ wildcard `_`/`[]`
- culture ของ decimal
- timezone ของ `currentDate`

### B10. ที่อยู่ของโค้ดและโปรเจกต์

แผนเสนอให้สร้าง `MappingDemo.Input` ใหม่ แต่ CSV reader อยู่ใน `MappingDemo.Shared` แล้ว และไม่ได้ระบุว่า Transformation Module จะอยู่ที่ไหน

**แนะนำ:** ใส่ทั้งสอง module ไว้ใน `MappingDemo.Shared` คนละ namespace เพื่อให้เข้ากับโครงเดิมของ demo หรือถ้าจะแยก ก็แยกทั้งสองตัวให้สม่ำเสมอกัน

### B11. Compatibility adapter ใน 19.12

demo นี้ reset DB ได้อยู่แล้ว

**แนะนำ:** ไม่ต้องทำ adapter และไม่ต้องกำหนด "วันถอดออก" แค่ reset แล้วแก้ `configure-demo.ps1` จะได้ไม่มี evaluator สองชุดตั้งแต่แรก

### B12. Preview แบบเลือก sample file

ยังไม่ได้กำหนดว่าจะใช้ multipart หรือส่งข้อความใน JSON และไม่ได้กำหนดขนาดสูงสุด

**แนะนำ:** v1 รับเป็น text ใน JSON จำกัดขนาด (เช่นไม่เกิน 50 lines หรือ 1 MB) ฝั่ง browser อ่านไฟล์แล้วส่งเป็น text

### B13. ขนาดของ phase

มี 25 step ในเฟสเดียว

**แนะนำ:** แยกเป็น Phase 19 (backend, 19.0–19.14) กับ Phase 20 (web) ให้ตรงกับกติกาในแผนที่ให้ backend ผ่านก่อนเริ่มเว็บ

---

## C. เรื่องเล็กที่ควรเขียนให้ชัด

- **Line ending:** `isoMTI-N7780.txt` ใช้ CRLF และมี CRLF ปิดท้ายไฟล์ ควรเขียนใน 19.0 ว่ารับทั้ง LF และ CRLF, บรรทัดว่างสุดท้ายไม่นับเป็น record และถ้ามี BOM จะทำอย่างไร
- **Row Number:** ควรเขียนคำนิยามใหม่ใน CONTEXT ว่าเป็นลำดับของ Import record ไม่นับ H/T เพราะคำนิยามเดิมพูดถึงแค่ header ของ CSV
- **ขอบเขตของ `inputLayout`:** Reprocess ไม่อ่านไฟล์ใหม่ ดังนั้นการเปลี่ยน `inputLayout` ใน version ใหม่จะมีผลเฉพาะไฟล์ใหม่ ควรเขียนไว้ใน Invariants
- **Requirement ของ Node:** "เวอร์ชันที่ Next.js ล่าสุดรองรับ" ยังกว้างไป ควร pin major version เช่น Node 22 LTS

---

## สรุปสถานะ

| ข้อ | หัวข้อ | สถานะ |
|---|---|---|
| A1 | ลำดับ persistence กับ Worker integration | ต้องแก้ |
| A2 | ไม่มี `trim` | ต้องแก้ (ขึ้นกับ B4) |
| A3 | ตัวอย่าง JSON | ต้องแก้ |
| A4 | UI value kinds ไม่ครบตาม 19.23 | ต้องแก้ |
| A5 | ที่เก็บ Input Row Error | ต้องแก้ (ขึ้นกับ B1/B2) |
| A6 | migration ที่ขาด | ต้องแก้ |
| A7 | `Filtered` ใน status calculator | ต้องแก้ |
| A8 | อ้างอิงคลาดเคลื่อน | ต้องแก้ |
| B1 | pre-scan ก่อน import | **ปิดก่อน 19.0** |
| B2 | Input Row Error กับสถานะไฟล์ | ตัดสินใจ |
| B3 | ความเข้มงวดของ H/D/T | ตัดสินใจ |
| B4 | trim / implicit conversion | **ปิดก่อน 19.0** |
| B5 | Filtered ตอน reprocess | ตัดสินใจ |
| B6 | anti-lookup กับ idempotency | **ปิดก่อน 19.0** |
| B7 | source ของ `FormatRegNo` | **ปิดก่อน 19.0** |
| B8 | timezone | ตัดสินใจ |
| B9 | `semanticsProfile` | ตัดสินใจ |
| B10 | ที่อยู่ของ module | ตัดสินใจ |
| B11 | compatibility adapter | ตัดสินใจ |
| B12 | preview sample file | ตัดสินใจ |
| B13 | แยก phase | ตัดสินใจ |
