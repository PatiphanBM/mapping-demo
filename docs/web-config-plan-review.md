# Review: web-config-plan.md (Phase 19–20)

วันที่ review: 2026-10-08

เอกสารนี้ review [web-config-plan.md](web-config-plan.md) เทียบกับ [demo-plan.md](demo-plan.md) หัวข้อ "ข้อสรุปรอบที่ 7–9", [CONTEXT.md](../CONTEXT.md) และโค้ดปัจจุบัน ได้แก่ migrations, `FileImportHandler`, `SourceRowWriter`, `NormalizedRowWriter`, `DdlBuilder` และ `NormalizationStatusCalculator`

แบ่งเป็นสามกลุ่ม:

- **A. ต้องแก้**: เอกสารขัดกันเองหรือขัดกับโค้ด
- **B. ต้องตัดสินใจ**: ทุกข้อมีคำแนะนำแนบไว้
- **C. เรื่องเล็กที่ควรเขียนให้ชัด**

ข้อที่ยังควรปิดก่อนเริ่ม 19.0 คือ **B7** เพราะมีผลต่อ golden fixture โดยตรง ส่วน B1 ปิดพร้อม A5/B2 และ B4/B6 ปิดแล้ว

---

## A. ต้องแก้

### A1. ลำดับ step ใช้ทำตามจริงไม่ได้

เดิม Worker ต้องอ่าน `inputLayout` และ `transformationProgram` จาก Config Version ก่อนที่แผนจะสร้างคอลัมน์ `input_layout jsonb` และ `transformation_program jsonb` ทำให้ทำตามลำดับไม่ได้

**ข้อสรุป (แก้แล้ว):** แยก persistence ตาม contract เป็น 19.3 สำหรับ Input Layout ก่อนเชื่อม File Import Worker ใน 19.4 และ 19.7 สำหรับ Transformation Program ก่อนเชื่อม Normalize Worker ใน 19.13 แต่ละ step ต้อง update baseline migration, Config Version API, `configure-demo.ps1` และทดสอบ round-trip/version pinning โดย B11 ตัดสินให้ breaking reset และไม่รองรับคอลัมน์เดิม

### A2. ไม่มี `trim` ใน AST

Source เก็บ raw slice ที่มี padding (หัวข้อ "ทิศทาง Input Layout ที่เลือก") และ CONTEXT ก็บอกว่า Normalized Table ต้องตัดช่องว่าง แต่ `ValueExpression` รุ่นแรกไม่มี `trim` ผลคือ Direct field ทุกตัวของ fixed-width จะติด padding ไปด้วย

**ข้อสรุป (แก้แล้ว):** ให้ `source` คืน raw text เสมอและเพิ่ม explicit `trim` node ซึ่งรับเฉพาะ text, ตัด whitespace ทั้งสองด้าน และรักษา `null`; compiler ต้องคืน Config error พร้อม exact path เมื่อ input ไม่ใช่ text ส่วน Target Assignment Coercion ใช้ข้อสรุป B4

### A3. ตัวอย่าง JSON ขัดกับ contract ที่ประกาศไว้

- `literal` ไม่ได้ระบุ type แต่รายการ `ValueExpression` บอกว่า "ค่าคงที่ที่ระบุ type ชัดเจน"
- `eq` ใช้ shorthand `field` + `value` แต่ UI ข้อ 7 ("operand เลือก Source field, metadata หรือ literal") และ multi-field condition ต้องการ operand ที่เป็น expression

**ข้อสรุป (แก้แล้ว):** ไม่รองรับ shorthand `field`/`value`; Boolean comparison ใช้ `left`/`right` ที่เป็น expression เสมอ, literal ทุกตัวต้องระบุ `type` และใช้ `outputKey` ให้ตรงกับ model ตัวอย่างจึงแก้เป็นรูปแบบต่อไปนี้

