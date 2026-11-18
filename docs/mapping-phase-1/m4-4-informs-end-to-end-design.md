---
title: M4.4 — ทดสอบ Mapping ถึง Informs จริงบน local
status: approved
last_updated: 2026-11-18
---

# M4.4 — ทดสอบ Mapping ถึง Informs จริงบน local

ผู้ใช้ยืนยัน source batch 12 ไฟล์และ public seams/scenarios แล้ว 2026-11-18; การยืนยันนี้ครอบคลุม implementation/tests ภายใน Mapping ไม่ใช่การแก้ Informs/Web หรือรับรอง UAT ของทุก partner

## ขอบเขตและ preflight ที่ทำแล้ว

- Mapping ฐาน source `3fd48f8` และเอกสาร `7f340fe`; M4.4 ยังไม่ปิดจากผล stub ของ M4.1–M4.3
- Checkout Informs ที่อยู่ข้าง workspace ยังเป็น `6ab23ad` ซึ่งขาด `fileReceivedAt`; ตรวจ origin/develop แล้วพบ `f1eca8df71af01cc491976319e037cdd13b2dff6` ใหม่กว่า จึงอ่านจาก clone ชั่วคราวโดยไม่เปลี่ยน checkout เดิม
- Build image local `insurancevalidation/informs:m44-f1eca8d` ด้วย Dockerfile ของ Informs ที่ commit นี้และ label revision เดียวกัน; `.dockerignore` ของ Informs ตัด appsettings.Development.json ออกจาก context และตรวจว่าไม่มีไฟล์นี้ใน image แล้ว
- รัน Informs จริงแบบชั่วคราว ปิด messaging และไม่เชื่อม DB; `GET /api/pa/informs/schema/1` ตอบ **200**, `$id=insurance-validation/pa-inform/1`, root มี `fileReceivedAt` ตาม D-80; SHA-256 ของ schema จาก HTTP คือ `38b1fc59bf83dda277eb2143e841a1fc34c89ef8f7b245d8a4ba8ab14e7da5bd`; หยุด container preflight แล้ว
- ผลนี้ยืนยันว่า code/runtime schema ใหม่พร้อมให้ทดสอบ ไม่ใช่ผลรับใบ/ตรวจครบ end-to-end และไม่ยืนยัน deploy บริษัท; Q4 ฝั่งรับข้อมูลจะตรวจต่อด้วย payload/เวลาจริงของ File Job ในชุดทดสอบ

## จุดทดสอบที่เสนอให้ยืนยันพร้อม source batch

ใช้ skill tdd: ทดสอบพฤติกรรมผ่าน public interfaces ทีละกรณี ใช้ fixture จัดข้อมูลเริ่มต้น ไม่เขียน tests ของ private methods หรือพึ่ง query schema ของ Informs เพื่อยืนยันผล

| จุดทดสอบ | สิ่งที่ตรวจได้ |
|---|---|
| Input folder → hosted intake/import/transform/delivery ของ Mapping + PostgreSQL/Kafka จริง | File Job และ Source เกิดจาก reader/worker จริง, ไม่มี direct insert outputs เพื่อทำให้ success ดูผ่าน |
| Mapping catalog/operations APIs ของ M4.3 | schema ที่ fetch จาก Informs จริงถูก pin; counts, raw Source/Payload, hashes, attempts, flags, retry schedule และ replay ตรวจผ่าน API |
| Informs `GET schema`, `POST /api/pa/informs` และ UW read APIs | รับใบจริง, อ่านใบ/snapshots/file counts ผ่าน API, 201/200/replayed/replaced/400/409 และ metadata D-80; ไม่อ้าง project/assembly หรืออ่าน DB `non_motor` จาก Mapping |
| HTTP fault boundary และ process restart | forward เข้า Informs จริงแล้วทิ้ง acknowledgement, proxy ตอบ 503 ก่อน forward ตามกรณี fault ที่ระบุ, restart Mapping แล้วใช้ bytes/key เดิม |
| Informs concurrency limiter จริง | ตั้ง max concurrent=1 และถือ permit ด้วย streaming request ที่ยังส่ง body ไม่ครบ; รอ probe ตอบ429ก่อนให้ Mapping ส่ง, จากนั้นปล่อย stream; ไม่มีการแก้ Informs/DB เพื่อสร้าง429 |

