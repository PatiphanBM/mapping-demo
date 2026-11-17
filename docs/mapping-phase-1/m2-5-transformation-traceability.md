---
title: M2.5 — Transformation Program traceability
status: draft
last_updated: 2026-11-17
---

# M2.5 — Transformation Program traceability

เอกสารนี้เทียบ capability ของ demo กับ Mapping ตาม M-03 เพื่อใช้ตรวจการย้าย logic โดยไม่ยก `sqlserver-legacy-v1` มาเป็นกฎกลาง ค่า `mapping/1` เป็น semantics profile ของ Program MTLS ปัจจุบัน

## ผลปิด M2.5 วันที่ 2026-11-17

Named Lookup ทั้งสองรายการมี SQL Server adapter จริงแล้ว ตามคำยืนยันผู้ใช้ใน M-28 ใช้ parameterized SELECT กับ `NewValidateDB.dbo.MTI_ORIGINAL` โดยไม่เขียนข้อมูลบริษัท

| Lookup | เงื่อนไขพบข้อมูล | ผลต่อ identity |
|---|---|---|
| `mtiOriginalPolicyExists@1` | `SYSTEM_ID = systemId AND CPRODUCTNAME = productName AND CLOANNUMBER = loanNumber` | นับทุกแถวที่ตรง keys |
| `mtiOriginalLoanExists@1` | `SYSTEM_ID = systemId AND CLOANNUMBER = loanNumber` | นับทุกแถวที่ตรง keys โดยไม่กรอง product |

ตารางจริงไม่มี `source_row_id`; `ID` เป็น int identity และ `TransID` เป็น uniqueidentifier ส่วน primary key คือ `POLICYTYPE + CLOANNUMBER + SOURCE` จึงไม่ใช้ `sourceRowNo` หรือ UUID ของ Source ใหม่เทียบกับ ID ใดใน SQL Server และไม่เพิ่มเงื่อนไขสถานะ/`POLICYTYPE`/`SOURCE` เอง การเปรียบเทียบใช้ collation ของ SQL Server (`Thai_CI_AI_KS`) ไม่แปลงเป็น ordinal comparison ใน Mapping

ตัวอย่างสมมติ: ตารางอ้างอิงมี `(SYSTEM_ID="SYN", CPRODUCTNAME="แผนทดสอบ", CLOANNUMBER="L-001")` แถวไฟล์เลข 1 และไฟล์อื่นเลข 1 ที่มี keys เดียวกันได้ `exists=true` ทั้งคู่; product ต่างกันได้ policy `false` แต่ loan `true`; `notExists` กลับค่าผลพบข้อมูลเมื่อ dependency พร้อมเท่านั้น

### Runtime และการตั้งค่า

- `SqlServerTransformationLookupResolver` อยู่ Infrastructure; Application ใช้ `ITransformationLookupResolver` แบบ async พร้อม cancellation และไม่อ้าง SqlClient
- validate ตรวจ lookup dependencies ทุกตำแหน่งของ Program รวม branch ที่ยังไม่ถูกเลือกและ preview ที่ไม่มี records; raw/file preview ส่ง resolver ตัวเดียวกันเข้า evaluator และอ่าน SQL Server อย่างเดียว
- availability ตรวจ metadata/ชนิด/ความยาว columns กับสิทธิ์ SELECT แยก policy/loan และ cache ภายใน scope ของ request; request ใหม่ตรวจใหม่ได้ เมื่อ query ล้มเหลวหลังตรวจ availability แล้วต้องคืน diagnostic เช่นกัน
- connection/schema/permission ผิดได้ `LookupDependencyUnavailable`; `notExists` ไม่กลายเป็น `true` เมื่อ DB ใช้ไม่ได้; cancellation ถูกส่งต่อ ไม่แปลงเป็น diagnostic; response ไม่คืน connection string หรือ SQL exception
- `GET /api/mapping/capabilities` รายงานเฉพาะ lookup ที่ dependency พร้อม; ถ้ามีอย่างน้อยหนึ่ง lookup จะมี `exists`/`notExists` ใน predicates แต่ `namedLookups` ระบุแต่ละชื่อที่พร้อมจริง
- รับ connection จาก `ConnectionStrings__MtiOriginalDb`; timeout จาก `MtiOriginalLookup__CommandTimeoutSeconds` (default 10, รับ 1..60 วินาที); ไม่เก็บ credentials ใน repo
- UAT นี้ SqlClient ตรวจ certificate ไม่ผ่าน ผู้ใช้อนุญาต `Encrypt=True;TrustServerCertificate=True` เฉพาะ UAT นี้ และ smoke test ผ่านหลังตั้งค่า; adapter ยังคงใช้ TLS settings จาก connection string ไม่ใส่ bypass เป็น default; environment อื่นต้องจัด certificate/secret ตามการอนุมัติของ environment นั้น