```json
{
  "kind": "eq",
  "left": {
    "kind": "trim",
    "value": { "kind": "source", "field": "GarageType" }
  },
  "right": { "kind": "literal", "type": "text", "value": "อู่ในเครือ" }
}
```

### A4. UI ตั้งค่าได้ไม่ครบตามที่ acceptance 20.8 ต้องการ

Value kind ใน UI มีแค่ Direct field, Constant, Current date, CASE และ Named transform แต่ 20.8 กำหนดให้สร้างทุกอย่างจาก UI ให้ coverage matrix ครบ ซึ่งรวม `substring`, `convert`, `coalesce`/`defaultIfBlank` และ `metadata` ด้วย

**ข้อสรุป (แก้แล้ว):** เพิ่ม `ValueExpressionEditor` แบบ recursive และ reuse ใน output field, CASE result และ function argument มุมมองพื้นฐานแสดง Direct field, Constant, Current date และ Not mapped ส่วน Advanced value รองรับ Metadata, Trim, Substring, Convert, Coalesce, Default if blank, CASE และ Named transform โดยจำกัด depth/node/arms ตาม capabilities และคง acceptance 20.8 ให้สร้างทุกชนิดผ่าน UI โดยไม่แก้ JSON

### A5. Input Row Error ไม่มีที่เก็บ

`row_errors.row_job_id` เป็น `NOT NULL` (`0004_row_jobs.sql`) แต่ Detail record ที่สั้นหรือยาวผิดจะไม่มี Source row และไม่มี Row Job ไม่มี step ไหนเพิ่มตารางหรือคอลัมน์สำหรับ error ระดับ import

**ข้อสรุป (แก้แล้วพร้อม B1/B2):** pre-scan snapshot ทั้งไฟล์ก่อนเขียน Source row แรก; decode/record type/length/slice error ทำไฟล์เป็น `ImportFailed`, ไม่สร้าง Source row/Row Job/outbox, เก็บ snapshot สำหรับ retry และบันทึก error แรกใน `file_jobs.last_error` ด้วย stable code, physical line, record type และ expected/actual value โดยไม่สร้างตารางใหม่ `row_errors` สงวนไว้สำหรับ normalization error ที่มี Row Job หากอนาคตต้องการ partial import จึงค่อยออกแบบ `file_import_errors` แยก

### A6. ไม่มี migration รองรับสิ่งที่แผนอ้างถึง

| สิ่งที่แผนอ้างถึง | สถานะในโค้ด | อ้างใน step |
|---|---|---|
| `row_errors.output_key`, `expression_path` | ยังไม่มี | ตาราง "ชนิด error", 19.12 |
| `row_jobs.evaluation_at` | ยังไม่มี | 19.10 |
| physical line number ของ Source row | Source table มีแค่ `row_number` | 19.4 |

**ข้อสรุป (แก้แล้ว):** 19.4 เพิ่ม `physical_line_number integer NOT NULL` ผ่าน generated Source DDL และปรับ writer/tests; 19.10 เพิ่ม migration `row_jobs.evaluation_at`; 19.12 เพิ่ม migration `row_errors.output_key`, `expression_path` และทำ `field` nullable เพื่อรองรับ error ใน `sourceWhen`/`emitWhen`; 19.13 persist และคืน diagnostics แบบ end-to-end ทุก schema ถูกสร้างก่อนจุดใช้งาน

### A7. `Filtered` ยังไม่อยู่ในการคำนวณสถานะ

`NormalizationStatusCalculator.Calculate` รับแค่ pending/done/invalid/failed

**ข้อสรุป (แก้แล้ว):** เพิ่ม `RowJobStatus.Filtered`, `FilteredRows` ใน calculator/count query/`FileJobResponse` และแสดงยอดแยกใน `GET /file-jobs/{id}`; Filtered เป็น terminal success ที่ไม่นับเป็น Done และไม่ทำให้เป็น `CompletedWithErrors`, Pending มี precedence เป็น `InProgress` และเฉพาะ Invalid/Failed ทำให้เป็น `CompletedWithErrors` ส่วนการลบ output เดิมเมื่อ reprocess แล้ว Filtered รอตัดสินใน B5