Informs ในชุดนี้เปิดเฉพาะ intake/UW reads และปิด validation messaging เพื่อให้ตรวจ Received/Cancelled/replacement ได้แน่นอน; การตรวจธุรกิจผ่าน Products/Client API ทุก stage ไม่ใช่ acceptance ของ M4.4 และไม่ใช้ผลรับใบแทนผลตรวจผ่าน

## Scenarios และตัวอย่างข้อมูล

1. **MTLS fixture เต็ม:** ใช้ไฟล์สังเคราะห์ 2,556 แถว SHA-256 `CDF70EFDB67641764995D525D97FCB9752F01441CFD9F586A2AD558DD6C29E99` และ program เดิม; schema success ต้อง fetch จาก Informs จริง ไม่เพิ่ม `fileReceivedAt` ลง synthetic schema; Source=2,556, transformed rows=2,556, durable outputs=2,556, Delivered=2,556 เมื่อรับครบ และอ่าน file/inform counts ฝั่ง Informs ให้ตรงกัน; แถว CANCELLED3แถว/ค่าที่แปลงไม่ได้ยังคงตาม fixture เดิม
2. **Delimited/FixedWidth:** ใช้ข้อมูลสมมติ เช่น `reference,amount` → `SYNTHETIC-M44-001,000180` และ fixed-width ที่ได้ raw fields เดียวกัน; แต่ละ format ผ่าน pipeline และ Informs จริง ไม่เพิ่ม partner business rules; `sourceRowNo` เริ่ม1และ `fileReceivedAt` เท่ากับ FileJob.CreatedAt ทุกใบของไฟล์
3. **Replay/restart:** A1 ส่งเข้า Informs และ commit แล้ว proxy ทิ้ง response → Mapping เก็บ OutcomeUnknown/RetryableFailed; restart ใช้ payload bytes/hash/key เดิม → A2 ได้200/replayed/informIdเดิม; outputs1, attempts2, Informs inform1/snapshot1
4. **Replaced/D-44:** partner ส่งไฟล์ใหม่ B1 ที่ reference เดิมของใบ Received พร้อมค่าแก้ เช่น premium150→180 →200/replaced/informIdเดิม; ส่ง exact old key/bytes ผ่าน transport acceptance testภายหลังได้ replay และไม่แทนข้อมูลใหม่กลับเป็น150; ไม่เปิด changed-payload retry/reprocess API ตาม M-22
5. **Permanent rejection:** client ส่ง envelopeที่จงใจผิด contractไป Informsจริงได้400พร้อม safe diagnostics; runner ส่งรายการ CANCELLED ของ referenceที่เคยรับเป็น NEW ได้409/Rejectedตาม BR-011/D-46 และไม่ auto retry; normal rowที่ผิด pinned schema ต้อง Blockedก่อน HTTP ไม่สร้าง outputปลอมเพื่อให้มี400
6. **Throttle/recovery:** real429มี Retry-After; Mappingจอง attemptครั้งเดียว เก็บ NextAtไม่เร็วกว่าเดิม/Retry-After และไม่ส่งก่อนกำหนดหลังrestart; fault503ก่อนforwardระบุว่าเป็น proxy fault ชัดเจน แล้ว recovery ไป Informsจริง; ไม่ reset budget หรือสร้าง attempt7
7. **กระทบยอดและ inspection:** อ่าน summary/rows/outputs/attempts/source/payload ผ่าน M4.3 APIs เทียบ counts และ identity/hashก่อนหลัง replay/inspection; reservations แยกจาก dispatched และ unique outputs ตาม M-36

## การรันและข้อจำกัด

