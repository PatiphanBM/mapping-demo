---
title: File-to-JSON Mapping and Export Design
version: 0.2
date_created: 2026-10-19
last_updated: 2026-10-19
owner: Mapping Demo Team
tags: [design, mapping, input, excel, json, web]
status: Superseded
superseded_by: spec-design-contract-api-legacy-delivery.md
---

# Introduction

> **Superseded on 2026-10-19:** Requirement ล่าสุดยกเลิก `JsonFile` destination และกำหนดให้เลือก `ApiContract` หรือ `LegacyTable` ต่อ Mapping Config โปรดใช้ [Contract API and Legacy Table Delivery Design](spec-design-contract-api-legacy-delivery.md) เป็น authoritative plan เอกสารนี้เก็บไว้เป็นประวัติของแนวคิดก่อนหน้าเท่านั้น

เอกสารนี้ออกแบบ feature ใหม่สำหรับรับไฟล์ต้นทางนามสกุล `.txt`, `.csv`, `.xlsx` และ `.xls` แล้วใช้ Source Table, Target Table Schema, Transformation Program, เงื่อนไข และ operator ชุดเดิมเพื่อสร้างไฟล์ JSON หนึ่งไฟล์ต่อหนึ่ง input file โดย root ของ JSON เป็น array

feature นี้ไม่ insert mapped result ลงตารางธุรกิจปลายทาง การเปลี่ยนแปลงในเอกสารนี้เป็นเพียงข้อเสนอการออกแบบ ยังไม่อนุญาตให้แก้ implementation เดิม

```text
Input file
(.txt/.csv/.xlsx/.xls)
        │
        ▼
Input Layout ──► Source Table ──► Transformation Program
                                      │
                                      ▼
                              Temporary export results
                                      │
                                      ▼
                             One JSON array file
```

## 1. Purpose & Scope

### 1.1 เป้าหมาย

- รับ input file แบบ Text Delimited, Text Fixed Width, CSV และ Excel
- กำหนด Source Table และ Target Table Schema ผ่านหน้าเว็บ
- ใช้ Transformation Program และ operator ที่ระบบรองรับอยู่แล้ว
- ไม่เขียน mapped result ลง Normalized Table สำหรับ config ที่ใช้ JSON destination
- สร้าง JSON หนึ่งไฟล์ต่อ input file โดยมี mapped records เป็น array
- preview ผลก่อนสร้าง Config Version และ activate ได้
- รักษา config และ execution mode เดิมไว้เพื่อไม่ให้ breaking change

### 1.2 ขอบเขต v1

| เรื่อง | ขอบเขต v1 |
|---|---|
| Input extensions | `.txt`, `.csv`, `.xlsx`, `.xls` |
| Text layouts | Delimited และ Fixed Width ที่มีอยู่เดิม |
| Excel layout | เลือก sheet, header row, data-start row และ map column ไป Source field |
| Transformation | ใช้ value expressions, predicates, output branches, named transforms และ named lookups เดิม |
| Destination | JSON file เท่านั้นสำหรับ mode ใหม่ |
| JSON document | root เป็น array หนึ่งไฟล์ต่อ input file |
| Character encoding | UTF-8 ไม่มี BOM |
| Error policy | ถ้ามี Invalid/Failed row จะไม่ publish JSON ทั้งไฟล์ |
| Versioning | Input, transformation และ JSON destination ถูกตรึงใน Config Version |
| JSON import | อยู่นอกขอบเขต ผู้ใช้ยืนยันว่าต้องการ export เท่านั้น |

### 1.3 นอกขอบเขต

- การ import JSON กลับเข้าระบบ
- การเพิ่ม operator ใหม่
- การ execute formula, macro, VBA หรือ external link ใน Excel
- การรวมหลาย input files เป็น JSON ไฟล์เดียว
- การแบ่ง JSON หนึ่ง input file ออกเป็นหลายไฟล์
- การส่ง JSON ไป API, message broker หรือ object storage
- การแก้ไข source code ในขั้นออกแบบนี้

### 1.4 สมมติฐานที่ยืนยันแล้ว

- หนึ่ง input file สร้างหนึ่ง JSON file
- JSON root เป็น array ของ mapped records
- ใช้ operator เดิมก่อน ไม่มี operator ใหม่ใน v1
- คำว่า “import JSON ที่ map แล้ว” ในคำขอแรกให้ตีความเป็น “export JSON หลัง map แล้ว”

### 1.5 สิ่งที่พบจากระบบปัจจุบัน

- `MappingConfig` ผูก input folder, Source Table และ Normalized Table อยู่แล้ว
- `ConfigVersion` เป็นเจ้าของ `InputLayout` และ `TransformationProgram` แบบ immutable
- Input Layout ปัจจุบันรองรับ `delimited` และ `fixedWidth`
- file watcher ปัจจุบันค้นหาเฉพาะ `*.csv`
- snapshot path ปัจจุบันลงท้าย `.csv` แบบ hard-coded
- Transformation Program รองรับหนึ่ง Source row ให้ผล `0..N` output records
- runtime ปัจจุบันเขียนแต่ละ output ลง Normalized Table ผ่าน `NormalizedRowWriter`
- หน้าเว็บปัจจุบันมี Input Layout editor, Transformation editor, preview, validate, version history และ activate อยู่แล้ว

