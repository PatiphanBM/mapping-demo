# Reverse-Engineered Specification: Mapping Demo

วันที่ตรวจโค้ด: 2026-11-11

## 1. ภาพรวม

Mapping Demo เป็นระบบ batch mapping แบบ event-driven สำหรับรับไฟล์ แยก record เก็บข้อมูลดิบ แปลงข้อมูลรายแถว และส่งผลไปปลายทางหนึ่งชนิดต่อ Config Version ได้แก่ตารางภายใน, Partner API, JSON file หรือฐานข้อมูลภายนอก ระบบแยก control plane (ตั้งค่า/activate/admin) ออกจาก data plane (watch/import/transform/deliver) และตรึง Config Version, schema และ profile version เพื่อให้งานเดิมทำซ้ำได้แบบ deterministic

สถานะในเอกสาร:

- **ทำงานจริง** — มี runtime path ใน `src/` หรือหน้าใช้งานใน `web/`
- **มี gate** — โค้ดมีแล้ว แต่ feature flag, runtime control, profile หรือ permission อาจปิดไว้
- **Prototype** — หน้าแนวคิด ไม่ใช่ runtime flow

ขอบเขตตรวจ: `src/`, `web/`, migrations, tests, runtime configuration และ entry points ไม่รวมระบบเดิมที่อธิบายใน `current-program-workflow.md`, เนื้อหาเรียนใน `learning/` และเอกสารแผนที่ยังไม่มี code path

## 2. Feature inventory แบบย่อ

| กลุ่ม | Feature ที่พบ | สถานะ |
|---|---|---|
| Table Catalog | สร้าง Source/Normalized table, เพิ่ม column แบบ expand-only, search/filter, schema verification, ดู config/job references | ทำงานจริง; mutation มี gate |
| Mapping Config | สร้าง config, immutable version, clone ผ่าน UI, validate, preview, activate, version compare | ทำงานจริง; destination บางชนิดมี gate |
| Input | Delimited `.txt/.csv`, fixed-width `.txt`, Excel `.xlsx/.xls`, UTF-8 validation, workbook preview | ทำงานจริง |
| Transformation | filter, 0..N outputs, nested object/array mapping, expressions, predicates, named transform/lookup, typed conversion | ทำงานจริง |
| File intake | FileSystemWatcher + periodic scan, extension ownership, version pinning, duplicate content detection, snapshot, archive | ทำงานจริง |
| Processing | Source row persistence, Kafka/outbox, per-row normalize, retry, row error, reconciliation, reprocess | ทำงานจริง |
| LegacyTable | upsert current normalized outputs และลบ stale outputs | ทำงานจริง |
| ApiContract | contract snapshot, encrypted durable payload, idempotent HTTP delivery, retry, resend, rollout/kill switch | ทำงานจริง; activation มี gate |
| JsonFile | รวมทั้ง File Job เป็น JSON array, encrypted staging, atomic publish, hash/quota/recovery/cleanup | ทำงานจริง; activation/finalizer มี gate |
| DatabaseTable | PostgreSQL/SQL Server, schema discovery/snapshot/drift check, parameterized INSERT, duplicate reconciliation, manual unknown-outcome resolution | มี gate; default ปิด |
| Admin | API profile lifecycle, Contract Registry lifecycle, Table admin, safe metrics/alerts/audit | ทำงานจริง; permission/config คุม |
| Web | Config Studio, workbench, delivery dashboard, API profiles, contracts, tables | ทำงานจริง |
| Prototype | `/prototype/file-to-json` variants A/B/C | Prototype เท่านั้น |

หลักฐาน: `src/MappingDemo.Shared/Destinations/DestinationDefinition.cs:7`, `src/MappingDemo.Worker/Program.cs:83`, `web/app/layout.tsx:43`

## 3. Architecture

### 3.1 Technology stack

- Backend/API/Worker: .NET 8 / C# (`global.json:2`, `src/MappingDemo.Api/MappingDemo.Api.csproj:1`)
- Mapping database: PostgreSQL + Dapper/Npgsql; migrations embedded ใน Shared project
- Messaging: Apache Kafka; transactional outbox ใน Mapping DB
- External database targets: PostgreSQL และ SQL Server
- Web: Next.js 16.4.3, React 19.3, TypeScript, Tailwind; Playwright E2E
- Local infrastructure: Docker Compose มี PostgreSQL 17, Kafka 4.3.1 และ Kafka UI
- API docs: OpenAPI/Swagger JSON + Scalar เฉพาะ Development

หลักฐาน: `src/MappingDemo.Shared/MappingDemo.Shared.csproj:10`, `src/MappingDemo.Worker/MappingDemo.Worker.csproj:11`, `web/package.json:11`, `docker-compose.yml:1`, `src/MappingDemo.Api/Program.cs:231`

### 3.2 Module structure

```text
src/
├─ MappingDemo.Api/       HTTP API, file watcher, admin/control plane
├─ MappingDemo.Worker/    Kafka consumers, import, normalize, deliveries
└─ MappingDemo.Shared/    domain contracts, compilers, DB migrations/adapters
web/
├─ app/                   Next.js routes
├─ components/            input/transformation/contract/table editors
├─ lib/api/               typed API clients
└─ e2e/                   Playwright behavior tests
tests/MappingDemo.Tests/  unit + integration + contract/golden tests
```