- ใช้ Testcontainers PostgreSQL/Kafka และ Informs API/migrator images ที่ build จาก commit เดียวกัน; containers ตั้งชื่อ `InsuranceValidation.<Service>...`; connection/ports เป็น local isolated fixtures ไม่มี company connection และไม่ใช้ข้อมูลส่วนบุคคลจริง (D-70)
- Informs migrations รันโดย migrator ของ Informs ใน DB test ของบริการนั้น; Mapping migrations ใช้ DbContext ของ Mapping ตามเดิม ไม่เพิ่มตาราง cross-schemaหรือแก้ DB ของ checkout เดิม
- `scripts/test-e2e.ps1` รับ Informs source path เป็น parameterเพื่อ build Dockerfile ของ dependencyแบบอ่านอย่างเดียวและ record revision/images; ไม่ hard-code path workspace/checkout และไม่เพิ่ม project/assembly reference ข้ามบริการ
- Acceptance tests ใช้ `MAPPING_E2E_INFORMS_IMAGE`, `MAPPING_E2E_INFORMS_MIGRATOR_IMAGE`, `MAPPING_E2E_INFORMS_REVISION`; รันปกติที่ไม่มี dependency images จะระบุ external E2E tests เป็น skip พร้อมเหตุผล ไม่ใช้เป็นหลักฐานปิด M4.4
- รอบปิด M4.4 ต้องตั้งค่าครบและรัน `check.ps1` ให้ external E2E tests ถูก execute จริง ไม่มี filter/skip; ใช้ temporary sequential-collection RunSettings ตามข้อจำกัด M4.2 และคง concurrency ภายใน tests; บันทึก revision/image IDs/schema hash/fixture hash/counts/TRX/ผลจริง
- หลัง acceptance ของแต่ละ format ผ่าน จึงปรับ capability `available=true` เฉพาะ Excel/Delimited/FixedWidth และ ApiContract ตามความพร้อมของ engine; destinationsอื่นยัง false, worker defaultsยัง disabled, ไม่ถือว่า config/bindings/business acceptance ของทุก partner หรือ Q9/UAT พร้อมแล้ว
- ยังไม่เปลี่ยน M1.2/Q8/Web review, company dependencies Q7, partner fields/fixtures/business bindings Q5/Q9 หรือวันส่งงาน; ไม่ commit/pushอัตโนมัติ

## Source batch ที่ยืนยันสำหรับ implementation

ทุก path อยู่ใน repo prjInsuranceValidationMapping ไม่มีไฟล์ลบ:

| เพิ่ม | หน้าที่ |
|---|---|
| `tests/Mapping.IntegrationTests/EndToEnd/InformsEndToEndFactAttribute.cs` | opt-in external acceptance และระบุเหตุผลเมื่อยังไม่มี dependency images |
| `tests/Mapping.IntegrationTests/EndToEnd/InformsEndToEndFixture.cs` | isolated DB/Kafka/Informs API+migrator lifecycle, HTTP/schema preflight, Mapping host/clients และ cleanup |
| `tests/Mapping.IntegrationTests/EndToEnd/EndToEndTestData.cs` | fixture setup ของ config/layout/program/files จากข้อมูลสังเคราะห์; ไม่มี pathอ้าง workspace/Informs code |
| `tests/Mapping.IntegrationTests/EndToEnd/MappingInformsEndToEndTests.cs` | full MTLS/Delimited/FixedWidth, D-80 metadata และ API counts/inspection |
| `tests/Mapping.IntegrationTests/EndToEnd/DeliveryAcceptanceEndToEndTests.cs` | actual201/200/replay/replaced/400/409 และ late old keyตาม D-44 |
| `tests/Mapping.IntegrationTests/EndToEnd/DeliveryRecoveryEndToEndTests.cs` | lost acknowledgement/restart/503/real429/Retry-After/attempt guards |
| `tests/Mapping.IntegrationTests/EndToEnd/InformsFaultProxy.cs` | loopback proxy ที่ forwardactualInformsและ injecttransportfaultตามscenario ไม่สวม success response |
| `tests/Mapping.IntegrationTests/EndToEnd/InformsRateLimitGate.cs` | streaming request ถือ permit ของ Informsจริงแบบ bounded และปล่อยในfinally |
| `scripts/test-e2e.ps1` | build API+migrator imagesจากsourcepathที่ระบุและรัน E2E/gate พร้อม evidence/config แบบชั่วคราว |

