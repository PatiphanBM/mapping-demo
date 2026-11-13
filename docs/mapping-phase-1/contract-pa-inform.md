---
title: contract pa-inform/1 — API รับใบคำขอ PA (Mapping → Informs)
status: review
last_updated: 2026-11-13
---

# API รับใบคำขอ PA — `POST /api/pa/informs`

> **สำเนาจากเอกสารกลาง** (`insurance-validation/03-prd/informs/api/inform-intake.md`, ฉบับ 2026-11-12) — Dev 1 อัปเดตให้เมื่อ contract เปลี่ยน · **ตัวจริงของ contract = JSON Schema ในโค้ด Informs** (บรรทัดล่าง) ถ้าเอกสารนี้ขัดกับ schema ให้เชื่อ schema แล้วแจ้ง Dev 1
> คำว่า "mapping-demo" ในไฟล์นี้ = ผู้ส่งข้อมูล ตอนนี้คือ **service Mapping** (`prjInsuranceValidationMapping`) · `D-xx` / `BR-xxx` ที่อ้าง ดูใน [rules.md](rules.md) (ข้อที่ไม่อยู่ในนั้นเป็นเรื่องฝั่ง Informs)
> ⚠️ `src/MappingDemo.Api/ContractFixtures/pa-inform-v1.json` ของ demo เป็นฉบับก่อน `insureds[]` — อย่าใช้

> **ชื่อตาม D-66 (2026-11-05):** product (เดิม campaign, `MTLSPA`) → package (เดิม product, `MTL2`) · contract `productCode` (เดิม `campaignCode`) · code `PACKAGE_NOT_IN_PRODUCT` / `PACKAGE_INACTIVE` / `PACKAGE_OUT_OF_SALE_PERIOD` · ผลตรวจที่บันทึกก่อนวันนั้นเก็บชื่อเดิม

> **ผู้เอาประกันหลายคน (D-75, 2026-11-12):** `inform.insured` → **`inform.insureds[]`** — 1 คน = 1 risk ของใบ มี `packageCode` (+ `premium` ว่างได้) และ `beneficiaries[]` ของตัวเอง · ตัด `inform.packageCode` / `inform.beneficiaries` ที่หัว · `productCode` + `totalPremium` คงที่หัว · แก้ `pa-inform/1` ตรง ๆ (ยังไม่มี partner ใช้จริง — แนว D-61 / D-63) · mapping MTLS ส่ง `insureds` 1 คน `packageCode` = `MTL2`

> ผู้เรียก: mapping-demo · JSON Schema ฉบับเต็ม: อยู่ในโค้ด `prjInsuranceValidationInforms/src/Informs/Informs.Api/Contracts/PaInform/pa-inform-1.schema.json` (ฝังเข้า assembly · ขอดูได้ที่ `GET /api/pa/informs/schema/1`) · เหตุผล: D-02 – D-06 ใน [rules.md](rules.md) §1–§2

## 1. หลักของ contract

1. **ตรวจแค่โครงสร้างและชนิดข้อมูล** — ไม่มีกฎธุรกิจ field ทางธุรกิจเกือบทั้งหมดจึง nullable (D-04)
2. **mapping ส่งเฉพาะสิ่งที่ partner ให้มา + ค่าคงที่ของ partner** — ไม่ส่งค่าที่ระบบสร้าง (`informId`, `status`, `rowVersion`) และไม่ส่ง `*_id` (UW เป็นคนเลือก)
3. **ค่าคงที่ที่ mapping เติมส่งเป็นค่าธรรมดา** — ไม่ส่งว่าค่ามาจากไหน (ดูได้จาก Source table + mapping config version)
4. **ข้อความกำกวมส่งตามที่ partner เขียนมา** (`fullName`, `occupation`, `address`, `relationship`) — ไม่แยกชื่อ / ที่อยู่ที่ mapping · ชื่อใน JSON ไม่มี suffix `Text` แต่ service เก็บลง column `*_text`
5. **ปิด field แปลกปลอม** (`additionalProperties: false`) ยกเว้นใน `extras` — mapping config ที่พิมพ์ path ผิดจะถูกจับตั้งแต่ส่ง
6. **mapping แปลงรหัสของ partner เป็นค่ากลาง** (MTLS `P` → `NEW`, `C` → `CANCELLED`) และส่งค่าเดิมของ partner ใน `sourceTransactionType`
7. **ค่าที่แปลงชนิดไม่ได้** (เช่น วันที่ `-293`) → `null` + ค่าเดิมจากไฟล์ใน `extras.unparsed.<field>` แทนการ reject
8. camelCase ใน JSON · วันที่ `yyyy-mm-dd` · เวลา `HH:MM` · เงินทศนิยม 2 ตำแหน่ง

## 2. Endpoint