### A8. อ้างอิงที่ไม่มีอยู่จริงหรือคลาดเคลื่อน

- ตรวจเอกสารปัจจุบันแล้ว `learning-plan.md` มี Phase 19 และ Phase 20 ที่ link มายัง `web-config-plan.md` จึงติ๊กแต่ละ Phase ที่ backend handoff 19.15 และ web handoff 20.9 ได้
- `demo-plan.md` รอบ 7 ระบุแล้วว่าถูกขยายด้วยรอบ 8 และเลิกยึดเงื่อนไข "ไม่เพิ่ม endpoint ใหม่" จึงไม่ต้องแก้
- ตาราง "ความหมายของตัวอย่างที่ต้องรองรับ" เดิมไม่มีเลขกำกับทั้งที่ step ต่าง ๆ อ้างตัวอย่าง 1–4 และยังมีกรณี output branches เป็นแถวที่ 5

**ข้อสรุป (แก้แล้ว):** ใส่หมายเลขตัวอย่าง 1–5 และผูก 19.9/20.6 กับตัวอย่าง 1–2, 19.10/20.7 กับตัวอย่าง 3, 19.11/20.7 กับตัวอย่าง 4, 19.12/20.6 กับตัวอย่าง 5 และให้ 19.6/19.15 ตรวจครบ 1–5 โดยไม่แก้ `demo-plan.md`

---

## B. ต้องตัดสินใจ

### B1. การนำเข้าแบบ commit ทีละ row ไปด้วยกันไม่ได้กับ file-level error (ปิดแล้ว)

`FileImportHandler.ImportRecordAsync` commit Source row พร้อม outbox ทีละแถว ทำให้ normalize เริ่มก่อนอ่านไฟล์จบ ถ้าเจอ invalid UTF-8 หรือ record type ที่ไม่รู้จักกลางไฟล์ แถวก่อนหน้าก็เข้า Source และ normalize ไปแล้ว ซึ่งขัดกับ 19.4 ที่บอกว่า "error ก่อน import ไม่ทิ้ง partial success ที่รายงานผิด"

**ข้อสรุป:** ทำ pre-scan pass ทั้งไฟล์ (decode + classify + ตรวจ length/slice) บน snapshot ก่อนเขียนแถวแรก เพราะไฟล์มีขนาดเล็กและทำต่อจากขั้นคำนวณ content hash ได้ เมื่อ pre-scan ผ่านแล้วยังคง commit Source row พร้อม outbox ทีละแถวได้

### B2. Input Row Error ส่งผลกับไฟล์อย่างไร (ปิดแล้ว)

มีสองทาง:

1. fail ทั้งไฟล์
2. ให้ไฟล์เป็น `Imported` แล้วนับแถวเสียแยกไว้

**ข้อสรุป:** ใช้ทางเลือก 1 — fail ทั้งไฟล์เป็น `ImportFailed`, เก็บ diagnostic แรกใน `file_jobs.last_error` และไม่สร้างตาราง import error เพิ่ม

### B3. ความเข้มงวดของ H/D/T (ปิดแล้ว)

- H/T ความยาวผิดถือเป็น error ไหม (ตอนนี้ action เป็น `Ignore` แต่มี `expectedLength` อยู่)
- บังคับไหมว่า H ต้องมีหนึ่งตัวอยู่ต้นไฟล์ และ T หนึ่งตัวอยู่ท้ายไฟล์
- ต้องตรวจจำนวน record ใน Trailer ไหม

