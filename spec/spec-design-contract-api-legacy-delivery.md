---
title: Contract API and Legacy Table Delivery Design
version: 2.0
date_created: 2026-10-19
last_updated: 2026-10-21
owner: Mapping Demo Team
tags: [design, mapping, contract, api, legacy, json-file, strangler-fig, json-schema]
status: Accepted for Phase 25 implementation
supersedes: spec-design-file-to-json-export.md
---

# Introduction

เอกสารนี้ออกแบบการขยาย Mapping Demo ให้รับไฟล์ `.txt`, `.csv`, `.xlsx` และ `.xls` แล้วแปลง Source row ไปยังปลายทางที่เลือกต่อ Mapping Config ได้สามแบบ:

1. `ApiContract` — สร้าง JSON document ตาม contract version แล้วส่งหนึ่ง HTTP request ต่อ mapped output/proposal
2. `LegacyTable` — เขียน Normalized Table แบบเดิมเพื่อรองรับ partner ที่ยังอยู่บน legacy
3. `JsonFile` — รวม mapped documents ของหนึ่ง File Job เป็น JSON array เดียว แล้ว publish ไป server-owned filesystem แบบ atomic

ทั้งสามแบบใช้ Input Layout, Transformation Program, condition และ typed expression ชุดเดียวกัน การเลือก destination ถูกตรึงใน Config Version เพื่อให้ File Job ใช้ behavior เดิมตลอดงานและ rollback ด้วยการ activate version ก่อนหน้าได้

```text
Input file
(.txt/.csv/.xlsx/.xls)
        │
        ▼
Input Layout ──► Source Table ──► Transformation Module
                                      │
                                      ▼
                              Mapped output 0..N
                                      │
                              Destination Module seam
                         ┌──────────┬──────────┴─────────┐
                         ▼          ▼                    ▼
               LegacyTable     ApiContract          JsonFile
               Normalized      HTTP request per     staged rows + one
               Table           mapped output        atomic JSON array
```

`ApiContract` และ `JsonFile` ยังคงบันทึก raw Source row เพื่อ audit/reprocess แต่ต้องไม่ scaffold entity, ไม่สร้าง dynamic Normalized Table และไม่ insert/update/delete Normalized Table

Revision 2.0 นี้ supersede ข้อความก่อนหน้าในเอกสารเดียวกันที่ตัด `JsonFile` ออกจาก scope หลังจากเลือก Prototype Variant C แล้ว ส่วน `spec-design-file-to-json-export.md` คงสถานะ historical reference และนำมาใช้เฉพาะ semantics เรื่อง ordering, atomic publish และ crash recovery; contract และ policy ในเอกสารนี้เป็น authoritative source

## 1. Purpose & Scope

### 1.1 เป้าหมาย

- รองรับ Destination สามแบบคือ `ApiContract`, `LegacyTable` และ `JsonFile`
- รักษา legacy behavior โดยไม่บังคับ partner ทุกแห่งย้ายพร้อมกัน
- ให้ Mapping Config Version pin contract เช่น `pa-proposal/1`
- ดึง JSON Schema ตอนสร้าง version แล้วเก็บ immutable snapshot พร้อม hash
- เปลี่ยน mapping จาก Source → Target column เป็น Source → Contract path สำหรับ API mode
- สร้าง nested object/array จาก leaf mappings โดยอัตโนมัติ
- จัดกลุ่มหลาย Source columns เป็น object items ภายใน array
- ส่งหนึ่ง API request ต่อ mapped output/proposal เพื่อ retry แยกรายการ
- ใช้ branch-by-abstraction และ strangler-fig โดยมี rollback ต่อ config
- publish JSON array หนึ่ง artifact ต่อ File Job แบบ deterministic และ atomic โดยไม่เปลี่ยน API delivery ให้เป็น HTTP batch
- เพิ่ม typed Value Expressions `add` และ `concat` ให้ใช้ร่วมกันก่อนเลือก Destination Adapter

### 1.2 Requirement traceability

| Requirement | Design section / IDs | Acceptance IDs |
|---|---|---|
| R1 ApiContract โดยไม่สร้าง/เขียน Normalized Table | 3.1, 4.2, 4.8 | AC-R1-01..02 |
| R2 destinations อยู่ร่วมกัน | 3.1, 4.3, 8.5 | AC-R2-01..02, AC-JF-ROW-02 |
| R3 Source → contract path จาก schema endpoint | 3.3, 4.5, 5.4 | AC-R3-01..02 |
| R4 pin contract version | 3.2, 4.1, 4.4 | AC-R4-01..02 |
| R26 nested object/array path | 3.4, 4.6 | AC-R26-01..03 |
| R27 auto-create intermediate containers | 3.4, 4.7 | AC-R27-01 |
| R28 scalar/object/array target types | 3.5, 4.5 | AC-R28-01..02 |
| R29 group columns เป็น array item | 3.6, 4.6, 10.2 | AC-R29-01 |
| R30 skip empty/hyphen-only item | 3.6, 4.7, 10.2 | AC-R30-01..02 |
| R31 preserve source slot as seq | 3.6, 4.6, 10.2 | AC-R31-01 |
| PA MTLS example without narrowing core coverage | 1.6, 3.8, 4.9, 4.10, 10.5 | AC-PAY-01..04 |
| JsonFile immutable destination and compatibility | JF-DST-001..008 | AC-JF-COMPAT-01, AC-JF-CONTRACT-01..03 |
| Contract reuse without HTTP coupling | JF-SCH-001..004 | AC-JF-SCHEMA-01..02 |
| `add` and `concat` typed expressions | EXP-001..006 | AC-EXP-ADD-01..02, AC-EXP-CONCAT-01..02, AC-EXP-COMPAT-01 |
| Adapter registry and transaction-aware routing | JF-RTR-001..004 | AC-JF-ROUTER-01..02 |
| Durable staging and file-level finalization | JF-STG-001..006, JF-PUB-001..008 | AC-JF-STAGE-01..02, AC-JF-ROW-01..02, AC-JF-PUBLISH-01..03 |
| Retry, reprocess and crash recovery | JF-REC-001..007 | AC-JF-RECOVERY-01..03 |
| Preview, UI, operations and security | JF-PRV-001..004, JF-OPS-001..009 | AC-JF-PREVIEW-01..02, AC-JF-UI-01..02, AC-JF-OPS-01..03 |
| Phase 25 E2E and documentation | JF-E2E-001..003, JF-DOC-001 | AC-JF-E2E-01..02, AC-JF-DOC-01 |

### 1.3 ขอบเขต v1

| Area | Scope |
|---|---|
| Input | `.txt`, `.csv`, `.xlsx`, `.xls` ตาม Input Layout ที่ pin ใน version |
| Destinations | `ApiContract`, `LegacyTable` หรือ `JsonFile`; เลือกหนึ่งแบบต่อ Config Version |
| API delivery | หนึ่ง request ต่อ mapped output/proposal |
| Source persistence | บันทึก Source Table ทั้งสาม destination |
| Contract schema | snapshot JSON Schema พร้อม SHA-256 hash ตอนสร้าง Config Version |
| Mapping | scalar leaf path, nested object, fixed array index และ grouped array items |
| Retry | แยกต่อ mapped output ด้วย stable idempotency key |
| Migration | config-by-config strangler rollout; ไม่มี big-bang migration |

### 1.4 นอกขอบเขต

- dual-delivery ของ mapped output เดียวข้าม destination ใด ๆ พร้อมกัน
- การ scaffold entity จาก contract
- การสร้าง Normalized Table สำหรับ `ApiContract`
- การ generate client SDK จาก JSON Schema
- การ execute raw SQL, script หรือ expression จาก config
- dynamic contract/schema lookup ระหว่าง runtime ของ File Job
- การ retire LegacyTable adapter ก่อนทุก partner ที่เกี่ยวข้องผ่าน migration gate
- การแก้ source code ในขั้นออกแบบนี้

### 1.5 Decisions ที่ยืนยันแล้ว

- Destination มี `ApiContract`, `LegacyTable` และ `JsonFile`; Config Version หนึ่งรุ่นเลือกได้เพียงหนึ่งชนิด
- API ส่งหนึ่ง request ต่อ mapped output/proposal
- API mode ยังเก็บ Source Table แต่ไม่สร้างและไม่เขียน Normalized Table
- Contract schema ถูก snapshot และ hash ตอนสร้าง Config Version
- array `seq` ถูกเติมอัตโนมัติจาก source slot และ config ระบุ sequence path
- operator เดิมยังใช้ต่อ และเพิ่ม typed Value Expressions `add`/`concat` ตาม EXP-001..006 เท่านั้น

### 1.6 Coverage principle

- แบบออกแบบนี้ต้องเป็น **contract-agnostic** และรองรับ contract/profile อื่นนอกเหนือจาก PA MTLS
- JSON ของ PA MTLS ในหัวข้อ 10.5 เป็น illustrative example เพื่อทดสอบ coverage เท่านั้น ไม่ใช่ canonical schema และไม่ใช่ข้อกำหนดให้ hard-code field/path ลงระบบกลาง
- รายการ field, required/optional, type, nested structure และ array item ต้องยึด Contract Snapshot ของ Config Version นั้น
- path ที่ระบบเติมจาก runtime metadata กับ path ที่ผู้ใช้ map ต้องกำหนดเป็น metadata ของ contract/profile และ validate กับ snapshot ห้ามอนุมานจากชื่อตัวแปร
- หากตัวอย่างเฉพาะ partner ขัดกับกติกากลาง ให้สร้าง profile/version-specific rule โดยไม่ลด coverage ของ `ApiContract`, `LegacyTable`, object/array, retry หรือ reprocess

## 2. Definitions

**Destination Definition**: immutable config ที่ระบุว่าจะส่ง mapped output ผ่าน `ApiContract`, `LegacyTable` หรือ `JsonFile`

**ApiContract**: destination ที่ประกอบ JSON document ตาม Contract Snapshot แล้วส่ง HTTP request ไป endpoint profile ที่ server เป็นเจ้าของ

**LegacyTable**: destination adapter ที่ใช้ Normalized Table และ reconciliation behavior เดิม

**JsonFile**: destination ที่ stage mapped documents ต่อ row แล้ว publish JSON array หนึ่งไฟล์ต่อ File Job ไปยัง server-owned filesystem profile; ไม่ใช่ HTTP batch และไม่ส่งสำเนาเดียวกันไป destination อื่น

**JSON Export Artifact**: final immutable JSON file พร้อม identity, revision, SHA-256, object count, byte count และ audit metadata

**Contract Reference**: identifier แบบ `contractId/version` เช่น `pa-proposal/1`

**Contract Snapshot**: canonical JSON Schema ที่บันทึกใน Config Version พร้อม hash และ metadata ตอนสร้าง version

**Contract Path**: path จาก root ของ contract ไปยัง node เช่น `proposal.insured.fullName`

**Leaf Path**: Contract Path ที่ assign scalar value ได้

**Container Path**: path ชนิด object หรือ array ที่ระบบสร้างจาก leaf assignments; ผู้ใช้ไม่ map value ตรงลง container

**Wildcard Array Path**: path ที่ใช้ `[]` แทน item schema เช่น `proposal.beneficiaries[].fullName`

**Fixed Array Index Path**: path ที่ระบุตำแหน่งแบบ zero-based เช่น `proposal.beneficiaries[0].fullName`