ข้อสรุป: reuse config เดิมได้เกือบทั้งหมด แต่ต้องเพิ่ม Excel Input Layout, accepted extensions, JSON destination และ file-level export finalization

## 2. Definitions

**File-to-JSON Config**: Mapping Config ที่เลือก destination เป็น `jsonFile`

**Source Table**: ตารางจริงที่เก็บค่าดิบจาก input file ทุก field เป็นข้อความตามหลักเดิม

**Target Table Schema**: นิยามชื่อ field, ลำดับ, data type, required, length, precision และ scale ของ JSON output ใช้นิยาม Normalized Table เดิมได้ แต่ JSON mode ห้าม insert mapped result ลงตารางนั้น

**Input Layout**: กติกาอ่าน physical input ไปเป็น Source row มีชนิด `delimited`, `fixedWidth` หรือ `excel`

**Excel Input Layout**: กติกาเลือก worksheet และตำแหน่งแถว/คอลัมน์เพื่ออ่าน Excel ไปเป็น Source row

**Transformation Program**: กฎเดิมที่เปลี่ยน Source row หนึ่งแถวเป็น Target-shaped output `0..N` records

**Output Branch**: ผลลัพธ์แต่ละแขนของ Transformation Program ระบุด้วย `outputKey` และ `emitWhen`

**JSON Destination**: กติกาสร้างชื่อและตำแหน่งไฟล์ JSON หลังทุก Source row ประมวลผลเสร็จ

**Temporary Export Result**: payload ต่อแถวในตารางระบบสำหรับรวมเป็น JSON ภายหลัง ไม่ใช่ตารางธุรกิจปลายทาง

**Publish**: การทำให้ JSON file สมบูรณ์มองเห็นได้ที่ output folder ผ่าน atomic rename

## 3. Requirements, Constraints & Guidelines

### 3.1 Functional requirements

- **REQ-001**: ระบบต้องรับ `.txt`, `.csv`, `.xlsx` และ `.xls` ตาม extensions ที่ตรึงใน active Config Version
- **REQ-002**: หนึ่ง Config Version ต้องเลือก Input Layout ได้หนึ่งชนิดเท่านั้น
- **REQ-003**: Delimited layout ต้องใช้กับ `.csv` หรือ `.txt` ได้
- **REQ-004**: Fixed Width layout ต้องใช้กับ `.txt` ได้
- **REQ-005**: Excel layout ต้องใช้กับ `.xlsx` หรือ `.xls` ได้
- **REQ-006**: Excel layout ต้องระบุ worksheet, header row และ data-start row
- **REQ-007**: ระบบต้อง map Excel column ไป Source column ด้วย header name; UI อาจแสดง column letter เพื่อช่วยผู้ใช้ แต่ persisted contract ใช้ header name
- **REQ-008**: Source values จากทุก input format ต้องเข้าสู่ Transformation Program ผ่าน contract เดียวกัน
- **REQ-009**: JSON mode ต้องใช้ Transformation Program เดิมโดยไม่สร้าง evaluator ชุดใหม่
- **REQ-010**: JSON mode ต้องไม่เรียก `NormalizedRowWriter` และต้องไม่ insert/update/delete mapped result ใน Normalized Table
- **REQ-011**: JSON mode ต้องสร้าง JSON หนึ่งไฟล์ต่อ File Import Job
- **REQ-012**: JSON root ต้องเป็น array และแต่ละสมาชิกต้องตรง Target Table Schema
- **REQ-013**: record ใน JSON ต้องเรียงตาม Source `rowNumber` และตามลำดับ Output Branch ใน Config Version
- **REQ-014**: Source row ที่ถูก `sourceWhen` filter หรือ branch ที่ไม่ผ่าน `emitWhen` ต้องไม่ปรากฏใน JSON
- **REQ-015**: ถ้าทุก Source row ถูก filter ระบบต้อง publish `[]`
- **REQ-016**: ถ้ามี Invalid หรือ Failed row ระบบต้องไม่ publish final JSON และต้องเก็บ diagnostics ใน job status
- **REQ-017**: ระบบต้องเขียน temporary file ใน output filesystem เดียวกับ final file และ atomic rename หลัง serialize สำเร็จ
- **REQ-018**: retry ของ File Export Job เดิมต้องสร้าง final path เดิมและต้องไม่เหลือ duplicate final files
- **REQ-019**: config เดิมที่ไม่มี Destination Definition ต้องทำงานเป็น database destination ตาม behavior เดิม
- **REQ-020**: ผู้ใช้ต้อง clone Config Version เดิมและเปลี่ยน destination เป็น JSON ได้โดยไม่สร้าง Transformation Program ใหม่
- **REQ-021**: preview ต้องแสดง Input row, Source row และ final JSON representation โดยไม่ persist source rows, temporary results หรือ output file
- **REQ-022**: หน้าเว็บต้องมี tab ใหม่ชื่อ `File → JSON`
- **REQ-023**: Source Table และ Target Table Schema ต้องเลือกหรือสร้างได้จาก flow ของ tab ใหม่
- **REQ-024**: Config Version ต้องตรึง accepted extensions และ Destination Definition เพิ่มจาก Input Layout และ Transformation Program เดิม

