# Mapping Demo — แผน Input Layout, Transformation Config และหน้าเว็บ (Phase 19–20)

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

### คำสั่งตั้งต้นสำหรับ AI agent (copy ไปใช้ได้)

```text
อ่าน docs/web-config-plan.md, docs/learning-plan.md (เฉพาะหัวข้อ "วิธีใช้แผนนี้"),
docs/demo-plan.md หัวข้อ "ข้อสรุปรอบที่ 7–9", CONTEXT.md และ current-program-workflow.md หัวข้อ 6.2 กับ 8.2
ก่อน implement ให้ตรวจเครื่องตาม "Requirement ของเครื่อง" ในแผนนี้ ถ้าไม่ตรงให้หยุดและบอกฉันก่อน
ถ้าฉันสั่งเริ่ม Phase 19 หรือ Phase 20 ให้ทำเฉพาะ step แรกที่ยังไม่เสร็จของ Phase นั้น แล้วหยุดรอคำสั่งใหม่
ถ้าฉันสั่ง Step ที่ระบุเลข ให้ทำเฉพาะ step นั้น อธิบาย concept ก่อนเขียนโค้ด แล้วบอกวิธีตรวจผล
ห้ามติดตั้ง npm package เพิ่มเอง ห้ามรับหรือ execute raw SQL จาก config และห้ามสร้าง git commit เอง
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

Config validation ต้องตรวจ encoding/position unit จาก allowlist, `preambleLines` ไม่ติดลบ, start/length เป็นจำนวนบวก, end ไม่เกิน expected length, Source column มีจริงและไม่ซ้ำ, discriminator ไม่กำกวม, sequence อ้าง rule ที่มีจริงและไม่ซ้ำ, min/max ถูกต้อง และทุก rule ถูกอ้างใน sequence ส่วน gap/overlap เป็น warning เพราะบาง layout อาจตั้งใจอ่านช่วงเดียวกันมากกว่าหนึ่ง field

Error แยกเป็น:

- Config error: layout/field/path ไม่ถูกต้อง ทำให้ create/activate ไม่ผ่าน
- File import validation error: decode ไม่ได้, record type ไม่รู้จัก, sequence/cardinality ไม่ตรง, expected length ไม่ตรงหรือ slice ไม่ได้ ทำให้ File Import Job ทั้งไฟล์เป็น `ImportFailed` ก่อนสร้าง Source row/Row Job/outbox และไม่ archive ว่าสำเร็จ

Worker ต้อง pre-scan snapshot ทั้งไฟล์ผ่าน decode/classify/length/slice validation ก่อนเขียน Source row แรก หากไม่ผ่านให้เก็บ snapshot ไว้สำหรับ retry และบันทึก error แรกใน `file_jobs.last_error` ด้วย stable error code, physical line number, record type และ expected/actual value เช่น `InvalidRecordLength: physical line 17, record type D, expected 3761 UTF-16 code units, actual 3750.` โดยไม่เขียน `row_errors`; ตารางนั้นสงวนไว้สำหรับ Source-to-Normalized Row Error ที่มี Row Job แล้ว หากอนาคตต้องการ partial import และเก็บหลาย input errors ให้เพิ่ม `file_import_errors` พร้อมออกแบบ status/counts เป็น feature แยก

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
- `evaluation_at` เป็น instant ที่เก็บใน `timestamptz`; `currentDate` แปลง instant นี้ด้วย IANA timezone `Asia/Bangkok` แล้วคืนเฉพาะ typed `Date` ห้ามใช้ `DateTime.Now` หรือ timezone ของเครื่อง Worker; retry ใช้ค่าเดิม ส่วน reprocess สร้าง evaluation time ใหม่
- ถ้า output ใดเกิด data error ให้การประเมิน Source row นั้นเป็น `Invalid` ทั้งชุดและไม่แก้ Current Normalized Result เดิม
- ถ้าไม่มี output ผ่าน filter ให้เป็น `Filtered` ไม่ใช่ `Invalid`
- `Filtered` เป็น terminal success: ไม่นับเป็น `Done` แต่ไม่ทำให้ File Job เป็น `CompletedWithErrors`; หากมี `Pending` ให้เป็น `InProgress` ก่อน และมีเพียง `Invalid`/`Failed` ที่ทำให้เป็น `CompletedWithErrors`
- Current Normalized Result เป็นชุด `0..N` outputs ต่อ Source row โดย identity คือ `(source_row_id, output_key)` ไม่ใช่ business key เช่น `CLOANNUMBER`; Source row เดียวจึงมีทั้ง `pmx` และ `ctp` ที่ใช้ `CLOANNUMBER` เดียวกันได้
- Reprocess ที่สำเร็จต้อง reconcile desired output set ใน transaction เดียว: upsert output ปัจจุบันและลบ stale output ที่ไม่ emit แล้ว; ถ้า desired set ว่างให้ลบ outputs เดิมทั้งหมดและ mark Row Job เป็น `Filtered` ใน transaction เดียว ส่วน `Invalid`/`Failed` ห้าม reconcile และต้องเก็บชุดเดิม
- จำกัด AST depth, node count, CASE arms, output count และ lookup keys เพื่อกัน config ที่ใช้ทรัพยากรเกินขอบเขต
- Config Version pin logic แต่ lookup ใช้ live committed business state; `mtiOriginalPolicyExists@1` ใช้ business key `(systemId, productName, loanNumber)` และตัดทุกผลที่มี `source_row_id = currentSourceRowId` ออกจากคำตอบ จึงทำให้ retry/reprocess ของ Source row เดิมไม่ block ตัวเอง ขณะที่ Source row อื่นที่มี product + loan เดียวกันยัง match และ loan เดียวกันคนละ product ไม่ match

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
| `formatRegNo@1` | In-process ถ้าถอด logic ได้ | ย้ายเป็น pure C# พร้อม golden parity tests; ถ้ายังพึ่ง legacy DB ใช้ typed port และ parameterized Adapter |
| `mtiOriginalPolicyExists@1` | Local-substitutable | ใน demo อ่าน PMIB Normalized table เดียวกับที่ Transformation Program เขียน; config เห็น logical capability เท่านั้น ส่วน Adapter resolve physical table ที่เชื่อถือได้และ in-memory Adapter ใช้ใน unit tests |
| เวลา | In-process input | Worker ส่ง persisted `evaluation_at` ซึ่งเป็น instant; evaluator ใช้ timezone `Asia/Bangkok` จาก semantics profile และ test ส่งค่าคงที่ |

ใน demo ให้ถือ PMIB Normalized table เป็น `MTI_ORIGINAL` ตามความหมายใน workbook และสร้างผ่าน Tables API โดย `configure-demo.ps1`; config เรียกเพียง `mtiOriginalPolicyExists@1(systemId, productName, loanNumber)` โดยไม่รู้ table/SQL ส่วน runtime เติม `currentSourceRowId` จาก Row Job context ให้ lookup request เอง ห้ามรับ identity นี้จากค่าที่ผู้ใช้กำหนด

Production Adapter ต้อง resolve physical table จาก server-owned metadata แล้ว query แบบ parameterized ด้วย business key `(SYSTEM_ID, CPRODUCTNAME, CLOANNUMBER)` พร้อมคืน `source_row_id` ที่ match เพื่อให้ caller ตัด `currentSourceRowId` ออก การ resolve ทั้ง batch ต้อง deduplicate keys และใช้จำนวน query คงที่ตาม batch ไม่ query ต่อ field/row; `configure-demo.ps1` สร้าง composite index อย่างน้อย `(SYSTEM_ID, CPRODUCTNAME, CLOANNUMBER)` ด้วย trusted setup SQL หลัง Tables API สร้างตาราง และตอน activate ต้องตรวจว่าคอลัมน์กับ index นี้มีจริง

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

- [ ] **19.0 ตรึง semantics และสร้าง coverage-driven golden cases**
  - ทำ: ใช้ PMIB-01/02 และ TIB-01 เป็น requirement source แล้วสร้าง minimal synthetic fixtures ตาม coverage matrix; ตรึง `sqlserver-legacy-v1` ให้ครอบคลุม null/empty/blank, three-valued boolean logic, ordinal case-insensitive comparison, LIKE `%`/`_`/bracket patterns, invariant decimal/exact date formats และ `Asia/Bangkok`; Transformation ครอบคลุม direct/default, implicit assignment trim/conversion, explicit trim, CASE ทุก branch, SUBSTRING/CONVERT, current date, `FormatRegNo`, anti-lookup แบบ other-source/self-exclusion/same-loan-different-product และ primary + CTP ส่วน Fixed-width ใช้ `H/D/D/T` กับ fields ตัวแทนต้น/กลาง/ท้าย, ภาษาไทย, padding และ date พร้อม structural fixture ที่มีหลาย header/trailer โดยไม่ใช้ workbook เป็น runtime input
  - เข้าใจ: fixture วัด behavior ไม่วัดว่าคัดลอก workbook ได้ครบจำนวน; `sqlserver-legacy-v1` คือ subset ที่ประกาศเอง ไม่ใช่ SQL Server emulator ทุก collation/operator และ behavior ของ identifier นี้ห้ามเปลี่ยนหลัง release ส่วน fixed-width ต้องตรึง encoding, position unit, newline, padding, short/long-record policy และความหมายของ `VALUEFORMAT=dd/MM/yyyy` เมื่อ raw เป็น `yyyyMMdd`
  - ตรวจ: coverage matrix ทุกช่องชี้ไปยัง fixture อย่างน้อยหนึ่งข้อและไม่มี fixture ที่มีไว้เพียงให้จำนวนเท่า workbook; truth table ของ null, case-insensitive equality/IN/LIKE และ wildcard ทุกชนิดตรง profile; decimal จุดผ่านแต่ comma คั่นหลักไม่ผ่าน; default/explicit Date/DateTime format ตรง expected; `currentDate` ให้ `2026-10-08` ที่ `2026-10-08T16:59:59Z` และให้ `2026-10-09` ที่ `2026-10-08T17:00:00Z`; positive fixed-width fixture มี Import 2/Ignore 2 และ byte length ของ Detail ต่างกันแต่ fields ต้น/กลาง/ท้ายไม่เลื่อน; negative fixture คืน stable error code พร้อม physical line/record details ตรงกรณี

- [ ] **19.1 สร้าง typed Input Layout contract และ compiler**
  - ทำ: สร้าง namespace/folder `MappingDemo.Shared.Input` ใน project เดิม โดยมี public Input Layout contracts/results, `InputRecordModule` facade และ opaque `CompiledInputLayout`; implementation ของ compiler เป็น `internal`; เปลี่ยน `FileToSourceRule` เป็น discriminated `DelimitedInputLayout`/`FixedWidthInputLayout`; Delimited เพิ่ม preamble lines กับ none/single-row header policy ส่วน Fixed-width เพิ่ม encoding, position unit, record discriminator/action/expected length, ordered `recordSequence` พร้อม min/max occurrences, field slice และ exact-path diagnostics
  - เข้าใจ: Input Layout เป็น Interface ของ File-to-Source parsing และเป็นส่วนหนึ่งของ Config Version; Module ไม่รู้ path/job/database และ `slice` ไม่ใช่ transformation `substring`; ไม่สร้าง `MappingDemo.Input` project ใน Phase 19
  - ตรวจ: API/Worker มองเห็นเฉพาะ public Interface/contracts ไม่เห็น compiler internals; serialization round-trip ของ CSV เดิม, Delimited ที่มีหลาย preamble lines และ compact fixed-width draft ผ่าน; compiler ปฏิเสธ unknown encoding/unit, preamble ติดลบ, start/length ไม่บวก, end เกิน record, target column ไม่มี/ซ้ำ, discriminator กำกวม, sequence อ้าง rule ผิด/ซ้ำ และ min/max ไม่ถูกต้อง

- [ ] **19.2 ทำ Fixed-width Input Record Parser**
  - ทำ: implement `internal` Delimited/FixedWidth readers หลัง `InputRecordModule` Interface เดียวกัน; ย้าย/รวม `CsvRecordReader` เดิมเข้า Input Module เพื่อไม่ให้มี parser สองชุด; fixed-width reader decode UTF-8 ก่อนนับตำแหน่ง, ตัด newline, classify ตาม record rules, ตรวจ sequence/cardinality และ length ของแต่ละ rule แล้ว slice ด้วย 1-based UTF-16 code units; คืน raw field values กับ physical line number และไม่ trim ก่อนเขียน Source
  - เข้าใจ: Detail มี 3,761 text positions แต่ 3,925–4,113 bytes จึงห้ามใช้ byte offset; parser เป็น in-process pure logic หลังรับ decoded text และทดสอบผ่าน Interface ของ Module
  - ตรวจ: tests เรียกผ่าน Input Module Interface ไม่อ้าง reader/compiler helper; compact `H/D/D/T` fixture ได้ Import 2/Ignore 2 และ `H/H/D/D/T/T` ผ่านเมื่อ cardinality อนุญาต; missing/excess/out-of-order records ไม่ผ่านด้วย stable error code; fields ต้น/กลาง/ท้ายตรง golden, Detail ที่มีภาษาไทยและ byte length ต่างกันไม่ทำให้ตำแหน่งเลื่อน, invalid UTF-8/non-BMP/unknown type/short/long Detail คืน error class ที่กำหนด

- [ ] **19.3 Persist Input Layout และปรับ Config Version API**
  - ทำ: replace `file_to_source` ด้วย `input_layout jsonb NOT NULL` ใน baseline migration, ลบ legacy property/model path ที่เกี่ยวข้อง, update create/clone/get request/response/service และ `configure-demo.ps1` ให้รับเฉพาะ Input Layout ใหม่ แล้วหยุด API/Worker และรัน `reset-demo.ps1` ก่อน configure ใหม่
  - เข้าใจ: เป็น breaking cutover ของ demo ไม่มี compatibility Adapter หรือการแปลง Config Version เดิม; Config Version เป็นเจ้าของ Input Layout แบบ immutable และ Worker ต้องโหลด layout จาก version ที่ File Job pin ไว้ ไม่ใช่จาก active version ล่าสุด
  - ตรวจ: schema ไม่มี `file_to_source`, request แบบ `fileToSource` ใช้งานไม่ได้, create/clone/get version round-trip Delimited/Fixed-width layout ครบ, version ที่ pin ไว้โหลด layout เดิมได้หลัง activate version ใหม่ และ reset + script demo ผ่าน

- [ ] **19.4 เชื่อม Input Record Parser เข้า File Import Worker**
  - ทำ: `FileImportHandler` เลือก compiled layout ตาม Config Version และใช้ parser เดียวสำหรับ Delimited/FixedWidth; เพิ่ม `physical_line_number integer NOT NULL` ใน generated Source DDL/reserved identifiers และให้ `SourceRowWriter` เขียนค่านี้; pre-scan snapshot ผ่าน decode/classify/sequence/cardinality/length/slice validation ก่อนเขียนแถวแรก แล้วจึงเขียนเฉพาะ Import records; หาก validation ไม่ผ่านให้ตั้ง `ImportFailed`, บันทึก stable diagnostic ใน `file_jobs.last_error`, เก็บ snapshot สำหรับ retry และไม่สร้าง Source row/Row Job/outbox; ปรับ duplicate/archive/status โดยไม่ทำ regression CSV
  - เข้าใจ: Header/Trailer ที่ Ignore ไม่ใช่ Source row และไม่เพิ่ม Row Number; `row_errors` ใช้เฉพาะ normalization ที่มี Row Job ส่วน input validation error ทำให้ fail ทั้งไฟล์และไม่ archive ว่าสำเร็จ
  - ตรวจ: DDL/writer tests มี `physical_line_number`; CSV acceptance เดิมและ Delimited ที่มีหลาย preamble lines ผ่าน, `H/H/D/D/T/T` สร้าง Source rows 1–2 ที่ physical lines 3–4; invalid UTF-8/unknown type/sequence/cardinality/short/long/slice error แต่ละกรณีได้ `ImportFailed` กับ code/physical line/record type/expected-actual ที่กำหนด และมี Source rows, Row Jobs, outbox messages เท่ากับศูนย์

- [ ] **19.5 ขยาย Table Definition และ result identity**
  - ทำ: เพิ่ม `Integer`, `DateTime`, `Guid` และ metadata ที่จำเป็นสำหรับ length/precision/scale; เพิ่ม `output_key` ใน Normalized table และ unique `(source_row_id, output_key)`; ปรับ DDL/tests และกำหนดว่า demo ต้อง reset DB
  - เข้าใจ: workbook ใช้ type มากกว่า demo และ `UNION` ทำให้ `source_row_id` อย่างเดียวไม่เป็น identity ที่ถูกต้อง
  - ตรวจ: DDL tests ครอบคลุม type ใหม่และหนึ่ง source row insert output key ต่างกันได้สองแถว แต่ key เดิมซ้ำไม่ได้

- [ ] **19.6 สร้าง typed Transformation Program contract**
  - ทำ: สร้าง namespace/folder `MappingDemo.Shared.Transformation` ใน project เดิม โดยมี public Transformation contracts/results, `TransformationModule` facade และ opaque `CompiledTransformationProgram`; compiler/evaluator/profile/function implementations เป็น `internal`; สร้าง model ของ program/output/field assignment/value/boolean expression, optional `inputFormat`, `languageVersion`, required string `semanticsProfile` และ JSON polymorphism แบบ explicit discriminator; เพิ่ม serialization round-trip tests โดยใช้ `sqlserver-legacy-v1`
  - เข้าใจ: AST เป็น declarative data ไม่ใช่ executable code; `languageVersion` คุม shape ส่วน `semanticsProfile` เป็น immutable server-owned preset ที่คุมความหมาย ไม่ใช่ option object; Module ไม่รู้ controller/Kafka/job/persistence transaction; direct mapping เป็น `source` expression ไม่ต้องมี pipeline พิเศษอีกชุด; comparison operands เป็น expression และ literal ระบุ type เสมอ
  - ตรวจ: API/Worker มองเห็นเฉพาะ public Interface/contracts ไม่เห็น compiler/evaluator internals; serialize/deserialize ตัวอย่าง 1–5, field assignment ที่มี/ไม่มี `inputFormat` และ `trim(source(...))` กลับมาเท่ากัน; payload ที่ profile หายหรือเป็น object, shorthand `field`/`value`, literal ที่ไม่มี `type` และ unknown `kind` ถูกปฏิเสธด้วย path ที่ชัดเจน

- [ ] **19.7 Persist Transformation Program และปรับ Config Version API**
  - ทำ: replace `source_to_normalized` ด้วย `transformation_program jsonb NOT NULL` ใน baseline migration, ลบ legacy property/model path ที่เกี่ยวข้อง, update create/clone/get request/response/service และ `configure-demo.ps1` ให้รับเฉพาะ Transformation Program ใหม่ แล้วหยุด API/Worker และรัน `reset-demo.ps1` ก่อน configure ใหม่
  - เข้าใจ: เป็น breaking cutover ของ demo ไม่มี compatibility Adapter, dual-read/write หรือ legacy AST converter; Config Version pin ทั้งโครงสร้าง AST และ semantic identifiers และ Worker ต้องประเมิน program จาก version ที่ Row Job pin ไว้
  - ตรวจ: schema ไม่มี `source_to_normalized`, request แบบ `sourceToNormalized` ใช้งานไม่ได้, create/clone/get version round-trip program ครบทุก node ใน contract, version ที่ pin ไว้โหลด program เดิมได้หลัง activate version ใหม่ และ reset + script demo ผ่าน

- [ ] **19.8 Compiler และ static validation**
  - ทำ: compile program กับ Source/Normalized schemas, resolve `languageVersion` และ `semanticsProfile` จาก server-owned catalogs, infer type/nullability, หา dependencies, แทรก Target Assignment Coercion สำหรับ trim/text-to-target conversion, validate `inputFormat`/required/duplicate targets/function/lookup/version และ enforce complexity limits; คืน diagnostic code + exact path
  - เข้าใจ: coercion อยู่ใน compiled program ไม่แก้ persisted JSON; create, activate, preview และ runtime ต้องใช้ compiler/profile implementation เดียวกัน; compiled program cache ด้วย Config Version/hash และห้ามเปลี่ยน behavior ของ profile identifier เดิม
  - ตรวจ: unit tests ครอบคลุม unknown/missing profile, implicit Text/Date/DateTime/Decimal/Integer/Boolean/Guid coercion, already-typed value ที่ไม่ convert ซ้ำ, `inputFormat` กับ target/input ที่ใช้ไม่ได้, unknown field, `trim` ของ input ที่ไม่ใช่ text, type mismatch, required หาย, duplicate output/target, AST ลึกเกิน และ path ตรง payload

- [ ] **19.9 Evaluator สำหรับ pure expressions**
  - ทำ: รองรับ source/metadata/literal, trim, coalesce/defaultIfBlank, CASE, comparison, blank, AND/OR/NOT, LIKE, IN, substring, convert และ Target Assignment Coercion โดย implement `sqlserver-legacy-v1` ตาม null truth table, ordinal case-insensitive text/LIKE, invariant decimal และ exact date formats ที่ตรึงไว้; ประเมิน common `sourceWhen` ก่อน output `emitWhen`
  - เข้าใจ: `source` คืน raw text; explicit `trim` ใช้ระหว่าง expression ส่วน assignment trim text และ convert ตาม target; blank required เป็น Row Error, blank optional เป็น null; evaluator คืนผลและ Row Errors ไม่เขียน DB; CASE short-circuit แบบ first-match-wins
  - ตรวจ: golden tests ครอบคลุม raw source ที่ยังมี padding, implicit assignment ของ padded/blank/null/date/decimal text, explicit `trim` ของ padded/empty/null text, conversion error path, ตัวอย่าง 1–2 และ operator ใน workbook โดยไม่สร้าง SQL

- [ ] **19.10 Current time และ named transform**
  - ทำ: เพิ่ม schema migration `row_jobs.evaluation_at timestamptz NOT NULL DEFAULT now()`, metadata/evaluation time และ function catalog ที่ pin version; initial/reprocess กำหนด instant ตอนสร้าง Row Job แต่ retry รักษาค่าเดิม; evaluator แปลง instant ด้วย IANA timezone `Asia/Bangkok` จาก semantics profile แล้วให้ `currentDate` คืน typed `Date`; implement `formatRegNo@1` เป็น pure C# จาก behavior ที่ยืนยันได้ หรือใช้ typed legacy Adapter ชั่วคราวถ้าต้องพึ่ง DB
  - เข้าใจ: ห้ามใช้ `DateTime.Now`, local timezone ของ Worker หรือค่าที่อ่านใหม่ตอน retry; config ระบุชื่อ logical capability เท่านั้น, `SELECT dbo...` ไม่อยู่ใน config และ retry ต้องได้ current date ค่าเดิม
  - ตรวจ: migration/schema test ผ่าน; boundary `2026-10-08T16:59:59Z → 2026-10-08` และ `2026-10-08T17:00:00Z → 2026-10-09` ผ่านทั้ง evaluator/preview; retry ใช้ `evaluation_at` เดิมแม้ข้ามเที่ยงคืนกรุงเทพฯ ส่วน reprocess ได้ instant ใหม่; parity tests ของ `FormatRegNo` ตามตัวอย่าง 3 ผ่าน และ unknown/version ผิด compile ไม่ผ่าน

- [ ] **19.11 Named lookup และ batch resolution**
  - ทำ: เพิ่ม lookup catalog/port ที่ Transformation Module เป็นเจ้าของสำหรับ `mtiOriginalPolicyExists@1`; วาง PostgreSQL Adapter ที่ API/Worker ใช้ร่วมกันใต้ `MappingDemo.Shared.Transformation.Adapters` และใช้ in-memory Adapter ใน tests; logical arguments จาก config คือ `systemId`, `productName`, `loanNumber` และ runtime แนบ `currentSourceRowId`; production Adapter resolve PMIB Normalized table จาก trusted metadata, query แบบ parameterized เป็น batch และคืน matching `source_row_id`; ให้ `configure-demo.ps1` สร้าง PMIB Source/Normalized ผ่าน Tables API และสร้าง composite index `(SYSTEM_ID, CPRODUCTNAME, CLOANNUMBER)` ด้วย trusted setup SQL แล้วตรวจ required columns/index ตอน activate
  - เข้าใจ: PMIB Normalized table คือ `MTI_ORIGINAL` ของ demo; lookup ตัด matching row ที่ `source_row_id` เท่ากับ current Source row, cache ได้เฉพาะใน batch และอ่าน live committed state จึงไม่ใช่กลไกรับประกัน uniqueness ระหว่างงาน concurrent; timeout เป็น system error ที่ retry ได้ ไม่ใช่ Row Error
  - ตรวจ: golden anti-lookup ตามตัวอย่าง 4 ผ่าน; retry และ reprocess ของ Source row เดิมไม่ block ตัวเอง, Source row อื่นที่มี product + loan เดียวกันถูกพบ, loan เดียวกันคนละ product ไม่ match; SQL injection input ไม่เปลี่ยนโครง query, จำนวน query คงที่ตาม batch และ PostgreSQL integration test ใช้ composite index ที่กำหนด

- [ ] **19.12 รองรับ `0..N` outputs และ atomic reconcile**
  - ทำ: เพิ่ม schema migration ให้ `row_errors` มี nullable `output_key`, non-null `expression_path` และเปลี่ยน `field` เป็น nullable; evaluator คืนหลาย output พร้อม `outputKey`; writer atomic reconcile desired output set โดย upsert current outputs และลบ stale outputs รวมถึงลบทั้งหมดเมื่อ desired set ว่าง; mark `Done`/`Filtered` ใน transaction เดียวกับ reconcile แต่ `Invalid`/`Failed` ไม่แตะ Current Normalized Result; เพิ่ม `RowJobStatus.Filtered`, `FilteredRows` ใน count query/`FileJobResponse` และปรับ `NormalizationStatusCalculator` ให้ Done/Filtered เป็น terminal success
  - เข้าใจ: legacy `UNION` คือ output branch เพิ่ม ไม่ใช่ string operator; `CLOANNUMBER` เดียวมีทั้ง `pmx`/`ctp` ได้เพราะ identity ใช้ `(source_row_id, output_key)`; `Filtered` คือ successful empty result จึงลบ outputs เดิม ไม่ใช่ Done/Invalid/Failed; Pending มี precedence เป็น `InProgress` และเฉพาะ Invalid/Failed ทำให้ `CompletedWithErrors`
  - ตรวจ: migration/schema test ผ่าน; field error ระบุ output/field/path, `emitWhen` error มี output แต่ field เป็น null และ `sourceWhen` error มี output/field เป็น null แต่ยังมี path; calculator tests ครอบคลุม all-Done, all-Filtered, Done+Filtered, Pending+Filtered, Filtered+Invalid และ Filtered+Failed; `GET /file-jobs/{id}` คืน `FilteredRows`; ตัวอย่าง 5 ทำให้ Source row/`CLOANNUMBER` เดียวได้ทั้ง `pmx`/`ctp`, reprocess จากสอง outputs เหลือเฉพาะ pmx, เหลือเฉพาะ ctp และเหลือศูนย์ได้ถูกต้อง; Filtered ซ้ำเป็น idempotent, Invalid/Failed เก็บชุดเดิม และ failure กลาง transaction rollback ทั้ง outputs/status

- [ ] **19.13 เชื่อม Transformation Module เข้า Worker**
  - ทำ: `RowNormalizeHandler` compile/cache ตาม Config Version, โหลด Source fields จาก dependencies, resolve lookup เป็น batch โดยแนบ `currentSourceRowId` จาก Row Job context, evaluate และส่งผลให้ writer; persist `row_errors.output_key`/nullable field/`expression_path`; ปรับ retry/reprocess/idempotency ให้รองรับหลาย output และ self-exclusion; ลบ `SourceToNormalizedRule`, `RowNormalizer` และ legacy serializer/evaluator path หลัง tests ใหม่ผ่าน
  - เข้าใจ: Worker reference `MappingDemo.Shared` และเรียก `MappingDemo.Shared.Transformation` ผ่าน Interface; Kafka, job orchestration และ persistence transaction อยู่ภายนอก Module แต่ business semantics อยู่หลัง seam เดียว
  - ตรวจ: direct-mapping characterization tests ให้ observable results เดิมผ่าน Module ใหม่โดยไม่ deserialize contract เก่า, repository ไม่เหลือ runtime reference ถึง legacy rules/evaluator, Row Error API คืน output/nullable field/expression path ครบ, retry งานเดิมและ reprocess ที่สร้าง Row Job ใหม่แต่ชี้ Source row เดิมไม่ถูก lookup ของตัวเองกรองทิ้ง และ end-to-end PMIB fixture ให้ status/count/output ตรง expected

- [ ] **19.14 เพิ่ม capabilities, validate และ preview endpoints**
  - ทำ: เพิ่มสาม endpoint ตามตาราง, capability catalog คืน immutable semantics profiles พร้อม read-only summary รวมทั้ง target conversion types/date formats สำหรับ `inputFormat`, ใช้ Input Layout/Transformation compiler และ evaluator ชุดเดียวกัน; preview รับ discriminated sample แบบ `textFile` JSON หรือ sample Source rows พร้อม `evaluationAt` แบบ ISO 8601 ที่มี `Z`/UTC offset, จำกัด text `1 MiB`/50 parsed records และ endpoint body `3 MiB`, echo instant ที่ใช้จริง และห้ามเขียน DB/disk หรือ log sample content; API ส่ง enum เป็น string ด้วย `JsonStringEnumConverter`
  - เข้าใจ: API reference `MappingDemo.Shared` และใช้ Module Interfaces เดียวกับ Worker; text file ถูก re-encode เป็น UTF-8 `MemoryStream`, `fileName` เป็น display metadata ไม่ใช่ server path และ request ที่เกิน limit ต้อง reject ไม่ truncate; capability catalog ทำให้ web ไม่ hard-code input format/encoding/position unit/operator/function/lookup ที่ backend ไม่รองรับ
  - ตรวจ: capabilities คืน `sqlserver-legacy-v1` กับ summary และ target conversion types/date formats ตรง compiler; `evaluationAt` ที่ไม่มี offset ถูกปฏิเสธ; preview echo instant และให้ `currentDate` ตรง boundary ของ `Asia/Bangkok`; invalid profile/`inputFormat`/draft คืน exact paths; valid 87 KB/23-record TIB sample ผ่าน, 1 MiB พอดีผ่าน, เกิน 1 byte/record ที่ 51/request body เกิน limit ถูกปฏิเสธด้วย `SampleTooLarge`/`TooManySampleRecords`/HTTP 413 โดยไม่ truncate; filename traversal ไม่ถูกเปิดเป็น path, log ไม่มี content, จำนวน file/Source/Normalized/Job rows และไฟล์บน disk ไม่เปลี่ยน และ API enum เป็นชื่อ

- [ ] **19.15 ตรวจรับ backend และ handoff ด้วย coverage-driven fixtures**
  - ทำ: สร้าง Config Version ขนาดเล็กที่ครอบคลุม semantics จากตัวอย่าง PMIB 1–5 และ fixed-width layout + transformations; รัน golden/unit/integration/end-to-end tests โดยใช้จำนวน rows/fields ต่ำสุดตาม coverage matrix; เมื่อผ่านให้อัปเดตเอกสาร backend ที่เกี่ยวข้องและติ๊ก Phase 19 ใน `learning-plan.md`
  - เข้าใจ: step นี้เป็น phase gate; ต้องผ่านก่อนจึงถือว่า backend contract พร้อมให้หน้าเว็บตั้งค่าได้จริงและเริ่ม Phase 20 ได้ ส่วน regression gate รักษา behavior ไม่ใช่ legacy JSON/schema compatibility
  - ตรวจ: reset แล้ว schema/Config API มีเฉพาะ contract ใหม่และ `configure-demo.ps1` ผ่าน; direct CSV ไม่ regression ผ่าน Input/Transformation Modules ใหม่โดยไม่มี legacy parser/evaluator, implicit assignment trim/conversion ให้ผลเท่าพฤติกรรมเดิม, Delimited preamble/header policy ถูกต้อง, fixed-width ได้ Import 2/Ignore 2, multi-header/trailer sequence และ cardinality ผ่าน/ไม่ผ่านตาม config, fields ตัวแทนตรงตำแหน่ง, CASE ทุก branch ตรง expected, `FormatRegNo` parity ผ่าน; lookup ครอบคลุม not found, other-source found, self-only ignored และ same-loan-different-product; retry/reprocess ไม่ block ตัวเอง, `CLOANNUMBER` เดียวมี `pmx`/`ctp` ได้ และ reprocess ผล `2→1→0` outputs reconcile identity/Current Normalized Result ถูกต้อง
  - Commit: หลังผ่านให้สรุป backend handoff และรอผู้ใช้สั่ง commit แยกต่างหาก; AI agent ห้ามสร้าง commit เอง

## Phase 20 — หน้าเว็บตั้งค่า

ห้ามเริ่ม Phase 20 จนกว่า 19.15 จะผ่านครบและ Phase 19 ถูกติ๊กเสร็จ เว้นแต่ผู้ใช้สั่งข้ามอย่างชัดเจน

- [ ] **20.0 ตรวจเครื่องสำหรับเว็บ**
  - ทำ: ตรวจ Node/npm/network/port 3000 และกรอกเวอร์ชัน; ยืนยัน API health และ backend 19.0–19.15 ผ่าน
  - ตรวจ: `node -v`, `npm -v`, `npm ping`, port 3000 ว่าง และ `GET http://localhost:5181/health` ได้ 200