| แก้ | ขอบเขต |
|---|---|
| `src/Mapping/Mapping.Application/Capabilities/Queries/GetCapabilitiesQuery.cs` | เปลี่ยน availability/reasonหลังactualacceptanceผ่านตามขอบเขตข้างต้น ไม่เปลี่ยน response shape/default worker |
| `tests/Mapping.IntegrationTests/Api/MappingCatalogApiTests.cs` | capability regression ให้ตรงกับ featureที่ผ่านactualacceptance |
| `tests/Mapping.IntegrationTests/Delivery/InformsClientTests.cs` | แก้ assertionข้อความ capabilityที่เคยระบุ actualInformsยังpending |

ถ้าพบ bug ใน production source นอก inventory นี้ ให้ระบุไฟล์/วิธีแก้/หลักฐานแล้วขอยืนยันเพิ่มตาม AGENTS.md; ไม่แก้ Informs/Web/demo/Program/Compose/Source writer หรือ schema snapshotเก่าเพื่อกลบ failure

## เกณฑ์ปิด

- User ยืนยันทั้ง public seams/scenarios และ source batch ก่อนเขียน tests; ทำทีละ failing test→ผลจริงตาม tdd
- Scenariosครบและ fullfixture/3formatsใช้ Informsจริง; schema/payload pin/เวลา/keys/countsตรวจได้, faultที่จำลองระบุแยกจากHTTPstatusจริง
- check.ps1รอบเปิดexternaltestsผ่าน ไม่มีskip/abort และบันทึกผลลง plan; ไม่ถือว่า HTTPschema200หรือผลstubเก่าเป็นการปิด M4.4

## Implementation และหลักฐานระหว่างทำ 2026-11-18

- Implementครบ source batch12ไฟล์: เพิ่ม8ไฟล์ใน `tests/Mapping.IntegrationTests/EndToEnd/` และ `scripts/test-e2e.ps1`; แก้ capabilities handler/MappingCatalogApiTests/InformsClientTestsตามรายการที่ยืนยัน ไม่มี package/project reference/migration/production worker/transportเปลี่ยน
- E2E8กรณีผ่านในการรันแยก: intake/replaced/old-key replay, actual400 diagnostics, actual409 terminal/restart, Delimited+FixedWidth pipeline, fullMTLS2556, lost acknowledgement/restart/replay, proxy503 recovery/ครบ6 และ actual429/Retry-After/restart; ยังรอ gate รวมก่อนติ๊ก M4.4
- FullMTLSรันแยกผ่าน4นาที5วินาที: Source2556, transformed2556, outputs2556, Delivered2556, reservations/dispatched2556, actualHTTP201ครบ; hashesของHTTPทุกrequestตรงกับ persisted payload, sourceRowNo1..2556, fileReceivedAtค่าเดิมทุกใบ, Informs file summaryมี Received2553/Cancelled3 และ FirstReceivedAtเท่ากับ FileJob.CreatedAt
- CSV/FixedWidthตรวจ raw amount `000180` ผ่านAPI Source, totalPremium180ใน Informs และ D-80เวลา/ลำดับ/summary; read-only inspectionคง counts; schema successมาจากHTTPจริง hash `38b1fc59bf83dda277eb2143e841a1fc34c89ef8f7b245d8a4ba8ab14e7da5bd` ไม่มี synthetic D-80 schemaในsuccess testsใหม่
- Recoveryทดสอบใหม่หลังแก้test utilityแล้วผ่าน3/3: streaming gateส่ง JSON whitespaceเป็นระยะเพื่อรักษาminimum body rateของreceiver และใช้429/Retry-Afterจริง; รอบแรกconnectionถูกตัดระหว่างถือbodyและทิ้งpendingjobไว้ ทำให้กรณี503นับrequestsของคนละSourceรวมกัน จึงไม่ใช้รอบfailนั้นเป็นหลักฐานผ่าน; รอบแก้ผ่านทั้ง429/restart, lostackและ503/ครบ6ด้วยassertionsเดิม
- เปิด capabilitiesเฉพาะ Excel/Delimited/FixedWidth/ApiContractหลังactualacceptanceรายกรณีผ่าน; catalog regressionเริ่มจากredเมื่อavailableยังfalse แล้วแก้handlerตามขอบเขตที่ยืนยัน; worker defaultsยังdisabledและdestinationsอื่นยังfalse
- `test-e2e.ps1 -Gate` build API+migratorจากcheckoutที่เลือกและปักrevisionเป็นlabel, ตั้งenvชั่วคราว/RunSettings collections sequential, รันcheck.ps1จริง และตรวจTRXว่ามีexternalresultsที่Passedทุกข้อ/ไม่มีskip; เมื่อไม่ตั้งdependency env การเรียกcheckปกติจะskipเฉพาะ8externaltestsพร้อมเหตุผล ไม่ถือเป็นM4.4acceptance
- รอบgateนี้เลือก Informs `f1eca8df71af01cc491976319e037cdd13b2dff6` ตามแบบที่ยืนยัน แม้origin/developขยับเป็น `6cb5664` ระหว่างทำงาน; ใช้cloneชั่วคราว pincommitเดิม ไม่เปลี่ยนcheckoutInformsข้างworkspace และไม่อ้างว่าทดสอบเวอร์ชันใหม่แล้ว
- API image `insurancevalidation/informs:m44-f1eca8df71af` ID `sha256:d623d30e2c9766b1d6bfc439eaf1ca3f8465de10ef5459401243fb0d85249690`; migrator `insurancevalidation/informs-migrator:m44-f1eca8df71af` ID `sha256:436abaa8ab0e02b002ab4c885ca5d1ea3fd8d07c3af8614a20b7838ba8f99772`; ไม่มี appsettings.Development.jsonในcontext/imageตามDockerfile/.dockerignoreของdependency
- Sourceยังไม่commit/push; companydependencies Q7, partnerbusiness bindings/fixtures Q5/Q9, M1.2/Q8/Web, Kubernetes probes/deploymentยังแยกติดตาม; Informsvalidation messagingปิดในfixtureจึงไม่อ้าง product/client/businessvalidationครบทุกstage