### 3.2 Operator และ expression ที่ reuse

ไม่มี operator ใหม่ใน v1

Boolean operators:

`eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `isNull`, `isBlank`, `notBlank`, `like`, `in`, `notIn`, `and`, `or`, `not`, `exists`, `notExists`

Value expressions:

`source`, `metadata`, `literal`, `coalesce`, `defaultIfBlank`, `trim`, `case`, `substring`, `convert`, `currentDate`, `call`

### 3.3 Excel rules

- **XLS-001**: `sheetName` ต้องตรงกับ worksheet name แบบ exact match
- **XLS-002**: `headerRow` และ `dataStartRow` เป็น 1-based และ `dataStartRow > headerRow`
- **XLS-003**: header ที่นำมา map ต้องไม่ว่างและต้องไม่ซ้ำหลัง trim
- **XLS-004**: empty row ระหว่างข้อมูลต้องถือเป็นข้อมูลผิดรูปแบบ ไม่ใช้เป็นสัญญาณจบไฟล์
- **XLS-005**: formula ต้องใช้ cached result ที่อยู่ใน workbook เท่านั้น ห้าม execute formula
- **XLS-006**: formula ที่ไม่มี cached result ต้องทำให้ file import ล้มเหลวพร้อมตำแหน่ง cell
- **XLS-007**: macro, VBA และ external link ต้องไม่ถูก execute
- **XLS-008**: password-protected/encrypted workbook ต้องถูกปฏิเสธใน v1
- **XLS-009**: ค่า Excel ต้อง normalize เป็น Source text อย่าง deterministic ดังนี้

| Excel cell | Source text |
|---|---|
| Blank | `null` |
| Text | ข้อความตามค่า cell |
| Boolean | `true` หรือ `false` |
| Integer/Decimal | invariant numeric text ไม่มี thousands separator |
| Date | `yyyy-MM-dd` |
| DateTime | `yyyy-MM-ddTHH:mm:ss` |
| Formula | cached result แล้วใช้กฎชนิดข้อมูลด้านบน |

### 3.4 JSON serialization rules

| Target data type | JSON representation |
|---|---|
| Text | string |
| Date | string รูปแบบ `yyyy-MM-dd` |
| DateTime | string รูปแบบ `yyyy-MM-ddTHH:mm:ss` |
| Decimal | number |
| Integer | number |
| Boolean | boolean |
| Guid | string |
| Optional missing value | `null` |

- **JSN-001**: JSON property name ต้องตรง Target column name รวมตัวพิมพ์เล็ก/ใหญ่
- **JSN-002**: property order ต้องตรง Target column ordinal เพื่อให้ผล deterministic แม้ consumer ไม่ควรพึ่ง order
- **JSN-003**: ทุก target property ต้องปรากฏในแต่ละ object; optional value ที่ไม่มีค่าใช้ `null`
- **JSN-004**: `outputKey` เป็น processing metadata และไม่ใส่ใน JSON object เว้นแต่ผู้ใช้สร้าง target field และ map ค่าเองในอนาคต
- **JSN-005**: output ใช้ UTF-8 ไม่มี BOM และ default เป็น compact JSON
- **JSN-006**: filename ห้ามมาจาก input path โดยไม่ sanitize
- **JSN-007**: output path ต้องอยู่ใต้ server-owned Output Root เท่านั้น

### 3.5 Constraints

- **CON-001**: Config Version ที่ activate แล้ว immutable ตาม behavior เดิม
- **CON-002**: absolute output path ห้ามส่งจาก browser; UI เก็บได้เฉพาะ relative folder ภายใต้ Output Root
- **CON-003**: หนึ่ง config ใช้ layout เดียว หากโครงสร้าง `.csv` และ `.xlsx` ต่างกันต้องใช้คนละ config
- **CON-004**: `.txt` ที่เป็น delimited และ `.txt` ที่เป็น fixed-width ต้องเลือก layout ให้ชัด ห้าม auto-detect
- **CON-005**: ระบบต้องกำหนด file size, worksheet, row และ column limits ก่อน implement Excel reader
- **CON-006**: Preview ต้องมี limit ต่ำกว่า runtime และห้าม persist sample file
- **CON-007**: Target Table เดิมอาจยังมี physical table จากระบบปัจจุบัน แต่ JSON mode ห้ามเขียนข้อมูลลงตารางนั้น

### 3.6 Guidelines

- **GUD-001**: ใช้คำว่า `Target Table Schema` ในหน้า File → JSON เพื่อสื่อว่าเป็น schema ไม่ใช่ database destination
- **GUD-002**: แสดงข้อความช่วยเหลือว่า Excel Sheet/Header/Data start อยู่ในขั้น Input File → Source
- **GUD-003**: แสดง extension compatibility ทันทีเมื่อผู้ใช้เลือก layout
- **GUD-004**: ใช้ preview engine เดียวกับ runtime สำหรับ parsing, transformation และ JSON serialization
- **GUD-005**: เก็บ temporary export results ตาม retention policy และล้างหลัง final output สำเร็จตามระยะเวลาที่กำหนด

## 4. Interfaces & Data Contracts

### 4.1 Config Version v2

Config เดิมต้องอ่านได้ โดย property ใหม่มี backward-compatible defaults

```json
{
  "acceptedExtensions": [".xlsx", ".xls"],
  "inputLayout": {
    "kind": "excel",
    "sheetName": "Customer",
    "headerRow": 3,
    "dataStartRow": 4,
    "fields": [
      { "inputHeader": "Customer No", "sourceColumn": "customer_no" },
      { "inputHeader": "Full Name", "sourceColumn": "full_name" }
    ]
  },
  "transformationProgram": {
    "languageVersion": 1,
    "semanticsProfile": "sqlserver-legacy-v1",
    "outputs": []
  },
  "destination": {
    "kind": "jsonFile",
    "relativeFolder": "customers",
    "fileNameTemplate": "{sourceBaseName}.{fileJobId}.json",
    "root": "array",
    "encoding": "utf-8",
    "indented": false,
    "rowErrorPolicy": "failFile"
  }
}
```

Backward-compatible defaults:

| Missing property | Default for existing version |
|---|---|
| `acceptedExtensions` | `[".csv"]` เพื่อรักษา behavior ปัจจุบัน |
| `destination` | `{ "kind": "database" }` |

### 4.2 Excel Input Layout

```text
ExcelInputLayout
├─ kind: "excel"
├─ sheetName: string
├─ headerRow: integer (1-based)
├─ dataStartRow: integer (1-based)
├─ blankRowPolicy: "error"
└─ fields[]
   ├─ inputHeader: string
   └─ sourceColumn: string