- [ ] **20.1 สร้างโปรเจกต์ `web/`**
  - ทำ: ใช้ `create-next-app` เลือก TypeScript, ESLint, Tailwind, App Router และไม่ใช้ `src/`; กรอกเวอร์ชันจริง
  - ตรวจ: `npm run dev` เปิดหน้าเริ่มต้นได้และ `git status` ไม่เห็น `node_modules`/`.next`

- [ ] **20.2 Proxy และ API client**
  - ทำ: rewrite `/api/:path*` ไป `API_BASE_URL`; สร้าง `.env.local.example`, contract types, fetch wrapper และ `ApiError` ที่เก็บ `status/title/detail/errors`
  - เข้าใจ: browser ใช้ origin เดียว; API client ไม่ผูก React เพื่อย้ายแนวคิดไป Angular service ได้
  - ตรวจ: `/api/health` ผ่านและ `npm run build` ผ่าน type check

- [ ] **20.3 Layout, list, create และ details**
  - ทำ: สร้าง `/configs`, `/configs/new`, `/configs/[id]`; แสดง config/version/output/rule counts, clone และ activate; map ValidationProblem/409 ตาม field path
  - ตรวจ: สร้าง config, ดู versions, clone และ activate ได้; API ปิดอยู่แล้ว UI แสดง error ไม่ค้างว่าง