### หลักฐาน

- `./scripts/check.ps1` ผ่าน build, unit **17/17**, integration **32/32** และ EF model check ด้วย .NET 10.0.404; ไม่เพิ่ม EF migration
- `SqlServerTransformationLookupResolverTests` **6 tests** ใช้ SQL Server 2022 Testcontainers และข้อมูลสมมติ: key sets/collation/Thai, key ยาวและ SQL injection text, ไม่ตัดแถวตามเลขแถวไฟล์, dependency แยกต่อ lookup/schema type/recovery, raw/file preview, invalid connection/connection refused, outage หลังตรวจ availability และ cancellation
- `PreviewServiceTests` เพิ่ม 4 tests: dependency ใน branch ที่ข้ามได้, failure หลัง validate, lookup ใน case arm/nested array และ cancellation; tests operator/MTLS full fixture เดิมผ่านทั้งหมด
- UAT read-only smoke: adapter ทั้งสองชื่อ `available=true`; query ด้วย keys สมมติได้ `false` ทั้งคู่; API ที่รันชั่วคราวคืน Named Lookup 2 รายการและ predicates **17 แบบ** แล้วหยุด process ทดสอบ ไม่มีการอ่านแถวข้อมูลจริงออกมาแสดงหรือเปลี่ยน DB บริษัท
- packages ที่เพิ่ม: `Microsoft.Data.SqlClient` 7.1.2 ใน Infrastructure และ `Testcontainers.MsSql` 3.10.0 ใน integration tests (รุ่นเดียวกับ Testcontainers เดิม)

**สถานะปัจจุบัน:** M2.5 ปิดแล้ว; operators/flow/Named Transform และ Named Lookup ครบตาม M-03; deploy ต้อง provision connection secret ของ environment นั้น ส่วน import/delivery runtime และ dependency อื่นของ Q7 ยังเป็นงานถัดไป

ข้อจำกัดที่ตรวจพบ: indexes เดิมมี `(ID)`, `(TransID)` และ PK `(POLICYTYPE, CLOANNUMBER, SOURCE)` ยังไม่มี index ที่เริ่มด้วย lookup keys; smoke ด้วย keys สมมติผ่าน แต่ยังไม่ใช่ load test กับปริมาณงานจริง หากต้องเพิ่ม index ให้เจ้าของ DB พิจารณาและรันเอง ไม่แก้ DB บริษัทในงานนี้

## ประวัติก่อนปิดงาน

