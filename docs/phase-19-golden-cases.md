# Phase 19.0 — Semantics และ Golden Cases

เอกสารนี้ตรึง observable behavior ก่อนสร้าง Input Layout และ Transformation Program ใน Phase 19 โดยใช้ `Mapping-PMIB-PRD.xlsx`, `Mapping-TIB-PRD.xlsx` และ `isoMTI-N7780.txt` เป็น requirement source แต่ใช้ข้อมูลสังเคราะห์ขนาดเล็กเป็น regression fixtures

## Test seams

- Input cases ต้องทดสอบผ่าน public `InputRecordModule` ด้วย `Stream` และ compiled layout เท่านั้น ห้ามอ้าง compiler/reader ภายใน
- Transformation cases ต้องทดสอบผ่าน public `TransformationModule` ด้วย schema, Source rows, evaluation instant และ dependency ports เท่านั้น ห้ามอ้าง evaluator/helper ภายใน
- PostgreSQL lookup, legacy dependency และเวลาเป็น system boundaries; unit tests ใช้ deterministic adapters/inputs ส่วน integration tests ใช้ adapter จริง
- ไฟล์ใน `tests/MappingDemo.Tests/Fixtures/Phase19` เป็น behavior fixtures ไม่ใช่ persisted Config Version contract รูป JSON ของ AST จะถูกตรึงใน 19.6

## Semantics ที่ตรึงแล้ว

### `sqlserver-legacy-v1`

- `null` ต่างจาก empty string และ blank text; comparison กับ `null` ได้ `unknown` (`null`) ตาม truth table ใน fixture
- predicate ผ่านเฉพาะ `true`; `false` และ `null` ไม่ผ่าน
- text comparison และ `LIKE` เป็น ordinal case-insensitive แบบ deterministic; `LIKE` รองรับ `%`, `_`, character set, range และ negated character set
- decimal ใช้ invariant culture, รับ `+`/`-` และจุดทศนิยม แต่ไม่รับ comma คั่นหลัก
- Date/DateTime parse exact ด้วย invariant culture; default คือ `yyyy-MM-dd` และ `yyyy-MM-ddTHH:mm:ss`
- `currentDate` แปลง evaluation instant ด้วย `Asia/Bangkok`
- Source text คงค่า raw; Target Assignment Coercion จึงเป็นผู้ trim และ convert ตอน assign ส่วน explicit `trim`/`convert` ใช้กับ intermediate expression

Expected values ทั้งหมดอยู่ใน [sqlserver-legacy-v1.golden.json](../tests/MappingDemo.Tests/Fixtures/Phase19/sqlserver-legacy-v1.golden.json)

### Fixed-width transport และตำแหน่ง

- encoding รุ่นแรกคือ strict UTF-8; รับ UTF-8 BOM ได้หนึ่งตัวเฉพาะต้น stream และไม่นับ BOM เป็นตำแหน่งข้อความ
- รับทั้ง LF และ CRLF; line terminator ไม่เป็นส่วนของ record และ final line terminator หนึ่งชุดไม่สร้าง empty record เพิ่ม ส่วน empty line อื่นถือเป็น physical record และต้องไม่ผ่าน validation
- นับ `start` แบบ 1-based UTF-16 code unit หลัง decode; v1 ปฏิเสธ non-BMP ทั้งไฟล์ก่อน slice เพื่อไม่ให้แบ่ง surrogate pair
- classify และตรวจ sequence/cardinality/expected length ก่อน slice; ตรวจ length ของ rule ที่ `Ignore` ด้วย
- Source field เก็บ raw slice รวม padding; trim เป็นเพียงค่าช่วยแสดงผลและเกิดจริงใน Transformation Program
- `rowNumber` นับเฉพาะ record ที่ action เป็น `Import`; `physicalLineNumber` นับทุก physical record รวม `Ignore`
- slice date `20261006` ต้องเก็บ raw เป็น `20261006`; ค่า `VALUEFORMAT=dd/MM/yyyy` ใน workbook ไม่ใช่กติกา Input Layout สำหรับตัวอย่างนี้ Transformation ต้องใช้ verified `inputFormat=yyyyMMdd` และได้ typed Date `2026-10-06`

Positive, structural และ negative cases อยู่ใน [fixed-width.golden.json](../tests/MappingDemo.Tests/Fixtures/Phase19/fixed-width.golden.json) โดยใช้ stable codes เหล่านี้:

| Code | ความหมาย |
|---|---|
| `InvalidEncoding` | byte stream ไม่ใช่ strict UTF-8 |
| `UnexpectedByteOrderMark` | พบ BOM นอกต้น streamหรือมากกว่าหนึ่งตัว |
| `UnsupportedCharacterForPositionUnit` | พบ non-BMP ซึ่ง v1 ไม่รองรับ |
| `UnknownRecordType` | ไม่มี record rule ที่ discriminator ตรง |
| `UnexpectedRecordLength` | ความยาว UTF-16 ไม่ตรง rule |
| `UnexpectedRecordSequence` | record type มาในลำดับที่ sequence ไม่อนุญาต |
| `RecordCardinalityExceeded` | จำนวน record rule มากกว่า `maxOccurs` |
| `RecordCardinalityNotMet` | ถึง EOF แต่จำนวนน้อยกว่า `minOccurs` |

### `formatRegNo@1` demo stand-in

repo และ workbook ไม่มี source/expected output ของ `dbo.FormatRegNo` จึงห้ามเรียก fixture นี้ว่า legacy parity รุ่น `formatRegNo@1` ของ demo ตรึง behavior แบบไม่ทำลายข้อมูลดังนี้:

- รับ `Text` หรือ `null`
- `null` คืน `null`
- text ตัด whitespace เฉพาะหัวท้ายและรักษาอักขระ/whitespace ภายในทุกตัว
- ไม่มี database lookup และไม่อ่าน culture ของเครื่อง

หากได้ source จริงภายหลังและ behavior ต่างจากนี้ ต้องเพิ่ม capability รุ่นใหม่ เช่น `formatRegNo@2`; ห้ามเปลี่ยน `@1` เงียบ ๆ Golden case อยู่ที่ `TR-F01` ใน [transformation-program.golden.json](../tests/MappingDemo.Tests/Fixtures/Phase19/transformation-program.golden.json)

## Coverage matrix

| Requirement | Fixture cases |
|---|---|
| null/empty/blank และ three-valued logic | `SP-N01..N04`, `SP-B01..B06`, `TR-A01..A04` |
| ordinal case-insensitive `eq`/`in` | `SP-T01..T03` |
| `LIKE` `%`, `_`, set, range, negated set | `SP-L01..L07` |
| invariant decimal และ invalid comma | `SP-D01..D04`, `TR-A01`, `TR-A03` |
| exact Date/DateTime และ TIB raw date | `SP-DT01..DT05`, `TR-A01`, `FW-P01` |
| Bangkok current-date boundary | `SP-C01`, `SP-C02` |
| direct/default, implicit trim/conversion, explicit trim | `TR-A01..A04` |
| CASE ทุก `WHEN`/`ELSE` และ first-match-wins | `TR-C01..C05` |
| LIKE/SUBSTRING/CONVERT ใน transformation | `TR-E01..E02`, `TR-A01`, `TR-A03` |
| named `formatRegNo@1` | `TR-F01..F03` |
| anti-lookup not found/other/self/different product | `TR-L01..L04` |
| common `sourceWhen` และ output filter | `TR-O00..O03` |
| ผล `0/1/2` outputs | `TR-O01..O03` |
| reprocess `2→1→0` และ stable output key | `TR-R01` |
| H/D/D/T, Import 2/Ignore 2, raw padding | `FW-P01` |
| LF/CRLF, final terminator และ UTF-8 BOM | `FW-P01..P03` |
| UTF-16 positions กับ UTF-8 byte length ต่างกัน | `FW-P01` detail records 2–3 |
| multi-header/trailer sequence/cardinality | `FW-P04`, `FW-E07`, `FW-E08` |
| invalid UTF-8/non-BMP/unknown/short/long/order/embedded blank/BOM | `FW-E01..E06`, `FW-E09`, `FW-E10` |

ไม่มี fixture ใดเพิ่มเพื่อให้จำนวนเท่ากับ 115 fields หรือ 21 detail records ของ workbook; capacity 115 fields ให้สร้างจาก test builder ใน 19.2 แยกจาก human-readable fixtures เหล่านี้

## Source traceability

| Workbook evidence | Golden behavior |
|---|---|
| PMIB-01 `CWORKSHOP` CASE | `TR-C01..C03` |
| PMIB-01 `CVEHCODE` premium branches ก่อน VehicleCode | `TR-C01..C05` |
| PMIB-01/02 `dbo.FormatRegNo(RegisterId)` | demo stand-in `TR-F01..F03`; ไม่มีคำกล่าวอ้าง parity |
| PMIB-01/02 anti-query ต่อ `MTI_ORIGINAL` | `TR-L01..L04` |
| PMIB-02 `MAPPING` + `UNION` | `TR-O01..O03`, `TR-R01` |
| TIB-01 `ROWNUM/QUERY` | `TR-O00` common `sourceWhen` |
| TIB-01 SUBSTRING positions 1–3761 | compact representative layout `FW-P01`; capacity test builder ใน 19.2 |