```
POST /api/pa/informs
Content-Type: application/json
```

- 1 request = 1 ใบคำขอ = 1 transaction (บันทึก original + งานของ stage `PRODUCT` แล้ว commit จึงตอบ) — **ไม่ตรวจใน request**
- mapping กำหนดความขนานเอง · service ตอบ `429` + `Retry-After` เมื่อรับไม่ไหว
- product group อยู่ใน URL (`/api/pa/...`) — motor มี endpoint + contract ของตัวเอง
- `GET /api/pa/informs/schema/{version}` คืน JSON Schema ให้หน้า config ของ mapping ดึงรายการ path · `{version}` = major (`1` → `pa-inform/1`) · ไม่รู้จัก → `404` 🟡
- 🟡 `429` ใช้ concurrency limit ต่อ instance (config `Intake:MaxConcurrentRequests`, ค่าเริ่ม 32) · `Retry-After: 1`

## 3. Request

```text
request body
├── contractVersion        "pa-inform/1"
├── fileName               ชื่อไฟล์ (optional — แสดงในสรุปต่อไฟล์ของหน้า UW, D-36)
├── fileJobId              ┐
├── sourceRowId            ├ idempotency key — ส่งซ้ำได้ผลเดิม
├── outputKey              ┘ "default" (MTLS 1 แถว = 1 ใบคำขอ)
├── sourceRowNo            ลำดับแถวในไฟล์ (V5 ใช้เรียงลำดับ — D-23)
├── mappingConfigVersionId
└── inform               ← ใบคำขอ 1 ใบ (ทุกอย่างนอก inform = ข้อมูลของการส่ง)
    ├── header: partnerCode, saleChannel, sourceReference, transactionType, วันที่, product / agent, เบี้ยรวม, สาขา
    ├── insureds[]         ผู้เอาประกัน 1 คน = 1 risk ของใบ (MTLS 1 คน = ผู้ถือกรมธรรม์)
    │   ├── packageCode    package ของคนนี้ (ต้องขายใน productCode)
    │   └── beneficiaries[]
    └── extras
```

### 3.1 MTLS: column ในไฟล์ → path

| column ไฟล์ | path | งานของ mapping |
|---|---|---|
| — | `inform.partnerCode` | ค่าคงที่ `MTLS` |
| — | `inform.saleChannel` | ค่าคงที่ `FILE` |
| — | `inform.productCode`, `inform.insureds[0].packageCode`, `inform.agentCode` | ค่าคงที่ `MTLSPA` / `MTL2` / `74000490` |
| — | `inform.saleId` | MTLS ไม่ส่ง (null) |
| — | `inform.insureds[0].relation` | ค่าคงที่ `PRIMARY` 🟡 (field เพิ่ม 2026-10-21 — §5) |
| `nSeq` | `sourceRowNo` | |
| `cPol_no` | `inform.sourceReference` | |
| `cTrans_type` | `inform.transactionType` + `sourceTransactionType` | `P` → `NEW`, `C` → `CANCELLED` + ส่งค่าเดิมด้วย |
| `cStart_Date`, `cFinish_date` | `inform.effectiveDate`, `expiryDate` | `yyyymmdd` → `yyyy-mm-dd`; แปลงไม่ได้ → null + `extras.unparsed` |
| `cTime` | `inform.coverStartTime` | `HH:MM` |
| `nTotal` | `inform.totalPremium` | |
| `cBranch_code`, `cBranch_name` | `inform.salesBranchCode`, `salesBranchName` | |
| `ccust_name` | `inform.insureds[0].fullName` | ข้อความตามที่ส่งมา ไม่แยกคำนำหน้า |
| `cCust_idno` | `inform.insureds[0].idNo` | |
| `cBirth_Date` | `inform.insureds[0].birthDate` | `yyyymmdd` → date |
| `nAge` | `inform.insureds[0].declaredAge` | |
| `cOccupation` | `inform.insureds[0].occupation` | ข้อความตามที่ส่งมา |
| `cAddress` | `inform.insureds[0].address` | ข้อความตามที่ส่งมา ไม่แยกที่อยู่ |
| `cBenf_1..2`, `cRelate_1..2` | `inform.insureds[0].beneficiaries[n].fullName`, `.relationship` | column → array ข้ามคนที่ว่าง · ไม่มีชื่อเลย → 1 คน "ทายาทตามกฎหมาย" (BR-035) |
| ทุก column | — | `-` → null |

### 3.2 ตัวอย่าง (ข้อมูลสมมติ)

แถวงานใหม่ (`P`):