**Array Group**: config ที่รวมหลาย Source expressions ให้เป็นหนึ่ง object item ใน target array

**Source Slot**: ลำดับช่องเดิมในไฟล์ เช่น beneficiary ชุดที่ 1 หรือชุดที่ 2 ใช้เป็นค่า `seq`

**Mapped Document**: nested typed business value ที่ผ่าน transformation และ validation ของ mapped paths แล้ว แต่อาจยังไม่มี System-bound Paths

**Delivery Payload**: document หลังเติม System-bound Paths และผ่าน final validation กับ Contract Snapshot แล้ว พร้อม persist/send โดย Destination Module

**Delivery Attempt**: การพยายามส่ง mapped output หนึ่งรายการไป destination หนึ่งครั้ง

**Idempotency Key**: stable key ของ mapped output ที่ปลายทางใช้ป้องกันการสร้างข้อมูลซ้ำเมื่อ retry

**System-bound Path**: Contract Path ที่ค่ามาจาก runtime context เช่น File Job, Source Row, Config Version หรือ Output Key และผู้ใช้แก้ expression ไม่ได้

**Mapped Path**: Contract Path ที่ค่ามาจาก Source field, constant, lookup หรือ expression ใน Transformation Program

**Illustrative Contract Profile**: ตัวอย่าง payload ของ partner/product หนึ่งรายที่ใช้ตรวจว่าแบบออกแบบครอบคลุม แต่ไม่เปลี่ยนเป็น global contract โดยอัตโนมัติ

## 3. Requirements, Constraints & Guidelines

### 3.1 Destination requirements

- **R1**: `ApiContract` ต้องส่ง mapped document ไป endpoint ปลายทางโดยไม่ scaffold entity, ไม่สร้าง Normalized Table และไม่เขียน Normalized Table
- **R2**: ระบบต้องรองรับ `ApiContract`, `LegacyTable` และ `JsonFile` พร้อมกัน โดยเลือก destination ผ่าน Mapping Config Version
- **DST-001**: Config Version หนึ่งรุ่นต้องมี destination เดียวเท่านั้น
- **DST-002**: Config Version เดิมที่ไม่มี Destination Definition ต้องถูกอ่านเป็น `LegacyTable` โดยอัตโนมัติ
- **DST-003**: File Job ต้อง pin Config Version และ Destination Definition ตั้งแต่ intake
- **DST-004**: การ activate version ใหม่ต้องไม่มีผลกับ File Job ที่สร้างไปแล้ว
- **DST-005**: การเปลี่ยน destination ต้องเกิดผ่าน version ใหม่ ห้ามแก้ active version เดิม
- **DST-006**: `LegacyTable` ต้องรักษา upsert, stale-output reconciliation, retry และ reprocess semantics เดิม
- **DST-007**: `ApiContract` ต้องสร้าง Delivery Job หนึ่งรายการต่อ Transformation output
- **DST-008**: Transformation result ที่เป็น Filtered หรือมี output เป็นศูนย์ต้องไม่สร้าง Delivery Job
- **DST-009**: Invalid transformation ต้องไม่เรียก destination adapter
- **JF-DST-001**: `JsonFile` ต้องไม่เขียน Normalized Table, ไม่สร้าง API Delivery Job และไม่เรียก outbound HTTP
- **JF-DST-002**: `JsonFile` หนึ่ง File Job ต้อง publish JSON array artifact เดียว หรือไม่ publish เลย
- **JF-DST-003**: `JsonFile` ต้องอ้าง logical File Destination Profile, Contract Reference, relative folder, filename template และ formatting เท่านั้น
- **JF-DST-004**: Output Root, filesystem topology, credential และ encryption key reference ต้องเป็น server-owned configuration และห้ามคืนให้ browser
- **JF-DST-005**: destination fields ของคนละ derived type ห้ามปะปนกัน และ unknown field/kind ต้องถูก reject ด้วย exact path
- **JF-DST-006**: migration ต้องเป็น expand-only, ห้าม rewrite Config Version เดิม และ `normalizedTableId`/`apiDestinationProfileId` ต้อง nullable ตามชนิด destination
- **JF-DST-007**: `JsonFile` ไม่ใช่ HTTP batch mode ของ `ApiContract`; lifecycle, retry และ status เป็น file-level export แยกจาก API Delivery Job
- **JF-DST-008**: ห้าม dual-delivery production payload เดียวกันข้าม `LegacyTable`, `ApiContract` และ `JsonFile`

### 3.2 Contract versioning requirements

- **R4**: ApiContract Config Version ต้องระบุ Contract Reference เช่น `pa-proposal/1`
- **CTR-001**: ตอนสร้าง Config Version ระบบต้องดึง schema จาก server-owned Contract Registry Module
- **CTR-002**: ระบบต้อง canonicalize schema, คำนวณ SHA-256 และเก็บ schema snapshot/hash ใน Config Version transaction เดียวกัน
- **CTR-003**: runtime compile/evaluate/deliver ต้องใช้ snapshot เท่านั้น ห้าม fetch schema ใหม่
- **CTR-004**: ถ้า fetch schema ไม่สำเร็จหรือ schema ใช้ feature ที่ไม่รองรับ ต้องไม่สร้าง Config Version
- **CTR-005**: การเปลี่ยน schema ที่ Contract Reference เดิมแต่ hash ต่างต้องถูกแจ้งเป็น drift และต้องสร้าง version ใหม่หลังผู้ใช้ยืนยัน
- **CTR-006**: Version history ต้องแสดง Contract Reference, schema hash, fetched-at และ endpoint profile
- **CTR-007**: schema snapshot ของ active/used version ห้ามถูกลบแม้ Contract Registry เลิกให้บริการ version นั้น

### 3.3 Contract path requirements

- **R3**: ApiContract mapping ต้องเปลี่ยนจาก Source → target column เป็น Source → Contract Path
- **PATH-001**: รายการ path ต้องมาจาก Contract Snapshot ที่ pin ไว้ ไม่รับ arbitrary path ที่ browser ส่งมาโดยไม่ validate
- **PATH-002**: UI ต้องแสดง tree และ searchable dropdown ของ paths พร้อม type, required และ description
- **PATH-003**: object/array container แสดงใน tree ได้แต่เลือกเป็น scalar assignment ไม่ได้
- **PATH-004**: leaf path ต้องอ้าง node ที่มีอยู่จริงและชนิดค่าต้อง compatible กับ expression
- **PATH-005**: ห้าม assign leaf path เดียวกันซ้ำใน output branch เดียว
- **PATH-006**: ห้ามเกิด parent/child conflict เช่น assign `proposal.insured` เป็น scalar พร้อมกับ `proposal.insured.fullName`
- **PATH-007**: LegacyTable mode ต้องแสดง flat target columns เดิมและไม่แสดง nested path editor

### 3.4 Nested object and array requirements

- **R26**: Config ต้องรองรับ nested paths เช่น `proposal.insured.fullName`, `proposal.beneficiaries[].fullName` และ `proposal.beneficiaries[0].fullName`
- **R27**: Document Assembler ต้องสร้าง intermediate object/array ให้อัตโนมัติจาก leaf assignments
- **NEST-001**: property separator คือ `.`
- **NEST-002**: fixed index ใช้ `[n]` โดย `n` เป็น zero-based integer
- **NEST-003**: wildcard `[]` ใช้ภายใน Array Group เพื่ออ้าง item schema; scalar assignment นอก group ห้ามใช้ wildcard
- **NEST-004**: fixed-index assignments ของ array เดียวกันต้องครอบคลุม index ต่อเนื่องตั้งแต่ `0`; gap ต้อง fail validation ด้วย `ArrayIndexGap`
- **NEST-005**: object ถูกสร้างเมื่อมี descendant อย่างน้อยหนึ่งค่าที่ emit
- **NEST-006**: array ถูกสร้างเมื่อมี item อย่างน้อยหนึ่งรายการ หรือเมื่อ schema บังคับ required array ซึ่งให้สร้าง `[]`
- **NEST-007**: path compiler ต้อง parse path เป็น immutable tokens ตอน compile ห้าม parse string ซ้ำทุก row

### 3.5 Target type requirements

- **R28**: Target model ต้องรองรับ `string`, `integer`, `number`, `boolean`, `date`, `time`, `object` และ `array` พร้อม item type จาก JSON Schema

| JSON Schema node | Internal target type | JSON representation | Assignable directly |
|---|---|---|---|
| `type: string` | Text | string | Yes |
| `type: string, format: date` | Date | `yyyy-MM-dd` string | Yes |
| `type: string, format: time` | Time | `HH:mm:ss` string | Yes |
| `type: integer` | Integer | number | Yes |
| `type: number` | Decimal | number | Yes |
| `type: boolean` | Boolean | boolean | Yes |
| `type: object` | Object | object | No; built from descendants |
| `type: array` | Array | array | No; built from items |

- **TYPE-001**: compiler ต้องใช้ schema required list, string length, numeric constraints และ array item schema ที่รองรับในการ validate
- **TYPE-002**: Transformation Program language version ใหม่ต้องเพิ่ม Time value type โดยไม่เพิ่ม boolean operator ใหม่
- **TYPE-003**: null อนุญาตเฉพาะ node ที่ contract กำหนดให้ nullable/optional
- **TYPE-004**: external `$ref`, recursive schema, `oneOf`, `anyOf` และ ambiguous unions อยู่นอก v1 และต้อง fail ตอน snapshot validation
- **TYPE-005**: local `$ref` ภายใน snapshot รองรับได้เมื่อ resolver ทำให้ได้ deterministic tree และตรวจ cycle

### 3.6 Array grouping requirements

- **R29**: Config ต้องจัด Source expressions หลายตัวเป็น item เดียวของ target array ได้
- **R30**: item ที่ business fields ทุก field เป็น null, empty, whitespace หรือ `-` หลัง trim ต้องถูกข้าม
- **R31**: sequence value ต้องใช้ Source Slot เดิม ไม่ใช่ index หลัง compact array
- **ARR-001**: Array Group ต้องระบุ array path, item mappings, Source Slot และ Sequence Path
- **ARR-002**: Sequence Path ต้องเป็น integer leaf ภายใน array item schema
- **ARR-003**: synthetic sequence field ไม่นับรวมตอนตัดสิน empty item
- **ARR-004**: ค่า `0`, `false` และ string ที่มี `-` ร่วมกับอักขระอื่นไม่ถือว่า empty
- **ARR-005**: ถ้ามี business field อย่างน้อยหนึ่ง field ไม่ empty ให้ emit item และ field อื่นใช้ null/omit ตาม schema
- **ARR-006**: หลัง skip items ระบบ compact JSON array แต่เก็บ sequence ตาม Source Slot
- **ARR-007**: Array Group หลายกลุ่มห้ามเขียน array path เดียวกันใน output branch เดียว
- **ARR-008**: direct fixed-index mapping และ Array Group ห้าม target array เดียวกันพร้อมกัน

### 3.7 API delivery requirements