```

ไม่เก็บ column letter เป็น contract หลัก เพราะผู้ใช้สามารถแทรกคอลัมน์ใน workbook ได้ แต่ header name ยังบอกเจตนาของ mapping ได้ชัดกว่า

### 4.3 Destination Definition

```text
DestinationDefinition
├─ DatabaseDestination
│  └─ kind: "database"
└─ JsonFileDestination
   ├─ kind: "jsonFile"
   ├─ relativeFolder: string
   ├─ fileNameTemplate: string
   ├─ root: "array"
   ├─ encoding: "utf-8"
   ├─ indented: boolean
   └─ rowErrorPolicy: "failFile"
```

Supported filename tokens in v1:

- `{sourceBaseName}`
- `{fileJobId}`
- `{configVersion}`

default template คือ `{sourceBaseName}.{fileJobId}.json` เพื่อไม่ให้ input คนละ job overwrite กัน

### 4.4 Temporary export persistence

ตารางระบบที่เสนอ:

```text
row_export_results
├─ file_job_id
├─ source_row_id
├─ row_number
├─ output_key
├─ output_ordinal
├─ config_version_id
├─ payload_json
├─ created_at
└─ UNIQUE(source_row_id, output_key, config_version_id)
```

ตารางนี้เป็น execution state ไม่ใช่ Target Table การ retry ต้อง upsert key เดิม และการ reprocess ต้อง reconcile stale output branches ตามหลักเดิมก่อน export ใหม่

Field Job ที่ควรเพิ่ม:

```text
file_jobs
├─ export_status: NotRequested | Pending | Exporting | Exported | ExportFailed
├─ output_path: nullable
├─ output_hash: nullable
├─ exported_at: nullable
└─ export_error: nullable
```

### 4.5 Proposed API surfaces

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/mapping-capabilities` | เพิ่ม `excel`, extensions และ JSON destination capability |
| `POST` | `/mapping-configs/{id}/versions/validate` | validate draft contract เดิมที่เพิ่ม destination |
| `POST` | `/mapping-configs/{id}/versions/preview-file` | multipart preview สำหรับ text และ binary Excel |
| `POST` | `/mapping-configs/{id}/versions` | สร้าง immutable version |
| `POST` | `/mapping-configs/{id}/versions/{versionId}/activate` | activate ตามเดิม |
| `GET` | `/file-jobs/{id}` | เพิ่ม export status/path/error |
| `POST` | `/file-jobs/{id}/retry-export` | retry เฉพาะขั้น publish โดยใช้ผลเดิมที่ valid |