```json
{
  "contractVersion": "pa-inform/1",
  "fileJobId": "1024",
  "fileName": "MTLSPA_20261021.xls",
  "sourceRowId": "row-000001",
  "outputKey": "default",
  "sourceRowNo": 1,
  "mappingConfigVersionId": "mtls-pa@3",
  "inform": {
    "partnerCode": "MTLS",
    "saleChannel": "FILE",
    "sourceReference": "PA0000000001",
    "transactionType": "NEW",
    "sourceTransactionType": "P",
    "effectiveDate": "2026-10-09",
    "expiryDate": "2027-10-08",
    "coverStartTime": "10:30",
    "productCode": "MTLSPA",
    "agentCode": "74000490",
    "saleId": null,
    "totalPremium": 180.00,
    "salesBranchCode": "0001",
    "salesBranchName": "สาขาตัวอย่าง",
    "insureds": [ {
      "isPolicyholder": true,
      "relation": "PRIMARY",
      "fullName": "นางสาวสมหญิง ใจดี",
      "idNo": "1234567890123",
      "birthDate": "1991-05-20",
      "declaredAge": 35,
      "occupation": "พนักงานบริษัท",
      "address": "99 ต.ในเมือง อ.เมือง จ.ขอนแก่น 40000",
      "packageCode": "MTL2",
      "premium": null,
      "beneficiaries": [ { "seq": 1, "fullName": "สมชาย ใจดี", "relationship": "บิดา" } ]
    } ],
    "extras": {}
  }
}
```

ใบครอบครัวคนละ package (partner สมมติ — D-75) — เฉพาะส่วนที่ต่าง:

```json
"productCode": "TKBPA", "totalPremium": 500.00,
"insureds": [
  { "relation": "PRIMARY", "fullName": "…", "packageCode": "MTL2", "premium": null,
    "beneficiaries": [ { "seq": 1, "fullName": "…", "relationship": "คู่สมรส" } ] },
  { "relation": "CHILD", "isPolicyholder": false, "fullName": "…", "packageCode": "MTLC", "premium": null } ]
```

แถวยกเลิก (`C`) — ข้อมูลไม่ครบตามที่ partner ส่งจริง:

```json
{
  "contractVersion": "pa-inform/1",
  "fileJobId": "1024",
  "fileName": "MTLSPA_20261021.xls",
  "sourceRowId": "row-000002",
  "outputKey": "default",
  "sourceRowNo": 2,
  "mappingConfigVersionId": "mtls-pa@3",
  "inform": {
    "partnerCode": "MTLS", "saleChannel": "FILE",
    "sourceReference": "PA0000000002",
    "transactionType": "CANCELLED", "sourceTransactionType": "C",
    "effectiveDate": null, "totalPremium": 0,
    "salesBranchCode": "0001",
    "insureds": [ { "fullName": "ยกเลิก", "birthDate": "1985-01-01",
                    "beneficiaries": [ { "seq": 1, "fullName": "สมศรี ดีมาก", "relationship": "มารดา" } ] } ],
    "extras": { "unparsed": { "effectiveDate": "-293", "expiryDate": "-293" } }
  }
}
```

## 4. Response

ทุก response ห่อด้วย `BaseResponse` (`success`, `messages[]`, `result`) แบบเดียวกับ API อื่นของระบบ (D-61) · **แยกกรณีด้วย HTTP status** · `messageText` ไว้อ่าน ห้ามใช้ตัดสิน

| HTTP | ความหมาย | body | mapping ทำอะไร |
|---|---|---|---|
| `201` | บันทึกแล้ว (ตรวจภายหลัง) | `result` = `{ "informId": "…", "replayed": false }` | Delivered |
| `200` | idempotency key นี้เคยบันทึกแล้ว — ผลเดิม | `result` = `{ "informId": "…", "replayed": true }` | Delivered |
| `200` | ใบซ้ำที่ยังไม่ผ่าน (`RECEIVED` / `EXCEPTION` / `TECHNICAL_HOLD` — D-46) และแถวที่ส่งมาเป็น `NEW` → แทนที่ + ตรวจใหม่ (BR-002) | `result` = `{ "informId": "…", "replayed": false, "replaced": true }` | Delivered |
| `409` | ใบซ้ำที่ `PASSED` / `CANCELLED`, หรือแถว `C` ของเลขที่เคยรับเป็นงานใหม่ (BR-011) | `messageID: "DUPLICATE_SOURCE_REFERENCE"` · `result` = `{ "existingInformId": "…" }` | Rejected — ไม่ retry |
| `400` | ผิด contract / contract version ที่ไม่รู้จัก | 1 จุดผิด = 1 message · `messageID` = JSON path (JSON อ่านไม่ได้ = `""`, version ไม่รู้จัก = `/contractVersion`) | Rejected — แก้ mapping config แล้วส่งใหม่ |
| `429` | รับไม่ไหว | `messageID: "TOO_MANY_REQUESTS"` + header `Retry-After` | retry (เคารพ `Retry-After`) — ปลอดภัยเพราะ idempotency key |
| `503` / `5xx` / timeout | ชั่วคราว | `5xx` จาก service: `messageID: "UnhandledException"` (body อาจไม่ใช่ `BaseResponse` ถ้ามาจาก proxy) | retry |