- [ ] **20.4 Input Layout editor และ raw-record preview**
  - ทำ: เพิ่ม Delimited/Fixed-width selector; Delimited form มี preamble lines กับ none/single-row header policy; fixed-width form มี encoding, position unit, record rules, ordered sequence/min/max occurrences, expected length และ field grid Source column/start/length/end; รับ pasted sample text หรือ UTF-8 file เพื่อแสดง raw slices, trimmed display, gap/overlap warnings และ physical line errors; browser ตรวจ `File.size`, decode `ArrayBuffer` ด้วย fatal UTF-8 `TextDecoder`, pre-check line count แล้วส่ง `textFile` JSON โดยไม่ใช้ multipart
  - เข้าใจ: UI ส่งตำแหน่ง 1-based แต่ไม่ตัดข้อความเองเป็น source of truth; browser validation ช่วยตอบเร็วแต่ API เป็น authoritative limit/parser และใช้ implementation เดียวกับ Worker; sample ไม่ถูก upload/persist เป็นไฟล์
  - ตรวจ: invalid UTF-8/เกิน 1 MiB/เกิน 50 records ถูกหยุดพร้อมข้อความตรง code; preview Delimited ที่มีหลาย preamble lines ได้; กรอกหรือ clone fixed-width draft แล้ว preview `H/H/D/D/T/T` ตาม min/max ได้ Import 2/Ignore 4, Source rows อ้าง physical lines 3–4, fields ต้น/กลาง/ท้ายและภาษาไทยไม่เลื่อน, padding แสดงทั้ง raw/trimmed และ ignored records แสดง Ignore โดยไม่ต้องกรอก 115 fields

