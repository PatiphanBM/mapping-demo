---
title: M4.3 — Delivery log, file summary, inspection และ controls API
status: approved
last_updated: 2026-11-18
---

# M4.3 — Delivery log, file summary, inspection และ controls API

## ขอบเขตที่ผู้ใช้ยืนยัน 2026-11-18

ผู้ใช้ยืนยัน API contract subset และ source batch นี้แล้ว; `approved` หมายถึง implementation ภายใน Mapping รอบ M4.3 ไม่ใช่ Dev 1 รับรอง M1.2/Q8/Web ทั้งชุด

ต่อจาก source commit `aca67c6` (M-33/M-34/M-35) เพิ่ม API สำหรับดูผลและควบคุมงาน Mapping ตาม M-10/M-12/M-18/M-19/M-20/M-22 ไม่ทำ Web, reprocess, business resend หรือแก้ Informs

Q8/M1.2 เคยพัก review route/DTO และห้าม implement controller จากร่างก่อน review จึงต้องให้การยืนยันรอบนี้ครอบคลุม **API contract เฉพาะ M4.3 ด้านล่างพร้อม source batch** ด้วย ไม่ถือว่าปิด M1.2/Q8 ทั้งชุดหรือ Dev 1 รับรอง Web integration แล้ว ส่วนที่กระทบ Web ยังส่งให้ Dev 1 review ตามเดิม

ใช้ `/api/mapping` และ BaseResponse เดิมตาม prefix ที่ยืนยันแล้ว; รายละเอียดนี้เป็น subset ที่ทำให้ตรวจได้ ไม่เปลี่ยน API ของ Source/preview เดิมหรือ contract pa-inform/1

## Routes ในรอบนี้

ทุก path ต่อท้าย `/api/mapping`:

| Route | ผลหลัก |
|---|---|
| GET `/file-jobs` | paged safe metadata ของไฟล์: IDs/config/version/inputLayoutKind/name/เวลา/import/archive states/counters/rowVersion; filter configId/inputLayoutKind/importState/archiveState/createdFrom/createdTo |
| GET `/file-jobs/{id}` | pinned IDs/schema hash/profile ID, import completeness/checkpoint/diagnostics และ summary แยก rows/outputs/attempts |
| GET `/file-jobs/{id}/rows` | paged Row Jobs: sourceRowId/rowJobId/rowNumber/physicalLineNumber/state/outputCount/nextAt/rowVersion และ delivery counts ไม่มี raw fields |
| GET `/file-jobs/{id}/errors` | paged Row Errors พร้อม row/attempt/stage/code/path/severity/sourceField; file/import diagnostics อยู่ใน file detail แบบ bounded array ไม่คัด free-form message |
| GET `/file-jobs/{fileId}/rows/{sourceRowId}` | read-only raw Source ตาม pinned definition ของแถวนี้เท่านั้น พร้อม lineage/row state/output counts/availability |
| GET `/file-jobs/{id}/delivery-jobs` | paged safe delivery log: identity/row no/output key/state/attemptsReserved/nextAt/status/code/inform IDs/flags/rowVersion/action availability |
| GET `/delivery-jobs/{id}/attempts` | paged immutable attempt history, reserved/dispatched/completed times/latency/status/code/IDs/flags และ safe field diagnostics ที่บันทึกจริง |
| GET `/delivery-jobs/{id}/payload` | read-only JSON จาก encrypted snapshot จริง: lineage/hash/byte length/logical key reference/availability/payload; ไม่มี regenerate/HTTP |
| POST `/delivery-jobs/{id}/retry` | request expectedRowVersion/requestId/reason → 202 actionId/deliveryJobId/state/attemptsReserved/nextAt/rowVersion; จัดคิวงานเดิม ไม่ส่ง HTTP ใน request |
| GET `/delivery-operations` | control flags/rowVersion/MaxParallel/active-grace lease count/expired sending count และ delivery-enabled configuration ของ instance ที่ตอบ |
| POST `/delivery-operations/pause` | request target intake หรือ delivery + expectedRowVersion/requestId/reason → 200 actionId/flags/rowVersion |
| POST `/delivery-operations/resume` | รูปแบบเดียวกับ pause; เปลี่ยน flag เดิม ไม่สร้าง jobs/attempts |