```json
// 201
{ "success": true, "messages": null,
  "result": { "informId": "0192f1a0-…", "replayed": false } }

// 409
{ "success": false,
  "messages": [ { "messageID": "DUPLICATE_SOURCE_REFERENCE",
                  "messageText": "sourceReference PA0000000001 of partner MTLS already exists with status PASSED and cannot be replaced by a NEW row" } ],
  "result": { "existingInformId": "0192f1a0-…" } }

// 400
{ "success": false,
  "messages": [ { "messageID": "/inform/insureds/0/birthDate", "messageText": "not a date" } ],
  "result": null }
```

`GET /api/pa/informs/schema/{version}` ที่เจอ ตอบไฟล์ JSON Schema ตรง (ไม่ห่อ — เครื่องมือ JSON Schema ใช้ URL ตรงได้) · ไม่เจอ → `404` ห่อ `BaseResponse` (`messageID: "NotFound"`)

ลำดับการตัดสินใน service: ตรวจ schema (`400`) → idempotency key เคยมี (`200 replayed`) → ใบซ้ำตาม `(productGroup, partnerCode, sourceReference)` (`200 replaced` / `409`) → บันทึกใหม่ (`201`; แถว `CANCELLED` → สถานะ `CANCELLED` ไม่สร้างงานตรวจ)

ทุกการบันทึก (สร้าง / แทนที่) อยู่ใน transaction เดียว: original + key ลง `inform_idempotency_key` (D-44) + snapshot ของสถานะหลังบันทึก (`MAPPING` / `MAPPING_REPLACED`) + outbox ของ stage `PRODUCT` (ยกเว้นแถว `CANCELLED`) · แทนที่ = ข้อมูล partner ทับทั้งใบ รวม `*_id` / product ที่ UW เลือก · `row_version` +1 · สถานะกลับเป็น `RECEIVED` · `received_at` คงเวลารับครั้งแรก (D-23) · 🟡 ไม่ส่ง `insureds` (หรือ array ว่าง) → ยังสร้างผู้เอาประกัน 1 คน + risk ของใบ 1 แถว (ทุก field null) — ความไม่ครบไปเป็น validation message (D-04) · request ที่ชนกันพร้อมกัน (key / เลขเดียวกัน) → ตัดสินใหม่ 1 รอบ

## 5. Versioning

- `contractVersion` = `pa-inform/<major>` — **เพิ่ม field optional ไม่ขึ้น major** (ทางที่ใช้ขยายทีละ partner) · ลบ / เปลี่ยนชนิด / เปลี่ยนความหมาย → ขึ้น major และ service รับ major เก่าไปพร้อมกันช่วงหนึ่ง
- mapping config version อ้าง contract version ที่ใช้
- `additionalProperties: false` → **deploy service ก่อน mapping เสมอ** เมื่อเพิ่ม field
- 2026-11-12 (D-75) `insured` → `insureds[]` · `packageCode` + `beneficiaries` ย้ายเข้าในแต่ละคน · เพิ่ม `premium` ต่อคน — เปลี่ยนโครง แต่**ไม่ขึ้น major** เพราะยังไม่มี partner ใช้ contract จริง (แนว D-61 / D-63) · mapping ต้องปรับ config MTLS ตาม
- 🟡 2026-10-21 เพิ่ม `insured.relation` (optional) — ความสัมพันธ์ของผู้เอาประกันที่ V4 ต้องใช้ (D-19) ตกลงไว้ว่า mapping เติม แต่ contract ฉบับแรกยังไม่มี field · ไม่ส่ง → V4 ออก `INSURED_RELATION_MISSING` (service ไม่เดาความสัมพันธ์เอง — การเติมค่าของ partner เป็นงาน mapping, D-06)
- 🟡 2026-10-26 เพิ่ม `fileName` (optional, นอก `inform`) — ชื่อไฟล์ต้นทางให้สรุปต่อไฟล์ของหน้า UW (D-36) · ไม่ส่ง → หน้าสรุปแสดง `fileJobId` แทน
- 2026-10-27 `fileJobId` เป็น `string` ไม่บังคับรูปแบบ uuid (ผู้ใช้) — mapping-demo ใช้ `file_jobs.id` เป็น `bigint` ส่งเป็นข้อความ (เช่น `"1024"`) · service ใช้เป็น key เทียบค่าอย่างเดียว ไม่แปลงชนิด · ยังไม่มีใครใช้ contract จึงไม่ขึ้น major

## 6. ยังไม่ตัดสิน

- authN / authZ ของผู้เรียก — ไม่กระทบโครง body