Preview request แบบ multipart:

- part `draft`: JSON ของ Config Version draft
- part `sample`: `.txt`, `.csv`, `.xlsx` หรือ `.xls`
- response: parsed source rows, transformation results, diagnostics และ JSON preview

### 4.6 Runtime sequence

```text
1. Watcher ตรวจเฉพาะ acceptedExtensions ของ active version
2. File Intake สร้าง File Job และ pin Config Version
3. File Import Worker snapshot ไฟล์โดยรักษานามสกุลเดิม
4. Input Record Module อ่าน Text/CSV/Excel เป็น Source rows
5. Source Row Writer บันทึก Source Table และสร้าง Row Jobs ตาม flow เดิม
6. Transformation Module ประเมิน conditions/operators เดิม
7. JSON mode upsert ผลลง row_export_results แทน Normalized Table
8. เมื่อ Row Jobs ทุกตัว terminal ระบบตรวจ file-level error policy
9. Export Worker อ่านผลตาม rowNumber/outputOrdinal แล้ว serialize ไป temporary file
10. Export Worker atomic rename เป็น final `.json` และบันทึก output hash/status
```

## 5. Frontend Design

### 5.1 Navigation

เพิ่ม top-level tab ใหม่โดยไม่เปลี่ยนหน้า config เดิมทันที

```text
┌─────────────────────────────────────────────────────────────────────┐
│ Mapping Demo                                                       │
│ [Mapping Configs]  [File → JSON]  [Tables / Schemas]               │
└─────────────────────────────────────────────────────────────────────┘
```

`Mapping Configs` คือ flow เดิม ส่วน `File → JSON` แสดงเฉพาะ config ที่ destination เป็น JSON file

### 5.2 File → JSON list

```text
┌─ File → JSON Configurations ──────────────────── [ + New config ] ┐
│ Search [________________]  Status [All▼]  Input type [All▼]       │
├───────────────────────────────────────────────────────────────────┤
│ Customer Excel Export                          Active · Version 3 │
│ .xlsx/.xls  →  customer_source  →  customer_json_schema          │
│ Output: customers/{sourceBaseName}.{fileJobId}.json              │
│ Last run: Exported  245 records                 [Open] [History]  │
├───────────────────────────────────────────────────────────────────┤
│ Policy TXT Export                              Draft only         │
│ .txt fixed-width → policy_source → policy_json_schema            │
│                                                   [Open] [Delete] │
└───────────────────────────────────────────────────────────────────┘
```

### 5.3 Create/Edit screen

ใช้ tabs ตามลำดับงาน ผู้ใช้กลับไปแก้ tab ก่อนหน้าได้ แต่สร้าง version ได้เมื่อ validate ผ่านเท่านั้น

```text
┌─ Customer Excel Export ───────────────────────────── Version draft ┐
│ [1 Setup] [2 Input File] [3 Mapping] [4 JSON Output] [5 Preview]  │
├────────────────────────────────────────────────────────────────────┤
│ เนื้อหาของ tab                                                     │
│                                                                    │
├────────────────────────────────────────────────────────────────────┤
│ [Save draft locally]                [Validate] [Create version]    │
└────────────────────────────────────────────────────────────────────┘
```

#### Tab 1 — Setup

```text
Config name       [Customer Excel Export________________]
Input folder      [customer-in__________________________]
Source Table      [customer_source___________________▼] [Create]
Target Schema     [customer_json_schema______________▼] [Create]
Destination       [JSON File_________________________▼]

ⓘ Target Schema กำหนดชื่อและชนิด property ของ JSON
  ระบบจะไม่ insert mapped result ลงตารางปลายทางใน JSON mode
```

Source Table creation ยังคงสร้าง physical Source Table ตาม behavior เดิม ส่วน Target Schema สามารถ reuse Normalized Table definition เดิมได้

#### Tab 2 — Input File

```text
File extensions   [✓ .xlsx] [✓ .xls] [ ] .csv [ ] .txt
Layout type       [Excel workbook ▼]

Sample workbook   [Choose file]  customer-sample.xlsx
Worksheet         [Customer ▼]
Header row        [3]
Data starts row   [4]

Column preview
┌─────┬────────────────────┬────────────────────────┬──────────────┐
│ Col │ Input header       │ Source column          │ Sample       │
├─────┼────────────────────┼────────────────────────┼──────────────┤
│ A   │ Customer No        │ customer_no         ▼ │ C0001        │
│ B   │ Full Name          │ full_name           ▼ │ Somchai ...  │
│ C   │ Register Date      │ register_date_raw   ▼ │ 2026-10-19   │
└─────┴────────────────────┴────────────────────────┴──────────────┘
```

ตำแหน่ง Excel ใน pipeline:

```text
Workbook bytes
   → เลือก Worksheet
   → อ่าน Header row
   → อ่านตั้งแต่ Data-start row
   → map Excel columns เป็น Source fields
   → บันทึก Source Table
   → Transformation Program
```