ไม่เพิ่ม file/row retry จากร่าง M1.2, reprocess/resend, retry-after-cap, max-parallel editor หรือ cross-menu link ในรอบนี้; worker M4.2 ยังเป็นผู้จอง attempt/ส่ง HTTP เพียงทางเดียว

## Request/response และ validation

- camelCase, UUID IDs, ISO UTC timestamps; page เริ่ม 1/default 1, pageSize default 20/max 100; invalid query/body → 400 BaseResponse พร้อม fixed field path/code ไม่ echo raw values
- Lists คืน `{items,totalCount,page,pageSize}`; sort เป็น allowlist ต่อ routeและมี ID tie-breakerเสมอ: files createdAt/id, rows rowNumber/id, errors createdAt/id, deliveries createdAt/sourceRowNo/id, attempts attemptNumber/id; ไม่รับ SQL identifiers/table name หรือ arbitrary sort expression
- File/child ที่ไม่มีหรือ sourceRowId อยู่คนละ file → 404; known entity version/state/request conflict → 409; retry accepted → 202; pause/resume และ reads → 200; storage unavailable → safe 503 ไม่ส่ง DB exception/details
- New controllers ใช้ MediatR เท่านั้น; route-local model validation แปลง binding errors ของ M4.3 เป็น BaseResponse โดยไม่เปลี่ยน controller/error behavior ของ feature เดิมทั้งระบบ
- reason ใช้ operator metadata ที่ไม่ว่างและยาวไม่เกิน 200 ตัวอักษรตาม audit pattern เดิม; FixedActor ใช้ server value `mapping-api` ไม่รับ actor/role จาก body หรือ custom header; response/list/log ไม่ echo reason หรือคัด raw payload/HTTP messageText
- HTTP Source/Payload inspection ตั้ง Cache-Control no-store; reads ไม่มี DB writes/audit insert/job/outbox/attempt/HTTP/archive side effects
- Action availability เป็น snapshot สำหรับ Web; POST recheck guards/rowVersion ใน transaction เสมอ ไม่เชื่อ canRetry/flags ที่ browser ส่งมา

## Counts ที่ต้องกระทบยอดได้

File detail อ่านใน read-only repeatable-read snapshot เดียว พร้อม asOf ของ DB เพื่อให้แต่ละกลุ่มไม่เห็นคนละจังหวะขณะที่ worker ทำงาน:

| กลุ่ม | ความหมาย |
|---|---|
| import | state/readCount/committedCount/complete/checkpoint; complete เฉพาะ import สำเร็จและ counters ตรง ไม่บอกว่าอ่านครบเมื่อยัง Importing/Blocked |
| rows | total Source/Row Jobs ที่ commit แล้ว + byState + blockedWithoutOutput; Filtered แยกจาก failure และไม่สมมติว่าแต่ละ Source ต้องมี 1 output สำหรับ engine กลาง |
| outputs | total durable Delivery Jobs + byState + notDelivered; นับ unique logical outputs ไม่ join attempts แล้ว count ซ้ำ |
| attempts | total reservations + byState Reserved/Dispatching/Completed/OutcomeUnknown; dispatched นับ DispatchedAt ที่มีจริงและเป็นอีก metric ไม่อ้างว่า reservations ทุกตัวส่ง HTTP แล้ว |

ตัวอย่างสมมติ: Source 3 แถว → Filtered 1, Blocked 1, Transformed 1 ที่มี 2 outputs; output D1 Delivered หลัง timeout/replay, D2 Rejected จาก 409:

```json
{
  "import": {"state":"Imported","readCount":3,"committedCount":3,"complete":true},
  "rows": {"total":3,"byState":{"Filtered":1,"Blocked":1,"Transformed":1},"blockedWithoutOutput":1},
  "outputs": {"total":2,"byState":{"Delivered":1,"Rejected":1},"notDelivered":1},
  "attempts": {"total":3,"byState":{"Completed":2,"OutcomeUnknown":1},"dispatched":3}
}
```

ไม่รวม rows.blockedWithoutOutput กับ outputs.notDelivered แล้วเรียกว่า outputs/ใบคำขอ 2 ใบ; คนละหน่วยนับและแถวที่ไม่มี output ยังต้องแสดงอยู่ การรับ duplicate occurrence แสดง canonical File Job ID และ counts ของ occurrence นี้ ไม่คัดยอด canonical มานับซ้ำ