### 3.3 Main data flow

```text
Input folder
  → FileWatcherService
  → file_jobs + outbox (same DB transaction)
  → Kafka file-import
  → snapshot/hash/parse
  → Source Table + row_jobs + outbox per imported row
  → Kafka row-normalize
  → compile/evaluate pinned Transformation Program
  → exactly one destination adapter
      ├─ LegacyTable    → Normalized Table
      ├─ ApiContract    → delivery_jobs → Kafka → Partner API
      ├─ JsonFile       → encrypted items → finalizer → JSON artifact
      └─ DatabaseTable  → encrypted intent → Kafka → PostgreSQL/SQL Server
```

หลักฐาน: `src/MappingDemo.Api/Program.cs:200`, `src/MappingDemo.Api/Services/FileIntake.cs:71`, `src/MappingDemo.Worker/Program.cs:177`, `src/MappingDemo.Worker/RowNormalizeHandler.cs:189`

## 4. Observed functional requirements (EARS)

### 4.1 Table Catalog และ physical schema

**OBS-TBL-001 — Catalog**  
เมื่อผู้ใช้ที่มีสิทธิ์เรียก Table API ระบบต้อง list/search/filter ตาม kind และ verification status, paginate สูงสุด 100 รายการ และอ่านรายละเอียด table/columns ได้  
หลักฐาน: `src/MappingDemo.Api/Controllers/TablesController.cs:37`

**OBS-TBL-002 — Typed creation**  
เมื่อได้รับชื่อ, `Source`/`Normalized` kind และ typed columns ที่ valid ระบบต้องสร้าง metadata และ physical PostgreSQL table พร้อม system columns  
หลักฐาน: `src/MappingDemo.Api/Controllers/TablesController.cs:231`, `src/MappingDemo.Shared/Tables/DdlBuilder.cs:1`

**OBS-TBL-003 — Expand-only mutation**  
เมื่อ operator ที่มีสิทธิ์ส่ง actor/reason/request ID ระบบต้องอนุญาตเพิ่ม column แต่ไม่มี API ลบ/rename/change type  
หลักฐาน: `src/MappingDemo.Api/Controllers/TablesController.cs:175`

**OBS-TBL-004 — Verification and impact**  
เมื่อขอตรวจ schema ระบบต้อง rate-limit, inspect physical schema, persist result และแสดง Config Version/pinned-job references ที่ได้รับผลกระทบ  
หลักฐาน: `src/MappingDemo.Api/Controllers/TablesController.cs:93`, `src/MappingDemo.Api/Controllers/TablesController.cs:113`, `src/MappingDemo.Api/Controllers/TablesController.cs:132`

### 4.2 Mapping Config และ version lifecycle

**OBS-CFG-001 — Config identity**  
เมื่อสร้าง Mapping Config ระบบต้องผูกชื่อและ input folder ที่ไม่ซ้ำกับ Source Table และ destination draft หนึ่งชนิด  
หลักฐาน: `src/MappingDemo.Api/Controllers/MappingConfigsController.cs:102`

**OBS-CFG-002 — Immutable version**  
เมื่อสร้าง Config Version ระบบต้อง persist Input Layout, Transformation Program, Destination Definition และ accepted extensions เป็น version ใหม่ โดยไม่แก้ version เดิม  
หลักฐาน: `src/MappingDemo.Api/Controllers/MappingConfigsController.cs:242`, `src/MappingDemo.Api/Services/MappingConfigService.cs:309`

**OBS-CFG-003 — Validation**  
เมื่อ validate draft ระบบต้อง compile input, transformation, destination และ dependencies โดยไม่บันทึก runtime job  
หลักฐาน: `src/MappingDemo.Api/Controllers/MappingConfigsController.cs:350`, `src/MappingDemo.Api/Services/MappingDraftService.cs:37`

**OBS-CFG-004 — In-memory preview**  
เมื่อ preview text/source rows หรือ upload workbook ระบบต้อง parse และ transform ใน memory; ต้องไม่สร้าง File Job, Source row หรือ delivery  
หลักฐาน: `src/MappingDemo.Api/Controllers/MappingConfigsController.cs:416`, `src/MappingDemo.Api/Controllers/MappingConfigsController.cs:773`

**OBS-CFG-005 — Safe destination preview**  
เมื่อ destination เป็น ApiContract หรือ JsonFile ระบบต้องแสดง mapped payload/artifact preview และ hash โดยไม่ส่ง HTTP request หรือเขียน final file  
หลักฐาน: `src/MappingDemo.Api/Contracts/MappingConfigs/MappingDraftContracts.cs:84`, `web/e2e/api-contract.spec.ts:140`, `web/e2e/json-file-workbench.spec.ts:198`

**OBS-CFG-006 — Activation gate**  
เมื่อ activate version ระบบต้องตรวจ profile availability, rollout/runtime controls, fresh database schema เมื่อเกี่ยวข้อง, compile และ lookup dependencies ก่อนสลับ active version  
หลักฐาน: `src/MappingDemo.Api/Controllers/MappingConfigsController.cs:502`