- **API-001**: ส่งหนึ่ง HTTP request ต่อ mapped output/proposal
- **API-002**: URL, HTTP method, authentication, timeout และ allowed headers ต้องมาจาก server-owned API Destination Profile ไม่ใช่ raw browser config
- **API-003**: Mapping Config Version เก็บเฉพาะ logical profile ID และ Contract Reference
- **API-004**: ทุก request ต้องมี stable Idempotency Key จาก `configVersionId:fileJobId:sourceRowId:outputKey`
- **API-005**: response `2xx` ถือว่าสำเร็จ
- **API-006**: network error, timeout, `408`, `429` และ `5xx` retry ได้ตาม bounded exponential backoff
- **API-007**: `4xx` อื่นเป็น permanent delivery failure จนกว่าจะ reprocess ด้วย version/config ใหม่
- **API-008**: retry ต้องใช้ payload bytes, Contract Snapshot และ Idempotency Key เดิม
- **API-009**: ห้าม log request/response body โดย default; เก็บ status, duration, correlation ID และ redacted error summary
- **API-010**: API credentials ต้องอ้าง secret provider และห้ามอยู่ใน Mapping Config JSON
- **API-011**: API adapter ต้อง validate final document กับ Contract Snapshot ก่อน enqueue/send
- **API-012**: endpoint ที่ไม่รองรับ idempotency ต้องถูกจัดเป็น rollout blocker หรือมี partner-specific deduplication contract ที่อนุมัติแล้ว

### 3.8 Payload ownership requirements

- **PAY-001**: ระบบต้องรองรับทั้ง System-bound Path และ Mapped Path ภายใน payload เดียวกัน
- **PAY-002**: การระบุว่า path ใดเป็น system-bound หรือ mapped ต้องมาจาก immutable contract/profile metadata ที่ pin กับ Config Version และต้อง validate path/type กับ Contract Snapshot
- **PAY-003**: System-bound Path ต้องไม่ปรากฏเป็น target ที่เลือก map ได้ แต่ UI และ Preview ต้องแสดงค่า/ที่มาของ field แบบ read-only
- **PAY-004**: runtime ต้องเติม System-bound Path ต่อ mapped output ก่อน final contract validation และก่อน persist payload สำหรับ delivery
- **PAY-005**: field name และรูปแบบของ runtime metadata ห้าม hard-code ให้เป็น PA MTLS; adapter ใช้ compiled binding เช่น `FileJobId → fileJobId`
- **PAY-006**: object extension เช่น `proposal.extras` map ได้เฉพาะ path/property ที่ Contract Snapshot อนุญาต; ห้ามถือว่า arbitrary key ใช้ได้เพียงเพราะตัวอย่างแสดง `{}`
- **PAY-007**: partner-specific field เช่น `sourceRowNo` จะเป็น required ได้เมื่อ schema/profile กำหนด โดย runtime binding ต้องระบุ source และ conversion ที่ชัดเจน
- **PAY-008**: ตัวอย่าง idempotency fields ภายใน payload ไม่แทนที่ delivery idempotency rule; header/key จริงยังเป็นข้อตกลงของ API Destination Profile ตาม API-004 และ `docs/phase-24-delivery-decisions.md`

### 3.9 General constraints

- **CON-001**: Source Table persistence, duplicate-file policy และ version pinning เดิมต้องคงอยู่
- **CON-002**: ไม่มี raw SQL, raw code หรือ arbitrary HTTP URL ใน Mapping Config
- **CON-003**: Preview ห้ามเขียน Source Table, LegacyTable, Delivery Job หรือเรียก partner API
- **CON-004**: active Config Version immutable
- **CON-005**: destination change ห้ามเกิดด้วย percentage random routing ภายใน config เดียว เพราะ retry/reprocess ต้อง deterministic
- **CON-006**: canary ทำเป็นราย Mapping Config/partner และ rollback ด้วย Config Version/feature flag

### 3.10 JsonFile contract and schema requirements

- **JF-SCH-001**: `JsonFile` ต้อง resolve Contract Reference ผ่าน Contract Registry Module ตอนสร้าง Config Version และ pin canonical snapshot, hash, Target Schema และ Payload Binding Snapshot แบบเดียวกับ `ApiContract`
- **JF-SCH-002**: File Destination Profile ไม่ต้องมี delivery endpoint หรือ authentication แต่ต้องระบุ logical output profile และ binding profile ที่ server เป็นเจ้าของ
- **JF-SCH-003**: runtime และ retry ต้องใช้ snapshot ที่ pin เท่านั้น ห้าม fetch schema ใหม่
- **JF-SCH-004**: Contract Snapshot เดียวกันกับ mapped values/runtime bindings เดียวกันต้องสร้าง document ที่ parsed JSON เท่ากันใน `ApiContract` และ `JsonFile`; ต่างกันเฉพาะ adapter envelope/transport

### 3.11 Typed Value Expression requirements

- **EXP-001**: `add` มี shape `{ kind: "add", values: ValueExpression[] }`, มี operand 2..256 รายการ และรับเฉพาะ Integer/Decimal
- **EXP-002**: `add` คืน Integer เมื่อทุก operand เป็น Integer และคืน Decimal เมื่อมี Decimal อย่างน้อยหนึ่ง operand; arithmetic ต้อง checked และ overflow เป็น path diagnostic
- **EXP-003**: `concat` มี shape `{ kind: "concat", values: ValueExpression[], separator: string }`, มี operand 2..256 รายการ, รับเฉพาะ Text และ separator เป็น string literal
- **EXP-004**: `add`/`concat` ใช้ null propagation; operand ใดเป็น null ให้ผลเป็น null ผู้ใช้ต้องใช้ `coalesce` หรือ supported explicit `convert` หากต้องการ semantics อื่น
- **EXP-005**: compiler/evaluator ต้องนับ dependency, node, depth และ collection limit ของ operands และคืน exact path `values[i]` ใน diagnostics
- **EXP-006**: model, request/response, OpenAPI, TypeScript union, capabilities, editor, preview และ runtime ต้องใช้ semantics เดียวกัน โดย config เดิม serialize/evaluate เหมือนเดิม

### 3.12 Destination routing and staging requirements

- **JF-RTR-001**: production caller ต้องเรียก transaction-aware Destination Router จุดเดียวหลัง Transformation/Document Assembly โดยเลือก adapter จาก `destination.kind`
- **JF-RTR-002**: registry ต้อง fail fast เมื่อ declared kind ไม่มี adapter หรือมี adapter ซ้ำ
- **JF-RTR-003**: transport-specific transaction, persistence, completion และ retry semantics ต้องอยู่หลัง adapter interface; caller ห้ามมี `IsApiContract`/`IsJsonFile` branch กระจาย
- **JF-RTR-004**: LegacyTable และ ApiContract ต้องรักษา row/byte/job side effects เท่า characterization baseline หลังย้ายเข้า router
- **JF-STG-001**: Row Job ต้อง upsert encrypted canonical mapped payload และ reconcile stale outputs ภายใน transaction เดียวกับ row completion
- **JF-STG-002**: staged identity ต้องประกอบด้วย File Job, pinned Config Version, Source Row, stable `outputKey` และ `outputOrdinal` เพื่อรองรับ 0..N outputs และ reprocess
- **JF-STG-003**: filtered/zero-output เป็น successful row outcome และยังนับในการตัดสิน file-level readiness
- **JF-STG-004**: latest Row Jobs ทุกตัวต้อง terminal และไม่มี Invalid/Failed ก่อนสร้าง export job/outbox; parallel last rows ต้องสร้างได้เพียงหนึ่ง job
- **JF-STG-005**: staged payload bytes ต้องเข้ารหัส AES-256-GCM ด้วย server-owned key reference และเก็บ SHA-256 ของ canonical plaintext เพื่อ recovery โดยไม่ log body
- **JF-STG-006**: query finalization ต้องอ่านตาม `rowNumber`, `outputOrdinal` และ stable tie-breaker ผ่าน index โดย memory ไม่โตตามจำนวน outputs

### 3.13 Publish and recovery requirements

- **JF-PUB-001**: final document เป็น JSON array; zero outputs publish `[]`; object order คือ `rowNumber`, `outputOrdinal`, `outputKey` และ staged item ID ตามลำดับ
- **JF-PUB-002**: serializer ใช้ UTF-8 ไม่มี BOM, deterministic target property order, invariant JSON scalar representation และ formatting `compact` หรือ `indented` ที่ pin ใน version
- **JF-PUB-003**: finalizer ต้อง stream ไป temp file, คำนวณ SHA-256/byte/object count ระหว่างเขียน, flush/close แล้ว atomic rename ภายใน filesystem volume เดียวกัน
- **JF-PUB-004**: final path ต้องไม่ visible ก่อน document สำเร็จทั้งไฟล์ และห้าม overwrite successful artifact
- **JF-PUB-005**: runtime limit คือ input ไม่เกิน 10 MiB, 10,000 parsed Source rows, 256 columns, 32,768 characters ต่อ cell, 32 outputs ต่อ Source row, 320,000 objects ต่อ artifact, 1 MiB canonical JSON ต่อ object และ 256 MiB ต่อ final artifact; เกิน limit ต้อง Block ก่อน publish
- **JF-PUB-006**: preview limit คงเป็น 1 MiB/50 parsed records/10 seconds และต้องรายงาน estimated bytes โดยไม่ stage หรือแตะ filesystem
- **JF-PUB-007**: temp และ final directory ต้องอยู่บน NTFS volume เดียวกันภายใต้ Output Root; symlink/reparse-point escape, absolute path และ traversal ต้องถูก reject
- **JF-PUB-008**: temp/final free-space preflight ต้องเผื่อ final limitหนึ่งชุดและ temp working copyหนึ่งชุด; disk-full/permission/quota failure ต้องไม่ทิ้ง partial final file
- **JF-REC-001**: export job claim ใช้ lease; stale lease retry ได้ด้วย staged payload/config/profile เดิม
- **JF-REC-002**: crash ก่อน rename ต้องลบ/แทน temp ของ export identity เดิมอย่างปลอดภัยแล้ว retry; crash หลัง renameก่อน DB update reconcile ได้เมื่อ path, identity, byte count และ hash ตรง
- **JF-REC-003**: final path collision ที่ hash/identity ต่างต้อง Block พร้อม diagnostic ห้าม overwrite; hash และ identity ตรงถือเป็น idempotent recovery
- **JF-REC-004**: retry export เดิมใช้ artifact identity, revision, filename, staged bytes และ formatting เดิม
- **JF-REC-005**: reprocess สร้าง export identity และ `artifactRevision` ใหม่แบบ monotonic พร้อม audit link ไป artifact ก่อนหน้า แม้ใช้ Config Version เดิม; ห้ามแทนที่ไฟล์สำเร็จเดิม
- **JF-REC-006**: allowed filename tokens คือ `{sourceBaseName}`, `{fileJobId}`, `{configVersion}`, `{configVersionId}` และ `{artifactRevision}`; default คือ `{sourceBaseName}.{fileJobId}.r{artifactRevision}.json`
- **JF-REC-007**: `sourceBaseName` ต้อง sanitize เป็นชื่อไฟล์ Windows ที่ปลอดภัย; token ไม่รู้จัก, separator, rooted path, reserved device name, trailing dot/space และผลลัพธ์เกิน 180 characters ต้องถูก reject

### 3.14 Preview, status, operations and security requirements