**ข้อสรุป:** ไม่ hardcode H/D/T หรือบังคับอย่างละหนึ่ง Fixed-width layout ใช้ ordered `recordSequence` ซึ่งอ้าง record rule และกำหนด `minOccurs`/`maxOccurs` (`null` หมายถึงไม่จำกัด) จึงรองรับหลาย header/trailer และชนิด record ต่างกันได้; parser ตรวจ order/cardinality/length ทุก rule ส่วน Delimited v1 รองรับหลาย preamble lines แต่ใช้ column-name header ได้หนึ่งแถว การประกอบชื่อคอลัมน์จากหลายแถวอยู่นอก scope

### B4. Trim และ implicit conversion

- Direct field ที่ปลายทางเป็น Date/Decimal จะ convert ให้อัตโนมัติ (ใช้ format แบบ `SourceToNormalizedRule.Format` เดิม) หรือผู้ใช้ต้องใส่ `convert` เอง
- เมื่อ A2 กำหนดให้ `source` คืน raw และมี explicit `trim` แล้ว compiler ควรแทรก trim ตอน assign ลง Normalized column ให้อัตโนมัติหรือบังคับให้ AST ระบุเอง

**ข้อสรุป (ปิดแล้ว):** ให้ `source` คืน raw เสมอและ compiler แทรก Target Assignment Coercion ใน compiled program โดยไม่แก้ persisted JSON: ผลชนิด text ถูก trim, target Text รับค่าที่ trim แล้ว, target Date/DateTime/Decimal/Integer/Boolean/Guid convert จาก text, blank required เป็น `Required` และ blank optional เป็น null; optional `inputFormat` ใช้ได้เฉพาะ text → Date/DateTime ส่วน explicit `trim`/`convert` ใช้สำหรับ intermediate expressions และ already-typed value ไม่ convert ซ้ำ UI Direct field ให้ผู้ใช้เลือกเพียง Source field กับ format ที่เกี่ยวข้อง

### B5. Reprocess ที่ได้ `Filtered` (0 outputs) ต้องลบผลเดิมไหม

ถ้าถือว่า Filtered คือ "สำเร็จ" การ reconcile จะลบ output ทั้งหมดของแถวนั้น คำนิยาม Current Normalized Result ใน CONTEXT ยังไม่ได้ครอบคลุมกรณีนี้

**ข้อสรุป (ปิดแล้ว):** ลบ เพราะ `Filtered` เป็น successful empty result Current Normalized Result คือชุด `0..N` outputs และ identity ใช้ `(source_row_id, output_key)` จึงรองรับ `CLOANNUMBER` เดียวที่มีทั้ง `pmx`/`ctp`; successful reprocess ต้อง atomic reconcile desired set — `pmx+ctp`, `pmx` อย่างเดียว, `ctp` อย่างเดียว หรือ empty set ที่ลบทั้งหมดและ mark Filtered ใน transaction เดียว ส่วน Invalid/Failed เก็บชุดเดิมและ failure กลาง transaction ต้อง rollback ทั้ง outputs/status อัปเดตนิยามใน CONTEXT แล้ว

### B6. Anti-lookup กับ idempotency

หัวข้อ "Invariants และ semantics" พูดถึง idempotency key แต่ไม่มี step ไหน implement ต้องตอบให้ได้ก่อนว่า `MTI_ORIGINAL` คือ Normalized table ของ config นี้เองหรือเป็นตารางอื่น

- ถ้าเป็นตารางเดียวกัน ตอน retry ตัว lookup จะเจอ output ของตัวเองแล้วกรองตัวเองทิ้ง จึงต้องกำหนดให้ exclude `source_row_id` เดิม
- ต้องระบุด้วยว่าใน demo ตาราง lookup อยู่ที่ไหนใน Postgres และใครเป็นคนสร้าง