### 4.3 Input layouts

**OBS-IN-001 — Delimited**  
เมื่อ layout เป็น delimited ระบบต้องรองรับ `.txt/.csv`, UTF-8, delimiter, preamble, optional single header row และ mapping header → Source column  
หลักฐาน: `src/MappingDemo.Shared/Input/InputLayoutDefinition.cs:11`, `src/MappingDemo.Shared/Input/InputFileExtensionPolicy.cs:13`

**OBS-IN-002 — Fixed width**  
เมื่อ layout เป็น fixed-width ระบบต้องรองรับ discriminator, expected length, 1-based UTF-16 code-unit slices, import/ignore record rules, sequence/min/max occurrence และ unmatched-as-error  
หลักฐาน: `src/MappingDemo.Shared/Input/FixedWidthInputLayout.cs:5`, `src/MappingDemo.Shared/Input/InputLayoutCompiler.cs:82`

**OBS-IN-003 — Excel**  
เมื่อ layout เป็น Excel ระบบต้องรองรับ `.xlsx/.xls`, auto-detect หรือชนิดที่ pin, sheet, header/data rows, header mapping และ blank-row policy `Skip`/`Error`  
หลักฐาน: `src/MappingDemo.Shared/Input/ExcelInputLayout.cs:3`, `src/MappingDemo.Shared/Input/InputRecordModule.cs:35`

**OBS-IN-004 — Input diagnostics**  
เมื่อ layout ผิด ระบบต้องรายงาน path/code/message เช่น duplicate mapping, invalid slice, ambiguous discriminator, sequence mismatch หรือ incompatible extension  
หลักฐาน: `src/MappingDemo.Shared/Input/InputLayoutCompiler.cs:9`, `src/MappingDemo.Shared/Input/InputFileExtensionPolicy.cs:28`

### 4.4 Transformation Program

**OBS-TRN-001 — Filter and branches**  
เมื่อประเมิน Source row ระบบต้องใช้ optional `sourceWhen`, สร้าง 0..N output branches และใช้ `emitWhen` ต่อ branch; empty successful result ต้องเป็น `Filtered`  
หลักฐาน: `src/MappingDemo.Shared/Transformation/TransformationProgram.cs:6`, `src/MappingDemo.Shared/Transformation/TransformationEvaluation.cs:13`

**OBS-TRN-002 — Value expressions**  
ระบบต้องรองรับ `source`, `metadata`, `literal`, `coalesce`, `add`, `concat`, `defaultIfBlank`, `trim`, `case`, `substring`, `convert`, `currentDate`, `call`  
หลักฐาน: `src/MappingDemo.Shared/Transformation/TransformationCapabilities.cs:85`

**OBS-TRN-003 — Predicates**  
ระบบต้องรองรับ comparison, null/blank, SQL-like pattern, set membership, boolean composition และ named lookup existence predicates  
หลักฐาน: `src/MappingDemo.Shared/Transformation/TransformationCapabilities.cs:63`

**OBS-TRN-004 — Typed/nested output**  
ระบบต้อง map flat columns หรือ nested contract paths, รองรับ array groups/source slots และ convert เป็น decimal, integer, date, time, date-time, boolean, GUID  
หลักฐาน: `src/MappingDemo.Shared/Transformation/TransformationProgram.cs:45`, `src/MappingDemo.Shared/Transformation/TransformationCapabilities.cs:34`

**OBS-TRN-005 — Deterministic semantics and limits**  
ระบบต้องใช้ `sqlserver-legacy-v1`, timezone Asia/Bangkok, pinned evaluation time และบังคับ limits เช่น outputs 32, fields/output 256, depth 32, nodes 2048  
หลักฐาน: `src/MappingDemo.Shared/Transformation/TransformationCapabilities.cs:10`, `src/MappingDemo.Shared/Transformation/TransformationCapabilities.cs:22`

### 4.5 File intake, import และ normalization

**OBS-FILE-001 — Detection**  
เมื่อ config มี active version ระบบต้องดูไฟล์จาก FileSystemWatcher และ periodic scan; watcher error ต้อง trigger immediate scan  
หลักฐาน: `src/MappingDemo.Api/Services/FileWatcherService.cs:36`, `src/MappingDemo.Api/Services/WatcherRegistry.cs:13`

**OBS-FILE-002 — Ownership**  
เมื่อหลาย config claim folder+extension เดียวกัน ระบบต้องปิด intake สำหรับคู่ที่ ambiguous  
หลักฐาน: `src/MappingDemo.Api/Services/FileWatcherService.cs:185`

**OBS-FILE-003 — Durable intake**  
เมื่อรับไฟล์ ระบบต้องสร้าง File Job ที่ pin Config Version และ outbox message ใน transaction เดียว  
หลักฐาน: `src/MappingDemo.Api/Services/FileIntake.cs:71`

**OBS-FILE-004 — Snapshot and duplicate**  
เมื่อ worker import ระบบต้อง delay ตาม config, copy snapshot, คำนวณ SHA-256 และ mark duplicate หาก content hash ซ้ำภายใต้ config เดียว  
หลักฐาน: `src/MappingDemo.Worker/FileImportHandler.cs:245`