- **JF-PRV-001**: preview ใช้ compiler, assembler และ serializer เดียวกับ runtime แล้วคืน ordered final JSON representation, object count, estimated UTF-8 bytes และ diagnostics โดยไม่มี side effect
- **JF-PRV-002**: File Job API เพิ่ม export lifecycle/status แบบ additive และแสดงเฉพาะ relative display path, count, bytes, hash, published-at และ redacted block reason
- **JF-PRV-003**: UI ต้องใช้ Variant C: `Legacy Table`, `Whole file · JSON file`, `One record at a time · API Contract`; เปลี่ยนชนิดแล้วต้อง clear stale fields ก่อน submit และสร้าง Config Version ใหม่เสมอ
- **JF-PRV-004**: UI ต้อง reuse production Input Layout, Transformation, Contract Path และ preview modules; production code ห้าม depend บน `/prototype/file-to-json`
- **JF-OPS-001**: v1 ไม่มี HTTP download endpoint; payload access ใช้ ACL ของ Output Root เฉพาะ Operations service account และ authorized downstream consumer จึงไม่มี browser payload-download permission
- **JF-OPS-002**: API/UI/log/metric ห้ามเผย absolute Output Root, credential, encryption key reference หรือ payload body
- **JF-OPS-003**: staged payload/temp file หมดอายุ 30 วันหลัง terminal state; cleanup ทำงานรายชั่วโมงและต้องลบภายใน 24 ชั่วโมงหลัง expiry เมื่อไม่มี legal hold
- **JF-OPS-004**: final JSON artifact เก็บ 30 วันหลัง Published แล้ว cleanup ภายใน 24 ชั่วโมงหลัง expiry; audit metadata/hash/path identity เก็บ 365 วัน
- **JF-OPS-005**: staged payload ใช้ application-level AES-256-GCM; temp/final volume ต้องใช้ BitLocker หรือ platform-managed encryption at rest และ ACL ตาม profile
- **JF-OPS-006**: metrics ต้องมี queue age, finalize latency, bytes, objects, blocked, retry, hash conflict และ cleanup failure แยก profile/config โดยไม่มี PII
- **JF-OPS-007**: global kill switch ต้องแยก `JsonFile` activation และ finalizer dispatch; pause/resume ต้องรักษา staged data/artifact identity และห้าม reroute pinned jobs
- **JF-OPS-008**: Business/Contract owner อนุมัติ Contract/Profile และ config activation; Platform/Operations owner ดูแล Output Root, quota, worker, cleanup, alert และ rollout; Security owner อนุมัติ ACL/encryption/key retention
- **JF-OPS-009**: `JsonFile` rollout ทำ config-by-config ผ่าน Shadow filesystem root ที่ไม่มี business consumer, Canary และ Expanded; ห้ามใช้ production dual-send เพื่อเปรียบเทียบ

### 3.15 Phase 25 completion requirements

- **JF-E2E-001**: acceptance ต้องครอบคลุม `.txt/.csv/.xlsx/.xls` → Source → nested mapping/array/`add`/`concat` → `JsonFile` สำหรับ zero/one/many outputs, parallel completion, retry, reprocess และ failure injection
- **JF-E2E-002**: LegacyTable characterization และ ApiContract exact-byte/idempotency suite ต้องผ่านโดยไม่มี destination/data rewrite
- **JF-E2E-003**: Gate E ต้องผ่าน backend unit/integration, migration, formatter, web lint/build/E2E, security tests และ owner sign-off ก่อนเปิด Production File Destination Profile
- **JF-DOC-001**: CONTEXT, README, workflow, OpenAPI/examples, setup/reset, retention/cleanup/backup/restore/rollback runbooks และ acceptance matrix ต้องแยก JsonFile publish, API delivery และ Legacy reconcile ชัดเจน และชี้ prototype เป็น design evidence เท่านั้น

## 4. Interfaces & Data Contracts

### 4.1 Mapping Config Version v2

```json
{
  "acceptedExtensions": [".xlsx", ".xls"],
  "inputLayout": {
    "kind": "excel",
    "sheetName": "Customer",
    "headerRow": 3,
    "dataStartRow": 4,
    "fields": []
  },
  "destination": {
    "kind": "apiContract",
    "apiDestinationProfileId": "pa-proposals",
    "contract": {
      "id": "pa-proposal",
      "version": "1",
      "reference": "pa-proposal/1",
      "schemaHash": "sha256:7d4f..."
    },
    "payloadBindingsSnapshot": {
      "bindings": []
    }
  },
  "contractSchemaSnapshot": {},
  "transformationProgram": {
    "languageVersion": 2,
    "semanticsProfile": "sqlserver-legacy-v1",
    "outputs": []
  }
}
```

Backward compatibility:

| Existing state | Interpreted v2 state |
|---|---|
| ไม่มี `destination` และมี `normalizedTableId` | `destination.kind = legacyTable` |
| Transformation language v1 | ใช้ `normalizedColumn` และ flat schema เดิม |
| New ApiContract version | language v2 และ `targetPath` |

### 4.2 Destination Definition

```text
DestinationDefinition
├─ LegacyTableDestination
│  ├─ kind: "legacyTable"
│  └─ normalizedTableId: long
├─ ApiContractDestination
   ├─ kind: "apiContract"
   ├─ apiDestinationProfileId: string
   └─ contract
      ├─ id: string
      ├─ version: string
      ├─ reference: string
      └─ schemaHash: string
└─ JsonFileDestination
   ├─ kind: "jsonFile"
   ├─ fileDestinationProfileId: string
   ├─ contract
   │  ├─ id: string
   │  ├─ version: string
   │  ├─ reference: string
   │  └─ schemaHash: string
   ├─ relativeFolder: string
   ├─ fileNameTemplate: string
   └─ formatting: "compact" | "indented"
```

`normalizedTableId` ต้อง nullable สำหรับ API/File configs และ `apiDestinationProfileId` ต้อง nullable สำหรับ Legacy/File configs ห้ามใส่ dummy ID Config Version เดิมที่ไม่มี `destination` ยังคงตีความเป็น `LegacyTable` โดยไม่ rewrite persisted JSON

### 4.3 Destination Module seam

Interface ภายนอกที่เสนอ:

```csharp
DestinationCompilationResult Compile(
    DestinationDefinition destination,
    TargetSchema targetSchema);

ValueTask<DeliveryAttemptResult> DeliverAsync(
    CompiledDestination destination,
    DeliveryEnvelope envelope,
    CancellationToken cancellationToken);
```

Adapters ที่ seam นี้:

- `LegacyTableDestinationAdapter` ครอบ `NormalizedRowWriter` และ reconciliation เดิม
- `ApiContractDestinationAdapter` ส่ง HTTP ผ่าน server-owned endpoint/auth profile
- `JsonFileDestinationAdapter` stage/reconcile canonical documents ต่อ row และส่งสัญญาณ file-level finalization
- test adapter แบบ in-memory สำหรับ contract tests

Interface ต้องซ่อน SQL, HTTP, filesystem, authentication, transaction และ retry classification จาก Transformation Module Registry ต้องตรวจ declared kinds กับ registrations ตอน startup และ fail fast เมื่อขาดหรือซ้ำ

### 4.4 Contract Registry Module

```csharp
ValueTask<ContractSnapshotResult> ResolveAsync(
    string apiDestinationProfileId,
    ContractReference contract,
    CancellationToken cancellationToken);
```

ความรับผิดชอบภายใน Module:

1. resolve schema endpoint เช่น `GET /api/pa/proposals/schema/{version}` จาก profile
2. authenticate request
3. fetch schema
4. validate supported JSON Schema subset
5. resolve local references
6. canonicalize schema
7. calculate SHA-256
8. derive typed path catalog
9. return snapshot, hash และ path catalog

Runtime processing ไม่เรียก Interface นี้

`JsonFile` เรียก Contract Registry Module ด้วย Contract Reference เช่นกัน แต่ resolve ผ่าน File Destination Profile ที่ไม่มี delivery endpoint/authentication จากนั้น snapshot schema, bindings และ hash ลง Config Version เหมือน `ApiContract`

### 4.5 Normalized target schema model

ทั้ง Legacy Table definition และ JSON Schema ต้อง compile เป็น model กลาง:

```text
TargetSchema
└─ Root: TargetNode
   ├─ kind: Scalar | Object | Array
   ├─ name
   ├─ pathTokens
   ├─ required
   ├─ scalarType
   ├─ constraints
   ├─ properties[]
   └─ itemSchema
```

LegacyTable ถูกแปลงเป็น root object ที่มี scalar properties ชั้นเดียว ApiContract ถูกแปลงเป็น tree เต็มจาก Contract Snapshot Transformation compiler จึงไม่ต้องรู้ว่า schema มาจากตารางหรือ JSON Schema

### 4.6 Transformation Program v2

```json
{
  "languageVersion": 2,
  "semanticsProfile": "sqlserver-legacy-v1",
  "outputs": [
    {
      "outputKey": "proposal",
      "fields": [
        {
          "targetPath": "proposal.insured.fullName",
          "value": {
            "kind": "trim",
            "value": { "kind": "source", "field": "ccust_name" }
          }
        }
      ],
      "arrays": [
        {
          "targetPath": "proposal.beneficiaries[]",
          "emptyItemPolicy": {
            "kind": "skipWhenAllEmpty",
            "trim": true,
            "emptyTextValues": ["", "-"]
          },
          "sequencePath": "seq",
          "items": [
            {
              "sourceSlot": 1,
              "fields": [
                {
                  "targetPath": "fullName",
                  "value": { "kind": "source", "field": "cBenf_1" }
                },
                {
                  "targetPath": "relationship",
                  "value": { "kind": "source", "field": "cRelate_1" }
                }
              ]
            },
            {
              "sourceSlot": 2,
              "fields": [
                {
                  "targetPath": "fullName",
                  "value": { "kind": "source", "field": "cBenf_2" }
                },
                {
                  "targetPath": "relationship",
                  "value": { "kind": "source", "field": "cRelate_2" }
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

`targetPath` ภายใน `items[].fields` เป็น relative path ต่อ array item schema ส่วน `sequencePath` เป็น relative integer path และถูกเติมจาก `sourceSlot`

### 4.7 Document Assembler rules

Document Assembler เป็น implementation ภายใน Transformation Module ไม่ใช่ destination adapter:

1. evaluate scalar Field Assignments
2. insert typed leaf values ตาม compiled path tokens
3. create object/array containers เมื่อจำเป็น
4. evaluate Array Groups ตามลำดับ config
5. ตรวจ business fields ของแต่ละ item ตาม Empty Item Policy
6. skip item ที่ empty ทั้งหมด
7. inject sequence จาก Source Slot
8. compact emitted items โดยไม่เปลี่ยน sequence
9. validate type/constraints ของ emitted mapped paths และ required paths ที่ไม่ใช่ System-bound Path
10. return immutable Mapped Document หรือ path-based diagnostics

### 4.8 Delivery persistence

ตารางระบบที่เสนอสำหรับ API mode:

```text
delivery_jobs
├─ id
├─ file_job_id
├─ row_job_id
├─ source_row_id
├─ config_version_id
├─ output_key
├─ destination_kind
├─ contract_reference
├─ schema_hash
├─ payload_json
├─ payload_hash
├─ idempotency_key
├─ status: Pending | Delivering | Delivered | RetryableFailed | PermanentFailed
├─ attempts
├─ next_attempt_at
├─ http_status
├─ correlation_id
├─ last_error_redacted
├─ created_at
└─ delivered_at
```

Required unique key:

```text
UNIQUE(config_version_id, file_job_id, source_row_id, output_key)
```

payload ถูก persist เพื่อให้ retry ใช้ bytes เดิม ห้าม rebuild จาก schema/config ล่าสุด

### 4.9 API Destination Profile

```text
api_destination_profiles
├─ id
├─ name
├─ base_url
├─ delivery_path_template
├─ schema_path_template
├─ http_method: POST
├─ auth_profile_reference
├─ timeout_seconds
├─ allowed_headers
├─ payload_bindings
├─ enabled
└─ updated_at
```

`payload_bindings` เป็น server-owned metadata ที่ผูก runtime value source กับ Contract Path เช่น `ContractReference`, `FileJobId`, `SourceRowId`, `SourceRowNumber`, `ConfigVersionReference` และ `OutputKey` โดยไม่ผูกชื่อ JSON กลางกับชื่อ path ของ partner รายใดรายหนึ่ง เมื่อสร้าง Config Version ระบบต้อง snapshot bindings ที่ใช้จริงพร้อม schema และ validate ว่า destination path มีอยู่และ type compatible

Mapping Config อ้าง profile ด้วย logical ID เท่านั้น credential อยู่ใน secret provider และ Contract Registry/ApiContract adapters เป็นผู้ resolve

### 4.10 Payload Binding Module

Interface ภายในที่เสนอ:

```csharp
PayloadBindingResult BindAndValidate(
    CompiledPayloadBindings bindings,
    MappedDocument mappedDocument,
    DeliveryContext context,
    TargetSchema targetSchema);