**ข้อสรุป (ปิดแล้ว):** ใน demo ให้ PMIB Normalized table ที่สร้างผ่าน Tables API เป็น `MTI_ORIGINAL` ตามความหมายของ workbook และให้ `configure-demo.ps1` สร้าง composite index `(SYSTEM_ID, CPRODUCTNAME, CLOANNUMBER)` ด้วย trusted setup SQL; config เลือกเพียง `mtiOriginalPolicyExists@1` และส่ง logical arguments `systemId`, `productName`, `loanNumber` โดย runtime เติม `currentSourceRowId` จาก Row Job context ห้ามให้ผู้ใช้กำหนดเอง Adapter query live committed state แบบ parameterized/batched และคืน matching `source_row_id` เพื่อตัด current Source row ออก ดังนั้น retry/reprocess ไม่ block ตัวเอง, Source row อื่นที่มี product + loan เดียวกันยัง match และ loan เดียวกันคนละ product ไม่ match ทั้งนี้เป็น anti-lookup ไม่ใช่ concurrency uniqueness guarantee; ถ้าต้องห้ามสองงานใหม่ที่ชนกันพร้อมกันต้องเพิ่ม constraint/locking เป็น requirement แยก

### B7. `formatRegNo@1` ยังไม่มี source of truth

ใน repo ยังไม่มีโค้ดหรือ behavior ของ `dbo.FormatRegNo` ทำให้ 19.0 และ 19.10 ทำ parity test ไม่ได้

**ต้องตัดสินใจ:** ไปขอ source ของ function จริงมา หรือนิยาม behavior เองสำหรับ demo แล้วเขียนกำกับไว้ว่าเป็น stand-in

### B8. `currentDate` ใช้ timezone ไหน

`GETDATE()` ของระบบเดิมใช้เวลาของ server (น่าจะเป็นเวลาไทย) แต่ `evaluation_at` เป็น timestamptz

**ข้อสรุป (ปิดแล้ว):** เก็บ `evaluation_at` เป็น instant ใน `timestamptz` และให้ `currentDate` แปลงด้วย IANA timezone `Asia/Bangkok` จาก semantics profile ก่อนคืน typed `Date`; ห้ามใช้ `DateTime.Now` หรือ timezone ของเครื่อง Worker; retry ใช้ instant เดิมแม้ทำงานข้ามเที่ยงคืน ส่วน reprocess สร้าง instant ใหม่ Preview ต้องรับ ISO 8601 ที่มี `Z`/offset และ echo instant ที่ใช้จริง โดยทดสอบ boundary `2026-10-08T16:59:59Z → 2026-10-08` กับ `2026-10-08T17:00:00Z → 2026-10-09`

### B9. ชื่อและเนื้อหาของ `semanticsProfile`

ตอนนี้ชื่อ `"pmib-v1"` แต่ TIB ก็ต้องใช้ profile นี้ด้วย

**ข้อสรุป (ปิดแล้ว):** เปลี่ยนเป็น required string `sqlserver-legacy-v1` ซึ่งอ้าง server-owned immutable preset ที่ PMIB/TIB ใช้ร่วมกัน ไม่ให้ config ส่ง option object และห้ามเปลี่ยน behavior ภายใต้ชื่อเดิม; ถ้าความหมายเปลี่ยนต้องเพิ่ม `sqlserver-legacy-v2` โดย `languageVersion` คุมรูป AST ส่วน profile คุม semantics ต่อไปนี้:

- null แยกจาก empty string, comparison/AND/OR/NOT ใช้ three-valued logic และ predicate ผ่านเฉพาะ `true`; `coalesce` จัดการ null ส่วน `isBlank`/`defaultIfBlank` รวมข้อความว่างหลัง trim
- text equality/IN/LIKE ใช้ ordinal case-insensitive แบบ deterministic; LIKE รองรับ `%`, `_`, `[abc]`, range และ negated bracket แต่ไม่ใช่ regex
- decimal ใช้ invariant culture, จุดทศนิยม, ไม่รับ comma คั่นหลัก และเปรียบเทียบแบบ decimal
- Date/DateTime parse exact ด้วย invariant culture โดย default เป็น `yyyy-MM-dd`/`yyyy-MM-ddTHH:mm:ss` และ format อื่นมาจาก allowlist
- `currentDate` ใช้ `Asia/Bangkok` ตาม B8