### ตรวจ race ใน test harness หลัง gate รวมรอบแรก

- Gateรวมรอบแรกผ่านunit38/38 แต่ integration167/170 (16นาที36วินาที) มี3failuresในrecovery จึงยังไม่ปิดM4.4; core/baseline162ข้อและactualacceptance/fullfixture/3formatsผ่านในรอบนี้
- ย่อ reproduction เป็น `StreamingGate_ShouldAcquireActualPermitBeforeProbeOnEveryRequest` วน20ครั้งกับactualInformsโดยไม่เริ่มMappingworker; รันแล้วพบholderได้429ใน5วินาที จึงยืนยันว่าprobeแย่งpermitก่อนholderได้ ไม่ใช่retryเพิ่มจากMapping
- แก้helperใช้HTTP `Expect: 100-continue`; รอcallbackส่งbodyหลังserver100Continueก่อนยิงprobe และตั้งdeadlineรอสัญญาณ5วินาทีซึ่งสั้นกว่าclientfallback20วินาที; ส่งJSONwhitespaceระหว่างถือpermitตามเดิม ไม่มีการเพิ่มsleepเพื่อเดาเวลา/แก้receiver
- Cleanupระบายholdingrequestโดยไม่ทับexceptionต้นเหตุ; explicitReleaseยังassertactual201เหมือนเดิม
- แยกfixture/DB/Kafka/Informsของแต่ละrecovery test เพื่อไม่ให้งานpendingจากกรณีที่ล้มไปกินfaultหรือปรากฏในcountsของกรณีถัดไป; คงassertionsเพดาน6/ไม่มีattempt7/hash/key/Retry-Afterเดิม และเพิ่มregression20รอบ ทำให้externaltestsรวม9ข้อ (sourceยังอยู่ใน12ไฟล์ที่ยืนยัน)
- หลักฐานรันแยกเดิมใช้ยืนยันพฤติกรรมได้ แต่ผลรอบรวมและregressionใหม่ต้องผ่านอีกครั้งก่อนปิดงาน; ไม่แก้productionretry/transport/worker หรือเพิ่มquotaเพื่อทำให้testsผ่าน
- หลังแก้ synchronization/isolation: `m44-recovery-isolated.trx` ผ่าน4/4 ไม่มีskip ใช้เวลา2นาที20วินาที รวมregression20รอบ, actual429/Retry-After/restart, lostack/replayและ503/ครบ6; เริ่มgateรวมใหม่แบบexternal9ข้อเปิดครบ ผลรวมคาด171 integrationข้อ ยังรอผลสุดท้าย

