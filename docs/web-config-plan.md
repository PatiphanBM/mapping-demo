# Mapping Demo — แผน Input Layout, Transformation Config และหน้าเว็บ (Phase 19)

แผนนี้ต่อจาก [learning-plan.md](learning-plan.md) โดยขยาย Mapping Config จาก CSV field mapping แบบ 1:1 ให้รองรับ fixed-width text จาก `Mapping-TIB-PRD.xlsx`, transformation ที่พบใน `Mapping-PMIB-PRD.xlsx` และหน้าเว็บตั้งค่า Config Version ผ่าน `MappingDemo.Api`

คำศัพท์เดิมยึดตาม [CONTEXT.md](../CONTEXT.md) การตัดสินใจเรื่องหน้าเว็บอยู่ใน [demo-plan.md](demo-plan.md) หัวข้อ "ข้อสรุปรอบที่ 7", ทิศทาง Transformation Program อยู่ในหัวข้อ "ข้อสรุปรอบที่ 8" และ fixed-width Input Layout อยู่ในหัวข้อ "ข้อสรุปรอบที่ 9"

---

## วิธีใช้แผนนี้

ใช้กติกาเดียวกับ [learning-plan.md — วิธีใช้แผนนี้](learning-plan.md#วิธีใช้แผนนี้) ทุกข้อ ทั้งการทำทีละ step รูปแบบ **ทำ / เข้าใจ / ตรวจ** และ **AI agent ห้ามสร้าง commit เอง** ส่วนที่เพิ่มสำหรับแผนนี้มีดังนี้

1. ทำ backend foundation และ golden tests ใน 19.0–19.14 ให้ผ่านก่อนเริ่มหน้าเว็บใน 19.15
2. ห้ามติดตั้ง npm package นอกเหนือจากที่ `create-next-app` ใส่มาให้โดยไม่ถามก่อน
3. โค้ดเว็บอยู่ใน `web/`; การแก้ .NET, migration และ script ทำได้เฉพาะ step ที่ระบุ
4. ถ้า Next.js เวอร์ชันที่ติดตั้งมีชื่อไฟล์หรือ API ต่างจากแผน ให้ใช้ตามเวอร์ชันที่ติดตั้ง แล้วบอกว่าต่างตรงไหน
5. ห้ามแปลง pseudo-SQL จาก workbook แล้ว execute โดยตรง ทุก config ต้องผ่าน typed model, compiler และ allowlist เดียวกัน

### คำสั่งตั้งต้นสำหรับ AI agent (copy ไปใช้ได้)

```text
อ่าน docs/web-config-plan.md, docs/learning-plan.md (เฉพาะหัวข้อ "วิธีใช้แผนนี้"),
docs/demo-plan.md หัวข้อ "ข้อสรุปรอบที่ 7–9", CONTEXT.md และ current-program-workflow.md หัวข้อ 6.2 กับ 8.2
ก่อน implement ให้ตรวจเครื่องตาม "Requirement ของเครื่อง" ในแผนนี้ ถ้าไม่ตรงให้หยุดและบอกฉันก่อน
ถ้าฉันสั่งเริ่ม Phase 19 ให้ทำเฉพาะ step แรกที่ยังไม่เสร็จ แล้วหยุดรอคำสั่งใหม่
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

| ตัวอย่าง | ความสามารถที่แท้จริง | รูปแบบใหม่ |
|---|---|---|
| `CASE WHEN GarageType ...` | คำนวณ target field จากเงื่อนไข | `case` expression ที่ใช้ `eq` และ literal |
| `CASE WHEN cTotalPremium ... WHEN VehicleCode ...` | CASE ที่อ่านหลาย Source fields และใช้ first-match-wins | `case` expression; แต่ละ arm อ้าง field คนละตัวได้ |
| `(SELECT dbo.FormatRegNo(RegisterId))` | named transformation; syntax นี้ไม่ได้เป็น generic table lookup โดยตรง แม้ implementation ของ function อาจอ่าน DB | `formatRegNo@1` จาก server-owned function catalog |
| `ApplicationID NOT IN (SELECT ... MTI_ORIGINAL ...)` | เงื่อนไขคัดทั้ง output และ cross-table anti-lookup | output `emitWhen` + named lookup `mtiOriginalPolicyExists@1` |
| `MAPPING` + `UNION` ใน PMIB-02 | Source row เดียวสร้าง primary/CTP ได้มากกว่าหนึ่งผลลัพธ์ | output branches ที่มี stable `outputKey` และคืนผล `0..N` rows |

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

Transformation draft ใช้จำนวน Source rows ต่ำสุดที่ครอบคลุม direct/default, null/blank, CASE ทุก `WHEN` และ `ELSE`, ลำดับ premium ก่อน Vehicle fallback, LIKE/SUBSTRING/CONVERT, current date, `formatRegNo@1`, lookup exists/not exists, common `sourceWhen`, output filter และผล `0/1/2` outputs รวมทั้ง reprocess จาก 2 เหลือ 1 output ไม่เพิ่ม rows เพียงเพื่อให้จำนวนเท่าตัวอย่างใน workbook

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

Implementation ควรอยู่ใน shared project เช่น `MappingDemo.Input` ไม่อยู่ใน `MappingDemo.Api` เพียงแห่งเดียว: API reference เพื่อ validate/preview และ Worker reference เพื่อ parse runtime ส่วน Module รับ `Stream` จาก caller จึงไม่รู้ path, archive, job หรือ database

Config Version เก็บ `inputLayout` แบบ discriminated union:

```text
InputLayoutDefinition
├─ Delimited
│  ├─ encoding / delimiter / header policy
│  └─ fields[]: inputHeader → sourceColumn
└─ FixedWidth
   ├─ encoding / positionUnit
   ├─ recordTypes[]
   │  ├─ key / discriminator / action / expectedLength
   │  └─ fields[]: sourceColumn / start / length
   └─ unmatchedRecordPolicy
```

TIB รุ่นแรกใช้ record types:

- `header`: slice `(1, 1) = "H"`, expected length 1,500, action `Ignore`
- `detail`: slice `(1, 1) = "D"`, expected length 3,761, action `Import`
- `trailer`: slice `(1, 1) = "T"`, expected length 1,500, action `Ignore`
- unmatched record เป็น File Import Error; ไม่เดาจากความยาวอย่างเดียว

Parser ต้อง decode stream ด้วย encoding ที่ Config Version pin ไว้ก่อน, ตัด newline ออก, classify record ก่อนตรวจ expected length แล้วจึง slice เฉพาะ `Import` record ทุก field เก็บ raw slice รวม padding ลง Source Table; trim/conversion เกิดใน Transformation Program เพื่อรักษาหลักฐานต้นทาง

Config validation ต้องตรวจ encoding/position unit จาก allowlist, start/length เป็นจำนวนบวก, end ไม่เกิน expected length, Source column มีจริงและไม่ซ้ำ และ discriminator ไม่กำกวม ส่วน gap/overlap เป็น warning เพราะบาง layout อาจตั้งใจอ่านช่วงเดียวกันมากกว่าหนึ่ง field

Error แยกเป็น:

- Config error: layout/field/path ไม่ถูกต้อง ทำให้ create/activate ไม่ผ่าน
- File-level error: decode ไม่ได้หรือมี record type ที่ไม่รู้จัก ทำให้ File Import Job ล้มเหลวโดยไม่ archive ว่าสำเร็จ
- Input Row Error: Detail record สั้น/ยาวผิดหรือ slice ไม่ได้ ระบุ physical line number และไม่สร้าง Source row ของ record นั้น

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

### Model ระดับ Config Version

```text
ConfigVersion
├─ inputLayout             Delimited หรือ FixedWidth
└─ transformationProgram
   ├─ languageVersion
   ├─ semanticsProfile
   ├─ sourceWhen           optional common BooleanExpression
   └─ outputs[]
      ├─ key               เช่น main, ctp
      ├─ emitWhen          optional BooleanExpression
      └─ fields[]
         ├─ normalizedColumn
         └─ value          ValueExpression
```

`ValueExpression` รุ่นแรก:

- `source` — อ่าน Source field
- `metadata` — อ่านค่าที่ระบบอนุญาต เช่น file name, row number, evaluation time
- `literal` — ค่าคงที่ที่ระบุ type ชัดเจน
- `coalesce` / `defaultIfBlank`
- `case` — first-match-wins และมี `else`
- `substring`
- `convert` — แปลง string เป็น decimal/integer/date/dateTime/guid/boolean
- `currentDate` — ใช้ evaluation time ที่ตรึงไว้ ไม่อ่าน wall clock ใหม่ทุก retry
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
  "semanticsProfile": "pmib-v1",
  "outputs": [
    {
      "key": "main",
      "fields": [
        {
          "normalizedColumn": "CWORKSHOP",
          "value": {
            "kind": "case",
            "cases": [
              {
                "when": { "kind": "eq", "field": "GarageType", "value": "อู่ในเครือ" },
                "then": { "kind": "literal", "value": "ซ่อมอู่" }
              },
              {
                "when": { "kind": "eq", "field": "GarageType", "value": "อู่ห้าง" },
                "then": { "kind": "literal", "value": "ซ่อมห้าง" }
              }
            ],
            "else": { "kind": "literal", "value": "" }
          }
        }
      ]
    }
  ]
}
```

JSON ข้างบนเป็นภาพประกอบของ contract; ชื่อ property สุดท้ายให้ยืนยันด้วย serialization tests ใน 19.5 ก่อนใช้เป็น public contract

### Invariants และ semantics

- `outputKey` ต้อง unique และ stable ข้าม Config Version; identity ของผลลัพธ์ใช้ key ไม่ใช้ตำแหน่งใน array
- Normalized column หนึ่งถูกกำหนดได้ครั้งเดียวต่อ output และ required columns ต้องมี assignment ครบทุก output ที่อาจ emit
- Expression อ้าง Source/metadata ได้ แต่ยังไม่ให้อ้าง target field อื่น จึงไม่เกิด dependency ตามลำดับ field
- CASE ใช้ first-match-wins; `emitWhen = false/null` หมายถึง skip output ไม่ใช่ Row Error
- `sourceWhen = false/null` หมายถึง Source row ถูกกรองก่อนทุก output และเป็น `Filtered`; output-specific `emitWhen` ประเมินหลังจากนั้น
- แยก `null` กับ empty string ให้ชัด: `coalesce` จัดการ null ส่วน `isBlank/defaultIfBlank` จัดการ null หรือข้อความว่างหลัง trim
- Source fields ยังคงเป็น raw text ตาม CONTEXT; การแปลง `COLUMN` จาก workbook ให้ย้ายมาเป็น expression/metadata ใน Transformation Program ไม่แก้ Source row ย้อนหลัง
- `currentDate` ใช้ `evaluation_at` ที่บันทึกกับ Row Normalization Job; retry ใช้ค่าเดิม ส่วน reprocess สร้าง evaluation time ใหม่
- ถ้า output ใดเกิด data error ให้การประเมิน Source row นั้นเป็น `Invalid` ทั้งชุดและไม่แก้ Current Normalized Result เดิม
- ถ้าไม่มี output ผ่าน filter ให้เป็น `Filtered` ไม่ใช่ `Invalid`
- Reprocess ที่สำเร็จต้อง reconcile output set ใน transaction เดียว: upsert output ปัจจุบันและลบ stale output ที่ไม่ emit แล้ว
- จำกัด AST depth, node count, CASE arms, output count และ lookup keys เพื่อกัน config ที่ใช้ทรัพยากรเกินขอบเขต
- Config Version pin logic แต่ lookup ใช้ live business state; บันทึก capability/version/evaluated-at เพื่อ audit และใช้ idempotency key เพื่อไม่ให้ output ของงานเดิม block retry ตัวเอง

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
| `mtiOriginalPolicyExists@1` | Local-substitutable หรือ remote-owned | Config เห็น logical capability เท่านั้น; production และ in-memory Adapter เป็นคนละ implementation |
| เวลา | In-process input | Worker ส่ง persisted `evaluation_at`; test ส่งค่าคงที่ |

Lookup ต้องรับ batch keys, deduplicate และ query แบบ parameterized ห้าม query ต่อ field หากอยู่ฐานเดียวกันควรมี index อย่างน้อยตาม access pattern `(SYSTEM_ID, CPRODUCTNAME, CLOANNUMBER)`

## ผลกระทบต่อ model ปัจจุบัน

- `FileToSourceRule(CsvHeader, SourceColumn)` เปลี่ยนเป็น `InputLayoutDefinition`; CSV เดิมเป็น `Delimited` และ TIB เป็น `FixedWidth`
- `CsvRecordReader`/`FileImportHandler` เรียก Input Record Parser Module เพื่อให้ทั้งสอง layout คืน Source row contract เดียวกัน
- `SourceToNormalizedRule(SourceColumn, NormalizedColumn, Format)` เปลี่ยนเป็น Transformation Program; direct mapping เป็น expression ชนิด `source`
- `RowNormalizer` ที่คืน dictionary เดียวเปลี่ยนเป็น compiler/evaluator ที่คืน `0..N` outputs
- Normalized table เปลี่ยนจาก `UNIQUE(source_row_id)` เป็น `UNIQUE(source_row_id, output_key)`
- `NormalizedRowWriter` เปลี่ยนจาก upsert หนึ่งแถวเป็น atomic output-set reconciliation
- `RowNormalizeHandler` โหลด Source fields จาก compiled dependencies และ resolve lookup แบบ batch
- เพิ่ม status/จำนวนผลลัพธ์ให้แยก `Done`, `Filtered`, `Invalid`, `Failed`
- ขยายชนิด Normalized column สำหรับ workbook จริงเป็นอย่างน้อย `Integer`, `DateTime`, `Guid` และ metadata ของ Text/Decimal เช่น max length, precision/scale; Source columns ยังเก็บ raw text
- สำหรับ repo demo นี้ migration ของ generated tables ใช้ `reset-demo.ps1`; การ migrate production table ที่มีข้อมูลอยู่นอก scope

## ประสบการณ์บนหน้าเว็บ

หน้า editor แบ่งเป็น **Input Layout** และ **Transformation** โดยใช้ progressive disclosure:

1. Input Layout เลือก `Delimited` หรือ `Fixed width`; แบบเดิมยังเลือก CSV header → Source column ได้ง่าย
2. Fixed width กำหนด encoding, position unit, H/D/T record rule, expected length และตาราง Source column/start/length/end
3. วาง sample line หรือเลือก sample file เพื่อ preview raw slice/trimmed display และ highlight ช่วงที่เลือก โดย browser ไม่แก้ไฟล์ต้นฉบับ
4. Transformation มี common `Source when` builder สำหรับกฎแบบ TIB `ROWNUM/QUERY`, tabs ของ output branch เช่น `Main`, `CTP` และปุ่ม clone/add output
5. แต่ละ output มี visual `Emit when` builder สำหรับ AND/OR และ predicate; lookup เลือกจาก catalog ไม่พิมพ์ query
6. หนึ่งแถวต่อ Normalized column มี Value kind:
   - Direct field
   - Constant
   - Current date
   - CASE
   - Named transform
7. CASE เพิ่ม WHEN/THEN ได้ โดย operand เลือก Source field, metadata หรือ literal
8. แสดง dependency summary ว่าใช้ fields/functions/lookups ใด และแสดง warning เรื่อง index/complexity
9. End-to-end preview แสดง decoded record → Source fields → output branches/filtered/errors โดยไม่เขียน DB
10. JSON preview เป็น read-only; ไม่มี raw SQL textarea

## เป้าหมายและขอบเขต

**อยู่ใน scope**

- Versioned Input Layout สำหรับ Delimited/CSV และ UTF-8 fixed-width text
- H/D/T record classification, expected record length, 1-based UTF-16 code-unit slice และ raw padding preservation
- Input Layout compile/validation และ end-to-end preview จาก raw record ไป Source row
- Typed/versioned Transformation Program และ compiler/evaluator ชุดเดียวสำหรับ API/Worker
- direct, literal, current date, CASE, multi-field predicate, coalesce/blank, LIKE, IN, substring และ typed conversion
- named `formatRegNo@1` และ named anti-lookup สำหรับตัวอย่าง PMIB
- output filter และหลาย output branches ต่อ Source row
- validation ตอน create/activate, capabilities endpoint และ preview แบบไม่เขียน DB
- หน้าเว็บดู/สร้าง Config, clone/activate version และ visual transformation editor
- golden tests แบบ coverage-driven ครอบคลุม behavior จากตัวอย่างทั้งสี่ข้อและ primary + CTP output โดยไม่คัดลอกจำนวน rows จาก workbook
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
| Node.js | 19.15 | |
| npm | 19.15 | |
| Next.js | 19.16 | |
| React | 19.16 | |
| TypeScript | 19.16 | |
| Tailwind CSS | 19.16 | |

## Endpoint ที่หน้าเว็บใช้

| งาน | Endpoint |
|---|---|
| รายการ/สร้าง Config | `GET /mapping-configs`, `GET /tables`, `POST /mapping-configs` |
| รายละเอียด/activate | `GET /mapping-configs/{id}`, `GET /tables/{id}`, `POST /mapping-configs/{id}/versions/{n}/activate` |
| Capability catalog | `GET /mapping-capabilities` — input formats/encodings/position units และ transformation operators/functions/lookups |
| Validate draft | `POST /mapping-configs/{id}/versions/validate` |
| Preview draft | `POST /mapping-configs/{id}/versions/preview` |
| สร้าง Version | `POST /mapping-configs/{id}/versions` |

Validate และ preview ใช้ Input Layout/Transformation compilers ตัวเดียวกับ create/activate; preview รับอย่างใดอย่างหนึ่งระหว่าง raw record/sample file กับ sample Source row แล้วคืน decoded record, extracted Source fields และ transformation outputs โดยห้ามเขียน Source/Normalized/Job tables

---

## Phase 19 — Transformation Config และหน้าเว็บ

- [ ] **19.0 ตรึง semantics และสร้าง coverage-driven golden cases**
  - ทำ: ใช้ PMIB-01/02 และ TIB-01 เป็น requirement source แล้วสร้าง minimal synthetic fixtures ตาม coverage matrix; Transformation ครอบคลุม direct/default, blank/null, CASE ทุก branch, LIKE/SUBSTRING/CONVERT, current date, `FormatRegNo`, anti-lookup และ primary + CTP ส่วน Fixed-width ใช้ `H/D/D/T` กับ fields ตัวแทนต้น/กลาง/ท้าย, ภาษาไทย, padding และ date โดยไม่ใช้ workbook เป็น runtime input
  - เข้าใจ: fixture วัด behavior ไม่วัดว่าคัดลอก workbook ได้ครบจำนวน; SQL เดิมยังต้องตรึง empty/null, LIKE collation และ decimal comparison ส่วน fixed-width ต้องตรึง encoding, position unit, newline, padding, short/long-record policy และความหมายของ `VALUEFORMAT=dd/MM/yyyy` เมื่อ raw เป็น `yyyyMMdd`
  - ตรวจ: coverage matrix ทุกช่องชี้ไปยัง fixture อย่างน้อยหนึ่งข้อและไม่มี fixture ที่มีไว้เพียงให้จำนวนเท่า workbook; positive fixed-width fixture มี Import 2/Ignore 2 และ byte length ของ Detail ต่างกันแต่ fields ต้น/กลาง/ท้ายไม่เลื่อน; negative fixture คืน error class ตรงกรณี

- [ ] **19.1 สร้าง typed Input Layout contract และ compiler**
  - ทำ: เปลี่ยน `FileToSourceRule` เป็น discriminated `DelimitedInputLayout`/`FixedWidthInputLayout`; เพิ่ม encoding, position unit, record discriminator/action/expected length, field slice และ exact-path diagnostics
  - เข้าใจ: Input Layout เป็น Interface ของ File-to-Source parsing และเป็นส่วนหนึ่งของ Config Version; `slice` ไม่ใช่ transformation `substring`
  - ตรวจ: serialization round-trip ของ CSV เดิมและ compact fixed-width draft ผ่าน; compiler ปฏิเสธ unknown encoding/unit, start/length ไม่บวก, end เกิน record, target column ไม่มี/ซ้ำ และ discriminator กำกวม

- [ ] **19.2 ทำ Fixed-width Input Record Parser**
  - ทำ: parser decode UTF-8 ก่อนนับตำแหน่ง, ตัด newline, classify H/D/T, ตรวจ length เฉพาะ record type แล้ว slice ด้วย 1-based UTF-16 code units; คืน raw field values กับ physical line number และไม่ trim ก่อนเขียน Source
  - เข้าใจ: Detail มี 3,761 text positions แต่ 3,925–4,113 bytes จึงห้ามใช้ byte offset; parser เป็น in-process pure logic หลังรับ decoded text และทดสอบผ่าน Interface ของ Module
  - ตรวจ: compact `H/D/D/T` fixture ได้ Import 2/Ignore 2, fields ต้น/กลาง/ท้ายตรง golden, Detail ที่มีภาษาไทยและ byte length ต่างกันไม่ทำให้ตำแหน่งเลื่อน, invalid UTF-8/non-BMP/unknown type/short/long Detail คืน error class ที่กำหนด

- [ ] **19.3 เชื่อม Input Record Parser เข้า File Import Worker**
  - ทำ: `FileImportHandler` เลือก compiled layout ตาม Config Version, ใช้ parser เดียวสำหรับ Delimited/FixedWidth, เขียนเฉพาะ Import records และเก็บ physical line number; ปรับ duplicate/snapshot/archive/status/error counts โดยไม่ทำ regression CSV
  - เข้าใจ: Header/Trailer ที่ Ignore ไม่ใช่ Source row และไม่เพิ่ม Row Number; file decode error ต้องไม่ถูก archive ว่าสำเร็จ
  - ตรวจ: CSV acceptance เดิมผ่าน, compact fixed-width fixture สร้าง 2 Source rows โดย Row Number 1–2 อ้าง physical Detail lines ถูกต้อง และ error ก่อน import ไม่ทิ้ง partial success ที่รายงานผิด

- [ ] **19.4 ขยาย Table Definition และ result identity**
  - ทำ: เพิ่ม `Integer`, `DateTime`, `Guid` และ metadata ที่จำเป็นสำหรับ length/precision/scale; เพิ่ม `output_key` ใน Normalized table และ unique `(source_row_id, output_key)`; ปรับ DDL/tests และกำหนดว่า demo ต้อง reset DB
  - เข้าใจ: workbook ใช้ type มากกว่า demo และ `UNION` ทำให้ `source_row_id` อย่างเดียวไม่เป็น identity ที่ถูกต้อง
  - ตรวจ: DDL tests ครอบคลุม type ใหม่และหนึ่ง source row insert output key ต่างกันได้สองแถว แต่ key เดิมซ้ำไม่ได้

- [ ] **19.5 สร้าง typed Transformation Program contract**
  - ทำ: สร้าง model ของ program/output/value/boolean expression, `languageVersion`, `semanticsProfile` และ JSON polymorphism แบบ explicit discriminator; เพิ่ม serialization round-trip tests
  - เข้าใจ: AST เป็น declarative data ไม่ใช่ executable code; direct mapping เป็น `source` expression ไม่ต้องมี pipeline พิเศษอีกชุด
  - ตรวจ: serialize/deserialize ตัวอย่างทั้งสี่ข้อกลับมาเท่ากัน และ unknown `kind` ถูกปฏิเสธด้วย path ที่ชัดเจน

- [ ] **19.6 Compiler และ static validation**
  - ทำ: compile program กับ Source/Normalized schemas, infer type/nullability, หา dependencies, validate required/duplicate targets/function/lookup/version และ enforce complexity limits; คืน diagnostic code + exact path
  - เข้าใจ: create, activate, preview และ runtime ต้องใช้ compiler เดียวกัน; compiled program cache ด้วย Config Version/hash
  - ตรวจ: unit tests ครอบคลุม unknown field, type mismatch, required หาย, duplicate output/target, AST ลึกเกิน และ path ตรง payload

- [ ] **19.7 Evaluator สำหรับ pure expressions**
  - ทำ: รองรับ source/metadata/literal, coalesce/defaultIfBlank, CASE, comparison, blank, AND/OR/NOT, LIKE, IN, substring และ convert โดยกำหนด culture/null semantics ตาม profile; ประเมิน common `sourceWhen` ก่อน output `emitWhen`
  - เข้าใจ: evaluator คืนผลและ Row Errors ไม่เขียน DB; CASE short-circuit แบบ first-match-wins
  - ตรวจ: golden tests ของตัวอย่าง 1–2 และ operator ใน workbook ผ่านโดยไม่สร้าง SQL

- [ ] **19.8 Current time และ named transform**
  - ทำ: เพิ่ม metadata/evaluation time และ function catalog ที่ pin version; implement `formatRegNo@1` เป็น pure C# จาก behavior ที่ยืนยันได้ หรือใช้ typed legacy Adapter ชั่วคราวถ้าต้องพึ่ง DB
  - เข้าใจ: config ระบุชื่อ logical capability เท่านั้น; `SELECT dbo...` ไม่อยู่ใน config และ retry ต้องได้ current date ค่าเดิม
  - ตรวจ: parity tests ของ `FormatRegNo` ผ่าน, unknown/version ผิด compile ไม่ผ่าน และ retry ด้วย `evaluation_at` เดิมให้ผลเดิม

- [ ] **19.9 Named lookup และ batch resolution**
  - ทำ: เพิ่ม lookup catalog/port สำหรับ `mtiOriginalPolicyExists@1`, production Adapter แบบ parameterized และ in-memory Adapter สำหรับ tests; deduplicate/batch keys และตรวจ index/access pattern ตอน activate
  - เข้าใจ: lookup เป็น dependency ที่เปลี่ยนได้ ไม่ใช่ SQL expression; timeout เป็น system error ที่ retry ได้ ไม่ใช่ Row Error
  - ตรวจ: golden anti-lookup ผ่าน, SQL injection input ไม่เปลี่ยนโครง query, จำนวน query ไม่โตตามจำนวน field และ integration test ใช้ composite index ที่กำหนด

- [ ] **19.10 รองรับ `0..N` outputs และ atomic reconcile**
  - ทำ: evaluator คืนหลาย output พร้อม `outputKey`; writer upsert current outputs และลบ stale outputs ใน transaction เดียว; เพิ่ม `Filtered` และ output counts โดยไม่ทับ Current Normalized Result เมื่อ Invalid/Failed
  - เข้าใจ: legacy `UNION` คือ output branch เพิ่ม ไม่ใช่ string operator; reprocess อาจทำให้ branch เดิมหายจึงต้อง reconcile ทั้งชุด
  - ตรวจ: Source row เดียวได้ทั้ง `main`/`ctp`, filter ทั้งหมดได้ `Filtered`, reprocess ลบ stale CTP และ failure กลาง transaction ไม่ทิ้งผลครึ่งชุด

- [ ] **19.11 เชื่อม Transformation Module เข้า Worker**
  - ทำ: `RowNormalizeHandler` compile/cache ตาม Config Version, โหลด Source fields จาก dependencies, resolve lookup เป็น batch, evaluate และส่งผลให้ writer; ปรับ retry/reprocess/idempotency ให้รองรับหลาย output
  - เข้าใจ: Kafka ยังเป็น orchestration ภายนอก Module; business semantics อยู่หลัง seam เดียว
  - ตรวจ: tests เดิมของ direct mapping ยังผ่าน และ end-to-end PMIB fixture ให้ status/count/output ตรง expected

- [ ] **19.12 ปรับ persistence และ API contract ของ Config Version**
  - ทำ: เพิ่ม `input_layout jsonb` และ `transformation_program jsonb`, update request/response/service และ `configure-demo.ps1`; เพิ่ม compatibility migration/adapter สำหรับ `FileToSourceRule`/`SourceToNormalizedRule` เดิมเฉพาะที่จำเป็น แล้วกำหนดวันถอดออก
  - เข้าใจ: Config Version ยัง immutable; repo demo ใช้ reset ได้แต่ต้องไม่ปล่อยให้มี evaluator สองชุดระยะยาว
  - ตรวจ: create/clone/get version round-trip program ครบ, version เก่าอ่านได้ตาม policy และ script demo ผ่าน

- [ ] **19.13 เพิ่ม capabilities, validate และ preview endpoints**
  - ทำ: เพิ่มสาม endpoint ตามตาราง, ใช้ Input Layout/Transformation compiler และ evaluator ชุดเดียวกัน, preview รับ raw record/sample Source row + evaluation time และห้ามเขียน DB; API ส่ง enum เป็น string ด้วย `JsonStringEnumConverter`
  - เข้าใจ: capability catalog ทำให้ web ไม่ hard-code input format/encoding/position unit/operator/function/lookup ที่ backend ไม่รองรับ
  - ตรวจ: invalid draft คืน exact paths, raw TIB preview แสดง Source fields แล้วต่อ CASE/lookup ได้, จำนวน row ใน DB ไม่เปลี่ยน และ API enum เป็นชื่อ

- [ ] **19.14 ตรวจรับ backend ด้วย coverage-driven fixtures**
  - ทำ: สร้าง Config Version ขนาดเล็กที่ครอบคลุม semantics จาก PMIB ทั้งสี่ข้อ, สอง output branches และ fixed-width layout + transformations; รัน golden/unit/integration/end-to-end tests โดยใช้จำนวน rows/fields ต่ำสุดตาม coverage matrix
  - เข้าใจ: ผ่าน step นี้ก่อนจึงถือว่า backend พร้อมให้หน้าเว็บตั้งค่าได้จริง
  - ตรวจ: direct CSV ไม่ regression, fixed-width ได้ Import 2/Ignore 2 และ fields ตัวแทนตรงตำแหน่ง, CASE ทุก branch ตรง expected, `FormatRegNo` parity ผ่าน, lookup exists/not exists ถูกต้อง และผล `0/1/2` outputs แยก identity ถูกต้อง

- [ ] **19.15 ตรวจเครื่องสำหรับเว็บ**
  - ทำ: ตรวจ Node/npm/network/port 3000 และกรอกเวอร์ชัน; ยืนยัน API health และ backend 19.0–19.14 ผ่าน
  - ตรวจ: `node -v`, `npm -v`, `npm ping`, port 3000 ว่าง และ `GET http://localhost:5181/health` ได้ 200

- [ ] **19.16 สร้างโปรเจกต์ `web/`**
  - ทำ: ใช้ `create-next-app` เลือก TypeScript, ESLint, Tailwind, App Router และไม่ใช้ `src/`; กรอกเวอร์ชันจริง
  - ตรวจ: `npm run dev` เปิดหน้าเริ่มต้นได้และ `git status` ไม่เห็น `node_modules`/`.next`

- [ ] **19.17 Proxy และ API client**
  - ทำ: rewrite `/api/:path*` ไป `API_BASE_URL`; สร้าง `.env.local.example`, contract types, fetch wrapper และ `ApiError` ที่เก็บ `status/title/detail/errors`
  - เข้าใจ: browser ใช้ origin เดียว; API client ไม่ผูก React เพื่อย้ายแนวคิดไป Angular service ได้
  - ตรวจ: `/api/health` ผ่านและ `npm run build` ผ่าน type check

- [ ] **19.18 Layout, list, create และ details**
  - ทำ: สร้าง `/configs`, `/configs/new`, `/configs/[id]`; แสดง config/version/output/rule counts, clone และ activate; map ValidationProblem/409 ตาม field path
  - ตรวจ: สร้าง config, ดู versions, clone และ activate ได้; API ปิดอยู่แล้ว UI แสดง error ไม่ค้างว่าง

- [ ] **19.19 Input Layout editor และ raw-record preview**
  - ทำ: เพิ่ม Delimited/Fixed-width selector; fixed-width form มี encoding, position unit, H/D/T rules, expected length และ field grid Source column/start/length/end; รับ sample line/file เพื่อแสดง raw slices, trimmed display, gap/overlap warnings และ physical line errors
  - เข้าใจ: UI ส่งตำแหน่ง 1-based แต่ไม่ตัดข้อความเองเป็น source of truth; preview endpoint เป็นผู้ใช้ parser implementation เดียวกับ Worker
  - ตรวจ: กรอกหรือ clone compact fixed-width draft แล้ว preview `H/D/D/T` ได้ Import 2/Ignore 2, fields ต้น/กลาง/ท้ายและภาษาไทยไม่เลื่อน, padding แสดงทั้ง raw/trimmed และ header/trailer แสดง Ignore โดยไม่ต้องกรอก 115 fields

- [ ] **19.20 Basic Transformation editor**
  - ทำ: editor มี output `main` เริ่มต้นและหนึ่งแถวต่อ Normalized column; รองรับ Direct field, Constant, Current date และไม่ map; Date/Decimal แสดง option ตาม capability
  - เข้าใจ: payload-index-to-control map ต้องคง error path ถูกแม้ซ่อน/reorder fields
  - ตรวจ: สร้าง direct mapping เทียบ demo เดิม, constant/current date preview ถูก และ required ที่ไม่ map แสดง error ตำแหน่งถูก

- [ ] **19.21 CASE, predicate และ output branch editor**
  - ทำ: เพิ่ม visual CASE builder, typed operands, common `sourceWhen`, AND/OR condition builder, output tabs/add/clone และ `emitWhen`; จำกัด depth/arms ตาม capabilities
  - เข้าใจ: field CASE กับ output filter เป็นคนละระดับ และ clone `main` เป็น `ctp` คือรูปแบบแทน UNION
  - ตรวจ: ตั้งค่าตัวอย่าง 1–2 ผ่าน UI และ preview PMIB-02 ออก main/ctp ตาม sample data

- [ ] **19.22 Named transform, lookup และ preview UI**
  - ทำ: dropdown named transform/lookup จาก capabilities, argument controls, dependency summary และ preview panel ที่แสดง outputs/filtered/errors; JSON แสดง read-only
  - เข้าใจ: ผู้ใช้เลือก logical capability โดยไม่เห็น credential, physical table หรือ SQL
  - ตรวจ: ตั้ง `formatRegNo@1` และ anti-lookup ตัวอย่าง 3–4 ได้, invalid argument ชี้ control ถูก และไม่มีช่อง execute SQL

- [ ] **19.23 ตรวจรับครบเส้นทางผ่านหน้าเว็บ**
  - ทำ: reset demo, สร้าง tables/config/version จาก UI, preview coverage-driven Transformation/Fixed-width drafts, activate, รัน Worker และ reprocess กรณี branch เปลี่ยน
  - ตรวจ: CSV เดิมและ compact fixed-width draft ผ่าน, coverage matrix ครบ, ผล `0/1/2` outputs, counts/status, retry determinism, stale-output cleanup และ errors ตรง golden cases

- [ ] **19.24 อัปเดตเอกสารและ commit handoff**
  - ทำ: อัปเดต CONTEXT ด้วย Input Layout/Fixed-width Slice/Transformation Program/Output Branch/Named Transform/Named Lookup, README วิธีรัน backend+web และติ๊ก Phase 19 ใน learning-plan; สรุปงานให้ผู้ใช้
  - ตรวจ: คนที่ไม่เคยเห็น repo ทำตาม README แล้วสร้าง/preview/activate Config Version ได้
  - Commit: ให้ผู้ใช้สั่ง commit แยกต่างหาก; AI agent ห้ามสร้าง commit เอง
