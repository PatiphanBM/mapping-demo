# Mapping Demo — แผน Input Layout, Transformation Config, Contract Mapping และหน้าเว็บ (Phase 19–28)

> **Design source:** Phase 21–24 ในเอกสารนี้เป็น implementation plan ของ [Contract API and Legacy Table Delivery Design](../spec/spec-design-contract-api-legacy-delivery.md) หากรายละเอียดขัดกันให้ design specification เป็น authoritative source ส่วน Phase 19–20 เก็บเป็น baseline ของ behavior เดิมที่ต้องไม่ regression Phase 25 เป็นภาคต่อจากการเลือก Prototype C และต้องแก้ design specification ให้รองรับ `JsonFile` อย่างเป็นทางการใน Step 25.0 ก่อนเริ่ม implementation ส่วน Phase 26 ย้าย API destination profile จาก duplicated application config ไปเป็น versioned server-owned catalog พร้อมหน้า Admin และต้องล็อก design/addendum ใน Step 26.0 ก่อนเริ่ม implementation ส่วน Phase 27 ย้าย Contract schema จาก bundled fixture ไปเป็น versioned Contract Registry พร้อมหน้า Admin และต้องล็อก lifecycle/authorization/compatibility contract ใน Step 27.0 ก่อนเริ่ม implementation ส่วน Phase 28 เพิ่ม Table Admin และ physical schema verification โดยต้องล็อก comparison/authorization/drift contract ใน Step 28.0 ก่อนเพิ่ม Inspector หรือ Admin mutation

แผนนี้ต่อจาก [learning-plan.md](learning-plan.md) โดยขยาย Mapping Config จาก CSV field mapping แบบ 1:1 ให้รองรับ fixed-width text จาก `Mapping-TIB-PRD.xlsx`, transformation ที่พบใน `Mapping-PMIB-PRD.xlsx` และหน้าเว็บตั้งค่า Config Version ผ่าน `MappingDemo.Api`

คำศัพท์เดิมยึดตาม [CONTEXT.md](../CONTEXT.md) การตัดสินใจเรื่องหน้าเว็บอยู่ใน [demo-plan.md](demo-plan.md) หัวข้อ "ข้อสรุปรอบที่ 7", ทิศทาง Transformation Program อยู่ในหัวข้อ "ข้อสรุปรอบที่ 8" และ fixed-width Input Layout อยู่ในหัวข้อ "ข้อสรุปรอบที่ 9"

---

## วิธีใช้แผนนี้