## ผลปิดงาน 2026-11-18

- `scripts/test-e2e.ps1 -InformsSource <clean checkout at f1eca8d> -NoBuild -Gate` ผ่านexit0: Release build, unit **38/38**, integration **171/171**, EF modelตรงกับmigration; integrationใช้ **17นาที5วินาที** และ scriptตรวจTRXว่าexternal **9/9** Passed ไม่มีskip/abort/filterในgate
- Sourceที่ตรวจคือ Mappingcommit `1b5f2f2` บนฐาน `3fd48f8` รวม12ไฟล์ตามinventory — `feat(mapping): verify Informs delivery and enable capabilities`; commitบนdevelopตามคำสั่งผู้ใช้2026-11-18แล้ว ไม่มีsourceเปลี่ยนเพิ่มจากชุดที่ผ่านgate; ไม่มีproductionmigration/transport/workerหรือInforms/Web/demoเปลี่ยน; แบบ/หลักฐานอยู่ในcommitเอกสารชุดนี้และยังไม่push
- TRXรอบผ่าน: `tests/Mapping.UnitTests/TestResults/m4-4-gate.trx` และ `tests/Mapping.IntegrationTests/TestResults/m4-4-gate.trx`; เก็บรอบfailเดิมแยก `m4-4-gate-before-isolation.trx` และredregression `m44-gate-race-red.trx` เป็นignoredartifacts ไม่มีdebugloggingค้าง
- FullMTLSในgateผ่าน **4นาที17.40วินาที**: Source/Transformed/outputs/Delivered/reservations/dispatched2556, hashesของHTTPทุกใบตรงpersistedpayload, InformsReceived2553/Cancelled3, metadata D-80และsourceRowNoคงเดิม; Delimited/FixedWidthผ่านactualintake/rawSource/summaryด้วย
- Real429/Retry-After/restartผ่าน34.28วินาที; streamingregression20รอบผ่าน20.39วินาที; lostack/replayผ่าน32.19วินาที; proxy503recovery/ครบ6/ไม่มีattempt7ผ่าน35.48วินาที; actual400/409/replaced/lateoldkeyผ่านครบ
- รันแยกแบบไม่ตั้งdependencyenvยืนยันexternal9ข้อเป็นskip9/Passed0/Failed0ใน `m44-offline-9.trx`; ผลนี้ใช้ตรวจopt-inเท่านั้น ไม่รวมเป็นหลักฐานacceptance; roundปิดงานด้านบนตั้งenvครบและไม่มีskip
- Capabilitiesปรับavailable=trueเฉพาะExcel/Delimited/FixedWidth/ApiContractตามผลactualacceptance, destinationsอื่นfalseและworkerdefaultsdisabledเหมือนเดิม; unit/APIregressionผ่านในgate
- Q4ปิดด้านcontractและlocalreceiverตามคำตอบDev1/D-80จากactualmetadata/การรับไฟล์; Q7/companydeploy, Q5/Q9/partnerbusinessfields/fixtures, M1.2/Q8/Web และhealth/probesยังแยกติดตาม; Informsเป้าหมายคือ `f1eca8d` ตามimage/revisionที่บันทึก ไม่อ้างว่าtested `6cb5664` หรือvalidationครบทุกstage
- BuildมีNU1903ของSSH.NET2023.0.0ผ่านTestcontainersเหมือนbaselineเดิม ไม่เปลี่ยนdependencyนอกชุดM4.4
