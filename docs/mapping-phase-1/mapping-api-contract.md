---
title: Mapping API และ DTO contract สำหรับหน้า Mapping
status: draft
last_updated: 2026-11-23
---

# Mapping API และ DTO contract สำหรับหน้า Mapping

เอกสารนี้เป็นแบบ M1.2 สำหรับ Web และ Mapping API ตาม [แผน](plan.md), [แบบข้อมูล/runtime](data-runtime-design.md) และ [แบบหลาย format](multi-format-api-web-design.md) ไม่ใช่ OpenAPI ที่รันแล้ว ผู้ใช้ต้องการ Excel, Delimited (CSV/TSV) และ Fixed-width ผ่าน API/หน้าเว็บชุดเดียวกัน โดย MTLS `.xls` BIFF5/cp874 เป็นหนึ่งตัวอย่าง และ partner อื่นจะเปิดใช้ใน UAT; destination ช่วงแรกยังเป็น `pa-inform/1` → Informs API (M-21) การแก้ D-01/D-70/วันส่งรอ Dev 1/PM ตาม Q9

## 1. กติกากลาง

- Prefix **`/api/mapping`** ตามคำตอบผู้ใช้ Q8 วันที่ 2026-11-16; health/OpenAPI เป็น route ของ host แยก
- JSON ใช้ camelCase, ID เป็น UUID string, เวลาเป็น ISO 8601 UTC ลงท้าย `Z`, local date เป็น `yyyy-MM-dd`; row number ของ Source เริ่ม 1 ตาม M-06
- ทุก route ของ feature คืน `BaseResponse<T>` เดิม: `{ "success": true, "messages": null, "result": ... }` หรือ `{ "success": false, "messages": [{ "messageID": "...", "messageText": "..." }], "result": null }`; ต้องทดสอบ serialization จริงก่อนถือว่า wire format ผ่าน
- `messageID` ของ field validation เป็น path เช่น `sourceDefinition.fields[1].key`, `transformationProgram.outputs[0].assignments[2].path`; error ที่ไม่ใช่ field ใช้ code คงที่ (`NotFound`, `RowVersionConflict`, `StateConflict`, `AttemptLimitReached`, `PayloadUnavailable`, `UnsupportedCapability`) และ `messageText` เป็นคำอธิบายปลอดข้อมูลดิบ
- `400` syntax/field/capability/validation, `404` ID ที่ไม่มีหรือไม่อยู่ใต้ parent, `409` version/state/ownership/idempotency conflict, `413` preview upload เกินขนาด, `202` action เข้าคิว, `200` read/synchronous action, `201` create; infrastructure unavailable ใช้ `503` โดยไม่คืน secret/raw exception
- `GET` list คืน `result: { items, totalCount, page, pageSize }`; `page` เริ่ม 1, `pageSize` default 20/max 100, รับ 10/20/50 ของ Web; ค่าผิดตอบ `400`; ทุก sort มี ID เป็น tie-breaker. Query sort รับเฉพาะ allowlist ต่อ route ไม่ต่อ SQL จาก browser
- Commands ที่เปลี่ยน config/Source definition/control/job ใช้ `expectedRowVersion` positive integer; mismatch ตอบ `409 RowVersionConflict` พร้อม current `rowVersion` ใน result เฉพาะกรณีพบ entity นั้นแล้ว; ไม่ทำ partial write
- Async/retry actions รับ `requestId` UUID + `reason` ที่ไม่ว่าง และ audit idempotently ตาม M1.1: `(action,requestId)` กับ body hash เดิมคืนผลเดิม; body ต่างตอบ `409`; `requestId` ไม่ใช่ `sourceRowId`/`outputKey` ใหม่
- `inputLayout.kind` เลือก adapter `Excel`, `Delimited` หรือ `FixedWidth`; field options/diagnostics แตกตาม kind แต่ routes, envelope, Source identity, job/count/action DTO ใช้ร่วมกันตาม [แบบหลาย format](multi-format-api-web-design.md)
- List ปกติไม่คืน raw Source, payload, secret, credential, HTTP error body หรือ full path; detail ที่มี raw ต้องเรียกเฉพาะ route inspection แยกตาม M-18/M-19

## 2. Route และขอบเขต DTO

ทุก route ในตารางต่อท้าย `/api/mapping`; `:id`/`:fileId`/`:rowId` เป็น UUID ไม่รับชื่อ physical table จาก browser `fields[]` หมายถึง field definition เชิงตรรกะ; raw values อยู่ physical Source Table ที่ Mapping resolve เอง