## Inspection และ field diagnostics

### Source

Resolve registry/file/config/definition version ฝั่ง Infrastructure และตรวจ owner/lineage ก่อนเลือก columns ตาม pinned catalog; query physical table จาก server-derived definition UUID และ validated keys พร้อม parameterized Source/file IDs ไม่อ่าน definition ล่าสุดหรือรับชื่อ table จาก browser คง SQL NULL/empty string/ค่าดิบเดิมทุก format

Source detail มี fields เป็น rawText|null กับ availability; limit raw response bytes ผ่าน options ถ้าเกินให้ availability/diagnostic ตามจริง ไม่ truncate/backfill ค่าเงียบ ๆ; Source/metadata ที่ยังไม่บันทึกไม่ถูกสร้างขึ้นจาก GET

### Payload

ใช้ IPayloadProtector เดิมเพื่อ verify AAD/hash แล้วคืน JSON จาก bytes ที่ persist; ไฟล์หรือแถวที่ยังไม่มี output ไม่สร้าง Delivery Job ปลอม เมื่อพบ delivery แล้วแต่เปิด payload ไม่ได้คืน 200 + availability และ payload null เช่น KeyUnavailable/IntegrityCheckFailed/TooLarge; delivery ID ไม่มี → 404

lineage/hash/logical key reference เป็น metadata ไม่คืน key bytes/key file path/URL/connection string; เปิด inspection รวม rejected delivery ได้ตาม M-19 และต้องไม่เปลี่ยน ciphertext/hash/state/counters/NextAt จาก GET

### เหตุผลระดับ field จาก Informs

M4.1 ตั้งใจเก็บ fixed outcome code อย่างเดียว จึงต้องเพิ่มข้อมูลนี้สำหรับ attempts ใหม่ใน M4.3 ไม่เอา messageText ที่อาจมี PII มาคัดลง log:

1. DeliveryContext เพิ่ม schema bytes ของ snapshot ที่ pin; Application ใช้ path catalog จาก schema เดิมเป็น allowlist เท่านั้น ไม่ compile/serialize payload ใหม่หรือ fetch latest schema
2. IInformsClient รับ optional allowed diagnostic paths; response 400 อ่าน bounded JSON ภายใต้ size/depth/timeout เดิมและคัดเฉพาะ messageID ที่ normalize แล้วอยู่ใน pinned catalog เช่น `/inform/insureds/0/birthDate` → `inform.insureds[0].birthDate`; code คงที่ ContractRejected ไม่มี messageText/value
3. path แปลก/ยาวเกิน/ชื่อ extras ที่ไม่ประกาศ/response ผิดรูปแบบ → generic path `payload`; diagnostics ไม่เกิน 100 รายการ ขนาด path ไม่เกิน 512; ไม่เก็บ unknown property names จาก response เป็น path
4. 400 ยังคงเป็น permanent Rejected แม้อ่าน error detail ไม่ได้/เกินขนาด/หมดเวลาฝั่ง client; 409 ใช้ safe fixed code/path ตาม contract เดิม ไม่เปลี่ยน outcome เป็น Delivered และไม่ auto retry permanent rejection
5. เพิ่ม `delivery_attempts.safe_diagnostics_json` เป็น bounded JSONB array default `[]`; commit พร้อม attempt/job result ของ M4.2, append-only completed-history guard คงเดิม; history เก่าไม่มีรายละเอียดให้คืน diagnostics ว่าง ไม่ regenerate หรือเติม field reasons จาก config ล่าสุด

ตัวอย่าง response สมมติที่รับมา `messageID=/inform/insureds/0/birthDate, messageText=not a date: SYNTHETIC-RAW` → สิ่งที่เก็บ/แสดงคือ `{"code":"ContractRejected","path":"inform.insureds[0].birthDate"}` เท่านั้น

## Retry/pause guards และ audit

Retry body ตัวอย่างสมมติ:

```json
{"expectedRowVersion":7,"requestId":"019a9000-0000-7000-8000-000000000401","reason":"ตรวจ dependency แล้ว ขอจัดคิวงานเดิม"}
```