- [ ] **20.5 Basic Transformation editor**
  - ทำ: สร้าง `ValueExpressionEditor` แบบ recursive เป็น editor กลาง; output `main` เริ่มต้นมีหนึ่งแถวต่อ Normalized column และแก้ `outputKey` ได้ มุมมองพื้นฐานรองรับ Direct field, Constant, Current date และไม่ map; Direct field ใช้ Target Assignment Coercion และ Date/DateTime แสดง `inputFormat` ตาม capability
  - เข้าใจ: ทุกตำแหน่งที่รับ value expression ต้อง reuse editor เดียวกัน; payload-index-to-control map ต้องคง error path ถูกแม้ซ่อน/reorder/nest fields
  - ตรวจ: สร้าง direct mapping เทียบ demo เดิม, padded text ถูก trim, raw date ที่เลือก `inputFormat` ถูก convert, typed constant/current date ไม่ convert ซ้ำ, required blank/ไม่ map แสดง error ตำแหน่งถูก และ nested control map diagnostic กลับมาที่ control ต้นทางได้

- [ ] **20.6 Advanced value, CASE, predicate และ output branch editor**
  - ทำ: ขยาย `ValueExpressionEditor` ให้รองรับ Metadata, Trim, Substring, Convert, Coalesce, Default if blank และ CASE; เพิ่ม typed comparison operands, common `sourceWhen`, AND/OR condition builder, output tabs/add/clone และ `emitWhen`; จำกัด depth/node/arms ตาม capabilities
  - เข้าใจ: nested expression ใช้ contract เดียวกับ backend ไม่สร้าง payload รูปแบบเฉพาะ UI; field CASE กับ output filter เป็นคนละระดับ และ PMIB เปลี่ยน default `main` เป็น `pmx` ก่อน clone เป็น `ctp` เพื่อแทน UNION
  - ตรวจ: สร้าง `convert(trim(source))`, metadata, substring, coalesce/default-if-blank และตัวอย่าง CASE 1–2 ผ่าน UI; สร้าง output branches `pmx`/`ctp` ตามตัวอย่าง 5 และ preview PMIB-02 ให้ `CLOANNUMBER` เดียวออกสอง branches ตาม sample data; diagnostics ของ nested expression ชี้ control ถูก