| กลุ่ม | รายการใน demo | สถานะก่อน M2.5 |
|---|---|---|
| Value expression (13) | `source`, `metadata`, `literal`, `coalesce`, `add`, `concat`, `defaultIfBlank`, `trim`, `case`, `substring`, `convert`, `currentDate`, `call` | MTLS subset: `source`, `literal`, `defaultIfBlank`, `case`, `convert` |
| Predicate (17) | `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `isNull`, `isBlank`, `notBlank`, `like`, `in`, `notIn`, `and`, `or`, `not`, `exists`, `notExists` | เฉพาะ `isNotBlank` และ `equals` ใน Array item ของ MTLS |
| Conversion target (7) | `decimal`, `integer`, `date`, `time`, `dateTime`, `boolean`, `guid` | `decimal`, `integer`, `date`, `boolean`; มี `text` เป็น conversion เพิ่มของ Mapping |
| Flow | `sourceWhen`, `emitWhen`, 0..N outputs, nested object/array | MTLS: 1 output `default`, nested array item, fallback item; `sourceWhen`/output `emitWhen` ยังไม่มี |
| Named Transform | `formatRegNo@1` | ยังไม่มี |
| Named Lookup | `mtiOriginalPolicyExists@1`, `mtiOriginalLoanExists@1` | ยังไม่มี; ต้องรู้ adapter และแหล่งข้อมูลจาก Q2/Q7 |

## ผลชุด M2.5 วันที่ 2026-11-16

| กลุ่ม | ผลใน Mapping | หลักฐาน |
|---|---|---|
| Value expression | 13/13 มี compiler และ evaluator | `TransformationExtendedTests.ValueExpressions_ShouldEvaluateAllThirteenKinds` |
| Predicate | 17/17 มี compiler และ evaluator; `exists`/`notExists` fail closed เมื่อไม่มี resolver | `Predicates_ShouldEvaluateAllSeventeenKindsWithThreeValuedNulls`, `TransformationLookupDependencyTests` |
| Conversion target | 7/7; ค่าแปลงไม่ได้เก็บใน `inform.extras.unparsed` ตาม M2.2 | `Convert_ShouldSupportSevenTargetsAndKeepUnparsed` |
| Flow | `sourceWhen` filter; `emitWhen` ทำ 0..N outputs; nested object/array เดิมยังผ่าน MTLS full fixture | `OutputBranches_ShouldEmitZeroOrMultipleOutputs`, `MtlsFullFixtureContractTests` |
| Named Transform | `formatRegNo@1` trim ข้อความตาม demo | `ValueExpressions_ShouldEvaluateAllThirteenKinds` |
| Named Lookup | 2 names compile ได้, มี `ITransformationLookupResolver`; ไม่มี resolver จริงจึงได้ `LookupDependencyUnavailable`; capability ไม่ประกาศว่าใช้งานได้ | `TransformationLookupDependencyTests` ใช้ fake resolver เท่านั้น |
| Limits / deterministic | จำกัด 32 outputs, 32 expression depth, 2,048 nodes; `currentDate` ต้องรับเวลา evaluation ที่ตรึงไว้ | `Compiler_ShouldBoundProgramSizeAndRejectUnknownLookup`, `CurrentDate_ShouldRequirePinnedEvaluationTime` |

`GET /capabilities` ระบุ 13 expressions, **15 predicates ที่ไม่พึ่ง lookup**, 7 conversion targets และ `formatRegNo@1`; `exists`/`notExists` และ `namedLookups` ยังไม่ประกาศจนมี resolver และแหล่งข้อมูลจริง Input layouts ยัง `available=false` เพราะ import runtime ไม่เสร็จ แต่ reason ระบุว่า reader/preview ใช้ได้แล้ว

**สถานะ ณ 2026-11-16:** M2.5 ยังไม่ปิด เพราะ Named Lookup 2 รายการมีเพียง contract/fake test; ข้อนี้ปิดแล้วด้วย SQL Server adapter และผลตรวจวันที่ 2026-11-17 ข้างบน

## ตัวอย่างข้อมูลสมมติ

Source `{"reference":" A-001 ","transaction":"P"}` และ Program ที่ใช้ `trim(source.reference)` ได้ค่า `"A-001"` ส่วน `sourceWhen` แบบ `eq(source.transaction, literal("P"))` ให้แถวนี้ผ่าน filter และ output `default` เท่านั้นตาม config MTLS ที่มีอยู่

Named Lookup สองรายการต้องตรวจ dependency ได้ก่อนใช้งานจริง หากไม่มี resolver หรือแหล่งข้อมูลที่ตกลงแล้ว ให้ compiler/validation หรือ evaluation ตอบ diagnostic ชัดเจน ห้ามตีความว่า lookup ไม่พบ และห้ามแสดง `available=true`

## เกณฑ์ปิด M2.5 (ครบแล้วตามหลักฐาน 2026-11-17)

- Test ของ operator ครบตามจำนวนข้างบน รวม null, conversion fail, limit และผลที่ deterministic
- Test ของ Named Lookup สองรายการกับ resolver ที่มีข้อมูลทดสอบและกรณี dependency ใช้ไม่ได้
- ผล `scripts/check.ps1` และสถานะ capability catalog หลัง implement
- ผล Q2/Q7 เรื่องแหล่งข้อมูล `mti_original` และ adapter ก่อนเปิด lookup ใน environment จริง