- Lock order control→delivery job→audit; ตรวจ replay ของ `(action,requestId)`/canonical body hash ก่อน rowVersion; body เดิมคืน action response เดิม แม้ job ไปต่อแล้ว body ต่าง → 409 RequestConflict ไม่มี mutation
- Retry ได้เฉพาะ RetryableFailed, attemptsReserved<6 และไม่มี lease; Delivered/Rejected/Blocked/Failed/Sending ไม่เปิด retry; cap เต็ม → 409 AttemptLimitReached; stale version → 409 RowVersionConflict พร้อม current rowVersion ของ entity ที่พบ ไม่สร้าง fake actionId
- Retry กำหนด NextAt=max(DB now, NextAt เดิม) จึงไม่เร่ง backoff/ข้าม Retry-After; counter/epoch/bytes/hash/key/Source/IDs คงเดิม ไม่จอง attempt หรือเรียก HTTP ใน API; audit/actionId/row version/NextAt commit พร้อมกัน และ worker ใช้ guards/budget เดิมต่อ
- Retry ขณะ delivery paused เป็นการจัดคิวเท่านั้น worker ยังรอ resume; response 202 ไม่ยืนยันการส่ง/การรับของ Informs
- Pause/resume รับ target intake|delivery; lock control row เดียวกับ worker และ update flags/rowVersion/audit atomic; ไม่ยกเลิก HTTP/import ที่เริ่มไปแล้ว ไม่ลบ queues และไม่เปลี่ยน MaxParallel หรือเปิด worker ที่ปิดจาก configuration
- Audit เก็บ action/request hash/fixed actor/operator reason และ safe result metadata ไม่มี raw Source/payload/response body; request ซ้ำไม่เพิ่ม audit หรือเปลี่ยน job/flag ซ้ำ; GET inspection ไม่สร้าง action/audit

ตัวอย่าง pause request:

```json
{"target":"delivery","expectedRowVersion":3,"requestId":"019a9000-0000-7000-8000-000000000402","reason":"พักการส่งเพื่อตรวจการเชื่อมต่อ"}
```

## Source batch ที่ยืนยันและใช้ implement

ทุก path อยู่ใน prjInsuranceValidationMapping; ไม่มีไฟล์ลบ รายการที่ย่อด้วย braces หมายถึงแต่ละไฟล์ที่ระบุใน layer นั้น:

| ส่วน | เพิ่ม | แก้ |
|---|---|---|
| Domain | `src/Mapping/Mapping.Domain/Repositories/DeliveryOperations/IDeliveryOperationsRepository.cs` | `Entities/{DeliveryJobs/DeliveryJobs,DeliveryControls/DeliveryControls,MappingAudits/MappingAudits,DeliveryAttempts/DeliveryAttempts}.cs`, `Repositories/DeliveryJobs/IDeliveryJobRepository.cs` (pinned schema/diagnostics records) |
| Application read seam | `src/Mapping/Mapping.Application/DeliveryOperations/{IMappingRuntimeReader,RuntimeReadResult,RuntimeQueryRules,RuntimeInspectionOptions}.cs` | `DependencyInjection.cs`, `Delivery/IInformsClient.cs` (optional pinned diagnostic path allowlist) |
| Application DTOs | `DeliveryOperations/DTOs/{PageResultDto,RuntimePageQueryDto,FileJobListItemDto,FileJobDetailDto,FileSummaryDto,ImportSummaryDto,RowSummaryDto,OutputSummaryDto,AttemptSummaryDto,FileRowDto,RowErrorDto,SourceRowInspectionDto,DeliveryJobLogDto,DeliveryAttemptLogDto,DeliveryPayloadInspectionDto,DeliveryOperationsDto,DeliveryRetryResultDto,DeliveryControlChangeDto,ActionAvailabilityDto}.cs`, `Delivery/DTOs/InformsDeliveryDiagnosticDto.cs` | `Delivery/DTOs/InformsDeliveryResultDto.cs` |
| Application queries | `DeliveryOperations/Queries/{GetFileJobsQuery,GetFileJobQuery,GetFileRowsQuery,GetFileErrorsQuery,GetSourceRowQuery,GetFileDeliveriesQuery,GetDeliveryAttemptsQuery,GetDeliveryPayloadQuery,GetDeliveryOperationsQuery}.cs` | `Delivery/DeliveryService.cs`, `Capabilities/Queries/GetCapabilitiesQuery.cs` |
| Application commands | `DeliveryOperations/Commands/{RetryDeliveryJobCommand,RetryDeliveryJobCommandValidator,ChangeDeliveryPauseCommand,ChangeDeliveryPauseCommandValidator}.cs` | — |
| Infrastructure | `src/Mapping/Mapping.Infrastructure/Repositories/{MappingRuntimeReader,DeliveryOperationsRepository}.cs` | `DependencyInjection.cs`, `Repositories/DeliveryJobRepository.cs`, `Informs/InformsClient.cs`, `Database/Configurations/DeliveryAttempts/DeliveryAttemptsConfiguration.cs`; optional query indexes ใน `Database/Configurations/{FileJobs/FileJobsConfiguration,RowJobs/RowJobsConfiguration}.cs` ตาม query evidence |
| Migration | `src/Mapping/Mapping.Infrastructure/Migrations/<timestamp>_AddDeliveryDiagnosticsAndLogIndexes.cs` และ `.Designer.cs` | `Migrations/MappingDbContextModelSnapshot.cs`; safe diagnostics column/default/checks และ query indexes ที่จำเป็น ไม่มีตารางนอก mapping |
| API | `src/Mapping/Mapping.Api/Controllers/{MappingFileJobsController,MappingDeliveryJobsController,MappingDeliveryOperationsController,MappingRequestValidationAttribute}.cs` | `appsettings.Local.json` สำหรับ inspection size limits เท่านั้น |
| Unit tests | `tests/Mapping.UnitTests/DeliveryOperations/{RuntimeQueryRulesTests,DeliveryOperationsValidationTests,DeliveryRetryGuardsTests,DeliveryControlGuardsTests}.cs` | `tests/Mapping.UnitTests/Delivery/DeliveryAttemptsTests.cs` |
| Integration tests | `tests/Mapping.IntegrationTests/DeliveryOperations/{MappingRuntimeReaderTests,RuntimeInspectionTests,DeliveryOperationsRepositoryTests,DeliveryDiagnosticsTests,RuntimeApiTestData}.cs`, `Api/{MappingFileJobsApiTests,MappingDeliveryJobsApiTests,MappingDeliveryOperationsApiTests}.cs` | `Delivery/{InformsClientTests,DeliveryRuntimeTests,DeliveryTestData}.cs` เฉพาะ diagnostics/fixtures, `Api/MappingCatalogApiTests.cs` เฉพาะ capability regression เมื่อจำเป็น |

ถ้า implementation ต้องใช้ source นอก inventory นี้ให้แจ้งเพิ่มก่อน ไม่แก้ Program/global error handler/Source writer/worker transport/Compose/Informs/Web หรือเอกสารของ Dev 1 เพื่อกลบ contract gap และไม่ commit/push รอบนี้อัตโนมัติ

## เกณฑ์ทดสอบและการปิดงาน

1. GET lists/detail/summary/rows/errors/deliveries/attempts มี pagination/filter/sort/UTC/BaseResponse ตาม contract; malformed input/wrong IDs/cross-file source/version/state conflicts มี safe status/code และไม่มี raw value/exception/key/path/URL ใน metadata responses
2. mixed 0/1/N outputs, partial import/Blocked/Duplicate, multiple attempts/unknown/replay ทำให้ totals ต่อ unique rows/outputs/attempts กระทบยอดใน snapshot เดียว ไม่สร้าง outputs ให้ blocked/filtered row
3. Source/payload inspections อ่าน pin เดิมหลัง activate/disable/add-columns, NULL/empty/raw คงเดิม; key missing/tamper/too-large availability ตามจริง; state/version/NextAt/bytes/hash/key/job-source-outbox-attempt-audit counts ไม่เปลี่ยนและไม่มี HTTP/archive
4. Retry/state/expected version/request replay/body conflict/race กับ worker/ครบ6/Rejected-Blocked-Delivered/Sending และ Retry-After guard ผ่าน; 202 ไม่จอง attempt/HTTP; pause/resume atomic/audited/replayed และ worker ยังเคารพ flags/global budget
5. reject field diagnostics ใช้ pinned schema allowlist/size/depth/codes ไม่มี response PII; malformed/stream-stall 400 ยังคง Rejected, historical diagnostics ว่างและ completed history ไม่แก้ย้อนหลัง; no actor จาก forged headers
6. check.ps1 ผ่านโดยใช้ temporary sequential-collection RunSettings ที่บันทึกใน M4.2 เมื่อจำเป็น; explicit concurrency tests ยังคงเดิม; อัปเดต plan/M-36/contract subset/แบบข้อมูล โดย M1.2/Q8/Web acceptance และ Q4/Q7/M4.4 ที่ยังรอยังไม่ปิด