ชื่อ profile สื่อว่าเป็น SQL Server-style legacy subset ที่ระบบประกาศรองรับ ไม่ใช่การจำลอง SQL Server ทุก collation/operator Compiler ต้อง reject profile ที่ไม่รู้จัก และ capabilities คืนชื่อ/summary แบบ read-only

### B10. ที่อยู่ของโค้ดและโปรเจกต์

แผนเสนอให้สร้าง `MappingDemo.Input` ใหม่ แต่ CSV reader อยู่ใน `MappingDemo.Shared` แล้ว และไม่ได้ระบุว่า Transformation Module จะอยู่ที่ไหน

**ข้อสรุป (ปิดแล้ว):** Phase 19 ไม่สร้าง project ใหม่ ให้สอง deep Modules อยู่ใน project เดิม `MappingDemo.Shared` แต่แยก namespace/folder เป็น `MappingDemo.Shared.Input` และ `MappingDemo.Shared.Transformation`; แต่ละ Module เปิดเฉพาะ facade, contracts/results, opaque compiled value และ dependency port ที่มี production/test Adapters จริง ส่วน parser/compiler/evaluator/helper เป็น `internal`; API/Worker เรียก Interface เดียวกันและยัง reference Shared ตามเดิม โดย Modules ห้ามรู้จัก controller, HTTP, Kafka, job orchestration, archive path หรือ persistence transaction; Input รับ `Stream` ส่วน Transformation รับ schema/rows/evaluation time และ injected ports; PostgreSQL named lookup Adapter ที่สอง callers ใช้ร่วมกันอยู่ใต้ `MappingDemo.Shared.Transformation.Adapters` ส่วน in-memory Adapter ใช้ใน tests ซึ่งทดสอบ observable behavior ผ่าน Interface ไม่อ้าง internals หากระบบโตจึงค่อย extract namespaces เป็นสอง projects โดยไม่เปลี่ยน Interface

### B11. Compatibility adapter ใน persistence steps 19.3 และ 19.7

demo นี้ reset DB ได้อยู่แล้ว

**ข้อสรุป (ปิดแล้ว):** ไม่ทำ compatibility Adapter, dual-read/write, legacy payload converter หรือ evaluator/parser สองชุด ให้ replace `file_to_source`/`source_to_normalized` ใน baseline migration ด้วย `input_layout`/`transformation_program`, เปลี่ยน Config Version request/response และ `configure-demo.ps1` เป็น contract ใหม่เท่านั้น แล้วหยุด API/Worker และรัน `reset-demo.ps1` ก่อน configure ใหม่; old JSON/schema ไม่ได้รับการรองรับ แต่ต้องมี characterization/golden tests ยืนยันว่า direct CSV ให้ observable behavior เดิมผ่าน Modules ใหม่ เมื่อ replacement เชื่อมครบให้ลบ legacy rules/readers/evaluator; rollback คือย้อน code แล้ว reset/reconfigure ไม่ใช่ migrate data กลับ ส่วน production data migration ให้เป็น one-time tool แยกหลัง AST นิ่งหากมี requirement จริง

### B12. Preview แบบเลือก sample file

ยังไม่ได้กำหนดว่าจะใช้ multipart หรือส่งข้อความใน JSON และไม่ได้กำหนดขนาดสูงสุด