**OBS-FILE-005 — Streaming row availability**  
เมื่อ parse record สำเร็จ ระบบต้อง insert Source row และสร้าง Row Job/outbox ต่อแถวใน transaction เดียว ทำให้ normalize เริ่มได้ก่อน import ทั้งไฟล์เสร็จ  
หลักฐาน: `src/MappingDemo.Worker/FileImportHandler.cs:674`, `src/MappingDemo.Worker/SourceRowWriter.cs:42`

**OBS-FILE-006 — Archive safety**  
เมื่อ import เสร็จ ระบบต้องย้าย original ไป archive ตามวันที่เมื่อ hash ยังตรงกับ snapshot; ถ้า original เปลี่ยนต้อง retain ทั้งสองและ mark `ChangedAfterRead`; file error retry สูงสุด 3 ครั้ง  
หลักฐาน: `src/MappingDemo.Worker/FileImportHandler.cs:23`, `src/MappingDemo.Worker/FileImportHandler.cs:564`

**OBS-FILE-007 — Row retry and errors**  
เมื่อ normalize throw ระบบต้อง retry หลัง 1/2/4 วินาทีรวมสูงสุด 4 attempts; transformation invalid ต้องบันทึก row errors แยกจาก fatal failure  
หลักฐาน: `src/MappingDemo.Worker/RowNormalizeHandler.cs:19`, `src/MappingDemo.Worker/RowNormalizeHandler.cs:189`

**OBS-FILE-008 — Retry versus reprocess**  
เมื่อผู้ใช้ retry File/Row Job ระบบต้องใช้ job/config เดิมและอนุญาตเฉพาะ failed state; เมื่อ reprocess File Job ระบบต้องสร้าง Row Jobs ใหม่ด้วย Config Version ที่เลือก  
หลักฐาน: `src/MappingDemo.Api/Controllers/FileJobsController.cs:80`, `src/MappingDemo.Api/Controllers/FileJobsController.cs:104`, `src/MappingDemo.Api/Controllers/RowJobsController.cs:17`

### 4.6 Destination: LegacyTable

**OBS-DST-LEG-001 — Current result reconciliation**  
เมื่อ transformation สำเร็จ ระบบต้อง upsert output ตาม `(source_row_id, output_key)` และลบ output เก่าที่ไม่อยู่ในผลล่าสุด  
หลักฐาน: `src/MappingDemo.Worker/NormalizedRowWriter.cs:18`, `src/MappingDemo.Worker/NormalizedRowWriter.cs:99`

**OBS-DST-LEG-002 — Status**  
เมื่อ output count เป็นศูนย์ระบบต้อง mark Row Job `Filtered`; เมื่อมี output ต้อง mark `Done`  
หลักฐาน: `src/MappingDemo.Worker/NormalizedRowWriter.cs:164`

### 4.7 Destination: ApiContract

**OBS-DST-API-001 — Pinned contract/profile**  
เมื่อสร้าง version ระบบต้อง resolve exact published contract, canonical schema/hash/path catalog, payload bindings และ active profile version แล้ว pin snapshot  
หลักฐาน: `src/MappingDemo.Api/Controllers/MappingConfigsController.cs:242`, `src/MappingDemo.Shared/Contracts/VersionedContractRegistry.cs:55`

**OBS-DST-API-002 — Durable encrypted intent**  
เมื่อ row output valid ระบบต้องเติม system-bound values, validate payload, encrypt bytes และ persist payload hash/idempotency key ก่อน enqueue delivery  
หลักฐาน: `src/MappingDemo.Worker/Destinations/ApiContractDestinationAdapter.cs:144`

**OBS-DST-API-003 — HTTP delivery**  
เมื่อ dispatch job ระบบต้องส่ง JSON ด้วย profile method/path, idempotency header, correlation header, timeout และ auth `None`, mTLS หรือ OAuth2 client credentials  
หลักฐาน: `src/MappingDemo.Worker/Delivery/DeliveryHttpAdapter.cs:36`

**OBS-DST-API-004 — Retry classification**  
เมื่อ response เป็น 408/429/5xx หรือ transport failure ระบบต้อง retry; 2xx ต้อง delivered; HTTP อื่นต้อง permanent failure; สูงสุด 6 attempts พร้อม backoff/jitter/Retry-After  
หลักฐาน: `src/MappingDemo.Shared/Delivery/DeliveryRetryPolicy.cs:10`, `src/MappingDemo.Worker/Delivery/DeliveryJobHandler.cs:200`

**OBS-DST-API-005 — Business resend**  
เมื่อ job Delivered และ operator มีสิทธิ์พร้อม reason + idempotent action GUID ระบบต้องสร้าง business resend แยกจาก retry/reprocess  
หลักฐาน: `src/MappingDemo.Api/Controllers/DeliveryJobsController.cs:41`

**OBS-DST-API-006 — Rollout controls**  
เมื่อ activate ApiContract ระบบต้องผ่าน per-config stage `Shadow`/`Canary`/`Expanded` และ global activation/delivery controls  
หลักฐาน: `src/MappingDemo.Api/Controllers/DeliveryOperationsController.cs:17`, `src/MappingDemo.Api/Controllers/DeliveryOperationsController.cs:39`