```

`DeliveryContext` เปิดเผย runtime values ด้วยชื่อกลาง เช่น `ContractReference`, `FileJobId`, `SourceRowId`, `SourceRowNumber`, `ConfigVersionReference` และ `OutputKey` ส่วน binding snapshot เป็นผู้แปลงชื่อกลางเหล่านี้ไปยัง Contract Path ของแต่ละ profile Module นี้ต้องซ่อน path insertion, type conversion, conflict detection และ final required/schema validation ไว้หลัง interface เดียว และคืน immutable Delivery Payload หรือ diagnostics โดยไม่ทำ HTTP/SQL side effect

ตัวอย่าง binding metadata เชิงโครงสร้าง (ไม่ใช่ PA MTLS global default):

```json
{
  "source": "FileJobId",
  "targetPath": "fileJobId",
  "conversion": "string"
}
```

### 4.11 File Destination Profile and export persistence

```text
FileDestinationProfile
├─ id: string
├─ outputRoot: absolute server-owned path (never returned to clients)
├─ encryptionPolicy: volume-encrypted
├─ quotaBytes
├─ minimumFreeBytes
├─ enabled
└─ updatedAt

json_export_jobs
├─ id / file_job_id / config_version_id / profile_id / artifact_revision
├─ status: Pending | Finalizing | Published | Blocked | RetryableFailed
├─ relative_temp_path / relative_final_path
├─ object_count / byte_count / sha256
├─ lease_owner / lease_expires_at / attempt_count
├─ last_error_redacted
├─ previous_export_job_id
├─ created_at / published_at / payload_expires_at / artifact_expires_at / audit_expires_at
└─ UNIQUE(file_job_id, config_version_id, artifact_revision)

json_export_items
├─ export_identity / artifact_revision / file_job_id / config_version_id
├─ source_row_id / row_number / output_key / output_ordinal
├─ encrypted_payload / nonce / tag / encryption_key_reference / payload_sha256
├─ created_at / expires_at
└─ UNIQUE(file_job_id, config_version_id, artifact_revision, source_row_id, output_key)
```

`export_identity` และ `artifact_revision` ถูก allocate/pin ก่อน Row Jobs ของ processing attempt เริ่ม Payload canonicalization เกิดหลัง Payload Binding/final contract validation แต่ก่อน encryption การ retry ต้อง decrypt bytes เดิม ไม่ rebuild จาก current config/schema `previous_export_job_id` เป็น audit link ของ reprocess revision และ outbox finalization ต้อง unique ต่อ export identity

## 5. Frontend Design

### 5.1 Navigation and flow

หน้า config ใช้ flow เดียวแต่เปลี่ยน controls ตาม Destination:

```text
┌─ Mapping Configuration ─────────────────────────── Version draft ┐
│ [1 Setup] [2 Input] [3 Destination] [4 Mapping] [5 Preview]     │
└──────────────────────────────────────────────────────────────────┘
```

Prototype Variant C เป็น design evidence สำหรับตำแหน่งและ interaction เท่านั้น Production workbench ต้อง reuse API/editor/validation จริง รองรับสาม destination และห้าม import runtime code จาก `/prototype/file-to-json`

### 5.2 Setup and destination selection

```text
Config name       [PA Proposal________________________]
Input folder      [pa-proposal-in____________________]
Source Table      [pa_proposal_source______________▼] [Create]

Destination
( ) Legacy Table
(•) Whole file · JSON file
( ) One record at a time · API Contract
```

เมื่อเลือก `ApiContract`:

```text
API profile       [PA Proposals API________________▼]
Contract          [pa-proposal_____________________▼]
Version           [1_______________________________▼]
Schema status     Ready · SHA-256 7d4f... · fetched just now
Delivery endpoint POST /api/pa/proposals

[Refresh schema preview]
```

- ไม่มี Target/Normalized Table selector
- ไม่มีปุ่มสร้าง Normalized Table
- schema refresh มีผลเฉพาะ draft และสร้าง snapshot ใหม่ตอน Create Version

เมื่อเลือก `LegacyTable`:

```text
Normalized Table  [normalized_pa_proposal__________▼] [Create]
Delivery behavior Existing upsert + reconciliation
```

เมื่อเลือก `JsonFile`:

```text
File profile      [Operations JSON export____________▼]
Contract          [pa-proposal_______________________▼]
Version           [1_________________________________▼]
Relative folder   [pa/proposals________________________]
File name         [{sourceBaseName}.{fileJobId}.r{artifactRevision}.json]
Formatting        (•) Compact  ( ) Indented
Output policy     UTF-8 no BOM · array · fail whole file · read-only
```

UI แสดงเพียง logical profile/relative path และ read-only policy summary ห้ามแสดง absolute Output Root, encryption key หรือ filesystem credential เมื่อสลับ destination ต้อง clear fields ของชนิดก่อนหน้าใน draft ก่อน submit

### 5.3 Contract path browser

```text
Search target path [insured name____________________________]

Contract pa-proposal/1 · schema 7d4f...
▼ proposal                                      object
  ▼ insured                                     object
    ○ fullName     proposal.insured.fullName    string · required
    ○ birthDate    proposal.insured.birthDate   date
  ▼ beneficiaries                              array<object>
    ○ fullName     proposal.beneficiaries[].fullName     string
    ○ relationship proposal.beneficiaries[].relationship string
    ○ seq          proposal.beneficiaries[].seq          integer
```

Object/array rows ใช้ expand/collapse และเลือกไม่ได้ Leaf rows แสดง full path, type, required, constraints และ description จาก snapshot

System-bound leaves แสดงไอคอน lock พร้อม runtime source และเลือกใน mapping editor ไม่ได้ เช่น `fileJobId ← FileJobId` ส่วน mapped leaves จึงเปิดให้เลือก Source/expression การแบ่งนี้มาจาก binding snapshot ไม่ใช่การตรวจชื่อ path ใน browser

### 5.4 Scalar mapping editor

```text
Source / Expression       → Contract path                         Type
ccust_name                → proposal.insured.fullName             string
convert(dBirth, date)     → proposal.insured.birthDate            date
CASE ...                  → proposal.policy.active                boolean
```

กฎเดิมทั้งหมด เช่น sourceWhen, emitWhen, CASE, trim, convert, lookup และ output branches ยังอยู่ โดยเปลี่ยน target selector เป็น Contract Path เฉพาะ API mode

### 5.5 Array Group editor

```text
Array group: Beneficiaries
Target array       proposal.beneficiaries[]      array<object>
Sequence path     seq                            integer
Empty item rule   Skip when all business fields are null/blank/"-"

┌─────────────┬──────────────┬──────────────────┬─────────────────────┐
│ Source slot │ Item field   │ Source/expression│ Target item path    │
├─────────────┼──────────────┼──────────────────┼─────────────────────┤
│ 1           │ Name         │ cBenf_1          │ fullName            │
│ 1           │ Relationship │ cRelate_1        │ relationship        │
├─────────────┼──────────────┼──────────────────┼─────────────────────┤
│ 2           │ Name         │ cBenf_2          │ fullName            │
│ 2           │ Relationship │ cRelate_2        │ relationship        │
└─────────────┴──────────────┴──────────────────┴─────────────────────┘

[+ Add source slot]  [+ Add item field]
```

UI แสดง derived sequence แบบ read-only:

```text
slot 1 → seq 1
slot 2 → seq 2
```

### 5.6 Preview

Preview ต้องมี tabs:

```text
[Input] [Source row] [Mapped document] [Delivery request] [Diagnostics]
```

ApiContract preview แสดง:

- Contract Reference และ schema hash
- endpoint profile/method/path โดยไม่แสดง credential
- headers ที่ไม่เป็น secret รวม Idempotency-Key ตัวอย่าง
- nested JSON request body
- System-bound fields และ runtime source แบบ read-only
- item ที่ถูก skip พร้อมเหตุผล
- diagnostics แบบ Source field → Contract Path

LegacyTable preview แสดง flat target values และ SQL destination summary เดิม แต่ไม่แสดง raw SQL

JsonFile preview แสดง ordered final JSON array, object count, estimated UTF-8 bytes, relative display path และ diagnostics โดยไม่ stage/write file; ไม่แสดง HTTP headers/endpoint เพราะ `JsonFile` ไม่ใช่ API batch

### 5.7 Version history and rollback

```text
Version 8  Active   API Contract · pa-proposal/1 · hash 7d4f...
Version 7           Legacy Table · normalized_pa_proposal
Version 9           JSON File · operations-json · pa-proposal/1 · compact