ใช้กติกาเดียวกับ [learning-plan.md — วิธีใช้แผนนี้](learning-plan.md#วิธีใช้แผนนี้) ทุกข้อ ทั้งการทำทีละ step รูปแบบ **ทำ / เข้าใจ / ตรวจ** และ **AI agent ห้ามสร้าง commit เอง** ส่วนที่เพิ่มสำหรับแผนนี้มีดังนี้

1. ทำ backend foundation และ golden tests ใน Phase 19 ให้ผ่านถึง 19.15 ก่อนเริ่มหน้าเว็บใน Phase 20
2. ห้ามติดตั้ง npm package นอกเหนือจากที่ `create-next-app` ใส่มาให้โดยไม่ถามก่อน
3. โค้ดเว็บอยู่ใน `web/`; การแก้ .NET, migration และ script ทำได้เฉพาะ step ที่ระบุ
4. ถ้า Next.js เวอร์ชันที่ติดตั้งมีชื่อไฟล์หรือ API ต่างจากแผน ให้ใช้ตามเวอร์ชันที่ติดตั้ง แล้วบอกว่าต่างตรงไหน
5. ห้ามแปลง pseudo-SQL จาก workbook แล้ว execute โดยตรง ทุก config ต้องผ่าน typed model, compiler และ allowlist เดียวกัน
6. Phase 21–28 ต้องทำตามลำดับและผ่าน Phase Gate ก่อนเริ่ม Phase ถัดไป ห้ามเปิด `ApiContract` delivery ก่อน Phase 24, ห้าม activate `JsonFile` ก่อนผ่าน Gate E ใน Phase 25, ห้ามเปิด profile administration ใน production ก่อนผ่าน Gate F ใน Phase 26, ห้าม publish Contract ผ่าน Admin ใน production ก่อนผ่าน Gate G ใน Phase 27 และห้ามเปิด Table Admin mutation ใน production ก่อนผ่าน Gate H ใน Phase 28
7. ทุก step ใหม่ต้องรักษา `LegacyTable` characterization tests ให้ผ่าน และห้ามเปลี่ยน destination ของ File Job ที่ pin Config Version ไปแล้ว
8. Schema endpoint, delivery endpoint, Output Root, authentication และ secret ต้องมาจาก server-owned profile ห้ามรับ raw URL/path/credential จาก browser
9. ห้าม dual-delivery production payload เดียวข้าม `LegacyTable`, `ApiContract` และ `JsonFile`; shadow test ใช้ stub/sandbox/output root แยกที่ไม่มี business side effect เท่านั้น
10. JSON ของ PA MTLS เป็น illustrative profile สำหรับ coverage ไม่ใช่ schema กลางและห้าม hard-code path ลง core Module

### คำสั่งตั้งต้นสำหรับ AI agent (copy ไปใช้ได้)

```text
อ่าน docs/web-config-plan.md, docs/learning-plan.md (เฉพาะหัวข้อ "วิธีใช้แผนนี้"),
docs/demo-plan.md หัวข้อ "ข้อสรุปรอบที่ 7–9", CONTEXT.md และ current-program-workflow.md หัวข้อ 6.2 กับ 8.2
ก่อน implement ให้ตรวจเครื่องตาม "Requirement ของเครื่อง" ในแผนนี้ ถ้าไม่ตรงให้หยุดและบอกฉันก่อน
ถ้าฉันสั่งเริ่ม Phase 19–28 ให้ทำเฉพาะ step แรกที่ยังไม่เสร็จของ Phase นั้น แล้วหยุดรอคำสั่งใหม่
ถ้าฉันสั่ง Step ที่ระบุเลข ให้ทำเฉพาะ step นั้น อธิบาย concept ก่อนเขียนโค้ด แล้วบอกวิธีตรวจผล
สำหรับ Phase 21–28 ให้อ่าน spec/spec-design-contract-api-legacy-delivery.md เพิ่มก่อนเริ่ม, ใน Phase 25 ให้อ่าน spec/spec-design-file-to-json-export.md เป็น historical reliability reference เท่านั้น, Phase 26–27 ต้องทำ design step ของ Phase ก่อนแก้ schema/runtime และ Phase 28 ต้องอ่าน docs/table-creation-old-vs-new.md พร้อมทำ Step 28.0 ก่อนเพิ่ม Inspector หรือ Admin mutation
ห้ามติดตั้ง package เพิ่มเอง ห้ามรับ raw URL/credential หรือ execute raw SQL จาก config และห้ามสร้าง git commit เอง
```

## ผลวิเคราะห์ `Mapping-PMIB-PRD.xlsx`

### ขนาดและรูปแบบข้อมูล

| Job | Source fields | Target rows | Direct map | Default | ไม่มี direct/default | Condition rows |
|---|---:|---:|---:|---:|---:|---:|
| PMIB-01 | 112 | 289 | 63 | 5 | 221 | 19 |
| PMIB-02 | 88 | 289 | 64 | 5 | 220 | 56 |

Condition ของ PMIB-01 แบ่งเป็น `COLUMN` 7 และ `MAPPING` 12 ส่วน PMIB-02 แบ่งเป็น `COLUMN` 6, `MAPPING` 25 และ `UNION` 25 ภายในพบ `CASE`, `ISNULL`, `LIKE`, `SUBSTRING`, `CONVERT`, current date, filter query และ cross-table lookup

### ความหมายของตัวอย่างที่ต้องรองรับ

| # | ตัวอย่าง | ความสามารถที่แท้จริง | รูปแบบใหม่ |
|---:|---|---|---|
| 1 | `CASE WHEN GarageType ...` | คำนวณ target field จากเงื่อนไข | `case` expression ที่ใช้ `eq` และ literal |
| 2 | `CASE WHEN cTotalPremium ... WHEN VehicleCode ...` | CASE ที่อ่านหลาย Source fields และใช้ first-match-wins | `case` expression; แต่ละ arm อ้าง field คนละตัวได้ |
| 3 | `(SELECT dbo.FormatRegNo(RegisterId))` | named transformation; syntax นี้ไม่ได้เป็น generic table lookup โดยตรง แม้ implementation ของ function อาจอ่าน DB | `formatRegNo@1` จาก server-owned function catalog |
| 4 | `ApplicationID NOT IN (SELECT ... MTI_ORIGINAL ...)` | เงื่อนไขคัดทั้ง output และ cross-table anti-lookup | output `emitWhen` + named lookup `mtiOriginalPolicyExists@1` |
| 5 | `MAPPING` + `UNION` ใน PMIB-02 | Source row เดียวสร้าง primary/CTP ได้มากกว่าหนึ่งผลลัพธ์ | output branches ที่มี stable `outputKey` และคืนผล `0..N` rows |

ข้อสรุปคือไม่ควรเพิ่มเพียง `operator`, `operand1`, `operand2` ใน `SourceToNormalizedRule` เดิม เพราะ nested CASE, multi-field condition, lookup และหลาย output จะทำให้ model แบน ๆ ใช้งานไม่ได้

## ผลวิเคราะห์ fixed-width จาก `Mapping-TIB-PRD.xlsx`

`Condition-TIB-01` มี 167 rules: `COLUMN` 117, `MAPPING` 25, `ROWNUM` 1 และ `UNION` 24 โดย 115 rules แรกหลังค่า `StatFlag` เป็น `SUBSTRING` ที่ map raw text record ไป Source fields

`ROWNUM/QUERY` เป็น common eligibility filter ก่อนประเมิน `MAPPING`/`UNION` จึง map เป็น `TransformationProgram.sourceWhen` หนึ่งจุด ส่วนเงื่อนไขเฉพาะ PMX/CTP ยังอยู่ใน `output.emitWhen`; ไม่สร้าง operator ชื่อ ROWNUM และไม่คัด Source row ทิ้งตอน File-to-Source เพราะ Source ต้องเก็บข้อมูลที่นำเข้าได้ครบเพื่อ reprocess

ข้อมูลจริงใน `isoMTI-N7780.txt` ยืนยัน layout ดังนี้:

| รายการ | ผลที่พบ |
|---|---|
| Encoding | UTF-8 ไม่มี BOM |
| จำนวน record | 23: Header `H` 1, Detail `D` 21, Trailer `T` 1 |
| Header/Trailer | ยาว 1,500 ตำแหน่งข้อความ |
| Detail | ทุกบรรทัดยาว 3,761 ตำแหน่งข้อความ |
| Detail byte length | 3,925–4,113 bytes เพราะข้อมูลไทยใช้ UTF-8 หลาย byte |
| Fixed-width rules | 115 fields, start 1 ถึง end 3,761 ต่อเนื่อง ไม่มี gap/overlap |
| ตำแหน่ง | start เป็น 1-based และค่าตัวที่สองเป็น length ตาม `.NET Substring(start - 1, length)` เดิม |

ตัวอย่างจาก Detail record แรก:

| Source field | Start | Length | ค่าที่อ่านได้หลัง trim เพื่อแสดงผล |
|---|---:|---:|---|
| `RecordType` | 1 | 1 | `D` |
| `CompanyCode` | 2 | 4 | `MTI` |
| `DateSent` | 6 | 8 | `20261006` |
| `ApplicationNo` | 14 | 10 | `A B1781228` |
| `FULLNAME_OWNER` | 42 | 100 | `บจก. โตโยต้าเฟรนส์ชิบ` |
| `MarketValuePrice` | 3702 | 10 | `0000791000` |
| `Suffix` | 3712 | 50 | `NYC100R-TEXGBT/B1 (2PS)` |

มี 21 slice rules ที่ระบุ `VALUEFORMAT = dd/MM/yyyy` แต่ raw sample เช่น `DateSent` เป็น `20261006`; format นี้จึงเป็น post-extraction conversion ที่ต้องตรึง expected value ใน 19.0 ไม่ใช่กติกานับตำแหน่งของ Input Layout นอกจากนี้ชื่อ `DateSent ` ใน workbook มี trailing space ขณะที่ typed contract ต้องอ้าง Source column แบบ exact; UI ใช้ dropdown ส่วน workbook importer ในอนาคตต้อง trim พร้อม warning ห้ามแก้เงียบ

ตำแหน่งต้องนับ **หลัง decode เป็นข้อความ ไม่ใช่ byte offset** ถ้าตัด raw UTF-8 bytes ตำแหน่งหลังข้อความภาษาไทยจะเลื่อนและได้ค่าผิด Contract รุ่นแรกจึงกำหนด `positionUnit = utf16CodeUnit` ให้ตรงกับ legacy .NET; newline ไม่อยู่ใน record และ v1 ปฏิเสธข้อความ non-BMP ที่ใช้ surrogate pair ด้วย `UnsupportedCharacterForPositionUnit` เพื่อไม่ให้ slice ครึ่งตัวอักษร

Case นี้อยู่ที่ **File-to-Source Input Layout** ไม่ใช่ Source-to-Normalized Transformation:

```text
Raw file bytes
  → decode ตาม encoding
  → classify H / D / T
  → fixed-width slice Detail record
  → Source row (เก็บ raw slice)
  → Transformation Program
  → 0..N Normalized outputs
```

`substring` จึงมีสองความหมายที่ต้องแยกชื่อใน model:

- `slice` ใน Fixed-width Input Layout: อ่านตำแหน่งจาก raw record ไป Source field
- `substring` ใน Transformation Program: ตัดค่าจาก Source field เพื่อคำนวณ Normalized field

ห้ามนำ 115 fixed-width rules ไปสร้าง expression 115 ตัวใน Transformation Program เพราะจะปะปน parsing กับ business transformation และทำให้ CSV/fixed-width ใช้ lifecycle คนละแบบ

### กลยุทธ์ Test Draft แบบ Coverage-driven

Workbook และไฟล์จริงใช้เพื่อค้นหา requirement และตรึง semantics เท่านั้น ไม่บังคับให้ draft ที่ใช้พัฒนา, preview หรือ acceptance ต้องคัดลอก 115 fields หรือ 21 Detail records ทั้งชุด ให้ใช้ข้อมูลสังเคราะห์จำนวนน้อยที่สุดที่ทำให้ coverage matrix ครบ และ reuse หนึ่ง record เพื่อครอบคลุมหลาย case ได้

Fixed-width draft หลักใช้ 4 physical records แบบ `H/D/D/T` และประมาณ 6 fields ตัวแทน: discriminator, ASCII ต้น record, ภาษาไทยหลาย byte กลาง record, padded blank, raw date `yyyyMMdd` และ field ท้าย record สอง Detail records ต้องมี byte length ต่างกันแต่ slice ตาม UTF-16 แล้วได้ตำแหน่งเดิม ส่วน error ใช้ fixture แยกหนึ่ง record ต่อกรณี invalid UTF-8, non-BMP, unknown record type, short และ long Detail เพื่อให้ failure reason ชัด

Transformation draft ใช้จำนวน Source rows ต่ำสุดที่ครอบคลุม direct/default, null/blank, CASE ทุก `WHEN` และ `ELSE`, ลำดับ premium ก่อน Vehicle fallback, LIKE/SUBSTRING/CONVERT, current date, `formatRegNo@1`, lookup exists/not exists, lookup ที่พบเฉพาะผลของ Source row ตัวเอง, `CLOANNUMBER` เดียวกันต่าง product, common `sourceWhen`, output filter และผล `0/1/2` outputs รวมทั้ง reprocess จาก 2 เหลือ 1 output ไม่เพิ่ม rows เพียงเพื่อให้จำนวนเท่าตัวอย่างใน workbook

หากต้องพิสูจน์ capacity 115 fields ให้สร้าง layout สังเคราะห์ด้วย test builder แยกจาก human-editable draft; full workbook/`isoMTI-N7780.txt` เป็น optional reference parity test ไม่ใช่ acceptance gate ประจำวัน

### ทางเลือกที่พิจารณา

| ทางเลือก | จุดแข็ง | จุดอ่อน | ข้อสรุป |
|---|---|---|---|
| เพิ่ม operator ลง rule เดิม | เปลี่ยนโค้ดน้อยสำหรับ CASE ง่าย ๆ | Interface ตื้น, nested expression/lookup/output branch รั่วไปทุก caller | ไม่เลือก |
| Transformation Module ที่รับ Row Job IDs แล้วจัดการครบ | Interface เล็กและมี Depth สูง | ผูก evaluator กับ DB/job orchestration มากไป ทำ preview และ pure tests ยาก | ใช้แนวคิด batch แต่ไม่ให้ Module เป็นเจ้าของ persistence |
| Generic typed AST + compile/evaluate | flexibility และ Locality สูง รองรับทุกตัวอย่าง | contract และ editor ซับซ้อนถ้าเปิดทุกอย่างพร้อมกัน | เลือกเป็น core แล้วจำกัด AST v1 |
| Progressive editor บน AST เดียวกัน | direct map ยังง่าย และเพิ่ม CASE/lookup/output ตามต้องการ | UI ต้อง map diagnostics กับ nested controls | เลือกเป็นหน้าเว็บ |

แบบที่เลือกเป็น hybrid: core ใช้ compile/evaluate ซึ่งแยกจาก persistence, Worker ส่ง batch เพื่อรวม lookup, และหน้าเว็บเริ่มจาก Direct field ก่อนเปิดความสามารถขั้นสูง ไม่มี evaluator คนละชุดสำหรับ Basic/Advanced mode

## ทิศทาง Input Layout ที่เลือก

สร้าง **Input Record Parser Module** เป็น deep Module คนละตัวกับ Transformation Program Module โดย API ใช้ compiler/preview และ Worker ใช้ parser implementation เดียวกัน:

```csharp
InputLayoutCompilationResult Compile(
    InputLayoutDefinition layout,
    TableDefinition sourceTable);

IAsyncEnumerable<InputRecordResult> ReadAsync(
    Stream input,
    CompiledInputLayout layout,
    CancellationToken cancellationToken);
```

Implementation อยู่ใน project เดิม `MappingDemo.Shared` ภายใต้ namespace/folder `MappingDemo.Shared.Input` ไม่สร้าง `MappingDemo.Input` project ใน Phase 19: API และ Worker ซึ่ง reference Shared อยู่แล้วใช้ Interface เดียวกันเพื่อ validate/preview และ parse runtime ส่วน Module รับ `Stream` จาก caller จึงไม่รู้ path, archive, job, Kafka หรือ database

Config Version เก็บ `inputLayout` แบบ discriminated union:

```text
InputLayoutDefinition
├─ Delimited
│  ├─ encoding / delimiter / preambleLines / headerPolicy
│  └─ fields[]: inputHeader → sourceColumn
└─ FixedWidth
   ├─ encoding / positionUnit
   ├─ recordRules[]
   │  ├─ key / discriminator / action / expectedLength
   │  └─ fields[]: sourceColumn / start / length
   ├─ recordSequence[]: recordRule / minOccurs / maxOccurs
   └─ unmatchedRecordPolicy
```

Delimited v1 รองรับ `preambleLines >= 0` และ header policy แบบไม่มี header หรือใช้หนึ่ง column-name row หลัง preamble; การประกอบชื่อคอลัมน์จากหลายแถวอยู่นอก scope รุ่นแรก

Fixed-width ไม่ hardcode ชื่อหรือจำนวน H/D/T แต่ใช้ ordered `recordSequence`: แต่ละรายการอ้าง `recordRule`, กำหนด `minOccurs >= 0` และ `maxOccurs` ซึ่งเป็นจำนวนที่ไม่น้อยกว่า min หรือ `null` เพื่อหมายถึงไม่จำกัด จำนวนและลำดับที่อ่านจริงต้องตรง sequence ก่อนจึง import ได้ TIB draft แรกใช้ rules ดังนี้:

- `header`: slice `(1, 1) = "H"`, expected length 1,500, action `Ignore`
- `detail`: slice `(1, 1) = "D"`, expected length 3,761, action `Import`
- `trailer`: slice `(1, 1) = "T"`, expected length 1,500, action `Ignore`
- sequence ตัวอย่าง: header `1..3`, detail `1..unbounded`, trailer `0..2`
- unmatched record เป็น File Import Error; ไม่เดาจากความยาวอย่างเดียว

Parser ต้อง decode stream ด้วย encoding ที่ Config Version pin ไว้ก่อน, ตัด newline ออก, classify record, ตรวจลำดับ/cardinality และ expected length แล้วจึง slice เฉพาะ `Import` record ทุก field เก็บ raw slice รวม padding ลง Source Table; trim/conversion เกิดใน Transformation Program เพื่อรักษาหลักฐานต้นทาง

Semantics ของ fixed-width v1 ตรึงไว้ดังนี้: decode แบบ strict UTF-8, รับ UTF-8 BOM ได้หนึ่งตัวเฉพาะต้น stream โดยไม่นับเป็นตำแหน่ง, รับทั้ง LF/CRLF, final line terminator หนึ่งชุดไม่สร้าง record เพิ่ม และ empty line อื่นเป็น physical record ที่ต้องผ่าน validation ตามปกติ ตำแหน่งนับแบบ 1-based UTF-16 code unit และปฏิเสธ non-BMP ทั้งไฟล์ก่อน slice `rowNumber` นับเฉพาะ `Import` record ส่วน `physicalLineNumber` นับทั้ง `Import`/`Ignore` รายละเอียดพร้อม golden examples อยู่ใน [phase-19-golden-cases.md](phase-19-golden-cases.md)

Config validation ต้องตรวจ encoding/position unit จาก allowlist, `preambleLines` ไม่ติดลบ, start/length เป็นจำนวนบวก, end ไม่เกิน expected length, Source column มีจริงและไม่ซ้ำ, discriminator ไม่กำกวม, sequence อ้าง rule ที่มีจริงและไม่ซ้ำ, min/max ถูกต้อง และทุก rule ถูกอ้างใน sequence ส่วน gap/overlap เป็น warning เพราะบาง layout อาจตั้งใจอ่านช่วงเดียวกันมากกว่าหนึ่ง field

Error แยกเป็น:

- Config error: layout/field/path ไม่ถูกต้อง ทำให้ create/activate ไม่ผ่าน
- File import validation error: decode ไม่ได้, record type ไม่รู้จัก, sequence/cardinality ไม่ตรง, expected length ไม่ตรงหรือ slice ไม่ได้ ทำให้ File Import Job ทั้งไฟล์เป็น `ImportFailed` ก่อนสร้าง Source row/Row Job/outbox และไม่ archive ว่าสำเร็จ

Worker ต้อง pre-scan snapshot ทั้งไฟล์ผ่าน decode/classify/length/slice validation ก่อนเขียน Source row แรก หากไม่ผ่านให้เก็บ snapshot ไว้สำหรับ retry และบันทึก error แรกใน `file_jobs.last_error` ด้วย stable error code จาก 19.0, physical line number, record type และ expected/actual value เช่น `UnexpectedRecordLength: physical line 17, record type D, expected 3761 UTF-16 code units, actual 3750.` โดยไม่เขียน `row_errors`; ตารางนั้นสงวนไว้สำหรับ Source-to-Normalized Row Error ที่มี Row Job แล้ว หากอนาคตต้องการ partial import และเก็บหลาย input errors ให้เพิ่ม `file_import_errors` พร้อมออกแบบ status/counts เป็น feature แยก

## ทิศทาง Transformation Program ที่เลือก

สร้าง **Transformation Program Module** เป็น deep Module โดยวาง seam ระหว่าง API/Worker กับ compiler และ evaluator:

```text
Transformation Program + Table schemas
                 │
                 ▼
          Compile / Validate ──► diagnostics + compiled program
                                      │
Source rows + evaluation time + lookup results
                                      │
                                      ▼
                                  Evaluate ──► 0..N outputs / row errors
```

Interface มีหน้าที่หลักเพียงสองอย่าง:

```csharp
CompilationResult Compile(
    TransformationProgram program,
    MappingSchema schema);

ValueTask<TransformationBatchResult> EvaluateAsync(
    CompiledTransformationProgram program,
    TransformationBatch batch,
    CancellationToken cancellationToken);
```

`CompiledTransformationProgram` เป็น opaque immutable value ผู้เรียกไม่ต้องรู้การเดิน expression tree, function dispatch, lookup batching หรือ conversion ภายใน API create/activate/preview และ Worker ต้องเรียก Module เดียวกันเพื่อไม่ให้ validation กับ runtime ตีความต่างกัน

### ตำแหน่ง Module และ dependency direction

Phase 19 ใช้ project เดิม `MappingDemo.Shared` และวางสอง deep Modules แยก namespace/folder โดยไม่สร้าง project ใหม่:

```text
MappingDemo.Shared
├─ Input
│  ├─ InputRecordModule + public contracts/results
│  └─ internal Delimited/FixedWidth compilers and readers
└─ Transformation
   ├─ TransformationModule + public contracts/results
   ├─ public dependency ports ที่มี production/test Adapters จริง
   └─ internal compiler/evaluator/profile/function implementations
```

`MappingDemo.Api` และ `MappingDemo.Worker` reference Shared ตามเดิมและเรียกผ่าน Module Interface เดียวกัน; ห้ามสอง Modules reference API/Worker หรือรู้จัก controller, HTTP, Kafka, job orchestration, archive path และ persistence transaction; Input Module ไม่ reference Transformation Module และกลับกัน API เป็นเจ้าของ endpoint/preview ส่วน Worker เป็นเจ้าของ orchestration และ transaction สำหรับ Source/Normalized/Job state

Public Interface มีเฉพาะ facade, config contracts, opaque compiled values, dependency ports ที่มีอย่างน้อย production + test Adapter และ result/diagnostic types ส่วน parser/compiler/evaluator/helper เป็น `internal`; Concrete Adapter ที่ API และ Worker ต้องใช้ร่วมกัน เช่น PostgreSQL named lookup อยู่ใต้ `MappingDemo.Shared.Transformation.Adapters` แต่ persistence ที่เป็น orchestration-specific ยังคงอยู่ใน caller; Tests ใช้ Interface เดียวกับ callers และ assert observable result โดยไม่อ้าง helper ภายใน หากภายหลังระบบโต สามารถแยกสอง namespace เป็น projects ได้โดยไม่เปลี่ยน Interface

### Model ระดับ Config Version

```text
ConfigVersion
├─ inputLayout             Delimited หรือ FixedWidth
└─ transformationProgram
   ├─ languageVersion
   ├─ semanticsProfile
   ├─ sourceWhen           optional common BooleanExpression
   └─ outputs[]
      ├─ outputKey         เช่น pmx, ctp
      ├─ emitWhen          optional BooleanExpression
      └─ fields[]
         ├─ normalizedColumn
         ├─ inputFormat    optional สำหรับ implicit text-to-date/dateTime
         └─ value          ValueExpression
```

`ValueExpression` รุ่นแรก:

- `source` — อ่าน Source field แบบ raw โดยไม่ trim
- `metadata` — อ่านค่าที่ระบบอนุญาต เช่น file name, row number, evaluation time
- `literal` — ค่าคงที่ที่ระบุ type ชัดเจน
- `coalesce` / `defaultIfBlank`
- `case` — first-match-wins และมี `else`
- `trim` — ตัด whitespace ซ้ายและขวาของ text โดยรักษา `null`
- `substring`
- `convert` — แปลง string เป็น decimal/integer/date/dateTime/guid/boolean
- `currentDate` — แปลง evaluation time ที่ตรึงไว้ไปเป็น `Asia/Bangkok` แล้วคืน typed `Date`; ไม่อ่าน wall clock หรือ timezone ของเครื่องใหม่ทุก retry
- `call` — เรียก named transform ที่อยู่ใน allowlist เช่น `formatRegNo@1`

`BooleanExpression` รุ่นแรก:

- `eq`, `ne`, `gt`, `gte`, `lt`, `lte`
- `isNull`, `isBlank`, `notBlank`
- `like`, `in`, `notIn`
- `and`, `or`, `not`
- `exists`, `notExists` ผ่าน named lookup เท่านั้น

ไม่มี node สำหรับ raw SQL, table name, SQL function name, reflection, loop, script หรือ URL จากผู้ใช้

### ตัวอย่าง config แบบย่อ

```json
{
  "languageVersion": 1,
  "semanticsProfile": "sqlserver-legacy-v1",
  "outputs": [
    {
      "outputKey": "pmx",
      "fields": [
        {
          "normalizedColumn": "CWORKSHOP",
          "value": {
            "kind": "case",
            "cases": [
              {
                "when": {
                  "kind": "eq",
                  "left": {
                    "kind": "trim",
                    "value": {
                      "kind": "source",
                      "field": "GarageType"
                    }
                  },
                  "right": {
                    "kind": "literal",
                    "type": "text",
                    "value": "อู่ในเครือ"
                  }
                },
                "then": {
                  "kind": "literal",
                  "type": "text",
                  "value": "ซ่อมอู่"
                }
              },
              {
                "when": {
                  "kind": "eq",
                  "left": {
                    "kind": "trim",
                    "value": {
                      "kind": "source",
                      "field": "GarageType"
                    }
                  },
                  "right": {
                    "kind": "literal",
                    "type": "text",
                    "value": "อู่ห้าง"
                  }
                },
                "then": {
                  "kind": "literal",
                  "type": "text",
                  "value": "ซ่อมห้าง"
                }
              }
            ],
            "else": {
              "kind": "literal",
              "type": "text",
              "value": ""
            }
          }
        }
      ]
    }
  ]
}
```

JSON ข้างบนใช้ shape ที่เลือกสำหรับ contract; serialization tests ใน 19.6 ต้องตรึงชื่อ property นี้ก่อนใช้เป็น public contract

### Semantics profile

`semanticsProfile` เป็น required string identifier ที่อ้าง server-owned immutable preset ไม่ใช่ object ที่ config ปรับ culture, collation หรือ timezone รายตัว รุ่นแรกมี `sqlserver-legacy-v1` สำหรับ PMIB และ TIB โดยกำหนดความหมายดังนี้:

- `null` ต่างจาก empty string; comparison กับ `null` คืน `null` ตาม three-valued logic (`false AND null = false`, `true AND null = null`, `true OR null = true`, `false OR null = null`, `NOT null = null`)
- `sourceWhen`/`emitWhen` ผ่านเฉพาะเมื่อได้ `true`; `false` หรือ `null` คือไม่ผ่าน ส่วน `coalesce` จัดการเฉพาะ `null` และ `isBlank`/`defaultIfBlank` จัดการ `null` หรือข้อความว่างหลัง trim
- text `eq`/`ne`/`in`/`notIn` และ `like` ใช้ ordinal case-insensitive comparison ที่ deterministic ไม่อิง PostgreSQL/OS collation; `like` รองรับ SQL Server-style `%`, `_`, `[abc]`, `[a-z]`, `[^abc]` และไม่เปิด regex
- decimal parse ด้วย `InvariantCulture`, อนุญาตเครื่องหมายบวก/ลบกับจุดทศนิยม และไม่รับ comma คั่นหลัก; เปรียบเทียบด้วยชนิด decimal ไม่ใช้ floating point
- Date/DateTime parse แบบ exact ด้วย `InvariantCulture`; ค่า default คือ `yyyy-MM-dd` สำหรับ Date และ `yyyy-MM-ddTHH:mm:ss` สำหรับ DateTime ส่วน format อื่นต้องเลือกจาก allowlist ผ่าน `inputFormat`
- `currentDate` แปลง persisted `evaluation_at` instant ด้วย IANA timezone `Asia/Bangkok` ตามข้อสรุป B8

ชื่อ profile นี้หมายถึง legacy subset ที่ระบบประกาศรองรับ ไม่ได้จำลอง SQL Server ทุก collation/operator Compiler ต้องปฏิเสธ identifier ที่ไม่รู้จัก; API capabilities คืน identifier กับคำอธิบายแบบ read-only และห้ามเปลี่ยน behavior หลัง release ภายใต้ชื่อเดิม หาก semantics เปลี่ยนให้เพิ่ม profile ใหม่ เช่น `sqlserver-legacy-v2` โดย `languageVersion` คุมรูป AST ส่วน `semanticsProfile` คุมความหมายตอนประเมิน

Truth table และ known-literal expected values ของ profile นี้ตรึงอยู่ใน [phase-19-golden-cases.md](phase-19-golden-cases.md) และ `tests/MappingDemo.Tests/Fixtures/Phase19/sqlserver-legacy-v1.golden.json`

### Invariants และ semantics

- `outputKey` ต้อง unique และ stable ข้าม Config Version; identity ของผลลัพธ์ใช้ key ไม่ใช้ตำแหน่งใน array
- Normalized column หนึ่งถูกกำหนดได้ครั้งเดียวต่อ output และ required columns ต้องมี assignment ครบทุก output ที่อาจ emit
- Expression อ้าง Source/metadata ได้ แต่ยังไม่ให้อ้าง target field อื่น จึงไม่เกิด dependency ตามลำดับ field
- Boolean comparison ใช้ `left`/`right` ที่เป็น expression เสมอและไม่รองรับ shorthand `field`/`value`; `literal` ทุกตัวต้องระบุ `type`
- CASE ใช้ first-match-wins; `emitWhen = false/null` หมายถึง skip output ไม่ใช่ Row Error
- `sourceWhen = false/null` หมายถึง Source row ถูกกรองก่อนทุก output และเป็น `Filtered`; output-specific `emitWhen` ประเมินหลังจากนั้น
- แยก `null` กับ empty string ให้ชัด: `coalesce` จัดการ null ส่วน `isBlank/defaultIfBlank` จัดการ null หรือข้อความว่างหลัง trim
- `source` คืน raw text เสมอ; `trim` รับเฉพาะ text, ตัด whitespace ทั้งสองด้าน และเป็น null-preserving ส่วน input type อื่นเป็น Config error พร้อม exact path
- Target Assignment Coercion ประเมิน expression ก่อน แล้ว trim ผลชนิด text; target Text รับค่าที่ trim แล้ว ส่วน Date/DateTime/Decimal/Integer/Boolean/Guid convert จาก text ตาม target type; blank หลัง trim เป็น `Required` สำหรับ required column และเป็น null สำหรับ optional column
- Compiler แทรก coercion เป็น compiled operation โดยไม่แก้ JSON ที่ persist; `inputFormat` ใช้ได้เฉพาะ expression ชนิด text ที่ assign ไป Date/DateTime มิฉะนั้นเป็น Config error ส่วน conversion failure เป็น Row Error ที่ชี้ path ของ field assignment
- Explicit `trim`/`convert` ยังใช้สำหรับ intermediate expression, predicate, CASE และ function argument; ถ้า expression คืนชนิดตรงกับ target แล้ว assignment ไม่ convert ซ้ำ
- Source fields ยังคงเป็น raw text ตาม CONTEXT; การแปลง `COLUMN` จาก workbook ให้ย้ายมาเป็น expression/metadata ใน Transformation Program ไม่แก้ Source row ย้อนหลัง
- Reprocess อ่าน Source row ที่มีอยู่และไม่อ่านไฟล์เดิมใหม่ ดังนั้น `inputLayout` ของ Config Version ที่เลือกไม่มีผลต่อ reprocess; layout ใหม่มีผลเฉพาะ File Import Job ใหม่ที่ pin version นั้น
- `evaluation_at` เป็น instant ที่เก็บใน `timestamptz`; `currentDate` แปลง instant นี้ด้วย IANA timezone `Asia/Bangkok` แล้วคืนเฉพาะ typed `Date` ห้ามใช้ `DateTime.Now` หรือ timezone ของเครื่อง Worker; retry ใช้ค่าเดิม ส่วน reprocess สร้าง evaluation time ใหม่
- ถ้า output ใดเกิด data error ให้การประเมิน Source row นั้นเป็น `Invalid` ทั้งชุดและไม่แก้ Current Normalized Result เดิม
- ถ้าไม่มี output ผ่าน filter ให้เป็น `Filtered` ไม่ใช่ `Invalid`
- `Filtered` เป็น terminal success: ไม่นับเป็น `Done` แต่ไม่ทำให้ File Job เป็น `CompletedWithErrors`; หากมี `Pending` ให้เป็น `InProgress` ก่อน และมีเพียง `Invalid`/`Failed` ที่ทำให้เป็น `CompletedWithErrors`
- Current Normalized Result เป็นชุด `0..N` outputs ต่อ Source row โดย identity คือ `(source_row_id, output_key)` ไม่ใช่ business key เช่น `CLOANNUMBER`; Source row เดียวจึงมีทั้ง `pmx` และ `ctp` ที่ใช้ `CLOANNUMBER` เดียวกันได้
- Reprocess ที่สำเร็จต้อง reconcile desired output set ใน transaction เดียว: upsert output ปัจจุบันและลบ stale output ที่ไม่ emit แล้ว; ถ้า desired set ว่างให้ลบ outputs เดิมทั้งหมดและ mark Row Job เป็น `Filtered` ใน transaction เดียว ส่วน `Invalid`/`Failed` ห้าม reconcile และต้องเก็บชุดเดิม
- จำกัด AST depth, node count, CASE arms, output count และ lookup keys เพื่อกัน config ที่ใช้ทรัพยากรเกินขอบเขต
- Config Version pin logic แต่ lookup ใช้ live committed business state; `mtiOriginalPolicyExists@1` ใช้ business key `(systemId, productName, loanNumber)` และตัดทุกผลที่มี `source_row_id = currentSourceRowId` ออกจากคำตอบ จึงทำให้ retry/reprocess ของ Source row เดิมไม่ block ตัวเอง ขณะที่ Source row อื่นที่มี product + loan เดียวกันยัง match และ loan เดียวกันคนละ product ไม่ match
- กรณี business rule ไม่แยก product ให้ใช้ `mtiOriginalLoanExists@1` ด้วย business key `(systemId, loanNumber)`; ห้ามส่ง `productName` เพื่อทำให้ความหมายต่างจาก lookup แบบ 3-key ชัดเจน ทั้งสอง lookup ตัด `currentSourceRowId` แบบเดียวกัน

### ชนิด error

| ชนิด | ตัวอย่าง | ผลต่อการทำงาน |
|---|---|---|
| Config error | unknown column/operator/function, type mismatch, AST ลึกเกิน | สร้างหรือ activate version ไม่ได้; คืน path เช่น `outputs[1].fields[3].value.cases[0].when` |
| Row Error | invalid date/decimal, required value ไม่มี | Row Job เป็น `Invalid`; ระบุ `outputKey`, field, expression path และ reason |
| Dependency/system error | lookup timeout, DB unavailable, implementation fault | Row Job เป็น `Failed` และ retry ได้ |

### Dependencies และ Adapter

| Dependency | ประเภท | แนวทาง |
|---|---|---|
| Input Layout compiler/fixed-width parser | In-process | รับ `Stream`, ทดสอบด้วย `MemoryStream`; encoding/position unit มาจาก allowlist |
| Input file snapshot | Local-substitutable | Worker เปิด snapshot file; tests ใช้ in-memory stream โดย Module ไม่ค้น path เอง |
| AST compiler/evaluator/conversion | In-process | อยู่หลัง seam เดียวและทดสอบผ่าน Interface ของ Module |
| PostgreSQL config/source/result | Local-substitutable | ใช้ Postgres จริงใน integration test; persistence อยู่นอก evaluator |
| `formatRegNo@1` | In-process | demo stand-in แบบ null-preserving และ trim เฉพาะหัวท้ายตาม 19.0; ไม่มีคำกล่าวอ้าง legacy parity และหากได้ source จริงที่ behavior ต่างต้องเพิ่ม version ใหม่ |
| `mtiOriginalPolicyExists@1` | Local-substitutable | ใน demo อ่าน PMIB Normalized table เดียวกับที่ Transformation Program เขียน; config เห็น logical capability เท่านั้น ส่วน Adapter resolve physical table ที่เชื่อถือได้และ in-memory Adapter ใช้ใน unit tests |
| `mtiOriginalLoanExists@1` | Local-substitutable | อ่านตารางเดียวกัน แต่เทียบเฉพาะ `SYSTEM_ID + CLOANNUMBER` สำหรับ config ที่ไม่แยก `CPRODUCTNAME`; ใช้ Adapter และ current-row exclusion ชุดเดียวกัน |
| เวลา | In-process input | Worker ส่ง persisted `evaluation_at` ซึ่งเป็น instant; evaluator ใช้ timezone `Asia/Bangkok` จาก semantics profile และ test ส่งค่าคงที่ |

ใน demo ให้ถือ PMIB Normalized table เป็น `MTI_ORIGINAL` ตามความหมายใน workbook และสร้างผ่าน Tables API โดย `configure-demo.ps1`; config เลือก `mtiOriginalPolicyExists@1(systemId, productName, loanNumber)` หรือ `mtiOriginalLoanExists@1(systemId, loanNumber)` โดยไม่รู้ table/SQL ส่วน runtime เติม `currentSourceRowId` จาก Row Job context ให้ lookup request เอง ห้ามรับ identity นี้จากค่าที่ผู้ใช้กำหนด

Production Adapter ต้อง resolve physical table จาก server-owned metadata แล้ว query แบบ parameterized ด้วย business key ที่ lookup เลือก พร้อมคืน `source_row_id` ที่ match เพื่อให้ caller ตัด `currentSourceRowId` ออก การ resolve ทั้ง batch ต้อง deduplicate keys และใช้จำนวน query คงที่ต่อชนิด lookup ไม่ query ต่อ field/row; `configure-demo.ps1` สร้าง composite index `(SYSTEM_ID, CPRODUCTNAME, CLOANNUMBER)` และ `(SYSTEM_ID, CLOANNUMBER)` ด้วย trusted setup SQL หลัง Tables API สร้างตาราง และตอน activate ต้องตรวจ required columns/index ของ lookup ที่ config ใช้

Lookup อ่าน committed state ล่าสุดและ cache ได้เฉพาะภายใน batch นั้น จึงรองรับ retry และ reprocess ของ Source row เดิม แต่ไม่ใช่ concurrency uniqueness guarantee: สอง Source rows ใหม่ที่ชน key และประมวลผลพร้อมกันอาจผ่านทั้งคู่ หากต้องห้ามกรณีนี้ต้องเพิ่ม unique constraint/locking เป็น requirement แยก

## ผลกระทบต่อ model ปัจจุบัน

- `FileToSourceRule(CsvHeader, SourceColumn)` เปลี่ยนเป็น `InputLayoutDefinition`; CSV เดิมเป็น `Delimited` และ TIB เป็น `FixedWidth`
- `CsvRecordReader`/`FileImportHandler` เรียก Input Record Parser Module เพื่อให้ทั้งสอง layout คืน Source row contract เดียวกัน
- `SourceToNormalizedRule(SourceColumn, NormalizedColumn, Format)` เปลี่ยนเป็น Transformation Program; direct mapping เป็น expression ชนิด `source`
- `RowNormalizer` ที่คืน dictionary เดียวเปลี่ยนเป็น compiler/evaluator ที่คืน `0..N` outputs
- Normalized table เปลี่ยนจาก `UNIQUE(source_row_id)` เป็น `UNIQUE(source_row_id, output_key)`
- `NormalizedRowWriter` เปลี่ยนจาก upsert หนึ่งแถวเป็น atomic output-set reconciliation
- `RowNormalizeHandler` โหลด Source fields จาก compiled dependencies และ resolve lookup แบบ batch โดยแนบ `currentSourceRowId` จาก Row Job context เพื่อ self-exclusion
- เพิ่ม `RowJobStatus.Filtered`; calculator และ File Job API แยก `PendingRows`, `DoneRows`, `FilteredRows`, `InvalidRows`, `FailedRows` โดยถือ Done/Filtered เป็น terminal success
- `row_errors` เก็บเฉพาะ Source-to-Normalized errors ที่อ้าง Row Job; File import validation error เก็บที่ `file_jobs.last_error` และไม่สร้าง partial Source rows
- generated Source table เพิ่ม `physical_line_number integer NOT NULL`; `row_number` เป็นลำดับ Import record ส่วน physical line นับทุกบรรทัดรวม preamble/header/trailer ที่ Ignore
- ขยายชนิด Normalized column สำหรับ workbook จริงเป็นอย่างน้อย `Integer`, `DateTime`, `Guid` และ metadata ของ Text/Decimal เช่น max length, precision/scale; Source columns ยังเก็บ raw text
- สำหรับ repo demo นี้ migration ของ generated tables ใช้ `reset-demo.ps1`; การ migrate production table ที่มีข้อมูลอยู่นอก scope

### Compatibility และ cutover policy ของ demo

Phase 19 ใช้ clean breaking cutover เพราะ repo นี้ประกาศ workflow แบบ reset/reconfigure และไม่มี production data หรือ external client contract อยู่ใน scope; ไม่สร้าง compatibility Adapter, dual-read/write, legacy payload converter หรือ evaluator/parser สองชุด `mapping_config_versions` ใน baseline migration ต้องเหลือเพียง `input_layout` และ `transformation_program`; request/response รุ่นใหม่ไม่รับ `fileToSource`/`sourceToNormalized` และ runtime ไม่อ่านคอลัมน์ `file_to_source`/`source_to_normalized`

เมื่อ baseline schema เปลี่ยนต้องหยุด API/Worker แล้วรัน `reset-demo.ps1` ก่อน `configure-demo.ps1`; script ต้องสร้าง Config Version ใหม่ด้วย contract ใหม่ทั้งหมด ของเดิมอย่าง `FileToSourceRule`, `SourceToNormalizedRule`, `CsvRecordReader` และ `RowNormalizer` ให้ลบเมื่อ replacement ถูกเชื่อมใน step ที่กำหนด ห้ามเก็บ fallback path ไว้; golden/acceptance tests รักษา observable behavior ของ direct CSV mapping เดิมผ่าน Modules ใหม่ แต่ไม่รักษา JSON/schema shape เดิม

Rollback สำหรับ demo คือย้อน code ไป revision ก่อน Phase 19 แล้ว reset/reconfigure ใหม่ ไม่ใช่ migrate data กลับ; หากอนาคตต้องรักษา production Config Versions ให้สร้าง one-time migration tool พร้อมแผนตรวจผลเป็นงานแยกหลัง AST นิ่ง ไม่เพิ่ม runtime compatibility layer ใน Phase 19

## ประสบการณ์บนหน้าเว็บ

หน้า editor แบ่งเป็น **Input Layout** และ **Transformation** โดยใช้ progressive disclosure:

1. Input Layout เลือก `Delimited` หรือ `Fixed width`; แบบเดิมยังเลือก CSV header → Source column ได้ง่าย
2. Delimited กำหนด preamble lines และ header policy; Fixed width กำหนด encoding, position unit, record rules, ordered sequence พร้อม min/max occurrences, expected length และตาราง Source column/start/length/end โดยไม่ hardcode H/D/T
3. วาง sample text หรือเลือก UTF-8 sample file เพื่อ preview raw slice/trimmed display และ highlight ช่วงที่เลือก โดย browser อ่านไฟล์เป็น text สำหรับ request เท่านั้นและไม่แก้ไฟล์ต้นฉบับ
4. Transformation มี common `Source when` builder สำหรับกฎแบบ TIB `ROWNUM/QUERY`, tabs ของ output branch เช่น `Main`, `CTP` และปุ่ม clone/add output
5. แต่ละ output มี visual `Emit when` builder สำหรับ AND/OR และ predicate; lookup เลือกจาก catalog ไม่พิมพ์ query
6. หนึ่งแถวต่อ Normalized column ใช้ `ValueExpressionEditor` แบบ recursive ตัวเดียวกัน โดยมุมมองเริ่มต้นแสดง Direct field, Constant, Current date และ Not mapped ส่วน Advanced value เปิด Metadata, Trim, Substring, Convert, Coalesce, Default if blank, CASE และ Named transform
7. Editor ของ Trim/Substring/Convert รับ value expression ลูก, Coalesce/Default if blank รับ expression หลักกับ fallback, CASE เพิ่ม WHEN/THEN/ELSE ได้ และ Named transform ใช้ editor เดียวกันสำหรับ arguments; จำกัด depth/node/CASE arms ตาม capabilities
8. ทุกตำแหน่งที่รับ expression เช่น output field, CASE result และ function argument ต้อง reuse editor เดียวกันและรักษา payload path ได้แม้ controls ซ้อนกัน
9. แสดง dependency summary ว่าใช้ fields/functions/lookups ใด และแสดง warning เรื่อง index/complexity
10. End-to-end preview แสดง decoded record → Source fields → output branches/filtered/errors โดยไม่เขียน DB
11. JSON preview เป็น read-only; ไม่มี raw SQL textarea

## เป้าหมายและขอบเขต

**อยู่ใน scope**

- Versioned Input Layout สำหรับ Delimited/CSV และ UTF-8 fixed-width text
- configurable Delimited preamble/header และ Fixed-width record classification/sequence/cardinality, expected record length, 1-based UTF-16 code-unit slice และ raw padding preservation
- Input Layout compile/validation และ end-to-end preview จาก raw record ไป Source row
- Typed/versioned Transformation Program และ compiler/evaluator ชุดเดียวสำหรับ API/Worker
- direct, literal, current date, CASE, multi-field predicate, trim, coalesce/blank, LIKE, IN, substring และ typed conversion
- named `formatRegNo@1` และ named anti-lookup สำหรับตัวอย่าง PMIB
- output filter และหลาย output branches ต่อ Source row
- validation ตอน create/activate, capabilities endpoint และ preview แบบไม่เขียน DB
- หน้าเว็บดู/สร้าง Config, clone/activate version และ visual transformation editor
- golden tests แบบ coverage-driven ครอบคลุม behavior จากตัวอย่าง 1–5 โดยไม่คัดลอกจำนวน rows จาก workbook
- golden tests ใช้ fixed-width draft ขนาดเล็กครอบคลุมตำแหน่งต้น/กลาง/ท้าย, ภาษาไทยหลาย byte, header/detail/trailer และ error classes; capacity 115 fields ใช้ generated test แยกเมื่อจำเป็น

**นอก scope (ทำภายหลัง)**

- Raw SQL/script/custom code และการเลือก table/function/URL ตามอำเภอใจ
- importer ที่แปลงทั้ง workbook เป็น config อัตโนมัติ; ให้ทำเป็น one-time migration tool หลัง AST นิ่ง
- fixed-width แบบ byte offset, packed decimal/binary record, variable-length segment และหลาย encoding ที่ยังไม่มี golden sample
- รองรับ operator ทุกชนิดของ legacy โดยไม่มี golden example
- หน้า CRUD ของ function/lookup catalog; catalog เป็น server-owned code/config
- migration ของ production data/table, auth, deploy และ production build
- หน้าจัดการ Tables, File Job, retry/reprocess; ยังใช้ API/script เดิม

## เครื่องมือและ Requirement ของเครื่อง

| เรื่อง | เลือก/ต้องการ | หมายเหตุ |
|---|---|---|
| Backend | .NET และ PostgreSQL ตาม learning-plan | API/Worker/tests เดิมต้องรันได้ |
| Frontend | Next.js App Router + TypeScript + Tailwind | client components และ `useState`; ไม่ลง UI kit |
| Node.js/npm | เวอร์ชันที่ Next.js ล่าสุดรองรับ | ตรวจ `node -v`, `npm -v`, `npm ping` |
| API | `http://localhost:5181` | `GET /health` ต้องได้ 200 |
| Web | port `3000` ว่าง | ใช้ Next rewrite `/api/*` ไป API |

### เวอร์ชันที่ใช้จริง (กรอกระหว่างทำ)

| รายการ | กรอกใน step | เวอร์ชัน |
|---|---|---|
| Node.js | 20.0 | |
| npm | 20.0 | |
| Next.js | 20.1 | |
| React | 20.1 | |
| TypeScript | 20.1 | |
| Tailwind CSS | 20.1 | |

## Endpoint ที่หน้าเว็บใช้

| งาน | Endpoint |
|---|---|
| รายการ/สร้าง Config | `GET /mapping-configs`, `GET /tables`, `POST /mapping-configs` |
| รายละเอียด/activate | `GET /mapping-configs/{id}`, `GET /tables/{id}`, `POST /mapping-configs/{id}/versions/{n}/activate` |
| Capability catalog | `GET /mapping-capabilities` — input formats/encodings/position units, target conversion types/date formats และ transformation operators/functions/lookups |
| Validate draft | `POST /mapping-configs/{id}/versions/validate` |
| Preview draft | `POST /mapping-configs/{id}/versions/preview` |
| สร้าง Version | `POST /mapping-configs/{id}/versions` |

Validate และ preview ใช้ Input Layout/Transformation compilers ตัวเดียวกับ create/activate; preview รับอย่างใดอย่างหนึ่งระหว่าง sample text file กับ sample Source rows แล้วคืน decoded record, extracted Source fields และ transformation outputs โดยห้ามเขียน Source/Normalized/Job tables

### Preview sample contract v1

Sample file ไม่ใช้ multipart และไม่อัปโหลด path ให้ server ฝั่ง browser อ่าน UTF-8 file แล้วส่ง text ใน JSON:

```json
{
  "evaluationAt": "2026-10-08T10:00:00Z",
  "sample": {
    "kind": "textFile",
    "fileName": "sample.txt",
    "content": "H...\nD...\nT..."
  },
  "inputLayout": {},
  "transformationProgram": {}
}
```

`fileName` ใช้แสดงผลเท่านั้นและห้ามนำไป resolve/open path บน server ส่วน `content` รองรับเฉพาะ UTF-8, มีขนาดหลัง encode ไม่เกิน `1 MiB` (`1,048,576` bytes) และ parse ได้ไม่เกิน 50 input records หากเกินข้อใดให้ reject ทั้ง request ไม่ truncate เพราะจะทำให้ sequence/header/trailer ให้ผลหลอก Endpoint ตั้ง request-body limit `3 MiB` เพื่อเผื่อ draft config/JSON escaping และคืน ValidationProblem ที่ path `sample.content` ด้วย stable code `SampleTooLarge`, `TooManySampleRecords` หรือ `InvalidSampleEncoding`

Browser ตรวจ `File.size`, อ่าน `ArrayBuffer`, decode ด้วย `TextDecoder("utf-8", { fatal: true })` และ pre-check จำนวนบรรทัดก่อนส่ง; API เป็นผู้ตรวจ authoritative limits อีกครั้ง จากนั้น re-encode content เป็น UTF-8 `MemoryStream` และเรียก Input Module implementation เดียวกับ Worker Preview ทำงานใน memory เท่านั้น ห้าม persist sample ลง disk/database หรือ log เนื้อหา Response คืนได้สูงสุด 50 record results

ข้อจำกัดนี้ครอบคลุม `isoMTI-N7780.txt` ซึ่งมีประมาณ 87 KB/23 records หากอนาคตต้องรักษา raw bytes, รองรับ encoding อื่นหรือไฟล์ใหญ่ ให้เพิ่ม version/endpoint แบบ multipart แยก ไม่เปลี่ยน contract v1

---

## Phase 19 — Backend Input Layout และ Transformation Config

- [x] **19.0 ตรึง semantics และสร้าง coverage-driven golden cases**
  - ทำ: ใช้ PMIB-01/02 และ TIB-01 เป็น requirement source แล้วสร้าง minimal synthetic fixtures ตาม coverage matrix; ตรึง `sqlserver-legacy-v1` ให้ครอบคลุม null/empty/blank, three-valued boolean logic, ordinal case-insensitive comparison, LIKE `%`/`_`/bracket patterns, invariant decimal/exact date formats และ `Asia/Bangkok`; Transformation ครอบคลุม direct/default, implicit assignment trim/conversion, explicit trim, CASE ทุก branch, SUBSTRING/CONVERT, current date, `FormatRegNo`, anti-lookup แบบ other-source/self-exclusion/same-loan-different-product และ primary + CTP ส่วน Fixed-width ใช้ `H/D/D/T` กับ fields ตัวแทนต้น/กลาง/ท้าย, ภาษาไทย, padding และ date พร้อม structural fixture ที่มีหลาย header/trailer โดยไม่ใช้ workbook เป็น runtime input
  - เข้าใจ: fixture วัด behavior ไม่วัดว่าคัดลอก workbook ได้ครบจำนวน; `sqlserver-legacy-v1` คือ subset ที่ประกาศเอง ไม่ใช่ SQL Server emulator ทุก collation/operator และ behavior ของ identifier นี้ห้ามเปลี่ยนหลัง release ส่วน fixed-width ต้องตรึง encoding, position unit, newline, padding, short/long-record policy และความหมายของ `VALUEFORMAT=dd/MM/yyyy` เมื่อ raw เป็น `yyyyMMdd`
  - ตรวจ: coverage matrix ทุกช่องชี้ไปยัง fixture อย่างน้อยหนึ่งข้อและไม่มี fixture ที่มีไว้เพียงให้จำนวนเท่า workbook; truth table ของ null, case-insensitive equality/IN/LIKE และ wildcard ทุกชนิดตรง profile; decimal จุดผ่านแต่ comma คั่นหลักไม่ผ่าน; default/explicit Date/DateTime format ตรง expected; `currentDate` ให้ `2026-10-08` ที่ `2026-10-08T16:59:59Z` และให้ `2026-10-09` ที่ `2026-10-08T17:00:00Z`; positive fixed-width fixture มี Import 2/Ignore 2 และ byte length ของ Detail ต่างกันแต่ fields ต้น/กลาง/ท้ายไม่เลื่อน; negative fixture คืน stable error code พร้อม physical line/record details ตรงกรณี
  - ผล: ตรึง test seams, transport/date semantics, stable input error codes, demo stand-in ของ `formatRegNo@1`, coverage/source traceability และ fixtures แล้วใน [phase-19-golden-cases.md](phase-19-golden-cases.md) กับ `tests/MappingDemo.Tests/Fixtures/Phase19/`; JSON parse ผ่าน, fixed-width records/slices ตรง expected และ baseline tests ผ่าน 73/73

- [x] **19.1 สร้าง typed Input Layout contract และ compiler**
  - ทำ: สร้าง namespace/folder `MappingDemo.Shared.Input` ใน project เดิม โดยมี public Input Layout contracts/results, `InputRecordModule` facade และ opaque `CompiledInputLayout`; implementation ของ compiler เป็น `internal`; เปลี่ยน `FileToSourceRule` เป็น discriminated `DelimitedInputLayout`/`FixedWidthInputLayout`; Delimited เพิ่ม preamble lines กับ none/single-row header policy ส่วน Fixed-width เพิ่ม encoding, position unit, record discriminator/action/expected length, ordered `recordSequence` พร้อม min/max occurrences, field slice และ exact-path diagnostics
  - เข้าใจ: Input Layout เป็น Interface ของ File-to-Source parsing และเป็นส่วนหนึ่งของ Config Version; Module ไม่รู้ path/job/database และ `slice` ไม่ใช่ transformation `substring`; ไม่สร้าง `MappingDemo.Input` project ใน Phase 19
  - ตรวจ: API/Worker มองเห็นเฉพาะ public Interface/contracts ไม่เห็น compiler internals; serialization round-trip ของ CSV เดิม, Delimited ที่มีหลาย preamble lines และ compact fixed-width draft ผ่าน; compiler ปฏิเสธ unknown encoding/unit, preamble ติดลบ, start/length ไม่บวก, end เกิน record, target column ไม่มี/ซ้ำ, discriminator กำกวม, sequence อ้าง rule ผิด/ซ้ำ และ min/max ไม่ถูกต้อง
  - ผล: เพิ่ม public `InputRecordModule.Compile`, typed Delimited/Fixed-width contracts, opaque `CompiledInputLayout` และ exact-path error/warning diagnostics ใน `MappingDemo.Shared.Input`; compiler implementation เป็น `internal` และ tests เรียกผ่าน Module Interface เท่านั้น legacy `FileToSourceRule` ยังเป็น runtime/persistence path ชั่วคราวจนถึง breaking cutover และการลบ legacy path ใน 19.3

- [x] **19.2 ทำ Fixed-width Input Record Parser**
  - ทำ: implement `internal` Delimited/FixedWidth readers หลัง `InputRecordModule` Interface เดียวกัน; ย้าย/รวม `CsvRecordReader` เดิมเข้า Input Module เพื่อไม่ให้มี parser สองชุด; fixed-width reader decode UTF-8 ก่อนนับตำแหน่ง, ตัด newline, classify ตาม record rules, ตรวจ sequence/cardinality และ length ของแต่ละ rule แล้ว slice ด้วย 1-based UTF-16 code units; คืน raw field values กับ physical line number และไม่ trim ก่อนเขียน Source
  - เข้าใจ: Detail มี 3,761 text positions แต่ 3,925–4,113 bytes จึงห้ามใช้ byte offset; parser เป็น in-process pure logic หลังรับ decoded text และทดสอบผ่าน Interface ของ Module
  - ตรวจ: tests เรียกผ่าน Input Module Interface ไม่อ้าง reader/compiler helper; compact `H/D/D/T` fixture ได้ Import 2/Ignore 2 และ `H/H/D/D/T/T` ผ่านเมื่อ cardinality อนุญาต; missing/excess/out-of-order records ไม่ผ่านด้วย stable error code; fields ต้น/กลาง/ท้ายตรง golden, Detail ที่มีภาษาไทยและ byte length ต่างกันไม่ทำให้ตำแหน่งเลื่อน, invalid UTF-8/non-BMP/unknown type/short/long Detail คืน error class ที่กำหนด
  - ผล: เพิ่ม public `InputRecordModule.ReadAsync` แบบ all-or-nothing พร้อม raw values, import row number, physical line และ stable structured error; reader ภายในรองรับ strict UTF-8/BOM, LF/CRLF, 1-based UTF-16 slicing, classification, sequence/cardinality/length validation และอย่างน้อย 115 fields; Delimited reader รองรับ preamble/header/quoted multiline ผ่าน Module เดียวกัน และ `CsvRecordReader` เดิมเหลือเป็น compatibility facade ที่ delegate เข้า implementation นี้จนถึง Worker cutover ใน 19.4

- [x] **19.3 Persist Input Layout และปรับ Config Version API**
  - ทำ: replace `file_to_source` ด้วย `input_layout jsonb NOT NULL` ใน baseline migration, ลบ legacy property/model path ที่เกี่ยวข้อง, update create/clone/get request/response/service และ `configure-demo.ps1` ให้รับเฉพาะ Input Layout ใหม่ แล้วหยุด API/Worker และรัน `reset-demo.ps1` ก่อน configure ใหม่
  - เข้าใจ: เป็น breaking cutover ของ demo ไม่มี compatibility Adapter หรือการแปลง Config Version เดิม; Config Version เป็นเจ้าของ Input Layout แบบ immutable และ Worker ต้องโหลด layout จาก version ที่ File Job pin ไว้ ไม่ใช่จาก active version ล่าสุด
  - ตรวจ: schema ไม่มี `file_to_source`, request แบบ `fileToSource` ใช้งานไม่ได้, create/clone/get version round-trip Delimited/Fixed-width layout ครบ, version ที่ pin ไว้โหลด layout เดิมได้หลัง activate version ใหม่ และ reset + script demo ผ่าน
  - ผล: baseline `mapping_config_versions` ใช้ `input_layout jsonb NOT NULL`; create/get/list/activate contracts และ service persist/deserialize typed `InputLayoutDefinition` พร้อม required `inputLayout` จึง reject payload legacy ที่มีเฉพาะ `fileToSource`; validation ใช้ `InputRecordModule.Compile` ชุดเดียวกับ runtime และ prefix path ด้วย `inputLayout`; `configure-demo.ps1` กับ acceptance-version script ส่ง Delimited layout ใหม่แล้ว ทั้งนี้ยังต้องหยุด API/Worker แล้วรัน `reset-demo.ps1` ก่อนทดสอบ configure กับฐานข้อมูลจริง เพราะเป็น intentional baseline cutover และ Worker integration อยู่ใน 19.4

- [x] **19.4 เชื่อม Input Record Parser เข้า File Import Worker**
  - ทำ: `FileImportHandler` เลือก compiled layout ตาม Config Version และใช้ parser เดียวสำหรับ Delimited/FixedWidth; เพิ่ม `physical_line_number integer NOT NULL` ใน generated Source DDL/reserved identifiers และให้ `SourceRowWriter` เขียนค่านี้; pre-scan snapshot ผ่าน decode/classify/sequence/cardinality/length/slice validation ก่อนเขียนแถวแรก แล้วจึงเขียนเฉพาะ Import records; หาก validation ไม่ผ่านให้ตั้ง `ImportFailed`, บันทึก stable diagnostic ใน `file_jobs.last_error`, เก็บ snapshot สำหรับ retry และไม่สร้าง Source row/Row Job/outbox; ปรับ duplicate/archive/status โดยไม่ทำ regression CSV
  - เข้าใจ: Header/Trailer ที่ Ignore ไม่ใช่ Source row และไม่เพิ่ม Row Number; `row_errors` ใช้เฉพาะ normalization ที่มี Row Job ส่วน input validation error ทำให้ fail ทั้งไฟล์และไม่ archive ว่าสำเร็จ
  - ตรวจ: DDL/writer tests มี `physical_line_number`; CSV acceptance เดิมและ Delimited ที่มีหลาย preamble lines ผ่าน, `H/H/D/D/T/T` สร้าง Source rows 1–2 ที่ physical lines 3–4; invalid UTF-8/unknown type/sequence/cardinality/short/long/slice error แต่ละกรณีได้ `ImportFailed` กับ code/physical line/record type/expected-actual ที่กำหนด และมี Source rows, Row Jobs, outbox messages เท่ากับศูนย์
  - ผล: `FileImportHandler` โหลด typed `input_layout` จาก Config Version ที่ File Job pin ไว้และ compile กับ Source schema ก่อน pre-scan snapshot ผ่าน `InputRecordModule`; input error บันทึก structured diagnostic ใน `last_error`, คง snapshot ไว้สำหรับ retry และหยุดก่อน Source/Row Job/outbox side effect; writer เขียนเฉพาะ `Import` records พร้อม `row_number` และ `physical_line_number`; generated Source DDL/identifier reservations รองรับ metadata ใหม่ และลบ `CsvRecordReader`, `CsvRecord`, `FileToSourceRule` รวมถึง legacy parser path แล้ว

- [x] **19.5 ขยาย Table Definition และ result identity**
  - ทำ: เพิ่ม `Integer`, `DateTime`, `Guid` และ metadata ที่จำเป็นสำหรับ length/precision/scale; เพิ่ม `output_key` ใน Normalized table และ unique `(source_row_id, output_key)`; ปรับ DDL/tests และกำหนดว่า demo ต้อง reset DB
  - เข้าใจ: workbook ใช้ type มากกว่า demo และ `UNION` ทำให้ `source_row_id` อย่างเดียวไม่เป็น identity ที่ถูกต้อง
  - ตรวจ: DDL tests ครอบคลุม type ใหม่และหนึ่ง source row insert output key ต่างกันได้สองแถว แต่ key เดิมซ้ำไม่ได้
  - ผล: Table Definition รองรับ `Integer`/`DateTime`/`Guid` และ optional `length`/`precision`/`scale` ครบทั้ง API, persistence, Worker schema loading และ baseline migration; generated DDL map เป็น `integer`, `timestamp without time zone`, `uuid`, `varchar(n)` และ `numeric(p, s)`; Normalized table มี `output_key NOT NULL` กับ unique `(source_row_id, output_key)` และ writer upsert ด้วย composite identity โดย normalization เดิมใช้ stable key `default` จนกว่าจะรองรับ multi-output ใน phase ถัดไป ทั้งนี้เป็น baseline cutover จึงต้องหยุด API/Worker และรัน `reset-demo.ps1` ก่อนทดสอบกับฐานข้อมูล demo จริง

- [x] **19.6 สร้าง typed Transformation Program contract**
  - ทำ: สร้าง namespace/folder `MappingDemo.Shared.Transformation` ใน project เดิม โดยมี public Transformation contracts/results, `TransformationModule` facade และ opaque `CompiledTransformationProgram`; compiler/evaluator/profile/function implementations เป็น `internal`; สร้าง model ของ program/output/field assignment/value/boolean expression, optional `inputFormat`, `languageVersion`, required string `semanticsProfile` และ JSON polymorphism แบบ explicit discriminator; เพิ่ม serialization round-trip tests โดยใช้ `sqlserver-legacy-v1`
  - เข้าใจ: AST เป็น declarative data ไม่ใช่ executable code; `languageVersion` คุม shape ส่วน `semanticsProfile` เป็น immutable server-owned preset ที่คุมความหมาย ไม่ใช่ option object; Module ไม่รู้ controller/Kafka/job/persistence transaction; direct mapping เป็น `source` expression ไม่ต้องมี pipeline พิเศษอีกชุด; comparison operands เป็น expression และ literal ระบุ type เสมอ
  - ตรวจ: API/Worker มองเห็นเฉพาะ public Interface/contracts ไม่เห็น compiler/evaluator internals; serialize/deserialize ตัวอย่าง 1–5, field assignment ที่มี/ไม่มี `inputFormat` และ `trim(source(...))` กลับมาเท่ากัน; payload ที่ profile หายหรือเป็น object, shorthand `field`/`value`, literal ที่ไม่มี `type` และ unknown `kind` ถูกปฏิเสธด้วย path ที่ชัดเจน
  - ผล: เพิ่ม deep Module `MappingDemo.Shared.Transformation` ที่เปิด `TransformationModule.Compile`, schema/diagnostic/result contracts และ opaque `CompiledTransformationProgram` โดยเก็บ compiler เป็น `internal`; ตรึง JSON AST แบบ explicit `kind` สำหรับ program/output/field assignment, value expressions และ boolean expressions ครบ scope รุ่นแรก, ใช้ optional `languageVersion`/`inputFormat` กับ required string `semanticsProfile`, typed literal type และไม่เปิด raw SQL/script node; round-trip ตัวอย่าง workbook 1–5 และ discriminator ทุกชนิดผ่าน พร้อม reject missing/object profile, comparison shorthand, missing literal type, unmapped property และ unknown kind ด้วย `JsonException` path ที่ตรวจได้ ส่วน static schema/type/profile validation และ compiled coercion ยังอยู่ใน 19.8 ตามขอบเขตเดิม

- [x] **19.7 Persist Transformation Program และปรับ Config Version API**
  - ทำ: replace `source_to_normalized` ด้วย `transformation_program jsonb NOT NULL` ใน baseline migration, ลบ legacy property/model path ที่เกี่ยวข้อง, update create/clone/get request/response/service และ `configure-demo.ps1` ให้รับเฉพาะ Transformation Program ใหม่ แล้วหยุด API/Worker และรัน `reset-demo.ps1` ก่อน configure ใหม่
  - เข้าใจ: เป็น breaking cutover ของ demo ไม่มี compatibility Adapter, dual-read/write หรือ legacy AST converter; Config Version pin ทั้งโครงสร้าง AST และ semantic identifiers และ Worker ต้องประเมิน program จาก version ที่ Row Job pin ไว้
  - ตรวจ: schema ไม่มี `source_to_normalized`, request แบบ `sourceToNormalized` ใช้งานไม่ได้, create/clone/get version round-trip program ครบทุก node ใน contract, version ที่ pin ไว้โหลด program เดิมได้หลัง activate version ใหม่ และ reset + script demo ผ่าน
  - ผล: baseline `mapping_config_versions` ใช้ `transformation_program jsonb NOT NULL` แทน `source_to_normalized`; create/get/list/activate contracts, controller, service และ reprocess version loading serialize/deserialize typed `TransformationProgram` โดยตรงและ reject payload legacy ที่มี `sourceToNormalized`; `ConfigVersionValidator` เรียก `TransformationModule.Compile` seam เดียวกับ runtime สำหรับ phase ถัดไป; demo และ acceptance-version scripts ส่ง direct-mapping AST พร้อม `sqlserver-legacy-v1`, stable output key `default` และ assignment `inputFormat` แล้ว ทั้งนี้ตรวจ contract/migration/regression และ PowerShell syntax แล้ว แต่ยังต้องหยุด API/Worker, รัน `reset-demo.ps1` และรัน `configure-demo.ps1` กับฐานข้อมูลจริง เพราะเป็น intentional baseline cutover; Normalize Worker จะเปลี่ยนจาก legacy evaluator ใน 19.13 ตามลำดับแผน

- [x] **19.8 Compiler และ static validation**
  - ทำ: compile program กับ Source/Normalized schemas, resolve `languageVersion` และ `semanticsProfile` จาก server-owned catalogs, infer type/nullability, หา dependencies, แทรก Target Assignment Coercion สำหรับ trim/text-to-target conversion, validate `inputFormat`/required/duplicate targets/function/lookup/version และ enforce complexity limits; คืน diagnostic code + exact path
  - เข้าใจ: coercion อยู่ใน compiled program ไม่แก้ persisted JSON; create, activate, preview และ runtime ต้องใช้ compiler/profile implementation เดียวกัน; compiled program cache ด้วย Config Version/hash และห้ามเปลี่ยน behavior ของ profile identifier เดิม
  - ตรวจ: unit tests ครอบคลุม unknown/missing profile, implicit Text/Date/DateTime/Decimal/Integer/Boolean/Guid coercion, already-typed value ที่ไม่ convert ซ้ำ, `inputFormat` กับ target/input ที่ใช้ไม่ได้, unknown field, `trim` ของ input ที่ไม่ใช่ text, type mismatch, required หาย, duplicate output/target, AST ลึกเกิน และ path ตรง payload
  - ผล: `TransformationCompiler` resolve `languageVersion` ว่างเป็นรุ่น 1 และยอมรับเฉพาะ immutable profile `sqlserver-legacy-v1`; compile expression ทุกชนิดเป็น type/nullability พร้อมรวบรวม Source dependencies และเก็บ Target Assignment Coercion แบบ `None`/trim text/trim-and-convert ไว้ใน opaque compiled program โดยไม่แก้ persisted AST; ตรวจ Source/Normalized fields, required/duplicate output และ target, literal/metadata/operator/function `formatRegNo@1`/lookup `mtiOriginalPolicyExists@1` signatures, exact Date/DateTime `inputFormat` และจำกัด outputs, fields, collection size, expression nodes/depth พร้อมคืน stable diagnostic code และ path ตรง payload; เพิ่ม compiler tests แบบ public seam ครบ coercion ทั้ง 7 ชนิด, already-typed/no mutation และ negative cases ตาม checklist โดย full suite ผ่าน 174/174, formatter และ `git diff --check` ผ่าน (มีเพียง line-ending warnings เดิมของ worktree)

- [x] **19.9 Evaluator สำหรับ pure expressions**
  - ทำ: รองรับ source/metadata/literal, trim, coalesce/defaultIfBlank, CASE, comparison, blank, AND/OR/NOT, LIKE, IN, substring, convert และ Target Assignment Coercion โดย implement `sqlserver-legacy-v1` ตาม null truth table, ordinal case-insensitive text/LIKE, invariant decimal และ exact date formats ที่ตรึงไว้; ประเมิน common `sourceWhen` ก่อน output `emitWhen`
  - เข้าใจ: `source` คืน raw text; explicit `trim` ใช้ระหว่าง expression ส่วน assignment trim text และ convert ตาม target; blank required เป็น Row Error, blank optional เป็น null; evaluator คืนผลและ Row Errors ไม่เขียน DB; CASE short-circuit แบบ first-match-wins
  - ตรวจ: golden tests ครอบคลุม raw source ที่ยังมี padding, implicit assignment ของ padded/blank/null/date/decimal text, explicit `trim` ของ padded/empty/null text, conversion error path, ตัวอย่าง 1–2 และ operator ใน workbook โดยไม่สร้าง SQL
  - ผล: เพิ่ม public `TransformationModule.EvaluateAsync` และ batch/result contracts ที่คืนแต่ละแถวเป็น `Done`/`Filtered`/`Invalid`, `0..N` outputs และ Row Errors โดย evaluator ภายในเป็น pure และไม่มี persistence; รองรับ pure value/boolean nodes ครบ scope, three-valued AND/OR/NOT กับ predicate ที่ผ่านเฉพาะ `true`, CASE first-match/short-circuit, ordinal case-insensitive comparison/IN และ SQL Server-style LIKE `%`/`_`/set/range/negated set แบบ deterministic; Target Assignment Coercion trim raw text แล้วแปลง Text/Date/DateTime/Decimal/Integer/Boolean/Guid ด้วย invariant/exact format, แยก required blank กับ optional null และคืน stable reason พร้อม `outputKey`/field/expression path; `sourceWhen` หยุดก่อน `emitWhen` และ data error ของ output ใดทำให้ทั้งแถว `Invalid`; tests ครอบคลุม golden IDs `TR-A01..A04`, `TR-C01`, `TR-E01..E02`, null/empty/coalesce/default, truth table, operators, LIKE และ valid/invalid conversion โดย full suite ผ่าน 218/218, formatter และ `git diff --check` ผ่าน (มีเพียง line-ending warnings เดิมของ worktree); `currentDate`/named transform/lookup คงไว้ทำใน 19.10–19.11 ตาม scope

- [x] **19.10 Current time และ named transform**
  - ทำ: เพิ่ม schema migration `row_jobs.evaluation_at timestamptz NOT NULL DEFAULT now()`, metadata/evaluation time และ function catalog ที่ pin version; initial/reprocess กำหนด instant ตอนสร้าง Row Job แต่ retry รักษาค่าเดิม; evaluator แปลง instant ด้วย IANA timezone `Asia/Bangkok` จาก semantics profile แล้วให้ `currentDate` คืน typed `Date`; implement `formatRegNo@1` เป็น pure C# ตาม demo stand-in ที่ตรึงใน 19.0 โดยไม่อ้าง legacy parity
  - เข้าใจ: ห้ามใช้ `DateTime.Now`, local timezone ของ Worker หรือค่าที่อ่านใหม่ตอน retry; config ระบุชื่อ logical capability เท่านั้น, `SELECT dbo...` ไม่อยู่ใน config และ retry ต้องได้ current date ค่าเดิม
  - ตรวจ: migration/schema test ผ่าน; boundary `2026-10-08T16:59:59Z → 2026-10-08` และ `2026-10-08T17:00:00Z → 2026-10-09` ผ่านทั้ง evaluator/preview; retry ใช้ `evaluation_at` เดิมแม้ข้ามเที่ยงคืนกรุงเทพฯ ส่วน reprocess ได้ instant ใหม่; golden tests `TR-F01..F03` ของ demo `formatRegNo@1` ผ่าน และ unknown/version ผิด compile ไม่ผ่าน
  - ผล: เพิ่ม migration `0005_row_job_evaluation_time.sql` ให้ `row_jobs.evaluation_at` เป็น persisted instant พร้อม `DEFAULT now()`; Initial/Reprocess ซึ่งสร้าง row ใหม่จึงได้ instant ตอนสร้างงาน ส่วน Retry อัปเดต row เดิมและไม่แตะคอลัมน์นี้; เพิ่ม `EvaluationAt` เป็น input บังคับของ `TransformationSourceRow`, ให้ `evaluationTime` metadata และ `currentDate` ใช้ instant เดียวกันผ่าน immutable profile `sqlserver-legacy-v1` กับ IANA `Asia/Bangkok` โดยไม่อ่าน wall clock/local timezone; รวม identifier ของ profile/timezone/function/lookup ไว้ใน server-owned catalog และ dispatch เฉพาะ `formatRegNo@1` ซึ่ง null-preserving, trim เฉพาะหัวท้ายและรักษาข้อความภายในโดยไม่เรียก SQL พร้อม reject `formatRegNo@2`; evaluator boundary และ preview-equivalent seam ให้วันที่ตรง `16:59:59Z → 2026-10-08`, `17:00:00Z → 2026-10-09`, golden `TR-F01..F03` ผ่าน ส่วน HTTP preview จะต่อ seam เดียวกันและ echo instant ใน 19.14 ตาม scope; migration/evaluator/compiler/full regression ผ่าน 226/226, formatter และ `git diff --check` ผ่าน (มีเพียง line-ending warnings เดิมของ worktree)

- [x] **19.11 Named lookup และ batch resolution**
  - ทำ: เพิ่ม lookup catalog/port ที่ Transformation Module เป็นเจ้าของสำหรับ `mtiOriginalPolicyExists@1`; วาง PostgreSQL Adapter ที่ API/Worker ใช้ร่วมกันใต้ `MappingDemo.Shared.Transformation.Adapters` และใช้ in-memory Adapter ใน tests; logical arguments จาก config คือ `systemId`, `productName`, `loanNumber` และ runtime แนบ `currentSourceRowId`; production Adapter resolve PMIB Normalized table จาก trusted metadata, query แบบ parameterized เป็น batch และคืน matching `source_row_id`; ให้ `configure-demo.ps1` สร้าง PMIB Source/Normalized ผ่าน Tables API และสร้าง composite index `(SYSTEM_ID, CPRODUCTNAME, CLOANNUMBER)` ด้วย trusted setup SQL แล้วตรวจ required columns/index ตอน activate
  - เข้าใจ: PMIB Normalized table คือ `MTI_ORIGINAL` ของ demo; lookup ตัด matching row ที่ `source_row_id` เท่ากับ current Source row, cache ได้เฉพาะใน batch และอ่าน live committed state จึงไม่ใช่กลไกรับประกัน uniqueness ระหว่างงาน concurrent; timeout เป็น system error ที่ retry ได้ ไม่ใช่ Row Error
  - ตรวจ: golden anti-lookup ตามตัวอย่าง 4 ผ่าน; retry และ reprocess ของ Source row เดิมไม่ block ตัวเอง, Source row อื่นที่มี product + loan เดียวกันถูกพบ, loan เดียวกันคนละ product ไม่ match; SQL injection input ไม่เปลี่ยนโครง query, จำนวน query คงที่ตาม batch และ PostgreSQL integration test ใช้ composite index ที่กำหนด
  - ผล: เพิ่ม public lookup port/catalog และ `currentSourceRowId` runtime context ใน Transformation Module; evaluator deduplicate logical keys แล้วเรียก resolver หนึ่งครั้งต่อ batch, ตัดเฉพาะ match ของ Source row ปัจจุบัน และปล่อย timeout/dependency failure เป็น system error; PostgreSQL Adapter resolve `mti_original` กับ required columns จาก trusted `table_definitions`/`table_columns`, ใช้ metadata query + parameterized `unnest` query คงที่ 2 ครั้งต่อ batch และ activation ตรวจชนิด table/columns/composite-index จริง; API/Worker ลงทะเบียน Adapter ร่วมกัน ขณะที่ tests ใช้ in-memory resolver; `configure-demo.ps1` สร้าง `source_pmib`/`mti_original` ผ่าน Tables API, สร้าง trusted index แล้ว activate PMIB config; anti-lookup 4 กรณี, batch dedup, timeout, dependency diagnostics, injection payload และ PostgreSQL execution plan ผ่าน พร้อม PowerShell syntax และ full regression 234/234 (integration test ใช้ isolated schema และ composite index จริง)

- [x] **19.12 รองรับ `0..N` outputs และ atomic reconcile**
  - ทำ: เพิ่ม schema migration ให้ `row_errors` มี nullable `output_key`, non-null `expression_path` และเปลี่ยน `field` เป็น nullable; evaluator คืนหลาย output พร้อม `outputKey`; writer atomic reconcile desired output set โดย upsert current outputs และลบ stale outputs รวมถึงลบทั้งหมดเมื่อ desired set ว่าง; mark `Done`/`Filtered` ใน transaction เดียวกับ reconcile แต่ `Invalid`/`Failed` ไม่แตะ Current Normalized Result; เพิ่ม `RowJobStatus.Filtered`, `FilteredRows` ใน count query/`FileJobResponse` และปรับ `NormalizationStatusCalculator` ให้ Done/Filtered เป็น terminal success
  - เข้าใจ: legacy `UNION` คือ output branch เพิ่ม ไม่ใช่ string operator; `CLOANNUMBER` เดียวมีทั้ง `pmx`/`ctp` ได้เพราะ identity ใช้ `(source_row_id, output_key)`; `Filtered` คือ successful empty result จึงลบ outputs เดิม ไม่ใช่ Done/Invalid/Failed; Pending มี precedence เป็น `InProgress` และเฉพาะ Invalid/Failed ทำให้ `CompletedWithErrors`
  - ตรวจ: migration/schema test ผ่าน; field error ระบุ output/field/path, `emitWhen` error มี output แต่ field เป็น null และ `sourceWhen` error มี output/field เป็น null แต่ยังมี path; calculator tests ครอบคลุม all-Done, all-Filtered, Done+Filtered, Pending+Filtered, Filtered+Invalid และ Filtered+Failed; `GET /file-jobs/{id}` คืน `FilteredRows`; ตัวอย่าง 5 ทำให้ Source row/`CLOANNUMBER` เดียวได้ทั้ง `pmx`/`ctp`, reprocess จากสอง outputs เหลือเฉพาะ pmx, เหลือเฉพาะ ctp และเหลือศูนย์ได้ถูกต้อง; Filtered ซ้ำเป็น idempotent, Invalid/Failed เก็บชุดเดิม และ failure กลาง transaction rollback ทั้ง outputs/status
  - ผล: เพิ่ม migration ขยาย `row_errors` ด้วย nullable `output_key`/`field` และ required `expression_path` พร้อม backfill ข้อมูลเดิม; evaluator coverage ยืนยันหลาย output และ context ของ field/emit/source errors; writer reconcile desired output set ด้วย composite identity แล้วลบ stale/all outputs ก่อนตั้ง `Done`/`Filtered` ใน transaction เดียวกัน ขณะที่ failure rollback ทั้ง output set และ status; File Job count/response แยก `FilteredRows` และ calculator ถือ Done/Filtered เป็น terminal success โดย Pending ยังมี precedence; PostgreSQL integration tests ยืนยันการเปลี่ยน `2→1→1→0`, Filtered ซ้ำ, Invalid/Failed คงผลเดิม, rollback กลางชุด, schema จริง และ API count พร้อม full regression 244/244

- [x] **19.13 เชื่อม Transformation Module เข้า Worker**
  - ทำ: `RowNormalizeHandler` compile/cache ตาม Config Version, โหลด Source fields จาก dependencies, resolve lookup เป็น batch โดยแนบ `currentSourceRowId` จาก Row Job context, evaluate และส่งผลให้ writer; persist `row_errors.output_key`/nullable field/`expression_path`; ปรับ retry/reprocess/idempotency ให้รองรับหลาย output และ self-exclusion; ลบ `SourceToNormalizedRule`, `RowNormalizer` และ legacy serializer/evaluator path หลัง tests ใหม่ผ่าน
  - เข้าใจ: Worker reference `MappingDemo.Shared` และเรียก `MappingDemo.Shared.Transformation` ผ่าน Interface; Kafka, job orchestration และ persistence transaction อยู่ภายนอก Module แต่ business semantics อยู่หลัง seam เดียว
  - ตรวจ: direct-mapping characterization tests ให้ observable results เดิมผ่าน Module ใหม่โดยไม่ deserialize contract เก่า, repository ไม่เหลือ runtime reference ถึง legacy rules/evaluator, Row Error API คืน output/nullable field/expression path ครบ, retry งานเดิมและ reprocess ที่สร้าง Row Job ใหม่แต่ชี้ Source row เดิมไม่ถูก lookup ของตัวเองกรองทิ้ง และ end-to-end PMIB fixture ให้ status/count/output ตรง expected
  - ผล: `RowNormalizeHandler` โหลด typed `transformation_program` จาก Config Version ที่ Row Job pin ไว้, compile/cache ด้วย `config_version_id`, โหลดเฉพาะ Source dependencies ผ่าน public Module facade และส่ง file/row/physical-line/evaluation metadata พร้อม `currentSourceRowId` เข้า evaluator; Done/Filtered reconcile หลาย outputs แบบ atomic, Invalid persist `output_key`/nullable `field`/`expression_path`, dependency/system failure retry 4 ครั้งแล้วเป็น Failed โดยไม่แตะ Current Result และ terminal message ซ้ำเป็น idempotent; File Job errors/latest history คืน context ใหม่ครบ; ลบ `SourceToNormalizedRule`, `RowNormalizer`, legacy converter/result และ tests เดิมทั้งหมด โดยไม่เหลือ runtime reference; PostgreSQL PMIB fixture ยืนยัน `pmx+ctp→pmx→Filtered`, self-exclusion, other-source match, invalid/failed preservation, Row Error API และ aggregate counts พร้อม integration 5/5, full regression 224/224 และ formatter ผ่าน

- [x] **19.14 เพิ่ม capabilities, validate และ preview endpoints**
  - ทำ: เพิ่มสาม endpoint ตามตาราง, capability catalog คืน immutable semantics profiles พร้อม read-only summary รวมทั้ง target conversion types/date formats สำหรับ `inputFormat`, ใช้ Input Layout/Transformation compiler และ evaluator ชุดเดียวกัน; preview รับ discriminated sample แบบ `textFile` JSON หรือ sample Source rows พร้อม `evaluationAt` แบบ ISO 8601 ที่มี `Z`/UTC offset, จำกัด text `1 MiB`/50 parsed records และ endpoint body `3 MiB`, echo instant ที่ใช้จริง และห้ามเขียน DB/disk หรือ log sample content; API ส่ง enum เป็น string ด้วย `JsonStringEnumConverter`
  - เข้าใจ: API reference `MappingDemo.Shared` และใช้ Module Interfaces เดียวกับ Worker; text file ถูก re-encode เป็น UTF-8 `MemoryStream`, `fileName` เป็น display metadata ไม่ใช่ server path และ request ที่เกิน limit ต้อง reject ไม่ truncate; capability catalog ทำให้ web ไม่ hard-code input format/encoding/position unit/operator/function/lookup ที่ backend ไม่รองรับ
  - ตรวจ: capabilities คืน `sqlserver-legacy-v1` กับ summary และ target conversion types/date formats ตรง compiler; `evaluationAt` ที่ไม่มี offset ถูกปฏิเสธ; preview echo instant และให้ `currentDate` ตรง boundary ของ `Asia/Bangkok`; invalid profile/`inputFormat`/draft คืน exact paths; valid 87 KB/23-record TIB sample ผ่าน, 1 MiB พอดีผ่าน, เกิน 1 byte/record ที่ 51/request body เกิน limit ถูกปฏิเสธด้วย `SampleTooLarge`/`TooManySampleRecords`/HTTP 413 โดยไม่ truncate; filename traversal ไม่ถูกเปิดเป็น path, log ไม่มี content, จำนวน file/Source/Normalized/Job rows และไฟล์บน disk ไม่เปลี่ยน และ API enum เป็นชื่อ
  - ผล: เพิ่ม `GET /mapping-capabilities`, draft validate และ in-memory preview สำหรับ `textFile`/`sourceRows` โดยใช้ Shared compilers/evaluator และ lookup self-exclusion เดียวกับ Worker; capability catalog กับ compiler อ้าง constants/limits/date formats ชุดเดียวกัน, preview บังคับ ISO 8601 offset, UTF-8 1 MiB/50 records และ body 3 MiB พร้อม exact diagnostic paths/stable codes โดยไม่เปิด `fileName` หรือ persist sample; response echo evaluation instant, raw decoded record, Source fields และ transformation result และ API serialize enum เป็นชื่อโดยยังรักษา lower-camel enum contract ที่มี converter เฉพาะ; real TIB 87 KB/23 records, exact 1 MiB/50 records, Asia/Bangkok boundary, traversal/lookup cases และ HTTP 413 ผ่าน พร้อม full PostgreSQL regression 238/238 และ formatter ผ่าน

- [x] **19.15 ตรวจรับ backend และ handoff ด้วย coverage-driven fixtures**
  - ทำ: สร้าง Config Version ขนาดเล็กที่ครอบคลุม semantics จากตัวอย่าง PMIB 1–5 และ fixed-width layout + transformations; รัน golden/unit/integration/end-to-end tests โดยใช้จำนวน rows/fields ต่ำสุดตาม coverage matrix; เมื่อผ่านให้อัปเดตเอกสาร backend ที่เกี่ยวข้องและติ๊ก Phase 19 ใน `learning-plan.md`
  - เข้าใจ: step นี้เป็น phase gate; ต้องผ่านก่อนจึงถือว่า backend contract พร้อมให้หน้าเว็บตั้งค่าได้จริงและเริ่ม Phase 20 ได้ ส่วน regression gate รักษา behavior ไม่ใช่ legacy JSON/schema compatibility
  - ตรวจ: reset แล้ว schema/Config API มีเฉพาะ contract ใหม่และ `configure-demo.ps1` ผ่าน; direct CSV ไม่ regression ผ่าน Input/Transformation Modules ใหม่โดยไม่มี legacy parser/evaluator, implicit assignment trim/conversion ให้ผลเท่าพฤติกรรมเดิม, Delimited preamble/header policy ถูกต้อง, fixed-width ได้ Import 2/Ignore 2, multi-header/trailer sequence และ cardinality ผ่าน/ไม่ผ่านตาม config, fields ตัวแทนตรงตำแหน่ง, CASE ทุก branch ตรง expected, demo `formatRegNo@1` ตรง golden cases โดยไม่อ้าง legacy parity; lookup ครอบคลุม not found, other-source found, self-only ignored และ same-loan-different-product; retry/reprocess ไม่ block ตัวเอง, `CLOANNUMBER` เดียวมี `pmx`/`ctp` ได้ และ reprocess ผล `2→1→0` outputs reconcile identity/Current Normalized Result ถูกต้อง
  - ผล: reset volume/runtime state แล้ว migrate/configure demo ผ่านด้วย Orders และ PMIB Config Versions; แก้ PowerShell payload ให้ discriminator `kind` ออกก่อนตาม .NET 8 polymorphic contract และเปลี่ยน acceptance v3 เป็น `yyyyMMdd` ซึ่ง compile ผ่านแต่ตั้งใจไม่ตรง fixture; schema/API ยืนยันไม่มี legacy columns/payload, runtime search ไม่พบ legacy parser/evaluator; direct CSV end-to-end ผ่าน Watcher/Kafka/Worker ได้ A=`100/98/2` พร้อม `InvalidDate`/`InvalidDecimal` และ B=`100/100/0` โดย Source เก็บ raw invalid text; coverage-driven golden/unit/PostgreSQL integration ครอบคลุม fixed-width, CASE, named transform/lookup และ reprocess `2→1→0`; full regression 238/238 กับ formatter ผ่าน, เพิ่ม [backend-config-handoff.md](backend-config-handoff.md), อัปเดต README/CONTEXT/acceptance guide และคืน demo เป็น reset+configured state ที่ runtime folders ว่าง
  - Commit: หลังผ่านให้สรุป backend handoff และรอผู้ใช้สั่ง commit แยกต่างหาก; AI agent ห้ามสร้าง commit เอง

## Phase 20 — หน้าเว็บตั้งค่า

ห้ามเริ่ม Phase 20 จนกว่า 19.15 จะผ่านครบและ Phase 19 ถูกติ๊กเสร็จ เว้นแต่ผู้ใช้สั่งข้ามอย่างชัดเจน

- [x] **20.0 ตรวจเครื่องสำหรับเว็บ**
  - ทำ: ตรวจ Node/npm/network/port 3000 และกรอกเวอร์ชัน; ยืนยัน API health และ backend 19.0–19.15 ผ่าน
  - ตรวจ: `node -v`, `npm -v`, `npm ping`, port 3000 ว่าง และ `GET http://localhost:5181/health` ได้ 200
  - ผล: ใช้ Node `v24.11.0` และ npm `11.19.1`; `npm ping` ไป `https://registry.npmjs.org/` ผ่าน; หยุดเฉพาะ process ของ VS Code Markdown Preview Enhanced ที่ยึด port 3000 หลังได้รับอนุญาต แล้วตรวจยืนยันว่าไม่มี listener เหลือ; backend 19.0–19.15 ถูกติ๊กครบ และ API จริงตอบ `GET /health` เป็น HTTP 200 พร้อม `{"status":"ok"}` ก่อนปิด process ทดสอบ

- [x] **20.1 สร้างโปรเจกต์ `web/`**
  - ทำ: ใช้ `create-next-app` เลือก TypeScript, ESLint, Tailwind, App Router และไม่ใช้ `src/`; กรอกเวอร์ชันจริง
  - ตรวจ: `npm run dev` เปิดหน้าเริ่มต้นได้และ `git status` ไม่เห็น `node_modules`/`.next`
  - ผล: สร้าง `web/` ด้วย `create-next-app 16.4.0` และ npm โดยได้ Next.js `16.4.0`, React/React DOM `19.3.0`, TypeScript `^5`, ESLint `^9` และ Tailwind CSS `^4`; ใช้ App Router ที่ `web/app` โดยไม่มี `web/src`; `npm run lint` ผ่านและ `npm run dev` แบบ Turbopack เปิด starter page ที่ `http://localhost:3000` ได้ HTTP 200; `.gitignore` กัน `node_modules`/`.next` และ `git status` เห็นเพียง `web/`; production audit (`npm audit --omit=dev`) พบ 0 vulnerabilities ส่วน full audit พบ 5 high ใน toolchain ของ `eslint-config-next` ซึ่งเวอร์ชันล่าสุดยังไม่มี non-breaking fix และคำสั่ง `--force` จะ downgrade แบบ breaking จึงไม่ใช้

- [x] **20.2 Proxy และ API client**
  - ทำ: rewrite `/api/:path*` ไป `API_BASE_URL`; สร้าง `.env.local.example`, contract types, fetch wrapper และ `ApiError` ที่เก็บ `status/title/detail/errors`
  - เข้าใจ: browser ใช้ origin เดียว; API client ไม่ผูก React เพื่อย้ายแนวคิดไป Angular service ได้
  - ตรวจ: `/api/health` ผ่านและ `npm run build` ผ่าน type check
  - ผล: เพิ่ม server-only `API_BASE_URL` (ค่า local เริ่มต้น `http://localhost:5181`) และ external rewrite `/api/:path*` ใน `next.config.ts` พร้อม commit-safe `.env.local.example`; เพิ่ม TypeScript contracts สำหรับ Health, Tables, Mapping Config/Version, Input Layout, Transformation AST, capabilities, validate และ preview รวมทั้ง `apiRequest<T>` ที่ไม่อ้าง React และ serialize JSON ผ่าน same-origin `/api`; `ApiError` เก็บ HTTP `status`, Problem Details `title`/`detail` และ ValidationProblem `errors`; `npm run lint` และ production build/type check ผ่าน และทดสอบ Next dev + API จริงแล้ว `GET http://localhost:3000/api/health` ได้ HTTP 200 กับ `{"status":"ok"}` ก่อนปิด processes ทดสอบ

- [x] **20.3 Layout, list, create และ details**
  - ทำ: สร้าง `/configs`, `/configs/new`, `/configs/[id]`; แสดง config/version/output/rule counts, clone และ activate; map ValidationProblem/409 ตาม field path
  - ตรวจ: สร้าง config, ดู versions, clone และ activate ได้; API ปิดอยู่แล้ว UI แสดง error ไม่ค้างว่าง
  - ผล: แทน starter ด้วย responsive Config Studio shell และเพิ่มสาม routes ตามกำหนด; `/configs` โหลด Config details/Table catalog แล้วแสดง aggregate config/version/output/fixed-width record-rule counts พร้อมสถานะ active, `/configs/new` เลือก Source/Normalized table และ map ValidationProblem paths กลับ `name`/`inputFolder`/`sourceTableId`/`normalizedTableId` โดย map 409 duplicate ไปยัง name กับ folder, `/configs/[id]` แสดง table/version/output/assignment/rule summaries พร้อม clone immutable payload เป็น version ใหม่และ activate; ทุก load/action มี loading, success, full-path error และ Retry states ผ่าน shared `ErrorPanel`; lint และ Next production build/type check ผ่าน รวม dynamic route แบบ Partial Prerender; runtime acceptance ผ่าน Next proxy สร้าง config, invalid 400 พร้อม 4 field paths, duplicate 409, clone และ activate version สำเร็จ, ทั้งสาม routes ตอบ 200 และเมื่อปิด API proxy คืน 500 แต่หน้า `/configs` ยังตอบ shell 200 เพื่อ render error state; reset/reconfigure คืน demo เป็น Orders/PMIB 2 configs และลบ acceptance data แล้ว

- [x] **20.4 Input Layout editor และ raw-record preview**
  - ทำ: เพิ่ม Delimited/Fixed-width selector; Delimited form มี preamble lines กับ none/single-row header policy; fixed-width form มี encoding, position unit, record rules, ordered sequence/min/max occurrences, expected length และ field grid Source column/start/length/end; รับ pasted sample text หรือ UTF-8 file เพื่อแสดง raw slices, trimmed display, gap/overlap warnings และ physical line errors; browser ตรวจ `File.size`, decode `ArrayBuffer` ด้วย fatal UTF-8 `TextDecoder`, pre-check line count แล้วส่ง `textFile` JSON โดยไม่ใช้ multipart
  - เข้าใจ: UI ส่งตำแหน่ง 1-based แต่ไม่ตัดข้อความเองเป็น source of truth; browser validation ช่วยตอบเร็วแต่ API เป็น authoritative limit/parser และใช้ implementation เดียวกับ Worker; sample ไม่ถูก upload/persist เป็นไฟล์
  - ตรวจ: invalid UTF-8/เกิน 1 MiB/เกิน 50 records ถูกหยุดพร้อมข้อความตรง code; preview Delimited ที่มีหลาย preamble lines ได้; กรอกหรือ clone fixed-width draft แล้ว preview `H/H/D/D/T/T` ตาม min/max ได้ Import 2/Ignore 4, Source rows อ้าง physical lines 3–4, fields ต้น/กลาง/ท้ายและภาษาไทยไม่เลื่อน, padding แสดงทั้ง raw/trimmed และ ignored records แสดง Ignore โดยไม่ต้องกรอก 115 fields
  - ผล: เพิ่ม editor ที่เปิด draft จาก immutable version เดิมและสลับ Delimited/Fixed width ได้ โดย Delimited รองรับ encoding/delimiter/preamble/header policy/field mappings ส่วน Fixed width รองรับ encoding/position unit/rules/discriminator/action/expected length/source field grid ที่คำนวณ End แบบ 1-based พร้อม gap/overlap warnings และ ordered sequence ที่แก้ min/max/reorder ได้; sample รับทั้ง paste กับ UTF-8 file โดยตรวจ `File.size` ก่อนอ่าน, decode แบบ fatal, จำกัด 1 MiB/50 parsed recordsใน browser และส่ง JSON `textFile` ไปให้ API เป็นผู้ parse/slice จริง; preview แสดง Import/Ignore, physical line, raw record, raw slice, trimmed display และ transformation errors โดย Ignore row ไม่ต้องมี Source fields; lint และ production build/type check ผ่าน; helper acceptance ยืนยัน `SampleTooLarge`, `TooManySampleRecords`, `InvalidEncoding` และ boundary 1 MiB; runtime ผ่าน Next proxy โดย fixed-width `H/H/D/D/T/T` ได้ Import 2/Ignore 4 ที่ physical lines 3–4, ภาษาไทยกับ padding อยู่ตำแหน่งถูกต้อง, Delimited preamble 2 บรรทัดได้ record ที่ physical line 4, ความยาวผิดคืน `UnexpectedRecordLength` ที่ line 3 และ preview ไม่เปลี่ยนจำนวน configs/versions/jobs (2/2/0)

- [x] **20.5 Basic Transformation editor**
  - ทำ: สร้าง `ValueExpressionEditor` แบบ recursive เป็น editor กลาง; output `main` เริ่มต้นมีหนึ่งแถวต่อ Normalized column และแก้ `outputKey` ได้ มุมมองพื้นฐานรองรับ Direct field, Constant, Current date และไม่ map; Direct field ใช้ Target Assignment Coercion และ Date/DateTime แสดง `inputFormat` ตาม capability
  - เข้าใจ: ทุกตำแหน่งที่รับ value expression ต้อง reuse editor เดียวกัน; payload-index-to-control map ต้องคง error path ถูกแม้ซ่อน/reorder/nest fields
  - ตรวจ: สร้าง direct mapping เทียบ demo เดิม, padded text ถูก trim, raw date ที่เลือก `inputFormat` ถูก convert, typed constant/current date ไม่ convert ซ้ำ, required blank/ไม่ map แสดง error ตำแหน่งถูก และ nested control map diagnostic กลับมาที่ control ต้นทางได้
  - ผล: เพิ่ม recursive `ValueExpressionEditor` เป็น control กลางที่แก้ basic leaf และแสดง nested expression เดิมแบบ read-only โดยรักษา path ของ child controls ไว้สำหรับ 20.6; Basic Transformation editor แสดงหนึ่งแถวต่อ Normalized column, แก้ `outputKey`, เลือก Direct field/typed Constant/Current date/Do not map และแสดง `inputFormat` จาก capabilities เฉพาะ text-to-Date/DateTime; config ที่ยังไม่มี version เริ่ม draft ด้วย output `main` และ auto-map ชื่อ Source/Normalized ที่ตรงกัน ส่วน draft จาก version เดิมรักษา program/output อื่นไว้; เพิ่ม validate API client และนำ compile/runtime diagnostics กลับ control ผ่าน payload field-index map ที่คำนวณใหม่หลังซ่อนแถว พร้อมผูก `RequiredTargetMissing` ไปยัง target row; pure acceptance ยืนยัน path หลัง No map, nested path และ required target mapping; runtime ผ่าน Next proxyโดย direct mapping trim padded text, `yyyyMMdd` แปลงเป็น Date, typed Date/Decimal/Boolean constants คงชนิด, `currentDate` ใช้วัน Asia/Bangkok, required blank ชี้ `outputs[0].fields[0].value`, No map และ nested expression คืน path ถูก, หน้า config ตอบ 200 และ preview ไม่เพิ่ม configs/jobs; ESLint และ production build/type check ผ่าน

- [x] **20.6 Advanced value, CASE, predicate และ output branch editor**
  - ทำ: ขยาย `ValueExpressionEditor` ให้รองรับ Metadata, Trim, Substring, Convert, Coalesce, Default if blank และ CASE; เพิ่ม typed comparison operands, common `sourceWhen`, AND/OR condition builder, output tabs/add/clone และ `emitWhen`; จำกัด depth/node/arms ตาม capabilities
  - เข้าใจ: nested expression ใช้ contract เดียวกับ backend ไม่สร้าง payload รูปแบบเฉพาะ UI; field CASE กับ output filter เป็นคนละระดับ และ PMIB เปลี่ยน default `main` เป็น `pmx` ก่อน clone เป็น `ctp` เพื่อแทน UNION
  - ตรวจ: สร้าง `convert(trim(source))`, metadata, substring, coalesce/default-if-blank และตัวอย่าง CASE 1–2 ผ่าน UI; สร้าง output branches `pmx`/`ctp` ตามตัวอย่าง 5 และ preview PMIB-02 ให้ `CLOANNUMBER` เดียวออกสอง branches ตาม sample data; diagnostics ของ nested expression ชี้ control ถูก
  - ผล: ขยาย recursive `ValueExpressionEditor` ให้แก้ Metadata, Trim, Substring, Convert พร้อม format, Coalesce, Default if blank และ CASE หลาย arms ด้วย AST contract เดิม; เพิ่ม reusable Boolean editor สำหรับ typed comparison, null/blank, LIKE, IN/NOT IN, NOT และ AND/OR โดยใช้ร่วมกันที่ common `sourceWhen`, CASE WHEN และ output `emitWhen`; เพิ่ม output tabs พร้อม add/clone/remove และแก้ fields แยกตาม branch โดยรักษา named transform/lookup เดิมแบบ read-only ไว้รอ 20.7; UI คำนวณ subtree nodes/depth และจำกัด outputs/collection arms ตาม capabilities ก่อนเพิ่ม พร้อม map nested literal/field diagnostics กลับ exact control; pure tests ยืนยัน type inference, node/depth count และ path ของ output ที่สอง ส่วน runtime ผ่าน Next proxy โดย `convert(trim(source))`, metadata, substring, coalesce/default-if-blank และ CASE 2 arms ให้ผลครบ 3 branches ของ CASE, invalid nested value ชี้ `outputs[0].fields[3].value.value.value`, PMIB-02 หนึ่งแถวออกทั้ง `pmx`/`ctp` ด้วย `cloannumber=CL-0002`, หน้า config ตอบ 200 และจำนวนคงเดิมที่ 2 configs/2 versions/0 jobs; ESLint และ production build/type check ผ่าน

- [x] **20.7 Named transform, lookup และ preview UI**
  - ทำ: เพิ่ม Named transform และ arguments ลง `ValueExpressionEditor`, dropdown lookup จาก capabilities, dependency summary และ preview panel ที่แสดง outputs/filtered/errors; JSON แสดง read-only
  - เข้าใจ: ผู้ใช้เลือก logical capability โดยไม่เห็น credential, physical table หรือ SQL
  - ตรวจ: ตั้ง `formatRegNo@1` พร้อม nested arguments และ anti-lookup ตัวอย่าง 3–4 ได้, invalid argument ชี้ control ถูก และไม่มีช่อง execute SQL
  - ผล: เปิด `call` ใน recursive `ValueExpressionEditor` พร้อม dropdown logical transform จาก capabilities, signature/reset arguments และ nested Text argument editor; เปิด `exists`/`notExists` ใน Boolean editor พร้อม dropdown logical lookup และ arguments `systemId`/`productName`/`loanNumber` ที่ auto-map Source columns ตามชื่อและแก้ด้วย value editor เดิม โดยไม่มี control สำหรับ SQL, credential, connection หรือ physical table; เพิ่ม logical dependency summary ที่ deduplicate named transforms/lookups จากทั้ง program; ขยาย preview ให้สรุป Done/Filtered/Invalid/outputs, แสดงค่าทุก output branch, filtered explanation, error reason พร้อม output/field/expression path และ raw response JSON แบบ read-only; runtime ผ่าน Next proxy โดย `formatRegNo@1(trim(source))` คืน `1กก 1234`, sample เดียวได้สถานะ Done/Filtered/Invalid, invalid transform argument ชี้ `outputs[0].fields[0].value.arguments[0]`, anti-lookup not-found ปล่อยเฉพาะ branch ที่ใช้ `notExists` และ invalid `systemId` ชี้ `outputs[0].emitWhen.arguments.systemId`; dependency collector คืนเฉพาะ logical names, หน้า config ตอบ 200 และจำนวนคงเดิมที่ 2 configs/2 versions/0 jobs; ESLint และ production build/type check ผ่าน

- [x] **20.8 ตรวจรับครบเส้นทางผ่านหน้าเว็บ**
  - ทำ: reset demo, สร้าง tables/config/version จาก UI, preview coverage-driven Transformation/Fixed-width drafts, activate, รัน Worker และ reprocess กรณี branch เปลี่ยน
  - ตรวจ: สร้าง Source/Metadata/Literal/Current date, Trim/Substring/Convert, Coalesce/Default if blank, CASE และ Named transform ผ่าน UI ได้โดยไม่แก้ JSON; pasted text และ valid UTF-8 file preview ผ่าน ส่วน invalid/oversized/51-record sample แสดง error โดยไม่ส่งหรือไม่ truncate; preview ไม่เพิ่ม DB rows/files; CSV เดิมและ compact fixed-width draft ผ่าน, coverage matrix ครบ, `CLOANNUMBER` เดียวได้ `pmx`/`ctp`, reprocess `2→1→0` outputs ลบ stale/all outputs ถูกต้อง, `GET /file-jobs/{id}` คืน Done/Filtered/Invalid/Failed counts และ aggregate status ตรง matrix, retry determinism และ errors ตรง golden cases
  - ผล: reset แล้วสร้าง Source/Normalized tables, config และ 3 versions ผ่าน Next same-origin proxy ที่พอร์ต 3001; rich preview ครบทุก value expression ตามรายการและได้ `Done/Filtered/Invalid`, pasted/valid UTF-8 กับ browser guard `InvalidEncoding`/`SampleTooLarge`/`TooManySampleRecords` ผ่านโดย preview คง DB rows/jobs เป็นศูนย์, compact fixed-width `H/H/D/D/T/T` ได้ Import 2/Ignore 4 ที่ physical lines 3–4; Worker ยืนยัน `CLOANNUMBER` เดียวออก `pmx`/`ctp`, reprocess `2→1→0` ลบ stale/all outputs และ matrix คืน `1/1/1/1` ก่อน retry กับ `2/1/1/0` หลัง retry โดย identity ไม่ซ้ำและ error paths ตรง; ระหว่างตรวจพบ PostgreSQL `jsonb` ย้าย `else` มาก่อน discriminator `kind` ทำให้ persisted CASE อ่านกลับไม่ได้ จึงเพิ่ม polymorphic JSON read boundary แบบ recursive ครบ API/Worker พร้อม regression test; full regression 239/239, formatter, ESLint และ production build ผ่าน แล้วคืน demo เป็น Orders/PMIB 2 configs/2 versions/0 jobs และ runtime folders ไม่มีไฟล์

- [x] **20.9 อัปเดตเอกสารและ commit handoff**
  - ทำ: อัปเดต CONTEXT ด้วย Input Layout/Fixed-width Slice/Transformation Program/Output Branch/Named Transform/Named Lookup, README วิธีรัน backend+web พร้อมคำเตือน breaking reset/rollback และติ๊ก Phase 20 ใน learning-plan; สรุปงานให้ผู้ใช้
  - ตรวจ: คนที่ไม่เคยเห็น repo ทำตาม README ตั้งแต่หยุด process → reset → configure แล้วสร้าง/preview/activate Config Version ใหม่ได้ และไม่พบคำแนะนำให้ใช้ legacy payload
  - Commit: ให้ผู้ใช้สั่ง commit แยกต่างหาก; AI agent ห้ามสร้าง commit เอง
  - ผล: ปรับ Domain Context ให้ Mapping Config ชี้ ownership ของ immutable Input Layout/Transformation Program ชัดเจน; README ครบ prerequisites, การหยุด process, destructive reset, เปิด API/Web/Worker, UI flow สร้าง config → แก้/preview draft → Create version → Activate, same-origin/API URL, breaking baseline และ rollback แบบ reset/reconfigure โดยกล่าวถึง legacy payload เฉพาะคำเตือนว่าใช้ไม่ได้; ระหว่างตรวจ handoff พบ editor ยัง persist draft ไม่ได้จึงเพิ่มปุ่ม `Create version` ที่ validate ก่อนเรียก API และ refresh ไปยังรุ่นใหม่ พร้อมแก้ Development ArchiveRoot จาก `archived/` ให้ตรง `archive/` ที่ reset/.gitignore/docs ใช้; runtime ผ่าน Next proxy โดยหน้า config ตอบ 200 และสร้าง/activate version 2 สำเร็จ, ESLint/production build กับ regression 239/239 ผ่าน, ติ๊ก Phase 20 แล้วและคืน demo เป็น Orders/PMIB 2 configs/2 versions/0 jobs โดยยังไม่สร้าง commit ตามข้อกำหนด

---

## ภาคต่อ Feature ใหม่ Phase 21–24

### เป้าหมาย

เพิ่มความสามารถต่อจาก baseline โดยยังใช้ Source Table, Input Layout, Transformation expressions, conditions, named transforms/lookups และ output branches เดิม:

```text
.txt/.csv/.xlsx/.xls
        │
        ▼
Input Record Module ──► Source Table ──► Transformation Module
                                                │ 0..N outputs
                                                ▼
                                      Destination Module seam
                                      ┌─────────┴──────────┐
                                      ▼                    ▼
                              LegacyTable Adapter   ApiContract Adapter
                              Normalized Table      Delivery Job/Outbox
                                                           │
                                                           ▼
                                                    Partner Endpoint
```

กติกาหลัก:

- Config Version เลือก destination ได้หนึ่งแบบ: `LegacyTable` หรือ `ApiContract`
- Config เก่าที่ไม่มี Destination Definition อ่านเป็น `LegacyTable`
- `ApiContract` ยังเขียน raw Source Table แต่ไม่ scaffold, สร้าง หรือเขียน Normalized Table
- หนึ่ง mapped output/proposal สร้างหนึ่ง API request และ retry แยกรายการ
- Contract Version ถูก snapshot พร้อม canonical SHA-256 ตอนสร้าง Config Version และ runtime ห้าม fetch schema ใหม่
- Mapping แบบ API ใช้ Source/expression → Contract Path; object/array containers ถูกสร้างอัตโนมัติ
- System-bound Paths มาจาก runtime metadata และแสดง read-only; Mapped Paths จึงเลือก expression ได้
- operator ชุดแรกใช้ของเดิมจาก Phase 19–20 ไม่มี raw code/SQL และไม่มี operator ใหม่เพียงเพื่อรองรับ JSON
- prototype `web/app/prototype/file-to-json/` เป็น throwaway reference ไม่ใช่ production route และไม่ใช่ source of truth
- `JsonFile` destination และ JSON file import/export ไม่อยู่ใน Phase 21–24 ตาม requirement ล่าสุด

### Dependency map ปัจจุบันและ seam ที่ต้องสร้าง

```text
FileWatcherService / FileIntake
  → FileImportRequested
  → FileImportHandler
      → InputRecordModule
      → SourceRowWriter
      → Row Job + RowNormalizeRequested
  → RowNormalizeHandler
      → TransformationModule
          → TransformationSchema (ปัจจุบันผูก NormalizedTable แบบ flat)
      → NormalizedRowWriter (ปัจจุบันเรียกตรง)

เป้าหมายหลัง Phase 24
  → TargetSchema Module
      ├─ LegacyTableTargetSchemaAdapter
      └─ ContractSnapshotTargetSchemaAdapter
  → Destination Module seam
      ├─ LegacyTableDestinationAdapter → NormalizedRowWriter
      └─ ApiContractDestinationAdapter → Delivery Job/Outbox
  → Contract Registry Module → server-owned schema profile
  → Payload Binding Module → System-bound Paths + final validation
  → API Delivery Worker → server-owned endpoint/auth profile
```

Seam มีเหตุผลเพราะมี adapter จริงสองตัว ห้ามสร้าง interface แบบ pass-through ที่เพียงย้าย method เดิมโดยไม่ซ่อน target compilation, persistence หรือ delivery behavior

### ขอบเขต Phase 21–24

**อยู่ใน scope**

- `.txt`, `.csv`, `.xlsx`, `.xls` เป็น input ของ File-to-Source
- Excel เลือก sheet, header row และ data-start row ใน Input Layout
- `LegacyTable` และ `ApiContract` อยู่ร่วมกันต่อ Mapping Config Version
- JSON Schema snapshot/hash, typed path catalog และ supported-subset validation
- nested object, fixed array index, wildcard Array Group และ array item type
- หลาย Source columns ต่อหนึ่ง array item, empty-item skip และ source-slot `seq`
- server-owned payload bindings สำหรับ runtime metadata
- persisted Delivery Job/outbox, one request per output, retry/idempotency/reprocess
- หน้าเว็บ Setup → Input → Destination → Mapping → Preview พร้อม version history/compare
- metrics, kill switch, config-by-config canary และ rollback ไป Legacy version สำหรับ File Jobs ใหม่

**นอก scope**

- arbitrary URL/header/credential/SQL/script จาก browser
- dynamic entity/Normalized Table scaffolding สำหรับ `ApiContract`
- runtime schema fetch หรือการเปลี่ยน snapshot ของ version ที่ถูกสร้างแล้ว
- dual-delivery production output เดียวไปทั้ง API และ LegacyTable
- arbitrary JSON property เมื่อ schema ไม่อนุญาต
- external/recursive `$ref`, ambiguous `oneOf`/`anyOf` ใน v1
- percent-random routing ภายใน Mapping Config เดียว
- retire `LegacyTable` ก่อนทุก partner ผ่าน migration gate

### Planned endpoints สำหรับ Phase 21–24

ชื่อ endpoint นี้เป็น implementation contract ของแผน หากเปลี่ยนระหว่างทำต้องอัปเดต API contracts, web client และเอกสารใน step เดียวกัน:

| งาน | Endpoint |
|---|---|
| Server-owned destination profiles | `GET /api-destination-profiles` |
| Contract/version catalog ของ profile | `GET /api-destination-profiles/{id}/contracts` |
| Resolve schema สำหรับ draft โดยไม่ persist version | `POST /mapping-configs/{id}/contract-schema/resolve` |
| Validate/preview draft | ใช้ `POST /mapping-configs/{id}/versions/validate` และ `/preview` เดิม แต่เพิ่ม destination/target schema |
| Preview binary Excel | `POST /mapping-configs/{id}/versions/preview-file` แบบ multipart และ server-side limits |
| Create immutable version | ใช้ `POST /mapping-configs/{id}/versions` เดิม; server fetch/snapshot schema ใน transaction workflow |
| ดู version target schema/path catalog | `GET /mapping-configs/{id}/versions/{n}/target-schema` |
| Delivery status ต่อ File Job | `GET /file-jobs/{id}` ขยาย summary และ `GET /file-jobs/{id}/deliveries` |
| Retry delivery ที่ retryable/permanent ตาม policy | `POST /delivery-jobs/{id}/retry` |

Browser ห้ามส่ง schema snapshot/hash ที่เชื่อถือเป็น authoritative input ตอน Create Version; server ต้อง resolve/canonicalize/hash เองจาก logical profile + Contract Reference

### Phase gates และ rollback summary

| Gate | ผ่านเมื่อ | Rollback |
|---|---|---|
| Gate A หลัง Phase 21 | Legacy characterization/golden tests ผ่าน, SQL effects และ statuses เท่า baseline | feature flag กลับ direct legacy path; schema ใหม่เป็น expand-only |
| Gate B หลัง Phase 22 | สี่ extension import → Source ได้ตาม layout, error ไม่มี partial rows/jobs | activate Config Version เดิมและปิด accepted extension ใหม่ |
| Gate C หลัง Phase 23 | schema/path/nested/array/payload binding preview ผ่านโดยไม่มี external side effect | ปิด ApiContract config creation; Legacy versions ยังทำงาน |
| Gate D หลัง Phase 24 | stub/sandbox, retry/idempotency, canary และ metrics ผ่านครบหนึ่ง release cycle | pause Delivery Worker; activate previous LegacyTable version สำหรับ File Jobs ใหม่; ห้าม reroute pinned jobs |

---

## Phase 21 — Characterization Safety Net และ Destination Seam

- [x] **21.0 ตรวจ baseline, dependency และ external contract inventory**
  - ทำ: รัน backend/web baseline; บันทึก dependency map จาก `RowNormalizeHandler` ถึง `NormalizedRowWriter`, Config Version persistence, File/Row Job statuses, reprocess และ API/web contracts; inventory PostgreSQL, Kafka, Source/Normalized dynamic tables, schema endpoint, delivery endpoint และ secret provider; สร้าง risk register พร้อม owner/rollback trigger
  - เข้าใจ: Phase นี้เป็น assessment ของ behavior ปัจจุบัน ไม่แก้ business semantics; ต้องรู้ external integrations/data contracts ก่อนออก seam
  - ตรวจ: `dotnet test`, formatter check, `npm run lint`, `npm run build` ผ่าน; dependency map ชี้ call site ที่เขียน Normalized Table ครบ; risk ทุกข้อมี impact/probability/mitigation/rollback trigger และไม่มี endpoint/credential ที่ browser เป็นเจ้าของ

- [x] **21.1 เพิ่ม Legacy characterization และ golden SQL effects**
  - ทำ: เพิ่ม tests ครอบ direct/default/CASE/lookup/output `0..N`, `Done/Filtered/Invalid/Failed`, retry, reprocess `2→1→0`, stale-output reconciliation, config version pinning และ exact Normalized row identity/values ก่อน refactor
  - เข้าใจ: tests ตรึง behavior ที่มีอยู่แม้บาง behavior จะไม่สวย; ห้ามแก้ expected ให้เข้ากับ implementation ใหม่โดยไม่มี approved behavior change
  - ตรวจ: characterization suite ผ่านบน code path เดิม; critical legacy path มี line/branch coverage เป้าหมายอย่างน้อย 80% หรือมีรายการ exception พร้อมเหตุผล; เก็บ baseline duration/status/row counts สำหรับเทียบ Phase 21.5

- [x] **21.2 ทำ expand-only database migration สำหรับ Destination Definition**
  - ทำ: เพิ่ม `destination_definition jsonb`, contract/profile snapshot metadata ที่ nullable ใน `mapping_config_versions`; backfill version เดิมเป็น `legacyTable` ที่อ้าง Normalized Table เดิม; ผ่อน `mapping_configs.normalized_table_id` ให้ nullableสำหรับ config ใหม่โดยยังรักษา FK ของ legacy; เพิ่ม constraints/index ที่ตรวจ discriminators ขั้นพื้นฐาน
  - เข้าใจ: Destination ถูก pin ที่ Config Version ไม่ใช่ active config ล่าสุด; migration ระยะนี้ยังไม่สร้าง Delivery Job และต้องให้ binary เดิมอ่านฐานข้อมูลใหม่ได้ใน rollback window
  - ตรวจ: migration จาก baseline data ผ่าน; ทุก version เดิม resolve เป็น `LegacyTable`; rollback application version เดิมยังทำงานกับ schema ที่ expand แล้ว; invalid destination JSON ถูก application validator ปฏิเสธด้วย exact path

- [x] **21.3 สร้าง Target Schema Module โดยเริ่มจาก LegacyTable adapter**
  - ทำ: เปลี่ยน `TransformationSchema` จากการเปิด `NormalizedTable` ตรงเป็น typed `TargetSchema` tree; ทำ `LegacyTableTargetSchemaAdapter` แปลง flat table definition เป็น root object + scalar leaves; compiler/evaluator เรียกผ่าน Interface เดียว
  - เข้าใจ: Module ต้องซ่อนแหล่ง schema จาก Transformation caller และคืน immutable compiled model; Phase นี้ยังไม่มี JSON Schema adapter แต่ Interface ต้องไม่ใช้ชื่อ column-only ที่ปิดทาง nested path
  - ตรวจ: transformation unit/golden tests เดิมผ่านโดย payload v1 ไม่เปลี่ยน; flat column diagnostics/path เท่าเดิม; deletion test แสดงว่าถ้าลบ Module ความรู้เรื่อง source schema จะกระจายกลับ compiler/caller มากกว่าหนึ่งจุด

- [x] **21.4 สร้าง Destination Module seam และ LegacyTable adapter**
  - ทำ: สร้าง Destination Definition discriminated union, compiled destination/result contracts และ `LegacyTableDestinationAdapter` ที่ครอบ `NormalizedRowWriter.ReconcileAndCompleteAsync`; register ผ่าน DI; ยังไม่เพิ่ม HTTP implementation
  - เข้าใจ: Transformation Module คืน outputs โดยไม่รู้ SQL/HTTP ส่วน Destination Module รับ compiled destination + output context; retry/persistence semantics ของ legacy อยู่หลัง adapter
  - ตรวจ: Interface มี surface เล็กและ tests เรียกผ่าน Interface เดียวกับ caller; Legacy adapter ทำ upsert/reconcile/complete ใน transaction เดิม; ไม่มี fake `ApiContract` adapter ที่ silently เขียน table

- [x] **21.5 เปลี่ยน RowNormalizeHandler ให้ route ตาม pinned destination**
  - ทำ: แยก load/compile/evaluate ออกจาก delivery orchestration; ให้ handler โหลด Destination Definition จาก pinned Config Version และเรียก Legacy adapter; ใส่ temporary feature flag สำหรับกลับ direct legacy pathระหว่าง rollout ของ seam
  - เข้าใจ: branch-by-abstraction ต้องเปลี่ยน call site ก่อนเพิ่ม adapter ใหม่; ห้ามเลือก destination จาก active version หรือ percentage random routing
  - ตรวจ: run characterization ทั้ง direct path และ adapter path แล้ว outputs, SQL effects, attempts, statuses และ errors เท่ากัน; config activate รุ่นใหม่ระหว่าง job รันไม่เปลี่ยน pinned behavior; rollback flag ใช้ได้โดยไม่ migrate data กลับ

- [x] **21.6 ปรับ Mapping Config API contracts แบบ backward-compatible**
  - ทำ: เพิ่ม Destination Definition ใน create/get/clone/validate/activate responses; request เก่าที่ไม่มี destination และมี Normalized Table ถูก normalize เป็น `legacyTable`; ApiContract draft อนุญาต `normalizedTableId = null` แต่ creation ยังถูก feature flag ปิดจน Phase 23 พร้อม
  - เข้าใจ: compatibility normalization อยู่ที่ contract boundary จุดเดียว ไม่กระจาย null/default checks; active version immutable และเปลี่ยน destination ต้องสร้าง version ใหม่
  - ตรวจ: old request fixtures round-trip เป็น LegacyTable; API reject destination ที่ใส่ทั้ง normalized table และ contract fields; clone รักษา destination; response แสดง destination ของทุก version ชัดเจน

- [x] **21.7 ปรับหน้า Config ให้รู้จัก destination โดยยังเปิดใช้ LegacyTable เท่านั้น**
  - ทำ: เพิ่ม Destination step/summary ใน production config page; แสดง `LegacyTable` เป็นค่าของ version เดิม; render `ApiContract` option หลัง feature flag แต่ disable พร้อมข้อความ dependency ที่ยังไม่พร้อม; ลบ assumption ว่า Normalized Table มีเสมอจาก TypeScript contracts/components
  - เข้าใจ: UI migration ต้องไม่สร้าง state model แยกจาก backend discriminated union; prototype ใช้ดูแนวคิดได้แต่ห้าม import production logic จาก prototype
  - ตรวจ: list/details/clone/activate เดิมยังทำงาน; null Normalized Table ไม่ทำหน้า crash; validation path ของ destination map กลับ control ถูก; lint/build ผ่าน

- [x] **21.8 Gate A — legacy parity และ handoff**
  - ทำ: reset/configure demo, รัน CSV/fixed-width, reprocess และ web flow ผ่าน adapter path; เปรียบเทียบ baseline metrics; อัปเดต dependency/risk register และบันทึก rollback command/flag
  - เข้าใจ: Phase 22 เริ่มได้เมื่อ seam ไม่ทำให้ legacy behavior เปลี่ยนเท่านั้น
  - ตรวจ: regression suite ทั้งหมดผ่าน, critical-path coverage ถึงเกณฑ์, row/status/SQL parity 100%, ไม่มี Delivery Job/API call และ Gate A ได้รับ sign-off

---

## Phase 22 — Input `.txt`, `.csv`, `.xlsx`, `.xls`

- [x] **22.0 ตรึง Excel/File semantics และสร้าง minimal fixtures**
  - ทำ: สร้าง fixtures `.xlsx`/`.xls` ขนาดเล็กที่มีหลาย sheets, sheet ไทย, header row ไม่ใช่แถวแรก, data-start row แยกจาก header, blank rows, mixed cell types, date/time/number/boolean/formula/error/merged cell; ตรึงว่า Source Table เก็บ text representation อะไรและ row/physical row นับอย่างไร
  - เข้าใจ: Excel อยู่ใน File-to-Source Input Layout ก่อน Transformation; transformation operator ไม่ควรรู้ cell/workbook; formula v1 อ่าน cached value หรือ reject ตาม policy ที่ตรึง ห้าม execute formula
  - ตรวจ: coverage matrix ระบุ expected Source values/row numbers ทุก case; มี corrupted/password-protected/wrong extension/unknown sheet/header duplicate/missing required column fixtures และ stable error codes

- [x] **22.1 เลือกและอนุมัติ workbook reader dependency**
  - ทำ: ประเมิน .NET library ที่อ่านทั้ง Open XML `.xlsx` และ binary `.xls` แบบ streaming/read-only; ตรวจ license, maintenance, security, memory behavior และ code-pages requirement; ขออนุมัติก่อนเพิ่ม package และ pin version ใน project
  - เข้าใจ: package choice เป็น implementation dependency ไม่ใช่ domain interface; Input Record Module ต้องซ่อน library-specific workbook/cell types
  - ตรวจ: spike อ่าน golden `.xlsx`/`.xls` ได้เหมือนกัน, ไม่ evaluate macro/formula, dispose stream ถูก และ memory limit ผ่าน fixture ที่กำหนด; บันทึกเหตุผลเลือก/ไม่เลือกไว้ในแผนหรือ ADR

- [x] **22.2 เพิ่ม accepted extensions และ Excel Input Layout contract**
  - ทำ: เพิ่ม `acceptedExtensions` ใน Config Version; เพิ่ม `ExcelInputLayout` discriminator ที่มี workbook kind/auto-detect policy, sheet selector, `headerRow`, `dataStartRow`, fields `inputHeader → sourceColumn` และ blank-row policy; compiler ตรวจ extension/layout compatibility และ Source schema
  - เข้าใจ: `.txt` อาจใช้ delimited หรือ fixed-width, `.csv` ใช้ delimited, `.xlsx/.xls` ใช้ excel; extension เป็น intake filter แต่ reader ต้องตรวจ file signature จริง
  - ตรวจ: serialization/canonical clone ครบทุก layout; reject duplicate/unknown extensions, sheet ว่าง, row < 1, `dataStartRow <= headerRow`, duplicate target/header และ extension-layout mismatch ด้วย exact diagnostics

- [x] **22.3 เพิ่ม Excel reader หลัง Input Record Module Interface เดิม**
  - ทำ: implement internal workbook adapter; เลือก sheet, อ่าน header/data rows ตาม compiled layout, map cells เป็น raw Source strings ตาม invariant policy, คืน import row number + worksheet row number และหยุดทั้งไฟล์เมื่อ structural validation fail
  - เข้าใจ: API preview และ Worker ต้องใช้ implementation เดียวกัน; reader ไม่รู้ File Job, database, archive หรือ destination
  - ตรวจ: `.xlsx`/`.xls` golden fixtures ให้ Source rows เท่ากัน; blank/formula/date/time/number/boolean/error cases ตรง policy; file signature mismatch, encrypted/corrupt workbook และ limits คืน stable code โดยไม่ leak library exception

- [x] **22.4 เชื่อม accepted extensions เข้ากับ Watcher และ File Intake**
  - ทำ: เปลี่ยน watcher จาก extension hard-code เป็น union ของ accepted extensions จาก active Config Versions; normalize case/dot; ตรวจ folder ownership/ambiguity; File Job pin extension/layout/version ก่อน publish import message
  - เข้าใจ: watcher เพียงเลือก candidate; Input Module ยังตรวจ signature/content authoritative และ active-version change ห้ามเปลี่ยน job ที่ intake แล้ว
  - ตรวจ: `.TXT/.CSV/.XLSX/.XLS` เข้า config ที่ถูกต้อง; unsupported/ambiguous file ไม่สร้าง wrong job; activate version ใหม่มีผลเฉพาะ file ใหม่; duplicate-file policy เดิมผ่าน

- [x] **22.5 เชื่อม Excel reader เข้า FileImportHandler และ Source persistence**
  - ทำ: pre-scan workbook ทั้ง structural/limits ก่อนเขียน Source row แรก; เขียนเฉพาะ data rows พร้อม worksheet row metadata; สร้าง Row Jobs/outbox เหมือน text; raw file snapshot/audit/archive/retry ใช้ lifecycle เดิม
  - เข้าใจ: `ApiContract` และ `LegacyTable` ใช้ Source persistence เดียวกัน; input failure ต้องไม่มี partial Source/Row Job/outbox
  - ตรวจ: สี่ extensions import สำเร็จ; sheet/header/data-start เลือกได้; invalid workbook ทุก case ได้ `ImportFailed` และ zero partial side effects; retry snapshot deterministic

- [x] **22.6 เพิ่ม binary preview endpoint พร้อม limits**
  - ทำ: เพิ่ม multipart `preview-file` สำหรับ `.xlsx/.xls` และอนุญาต text filesเพื่อใช้ endpoint เดียวได้ภายหลัง; stream เข้า Input Module โดยไม่ persist; กำหนด request/file/worksheet-row/column/cell-length/time limits, filename display-only และ content sniffing
  - เข้าใจ: binary Excel ส่งผ่าน JSON text contract เดิมไม่ได้; preview เป็น side-effect-free และ server เป็น authoritative parser/limit checker
  - ตรวจ: valid preview คืน sheet catalog, selected sheet, header, Source rows และ diagnostics; oversized/corrupt/wrong signature/too-many-row cases reject ไม่ truncate; DB/file-system/job counts ไม่เปลี่ยนและ payload content ไม่ถูก log

- [x] **22.7 เพิ่ม Excel controls ใน Input Layout editor**
  - ทำ: เพิ่ม format selector, file picker, sheet selector จาก preview, header row, data-start row, blank-row policy และ field mapping grid; แสดง workbook/worksheet row numbers; text layout controls เดิมไม่เปลี่ยน
  - เข้าใจ: browser ไม่ parse Excel เป็น source of truth; file ใช้ preview เท่านั้น ส่วน saved Config Version เก็บ layout ไม่เก็บ sample
  - ตรวจ: ผู้ใช้เลือก sheet/header/data-start แล้ว preview `.xlsx/.xls` ได้; validation map exact control; switching layout ไม่ทำ stale fields รั่ว; browser/API error state, lint และ build ผ่าน

- [x] **22.8 Gate B — File-to-Source end-to-end**
  - ทำ: สร้าง Config Versions อย่างน้อยหนึ่งต่อ layout, activate, drop files จริง, รัน Worker และตรวจ Source/audit/reprocess; ทดสอบ rollback ด้วย activate version เดิมที่ไม่รับ Excel
  - เข้าใจ: Phase นี้จบที่ Source + transformation legacy path ยังไม่ส่ง partner API
  - ตรวจ: `.txt/.csv/.xlsx/.xls` ผ่าน expected matrices; invalid input ไม่มี partial write; legacy normalized outputs ยัง parity; metrics/limits documented และ Gate B sign-off

---

## Phase 23 — Contract Schema, Nested Mapping และหน้า Config

- [x] **23.0 ตรึง supported JSON Schema subset และ contract fixtures**
  - ทำ: สร้าง contract fixtures อย่างน้อย PA MTLS illustrative profile และอีกหนึ่ง profile ที่ใช้ envelope paths ต่างกัน; ครอบ string/integer/number/boolean/date/time/object/array, required/nullable, local `$ref`, constraints, fixed index, wildcard array group และ `additionalProperties`; สร้าง negative fixtures สำหรับ external/recursive ref และ ambiguous union
  - เข้าใจ: PA MTLS ใช้พิสูจน์ coverage ไม่ใช่ global schema; path/type/required จริงมาจาก snapshot
  - ตรวจ: fixture-to-requirement matrix ครบ R3/R4/R26–R31 และ PAY-001–PAY-008; profile ที่สองไม่ใช้ชื่อ `proposal`, `fileJobId` หรือ `beneficiaries` เหมือน profileแรกทั้งหมด

- [x] **23.1 สร้าง server-owned API Destination Profile catalog**
  - ทำ: เพิ่ม typed configuration/persistence สำหรับ logical profile ID, schema/delivery path templates, HTTP method, allowed headers, auth/secret reference, timeout, supported contracts และ payload bindings; expose read-only safe metadata ให้ browser
  - เข้าใจ: browser เลือก logical ID เท่านั้น; base URL, credential และ secret value ไม่อยู่ Mapping Config JSON/response/log
  - ตรวจ: invalid URL scheme/host/path template/header/auth reference ถูก reject ตอน startup/admin load; API response ไม่เผย secret/base URL ที่ไม่ควรเห็น; disabled profile สร้าง version ใหม่ไม่ได้

- [x] **23.2 สร้าง Contract Registry Module**
  - ทำ: implement Interface `ResolveAsync(profileId, contractReference)` ที่ authenticate/fetch schema, enforce size/timeout/content type, parse supported subset, resolve local refs, canonicalize, hash SHA-256 และคืน snapshot + typed path catalog; ใช้ fake adapter ใน tests
  - เข้าใจ: Interface ซ่อน HTTP/auth/canonicalization; runtime Row Job/Delivery Worker ห้ามเรียก Module นี้
  - ตรวจ: same logical schema ต่าง whitespace/property order ได้ canonical hash เดียวกัน; schema drift hash ต่าง; timeout/auth/invalid JSON/unsupported feature คืน stable diagnostics และไม่ persist partial version

- [x] **23.3 Persist immutable Contract Snapshot และ Payload Binding Snapshot**
  - ทำ: ตอน Create ApiContract Version ให้ server resolve schema และ profile bindings, validate binding path/type, แล้ว persist contract reference, canonical schema, hash, fetched-at, logical profile ID และ bindings ใน transaction เดียวกับ version; drift refresh ใช้เฉพาะ draft
  - เข้าใจ: client-provided hash/snapshot เป็น display input ที่เชื่อถือไม่ได้; used version ห้าม update/delete snapshot
  - ตรวจ: create failure ไม่ทิ้ง version; registry เปลี่ยนหลัง create แล้ว runtime/preview version เดิมยังใช้ old bytes/hash; clone เลือกชัดว่าจะ pin snapshot เดิมหรือ resolve ใหม่ตาม action และ history แสดงผลต่าง

- [x] **23.4 เพิ่ม Contract Snapshot adapter ให้ Target Schema Module**
  - ทำ: compile JSON Schema เป็น immutable TargetNode tree/path tokens/path catalog; แยก leaf assignable กับ object/array containers; รองรับ local refs, constraints, required/nullable/item schema; cache ตาม schema hash
  - เข้าใจ: Transformation compiler ใช้ TargetSchema เดียวกันทั้ง flat legacy และ nested contract โดยไม่รู้ source ของ schema
  - ตรวจ: path catalog/type tree ตรง fixtures; unknown/container/parent-child/duplicate path คืน exact diagnostics; second profile compile โดยไม่แก้ core Module

- [x] **23.5 เพิ่ม Transformation Program language v2 และ compatibility v1**
  - ทำ: เพิ่ม `targetPath` และ target value type `Time`; v1 LegacyTable ยังใช้ normalized column semantics; Config Version pin language version; reuse value/boolean expressions, sourceWhen, emitWhen, CASE, transform, lookup และ operators เดิม
  - เข้าใจ: ความ nested อยู่ที่ target addressing/assembler ไม่ใช่ operator ใหม่; active v1 program ห้าม auto-rewrite
  - ตรวจ: v1 golden suite ผ่าน unchanged; v2 reject path absent/type incompatible/duplicate/parent-child conflict; serialization order/discriminator round-tripผ่าน PostgreSQL jsonb

- [x] **23.6 สร้าง Document Assembler สำหรับ object และ fixed-index array**
  - ทำ: parse target paths เป็น immutable tokens ตอน compile; evaluate leaf assignments แล้วสร้าง intermediate object/array; รองรับ `proposal.insured.fullName` และ contiguous `[0]`, `[1]`; omit optional empty container และสร้าง required empty arrayตาม schema policy
  - เข้าใจ: caller map เฉพาะ leaf; assembler เป็น pure implementation ภายใน Transformation Module และยังไม่ส่ง HTTP
  - ตรวจ: three-level object, required/optional/null, fixed indices, array gap, scalar/container conflict, deterministic JSON ordering/hash และ error paths ผ่าน golden tests

- [x] **23.7 เพิ่ม Array Group semantics R29–R31**
  - ทำ: เพิ่ม array path, sequence path, empty-item policy, items/source slots และ relative item target paths; evaluate business fields, skip เมื่อทั้งหมด null/blank/whitespace/`-`, retain `0`/`false`, inject source slot เป็น seq แล้ว compact emitted array
  - เข้าใจ: wildcard `[]` ใช้ใน Array Group เท่านั้น; synthetic seq ไม่นับ empty; direct fixed-index กับ group ห้ามเขียน array เดียวกัน
  - ตรวจ: `cBenf_1/cRelate_1` และ `_2` ครบ, slot 1 ถูก skip แต่ emitted item แรกมี `seq:2`, partially filled item, required empty array, duplicate group/direct conflict และ nested item path ผ่าน

- [x] **23.8 สร้าง Payload Binding Module สำหรับ System-bound Paths**
  - ทำ: compile server-owned bindings จาก runtime value source เช่น ContractReference/FileJobId/SourceRowId/SourceRowNumber/ConfigVersionReference/OutputKey ไป Contract Path; เติมค่าหลัง business mapping; ตรวจ conflict/type/required และ final schema validation; คืน immutable Delivery Payload
  - เข้าใจ: field/path ของ PA MTLS ห้าม hard-code; Module มี Interface เดียวที่ซ่อน insertion/conversion/final validation และไม่มี HTTP/SQL side effect
  - ตรวจ: PA MTLS illustrative payload เติม envelope ได้; profile ที่สอง bind runtime values ไป path อื่นโดยไม่แก้ code; system/mapped conflict, missing required binding และ invalid conversion คืน exact diagnostics

- [x] **23.9 ขยาย validate/preview/capabilities API สำหรับ ApiContract**
  - ทำ: validate draft ด้วย snapshot/path catalog/TargetSchema/Transformation v2/Array Groups/bindings; preview คืน Input → Source → Mapped Document → Delivery Payload/Request summary → Diagnostics; ไม่สร้าง Delivery Jobและไม่ call endpoint
  - เข้าใจ: preview ใช้ schema/profile safe metadata แต่ credential/secret/raw internal URL ไม่ออก response; example idempotency key ต้องระบุว่า preview เท่านั้น
  - ตรวจ: preview outputs `0..N`, skipped array items/reasons, system-bound values และ hash/reference ถูกต้อง; DB/outbox/HTTP side effects เป็นศูนย์; malformed browser path ถูก server rejectแม้ UI ไม่เปิดให้เลือก

- [x] **23.10 ทำหน้า Destination และ Contract Schema flow**
  - ทำ: เปิด `ApiContract` option หลัง feature flag; เลือก profile/contract/version, resolve schema preview, แสดง hash/fetched-at/drift; เมื่อเลือก LegacyTable แสดง Normalized Table เดิม; form state เป็น discriminated unionและ clear fields ของอีก destination
  - เข้าใจ: schema resolve ตอน draft เป็น preview ส่วน Create Version server resolve/snapshot authoritative อีกครั้ง; destination เปลี่ยนต้องเป็น version ใหม่
  - ตรวจ: ไม่มี Normalized Table/create table control ใน API mode; ไม่มี raw URL/header/credential; loading/drift/error/disabled-profile states ครบ; clone/history แสดง destination ถูก

- [x] **23.11 สร้าง Contract Path browser และ scalar mapping editor**
  - ทำ: แสดง searchable tree พร้อม type/required/constraints/description; object/array expand ได้แต่ assign ตรงไม่ได้; System-bound leaves lock พร้อม runtime source; Mapped leavesเลือก expression editor เดิม; diagnostics map path กลับ control
  - เข้าใจ: path list มาจาก version/draft catalog ไม่ประกอบเองจากชื่อ; browser และ Worker ใช้ compiler contract เดียวกัน
  - ตรวจ: nested paths เลือกได้, system path เลือกไม่ได้, required missing/type mismatch/unknown path ชี้ control ถูก; LegacyTable editor ยังแสดง flat columnsและ v1 config แก้/clone ได้

- [x] **23.12 สร้าง Array Group editor**
  - ทำ: เลือก target array, sequence integer leaf, empty policy; เพิ่ม/reorder source slotsและ item fields; item target ใช้ relative path; แสดง derived `slot → seq`; block fixed-index/group conflict
  - เข้าใจ: UI สร้าง AST เดียวกับ backend ไม่มี JSON-specific hidden payload; `seq` เป็น system-derivedภายใน group ไม่ใช่ manual expression
  - ตรวจ: สร้าง MTLS beneficiary slots 1–2 ผ่าน UI; blank/`-` preview แสดง skipped reason; `0`/`false` ไม่ถูก skip; errors หลัง reorder/remove ยัง map index/control ถูก

- [x] **23.13 เพิ่ม Delivery Preview, version compare และ frontend acceptance**
  - ทำ: เพิ่ม tabs Input/Source/Mapped/Delivery/Diagnostics; แสดง endpoint profile/method/pathแบบ safe, headers ที่ไม่ secret, schema hash, idempotency example และ JSON read-only; compare destination/reference/hash/paths/groups/bindings; ห้ามมี send buttonใน Phase นี้
  - เข้าใจ: design-time จบก่อน runtime side effect; ผู้ใช้ตรวจ payload ทุก branch ได้โดยไม่ยิง partner
  - ตรวจ: PA MTLS และ second profile config/preview ผ่าน UI; JSON ตรง golden snapshot; preview ไม่เพิ่ม rows/jobsและไม่ network callออก partner; lint/build/E2E ผ่าน

- [x] **23.14 Gate C — contract design-time sign-off**
  - ทำ: รัน schema drift, nested/object/array, payload binding, preview security และ legacy parity suites; ทบทวน unsupported schema subsetกับ contract owner; บันทึก feature flags/rollback
  - เข้าใจ: Phase 24 เริ่มได้เมื่อ final Delivery Payload deterministicและผ่าน contract validationโดยยังไม่มี external delivery
  - ตรวจ: requirements R1–R4/R26–R31/PAY trace ไป test ได้; two-profile portabilityผ่าน; secrets/PIIไม่อยู่ logs; legacy suite green; Gate C sign-off

---

## Phase 24 — API Delivery, Retry, Reprocess และ Strangler Rollout

- [x] **24.0 ปิด Required Decisions ก่อนทำ side effect**
  - ทำ: ยืนยัน per profile เรื่อง auth, idempotency header/key/retention, timeout, retry/backoff, success response, correlation ID, reprocess หลัง Delivered, payload encryption/retention, identifier formats, `extras` policy, SLA และ risk thresholds; สร้าง stub contract fixtures
  - เข้าใจ: PA MTLS example ไม่ตัดสิน policy เหล่านี้แทน partner contract; ไม่มี production send ก่อน decision owner sign-off
  - ตรวจ: decision checklist มี owner/date/value ทุกข้อ; API stub จำลอง 2xx/400/408/429/5xx/timeout/commit-before-timeout/idempotency conflict ได้
  - ผล: บันทึก owner sign-off และ rollback policy ที่ `docs/phase-24-delivery-decisions.md`; เพิ่ม stub scenario fixture ครบทุก outcome และ automated fixture/policy tests

- [x] **24.1 เพิ่ม Delivery Job, Attempt และ outbox schema**
  - ทำ: migration เพิ่ม `delivery_jobs` ที่เก็บ file/row/source/config/output identity, destination/contract/hash, payload JSON/bytes + hash, idempotency key, status/attempts/next-at/http/correlation/redacted error/timestamps; เพิ่ม delivery attempts audit และ outbox message; unique constraint ตาม approved identity
  - เข้าใจ: retry โหลด persisted payload bytes/key เดิมและไม่ rebuild จาก Source/schema/config ล่าสุด; schema เป็น expand-only เพื่อ rollback workerได้
  - ตรวจ: migrations/up/down policy ผ่าน test DB; duplicate schedulingไม่สร้าง jobซ้ำ; encrypted/retention fieldsเป็นไปตาม decision; indexesรองรับ pending queue/file-job lookup
  - ผล: migration `0009_api_delivery.sql` เป็น expand-only เพิ่ม encrypted Delivery Job, attempt audit, resend audit, identity constraints, lease/queue/file indexes และ retention timestamps; rollback ใช้ worker/activation disable โดยไม่ drop audit ระหว่าง retention

- [x] **24.2 Implement ApiContract scheduling adapter**
  - ทำ: เพิ่ม second real adapter ที่รับ final Delivery Payload แล้วสร้าง Delivery Job + outbox ใน transaction ของ Row Job; ApiContract pathไม่เรียก NormalizedRowWriter/DDL; `Filtered`/zero outputไม่สร้าง delivery; invalid outputบันทึก row errors
  - เข้าใจ: adapter นี้ schedule durable work ไม่ทำ HTTP ภายใน RowNormalize transaction; one output = one Delivery Job
  - ตรวจ: one row two outputsสร้างสอง jobs/keys; Source Tableยังมี raw row; query/probeยืนยันไม่มี Normalized Table insert/update/delete; transaction crashไม่เกิด jobโดยไม่มี outboxหรือกลับกัน
  - ผล: ApiContract adapter bind/validate pinned snapshot แล้ว encrypt payload และ insert job+outbox ใน Row Job transaction; integration test ยืนยัน two outputs, rollback atomic, duplicate no-op และไม่มี Normalized destination/write

- [x] **24.3 เพิ่ม Delivery consumer/worker และ HTTP adapter**
  - ทำ: เพิ่ม topic/message/dispatcher/consumer; resolve server-owned profile+secret, สร้าง requestจาก persisted payload, allowed headersและ idempotency key; ใช้ timeout/cancellation; redact logs; บันทึก response metadataโดยไม่เก็บ body default
  - เข้าใจ: Delivery Worker ไม่ fetch schemaและไม่ re-evaluate transformation; HTTP/auth/retry classificationอยู่หลัง Destination adapter implementation
  - ตรวจ: local stubได้รับหนึ่ง requestต่อ jobพร้อม exact bytes/header; secretไม่อยู่ DB/log; disabled profile pause/fail controlled; cancellation/disposal/connection reuseผ่าน tests
  - ผล: เพิ่ม delivery consumer, reusable authenticated transport สำหรับ mTLS/OAuth2 client credentials, server-owned environment secret provider, exact-byte HTTP adapter, timeout/cancellation, correlation metadata และ body-free/redacted logging

- [x] **24.4 ทำ retry classifier, lease และ idempotent state machine**
  - ทำ: classify network/timeout/408/429/5xx เป็น retryableและ 4xxอื่น permanentตาม profile; bounded exponential backoff+jitter; atomic lease/attempt update; recover stale Delivering; preserve bytes/hash/keyทุก attempt
  - เข้าใจ: timeoutหลัง partner commitเป็นเหตุผลที่ endpointต้องรองรับ idempotency; process crashต้องไม่ทำให้ stateสูญหายแม้อาจเกิด at-least-once call
  - ตรวจ: failure injectionครบก่อน/หลัง send/response persistence; concurrent consumersส่ง jobเดียวตาม lease policy; retriesมี hash/keyเดิม; max attemptsไป terminal stateและไม่ busy-loop
  - ผล: เพิ่ม atomic lease/stale recovery, persisted attempt audit, bounded backoff+jitter/Retry-After, due-job scheduler และ terminal max-attempt guard; integration test ยืนยัน 503→202 ใช้ payload/hash/keyเดิม และ commit-before-timeout ยัง retry ด้วย keyเดิม

- [x] **24.5 เชื่อม Row/File Job status โดยไม่ปน transformation กับ delivery**
  - ทำ: กำหนด transformation completionกับ delivery lifecycleแยก field/summary; ขยาย File Job responseให้แสดง Pending/Delivering/Delivered/RetryableFailed/PermanentFailed countsและ end-to-end status; Legacy responseยัง compatible
  - เข้าใจ: Row transformationสำเร็จไม่ได้แปลว่า partnerรับแล้ว; status modelต้องไม่ทำให้ legacy consumerตีความผิด
  - ตรวจ: status matrixสำหรับ zero/one/multiple outputsและ mixed outcomesตรง spec; polling API deterministic; old clientsยังอ่าน fieldsเดิมได้
  - ผล: File Job response คง fields เดิมและเพิ่ม additive delivery counts/status/end-to-end status; transformation Row Job จบหลัง durable scheduling โดยไม่รอ partner HTTP

- [x] **24.6 ทำ retry/reprocess/resend semantics**
  - ทำ: retry Delivery Jobเดิมใช้ payload/keyเดิม; reprocess Source rowด้วย Config Versionใหม่สร้าง identityตาม approved policy; Delivered resendต้อง explicitและมี audit/authorization hook; pending jobsห้ามแก้ payload; Legacy reprocess `2→1→0`คงเดิม
  - เข้าใจ: retry, reprocessและbusiness resendเป็นคนละ action; rollbackห้าม reroute pending API jobไป LegacyTableอัตโนมัติ
  - ตรวจ: retry determinism, new-version reprocess, duplicate request, Delivered guardและauditผ่าน; source/config/schemaเปลี่ยนแล้ว retryเดิมยังส่ง bytesเดิม
  - ผล: manual retry อนุญาตเฉพาะ RetryableFailed และไม่แก้ payload/key; Delivered retry ได้ 409; explicit resend ต้องผ่าน authorization hook/เหตุผลและสร้าง audited job/key ใหม่; Config Version ใหม่สร้าง identity ใหม่ ขณะที่ version เดิม dedupe

- [x] **24.7 เพิ่ม Delivery status/retry UI**
  - ทำ: ขยาย File Job detailsหรือสร้างหน้า delivery list; แสดง contract/config/output key, status, attempts, next attempt, HTTP status, correlation, redacted errorและ payload hash; retry buttonเฉพาะ policyอนุญาตพร้อม confirmation; ไม่แสดง secret/PII body default
  - เข้าใจ: operation UIต้องแยก automatic retryจาก explicit reprocess/resend; APIเป็น authoritative permission/state check
  - ตรวจ: mixed delivery states renderครบ; stale actionได้ 409พร้อม refresh; accessibility/loading/error states, lint/build/E2Eผ่าน; browser devtoolsไม่มี credential
  - ผล: เพิ่ม `/delivery` operations UI, file-job selector, mixed status/attempt/next-at/HTTP/correlation/redacted error/hash และ policy-aware retry confirmation; stale 409 refresh จาก API authoritative state และ Playwright ยืนยันว่าไม่มี payload body/credential

- [x] **24.8 เพิ่ม observability, alert และ kill switches**
  - ทำ: metrics success/failure/status distribution, p50/p95/p99 latency, queue age, retry count, duplicate conflict, schema validation failureและ source-to-delivered durationแยก config/contract; alert thresholdsจาก 24.0; kill switch pause deliveryและ disable new ApiContract activation
  - เข้าใจ: kill switchหยุด side effectแต่ไม่เปลี่ยน destinationของ pinned jobs; metric labelsต้องไม่ใช้ PII/high-cardinality IDs
  - ตรวจ: dashboards/alertsจาก stub failuresทำงาน; pauseแล้วไม่มี outbound callแต่ jobsคง durable; resumeส่งต่อด้วย keyเดิม; runbookระบุ ownerและrollback trigger
  - ผล: เพิ่ม SQL-backed operations snapshot/UI, status/success/failure/latency/queue/retry/conflict/schema/end-to-end metrics, approved threshold alerts และ audited database kill switches; schedulerหยุด dispatchและ queued consumerคืนงาน durableเมื่อ pause

- [x] **24.9 Shadow validation กับ stub/sandbox**
  - ทำ: เปิด scheduling/deliveryเฉพาะ test Mapping Configที่ชี้ non-mutating stub/sandbox; เปรียบเทียบ golden Delivery Payloadและ contract response; soak retry/crash/queue behavior
  - เข้าใจ: ห้าม dual-send production proposalไป partnerจริงและ LegacyTableเพื่อเทียบ; shadow environmentต้องไม่มี business side effect
  - ตรวจ: contract validation 100%, duplicate business recordsเป็นศูนย์, queue age/error/latencyอยู่thresholdและ security log scanผ่านก่อน canary
  - ผล: เพิ่ม profile environment `Stub|Sandbox|Production`, rollout stage `Shadow` และ activation guardที่ห้าม Shadowชี้ Production; deterministic stub fixture/testsครอบคลุม outcomes, exact bytes/headers, retry identityและ no body/credential exposure

- [x] **24.10 Canary ราย Mapping Config และ rollback rehearsal**
  - ทำ: เลือก low-risk partner/config, สร้าง ApiContract versionและ activateสำหรับ filesใหม่; monitorหนึ่ง release cycle; ซ้อม pause pending jobs, activate previous LegacyTable versionสำหรับ filesใหม่และ compensation pathสำหรับ delivered side effect
  - เข้าใจ: canaryเป็น config-by-config ไม่ใช่สุ่มเปอร์เซ็นต์ภายใน config; already pinned API jobsยังคง APIหรือ pauseตาม runbook
  - ตรวจ: rollback timeอยู่ SLA, filesใหม่หลัง rollbackใช้ LegacyTable, pending API jobsไม่ถูก reroute, Delivered side effectมี audit/compensation ownerและ metricsผ่าน gate
  - ผล: เพิ่ม audited config-by-config rollout (`Shadow/Canary/Expanded`), activation gate และ configure script; integration rehearsalยืนยัน rollbackต่ำกว่า local SLA, jobเดิมยัง pin APIและ fileใหม่ pin Legacy version โดย runbookระบุ compensation owner

- [x] **24.11 Full end-to-end acceptance**
  - ทำ: ทดสอบ `.txt/.csv/.xlsx/.xls` → Source → nested mapping/array/system bindings → one API request per output พร้อม retry/reprocess; ทดสอบ LegacyTableคู่ขนานคนละ config; รวม failure injectionและschema drift
  - เข้าใจ: acceptanceวัดทั้งสอง destinationและ immutable version behavior ไม่ใช่เพียง happy-path JSON shape
  - ตรวจ: requirementsทุกข้อ traceถึง automated test; no Normalized writesใน API mode; raw Source auditครบ; retry bytes/keyเดิม; second contract profileผ่านโดยไม่แก้ core; full backend/web regression green
  - ผล: Gate D matrix trace inputสี่ชนิด, nested/array/system binding, API scheduling/send/retry/reprocess, no Normalized write, second profile, Legacy parity, rollout/rollbackและ UI securityไปยัง automated suites; full regression commandsอยู่ใน acceptance/runbook

- [x] **24.12 Documentation, operations และ commit handoff**
  - ทำ: อัปเดต CONTEXT, README, current workflow, OpenAPI/examples, configure/reset scripts, secret/profile setup, migration/rollback/retention/runbooksและ learning plan; ระบุ prototype/superseded specชัด; สรุป files/migrations/tests/known limits
  - เข้าใจ: `LegacyTable` retireได้ภายหลังเมื่อทุก partnerผ่านอย่างน้อยหนึ่ง release cycleและไม่มี pending retention obligations; Phase นี้ยังไม่ลบ adapter
  - ตรวจ: คนใหม่ทำ local stub flowและ rollbackตาม READMEได้; linksไม่เสีย; ไม่มี secret/sample PII; all checksผ่าน; ผู้ใช้เป็นผู้สั่ง commitแยกต่างหากและ AI agentไม่สร้าง commitเอง
  - ผล: อัปเดต README/CONTEXT/current workflow/learning plan, เพิ่ม operations runbook, acceptance matrix, Gate D handoff, rollout script, profile/secret/OpenAPI guidance และ hourly payload/audit retention cleanup; ระบุ prototype/superseded spec/known limitsชัดและไม่สร้าง commit

### Definition of Done ของ Feature ใหม่

- Config Versionหนึ่งรุ่นเลือก `LegacyTable` หรือ `ApiContract`ได้แบบ immutable
- Config/versionเก่าทั้งหมดยังทำงานเป็น LegacyTableและ golden parityผ่าน
- `.txt`, `.csv`, `.xlsx`, `.xls` เข้า Source Tableด้วย versioned Input Layoutและไม่มี partial importเมื่อ fail
- ApiContract versionเก็บ canonical Contract Snapshot/hashและ Payload Binding Snapshot; runtimeไม่ fetch schema
- UI map Source/expressionไป nested Contract Path, fixed indexและ Array Groupได้ โดย System-bound Pathเป็น read-only
- array empty/seq semanticsตรง R29–R31 และ profileที่สองพิสูจน์ว่า coreไม่ hard-code PA MTLS
- API modeเก็บ raw Source แต่ไม่ scaffoldหรือเขียน Normalized Table
- mapped outputแต่ละรายการมี durable Delivery Job, request, retryและidempotencyแยกกัน
- previewไม่มี DB/file/API side effect; logs/responseไม่เผย credentialหรือ payload PIIโดย default
- metrics, kill switch, canary, rollbackและrunbookผ่านการซ้อม
- backend tests, integration/E2E, formatter, web lint/buildและsecurity checksผ่านก่อน activate production profile

---

## ภาคต่อ Feature ใหม่ Phase 25 — Variant C และ JSON ทั้งไฟล์

### Product decision ที่ยืนยันแล้ว

- เลือก Prototype C (`Mapping workbench`) เป็นทิศทางของ production UI แต่ต้องเขียน production implementation จาก Module และ Interface จริง ห้าม promote throwaway prototype ตรง ๆ
- `Whole file · Batch` หมายถึง destination ใหม่ `JsonFile`: รวม mapped outputs ของหนึ่ง File Job เป็น JSON array หนึ่งไฟล์
- `One record at a time` หมายถึง `ApiContract` เดิม: หนึ่ง mapped output เป็นหนึ่ง Delivery Job และหนึ่ง HTTP request
- Production config editor ยังต้องรองรับ `LegacyTable` เป็นตัวเลือกที่สาม แม้ Prototype C เน้นเปรียบเทียบ JSON file กับ API
- Config Version หนึ่งรุ่นเลือก destination ได้หนึ่งชนิดแบบ immutable: `LegacyTable`, `ApiContract` หรือ `JsonFile`
- UI ใช้คำว่า Delivery mode เพื่อให้ผู้ใช้เข้าใจง่าย แต่ persisted model ใช้ discriminated union `destination.kind`; ไม่เพิ่ม `deliveryMode` แยกอีก field เพราะจะสร้าง combination ที่ไม่มีความหมาย เช่น `jsonFile + perRecord`
- Config Version เดิมไม่ถูก rewrite: destination ที่หายไปยัง normalize เป็น `LegacyTable`, `apiContract` เดิมยังส่งทีละ mapped output และ `legacyTable` เดิมยัง reconcile Normalized Table เหมือนเดิม
- การเลือก `JsonFile` ต้องเกิดจากการสร้างหรือ clone เป็น Config Version ใหม่เท่านั้น ห้ามเปลี่ยน destination ของ File Job หรือ Config Version ที่ pin แล้ว
- Phase นี้เพิ่ม destination ทางเลือก ไม่ทำ dual-delivery; mapped output ชุดเดียวต้องไม่ถูกส่งทั้งไฟล์, API และ Legacy Table พร้อมกัน

### Config model ที่เลือก

```json
{
  "destination": {
    "kind": "jsonFile",
    "fileDestinationProfileId": "local-json-export",
    "contract": {
      "id": "customer-export",
      "version": "1",
      "reference": "customer-export/1",
      "schemaHash": "<sha256>"
    },
    "relativeFolder": "customers",
    "fileNameTemplate": "{sourceBaseName}.{fileJobId}.json",
    "formatting": "compact"
  }
}
```

`fileDestinationProfileId` อ้าง server-owned profile ที่กำหนด Output Root, temporary publish directory, quota และ retention; browser ส่งได้เฉพาะ relative folder กับ filename template ที่ผ่าน allowlist ส่วน v1 ตรึง document root เป็น array, encoding เป็น UTF-8 without BOM และ row error policy เป็น fail whole file จึงแสดงเป็น read-only summary ใน UI แทนการเพิ่ม field ที่ยังไม่มีทางเลือกจริง

`JsonFile` reuse Contract Snapshot, Target Schema, nested path mapping, Array Group, Document Assembler และ Payload Binding ที่ pin ใน Config Version เดียวกับ `ApiContract` แต่ไม่มี endpoint/auth และไม่สร้างหรือเขียน Normalized Table

### Target flow และ seam

```text
Input file → Source rows → Transformation Module → 0..N mapped outputs
                                                   │
                                      Destination router by kind
                         ┌─────────────────┼──────────────────┐
                         ▼                 ▼                  ▼
               LegacyTable Adapter  ApiContract Adapter  JsonFile Adapter
               reconcile per row    schedule per output  stage per row
                                                               │
                                                all latest Row Jobs terminal
                                                               ▼
                                                    JsonFile Finalizer
                                              temp file → fsync → atomic rename
```

`JsonFile Adapter` เป็น Adapter ตัวที่สามหลัง Destination Module seam ไม่ใช่ branch ของ Transformation Module ความซับซ้อนเรื่อง ordering, staging, retry, atomic publish และ path safety ต้องอยู่หลัง Interface ของ adapter/finalizer เพื่อให้ caller ไม่ต้องรู้วิธีประกอบไฟล์

### ขอบเขต Phase 25

อยู่ในขอบเขต:

- JSON array หนึ่งไฟล์ต่อหนึ่ง File Job โดยเรียง `rowNumber` แล้ว `outputOrdinal`
- zero output สำเร็จเป็น `[]`; Source row หนึ่งแถวที่ emit หลาย branch เขียนหลาย object ตาม branch order
- temporary result ที่รองรับ parallel Row Jobs, retry และ reprocess `2 → 1 → 0`
- atomic publish, SHA-256, deterministic bytes, crash recovery และ status ระดับ File Job
- typed Value Expressions `add` และ `concat` ที่ใช้ร่วมกันได้ทั้ง `LegacyTable`, `ApiContract` และ `JsonFile`
- validate/preview แบบไม่มี DB/file/API side effect
- Production Mapping workbench ตาม Variant C พร้อม destination selector และ live preview

อยู่นอกขอบเขต:

- HTTP batch request ที่ส่ง array ทั้งไฟล์ไป partner endpoint
- JSON หนึ่งไฟล์ต่อ mapped output
- dual-delivery หรือ fallback อัตโนมัติข้าม destination
- raw absolute path, storage credential หรือ arbitrary filename expression จาก browser
- object storage implementation จนกว่า Step 25.0 จะเลือกและทดสอบ Adapter แยกอย่างชัดเจน

### Gate E และ rollback

| Gate | ผ่านเมื่อ | Rollback |
|---|---|---|
| Gate E หลัง Phase 25 | existing config parity ผ่าน, JSON bytes/order/hash deterministic, invalid row ไม่ publish, retry ก่อน/หลัง rename ไม่สร้างไฟล์ซ้ำ, Variant C ใช้ API จริงและ E2E ครบสี่ input formats | ปิดการสร้าง/activate `JsonFile` version, pause JsonFile finalizer และ activate version เดิมสำหรับ File Jobs ใหม่; pinned JsonFile jobs คง destination เดิมหรือ pause ตาม runbook ห้าม reroute ไป API/LegacyTable |

## Phase 25 — Implement Variant C และ `JsonFile` Destination

- [x] **25.0 แก้ authoritative specification และปิด Required Decisions**
  - ทำ: แก้ `spec/spec-design-contract-api-legacy-delivery.md` ให้เพิ่ม `JsonFile` เป็น destination ที่สามและบันทึกว่า decision ใหม่นี้ supersede ข้อความที่ตัด `JsonFile` ออกจาก scope; ใช้ `spec/spec-design-file-to-json-export.md` เฉพาะ requirement เรื่อง ordering/atomicity/recovery; ยืนยัน storage ชนิดจริง, Output Root/profile owner, file/row/byte limits, temp/final volume, retention/encryption, filename tokens, collision/reprocess policy, formatting, download authorization, cleanup SLA และ rollout owner
  - เข้าใจ: การเลือก Variant C ยืนยัน interaction แต่ยังไม่ตอบ operational policy; implementation เริ่มได้เมื่อ contract ของไฟล์, ownership และ recovery semantics เป็นข้อยุติเดียวกัน
  - ตรวจ: specification ไม่มีข้อความขัดกันเรื่องจำนวน destination; decision table มี owner/date/value ทุกข้อ; requirement ทุกข้อใน Phase 25 trace ไป acceptance ID ได้; review ยืนยันว่า `JsonFile` ไม่ใช่ HTTP batch และไม่ใช่ dual-send
  - ผล: ยกระดับ authoritative specification เป็น v2.0/Accepted และรองรับ discriminated union สาม destination; ตรึงว่า `JsonFile` reuse Contract Snapshot/Target Schema/Document Assembler แต่ stage ต่อ row และ publish JSON array ระดับ File Job โดยไม่สร้าง API Delivery Job/Normalized write; ปิด filesystem profile, limits, same-volume atomic rename, retention/encryption, filename/collision/reprocess, formatting, no-download, cleanup SLA, rollout/rollback owner ครบวันที่ 2026-10-21; เพิ่ม JF/EXP requirements, acceptance IDs และ traceability ครบ Step 25.0–25.14 โดยยังเก็บ spec File-to-JSON เดิมเป็น historical ordering/atomicity/recovery reference เท่านั้น

- [x] **25.1 ตรึง compatibility baseline ก่อนเพิ่ม destination**
  - ทำ: เพิ่ม characterization fixtures สำหรับ config เก่าที่ไม่มี destination, explicit `legacyTable`, `apiContract`, clone/version activation, pinned File Job และ retry payload เดิม; เก็บ golden responses/DB effects ของทั้ง LegacyTable และ ApiContract
  - เข้าใจ: default ของข้อมูลเก่าคือ behavior ที่มีอยู่จริง ไม่ใช่ค่าเริ่มต้นใน prototype; baseline นี้ป้องกัน migration หรือ router ใหม่เปลี่ยน destination เงียบ ๆ
  - ตรวจ: fixtures เดิม round-trip โดยไม่ rewrite JSON; missing destination ยังเป็น LegacyTable; ApiContract หนึ่ง output ยังสร้างหนึ่ง Delivery Job/HTTP request; query ยืนยันจำนวน Normalized writes, delivery jobs และ outbox เท่า baseline
  - ผล: เพิ่ม `Fixtures/Phase25/compatibility-baseline.golden.json` ตรึง request/response ของ missing destination, explicit `legacyTable`, `apiContract`, Legacy/API DB effects และ retry payload; เพิ่ม contract + PostgreSQL integration tests ที่ clone version ผ่าน public service, activate สลับรุ่น, intake File Job ก่อน/หลัง activation และยืนยัน pinned Config Version/JSON เดิม/outbox; ผูก LegacyTable และ ApiContract runtime tests กับ golden counts โดยยืนยันหนึ่ง Delivery Job/outbox ต่อ output, หนึ่ง HTTP request ต่อ attempt และ retry ใช้ payload bytes/idempotency เดิม ระหว่าง full regression พบและแก้ Dapper `timestamptz` mapping ของ operations/delivery status โดยคง `DateTimeOffset` response contract และแก้ success-rate denominator ให้ตรง golden behavior; `dotnet format --verify-no-changes` ผ่าน และ full backend/PostgreSQL integration ผ่าน 316/316 โดยบังคับไม่ให้ skip

- [x] **25.2 เพิ่ม `JsonFileDestination` แบบ expand-only**
  - ทำ: เพิ่ม derived type `JsonFileDestination` ใน `DestinationDefinition`, request/response/OpenAPI และ TypeScript union; เพิ่ม validator สำหรับ profile ID, Contract Reference, relative folder, filename template และ formatting; ทำ migration ขยาย destination kind check โดยไม่ update row เดิม และให้ `normalizedTableId`/`apiDestinationProfileId` เป็น null ตามชนิด destination
  - เข้าใจ: persisted discriminated union เป็น source of truth ส่วนคำว่า Delivery mode เป็น presentation; Config Version เดิมจึงไม่ต้องมี field ใหม่และไม่มี default ที่เปลี่ยน behavior
  - ตรวจ: serialize/deserialize ทั้งสามชนิดผ่าน; unknown/ผสม field ข้ามชนิดถูก reject ด้วย exact path; path traversal, absolute path, separator ที่ไม่อนุญาตและ token ไม่รู้จักถูก reject; migration up + application rollback compatibility ผ่าน test DB
  - ผล: เพิ่ม `JsonFileDestination` พร้อม Contract Reference, logical File Destination Profile, relative folder, filename template และ `compact`/`indented` ใน .NET request/response, OpenAPI `oneOf` และ TypeScript union; validator ใช้ทั้ง raw/typed seam และปฏิเสธ unknown/mixed fields, unsafe Windows path/name, traversal/absolute path, separator, reserved/overlong name และ token นอก allowlist ด้วย exact path ก่อนถึง filesystem; migration `0011_json_file_destination.sql` ขยาย kind check โดยไม่ rewrite row เดิม และ PostgreSQL test ยืนยัน row token/JSON เดิมไม่เปลี่ยน, Legacy application flow ยังสร้าง/อ่าน version ได้ และ JsonFile row ใช้ `normalized_table_id`/`api_destination_profile_id` เป็น null; ตรึง expand-only guard ไม่ให้ create/activate JsonFile ผ่าน runtime ก่อน Step 25.3; full backend/PostgreSQL integration ผ่าน 341/341 โดยไม่ skip, web lint/build และ `dotnet format --verify-no-changes` ผ่าน

- [x] **25.3 แยก Contract Snapshot/Target Schema ออกจาก HTTP transport**
  - ทำ: reuse schema fetch/canonicalize/hash, Payload Binding Snapshot, Target Schema และ Document Assembler สำหรับ `JsonFile`; เพิ่ม server-owned File Destination Profile resolver โดยไม่บังคับ endpoint/auth; resolve และ pin snapshot ตอนสร้าง versionเหมือน ApiContract และไม่ fetch schemaตอน runtime
  - เข้าใจ: JSON file กับ API ต่างกันที่ Destination Adapter ไม่ใช่ mapping language; contract-aware Module เดียวต้องประกอบ document เดียวกันได้ก่อนเลือก transport
  - ตรวจ: contract fixture เดียวกันให้ mapped document เท่ากันแบบ parsed JSON ใน JsonFile/ApiContract; nested object, fixed index, Array Group, system binding และ second profile ผ่านโดยไม่ hard-code path; secret/output root ไม่อยู่ response หรือ browser state
  - ผล: เพิ่ม transport-neutral contract profile และ server-owned File Destination Profile resolver; JsonFile resolve/pin Contract Snapshot, Target Schema และ Payload Binding ตอนสร้าง version โดยไม่ fetch schema ตอน runtime; Document Assembler parity ครอบคลุม nested object, fixed index, Array Group, system binding และ second profile พร้อมยืนยันว่า output root/encryption policy ไม่รั่วสู่ response/browser state

- [x] **25.4 เพิ่ม typed Value Expressions `add` และ `concat` แบบ end-to-end**
  - ทำ: เพิ่ม `AddExpression` (`kind: "add"`, `values[]`) และ `ConcatExpression` (`kind: "concat"`, `values[]`, `separator`) ใน polymorphic backend model, request/response/OpenAPI, TypeScript union และ capabilities; ขยาย compiler/type inference, evaluator, dependency/node/depth counting, diagnostics และ production `ValueExpressionEditor`; กำหนดอย่างน้อย 2 operandsและใช้ collection limitเดิม โดย `add` รับเฉพาะ Integer/Decimal, promoteผลเป็น Decimalเมื่อมี Decimalอย่างน้อยหนึ่ง operandและใช้ checked overflow ส่วน `concat` รับเฉพาะ Textและ separatorเป็น string literal
  - เข้าใจ: ทั้งสอง expression อยู่ใน Transformation Moduleและทำงานก่อนเลือก Destination Adapter; v1 ใช้ null propagation—operandใดเป็น nullให้ผลเป็น null—เพื่อไม่แอบแทนค่าด้วย `0`/ข้อความว่าง ผู้ใช้ต้องใช้ `coalesce` เมื่อต้องการ default และต้องใช้ explicit `convert` ที่ compilerรองรับเมื่อต้องการเปลี่ยนชนิด
  - ตรวจ: serialization reject unknown/missing/extra fields; compiler reject operandsน้อยกว่า 2, typeไม่ตรง, collection/depth/nodeเกิน limitพร้อม exact path `values[i]`; testsครอบ Integer+Integer, Integer+Decimal promotion, negative/zero, decimal/integer overflow, Thai/Unicode concat, empty/non-empty separator, nested expression, null propagationและ `coalesce`; evaluator/preview/runtimeให้ typed resultและ diagnosticเดียวกัน; capabilitiesซ่อน controlจาก clientเก่าที่ยังไม่รองรับ; config/versionเดิม round-tripและ evaluateเท่าเดิม
  - ผล: เพิ่ม `AddExpression`/`ConcatExpression` ครบ polymorphic JSON และ OpenAPI, compiler inference/limits/dependencies, null-propagating evaluator พร้อม checked overflow diagnostics, capabilities/TypeScript union และ production editor ที่บังคับอย่างน้อย 2 operands; focused tests ครอบคลุม promotion, Unicode, separator, nested/null/coalesce/overflow และยืนยัน typed result ผ่าน preview กับ worker runtime โดย full regression เดิมยังผ่าน

- [x] **25.5 ทำ Destination router ให้รองรับ Adapter ตัวที่สามโดยไม่เพิ่ม type branch กระจาย**
  - ทำ: สร้าง registry/router หลัง Destination Module seam ที่เลือก adapter จาก `destination.kind`; ทำ transaction-aware row delivery Interface ให้ LegacyTable reconcile, ApiContract schedule และ JsonFile stage ผ่าน call site เดียว; ย้าย special-case `IsApiContract` ออกจาก `RowNormalizeHandler` เมื่อ characterization tests ป้องกัน behavior แล้ว
  - เข้าใจ: caller ควรรู้เพียง compiled destination + Delivery Envelope; transport-specific transaction, persistence และ completion semantics อยู่หลัง Adapter เพื่อให้เพิ่ม destination แล้วไม่เพิ่ม `if` ในทุก caller
  - ตรวจ: router มี adapter ครบทุก declared kindและ fail fastเมื่อ registration หาย/ซ้ำ; tests เรียก Interface เดียวกับ production caller; Legacy/API golden baseline จาก 25.1 ผ่าน byte/row/job parity; deletion test ยืนยันว่าถ้าลบ router complexity จะกลับไปกระจายใน caller
  - ผล: เพิ่ม `IDestinationRouter`/registry ที่ตรวจ declared destination ทุกชนิดและ fail fast เมื่อ adapter หาย/ซ้ำ พร้อม transaction-aware `IDestinationAdapter` call เดียวสำหรับ compile/deliver; ย้าย Legacy reconcile และ API schedule ให้อยู่หลัง adapter, ลงทะเบียน JsonFile adapter slot สำหรับ staging ใน Step 25.7 และลบ `IDestinationModule`, `DeliveryAttemptResult`, `IsApiContract` กับ concrete-adapter dependency ออกจาก `RowNormalizeHandler`; router/architecture tests บังคับให้ production caller พึ่ง seam เดียว และ PostgreSQL full suite ผ่าน 366/366 โดยไม่ skip ครอบคลุม Legacy golden behavior, API byte/row/job/outbox parity, rollback และ idempotency

- [x] **25.6 เพิ่ม JSON export persistence และ outbox แบบ expand-only**
  - ทำ: เพิ่ม `json_export_jobs` สำหรับ identity/status/profile/temp/final path/object count/bytes/hash/lease/error/timestamps และ `json_export_items` สำหรับ file/config/source/row/output identity, output ordinal, canonical payload, hash และ retention; เพิ่ม unique constraints/indexes และ outbox message สำหรับ finalization
  - เข้าใจ: Row Jobs ทำงาน parallel/retry จึงห้ามเขียน final file โดยตรง; durable staged items เป็นจุดรวมที่ทำให้ ordering, reconciliation และ crash recovery deterministic
  - ตรวจ: duplicate stage เป็น idempotent; key รองรับหนึ่ง row หลาย outputs และ reprocess; query ตาม file/order ใช้ index; migration ไม่ lock/rewrite payload เดิมเกิน policy; encryption/retention/cleanup fields ตรง decision 25.0
  - ผล: เพิ่ม expand-only migration `0012_json_export_persistence.sql` พร้อม `json_export_jobs` สำหรับ profile/revision/status/path/count/bytes/hash/lease/recovery/audit และ `json_export_items` สำหรับ row/output identity กับ AES-256-GCM ciphertext metadata; unique/composite foreign keys รองรับ idempotent upsert, หลาย outputs และ artifact revision ใหม่, finalization index เรียง `rowNumber/outputOrdinal/outputKey/id`, cleanup indexes รองรับ staged payload 30 วัน/final artifact 30 วัน/audit 365 วันและ legal hold ที่บังคับ reason/owner/until; เพิ่ม unique outbox topic `mapping.json-export-finalize`, message contract และ topic provisioning โดยไม่เริ่ม finalizer; PostgreSQL tests ยืนยัน no rewrite, upsert/revision/order/index/purge/encryption/legal-hold/outbox deduplication และ full suite ผ่าน 369/369 โดยไม่ skip

- [x] **25.7 Implement `JsonFileDestinationAdapter` สำหรับ staging ต่อ row**
  - ทำ: compile JsonFile settings/profile/snapshot; ใน transaction เดียวกับ Row Job ให้ upsert canonical mapped outputs, ลบ outputs เดิมที่หายเมื่อ reprocess, เก็บ `rowNumber/outputOrdinal/outputKey` และประเมิน race-safe ว่า latest Row Jobs ครบ terminal แล้วหรือยัง; สร้าง export job/outbox เพียงครั้งเดียวเมื่อพร้อม
  - เข้าใจ: Adapter ยังไม่ publish file แต่ทำให้ row completion กับ staged results atomic; filtered/zero-output เป็นผลสำเร็จที่ยังมีส่วนต่อการตัดสินใจ finalize
  - ตรวจ: crash ก่อน/หลัง commit ไม่เกิด completed row ที่ขาด stage หรือ stage orphan; parallel last rows สร้าง finalization jobเดียว; source row `2 → 1 → 0` reconcile ถูก; JsonFile pathไม่มี Normalized write, API Delivery Job หรือ outbound call
  - ผล: `JsonFileDestinationAdapter` compile destination/schema/bindings และ stage canonical JSON ที่เข้ารหัส AES-256-GCM พร้อม hash/row/output identity ใน transaction เดียวกับ Row Job; upsert คง ciphertext เดิมเมื่อ payload ไม่เปลี่ยนและลบ stale outputs สำหรับ reprocess/filtered row; `JsonExportReadiness` lock ระดับ File Job ตรวจ latest Row Jobs กับ config snapshot และสร้าง export job/outbox แบบ once-only ทั้งจาก row completion และจุดเปลี่ยน import เป็น `Imported` จึงครอบ race ที่ rows เสร็จก่อน import; ไม่มี normalized write, API Delivery Job หรือ outbound call และ PostgreSQL tests ยืนยัน rollback atomicity, parallel last rows, `2 → 1 → 0`, delayed import readiness พร้อม full suite ผ่าน 373/373 โดยไม่ skip

- [x] **25.8 Implement streaming `JsonFileFinalizer` และ atomic publish**
  - ทำ: claim export jobด้วย lease; ตรวจ latest Row Job summary; stream `[` + itemsตาม `rowNumber/outputOrdinal` + `]` เป็น UTF-8 no BOM ไป temp file, คำนวณ SHA-256/byte/object countระหว่างเขียน, flushตาม durability policy และ atomic renameไป final path; mark Publishedพร้อม metadata
  - เข้าใจ: finalizer เป็น Module ระดับ File Job แยกจาก row adapter; memory usageต้องขึ้นกับ buffer ไม่ใช่จำนวน outputs และ final pathต้องไม่ visibleก่อนเอกสารครบ
  - ตรวจ: `[]`, one/many outputs, Thai Unicode, null/date/decimal/boolean, compact/indentedและไฟล์ใหญ่ผ่าน byte/hash tests; orderingคงที่เมื่อ Row Jobsจบสลับลำดับ; watcherเห็นเฉพาะ final fileที่ valid JSON; memory/performanceอยู่ใน limit 25.0
  - ผล: เพิ่ม `JsonFileFinalizer` ที่ lock/claim งานด้วย lease และ recheck latest Row Jobs ก่อนอ่าน staged ciphertext แบบ sequential ตาม `rowNumber/outputOrdinal/outputKey/id`; decrypt/ตรวจ payload hash ทีละ object แล้ว stream JSON array ผ่าน UTF-8 no BOM writer โดย pin compact/indented และ LF ให้ byte deterministicทุก OS พร้อมคำนวณ SHA-256/byte/object countระหว่างเขียนและบังคับ 1 MiB/object, 320,000 objects, 256 MiB/artifact; `FileSystemJsonExportPublisher` จำกัด pathใต้ server-owned Output Root, reject reparse escape, ใช้ WriteThrough + durable flush และ create-no-overwrite atomic moveก่อน mark `Published`/retention metadata; เพิ่ม Kafka finalization consumer กับ Worker-owned file profile catalog; PostgreSQL testsยืนยัน `[]`, ordered one/many, Thai/null/date/decimal/boolean, exact compact/indented bytes/hash, large streaming artifact, active lease/parallel claim, latest-row recheck และ final pathไม่ visibleก่อน valid document โดย full suiteผ่าน 380/380 ไม่มี skip

- [x] **25.9 ทำ failure, retry, reprocess และ crash-recovery state machine**
  - ทำ: invalid/failed latest row mark export Blockedและไม่ publish; retry stale lease; reconcileกรณี crashก่อน rename, หลัง renameก่อน DB update และ final path collisionด้วย expected hash; กำหนด reprocess filename/revisionจาก 25.0 และห้าม overwrite successful artifactโดยไม่มี audit link
  - เข้าใจ: retry export เดิมต้องใช้ staged canonical payload/config/profileเดิม ส่วน reprocessด้วย Config Versionใหม่เป็น artifact identityใหม่; rollbackห้ามเปลี่ยน destinationของ pinned job
  - ตรวจ: failure injectionทุก transitionไม่สร้าง partial/duplicate final file; hashตรงจึง reconcileได้แต่ hashต่างต้องหยุดพร้อม diagnostic; Invalidหนึ่ง row blockทั้งไฟล์; retry/reprocess/collision testsยืนยัน auditและ retentionครบ
  - ผล: finalizer block ทั้ง export เมื่อ latest Row Job มี `Invalid`/`Failed`, claim `RetryableFailed` และ stale `Finalizing` lease ได้อย่างปลอดภัย และมี recovery scheduler scan งานค้าง; publish ใช้ temp เดิมอย่าง atomic โดย retry ก่อน rename ไม่ทิ้ง partial file, crash หลัง rename reconcile จาก byte count + SHA-256 เมื่อ bytes ตรงกัน และ block ด้วย redacted diagnostic โดยไม่ overwrite เมื่อ final path มี bytes ต่างกัน; readiness สร้าง initial export identity แม้ทุก row invalid/ไม่มี output; reprocess allocate artifact revision แบบ monotonic ก่อนสร้าง Row Jobs, pin config/profile/path ใหม่, เชื่อม `previous_export_job_id` และคง retention/audit ของ artifact เดิม; เพิ่ม shared path resolver และ failure/recovery/reprocess integration tests โดย full suite ผ่าน 390/390 ไม่มี skip

- [x] **25.10 ขยาย validate/preview/status API โดยไม่มี side effect**
  - ทำ: เพิ่ม JsonFile capabilities/profile summaryใน create/get/clone/compare; preview คืน ordered final JSON representation, object count, estimated bytesและ diagnosticsโดยไม่ stage/write; ขยาย File Job responseแบบ additiveด้วย export status/path-safe display/object count/bytes/hash/published-at/block reason
  - เข้าใจ: preview ใช้ compiler/assembler/serializerเดียวกับ runtimeแต่ใช้ in-memory adapter; transformation completion, API delivery status และ file export statusเป็นคนละ lifecycle
  - ตรวจ: preview bytesเท่ากับ runtime serializerสำหรับ fixtureเดียวกัน; previewไม่เพิ่ม source/row/export/outboxและไม่แตะ filesystem; old clientsยังอ่าน responseเดิมได้; status matrix zero/valid/invalid/retry/published deterministic
  - ผล: เพิ่ม JsonFile capabilities/limits และ safe profile summary โดยไม่เปิดเผย Output Root พร้อมแนบ summary แบบ additive ใน create/get/version data สำหรับ clone/compare; เปิด validate/preview ทั้ง JSON และ multipart ให้คืน ordered final JSON, relative display path, object count, estimated UTF-8 bytes และ diagnostics ผ่าน `JsonArtifactSerializer` เดียวกับ runtime โดย in-memory preview ไม่เพิ่ม source/row/export/outbox และไม่แตะ filesystem; ขยาย File Job response ด้วย latest export lifecycle/metadata แบบ nullable และซ่อน block reason นอกสถานะ `Blocked`; compatibility golden เดิมคงรูปเพราะ field ใหม่ที่ไม่มีค่าถูก omit, status matrix และ exact byte/hash parity ผ่าน รวม web lint/build และ PostgreSQL full suite 397/397 ไม่มี skip

- [x] **25.11 Promote แนวคิด Variant C เข้าหน้า production config**
  - ทำ: สร้าง production Mapping workbenchจาก real Input Layout, Transformation, Contract Path และ preview Modules; วาง destination selectorในแผง Deliveryตาม Prototype C โดยมี `Legacy Table`, `Whole file · JSON file` และ `One record at a time · API Contract`; แสดง controlเฉพาะชนิดที่เลือกและ read-only summaryสำหรับ root/encoding/error policy; create/cloneเป็น Config Versionใหม่เสมอ
  - เข้าใจ: prototypeตอบตำแหน่งและ interactionเท่านั้น; production UIต้องใช้ validation, loading/error/accessibilityและ immutable version flowจริง โดย reuse editorเดิมแทน copy mock state/fixture
  - ตรวจ: สลับ destinationแล้ว stale fieldsของชนิดก่อนหน้าถูก clearก่อน submit; clone versionเดิมไม่เปลี่ยน destinationจนผู้ใช้เลือกและสร้าง versionใหม่; live previewเปลี่ยน array/object/tableตามชนิด; keyboard/screen reader, responsive layout, lint/buildและ Playwright E2Eผ่าน
  - ผล: เพิ่ม production `MappingWorkbench` ที่วาง Delivery selector แบบ Variant C เหนือ editor จริงและเลือกได้ `Legacy Table`, `Whole file · JSON file` หรือ `One record at a time · API Contract` โดยแสดงเฉพาะ controls ของชนิดปัจจุบันและล้าง state ของชนิดก่อนหน้า; reuse Input Layout/Transformation editor เดิมสำหรับ Legacy และ Contract Path editor เดิมสำหรับ API/JsonFile พร้อม file contract resolver และ read-only root/UTF-8/error-scope/path summary ที่ไม่เปิดเผย Output Root; เพิ่ม JSON final-array preview แบบไม่มี filesystem side effect, คง clone request จาก immutable version เดิมรวม accepted extensions และสร้างการแก้ไขเป็น Config Version ใหม่เท่านั้น; keyboard-accessible radio/tab semantics, responsive layout, lint, TypeScript, production build และ Playwright regression 6/6 ผ่าน ครอบคลุม stale-field submit, schema resolution, object/array preview และ clone destination parity

- [x] **25.12 เพิ่ม export operations, security และ observability**
  - ทำ: แสดง export status/history/pathแบบ relative/hash/count/size/เวลาและ retry actionตาม policy; เพิ่ม metrics queue age/finalize latency/bytes/objects/blocked/retry/hash conflict/cleanup failureแยก profile/configโดยไม่มี PII; เพิ่ม kill switchสำหรับ new JsonFile activationกับ finalizer dispatch และ authorizationสำหรับ downloadถ้ามีใน scope 25.0
  - เข้าใจ: file payloadอาจมี PIIเท่ากับ API payload; path, logs, metricsและoperations UIต้องไม่กลายเป็นช่องทางอ่านข้อความหรือ filesystem topology
  - ตรวจ: traversal/symlink/collision/quota/disk-full/permission/oversize testsผ่าน; logsและbrowserไม่มี absolute root, credentialหรือ payload body; pause/resumeรักษา staged dataและ identityเดิม; alert/runbookครอบ queue/disk/cleanup/hash conflict
  - ผล: ขยาย `/delivery` และ JSON export API ให้แสดงเฉพาะ relative path/hash/count/bytes/timestamps/redacted error พร้อม retry ที่ API ตรวจ authoritative state และ audit operator/reason; เพิ่ม aggregate metrics แยก config/version/profileโดยไม่ส่ง config nameหรือ payload, alert queue/publication/blocked/hash conflict/cleanup failure และ runbookตอบสนอง queue/disk/permission/cleanup/hash conflict; เพิ่ม audited kill switches สำหรับ new JsonFile activation/finalizer โดย pause ก่อน claim และ resume ด้วย staged revision/path/hashเดิม; เพิ่ม secure publisher/cleanup ที่ป้องกัน traversal/reparse point, collision, quota/minimum disk, permission failure และ redact exception จาก logs/UI; ไม่มี download endpoint/buttonตาม v1 scope; local backend suite 404/404, focused security 8/8, Playwright 2/2, formatter, lint, TypeScript และ production buildผ่าน โดย PostgreSQL-gated assertions compileผ่านแต่ไม่ได้ executeในรอบนี้เพราะ environmentไม่มี integration connection/daemon

- [x] **25.13 Full regression, E2E และ Gate E sign-off**
  - ทำ: รัน `.txt/.csv/.xlsx/.xls` → Source → nested mapping/array/`add`/`concat` → JsonFileสำหรับ zero/one/many outputs รวม parallel/retry/reprocess/failure injection; รัน LegacyTable characterizationและ ApiContract exact-byte/idempotency suites; ทดสอบ create/clone/activate/rollbackผ่าน Variant C production UI
  - เข้าใจ: acceptanceวัดสาม Destination Adapterกับ immutable Config Version ไม่ใช่ตรวจว่าไฟล์ JSON parseได้เพียงอย่างเดียว
  - ตรวจ: requirementทุกข้อ traceถึง automated test; existing config parity 100%; no partial/duplicate file; JsonFileไม่มี Normalized/API side effect; full backend tests, integration, formatter, web lint/build/E2Eและ security scanผ่าน; Gate E ownerลงนามก่อนเปิด production profile
  - ผล: `docs/phase-25-gate-e-evidence.md` trace AC-JF-E2E-01..02 ไปยัง production seams/tests; PostgreSQL-required backend 404/404, formatter, lint, TypeScript, Next.js 16.4.3 build, Playwright 9/9 แบบ workerเดียว และ npm/NuGet vulnerability scans ผ่าน; create/clone/activate JsonFile/rollback Legacy ผ่าน UI จริง; วันที่ 2026-10-26 ผู้ใช้ยืนยัน Gate E ในนาม Business/Contract, Platform/Operations และ Security โดยอนุมัติผลทดสอบกับ rollback plan; production activation และ finalizer kill switch ยังคง fail-closed และไม่ได้ถูกเปิดจากการ sign-off นี้

- [x] **25.14 Documentation, prototype capture และ commit handoff**
  - ทำ: อัปเดต CONTEXT, README, current workflow, OpenAPI/examples, setup/reset scripts, retention/cleanup/backup/restore/rollback runbooksและ acceptance matrix; บันทึกว่า Variant Cชนะและเหตุผล; เก็บ prototypeเป็น primary-source pointerตาม prototype policyแล้วให้ production codeไม่ dependบน `/prototype/file-to-json`
  - เข้าใจ: prototypeเป็นหลักฐานการตัดสินใจ ไม่ใช่ production dependency; operatorและผู้พัฒนาคนใหม่ต้องแยก JSON file publish, API deliveryและ Legacy reconcileออกจากกันได้จากเอกสาร
  - ตรวจ: คนใหม่สร้าง JsonFile version, preview, process sample,ตรวจ hash,จำลอง blocked exportและ rollbackตาม runbookได้; links/commandsไม่เสีย; ไม่มี secret/sample PII; สรุป changed files/migrations/tests/known limitsครบและ AI agentไม่สร้าง commitเอง
  - ผล: อัปเดต README, CONTEXT, current workflow และ API `.http` examples ให้แยก `LegacyTable`/`ApiContract`/`JsonFile` กับ lifecycle/control ชัดเจน; เพิ่ม `phase-25-handoff.md` walkthrough ด้วยข้อมูลสังเคราะห์ตั้งแต่ create/preview/publish/hash/blocked/rollback, `phase-25-acceptance-matrix.md` และขยาย operations runbookด้วย retention/cleanup/backup/restore/rollback; เก็บ Variant C decision recordไว้ที่ prototypeโดยชี้ production workbenchเป็น implementationจริงและสแกน production sourceแล้วไม่พบ prototype dependency; resetล้าง/ignore outputและ ignore backup, rollout scriptรองรับ JSON activation/finalizer switchesแบบ explicit; PowerShell parse, local Markdown links และ `git diff --check` ผ่าน ไม่มี secret/sample PIIใหม่และไม่ได้สร้าง commit

### Definition of Done ของ Phase 25

- Config Version เดิมทุกชนิดให้ผลเท่า baselineและไม่มี data rewrite
- Config Version ใหม่เลือก `LegacyTable`, `ApiContract` หรือ `JsonFile` ได้หนึ่งชนิดแบบ immutable
- `add` และ `concat` ผ่าน typed AST, compiler, evaluator, capabilities, production editorและ preview/runtime parity โดย configเดิมไม่เปลี่ยนผล
- `JsonFile` reuse mapping/contract Modules เดิม, ไม่เขียน Normalized Tableและไม่สร้าง API Delivery Job
- หนึ่ง File Job publish JSON array deterministicหนึ่งไฟล์หรือไม่ publishเลยเมื่อมี invalid/failed row
- retry/reprocess/crash recoveryไม่สร้าง partial, duplicateหรือ overwrite artifactโดยไม่มี audit
- Production UI ใช้แนวคิด Variant Cกับ real API/state; prototypeไม่เป็น runtime dependency
- Gate E, regression, security, observability, rollback rehearsalและ documentationผ่านก่อน activate `JsonFile` production profile

---

## Phase 26 — Admin สำหรับ API Destination Profiles

### เป้าหมายและขอบเขต

เพิ่มหน้า `/admin/api-destination-profiles` เพื่อให้ผู้มีสิทธิ์สร้าง ตรวจสอบ activate, disable และดูประวัติ API destination profile เมื่อมี partner ใหม่ โดยไม่ต้องแก้ `appsettings.Development.json` ซ้ำใน API/Worker หรือ restart runtime ทุกครั้ง Phase นี้ไม่สร้าง physical table แยกต่อ partner แต่ใช้ versioned catalog กลางและเพิ่ม row/profile version ใหม่แทน

> **Design addendum:** [API Destination Profile Administration Design](../spec/spec-design-api-destination-profile-administration.md) เป็น authoritative source ของ identity, lifecycle, authorization, profile-version pinning, cache, cutover และ rollback ใน Phase 26; ต้องบันทึก owner approval ของ Step 26.0 ก่อนเริ่ม implementation

กติกาหลัก:

- API destination profile เป็น server-owned configuration; หน้า Admin ห้ามรับหรือแสดง credential จริง และเก็บได้เฉพาะ logical `AuthSecretReference`
- Profile Version ที่ activate แล้วเป็น immutable; การแก้ endpoint, method, auth, timeout, contracts, headers หรือ payload bindings ต้องสร้าง draft version ใหม่
- Mapping Config Version และ Delivery Job ต้อง pin profile version/contract snapshot เดิม; disable profile ห้ามงานใหม่ แต่ retry งานเก่าต้องยัง resolve snapshot ที่ถูก pin ได้
- API และ Worker ต้องอ่าน Postgres catalog เดียวกัน; application config ใช้ได้เฉพาะ bootstrap/local fallback ที่ระบุชัดและห้ามเกิดสอง authoritative sources
- Endpoint validation ต้องคง HTTPS-only, non-loopback/non-IP host, safe path template, method allowlist และ SSRF/DNS policy ตาม design ที่อนุมัติ
- Admin mutation ทุกครั้งต้องมี authorization, operator, reason, optimistic concurrency และ append-only audit; response/log/browser ห้ามเปิด credential, secret value หรือข้อมูล partner payload
- Phase 26 v1 ครอบเฉพาะ `ApiDestinationProfiles`; `FileDestinationProfiles` ยังเป็น server-managed config จนกว่าจะมี design และ authorization model แยก

- [x] **26.0 ล็อก Profile Administration design และ Gate F contract**
  - ทำ: เพิ่ม design addendum ระบุ lifecycle `Draft → Active → Disabled`, ownership, schema, authorization roles, endpoint/secret policy, version pinning, cache consistency, migration/cutover, rollback และ acceptance IDs; ตัดสินว่า profile ID เปลี่ยนชื่อได้หรือไม่และ version identity ใช้ค่าใด
  - เข้าใจ: หน้า Admin เป็น control plane ที่เปลี่ยนปลายทางส่งข้อมูลจริง จึงต้องออกแบบ fail-closed และแยกจาก Mapping Config workbench ซึ่งเป็น consumer ของ safe catalog
  - ตรวจ: Contract, Platform/Operations และ Security owner ยืนยัน schema/lifecycle/permissions; unresolved decision ไม่มี default แบบเปิดกว้าง; trace requirement ไป Phase 26 steps ครบ
  - ผล: เพิ่ม `spec/spec-design-api-destination-profile-administration.md` กำหนด `profileKey` immutable, `versionNo` แบบ monotonic, Draft/Active/Disabled และ audited rollback, PostgreSQL aggregate/API/safe projection, authorization roles, HTTPS/SSRF/DNS/secret policy, exact `profileVersionId` pinning, cache fail-closed, Configuration→Compare→Postgres cutover, rollback และ AC trace 26.1–26.6 ครบ; design ไม่มี unresolved default และวันที่ 2026-10-29 ผู้ใช้อนุมัติในนาม Contract/Business, Platform/Operations และ Security จึง Accepted สำหรับ Phase 26 implementation

- [x] **26.1 เพิ่ม versioned Postgres profile catalog และ migration**
  - ทำ: เพิ่มตารางกลางอย่างน้อย `api_destination_profiles`, `api_destination_profile_versions`, `api_destination_profile_contracts`, `api_destination_profile_bindings` และ append-only `api_destination_profile_audit`; ใช้ row ต่อ partner/profile version ไม่สร้าง table ใหม่ต่อ partner
  - เข้าใจ: identity/lifecycle อยู่ที่ profile ส่วน endpoint/auth/contracts/bindings อยู่ใน immutable version; credential จริงอยู่นอก DB และ DB เก็บเพียง secret reference
  - ตรวจ: PK/FK/unique/check constraints, concurrent draft creation, single-active-version policy, disabled behavior และ rollback migration ผ่าน PostgreSQL integration tests; migration ไม่ rewrite Mapping Config/File Job เดิม
  - ผล: เพิ่ม migration `0014_api_destination_profile_catalog.sql` พร้อม versioned profile aggregate, nullable pinning FK สำหรับ Mapping Config Version/Delivery Job, partial unique indexes สำหรับ Draft/Active, deferred active-pointer consistency, immutable profile/version/children guards และ append-only audit guard; ไม่มี seed/backfill หรือ data rewrite ใน Step นี้ โดย PostgreSQL integration tests ยืนยัน legacy row `xmin` ไม่เปลี่ยน, old writer ยังใช้ expanded schema ได้, lifecycle/disable/rollback/immutability/audit constraints ทำงาน และ concurrent Draft มีผู้ชนะหนึ่งรายการ ครบ 4/4

- [x] **26.2 ทำ Profile Catalog Module และ Postgres Adapter**
  - ทำ: วาง seam เล็กสำหรับ list safe profiles, resolve active version และ resolve pinned version; เพิ่ม Postgres adapter ให้ API/Worker ใช้ร่วมกันและคง configuration adapter เฉพาะ bootstrap/local test; กำหนด cache TTL/invalidation และ unavailable behavior แบบ fail-closed
  - เข้าใจ: caller ไม่ควรรู้ schema/cache/source ของ profile; Module เดียวต้องซ่อน validation, version resolution และ safe projection เพื่อให้ behavior ของ API/Worker ไม่ drift
  - ตรวจ: contract tests ชุดเดียวผ่านทั้ง Postgres และ configuration adapters; API/Worker เห็น version เดียวกันหลัง activate; DB/cache outage ไม่ fallback ไป profile คนละรุ่นและไม่ส่ง payloadผิด endpoint
  - ผล: เพิ่ม `ApiDestinationProfileCatalogModule` ครอบ source adapter ด้วย safe-list cache 15 วินาที, exact pinned-version cache 5 นาทีและ local invalidation โดย `ResolveActive` ไม่ใช้ cache และทุก cache miss/expired outage คืน `ProfileCatalogUnavailable` แบบ fail-closed; Postgres/configuration adapters ใช้ contract/validation เดียวกัน, ปฏิเสธ Draft หรือ substituted pinned version และ API/Worker เลือก source ผ่าน explicit `Configuration|Postgres` mode โดย default ยังเป็น `Configuration` จน Step 26.5; targeted contract/cache/PostgreSQL tests ผ่าน 18/18 รวม two-adapter parity, activation visibility, historical disabled pin และ outage/no-fallback ส่วน full backend regression ผ่าน 425/425

- [x] **26.3 เพิ่ม Admin API พร้อม authorization และ audit**
  - ทำ: เพิ่ม endpoint สำหรับ list/get/version history, create draft, validate, activate และ disable; ใช้ allowlisted typed request, concurrency token, operator/reason และ authorization policy แยก read/write/activate; ห้าม endpoint delete profile/version ที่เคยถูกอ้างอิง
  - เข้าใจ: validate ไม่มี side effect, activate เป็น audited state transition และ disable ไม่ลบ historical resolution; secret provider ตรวจได้เฉพาะ existence/shape โดยไม่คืน secret value
  - ตรวจ: unauthorized/forbidden, invalid URL/template/header/auth, stale update, duplicate ID/version, missing secret reference, activate/disable race และ audit completeness tests ผ่าน; OpenAPI ไม่เปิด field ต้องห้าม
  - ผล: เพิ่ม Admin API list/get/permissions, create/update draft, validate, activate/rollback และ disable พร้อม policy projection, claim-derived actor, production activation policy, `If-Match`, safe endpoint redaction, append-only audit และ cache invalidation; secret validator อ่านเฉพาะ environment-backed logical reference เพื่อตรวจ existence/shape และไม่คืนค่า secret. PostgreSQL integration tests กลุ่ม `ApiDestinationProfile` ผ่าน 27/27 รวม stale token, missing secret แบบไม่มี side effect, audit completeness และ activate/disable race

- [x] **26.4 สร้างหน้า `/admin/api-destination-profiles`**
  - ทำ: เพิ่ม list/search/filter ตาม status/environment, create/edit draft form, supported contracts, allowed headers, payload bindings, safe endpoint preview, validation diagnostics, version diff/history และ explicit activate/disable confirmation; Mapping Config selector ใช้เฉพาะ Active profiles
  - เข้าใจ: หน้า Admin จัดการ profile catalog ไม่แก้ Mapping Config Version; endpoint host แสดงได้เฉพาะ role ที่ design อนุญาต ส่วน credential แสดงเพียง logical reference/redacted status
  - ตรวจ: keyboard/screen reader, responsive layout, loading/error/empty state, unsaved-change guard, stale concurrency, authorization visibility และ Playwright create→validate→activate→select flow ผ่าน; browser state/log ไม่มี secret value
  - ผล: หน้า Admin รองรับ search/filter, typed create/edit draft, contracts/headers/bindings, safe endpoint preview, diagnostics, version compare/history, policy-based action visibility, confirmations, unsaved guard และ stale-edit recovery; Playwright targeted ผ่าน 2/2, ESLint/TypeScript และ Next.js production build ผ่าน โดยไม่มี credential field/value ใน browser flow

- [x] **26.5 Migrate profile เดิมและ cut over API/Worker**
  - ทำ: seed `pa-mtls`, `pa-mtls-inform` และ `claims-envelope` จากค่าที่อนุมัติแบบ idempotent, เปรียบเทียบ canonical profile snapshots ก่อน cutover, สลับ API/Worker ไป Postgres catalog ด้วย rollout flag และเอา duplicated runtime definitions ออกจาก application config หลังพิสูจน์ parity
  - เข้าใจ: seed เป็น bootstrap ไม่ใช่ source of truth ถาวร; cutover ต้องรักษา profile ID, contract reference, payload binding, auth scheme และ pinned delivery behavior เดิมทุก byte
  - ตรวจ: pre/post-cutover parity, restart, rolling mixed-version deployment, cache refresh, retry งานเดิมและ rollback rehearsal ผ่าน; config drift detector ยืนยันเหลือ authoritative source เดียว
  - ผล: เพิ่ม migration `0015` seed `pa-mtls`, `pa-mtls-inform` และ `claims-envelope` แบบ idempotent พร้อม canonical content hash/audit และ backfill exact Profile Version ให้ Mapping Config Version/Delivery Job โดย block key ที่ missing, ambiguous หรือ pin ไม่ตรง; เพิ่ม `Compare` mode ที่เทียบ canonical snapshot, freeze mutation และเลือก authoritative side แบบ explicit เพื่อรองรับ mixed deployment/rollback ก่อน DB-only use, เปลี่ยน default API/Worker เป็น `Postgres`, ตรวจ catalog ตอน startup แบบ fail-closed และลบ duplicated profile definitions จาก application config. PostgreSQL cutover/idempotence/hash tests, catalog/Compare/rollback tests, Admin hash/mutation-freeze regression และเดิม migration/catalog contract ผ่าน 27/27; API และ Worker restart จาก Postgres สำเร็จ. Full regression เป็นงาน Step 26.6 และรอบ diagnostic ปัจจุบันผ่าน 431/439 โดย 8 failure นอก cutover path แยกบันทึกใน [Phase 26.5 handoff](phase-26.5-cutover-handoff.md)

- [x] **26.6 Full regression, security review, operations และ Gate F sign-off**
  - ทำ: รัน backend/PostgreSQL/web E2E ครบ, เพิ่ม metrics/alerts สำหรับ profile validation/activation/cache failures, เขียน runbook onboarding partner, secret rotation, disable/rollback และ incident response; บันทึก evidence/acceptance matrix
  - เข้าใจ: profile activation อาจเปลี่ยนปลายทางข้อมูลจริงแม้ Mapping Config ไม่เปลี่ยน จึงต้องมี operational gate, audit และ rollback ที่ทดสอบได้
  - ตรวจ: LegacyTable/ApiContract/JsonFile regression ผ่าน, ไม่มี cross-partner routing, secret/PII leak หรือ SSRF bypass, rollback คืน active version เดิมโดยไม่แก้ pinned jobs และ Contract/Platform/Security owner ลงนาม Gate F ก่อนเปิด production Admin
  - ผล: บังคับ PostgreSQL full backend regression ผ่าน 471/471, format/lint/TypeScript/Next production build ผ่าน, Playwright full E2E ผ่าน 16/16 แบบ deterministic one worker และ npm/NuGet scan พบ 0 vulnerabilities; เพิ่ม HTTPS:443/DNS rebinding/private-range/redirect/header/template controls, bounded list `limit/offset` 1–100 แบบ fixed-query, active resolve one roundtrip พร้อม Gate threshold 32 concurrent/p95 ≤250 ms, safe metrics/alerts และ [operations runbook](phase-26-api-destination-profile-operations-runbook.md). [Acceptance matrix](phase-26-acceptance-matrix.md) และ [Gate F evidence](phase-26-gate-f-evidence.md) ครบ; วันที่ 2026-11-02 ผู้ใช้อนุมัติในนาม Contract/Business, Platform/Operations และ Security จึงปิด Gate F และอนุญาต controlled Shadow→Canary→Expanded rollout โดยไม่เปิด Production profile/runtime controls อัตโนมัติ

### Definition of Done ของ Phase 26

- ผู้มีสิทธิ์ onboard partner ใหม่ผ่านหน้า Admin โดยไม่แก้ application config หรือสร้าง table ใหม่ต่อ partner
- Profile Version ที่ activate แล้ว immutable และทุก mutation มี operator/reason/audit/concurrency control
- API และ Worker resolve จาก Postgres catalog เดียวกัน; ไม่มี duplicated authoritative profile definitions
- Mapping Config Version/Delivery Job เดิมยัง resolve pinned snapshot และ retry ได้หลัง profile version ใหม่หรือ disable
- Credential ไม่เข้า DB/API/browser/log; มีเพียง logical secret reference และ redacted validation status
- Admin E2E, PostgreSQL integration, security/SSRF, mixed-deployment, rollback และ full destination regression ผ่าน
- Gate F owner sign-off เสร็จก่อนเปิด profile administration ใน production

---

## Phase 27 — Admin สำหรับ Versioned Contract Registry

### เป้าหมายและขอบเขต

เพิ่มหน้า `/admin/contracts` เพื่อให้ผู้มีสิทธิ์สร้าง ตรวจสอบ clone, publish, retire และดูประวัติ Contract schema โดยไม่ต้องแก้ `ContractFixtures`, `BundledContractSchemaFetcher` หรือ restart runtime ทุกครั้ง Contract Registry เป็น catalog กลางที่ทั้ง `ApiContract` และ `JsonFile` ใช้ร่วมกัน ส่วน Mapping Config Workbench เป็น consumer ที่เลือกได้เฉพาะ Contract Version ที่ Publish แล้ว

Lifecycle ที่ต้องรองรับ:

```text
Draft → Validate → Published → Retired
             └──── Clone published version → new Draft
```

กติกาหลัก:

- Contract identity แยก `contractId`, `version` และ immutable `reference` เช่น `proposal`, `2`, `proposal/2`; ห้ามเปลี่ยนชื่อหรือแก้ schema ของ Version ที่ Publish แล้ว
- การเปลี่ยน `proposal/1` ต้องใช้ `Clone as new version` เพื่อสร้าง `proposal/2`; Mapping Config Version และ Delivery Job เดิมยังใช้ schema snapshot/hash ที่ pin ไว้
- Draft แก้ได้ด้วย optimistic concurrency; Publish ต้อง canonicalize JSON Schema, คำนวณ SHA-256 ฝั่ง server, validate supported subset และบันทึก operator/reason/audit ใน transaction เดียวกัน
- Retire ป้องกันการเลือกสร้าง Mapping Version ใหม่ แต่ห้ามลบ Contract Version หรือทำให้ historical snapshot/pinned delivery resolve ไม่ได้
- Browser ห้ามส่ง schema hash หรือ path catalog เป็น authoritative input; server ต้อง derive canonical schema, hash, typed paths, required/nullability/constraints และ compatibility diagnostics เอง
- v1 รองรับ JSON Schema subset เดิมเท่านั้น: local `$ref`, nested object, scalar, fixed index/wildcard array และ constraints ที่ compiler รองรับ; external/recursive `$ref`, ambiguous `oneOf`/`anyOf`, executable code และ remote URL ต้อง fail closed
- Schema, description และ examples ต้องผ่าน size/content policy และห้ามมี credential, secret หรือข้อมูลลูกค้าจริง; Admin preview ใช้ synthetic sample เท่านั้น
- Contract publish/retire เป็น control-plane mutation ต้องมี authorization แยก read/write/publish, explicit confirmation, operator, reason, concurrency token และ append-only audit

- [x] **27.0 ล็อก Contract Registry design และ Gate G contract**
  - ทำ: เพิ่ม design addendum ระบุ lifecycle/identity, immutable publish, clone semantics, ownership/roles, supported JSON Schema subset, canonicalization/hash, compatibility policy, profile linkage, cache consistency, migration/cutover, rollback และ acceptance IDs
  - เข้าใจ: Contract Version คือข้อตกลงข้อมูลที่ Mapping Config และ delivery pin อยู่ การแก้ published schema in place ทำให้ payload เดิมเปลี่ยนความหมายและทำลาย replay/audit
  - ตรวจ: Contract, Platform/Operations และ Security owner ยืนยัน lifecycle/permissions/compatibility; ตัดสิน policy ของ breaking/non-breaking change และ version naming โดยไม่มี default แบบเปิดกว้าง
  - ผล: เพิ่ม [Versioned Contract Registry design](../spec/spec-design-versioned-contract-registry.md) ล็อก identity `contractKey/version`, Draft→Published→Retired, published immutability, breaking-change acknowledgement, exact historical resolution, authorization/audit และ fail-closed cache; คำสั่งให้ทำถึง 27.2 อนุมัติ design เพื่อ implementation แต่ไม่ใช่ Gate G production sign-off

- [x] **27.1 เพิ่ม versioned Postgres Contract catalog และ migration**
  - ทำ: เพิ่มตารางอย่างน้อย `contracts`, `contract_versions` และ append-only `contract_audit`; เก็บ draft schema, canonical published schema, SHA-256, lifecycle, concurrency token, timestamps/operator/reason และเชื่อม profile-contract allowlist ด้วย FK/reference ที่ตรวจได้
  - เข้าใจ: Draft และ Published ใช้ identity เดียวกันแต่ Published payload เป็น immutable artifact; schema hash เป็นผลลัพธ์ของ server canonicalization ไม่ใช่ค่าที่ Admin ป้อน
  - ตรวจ: PK/FK/unique/check constraints, duplicate version, concurrent edit/publish, immutable published row, retire behavior และ rollback migration ผ่าน PostgreSQL integration tests; migration ไม่ rewrite snapshots เดิม
  - ผล: เพิ่ม migration `0016_versioned_contract_registry.sql` พร้อม DB constraints/triggers สำหรับ lifecycle, immutable identity/content, one Draft, append-only audit และ exact nullable FK จาก profile allowlist; PostgreSQL migration tests ผ่าน 4/4 โดยไม่ rewrite Mapping snapshot เดิม

- [x] **27.2 ทำ Contract Registry Module และ Postgres Adapter**
  - ทำ: วาง seam สำหรับ list published contracts, get/version history, create/update draft, validate, clone, publish, retire และ resolve exact published reference; reuse compiler/path catalog/canonical hash เดิมและเพิ่ม cache invalidation แบบ fail-closed
  - เข้าใจ: Mapping/Delivery caller ไม่ควรรู้ว่า schema มาจาก fixture หรือ DB; Module เดียวต้องรักษา resolve result และ diagnostics ให้เท่ากันทั้ง Admin preview, Create Version และ runtime snapshot verification
  - ตรวจ: contract tests ชุดเดียวผ่าน bundled fixture และ Postgres adapters; API/Worker เห็น published version/hash เดียวกัน; DB/cache outage ไม่ fallback ไป schema คนละ version
  - ผล: เพิ่ม shared `VersionedContractRegistryModule`, compatibility analyzer, read-only bundled adapter และ PostgreSQL adapter; exact/list cache มี bounded TTL, mutation invalidation และ outage fail closed โดยไม่ fallback. Contract/adapter tests รวมกับ migration tests ผ่าน 10/10 ครอบคลุม bundled/Postgres parity, API/Worker-equivalent resolution, compatibility, concurrency, audit, cache invalidation และ outage; forced-PostgreSQL backend regression ผ่าน 481/481, format ผ่าน และ API startup ใช้ migration `0016` ได้โดยยังไม่สลับ runtime source

- [x] **27.3 เพิ่ม Contract Admin API พร้อม authorization และ audit**
  - ทำ: เพิ่ม endpoint สำหรับ list/get/history, create/update draft, validate, clone published version, publish และ retire; ใช้ typed request, request-size limit, concurrency token, operator/reason และ policy แยก read/write/publish; ไม่มี hard delete สำหรับ Contract ที่ Publish หรือถูกอ้างอิงแล้ว
  - เข้าใจ: Validate ไม่มี side effect; Publish และ Retire เป็น audited state transition; response คืน safe schema/path/diagnostics/hash แต่ไม่คืน server filesystem path หรือข้อมูล configuration ภายใน
  - ตรวจ: unauthorized/forbidden, malformed/oversized schema, unsupported keyword/reference, stale update, duplicate reference, publish race, retire-in-use และ audit completeness tests ผ่าน; OpenAPI ไม่เปิด field ต้องห้าม
  - ผล: เพิ่ม typed `/admin/contracts` list/get/history/create/update/validate/clone/publish/retire, request limit, `If-Match`, safe diagnostics, policy `ContractAdmin.Read|WriteDraft|Publish`, production publish flag, credential/private-key content policy และ retire-in-use guard; controller/OpenAPI/PostgreSQL tests ผ่าน

- [x] **27.4 สร้างหน้า `/admin/contracts`**
  - ทำ: เพิ่ม list/search/filter ตาม lifecycle, create draft, JSON editor/file upload, schema diagnostics, derived Contract paths, required/type/constraint summary, version diff/history, compatibility warning และ explicit Validate/Clone/Publish/Retire actions; profile editor เลือกได้เฉพาะ Published versions
  - เข้าใจ: Admin แก้ได้เฉพาะ Draft; หน้า Published แสดง read-only พร้อม `Clone as new version` เช่น `proposal/1 → proposal/2`; Mapping Workbench ไม่ใช่ schema editor
  - ตรวจ: keyboard/screen reader, responsive layout, malformed JSON feedback, unsaved-change guard, stale concurrency, authorization visibility และ Playwright create draft→validate→publish→attach profile→select in Mapping Workbench flow ผ่าน
  - ผล: หน้า Contract Admin รองรับ search/lifecycle filter, JSON editor/upload, live malformed feedback, derived path/type/required/nullability/constraints, compatibility acknowledgement, history/diff, clone/publish/retire, unsaved/stale guard และ permission visibility; Profile Admin เลือกได้เฉพาะ Published Contract. Targeted และ full Playwright ผ่าน

- [x] **27.5 Migrate bundled contracts และ cut over resolver**
  - ทำ: seed `proposal/1` จาก `pa-proposal-v1.json` และ `claims/2` จาก `claims-envelope-v2.json` แบบ idempotent; เปรียบเทียบ canonical schema/hash/path catalog ก่อน cutover; สลับ resolver ไป Postgres ด้วย rollout flag แล้วลด `ContractFixtures`/`BundledContractSchemaFetcher` เป็น bootstrap/local-test fallback ที่ไม่เป็น authoritative source
  - เข้าใจ: migration ต้องรักษา reference, canonical bytes/hash, payload bindings และ snapshot behavior เดิม; fixture fallback ห้ามซ่อน DB outage หรือสร้าง split-brain schema
  - ตรวจ: pre/post-cutover parity, restart, rolling mixed-version deployment, cache refresh, historical Mapping Version/retry และ rollback rehearsal ผ่าน; drift detector ยืนยัน production มี authoritative source เดียว
  - ผล: เพิ่ม migration `0017`, explicit `Bundled|Postgres` source flag, idempotent startup bootstrap/parity verifier และ registry-backed schema fetcher; `proposal/1`/`claims/2` ตรงกันทั้ง canonical bytes/hash/path catalog, profile FK backfill/auto-link exact Published Version, API startup Postgres mode ผ่าน และ outage ไม่ fallback

- [x] **27.6 Full regression, security review, operations และ Gate G sign-off**
  - ทำ: รัน backend/PostgreSQL/web E2E ครบ, เพิ่ม metrics/alerts สำหรับ validate/publish/resolve/cache failure, เขียน runbook create/clone/publish/retire/rollback และบันทึก acceptance matrix/evidence
  - เข้าใจ: การ Publish Contract อาจเปิดให้สร้าง payload รูปแบบใหม่ แม้ endpoint/profile ไม่เปลี่ยน จึงต้องมี approval, observability และ rollback ที่ไม่แก้ published artifact เดิม
  - ตรวจ: LegacyTable/ApiContract/JsonFile regression ผ่าน, ไม่มี schema substitution, hash drift, secret/PII leak หรือ unauthorized publish; rollback ปิด version ใหม่และคืน allowlist เดิมโดยไม่กระทบ pinned jobs; Contract/Platform/Security owner ลงนาม Gate G
  - ผล: technical gate ผ่าน—forced-PostgreSQL backend 494/494, format/lint/TypeScript/Next build, Playwright 18/18, npm production และ NuGet vulnerabilities 0; metrics/alerts, [runbook](phase-27-contract-registry-operations-runbook.md), [acceptance matrix](phase-27-acceptance-matrix.md) และ [Gate G evidence](phase-27-gate-g-evidence.md) ครบ. npm all-dependency scan มี 16 moderate ใน dev-only ESLint/minimatch chain และยังไม่มี compatible upstream fix; วันที่ 2026-11-03 ผู้ใช้ยืนยันในนาม Contract/Business, Platform/Operations และ Security พร้อมยอมรับข้อยกเว้นนี้ จึงปิด Gate G และอนุญาต controlled publishing rollout โดย production `PublishingEnabled` ยังคงเป็น `false` จนกว่าจะมี rollout change แยกต่างหาก

### Definition of Done ของ Phase 27

- ผู้มีสิทธิ์สร้างและ Publish Contract Version ใหม่ผ่าน Admin โดยไม่แก้ application config, fixture mapping หรือ restart runtime
- Published Contract Version immutable; การเปลี่ยน schema ใช้ Clone เป็น version ใหม่ และทุก mutation มี operator/reason/audit/concurrency control
- Mapping Workbench และ profile editor เห็นเฉพาะ Published/non-retired Contract Version ที่ได้รับอนุญาต ส่วน historical snapshot/retry ยังทำงานหลัง Retire
- API และ Worker resolve exact reference จาก Postgres Contract Registry เดียวกัน; ไม่มี duplicated authoritative schema definitions
- Canonical schema/hash/path catalog มาจาก server และผ่าน supported-subset/size/security validation; browser ไม่เป็น authoritative source
- Bundled `proposal/1` และ `claims/2` migrate ด้วย canonical parity และมี rollback/cutover evidence
- Admin E2E, PostgreSQL integration, compatibility/security, mixed-deployment, rollback และ full destination regression ผ่าน
- Gate G owner sign-off เสร็จก่อนเปิด Contract publishing ผ่าน Admin ใน production

---

## Phase 28 — Admin สำหรับ Table Catalog และ Schema Verification

### เป้าหมายและขอบเขต

เพิ่มหน้า `/admin/tables` เพื่อให้ผู้มีสิทธิ์สร้าง Source/Normalized table, ดูตารางที่สร้างแล้ว, เพิ่มคอลัมน์แบบ additive และตรวจว่า metadata ตรงกับ physical PostgreSQL schema โดยไม่ต้องใช้ `requests/tables.http` หรือเปิด database client หลังสร้างสำเร็จ UI ต้อง redirect ไปหน้ารายละเอียดของ table ใหม่และแสดงผล verification ล่าสุดทันที

สถานะ verification ที่ต้องรองรับ:

```text
Unknown → Verifying → Healthy
                    ├→ Drifted
                    ├→ Missing
                    └→ VerificationFailed
```

กติกาหลัก:

- `table_definitions` และ `table_columns` เป็น authoritative metadata; browser ส่งได้เฉพาะ typed table/column definition และ table ID ห้ามส่ง raw SQL, schema name หรือ connection information
- Server ตรวจ physical schema ผ่าน PostgreSQL catalog ด้วย table identity ที่ resolve จาก metadata เท่านั้น แล้วเปรียบเทียบ business columns, generated system columns, data type, length/precision/scale, nullability, primary key และ unique constraints ที่ `DdlBuilder` คาดหวัง
- ผล verification ต้องแยก `Healthy`, `Drifted`, `Missing` และ `VerificationFailed`; response คืน safe structural diagnostics แต่ไม่คืน connection string, server path, raw catalog query หรือ business row values
- การสร้าง table และเพิ่ม column ยังคงใช้ transaction เดิม; UI ห้ามประกอบหรือ execute DDL เอง และต้องแสดง `400`, `404`, `409` กับ server error แบบแก้ไขได้
- Source column ต้องเป็น `Text` และ `isRequired = false`; Normalized column ใช้กฎ type metadata เดิมจาก `TableDefinitionValidator`
- v1 เป็น expand-only: สร้าง table และเพิ่ม column ได้ แต่ไม่มี drop, rename, alter type/length, truncate หรือ hard delete; drift detection เป็น read-only และไม่ auto-repair
- หน้า detail ต้องแสดง Mapping Config/Config Version ที่อ้าง table เพื่อให้เห็น impact ก่อนเพิ่ม column; historical version และ pinned job ห้ามถูกแก้ย้อนหลัง
- หน้า Admin ตรวจโครงสร้าง ไม่ใช่ data browser; v1 ห้าม query, preview, export หรือค้น business rows เพื่อไม่เปิด PII ผ่าน control plane
- Admin mutation ต้องมี authorization แยก read/create/add-column, operator/reason, audit และ correlation ID; verify เป็น read-only แต่ต้อง rate-limit และบันทึกผล/เวลาโดยไม่ log sensitive values

- [x] **28.0 ล็อก Table Administration และ Schema Verification contract พร้อม Gate H**
  - ทำ: เพิ่ม design addendum ระบุ ownership/roles, authoritative metadata, expected physical schema ต่อ `Source`/`Normalized`, comparison rules, verification statuses, safe diagnostics, usage/impact projection, audit, rate limit, rollout และ rollback; ตัดสิน policy เมื่อพบ drift โดยห้าม default เป็น auto-repair
  - เข้าใจ: `GET /tables/{id}` ปัจจุบันยืนยัน metadata เท่านั้น ไม่พิสูจน์ว่า physical table, columns และ constraints ยังตรงกัน การให้ Admin เห็นคำว่า “สร้างแล้ว” จึงต้องแยก persistence success จาก schema verification
  - ตรวจ: Platform/Operations, Security และ Mapping owner ยืนยัน comparison/permissions; acceptance IDs ครอบคลุม healthy, extra/missing/changed column, missing table, catalog failure และ concurrent create/add-column
  - ผล: เพิ่ม [Table Administration and Schema Verification design](../spec/spec-design-table-administration-schema-verification.md) ล็อก authoritative metadata, expected Source/Normalized physical schema, status/diagnostic contract, expand-only mutation, impact projection, rate/audit/rollout และ no-auto-repair policy; คำสั่งให้ทำ Phase 28 อนุมัติ design สำหรับ implementation แต่ไม่ใช่ Gate H production sign-off

- [x] **28.1 สร้าง Table Schema Inspector Module และ PostgreSQL Adapter**
  - ทำ: เพิ่ม Interface ที่รับ table ID/typed expected definition แล้วอ่าน `pg_catalog`/`information_schema` ผ่าน parameterized query; canonicalize PostgreSQL types และเปรียบเทียบ expected/actual columns, nullability, identity, primary key และ unique constraints; คืน typed verification result กับ diagnostics ราย field
  - เข้าใจ: Controller/UI ไม่ควรรู้ catalog query หรือกฎ system columns; Inspector Module เป็นจุดเดียวที่ `DdlBuilder` tests และ Admin verification ใช้ expected-schema vocabulary ร่วมกันเพื่อลด drift ของกฎ
  - ตรวจ: unit tests ครอบคลุม type aliases และ ordering; PostgreSQL integration tests ครอบคลุม Source/Normalized ที่ healthy, extra column, missing column/table, type/nullability/constraint drift และ permission/timeout failure; ไม่มี query จากชื่อที่ browser ส่งตรง
  - ผล: เพิ่ม `ITableSchemaInspector`, shared expected-schema vocabulary, PostgreSQL type canonicalizer และ parameterized `pg_catalog` adapter; forced integration พิสูจน์ Healthy, extra/missing/type/constraint drift, Missing, safe outage และ malicious identifier ไม่ execute SQL

- [x] **28.2 เพิ่ม Admin Tables API, usage projection, authorization และ audit**
  - ทำ: reuse `GET/POST /tables`, `GET /tables/{id}` และ `POST /tables/{id}/columns` หลังวาง policy/audit ที่เหมาะสม; เพิ่ม endpoint read-only สำหรับ verification และ references/impact โดย response มี metadata, expected/actual safe summary, status, diagnostics, checkedAt และ config usage; รองรับ list search/filter ตาม name/kind/status แบบ bounded pagination
  - เข้าใจ: create response ต้องคืน ID ที่ UI ใช้ redirect ไป detail; verification ต้อง resolve table จาก ID ฝั่ง serverและไม่เปลี่ยน schema; list ไม่ควรยิง catalog query N+1 จึงใช้ persisted latest status หรือ bounded batch inspection ตาม contract ที่ Step 28.0 อนุมัติ
  - ตรวจ: unauthorized/forbidden, invalid definition, duplicate name/column, missing ID, stale/concurrent add-column, rate limit, audit completeness และ safe ProblemDetails tests ผ่าน; OpenAPI ไม่มี raw SQL/catalog/connection/data-row fields
  - ผล: ขยาย typed `/tables` ด้วย bounded search/kind/status, permissions, latest/fresh verification และ Config/Version/pinned-job references; migrations `0018`/`0019` เพิ่ม latest status, verification history/audit และ database append-only guards; create/add-column commit success audit ใน transaction เดิมและ concurrent add-column serialize ด้วย metadata row lock

- [x] **28.3 สร้างหน้า `/admin/tables` และ Create Table flow**
  - ทำ: เพิ่ม list/search/filter/status badge, empty/loading/error state และ create form สำหรับ name, kind และ ordered columns; form เปลี่ยน controls ตาม Source/Normalized rules, validate client เพื่อ feedback แต่ถือ server validation เป็น authoritative; เมื่อ `201 Created` ให้ redirect `/admin/tables/{id}` แล้ว trigger verification
  - เข้าใจ: ผู้ใช้ต้องยืนยันได้จากหน้าเดียวว่าคำสั่งสร้าง metadata/DDL สำเร็จและ physical schema healthy ไม่ใช่อาศัย toast อย่างเดียว; duplicate/conflict ต้องเก็บค่าฟอร์มไว้แก้ได้
  - ตรวจ: keyboard/screen reader, responsive layout, add/remove/reorder column form, Source type lock, Decimal/Text metadata controls, unsaved-change guard และ Playwright create Source/Normalized→redirect→Healthy ผ่าน
  - ผล: เพิ่ม list/search/filter/status, empty/loading/error, responsive typed create form, ordered add/remove/reorder controls, Source lock, Decimal/Text metadata, before-unload guard และ create→dynamic detail→auto-verify flow; Next 16 detail routeใช้ Suspense/Partial Prerender ตาม local framework docs

- [x] **28.4 สร้าง Table Detail, Verify และ Add Column flow**
  - ทำ: แสดง table ID/name/kind, business/system columns, type metadata, constraints, references, created/checked timestamps และ verification diagnostics; เพิ่ม `Verify now` และ add-column dialog ที่บังคับ reason แล้ว refresh metadata/verification หลัง `204 No Content`; แสดง expected-vs-actual diff แบบ field-level เมื่อ Drifted
  - เข้าใจ: extra/missing/changed physical object ต้องมองเห็นได้ แต่ v1 ให้ Operatorแก้ผ่าน runbook ไม่ auto-repair; เพิ่ม column เป็น additive operation เดียวที่ UI อนุญาต
  - ตรวจ: Playwright add column→Healthy, duplicate conflict, missing table, type/constraint drift, verify failure/retry, authorization visibility และ concurrent Admin flows ผ่าน; UI ไม่มี drop/rename/type-change/data-preview control
  - ผล: detail แสดง table identity, business/system columns, types/nullability/identity, PK/unique, checked time, field diff และ Config impact; Verify now เป็น read-only, add-column บังคับ operator/reason แล้ว reset/refresh/verify; kill switch ซ่อน mutation โดยยังตรวจ schema ได้และไม่มี destructive/data-browser control

- [x] **28.5 เพิ่ม observability, drift runbook และ rollout controls**
  - ทำ: เพิ่ม metrics/alerts สำหรับ create/add-column/verification outcome และ duration โดยไม่ใช้ table name เป็น unbounded metric label; เขียน runbook สำหรับ Healthy, Drifted, Missing, catalog permission failure, metadata/DDL rollback และ restore; เพิ่ม feature flag แยก Admin mutations จาก read/verify เพื่อ rollback UI write path ได้โดยไม่ปิดการตรวจสอบ
  - เข้าใจ: verification ไม่ควรทำให้ API หรือ PostgreSQL หนักจาก polling และ drift ห้ามถูกซ่อมอัตโนมัติโดยไม่มี impact analysis/backup; read-only inspection ยังมีประโยชน์เมื่อ mutation ถูก pause
  - ตรวจ: alert thresholds/load test/rate limit ผ่าน, logs มี correlation ID แต่ไม่มี PII/SQL/connection data, mutation kill switch ปิด create/add-column และยัง list/detail/verify ได้, runbook rehearsal บันทึก evidence ครบ
  - ผล: เพิ่ม bounded-label metrics สำหรับ mutation/verification/duration/rate-limit/unauthorized พร้อม drift, repeated failure และ unauthorized alerts; verify จำกัด 12 ครั้ง/table/minute และ catalog timeout 5 วินาที; [operations runbook](phase-28-table-administration-operations-runbook.md) ครอบคลุม backup/restore/incident/no-auto-repair และ production `MutationsEnabled=false`

- [x] **28.6 Full regression, security review, E2E และ Gate H sign-off**
  - ทำ: รัน backend/PostgreSQL/web regression ครบ; ทดสอบ create/list/detail/add-column/verify/reference impact, catalog tampering fixture, concurrent requests, authorization/audit, accessibility และ rollback; อัปเดต README, API examples และ `table-creation-old-vs-new.md` ให้ชี้หน้า Admin แทนข้อความว่าใช้ `.http` เท่านั้นเมื่อ rollout สำเร็จ
  - เข้าใจ: Phase นี้ห้ามเปลี่ยน runtime mapping semantics, immutable Config Version หรือ pinned Job; Admin เป็น control plane เหนือ Table Module เดิม
  - ตรวจ: existing Mapping Config และ LegacyTable/ApiContract/JsonFile regression ผ่าน; ไม่มี arbitrary SQL, identifier injection, data-row disclosure หรือ auto-repair; Platform/Operations, Security และ Mapping owner ลงนาม Gate H ก่อนเปิด mutation ใน production
  - ผล: technical gate ผ่าน—forced-PostgreSQL backend 514/514, format/TypeScript/ESLint/Next build, Playwright 20/20, API migration/startup และ npm production/NuGet vulnerabilities 0; [acceptance matrix](phase-28-acceptance-matrix.md), [runbook](phase-28-table-administration-operations-runbook.md) และ [Gate H evidence](phase-28-gate-h-evidence.md) ครบ. วันที่ 2026-11-03 ผู้ใช้อนุมัติในนาม Platform/Operations, Security และ Mapping พร้อมยอมรับ 16 moderate ใน dev-only ESLint/minimatch chain เดิม; production `MutationsEnabled` ยังเป็น `false` จนกว่าจะมี controlled rollout change แยกต่างหาก

### Definition of Done ของ Phase 28

- ผู้มีสิทธิ์สร้าง Source/Normalized table ผ่าน `/admin/tables` และถูกพาไป detail ของ table ใหม่ทันที
- หน้า detail แสดง metadata, expected system/business columns, constraints, references และผล physical schema verification พร้อม `checkedAt`
- Healthy/Drifted/Missing/VerificationFailed แยกชัดและมี field-level safe diagnostics; verification ไม่แก้ DB และไม่อ่าน business rows
- เพิ่ม column ผ่าน UI/API แบบ transaction เดิมแล้ว refresh เป็น Healthy; duplicate/concurrency/error แสดงแบบแก้ไขได้
- ไม่มี drop, rename, alter type/length, truncate, hard delete, raw SQL หรือ data browser ใน v1
- Authorization, operator/reason, audit, rate limit, metrics/alerts, kill switch และ drift runbook ผ่านการทดสอบ
- Backend/PostgreSQL integration, web E2E/accessibility, security และ full mapping regression ผ่าน
- Gate H owner sign-off เสร็จก่อนเปิด Table Admin mutation ใน production