### 4.8 Destination: JsonFile

**OBS-DST-JSON-001 — Whole-file artifact**  
เมื่อทุก latest Row Job เสร็จและ File Job imported ระบบต้องรวม outputs ตาม row/output ordinal เป็น JSON array หนึ่งไฟล์  
หลักฐาน: `src/MappingDemo.Worker/Destinations/JsonExportReadiness.cs:68`, `src/MappingDemo.Worker/Destinations/JsonFileFinalizer.cs:418`

**OBS-DST-JSON-002 — Encrypted staging**  
เมื่อ row output valid ระบบต้องเก็บ staged JSON object แบบ encrypted พร้อม SHA-256 และ reconcile stale items  
หลักฐาน: `src/MappingDemo.Worker/Destinations/JsonFileDestinationAdapter.cs:170`

**OBS-DST-JSON-003 — Deterministic naming/revisions**  
ระบบต้อง resolve relative folder/template ด้วย safe tokens และสร้าง artifact revision ใหม่เมื่อ config เปลี่ยนหรือ revision ก่อน Published/Blocked  
หลักฐาน: `src/MappingDemo.Worker/Destinations/JsonFileDestinationAdapter.cs:249`, `src/MappingDemo.Shared/Destinations/JsonExportArtifactPath.cs:1`

**OBS-DST-JSON-004 — Safe atomic publication**  
เมื่อ finalizer publish ระบบต้อง enforce profile quota/minimum free space/path containment, write temp, compute metadata และ move แบบ no-overwrite; final file ต้องมองเห็นเมื่อครบเท่านั้น  
หลักฐาน: `src/MappingDemo.Worker/Destinations/JsonExportPublisher.cs:53`, `src/MappingDemo.Worker/Destinations/JsonExportPublisher.cs:109`

**OBS-DST-JSON-005 — Recovery and retention**  
เมื่อ worker restart หรือ lease หมด ระบบต้อง recover Pending/RetryableFailed/stale Finalizing jobs; cleanup ต้อง purge encrypted payload และลบ expired artifact ตาม policy  
หลักฐาน: `src/MappingDemo.Worker/Destinations/JsonExportRecoveryScheduler.cs:15`, `src/MappingDemo.Worker/Destinations/JsonExportCleanupService.cs:42`

**OBS-DST-JSON-006 — Operations**  
เมื่อ operator ดู history ระบบต้องคืนเฉพาะ relative path, count, bytes, SHA-256, timestamps และ redacted error; retry ได้เฉพาะ `RetryableFailed`  
หลักฐาน: `src/MappingDemo.Api/Controllers/JsonExportsController.cs:13`, `src/MappingDemo.Api/Controllers/JsonExportsController.cs:32`

### 4.9 Destination: DatabaseTable

**OBS-DST-DB-001 — Safe catalog**  
เมื่อเลือก external target ระบบต้อง list server-owned logical profiles/allowed schemas/tables และ inspect schema โดยไม่คืน connection string  
หลักฐาน: `src/MappingDemo.Api/Controllers/DatabaseDestinationsController.cs:13`

**OBS-DST-DB-002 — Provider support**  
ระบบต้อง inspect และ write ไป PostgreSQL หรือ SQL Server ผ่าน provider adapters  
หลักฐาน: `src/MappingDemo.Shared/DatabaseDestinations/DatabaseDestinationProfiles.cs:6`, `src/MappingDemo.Worker/Program.cs:72`

**OBS-DST-DB-003 — Snapshot and drift**  
เมื่อสร้าง/activate/write ระบบต้องใช้ pinned target schema/hash และ fresh inspection; breaking drift หรือ unavailable schema ต้อง block delivery  
หลักฐาน: `src/MappingDemo.Shared/DatabaseDestinations/TargetSchemaModels.cs:55`, `src/MappingDemo.Worker/DatabaseDelivery/DatabaseWriteAdapters.cs:140`

**OBS-DST-DB-004 — Insert-only**  
เมื่อส่ง mapped values ระบบต้องสร้าง parameterized `INSERT` จาก writable mapped columns; ห้ามรับ raw SQL และไม่มี runtime DDL ที่ target  
หลักฐาน: `src/MappingDemo.Worker/DatabaseDelivery/DatabaseWriteAdapters.cs:307`, `src/MappingDemo.Worker/DatabaseDelivery/DatabaseWriteAdapters.cs:531`

**OBS-DST-DB-005 — Duplicate reconciliation**  
เมื่อใช้ existing unique key ระบบต้องตรวจ full key หลัง unique/ambiguous outcome แล้ว classify `AlreadyDelivered`, `DeliveryConflict` หรือ `UnknownOutcome` จาก canonical value comparison  
หลักฐาน: `src/MappingDemo.Worker/DatabaseDelivery/DatabaseWriteAdapters.cs:372`

**OBS-DST-DB-006 — Manual unknown outcome**  
เมื่อไม่มี reconciliation key และผล commit ไม่แน่ชัด ระบบต้องหยุด automatic retry ที่ `UnknownOutcome`; operator ต้องส่ง reason/correlation และยอมรับ duplicate risk หากเลือก retry  
หลักฐาน: `src/MappingDemo.Worker/DatabaseDelivery/DatabaseDeliveryScheduler.cs:16`, `src/MappingDemo.Api/Controllers/DatabaseDeliveryOperationsController.cs:17`