เมื่อเลือก `.csv` หรือ delimited `.txt` ให้ reuse Delimited editor เดิม เมื่อเลือก fixed-width `.txt` ให้ reuse Fixed Width editor เดิม

#### Tab 3 — Mapping

reuse Transformation editor เดิมทั้งหน้าจอ:

- Common source filter (`sourceWhen`)
- Output Branch tabs และ `emitWhen`
- Direct source, constant, current date, trim, substring, convert, coalesce, default-if-blank และ CASE
- named transform และ named lookup
- operator เดิมทั้งหมด
- Target column grid เปลี่ยน label จาก `Normalized target` เป็น `JSON target field` เมื่ออยู่ใน JSON mode

```text
Output branch: [main] [+ Add output]

JSON target field        Value / Expression                 Type
orderId                  trim(source.order_no)              Text
amount                   convert(source.amount_raw)         Decimal
isPaid                   CASE status = "PAID" ...           Boolean
```

#### Tab 4 — JSON Output

```text
Output folder     [customers____________________________]
File name         [{sourceBaseName}.{fileJobId}.json_____]
Document root     [Array (fixed in v1)___________________]
Encoding          [UTF-8 without BOM (fixed in v1)_______]
Formatting        (•) Compact  ( ) Indented
On row error      [Fail whole file (fixed in v1)_________]

Example path
<OutputRoot>/customers/customer-20261019.1842.json

Example document
[
  { "customerId": "C0001", "name": "Somchai", "active": true },
  { "customerId": "C0002", "name": "Suda", "active": false }
]
```

#### Tab 5 — Preview & Publish

```text
[Choose sample file] customer-sample.xlsx     [Run preview]

Summary: 10 source rows · 9 outputs · 1 filtered · 0 invalid

[Input] [Source rows] [Mapped rows] [Final JSON]
┌───────────────────────────────────────────────────────────────────┐
│ [                                                               │
│   { "customerId": "C0001", "name": "Somchai" },               │
│   { "customerId": "C0002", "name": "Suda" }                  │
│ ]                                                               │
└───────────────────────────────────────────────────────────────────┘

Diagnostics: No errors
[Validate draft] [Create version] [Activate version]
```

### 5.4 Job status

```text
Import: Imported 10 rows
Mapping: Done 9 · Filtered 1 · Invalid 0 · Failed 0
Export: Exported
Output: customers/customer-20261019.1842.json
SHA-256: 8F3A...
```

หากมี invalid row:

```text
Export: Not published
Reason: 1 row is invalid; rowErrorPolicy is failFile
[View row errors] [Reprocess with another version]
```

## 6. Acceptance Criteria

- **AC-001**: Given a delimited `.csv` config, When a valid file arrives, Then one UTF-8 JSON array file is published and no row is inserted into the Target Table
- **AC-002**: Given a delimited `.txt` config, When the delimiter and headers match, Then Source values and JSON output match the same config run with equivalent CSV data
- **AC-003**: Given a fixed-width `.txt` config, When record rules classify header/detail/trailer, Then only Import records contribute mapped JSON objects
- **AC-004**: Given an `.xlsx` workbook whose header is row 3 and data starts at row 4, When the configured sheet exists, Then the first Source row comes from Excel row 4
- **AC-005**: Given a valid legacy `.xls` file, When it uses the same logical columns as the `.xlsx` fixture, Then both produce equivalent JSON values
- **AC-006**: Given a missing worksheet, encrypted workbook or formula without cached result, When import runs, Then the File Job fails with a precise workbook/cell diagnostic and no final JSON is published
- **AC-007**: Given one Source row that emits two branches, When export completes, Then two JSON objects are written in Transformation Program branch order
- **AC-008**: Given every Source row is filtered, When processing completes successfully, Then the output content is `[]`
- **AC-009**: Given one invalid row, When all Row Jobs reach terminal status, Then no final JSON exists and Export Status explains the invalid row count
- **AC-010**: Given a crash before atomic rename, When retry runs, Then no partial final JSON is visible
- **AC-011**: Given a crash after rename but before database status update, When retry runs, Then the same final path is reconciled without creating a second output file
- **AC-012**: Given an existing Config Version without destination, When it runs after deployment, Then it continues writing to its Normalized Table as before
- **AC-013**: Given an existing version is cloned, When destination changes to `jsonFile`, Then its Input Layout, Transformation Program, conditions and operators remain unchanged
- **AC-014**: Given a JSON preview request, When preview completes, Then database row counts and output folders remain unchanged
- **AC-015**: Given an input file with the wrong extension for its active version, When the watcher scans, Then no File Job is created for that file

## 7. Test Automation Strategy

### 7.1 Test levels