- [ ] **20.7 Named transform, lookup และ preview UI**
  - ทำ: เพิ่ม Named transform และ arguments ลง `ValueExpressionEditor`, dropdown lookup จาก capabilities, dependency summary และ preview panel ที่แสดง outputs/filtered/errors; JSON แสดง read-only
  - เข้าใจ: ผู้ใช้เลือก logical capability โดยไม่เห็น credential, physical table หรือ SQL
  - ตรวจ: ตั้ง `formatRegNo@1` พร้อม nested arguments และ anti-lookup ตัวอย่าง 3–4 ได้, invalid argument ชี้ control ถูก และไม่มีช่อง execute SQL

- [ ] **20.8 ตรวจรับครบเส้นทางผ่านหน้าเว็บ**
  - ทำ: reset demo, สร้าง tables/config/version จาก UI, preview coverage-driven Transformation/Fixed-width drafts, activate, รัน Worker และ reprocess กรณี branch เปลี่ยน
  - ตรวจ: สร้าง Source/Metadata/Literal/Current date, Trim/Substring/Convert, Coalesce/Default if blank, CASE และ Named transform ผ่าน UI ได้โดยไม่แก้ JSON; pasted text และ valid UTF-8 file preview ผ่าน ส่วน invalid/oversized/51-record sample แสดง error โดยไม่ส่งหรือไม่ truncate; preview ไม่เพิ่ม DB rows/files; CSV เดิมและ compact fixed-width draft ผ่าน, coverage matrix ครบ, `CLOANNUMBER` เดียวได้ `pmx`/`ctp`, reprocess `2→1→0` outputs ลบ stale/all outputs ถูกต้อง, `GET /file-jobs/{id}` คืน Done/Filtered/Invalid/Failed counts และ aggregate status ตรง matrix, retry determinism และ errors ตรง golden cases

- [ ] **20.9 อัปเดตเอกสารและ commit handoff**
  - ทำ: อัปเดต CONTEXT ด้วย Input Layout/Fixed-width Slice/Transformation Program/Output Branch/Named Transform/Named Lookup, README วิธีรัน backend+web พร้อมคำเตือน breaking reset/rollback และติ๊ก Phase 20 ใน learning-plan; สรุปงานให้ผู้ใช้
  - ตรวจ: คนที่ไม่เคยเห็น repo ทำตาม README ตั้งแต่หยุด process → reset → configure แล้วสร้าง/preview/activate Config Version ใหม่ได้ และไม่พบคำแนะนำให้ใช้ legacy payload
  - Commit: ให้ผู้ใช้สั่ง commit แยกต่างหาก; AI agent ห้ามสร้าง commit เอง