[Open as draft] [Activate] [Compare]
```

Compare ต้องแสดง destination, contract reference/hash, target paths, array groups และ changed expressions

## 6. Runtime Sequences

### 6.1 ApiContract

```text
1. Watcher ตรวจไฟล์ด้วย accepted extensions ของ active Config Version
2. File Intake สร้าง File Job และ pin Config Version
3. Input Module อ่านไฟล์และ Source Row Writer บันทึก raw Source rows
4. Row Job โหลด Source row, Transformation Program และ Contract Snapshot ที่ pin ไว้
5. Transformation Module compile/evaluate และ Document Assembler สร้าง Mapped Document 0..N
6. Payload Binding implementation เติม System-bound Paths ของแต่ละ output และ validate final document กับ snapshot
7. แต่ละ valid output สร้าง Delivery Job + outbox ภายใน database transaction
8. Row Job จบขั้น transformation โดยไม่แตะ Normalized Table
9. Delivery Worker โหลด persisted payload และ ApiContract adapter ส่ง HTTP request
10. สำเร็จบันทึก Delivered; retryable failure schedule ครั้งถัดไป; permanent failure รอ reprocess
```

### 6.2 LegacyTable

```text
1. Intake/Input/Source/Transformation ใช้ flow เดิม
2. Target Schema compile จาก Normalized Table definition แบบ flat
3. LegacyTable adapter ครอบ NormalizedRowWriter
4. Upsert outputs และ reconcile stale outputs ใน transaction เดิม
5. Row Job จบเป็น Done/Filtered/Invalid/Failed ตาม behavior เดิม
```

### 6.3 JsonFile

```text
1. Intake/Input/Source/Transformation ใช้ common flow และ pin Config Version/Profile/Snapshot
2. Payload Binding + final schema validation สร้าง canonical mapped documents 0..N ต่อ Source row
3. JsonFile adapter upsert encrypted staged items และ complete Row Job ใน transaction เดียวกัน
4. เมื่อ latest Row Jobs ครบ terminal และ valid ระบบสร้าง export job/outbox เพียงหนึ่งครั้ง
5. Finalizer claim lease แล้ว stream items ตาม rowNumber/outputOrdinal/outputKey ไป temp fileบน Output Root volume
6. ระหว่างเขียนคำนวณ SHA-256, byte count และ object count; flush/close ก่อน atomic rename
7. rename สำเร็จแล้ว mark Published; crash หลัง rename reconcile ด้วย identity/hash เดิม
8. ไม่มี Normalized write, API Delivery Job หรือ outbound HTTP ตลอด flow
```

### 6.4 Reprocess

- reprocess ต้องเลือก Config Version ได้
- reprocess ไป Legacy version ใช้ reconciliation เดิม
- reprocess ไป API version สร้าง Delivery Jobs ด้วย config version ใหม่และ idempotency keys ใหม่
- reprocess ไป JsonFile version สร้าง artifact revision ใหม่และ audit link โดยไม่ overwrite artifact สำเร็จเดิม
- retry ของ Delivery Job เดิมต้องใช้ idempotency key/payload เดิม
- retry ของ JSON export เดิมต้องใช้ staged payload, profile, filename, formatting และ artifact identity เดิม
- การ resend business document หลัง permanent failure ต้องเป็น explicit reprocess ไม่ใช่แก้ payload ของ Delivery Job เดิม

## 7. Acceptance Criteria

- **AC-R1-01**: Given an ApiContract config, When the version is created, Then no Normalized Table is scaffolded or required
- **AC-R1-02**: Given an ApiContract File Job, When transformation succeeds, Then the Source row is persisted and no insert/update/delete occurs against any Normalized Table
- **AC-R2-01**: Given one LegacyTable config and one ApiContract config, When both are active, Then each processes files through its pinned destination without affecting the other
- **AC-R2-02**: Given a pre-existing version without Destination Definition, When it runs, Then it uses LegacyTable behavior unchanged
- **AC-R3-01**: Given `pa-proposal/1`, When the draft loads, Then selectable target paths come only from its resolved schema path catalog
- **AC-R3-02**: Given a browser submits a path absent from the snapshot, When validation runs, Then creation fails with `UnknownTargetPath`
- **AC-R4-01**: Given schema fetch succeeds, When a Config Version is created, Then Contract Reference, canonical snapshot and SHA-256 are stored immutably
- **AC-R4-02**: Given the registry later changes the same reference, When an existing File Job runs, Then it still uses its pinned snapshot/hash
- **AC-R26-01**: Given mapping to `proposal.insured.fullName`, When evaluated, Then the result contains nested `proposal` and `insured` objects automatically
- **AC-R26-02**: Given a valid fixed index path `[0]`, When evaluated, Then the value appears at array index zero
- **AC-R26-03**: Given fixed index paths with a gap, When validation runs, Then creation fails with `ArrayIndexGap`
- **AC-R27-01**: Given only leaf mappings, When the document is assembled, Then all necessary intermediate objects/arrays are created without separate mappings
- **AC-R28-01**: Given schema leaves for string/integer/number/boolean/date/time, When compatible expressions are mapped, Then JSON values use the required representations
- **AC-R28-02**: Given an object/array container is selected as a scalar target, When validation runs, Then creation fails with `ContainerIsNotAssignable`
- **AC-R29-01**: Given MTLS columns `cBenf_1/cRelate_1` and `cBenf_2/cRelate_2`, When configured as two source slots, Then each non-empty slot produces one beneficiary object
- **AC-R30-01**: Given all business fields in slot 1 are null/blank/whitespace/`-`, When evaluated, Then slot 1 is omitted
- **AC-R30-02**: Given one field in a slot is `0` or `false`, When evaluated, Then that slot is not considered empty
- **AC-R31-01**: Given slot 1 is skipped and slot 2 is emitted, When JSON is assembled, Then the first emitted array element has `seq: 2`
- **AC-API-01**: Given two mapped outputs from one Source row, When delivery is scheduled, Then two independent Delivery Jobs and two HTTP requests are created
- **AC-API-02**: Given a retryable API failure, When retry occurs, Then payload hash and Idempotency Key match the first attempt
- **AC-API-03**: Given HTTP `400`, When delivery completes, Then status is PermanentFailed and automatic retry is not scheduled
- **AC-API-04**: Given HTTP `429` or `503`, When delivery completes, Then bounded retry is scheduled
- **AC-PAY-01**: Given a contract profile with system bindings, When a mapped output is assembled, Then runtime values are inserted into the configured paths before final validation
- **AC-PAY-02**: Given a System-bound Path, When the mapping screen is opened, Then the path is visible as read-only and cannot receive a Source expression
- **AC-PAY-03**: Given a different contract profile whose metadata fields use different paths, When compiled, Then the same runtime value sources bind without PA MTLS-specific code
- **AC-PAY-04**: Given `proposal.extras` disallows additional properties, When an unknown key is configured, Then version creation fails instead of emitting the key
- **AC-PREVIEW-01**: Given preview is run, Then no Source row, target row, Delivery Job or partner HTTP request is created
- **AC-ROLLBACK-01**: Given an API rollout issue, When the previous LegacyTable version is activated, Then new File Jobs use LegacyTable while already pinned jobs retain their original destination
- **AC-JF-COMPAT-01**: Given missing-destination, explicit LegacyTable and ApiContract fixtures, When they round-trip through the three-destination release, Then persisted JSON and database/HTTP side effects remain byte/row/job equivalent
- **AC-JF-CONTRACT-01**: Given each of the three destination shapes, When serialized/deserialized, Then its derived type round-trips and mixed/unknown fields fail at the exact path
- **AC-JF-CONTRACT-02**: Given an existing database before the expand migration, When migration up and application rollback are exercised, Then existing rows are not rewritten and the previous binary can still read them
- **AC-JF-CONTRACT-03**: Given absolute/traversing/reserved/overlong paths or unknown filename tokens, When JsonFile validation runs, Then creation fails before filesystem access
- **AC-JF-SCHEMA-01**: Given one Contract Snapshot and equivalent mapped/runtime values, When ApiContract and JsonFile assemble documents, Then parsed document JSON is equal
- **AC-JF-SCHEMA-02**: Given nested/fixed-index/grouped arrays and a second profile, When JsonFile compiles, Then no endpoint/auth or contract-specific hard-coded path is required and Output Root remains server-only
- **AC-EXP-ADD-01**: Given Integer/Decimal operands including negative and zero, When `add` evaluates, Then promotion and checked result follow EXP-001..004
- **AC-EXP-ADD-02**: Given too few, mistyped, null, overflowing or over-limit `add` operands, When compile/evaluate runs, Then exact-path diagnostics and null propagation match preview/runtime
- **AC-EXP-CONCAT-01**: Given Thai/Unicode Text operands and empty/non-empty separators, When `concat` evaluates, Then the joined Text is deterministic
- **AC-EXP-CONCAT-02**: Given too few, mistyped, null, nested or over-limit `concat` operands, When compile/evaluate runs, Then exact-path diagnostics and null propagation match preview/runtime
- **AC-EXP-COMPAT-01**: Given a config without `add`/`concat`, When it round-trips/evaluates after rollout, Then its JSON and result are unchanged and older clients do not receive unsupported controls
- **AC-JF-ROUTER-01**: Given one adapter per declared destination kind, When production row delivery runs, Then all kinds use the same router call site and preserve the Legacy/API golden baselines
- **AC-JF-ROUTER-02**: Given a missing or duplicate adapter registration, When the process starts, Then startup fails before consuming work
- **AC-JF-STAGE-01**: Given duplicate delivery, crash around commit or parallel last rows, When JsonFile stages results, Then row completion/items are atomic and only one finalization job/outbox exists
- **AC-JF-STAGE-02**: Given indexed finalization lookup and encrypted staged payload, When queried/decrypted, Then ordering/hash match canonical plaintext without logging the body
- **AC-JF-ROW-01**: Given one Source row is reprocessed from 2 → 1 → 0 outputs, When JsonFile adapter reconciles, Then stale items are removed and zero-output remains successful
- **AC-JF-ROW-02**: Given a JsonFile row path, When it completes, Then Normalized writes, API Delivery Jobs and outbound calls are all zero
- **AC-JF-PUBLISH-01**: Given zero/one/many outputs with Thai, null, date, decimal and boolean values, When compact/indented finalization runs, Then valid UTF-8-no-BOM JSON has stable order/count/bytes/hash
- **AC-JF-PUBLISH-02**: Given Row Jobs finish in different orders, When finalization runs, Then artifact bytes are identical and final path becomes visible only after complete atomic rename
- **AC-JF-PUBLISH-03**: Given a configured object/file/free-space limit is exceeded, When finalization is attempted, Then export is Blocked with no partial final artifact and bounded memory usage
- **AC-JF-RECOVERY-01**: Given crashes before rename and after rename-before-DB-update, When lease recovery runs, Then it publishes/reconciles exactly one artifact
- **AC-JF-RECOVERY-02**: Given a final-path collision, When identity/hash match it reconciles; when either differs it Blocks without overwrite
- **AC-JF-RECOVERY-03**: Given retry and reprocess, When they complete, Then retry retains one identity/path/bytes while reprocess creates a monotonic revision and audited link without replacing the prior artifact
- **AC-JF-PREVIEW-01**: Given a JsonFile draft, When preview runs, Then representation/object count/estimated bytes equal the runtime serializer fixture and no database/filesystem state changes
- **AC-JF-PREVIEW-02**: Given each zero/valid/invalid/retry/published state, When File Job status is read, Then additive export fields are deterministic and reveal no absolute path or secret
- **AC-JF-UI-01**: Given Variant C production UI, When the user switches destination and creates/clones a version, Then stale fields are cleared and the prior immutable version remains unchanged
- **AC-JF-UI-02**: Given keyboard/screen-reader/mobile use, When the workbench edits real modules, Then destination-specific controls and live table/object/array preview remain accessible without prototype dependency
- **AC-JF-OPS-01**: Given traversal, reparse-point, collision, quota, disk-full, permission or oversize attacks, When export runs, Then it fails closed without payload/root/secret leakage
- **AC-JF-OPS-02**: Given expiry and cleanup, When hourly cleanup passes the SLA, Then staged/temp/final/audit data follow JF-OPS-003..005 and legal hold prevents deletion
- **AC-JF-OPS-03**: Given activation/finalizer kill switches and Shadow/Canary/Expanded rollout, When paused/resumed/rolled back, Then pinned identity remains unchanged and metrics/alerts contain no PII
- **AC-JF-E2E-01**: Given all four input formats and nested/array/`add`/`concat` mappings, When E2E runs for zero/one/many outputs plus parallel/retry/reprocess/failure injection, Then exactly one valid artifact or none is published as specified
- **AC-JF-E2E-02**: Given the complete three-destination suite, When Gate E runs, Then legacy parity and API exact-byte/idempotency remain green and JsonFile has no legacy/API side effect
- **AC-JF-DOC-01**: Given a new operator follows the runbook, When they create/preview/process/hash/block/retry/rollback a JsonFile version, Then every command/link works without secret or sample PII

### 7.1 Phase 25 plan-to-acceptance traceability

| Step | Acceptance IDs |
|---|---|
| 25.0 | AC-JF-CONTRACT-01, AC-JF-SCHEMA-01, AC-JF-PUBLISH-01, AC-JF-OPS-01 |
| 25.1 | AC-JF-COMPAT-01 |
| 25.2 | AC-JF-CONTRACT-01..03 |
| 25.3 | AC-JF-SCHEMA-01..02 |
| 25.4 | AC-EXP-ADD-01..02, AC-EXP-CONCAT-01..02, AC-EXP-COMPAT-01 |
| 25.5 | AC-JF-ROUTER-01..02 |
| 25.6 | AC-JF-STAGE-01..02 |
| 25.7 | AC-JF-ROW-01..02, AC-JF-STAGE-01 |
| 25.8 | AC-JF-PUBLISH-01..03 |
| 25.9 | AC-JF-RECOVERY-01..03 |
| 25.10 | AC-JF-PREVIEW-01..02 |
| 25.11 | AC-JF-UI-01..02 |
| 25.12 | AC-JF-OPS-01..03 |
| 25.13 | AC-JF-E2E-01..02 |
| 25.14 | AC-JF-DOC-01 |

## 8. Strangler-Fig Migration Plan

### 8.1 Dependency map

```text
FileSystemWatcher
  → FileIntake / File Job / pinned Config Version
  → InputRecordModule
  → SourceRowWriter → dynamic Source Table
  → RowNormalizeHandler
      → TransformationModule
          → TransformationSchema (currently flat NormalizedTable)
          → TransformationCompiler / Evaluator
          → Named Lookup adapters
      → NormalizedRowWriter (current hard dependency)