**OBS-DST-DB-007 — Background schema monitor**  
เมื่อ feature เปิด ระบบต้องตรวจ bounded active/pending targets เป็นรอบ, persist compatibility snapshot และสร้าง safe metrics/alerts  
หลักฐาน: `src/MappingDemo.Worker/DatabaseDelivery/DatabaseTargetSchemaMonitor.cs:89`

### 4.10 Admin catalogs และ operations

**OBS-ADM-001 — API Destination Profile lifecycle**  
เมื่อผู้มีสิทธิ์ดูแล profile ระบบต้องรองรับ create draft, clone draft, update, validate, activate, rollback และ disable พร้อม ETag/If-Match concurrency และ audit reason  
หลักฐาน: `src/MappingDemo.Api/Controllers/ApiDestinationProfileAdminController.cs:63`

**OBS-ADM-002 — Profile safety**  
ระบบต้องเก็บ secret เป็น reference, redact endpoint/auth reference เมื่อไม่มี endpoint-read permission และ validate public HTTPS port 443/DNS เพื่อกัน loopback/private/reserved network  
หลักฐาน: `src/MappingDemo.Api/Controllers/ApiDestinationProfileAdminController.cs:314`, `src/MappingDemo.Shared/Contracts/ApiDestinationProfileNetworkPolicy.cs:8`

**OBS-ADM-003 — Contract Registry lifecycle**  
เมื่อผู้มีสิทธิ์ดูแล contract ระบบต้อง create/update Draft, validate/canonicalize, clone, publish immutable version และ retire version  
หลักฐาน: `src/MappingDemo.Api/Controllers/ContractAdminController.cs:65`

**OBS-ADM-004 — Compatibility/concurrency**  
เมื่อ publish contract ระบบต้อง classify Initial/NonBreaking/Breaking, require explicit breaking acknowledgement และใช้ ETag/If-Match optimistic concurrency  
หลักฐาน: `src/MappingDemo.Shared/Contracts/VersionedContractRegistry.cs:13`, `src/MappingDemo.Api/Controllers/ContractAdminController.cs:165`

**OBS-ADM-005 — Safe operational views**  
ระบบต้องมี profile, contract, table, API delivery, JSON export และ database delivery metrics/alerts โดยไม่คืน credential, payload body, raw SQL หรือ absolute output root  
หลักฐาน: `src/MappingDemo.Api/Controllers/ApiDestinationProfileOperationsController.cs:13`, `src/MappingDemo.Api/Controllers/ContractRegistryOperationsController.cs:13`, `src/MappingDemo.Api/Controllers/TableAdministrationOperationsController.cs:13`

### 4.11 Web user interface

**OBS-WEB-001 — Config Studio**  
เว็บต้อง list configuration summary, versions, outputs, rules และ active state; สร้าง config ได้สำหรับ destination ทั้ง 4 ชนิดตาม capability/gate  
หลักฐาน: `web/app/configs/page.tsx:16`, `web/app/configs/new/page.tsx:49`

**OBS-WEB-002 — Workbench**  
เว็บต้องมี editors สำหรับ Delimited/Fixed-width/Excel, transformation expressions, nested contract/array mapping, preview, create/clone version, compare และ activate  
หลักฐาน: `web/components/input-layout/input-layout-editor.tsx:265`, `web/components/transformation/transformation-editor.tsx:201`, `web/components/contracts/api-contract-editor.tsx:441`

**OBS-WEB-003 — Delivery dashboard**  
เว็บต้องแสดง API delivery, JSON export, database delivery, metrics/alerts/schema health และ authorized controls/resolution จากหน้า `/delivery`  
หลักฐาน: `web/app/delivery/page.tsx:19`

**OBS-WEB-004 — Admin screens**  
เว็บต้องมี API profiles, Contract Registry และ Table Catalog/detail admin screens  
หลักฐาน: `web/app/admin/api-destination-profiles/page.tsx:51`, `web/app/admin/contracts/page.tsx:37`, `web/app/admin/tables/page.tsx:40`

**OBS-WEB-005 — API proxy**  
เว็บต้อง proxy same-origin `/api/*` ไป `API_BASE_URL` ซึ่ง default เป็น `http://localhost:5181`  
หลักฐาน: `web/next.config.ts:3`

### 4.12 Platform/API support

**OBS-PLT-001 — Database bootstrap**  
เมื่อ API start ระบบต้อง apply embedded migrations และ bootstrap registry/catalog ที่ config ระบุ ก่อนรับ request  
หลักฐาน: `src/MappingDemo.Api/Program.cs:214`

**OBS-PLT-002 — Health and API reference**  
ระบบต้องมี `/health`, `/health/db`; ใน Development ต้องเปิด OpenAPI JSON และ Scalar UI  
หลักฐาน: `src/MappingDemo.Api/Controllers/HealthController.cs:18`, `src/MappingDemo.Api/Program.cs:231`