## Implementation และหลักฐาน 2026-11-18

- source commit `3fd48f8` บนฐาน `aca67c6` — `feat(mapping): add delivery operations and inspection APIs`; commit/push develop ตามคำสั่งผู้ใช้2026-11-18แล้ว ตรวจ local/remote ตรงกัน; เปลี่ยน77ไฟล์ (แก้19/เพิ่ม58 รวม migration/designer/tests) ใน repo Mapping ตาม inventory ที่ยืนยัน ไม่มีไฟล์ลบ; ไม่แก้ Informs/Web/demo/Program/global error handler/Source writer/worker transport
- ปิด M4.3 เฉพาะ API/local tests: `scripts/check.ps1` ผ่าน Release build, unit **38/38**, integration **162/162** และ EF model check; เพิ่ม unit6/integration24 ข้อบน baseline32/138 ใช้เวลา integration8นาที31วินาที; TRX `m4-3-gate-serial.trx` ในทั้งสองtest projects ไม่มี fail/skip/abort
- Gate ใช้ temporary RunSettings `ParallelizeTestCollections=false` ตามข้อจำกัด M4.2 ไม่มี filter และ explicit concurrency ภายในtestsคงเดิม; ไม่แก้ runner configuration ถาวร และไม่อ้างว่า default parallel gateเสถียรแล้ว; มี warning NU1903 ของ SSH.NET2023.0.0 เดิมผ่าน Testcontainers ไม่เปลี่ยน dependencies ในงานนี้
- Read seam `IMappingRuntimeReader`/`MappingRuntimeReader`, query handlers9ชุด และ controllers3ตัวครอบคลุม12routes; typed binding failures ใช้ route-local attribute, fixed safe error และ storage unavailable503 รวมกรณี EF ห่อ transient DB exception
- คำสั่ง retry/pause/resume ใช้ domain guards/FluentValidation/`DeliveryOperationsRepository`; fixed actor มาจากserver, request replay/body conflict/version race/audit rollback มี tests; ใช้ snapshotเดิมและไม่จอง attempt/HTTP จากคำสั่ง
- GET tests เทียบ evidence ของ file/source registry/row/job/attempt/control/audit/outbox/inbox ทั้งก่อนและหลัง; ทดสอบ counts ระหว่าง worker commitด้วย DB interceptorยืนยัน repeatable-read, partial/Blocked/Duplicate, NULL/empty และ pinned fieldsหลัง append/activate/disable; payloadเปิดไม่ได้/เกินขนาดคืน availability โดยไม่มี mutation/HTTP
- Diagnostics tests ใช้ HTTP loopback + pinned schema ที่ต่างจาก active version; unknown names/messageTextไม่ออกในresult/history, malformed/size/depth/stream-stall400เป็นRejected, default[]/DBCHECKและ immutable historyผ่าน gate รวม; schema/bytes/keyเดิม ไม่ติดต่อactual Informsหรือcompany DB
- migration `20261118031413_AddDeliveryDiagnosticsAndLogIndexes` เพิ่ม diagnostics column/default/check และ2file indexes; ไม่มีตารางใหม่ จำนวนตารางคงเดิม20รวม EF history; index config_id เดิมถูกรวมกับ `(config_id,created_at,id)`
- ไฟล์ optionalที่ไม่ต้องแก้: RowJobsConfiguration, DeliveryRuntimeTests, DeliveryTestData และ MappingCatalogApiTests; fixturesใหม่ใช้ RuntimeApiTestData ภายในtests ไม่อ้าง pathของworkspace
- Senderยัง default disabled; capabilitiesยัง falseและ reasonระบุว่ารอactual Informs acceptance; M1.2/Q8/Web review, Q4/M4.4และ Q7ยังแยกติดตาม; source `3fd48f8` pushแล้วและแบบ/หลักฐานอยู่ใน commit เอกสารชุดนี้