| Route | Request / result หลัก |
|---|---|
| `GET /capabilities` | `inputLayouts[] {kind,extensions,available,reason,limits}`, `valueExpressions`, `predicates`, `conversionTargets`, `namedTransforms`, `namedLookups`, `destinations[] {kind,available,reason}`; format/extension ที่ยังไม่ทดสอบไม่โฆษณาว่าใช้ได้ |
| `GET /source-definitions`, `GET /source-definitions/{id}` | list metadata; detail มี `id`, `configId`, `currentVersionId`, `rowVersion`, `physicalTableName`, `fields[] {key,ordinal,displayName,logicalTypeHint}`; `physicalTableName` อ่านจาก DB เพื่อแสดงอย่างเดียวตาม M-38 ไม่รับชื่อตารางหรือ SQL จาก browser |
| `POST /source-definitions` | `{configId,expectedRowVersion,requestId,reason,physicalTableName,fields:[...]}` → `201 {sourceDefinitionId,versionId,rowVersion}`; ตาม M-39 Web บังคับชื่อ `[a-z][a-z0-9_]{0,47}` เช่น `partner_d_raw` ใน schema `mapping`; API รองรับการไม่ส่งชื่อสำหรับ client เดิมด้วยชื่ออัตโนมัติเดิม; identifier ผิดคืน `400` messageID `physicalTableName`, ชื่อชน metadata/relation เดิมคืน `409 TableNameConflict`; physical columns เป็น `text NULL` และ metadata+DDL+audit atomic |
| `POST /source-definitions/{id}/columns` | `{expectedRowVersion,requestId,reason,fields:[...]}` → `201 {sourceDefinitionId,versionId,rowVersion}`; เพิ่ม key ใหม่เท่านั้น; ห้ามลบ/rename/เปลี่ยนชนิดเดิม (M-17) |
| `GET /contracts/pa-inform/1`, `GET /destination-profiles` | schema hash/path catalog/safe profile metadata + availability; snapshot มาจาก Informs ไม่ส่ง URL/secret |
| `GET /mapping-configs`, `GET /mapping-configs/{id}` | list/detail: `id,name,enabled,activeVersionId,currentVersionNumber,rowVersion`, input route แบบ relative/safe; ไม่ส่ง credential |
| `POST /mapping-configs` | `{name,inputPath,requestId}` → `201 {id,rowVersion}`; config identity มาก่อน definition/version |
| `POST /mapping-configs/{id}/versions` | `{expectedRowVersion,requestId,sourceDefinitionVersionId,inputLayout,transformationProgram,destination}` → `201 {versionId,versionNumber,rowVersion}`; immutable version ใหม่; `inputLayout.kind` เป็น discriminated union ของ Excel/Delimited/FixedWidth ไม่ใช่ MTLS DTO; `destination.kind=ApiContract` ช่วงแรก |
| `GET /mapping-configs/{id}/versions`, `GET /mapping-configs/{id}/versions/{versionId}` | list/detail immutable versions, pinned definition/schema/profile IDs+hash; ไม่มี secret |
| `POST /mapping-configs/{id}/validate` | `{versionId}` หรือ draft definition → `200 {valid,diagnostics[] {code,path,message},schemaHash,profileVersion}`; pure check ไม่มี job/outbox |
| `POST /mapping-configs/{id}/preview` | `{versionId|draft,records:[...]}` → `200 {records,outputs,diagnostics,counts,truncated}`; records เป็น raw fields/row positions, ไม่มี persistent IDs; ไม่เขียน DB; จำกัด 50 records และ JSON body 1 MiB |
| `POST /mapping-configs/{id}/preview-file` | `multipart/form-data` กับ `file` + `versionId` หรือ draft → `200 {records,outputs,diagnostics,counts,truncated}`; adapter ตาม `inputLayout.kind` อ่าน Excel/Delimited/FixedWidth ภายใต้ 20 MiB upload/สูงสุด 50 data records, Excel scan ไม่เกิน 100,000 cells; ไม่สร้าง File Job/Source/HTTP; ไฟล์ผิด format/encoding/limit แจ้ง path/code ชัดเจน |
| `POST /mapping-configs/{id}/activate`, `/disable` | `{versionId,expectedRowVersion,requestId,reason}` สำหรับ activate; disable ไม่ต้อง versionId; `200 {id,enabled,activeVersionId,rowVersion}`; active route/owner check ใน transaction เดียว |
| `GET /file-jobs`, `GET /file-jobs/{id}` | filters `configId,inputLayoutKind,status,createdFrom,createdTo,page,pageSize`, sort `createdAt`/`id`; detail มี pin IDs และ `inputLayoutKind`, `createdAt`, import/archive states, hash, duplicate canonical ID, checkpoint/counters, `rowVersion` |
| `GET /file-jobs/{id}/rows`, `GET /file-jobs/{id}/errors` | paged metadata: row ID/number/physical line/state/outputCount หรือ safe stage/code/path/message; sort `rowNumber`/`createdAt` + ID |
| `GET /file-jobs/{fileId}/rows/{rowId}` | raw Source detail เฉพาะ row ที่อยู่ใน file นี้: `sourceRowId,fileJobId,sourceDefinitionVersionId,configVersionId,rowNumber,physicalLineNumber,rowJobState,fields:{key:rawText|null},deliverySummary`; fields มาจาก pinned definition; read-only |
| `POST /file-jobs/{id}/retry`, `POST /row-jobs/{id}/retry` | `{expectedRowVersion,requestId,reason}` → `202 {actionId,jobId,state}` เฉพาะ recoverable import/row failure; ไม่สร้าง Source identity ใหม่ |
| `GET /file-jobs/{id}/delivery-jobs`, `GET /delivery-jobs/{id}/attempts` | paged log/attempt metadata; `attemptNumber`, dispatch/outcome status, HTTP status/code/path/time, inform IDs/flags; ไม่มี raw response body |
| `GET /delivery-jobs/{id}/payload` | read-only: lineage/config/schema/profile IDs, payload hash, availability, `payload` JSON ที่ decrypt จาก snapshot จริงเมื่อพร้อม; `payload=null` และ reason เมื่อ key/bytes ไม่มี; ไม่มี regenerate/HTTP |
| `POST /delivery-jobs/{id}/retry` | `{expectedRowVersion,requestId,reason}` → `202 {actionId,deliveryJobId,state,attemptsReserved}` เฉพาะ transient ก่อนครบ 6; guard เดียวกับ worker |
| `GET /delivery-operations`, `POST /delivery-operations/pause`, `/resume` | read safe counts/budget/pause flags; command `{target:"intake"|"delivery",expectedRowVersion,requestId,reason}` → `200` flags+rowVersion; ไม่แก้งานเดิม |