**OBS-PLT-003 — Capability discovery**  
ระบบต้อง expose formats, operators, expressions, functions, lookups, limits, destination kinds และ safe file profiles ผ่าน `/mapping-capabilities`  
หลักฐาน: `src/MappingDemo.Api/Controllers/MappingCapabilitiesController.cs:20`

## 5. Complete HTTP API inventory

| Route group | Operations |
|---|---|
| `/health` | health, DB health |
| `/mapping-capabilities` | read supported mapping capabilities |
| `/mapping-configs` | list, detail, create config, create/validate/preview/preview-file/activate version |
| `/tables` | permissions, list/detail, latest/fresh verification, references, create, add column |
| `/file-jobs` | list/detail/errors/row history, retry import, reprocess |
| `/row-jobs` | retry failed row job |
| `/api-destination-profiles` | list safe profiles, resolve contract/profile |
| `/file-destination-profiles` | list safe profiles, resolve contract/profile |
| `/database-destinations` | profiles, schemas, tables, inspect table |
| `/delivery-jobs` | list by File Job, retry, authorized business resend |
| `/json-exports` | list history by File Job, authorized retry |
| `/delivery-operations` | dashboard snapshot, global controls, per-config rollout |
| `/database-delivery-operations` | snapshot, retry, mark delivered, mark failed |
| `/admin/api-destination-profiles` | permissions, list/detail, create/update/validate/activate/rollback/disable |
| `/admin/api-destination-profile-operations` | safe profile metrics/alerts |
| `/admin/contracts` | permissions, list/detail, create/update/validate/clone/publish/retire |
| `/admin/contract-registry-operations` | safe contract metrics/alerts |
| `/admin/table-operations` | safe table metrics/alerts |

หลักฐานรวม: `src/MappingDemo.Api/Controllers/`

## 6. Data model/persistence

ระบบเก็บอย่างน้อย:

- Table metadata/columns, verification snapshots, admin audit
- Mapping Configs/immutable versions/active version
- File Jobs, Source rows ใน generated physical tables, Row Jobs, Row Errors
- Transactional outbox
- API Delivery Jobs/Attempts/Resend audit/runtime controls/rollouts
- JSON Export Jobs/Items/Retry audit/operation events
- API Destination Profiles/Versions/contracts/bindings/audit
- Contracts/Versions/audit
- Database Delivery Jobs/Attempts/resolution audit/schema checks

หลักฐาน: migrations `src/MappingDemo.Shared/Database/Migrations/0001_table_definitions.sql:1` ถึง `0020_database_delivery.sql:23`

## 7. Observed non-functional requirements

### Security

- API profile URL policy: HTTPS, DNS host, port 443, no credentials/path/query/fragment; runtime DNS blocks loopback/private/reserved addresses and redirects disabled
- Secret valuesไม่อยู่ใน profile payload; runtime resolve จาก environment secret reference
- API/JSON/DB durable payload valuesเข้ารหัสก่อน persist
- External DB ใช้ logical profile + named connection string; API/UI ไม่คืน connection string/raw SQL/payload
- Admin mutation ต้องมี operator, reason/request ID, permission/config gate; optimistic concurrency ใช้ ETag/If-Match
- ข้อสังเกตสำคัญ: `Program.cs` เรียก `UseAuthorization()` แต่ไม่พบ `AddAuthentication()`/authentication middleware ใน repo; production identity integration จึงยังไม่เห็นจาก code path นี้

หลักฐาน: `src/MappingDemo.Shared/Contracts/ApiDestinationProfileNetworkPolicy.cs:8`, `src/MappingDemo.Api/Program.cs:227`, `src/MappingDemo.Api/Services/ProfileAdministrationAuthorizer.cs:1`

### Reliability and consistency

- Transactional outbox ผูก job intent กับ event publication
- Kafka producer เปิด idempotence/Acks.All; consumersปิด auto commit
- Job handlersตรวจ terminal state และใช้ unique constraints/idempotency identities
- API payload retryใช้ immutable encrypted bytes; JSON artifactใช้ deterministic identity/revision; DB intentใช้ deterministic mapped-values hash
- Lease/recovery มีใน delivery/finalizer flows; archive retryแยกจาก normalize retry

หลักฐาน: `src/MappingDemo.Worker/Program.cs:129`, `src/MappingDemo.Worker/RowNormalizeHandler.cs:246`, `src/MappingDemo.Worker/Destinations/JsonFileFinalizer.cs:26`

### Limits/performance

- Transformation: outputs 32, fields/output 256, collection items 256, depth 32, nodes 2048
- Preview text: 1 MiB, parsed records 50; multipart requestมี bounded limit
- API profile/contract catalogsมี bounded caches; external schema inspectorมี per-profile cache
- Table listing limit 100; schema monitor targets bounded/configurable
- Worker concurrencyแยกตาม Kafka consumer groups/config; default Development ส่วนใหญ่ 1

หลักฐาน: `src/MappingDemo.Shared/Transformation/TransformationCapabilities.cs:22`, `src/MappingDemo.Api/Services/MappingDraftService.cs:1`, `src/MappingDemo.Api/Controllers/TablesController.cs:37`

### Observability/error handling