**ข้อสรุป (ปิดแล้ว):** v1 ไม่ใช้ multipart ให้ browser ตรวจขนาด, อ่าน `ArrayBuffer`, decode UTF-8 ด้วย fatal `TextDecoder`, pre-check line count แล้วส่ง `{ kind: "textFile", fileName, content }` ใน JSON; `fileName` ใช้แสดงผลเท่านั้น API ตรวจ authoritative limit ที่ UTF-8 content ไม่เกิน `1 MiB`/50 parsed records และ request body ไม่เกิน `3 MiB`, reject โดยไม่ truncate ด้วย `SampleTooLarge`/`TooManySampleRecords`/`InvalidSampleEncoding`, re-encode เป็น `MemoryStream` ให้ Input Module และทำงานใน memory โดยไม่ persist disk/database หรือ log content ไฟล์ TIB 87 KB/23 records ต้อง preview ผ่าน ส่วน byte-perfect/encoding อื่น/ไฟล์ใหญ่ให้เพิ่ม version หรือ multipart endpoint แยกในอนาคต

### B13. ขนาดของ phase

มี 26 step ในเฟสเดียวหลังแยก persistence ตาม A1

**ข้อสรุป (ปิดแล้ว):** แยกเป็น Phase 19 สำหรับ backend ขั้น 19.0–19.15 และ Phase 20 สำหรับเว็บขั้น 20.0–20.9 โดย 19.15 เป็น backend acceptance/handoff gate และห้ามเริ่ม Phase 20 จนกว่า Phase 19 จะผ่านและถูกติ๊กเสร็จ เว้นแต่ผู้ใช้สั่งข้ามอย่างชัดเจน; เพิ่มสอง Phase แยกกันใน `learning-plan.md`

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
| A1 | ลำดับ persistence กับ Worker integration | แก้แล้ว — แยกเป็น 19.3 และ 19.7 |
| A2 | ไม่มี `trim` | แก้แล้ว — explicit `trim` และ assignment coercion ตาม B4 |
| A3 | ตัวอย่าง JSON | แก้แล้ว — expression operands และ typed literals |
| A4 | UI value kinds ไม่ครบตาม 20.8 | แก้แล้ว — recursive ValueExpressionEditor ครบ scope |
| A5 | ที่เก็บ Input Row Error | แก้แล้ว — fail ทั้งไฟล์และใช้ `file_jobs.last_error` |
| A6 | migration ที่ขาด | แก้แล้ว — กำหนดเจ้าของใน 19.4/19.10/19.12/19.13 |
| A7 | `Filtered` ใน status calculator | แก้แล้ว — terminal success และมี count แยก |
| A8 | อ้างอิงคลาดเคลื่อน | แก้แล้ว — ตัวอย่าง 1–5 และ reference ตรงกัน |
| B1 | pre-scan ก่อน import | ปิดแล้ว — validate ทั้ง snapshot ก่อนเขียน |
| B2 | Input Row Error กับสถานะไฟล์ | ปิดแล้ว — `ImportFailed` ทั้งไฟล์ |
| B3 | ความเข้มงวดของ H/D/T | ปิดแล้ว — configurable sequence/cardinality |
| B4 | trim / implicit conversion | ปิดแล้ว — Target Assignment Coercion |
| B5 | Filtered ตอน reprocess | ปิดแล้ว — atomic reconcile empty set และลบผลเดิม |
| B6 | anti-lookup กับ idempotency | ปิดแล้ว — PMIB Normalized table, composite key และ self-exclusion |
| B7 | source ของ `FormatRegNo` | **ปิดก่อน 19.0** |
| B8 | timezone | ปิดแล้ว — `Asia/Bangkok` จาก persisted `evaluation_at` instant |
| B9 | `semanticsProfile` | ปิดแล้ว — immutable `sqlserver-legacy-v1` preset |
| B10 | ที่อยู่ของ module | ปิดแล้ว — สอง namespaces ใน `MappingDemo.Shared` |
| B11 | compatibility adapter | ปิดแล้ว — breaking reset ไม่มี runtime adapter |
| B12 | preview sample file | ปิดแล้ว — UTF-8 text JSON, 1 MiB/50 records |
| B13 | แยก phase | ปิดแล้ว — Phase 19 backend, Phase 20 web |