`POST /file-jobs/{id}/reprocess` ไม่เปิดตาม M-22; unsupported route ตอบ `404` ไม่สร้าง operation ปลอม. Preview กับ payload detail ไม่ใช่ destination `JsonFile`

## 3. Counts และ action guards

File summary แยก `import {state,readCount,committedCount,complete}`, `rows {total,byState,blockedWithoutOutput}`, `outputs {total,byState}` และ `attempts {reserved,dispatched,completed,unknown}`; `readCount` ขณะ import ยังไม่ complete เป็นยอดที่ reader พบแล้ว ไม่ใช่จำนวนทั้งหมดในไฟล์ `rows.total=committedCount` เมื่อ commit ครบ; output total คือจำนวน `DeliveryJob` ไม่ใช่จำนวน Source หรือ attempts (M-03)

| Action | อนุญาตเมื่อ | ปฏิเสธ |
|---|---|---|
| activate | version เป็นของ config, validate ผ่าน pinned schema/profile, expected row version ตรง, route ไม่ชน | `400` validation หรือ `409` conflict; ไม่มี partial activation |
| disable | config enabled, expected row version ตรง | `409`; งานเดิมยัง pin version เดิม |
| file/row retry | state recoverable, ไม่มี lease ที่ยังทำงาน, expected row version ตรง | `409 StateConflict`; ไม่ reset Source/counters |
| delivery retry | `RetryableFailed` หรือ transient failure ที่จัดเป็น retryable, `attemptsReserved<6`, ไม่มี active lease, expected row version ตรง | `409 AttemptLimitReached` หรือ `StateConflict`; `Rejected` 400/409, `Blocked`, `Delivered`, `OutcomeUnknown` ที่ครบโควตาไม่ส่งอีก |
| pause/resume | expected control row version ตรง; target enum ถูกต้อง | `400` target ผิด, `409` stale state/version |

Manual retry **ขอจัดคิว**; worker จอง attempt ใหม่ภายใต้ shared global budget ใน transaction ตาม M1.1; response `202` ไม่ยืนยันว่า HTTP ส่งแล้วหรือ Informs รับแล้ว. Completion แสดง `Delivered` เฉพาะมี inform ID; `OutcomeUnknown` ไม่แปลว่าไม่ได้รับ