- Structured logging + scoped IDs สำหรับ config/file/row/delivery
- Safe metrics/alerts สำหรับ profile, registry, table, API/JSON/DB delivery
- User-visible error contractsใช้ validation problems, conflict/precondition/rate-limit และ redacted diagnostics
- Row-level invalid dataไม่หยุด valid rows ในไฟล์เดียวกัน

## 8. Inferred acceptance criteria

### AC-001 — File to LegacyTable

Given active config และ valid input file  
When watcherรับไฟล์และ workersทำงานครบ  
Then Source rowsต้องครบ, valid outputsต้องอยู่ Normalized table, invalid rowsต้องมี Row Errors และ original fileต้องถูก archive

### AC-002 — Duplicate file

Given content เดิมเคย import ภายใต้ config เดียวกัน  
When fileชื่อใหม่แต่ SHA-256 เดิมเข้ามา  
Then File Jobต้องเป็น Duplicate และห้ามสร้าง Source/Row output ซ้ำ

### AC-003 — Immutable config pinning

Given File Jobถูกสร้างด้วย active version N  
When operator activate version N+1 ระหว่างประมวลผล  
Thenงานเดิมต้องใช้ N ต่อ; งานรับใหม่หลัง watcher refreshจึงใช้ N+1

### AC-004 — Preview side-effect free

Given valid draft + sample  
When user preview  
Thenต้องได้ parsed/transformed result โดยไม่เพิ่ม jobs/outbox/source rows, ไม่ส่ง API และไม่เขียน final JSON

### AC-005 — API retry

Given partnerตอบ 503  
When delivery attemptจบ  
Then jobต้องเป็น RetryableFailed พร้อม next-at; เมื่อ 2xx ภายหลังต้องเป็น Delivered โดยใช้ idempotency identity เดิม

### AC-006 — JSON atomicity

Given JSON exportกำลัง finalize  
Whenเขียนไม่ครบหรือ storage policyไม่ผ่าน  
Then final pathต้องไม่มี partial artifact; jobต้อง RetryableFailed/Blocked พร้อม safe error

### AC-007 — Database schema drift

Given pinned snapshot  
When fresh target schemaมี breaking change  
Then activation/writeต้องถูก block และห้าม execute INSERT

### AC-008 — Unknown database outcome

Given no reconciliation key และ commit outcomeพิสูจน์ไม่ได้  
When schedulerตรวจ job  
Then jobต้องเป็น UnknownOutcome และห้าม auto retry จน operator resolve พร้อม audit context

### AC-009 — Contract publishing

Given Draft schemaเป็น breaking change  
When publishโดยไม่ acknowledge  
Thenระบบต้องตอบ conflict; เมื่อ acknowledge + valid If-Matchจึง publish immutable versionได้

### AC-010 — Admin data minimization

Given userไม่มี endpoint-read permission  
Whenอ่าน API Destination Profile  
Then responseต้องไม่รวม base URLs และ auth secret reference

## 9. Uncertainties and questions

- [ ] Production authentication/identity providerอยู่ที่ reverse proxy หรือยังไม่ได้ต่อ? Repoไม่พบ `AddAuthentication()`
- [ ] DatabaseTable Gate I พร้อมเปิดจริงหรือยัง? Default API flags `DatabaseTableDestinationCreation/Activation=false`; Worker flags `DatabaseTableDelivery/SchemaMonitor=false`
- [ ] `ContractRegistry:SourceMode=Bundled` ตั้งใจใช้เฉพาะ fallback/dev หรือรองรับ production read-only?
- [ ] File/API destination profilesจะย้ายทั้งหมดเข้า PostgreSQL หรือคง config-file catalogบางส่วน?
- [ ] UI route `/prototype/file-to-json` ต้องเก็บไว้เพื่อ design reference หรือควรซ่อนจาก deploy?
- [ ] Live-provider acceptance ของ PostgreSQL/SQL Server และ production auth ยังต้องยืนยันนอก repo; automated testsไม่แทน infrastructure sign-off

## 10. Recommendations

1. เพิ่มเอกสาร deployment authentication boundary ให้ชัด: ใครสร้าง `ClaimsPrincipal`, header ไหนถูก strip/trust ที่ proxy
2. แสดง feature-gate matrix ต่อ environment เพื่อไม่ให้ “มีโค้ด” ถูกเข้าใจว่า “เปิดใช้งานแล้ว”
3. Generate endpoint catalog จาก OpenAPI ใน CI เพื่อลด drift ระหว่างเอกสารกับ controllers
4. เพิ่ม smoke test ที่ start API+Worker+Web พร้อม infra แล้วพิสูจน์หนึ่ง flow ต่อ destination
5. แยก prototype route ออกจาก production navigation/build flag ให้ชัด

## 11. Verification evidence

- พบ migrations 20 ชุด (`0001`–`0020`)
- พบ xUnit `[Fact]/[Theory]` 353 declarations
- พบ Playwright `test(...)` 23 cases
- ไม่พบ `TODO|FIXME|HACK|XXX` ใน `src/`, `web/`, `tests/` ตอนสำรวจ
- `dotnet test tests/MappingDemo.Tests/MappingDemo.Tests.csproj --no-restore --nologo`: ผ่าน 546, fail 0, skip 0
- Live PostgreSQL/Kafka/provider acceptance ยังต้องรันแยกตาม environment gates