Proposed seams
  → TargetSchema compiler
      ├─ Legacy Table definition adapter
      └─ Contract Snapshot adapter (ApiContract + JsonFile)
  → Destination Module
      ├─ LegacyTable adapter → NormalizedRowWriter/PostgreSQL
      ├─ ApiContract adapter → Delivery Job/Outbox → Partner HTTP API
      └─ JsonFile adapter → encrypted staging/Outbox → atomic filesystem publish
  → Contract Registry Module → Schema endpoint
```

### 8.2 Phase plan and rollback

| Phase | Change | Validation gate | Rollback |
|---|---|---|---|
| 0 Characterize | Golden tests for current table mappings, retry/reprocess and reconciliation | Existing legacy behavior captured and green; target 80%+ critical-path coverage | No production change |
| 1 Introduce seam | Add Destination/TargetSchema interfaces with LegacyTable adapter only | Golden outputs/SQL effects identical; no status drift | Feature flag forces direct legacy path |
| 2 Contract design-time | Add registry snapshot, path catalog, v2 compiler, nested assembler and preview; delivery disabled | Contract/path/array golden tests pass; preview has no side effects | Disable ApiContract config creation |
| 3 API shadow validation | Create Delivery Jobs but send only to controlled stub/sandbox | Payload contract validation 100%; no PII logging; retry deterministic | Disable delivery worker and discard sandbox jobs |
| 4 Canary | Activate ApiContract for one low-risk Mapping Config/partner | Error rate, latency, duplicates and schema rejects within agreed baseline | Activate previous LegacyTable version; force destination flag to legacy for new jobs |
| 5 Expand | Migrate configs partner-by-partner | Each partner completes one release cycle at 100% successful API delivery | Roll back only affected config to its LegacyTable version |
| 6 Retire per partner | Stop creating new LegacyTable versions for migrated partner | No legacy writes for agreed observation period and business sign-off | Retain adapter/version until retention window ends |

ห้าม dual-send production payload เดียวข้าม destination ทั้งสามเพื่อเปรียบเทียบ เพราะสร้าง business side effects ซ้ำ Shadow validation ใช้ API sandbox/stub หรือ isolated filesystem root ที่ไม่มี business consumer เท่านั้น

### 8.3 Rollout metrics

- Delivery success rate แยกตาม config/contract version
- retryable/permanent failure rate และ HTTP status distribution
- p50/p95/p99 delivery latency
- duplicate/idempotency conflict count
- schema validation failure count แยกตาม Contract Path
- queue age และ pending Delivery Jobs
- LegacyTable vs ApiContract throughput ของ configs ที่เทียบเคียงได้
- Source-to-delivered end-to-end duration

### 8.4 Rollback semantics

- rollback มีผลกับ File Jobs ใหม่ผ่านการ activate previous version หรือ kill switch
- jobs ที่ pin ApiContract แล้วไม่เปลี่ยน destination กลางงาน
- pending API Delivery Jobs สามารถ pause ได้ แต่ห้าม reroute ไป LegacyTable อัตโนมัติ
- external API side effect ที่ Delivered แล้ว rollback ไม่สามารถ undo ได้ ต้องใช้ partner-defined compensation ถ้ามี

### 8.5 Legacy retirement gate

LegacyTable adapter ลบได้เมื่อ:

1. ทุก config ที่ใช้งานจริงมี ApiContract version ที่ผ่าน acceptance
2. ทุก partner รัน API 100% อย่างน้อยหนึ่ง release cycle
3. ไม่มี pending/retry/reprocess ที่อ้าง LegacyTable version นอก retention policy
4. audit และ business owner sign-off
5. rollback window ปิดอย่างเป็นทางการ

## 9. Test Automation Strategy

### 9.1 Characterization safety net

- snapshot Transformation Program v1 inputs/outputs
- golden SQL effects ของ `NormalizedRowWriter`
- retry/reprocess `2→1→0` output reconciliation
- status counts และ row errors เดิม
- old Config Version serialization/deserialization
- tests ต้องผ่านบน unmodified legacy path ก่อนเปิด adapter seam

### 9.2 New tests

| Level | Coverage |
|---|---|
| Unit | path parser, type compatibility, document assembler, empty-item policy, seq preservation, canonical hash, retry classifier |
| Contract | JSON Schema fixtures → path catalog; mapped document validation; partner API request/response contract |
| Integration | snapshot transaction, Delivery Job/outbox, Legacy adapter, HTTP adapter with local stub, idempotent retry |
| End-to-End | input file → Source → nested document → one HTTP request per output; legacy flow unchanged |
| Failure injection | timeout, connection reset, 400, 408, 429, 500, crash before/after response persistence |
| Security | credential redaction, SSRF prevention, path/profile allowlist, payload log redaction |

### 9.3 Required golden cases

- nested object three levels deep
- object parent/child conflict
- array of objects with two complete slots
- slot 1 skipped and slot 2 emitted with `seq: 2`
- partially filled item emitted
- `0` and `false` retained
- all items skipped → required array `[]`
- fixed index contiguous and gap error
- date/time conversion and invalid format
- two output branches → two Delivery Jobs
- retry preserves payload/hash/key
- schema drift after version creation does not affect runtime
- system-bound and mapped values coexist in one final document
- system binding path/type mismatch fails Config Version creation
- second contract profile uses different envelope paths without changing the Transformation Module
- LegacyTable output parity before/after seam

## 10. Examples & Edge Cases

### 10.1 Nested object

Mapping:

```text
ccust_name → proposal.insured.fullName
```

Output:

```json
{
  "proposal": {
    "insured": {
      "fullName": "Somchai Jaidee"
    }
  }
}
```

### 10.2 MTLS beneficiaries

Source:

```text
cBenf_1   = "-"
cRelate_1 = ""
cBenf_2   = "Suda Rakthai"
cRelate_2 = "SISTER"
```

Output:

```json
{
  "proposal": {
    "beneficiaries": [
      {
        "seq": 2,
        "fullName": "Suda Rakthai",
        "relationship": "SISTER"
      }
    ]
  }
}
```

array ถูก compact เหลือหนึ่ง item แต่ `seq` ยังเป็น 2 ตาม Source Slot

### 10.3 Multiple output branches

Source row หนึ่งรายการที่ emit `pmx` และ `ctp` สร้าง Mapped Documents สองรายการ, Delivery Jobs สองรายการ และ HTTP requests สองครั้ง แต่ละรายการมี `outputKey` และ Idempotency Key แยกกัน

### 10.4 Schema drift

ถ้า `GET /api/pa/proposals/schema/1` คืน schema hash ใหม่หลัง version ถูกสร้าง:

- active jobs ยังใช้ old snapshot/hash
- draft refresh แสดง drift warning
- user ต้องสร้าง version ใหม่เพื่อใช้ schema ใหม่
- ห้ามแก้ snapshot ของ version เดิม

### 10.5 Illustrative PA MTLS payload profile

ตัวอย่างนี้ใช้ประกอบการตรวจ coverage เท่านั้น schema และ profile จริงยังเป็น source of truth:

```json
{
  "contractVersion": "pa-proposal/1",
  "fileJobId": "8dca1165-4a9b-4c92-957b-d8b7587f43fe",
  "sourceRowId": "source-row-000001",
  "outputKey": "default",
  "sourceRowNo": 1,
  "mappingConfigVersionId": "mtls-pa@3",
  "proposal": {
    "partnerCode": "MTLS",
    "saleChannel": "BRANCH",
    "sourceReference": "MTLS-20261019-000001",
    "transactionType": "NEW",
    "sourceTransactionType": "N",
    "effectiveDate": "2026-10-19",
    "expiryDate": "2027-10-19",
    "coverStartTime": "00:00:00",
    "campaignCode": "PA2026",
    "packageCode": "PA-01",
    "agentCode": "A001",
    "saleId": "S001",
    "totalPremium": 1200.00,
    "salesBranchCode": "B001",
    "salesBranchName": "Bangkok",
    "insured": {
      "isPolicyholder": true,
      "fullName": "Somchai Jaidee",
      "idNo": "<redacted>",
      "birthDate": "1990-01-15",
      "declaredAge": 36,
      "occupation": "EMPLOYEE",
      "address": "<redacted>"
    },
    "beneficiaries": [
      {
        "seq": 1,
        "fullName": "Suda Jaidee",
        "relationship": "SPOUSE"
      }
    ],
    "extras": {}
  }
}
```

Coverage mapping ของตัวอย่าง:

| Example area | Design treatment |
|---|---|
| `contractVersion`, `fileJobId`, `sourceRowId`, `outputKey`, `sourceRowNo`, `mappingConfigVersionId` | ผู้ดูแล contract/profile เลือกผูกเป็น System-bound Paths; ชื่อและ type จริงมาจาก snapshot/bindings |
| `proposal.*` scalar fields | Mapped Paths ใช้ Source/expression/operator เดิมและ type-check กับ schema |
| `proposal.insured.*` | nested object สร้างจาก leaf mappings ตาม R26-R28 |
| `proposal.beneficiaries[]` | Array Group, empty-item policy และ source-slot sequence ตาม R29-R31 |
| `proposal.extras` | object extension ใช้ได้ตาม `properties`/`additionalProperties` ของ schema เท่านั้น |
| idempotency comment ในตัวอย่าง | เป็น contract hint; key/header ที่ใช้จริงต้องกำหนดและ snapshot ใน destination profile |

ตัวอย่างนี้ไม่ตัดสินชนิด identifier, วิธีสร้าง `sourceRowId`, reprocess identity หรือ policy ของ `extras` แทน contract จริง ประเด็นดังกล่าวยังต้องยืนยันก่อน implementation

### 10.6 Edge cases

- target leaf ถูกลบจาก schema version ใหม่ → version เดิมยังรันได้; draft ใหม่แสดง UnknownTargetPath
- optional object ไม่มี descendant value → omit object
- required object ไม่มี descendant value → Invalid พร้อม path ของ required object
- empty-item fields มีค่าหนึ่งเป็น `-` และอีกค่าเป็น `SON` → emit item
- Source value เป็น ` - ` → empty หลัง trim
- explicit array index `[1]` โดยไม่มี `[0]` → `ArrayIndexGap`
- API ตอบ `202` → Delivered เพราะอยู่ใน `2xx`
- API timeout หลัง partner commit → retry ด้วย Idempotency Key เดิม
- endpoint profile ถูก disable หลัง job queued → pause/fail controlled; ห้ามเปลี่ยน URL จาก config
- schema endpoint unavailable ตอน Create Version → draft preview เก่ายังดูได้แต่สร้าง version ใหม่ไม่ได้

## 11. Risk Register

| Risk | Likelihood | Impact | Mitigation | Rollback trigger |
|---|---|---|---|---|
| Contract Reference เดิมแต่ schema เปลี่ยน | Medium | High | snapshot + hash + drift warning | hash mismatch โดยไม่มี approved version |
| API retry สร้าง proposal ซ้ำ | Medium | Critical | stable idempotency key, partner dedupe contract | duplicate count > 0 |
| API latency ทำ queue ค้าง | Medium | High | async Delivery Jobs, timeout, backoff, queue-age alert | queue age เกิน SLA |
| nested path conflict สร้าง JSON ผิด | Medium | High | compile path tokens, conflict validation, golden tests | contract validation failure สูงกว่า baseline |
| `-` เป็น business value จริง | Low | Medium | empty policy explicit per Array Group และ preview skipped reasons | business owner rejects sample output |
| schema feature ไม่รองรับ | Medium | Medium | supported-subset validation ก่อน version creation | unresolved/ambiguous node พบใน active draft |
| credential/PII รั่วใน log | Low | Critical | secret reference, redaction, no body logging | any secret/PII detection alert |
| Legacy behavior เปลี่ยนจาก seam | Medium | Critical | characterization/golden parity before rollout | any golden mismatch |
| rollback หลัง API side effect | Medium | High | no automatic reroute; compensation plan; idempotency | partner reports irreversible incorrect delivery |
| payload snapshot ใช้พื้นที่สูง | Medium | Medium | retention/size limits/encryption decision | storage growth exceeds threshold |
| partial/duplicate JSON artifact | Low | Critical | encrypted staging, lease, hash reconciliation, same-volume atomic rename | partial/duplicate count > 0 |
| Output Root traversal/reparse escape | Low | Critical | canonical containment, reparse rejection, ACL and hostile-path tests | any path outside configured root |
| disk/quota exhaustion | Medium | High | 256 MiB artifact cap, two-copy free-space preflight, quota/queue alerts | free space below two artifact caps or any partial publish |
| JSON payload/absolute path รั่ว | Low | Critical | no download API, relative display path, no body logging/metrics | any payload/root detection alert |

Phase 24 API thresholds อยู่ใน `docs/phase-24-delivery-decisions.md`; JsonFile thresholds/owners อยู่ใน decision table 12.1 และเป็น Gate E blocker หากเปลี่ยนโดยไม่มี owner approval

## 12. Dependencies & External Integrations

### External systems

- **EXT-001**: Partner Contract Schema Endpoint — คืน JSON Schema ตาม contract version
- **EXT-002**: Partner Delivery Endpoint — รับหนึ่ง proposal ต่อ request และต้องมี idempotency semantics
- **EXT-003**: PostgreSQL — Source Tables, config snapshots, legacy targets, jobs/outbox/delivery audit
- **EXT-004**: Existing message broker — File Import, Row Transformation และ API Delivery messages
- **EXT-005**: Secret provider — credentials ของ schema/delivery endpoints
- **EXT-006**: Server-owned NTFS Output Root — temp/final JSON files บน volume เดียวกันพร้อม volume encryption และ ACL

### 12.1 Resolved decisions before Phase 25 implementation

Phase 24 API decisions (authentication, idempotency, timeout/retry, schema subset, encryption/retention, resend/reprocess, identifiers, extension allowlist และ rollout thresholds) ถูกปิดเมื่อ 2026-10-20 ใน `docs/phase-24-delivery-decisions.md` และยังมีผลโดยไม่เปลี่ยนแปลง ตารางนี้ปิด Required Decisions ของ `JsonFile`:

| Decision | Approved value | Owner | Decision date |
|---|---|---|---|
| Storage kind | Server-owned NTFS filesystem; local path สำหรับ single Worker หรือ mounted shared NTFS path ที่ให้ same-volume atomic rename semantics; object storage ไม่อยู่ใน v1 | Platform / Operations | 2026-10-21 |
| Output Root/profile owner | `FileDestinationProfiles` ระบุ logical ID → absolute Output Root/quota/encryption policy; Platform/Ops เป็นเจ้าของและ browser เห็นเฉพาะ logical ID/relative display path | Platform / Operations | 2026-10-21 |
| Input/workbook limits | 10 MiB input, 10,000 parsed rows, 256 columns, 32,768 characters/cell; preview 1 MiB, 50 records, 10 seconds | Platform / Operations | 2026-10-21 |
| Output limits | 32 outputs/row, 320,000 objects/artifact, 1 MiB canonical JSON/object, 256 MiB final artifact; fail whole fileก่อน publishเมื่อเกิน | Platform / Operations | 2026-10-21 |
| Temp/final volume | temp และ final อยู่ใต้ profile root บน NTFS volume เดียวกัน; ต้อง preflight free spaceสำหรับ temp+final และใช้ atomic rename | Platform / Operations | 2026-10-21 |
| Staged payload retention/encryption | AES-256-GCM ด้วย server-owned key reference; expiry 30 วันหลัง terminal; cleanup รายชั่วโมงและเสร็จใน 24 ชั่วโมงหลัง expiry | Security / Operations | 2026-10-21 |
| Final artifact retention/encryption | เก็บ 30 วันหลัง Published; volume encryption (BitLocker/platform-managed) + ACL; cleanup ภายใน 24 ชั่วโมงหลัง expiry; audit metadata 365 วัน | Security / Operations / Business | 2026-10-21 |
| Filename tokens/default | allowlist `{sourceBaseName}`, `{fileJobId}`, `{configVersion}`, `{configVersionId}`, `{artifactRevision}`; default `{sourceBaseName}.{fileJobId}.r{artifactRevision}.json`; Windows-safe result ≤180 chars | Platform / Operations | 2026-10-21 |
| Collision policy | create-no-overwrite; identity/hash ตรง reconcile ได้, ต่างกัน Block และ alert | Platform / Operations | 2026-10-21 |
| Retry/reprocess | retry ใช้ identity/revision/path/bytesเดิม; reprocess สร้าง monotonic artifact revision ใหม่พร้อม link ไปของเดิมและไม่ overwrite | Business / Contract / Operations | 2026-10-21 |
| Formatting | JSON array, deterministic target property order, UTF-8 no BOM; `compact` default และ `indented` เป็น pinned option; invariant JSON scalars | Contract / Business | 2026-10-21 |
| Error policy | fail whole file; Invalid/Failed/oversize row ทำ export Blocked และไม่มี final artifact; filtered/zero-output สำเร็จและอาจ publish `[]` | Business / Contract | 2026-10-21 |
| Download authorization | ไม่มี HTTP download endpoint ใน v1; Output Root ACL อนุญาตเฉพาะ Operations service account และ authorized downstream consumer | Security / Operations | 2026-10-21 |
| Cleanup SLA/legal hold | sweeper รายชั่วโมง, ลบภายใน 24 ชั่วโมงหลัง expiry; legal hold pause deletion โดยเก็บ audit reason/owner/until | Security / Operations / Legal | 2026-10-21 |
| Rollout ownership | Business/Contract อนุมัติ profile+config; Platform/Ops เปิด Shadow/Canary/Expanded และดู kill switch/alerts; Security อนุมัติ ACL/encryption/key lifecycle | Business / Contract / Platform / Operations / Security | 2026-10-21 |
| Rollback | ปิด new JsonFile activation และ pause finalizer; activate prior versionเฉพาะ File Jobs ใหม่; pinned JsonFile jobsคง destinationเดิมหรือ pause ห้าม reroute | Platform / Operations / Business | 2026-10-21 |

## 13. Validation Criteria

ก่อนเริ่ม implementation ต้องผ่านทุกข้อ:

- requirements R1-R4 และ R26-R31 trace ไปยัง contract, UI และ acceptance tests ได้
- requirements เดิมและ JF/EXP requirements trace ไปยัง acceptance IDs ใน 1.2/7.1 ได้
- Destination Definition เป็น discriminated union สามแบบและ config เก่า default เป็น LegacyTable โดยไม่ rewrite
- ApiContract flow ไม่มี Normalized Table ID, DDL หรือ NormalizedRowWriter call
- JsonFile flow ไม่มี Normalized write, API Delivery Job หรือ outbound HTTP และไม่ใช่ HTTP batch
- TargetSchema/Delivery Module interfaces ซ่อน schema source และ delivery implementation จาก Transformation caller
- Contract Snapshot/hash เป็น immutable runtime source of truth
- wildcard/fixed-index/Array Group semantics ไม่มี ambiguity
- Empty Item Policy และ sequence preservation มี golden examples
- retry ใช้ persisted payload และ idempotency key เดิม
- JSON retry/reprocess/collision/recovery ใช้ identity/hash/revision semantics เดียวตาม 3.13
- decision table 12.1 มี approved value, owner และ date ครบโดยไม่มี Required Decision ค้าง
- LegacyTable characterization suite ผ่านก่อนเปิด API adapter
- ทุก migration phase มี validation gate และ rollback ที่ไม่ reroute pinned jobs
- UI ไม่รับ raw URL, credential, raw SQL หรือ arbitrary Contract Path
- PA MTLS payload คงสถานะ illustrative profile; core modules ไม่มี hard-coded PA field/path
- System-bound Path metadata ถูก snapshot และตรวจ path/type กับ Contract Snapshot

## 14. Related Specifications / Further Reading

- [API Destination Profile Administration Design](spec-design-api-destination-profile-administration.md)
- [Historical File-to-JSON Reliability Reference](spec-design-file-to-json-export.md)
- [Web Config Plan](../docs/web-config-plan.md)
- [Domain Context](../CONTEXT.md)
- [Current Program Workflow](../current-program-workflow.md)
- [Phase 19 Golden Cases](../docs/phase-19-golden-cases.md)