## 4. ตัวอย่าง wire contract (ข้อมูลสมมติ)

`POST /api/mapping/mapping-configs/018f0000-0000-7000-8000-000000000001/activate`:

```json
{"versionId":"018f0000-0000-7000-8000-000000000002","expectedRowVersion":4,"requestId":"018f0000-0000-7000-8000-000000000003","reason":"เปิดใช้ version ที่ตรวจแล้ว"}
```

`200`:

```json
{"success":true,"messages":null,"result":{"id":"018f0000-0000-7000-8000-000000000001","enabled":true,"activeVersionId":"018f0000-0000-7000-8000-000000000002","rowVersion":5}}
```

หากมีการเปลี่ยน config ก่อน request นี้ ตอบ `409` และไม่ activate:

```json
{"success":false,"messages":[{"messageID":"RowVersionConflict","messageText":"Config เปลี่ยนแล้ว กรุณาโหลดข้อมูลล่าสุด"}],"result":{"rowVersion":5}}
```

`GET /api/mapping/file-jobs/018f0000-0000-7000-8000-000000000010/rows/018f0000-0000-7000-8000-000000000011` ตัวอย่าง Source ดิบที่ไม่มีข้อมูลบุคคลจริง:

```json
{"success":true,"messages":null,"result":{"sourceRowId":"018f0000-0000-7000-8000-000000000011","fileJobId":"018f0000-0000-7000-8000-000000000010","sourceDefinitionVersionId":"018f0000-0000-7000-8000-000000000012","configVersionId":"018f0000-0000-7000-8000-000000000013","rowNumber":1,"physicalLineNumber":2,"rowJobState":"Completed","fields":{"field_a":"SAMPLE-001","field_b":null},"deliverySummary":{"outputCount":1,"delivered":0,"pending":1}}}
```

`GET /api/mapping/file-jobs?page=1&pageSize=20` เมื่อมีงาน 1 รายการ:

```json
{"success":true,"messages":null,"result":{"items":[{"id":"018f0000-0000-7000-8000-000000000010","configId":"018f0000-0000-7000-8000-000000000001","status":"Importing","createdAt":"2026-11-16T02:00:00Z","import":{"readCount":1,"committedCount":1,"complete":false}}],"totalCount":1,"page":1,"pageSize":20}}
```

ตัวอย่าง field validation error ของ `POST /source-definitions` (`400`):

```json
{"success":false,"messages":[{"messageID":"sourceDefinition.fields[1].key","messageText":"Field key ซ้ำ"}],"result":null}
```

`POST /api/mapping/delivery-jobs/018f0000-0000-7000-8000-000000000020/retry` เมื่อครบ 6 attempts:

```json
{"success":false,"messages":[{"messageID":"AttemptLimitReached","messageText":"Delivery นี้ใช้ครบ 6 attempts แล้ว"}],"result":null}
```

`GET /api/mapping/delivery-jobs/018f0000-0000-7000-8000-000000000020/payload` ต้องคืน `payload` จาก durable snapshot เดิม เมื่อ `availability="Available"` เท่านั้น; หาก decrypt ไม่ได้ ให้คืน `availability="KeyUnavailable", payload=null` พร้อม lineage/hash ไม่เอา config ล่าสุดมาสร้างใหม่

## 5. งานยืนยันก่อน implement

- Q8: prefix ยืนยันแล้ว; route/DTO ที่เหลือเป็นแบบภายใน Mapping รอ Dev 1 review เฉพาะส่วนที่กระทบ Web/contract boundary ตาม AGENTS.md
- ตรวจ OpenAPI/JSON serializer, `[ApiController]` auto-validation และ global exception handler ให้ตอบ envelope ที่กำหนดจริง; current source ยังไม่มี feature controllers
- Q4 ต้องให้ Dev 1 ตัดสิน metadata เวลาฝั่ง Informs; Q5 รับ fields ภายหลัง; Q7 keys/fixture/corporate dependencies ไม่อ้างว่าพร้อมจาก local design
- ขนาด preview 20 MiB/50 records เป็น limit เริ่มต้นร่วม; 100,000 cells ใช้กับ Excel เท่านั้น ต้องพิสูจน์ Delimited/Fixed-width ด้วย fixture สมมติของ format นั้นก่อนเปิด capability; ปรับด้วย configuration ก่อนเปิดใช้งานจริง ไม่ลดเพดานรับไฟล์จริงจาก limit preview