- **Unit**: extension validation, Excel cell conversion, filename tokens, path safety, JSON type serialization และ deterministic ordering
- **Integration**: Excel reader, Source Row Writer, temporary export persistence, export finalizer และ PostgreSQL transaction boundaries
- **End-to-End**: watcher → import → source → transform → JSON publish สำหรับทั้งสี่ extensions
- **Regression**: database destination และ 239 existing regression tests ต้องยังผ่าน

### 7.2 Minimum test matrix

| Input | Case |
|---|---|
| CSV | header/no header, quoted delimiter, blank, invalid row |
| TXT delimited | tab/pipe delimiter, UTF-8 Thai text |
| TXT fixed-width | H/D/T, Thai text, short/long record |
| XLSX | sheet selection, header offset, date/decimal/boolean/formula cache |
| XLS | equivalent logical data, unsupported/encrypted workbook |
| JSON | zero/one/many branches, nulls, types, deterministic order |
| Reliability | retry before/after rename, duplicate input, reprocess |

### 7.3 Test data management

- ใช้ workbook fixtures ขนาดเล็กที่ commit ได้และไม่มีข้อมูลจริง
- สร้าง fixture ที่มี Thai Unicode, null, zero, large decimal, date และ duplicate header
- แยก corrupt/encrypted/oversized fixtures ออกจาก happy-path fixtures
- ตรวจ output ด้วย parsed JSON และ byte-level UTF-8/hash test ไม่เปรียบเทียบเฉพาะข้อความที่จัด format

### 7.4 CI/CD and coverage

- backend unit/integration tests ต้องรันใน pipeline เดิม
- frontend lint, type check และ component tests ต้องครอบคลุม tab navigation และ diagnostic-to-control mapping
- acceptance ต้องยืนยันว่า JSON mode ไม่มี SQL write ไป Target Table
- กำหนด performance baseline หลังระบุ production file-size/row-count limits แล้ว

## 8. Rationale & Context

### 8.1 เหตุผลที่ reuse Transformation Program

Transformation Program ปัจจุบันแยก compile/evaluate ออกจาก persistence อยู่แล้ว ผลลัพธ์คือ `TransformationOutputResult` จึงเปลี่ยนเฉพาะ output adapter จาก `NormalizedRowWriter` เป็น temporary result + JSON finalizer ได้ โดยไม่สร้าง syntax หรือ operator อีกชุด

### 8.2 เหตุผลที่ยังใช้ Target Table definition

compiler ต้องทราบชื่อ field, type, required, length, precision และ scale เหมือนเดิม นิยาม Normalized Table จึงใช้เป็น Target Table Schema ได้ทันที แม้ JSON mode จะไม่เขียนตารางจริง หน้าเว็บต้องใช้คำว่า Target Schema เพื่อลดความสับสน

### 8.3 เหตุผลที่ต้องมี temporary export results

runtime เดิมทำงานทีละ Row Job และอาจ parallel/retry แต่ requirement ใหม่ต้องรวมผลทั้งไฟล์เป็น array เดียว หากเขียน JSON โดยตรงจากแต่ละ Row Job จะเกิด partial file, ลำดับไม่แน่นอน และ race condition การเก็บผลชั่วคราวทำให้ finalizer รวมผลตามลำดับและ publish แบบ atomic ได้

### 8.4 เหตุผลที่ Excel ต้องมี Sheet/Header/Data-start

CSV และ TXT มี record sequence แบบเส้นตรง แต่ workbook อาจมีหลาย worksheet, title/preamble หลายแถว และ header ไม่ได้เริ่มแถวแรก ค่าทั้งสามจึงอยู่ใน Excel Input Layout ซึ่งทำงานก่อน Source Table และก่อน Transformation Program

### 8.5 Migration strategy

1. เพิ่ม contract ใหม่แบบ optional พร้อม defaults สำหรับ config เก่า
2. เพิ่ม Excel compiler/reader และ binary preview โดยยังไม่เปลี่ยน writer เดิม
3. เพิ่ม JSON destination adapter และ temporary result persistence
4. เพิ่ม export finalizer/status/retry
5. เพิ่ม File → JSON tab โดย reuse editors เดิม
6. เปิดใช้ผ่าน explicit `destination.kind = jsonFile` เท่านั้น
7. รัน regression ของ database destination ก่อน activate JSON config แรก

## 9. Dependencies & External Integrations

### External systems

- **EXT-001**: Input filesystem — ตรวจไฟล์ตาม accepted extensions ภายใต้ config folder
- **EXT-002**: Output filesystem — publish JSON ภายใต้ server-owned Output Root
- **EXT-003**: PostgreSQL — Source Table, jobs, config versions และ temporary export results
- **EXT-004**: Existing message broker — dispatch File Import, Row Transformation และ File Export jobs

### Third-party capabilities

- **SVC-001**: Excel workbook reader — ต้องอ่าน `.xlsx` และ legacy `.xls`, stream input ได้, ไม่ execute macro/formula และเปิด strict limits ได้
- **SVC-002**: JSON serializer — ต้อง serialize supported target types แบบ deterministic และ stream ไป output ได้

ห้ามเลือกระบุ package/version ก่อนทำ technical spike และ license/security review โดยเฉพาะ `.xls` ซึ่งใช้ binary workbook format ต่างจาก `.xlsx`

### Infrastructure dependencies

- **INF-001**: Output Root ต้องเขียนได้และควรอยู่ volume เดียวกับ temporary publish directory เพื่อรองรับ atomic rename
- **INF-002**: ต้องมีพื้นที่เพียงพอสำหรับ input snapshot, temporary JSON และ final JSON พร้อมกัน
- **INF-003**: cleanup process ต้องไม่ลบ temporary result ของ job ที่ยัง retry ได้

### Security dependencies

- **SEC-001**: ป้องกัน path traversal จาก filename template และ source filename
- **SEC-002**: จำกัด workbook compressed/uncompressed size เพื่อป้องกัน zip bomb
- **SEC-003**: จำกัด sheet/row/column/cell length และ processing time
- **SEC-004**: sanitize spreadsheet-origin text แต่ห้ามเปลี่ยน business value เงียบ ๆ
- **SEC-005**: ห้าม log cell contents หรือ mapped payload ทั้งชุดโดย default

## 10. Examples & Edge Cases

### 10.1 Mapping example

Source rows:

```text
order_no | amount_raw | status
A001     | 1200.50    | PAID
A002     |            | PENDING
```

Target Schema:

| Field | Type | Required |
|---|---|---|
| `orderId` | Text | Yes |
| `amount` | Decimal | No |
| `isPaid` | Boolean | Yes |

Mapped JSON:

```json
[
  {
    "orderId": "A001",
    "amount": 1200.50,
    "isPaid": true
  },
  {
    "orderId": "A002",
    "amount": null,
    "isPaid": false
  }
]
```

### 10.2 Multiple output branches

ถ้า Source row 7 emit branches `pmx` และ `ctp` ให้เขียนสอง objects ติดต่อกันตาม branch order จาก Config Version ไม่เรียงตามชื่อ `outputKey`

### 10.3 Edge cases

- input file ไม่มี data row แต่ layout ถูกต้อง → publish `[]`
- Excel sheet มี header ซ้ำหลัง trim → reject draft/preview
- Excel row มี cell เกิน mapped fields → ignore เฉพาะ column ที่ไม่ map
- required Excel header หาย → import failed ก่อนเขียน Source row
- Target required field ได้ null → row Invalid และไม่ publish ทั้งไฟล์
- output filename ชนกับคนละ File Job → default template ป้องกันด้วย `{fileJobId}`
- duplicate input content → ใช้ duplicate policy เดิมและไม่สร้าง JSON ซ้ำ
- reprocess ด้วย version ใหม่ → สร้างผลของ reprocess โดยใช้ deterministic path policy ที่ต้องระบุชัดก่อน implementation; ห้าม overwrite successful output เดิมโดยไม่เก็บ audit link
- `.XLSX` ตัวพิมพ์ใหญ่ → match extension แบบ case-insensitive
- file extension ถูกแต่ content signature ไม่ตรง → reject ด้วย `InputFormatMismatch`

## 11. Validation Criteria

ก่อนเริ่ม implementation ต้องตอบ “ผ่าน” ทุกข้อ:

- contract ใหม่อ่าน config versions เดิมได้โดยไม่ rewrite JSON เดิม
- Excel Input Layout แยกจาก Transformation Program ชัดเจน
- Target Schema ไม่ถูกใช้เป็น persistence destination ใน JSON mode
- accepted extensions มาจาก pinned version ไม่ใช่ global wildcard
- JSON array ordering, null และ data types มี semantics เดียวและ test ได้
- final file ไม่ visible ก่อน job ทั้งไฟล์สำเร็จ
- retry และ crash recovery ไม่สร้าง duplicate/partial output
- UI แสดง Source Table กับ Target Schema ต่างหน้าที่กันชัดเจน
- preview ไม่เปลี่ยน database/file state
- operator/value expression list ตรงกับ capability เดิม ไม่มี syntax ใหม่

### Decisions required before implementation

ประเด็นต่อไปนี้ไม่ขวางการออกแบบ UI แต่ต้องยืนยันก่อนเขียน code:

1. production limit ของ input file, workbook sheets, rows และ columns
2. retention period ของ temporary export results และ JSON output
3. reprocess ต้องสร้างไฟล์ใหม่ที่มี revision suffix หรือแทนที่ไฟล์เดิม
4. output folder เป็น local/shared filesystem หรือ object storage ใน production
5. `.xls` fixture จริงและชนิด cell/formula ที่ต้องรองรับ

## 12. Related Specifications / Further Reading

- [Web Config Plan](../docs/web-config-plan.md)
- [Domain Context](../CONTEXT.md)
- [Current Program Workflow](../current-program-workflow.md)
- [Phase 19 Golden Cases](../docs/phase-19-golden-cases.md)
