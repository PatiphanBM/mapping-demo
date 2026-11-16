---
title: แผน implement Mapping API จาก demo สำหรับ phase 1
status: draft
last_updated: 2026-11-16
---

# แผน implement Mapping API จาก demo สำหรับ phase 1

เริ่ม implement ที่ `prjInsuranceValidationMapping` ก่อน แล้วจึงทำหน้า mapping ใน `prjInsuranceValidationWeb` เมื่อได้รับ Web design เอกสารนี้เป็นแผนสำหรับ review และแตกงาน ไม่ใช่หลักฐานว่า feature ถูก implement หรือข้อตกลง kickoff ได้รับอนุมัติแล้ว ติดตามงานและคำถามที่ [plan.md](plan.md)

**คำตอบผู้ใช้ 2026-11-13:** Q1 / kickoff ข้อ 1 เลือก immutable Source → JSON `pa-inform/1` โดยตรง → HTTP Informs; ข้อ 2 เลือกย้าย Transformation Program ทั้งหมด โดยทำให้ MTLS ใช้ได้ก่อนตาม M-03 จึงปรับขอบเขต engine ในตารางด้านล่าง ส่วนคำถามอื่นและ dependency ที่ยังไม่ตกลงยังคงสถานะรอ review

ข้อ 3 ผู้ใช้เลือกเริ่มไฟล์ MTLS **`.xls` ตามตัวอย่างก่อน** (sheet แรก/header 19 columns/cp874); Dev 1 ตรวจใหม่วันที่ 2026-11-16 ว่าไฟล์จริงเป็น **BIFF5/stream `Book`**, CODEPAGE ระบุ 1252 แต่ bytes ไทยเป็น cp874 ตาม [mtls-file.md](mtls-file.md) จึงต้องอ่าน BIFF5 และบังคับ cp874; `.xlsx` ไม่เป็นเงื่อนไขตรวจรับช่วงแรก [fixture สมมติ 2,556 แถว](fixtures/README.md) พร้อมแล้ว

ข้อ 4 ผู้ใช้เลือกเก็บ default ผู้รับประโยชน์ตาม BR-035 ใน **versioned config ของ MTLS ผ่าน Transformation Program** ตาม M-04; เรื่อง field สัดส่วนใน Q5 ยังรอคำตอบ

ข้อ 5 ผู้ใช้เลือกเก็บค่าคงที่ MTLS และกฎ **`P → NEW` / `C → CANCELLED` ใน versioned config ผ่าน `literal` / `case`** ตาม M-05; ค่า transaction type ที่ว่างหรือไม่ใช่ P/C ยังรอคำตอบใน Q5

ข้อ 6.1 ผู้ใช้เลือก **`sourceRowNo` เป็นลำดับแถวข้อมูลที่อ่านได้ เริ่ม 1 ไม่รวม header/แถวว่าง และใช้เลขเดิมเมื่อ retry** ตาม M-06; เก็บ Physical Line Number แยกและคง `nSeq` เป็นค่าดิบใน Source ไม่ใช้จัดลำดับ ส่วนที่เหลือของข้อ 6 และรายละเอียดเวลารับไฟล์ครั้งแรกใน Q4 ยังรอข้อสรุป

ข้อ 6.2 ผู้ใช้เลือก **เริ่ม HTTP concurrency ไป Informs สูงสุด 1 request แล้วปรับผ่าน config ภายหลัง** ตาม M-07; ต้องปิดวิธีคุมเพดานเมื่อมีหลาย pod ใน M1.1/M4.2 ส่วนจำนวน attempts/backoff/timeout ยังรอข้อสรุป

ข้อ 6.3 ผู้ใช้เลือก **delivery รวมสูงสุด 6 attempts รวมครั้งแรก 1 + retry สูงสุด 5** ตาม M-08; restart/redelivery ไม่รีเซ็ตจำนวนครั้ง และครบเพดานแล้วหยุด retry ทั้ง automatic และ Web/manual ตามคำตอบ Q2.6/M-20 ส่วน retry delay/timeout ยังต้องลงรายละเอียด

ข้อ 6.4 ผู้ใช้เลือก **เวลาที่ Mapping รับไฟล์และสร้าง File Job ครั้งแรก แล้วคงเดิมเมื่อ resume/retry** ตาม M-09 โดยชี้แจงใน Q4/M-23 ให้ใช้ `FileJob.CreatedAt` เป็น UTC ไม่เพิ่ม column เวลารับไฟล์แยก ส่วนวิธีส่ง/ใช้เวลานี้ใน Informs ยังรอ Dev 1 เพราะ contract ปัจจุบันไม่มี field รับเวลาและ handler ใช้เวลา HTTP intake ตั้ง `ReceivedAt`

ข้อ 7.1 ผู้ใช้เลือก **ชุดข้อมูล delivery log ตาม M-10**: ชื่อไฟล์/แถว/config version, ยอดทั้งหมด/ส่งสำเร็จ/รอส่ง/ส่งไม่สำเร็จ, สถานะ/HTTP status/error code/path, attempts และเวลาส่งแต่ละครั้ง, `informId`/`replayed`/`replaced`; รายการปกติไม่แสดง raw Source/payload ส่วน detail แบบ read-only ใช้ M-18/M-19 ตาม Q2.4–Q2.5

ข้อ 7.2 ผู้ใช้เลือก **ทีม Mapping / Dev / Ops** เป็นกลุ่มผู้ใช้งาน delivery log ช่วง phase 1 ตาม M-11; ใช้ fixed user ไม่มีสิทธิ์แยกตาม `web.md` ไม่ใช่การเพิ่ม access control หรือ workflow ของ UW ในหน้า Mapping

ข้อ 8 ผู้ใช้เลือก **Config + ผลการส่ง + retry ตามสถานะ** สำหรับหน้า Mapping ช่วงแรกตาม M-12: สร้าง version/validate/preview/activate/disable, ดูไฟล์/แถว/attempts และ retry ก่อนครบเพดานที่อนุญาต; layout/interaction ยังรอ Web design ส่วน Source/Payload detail ใช้ M-18/M-19 แล้ว ครบเพดานไม่เปิด retry ตาม M-20 และ changed-payload reprocess/business resend ปิดช่วงแรกตาม M-22

ข้อ 9.1 ผู้ใช้เลือก **Kafka + transactional outbox สำหรับงานภายใน Mapping** ตาม M-13 และ pattern Informs; Mapping → Informs ยังคง HTTP ตาม D-03 ส่วนการเริ่ม local ใช้ข้อ 9.2 ด้านล่าง และ broker/topics/groups/owner บริษัทใน Q7 ยังรอข้อมูล

ข้อ 9.2 ผู้ใช้เลือก **เริ่ม Kafka local ผ่าน Docker ก่อน** ตาม M-14 เพื่อพัฒนา/ทดสอบระหว่างรอ infrastructure บริษัท; ต้องลงรายละเอียดและยืนยันรายการไฟล์ Compose/config/test ก่อนแก้ การเลือก environment ยังไม่ใช่การ provision หรือหลักฐานว่าทดสอบ Kafka ผ่าน

**Q2.1 ผู้ใช้เลือก physical Source Table แยกตาม config ตาม M-15** แทนข้อเสนอตารางกลาง + JSONB; ทุกตารางอยู่ schema `mapping` และเก็บ Source แบบ immutable ตาม D-07 โดยวิธีสร้างผ่าน Web และ schema evolution ใช้คำตอบ Q2.2–Q2.3 ด้านล่าง

**เหตุผลจากผู้ใช้:** ข้อมูลจากแต่ละเจ้ามีโครงสร้าง field ต่างกัน แต่ละ config จึงมี Source definition/table ของตัวเอง แล้ว Transformation Program แปลงเข้า contract กลาง; 19 columns เป็นข้อกำหนดไฟล์ MTLS ไม่ใช่โครง Source ที่ hardcode ให้ทุก config การออกแบบนี้ไม่เพิ่ม partner อื่นเข้าขอบเขต phase 1 ตาม D-01

**Q2.2 ผู้ใช้เลือกกำหนด fields และสร้าง Source Table ผ่านหน้า Mapping ตาม M-16** จึงเพิ่ม Source catalog/controlled creation ใน M1.5 และ Web scope; กติกาแก้ schema ใช้ M-17 ส่วน allowed names/types, DDL permissions และ definition pin ลงรายละเอียดใน M1.1/M1.2 ก่อนลง source

**Q2.3 ผู้ใช้เลือกเพิ่ม column ในตารางเดิมได้อย่างเดียวตาม M-17** ห้ามลบ/rename/เปลี่ยนชนิด field เดิม; หากต้องเปลี่ยนแบบนั้นให้สร้าง table/definition ใหม่แล้วให้ config version ใหม่อ้าง ส่วนงานเก่ายังใช้ definition/table เดิม การเพิ่ม column ไม่เติมค่าธุรกิจย้อนหลังใน Source เก่า

**Q2.4 ผู้ใช้เลือก Source view รายแถวแบบ read-only ตาม M-18** เปิดจากรายการไฟล์/แถว พร้อม config version/สถานะส่ง ใช้ตรวจค่าต้นทางและยอดแถวที่อ่านพบเทียบกับ Source ที่บันทึก รวมแถวที่ยังไม่ถึง Informs; รายการ log ปกติยังคืน metadata ส่วน Delivery Payload detail ใช้คำตอบ Q2.5 ด้านล่าง

**Q2.5 ผู้ใช้เลือก Delivery Payload inspection แบบ read-only ตาม M-19** ใช้ JSON จาก durable snapshot ที่ Mapping บันทึกไว้จริงเพื่อเทียบ Source รวมกรณี Informs reject; ไม่ compile ด้วย config ใหม่แทน snapshot และหากยังไม่มี payload ให้แสดง availability/diagnostic โดยไม่สร้างทดแทน

**Q2.6 ผู้ใช้เลือกหยุดที่ 6 attempts และไม่เปิด Web retry หลังครบเพดานตาม M-20** API/action guard และ worker ต้องคุมเพดานเดียวกัน ไม่สร้าง attempt 7 ของ delivery เดิม ไม่รีเซ็ตจำนวนครั้ง/ประวัติหรือสร้าง identity ใหม่เพื่อหลบเพดาน

**Q2.7 ผู้ใช้เลือกทำปลายทาง Informs API ก่อนตาม M-21** และเก็บ LegacyTable/JsonFile/DatabaseTable เป็น backlog; การย้าย Transformation Program ทั้งหมดไม่รวม destination runtime เหล่านั้น ส่วน preview/การเปิดดู JSON ตาม M-19 ยังคงอยู่ในขอบเขต

**Q3 ผู้ใช้เลือกยังไม่เปิด changed-payload reprocess/business resend ช่วงแรกตาม M-22** config ใหม่ใช้กับไฟล์ที่รับใหม่ ส่วนงานเดิมคง version/schema/profile/identity ที่ pin ไว้และ retry ใช้ bytes/hash/key เดิมภายใน 6 attempts; ปิด Q3 สำหรับขอบเขตช่วงแรกด้วยการเลื่อน feature ไม่ได้อนุมัติ key/lineage สำหรับส่ง payload ใหม่ในอนาคต

**Q4 ผู้ใช้ชี้แจงให้ใช้ `CreatedAt` ของ File Job ฝั่ง Mapping ตาม M-23** เป็นเวลารับไฟล์ครั้งแรก ไม่เพิ่ม field เวลาแยกในตาราง; วิธีให้ Informs รับ/ใช้เวลานี้ตาม D-23 ยังรอ Dev 1 ไม่ใช่การเปลี่ยนไปใช้เวลาสร้างใบฝั่ง Informs

**Q5 ผู้ใช้ให้พักเรื่อง field/สัดส่วนและจะส่ง fields มาเองตาม M-24** จึงเดินหน้าออกแบบกลไกกลางด้วย Source definition/config/contract schema โดยไม่ถามหรือเดา business fields ทีละรายการต่อ; [ร่างข้อมูลและ runtime](data-runtime-design.md) ลงรายละเอียด M1.1 และ scenarios S01–S36 สำหรับหลายโครงสร้าง ไม่ใช่การปิด Q5/เปลี่ยน schema ตัวจริงหรือผลทดสอบ runtime

**Q6 ผู้ใช้ตกลง policy ตาม M-25:** hash ของ stable snapshot เดิมภายใน config เดิมถือว่าซ้ำข้าม versions/ชื่อไฟล์; bytes เปลี่ยนรับเป็น File Job ใหม่และประมวลผลทุกแถว คนละ config ไม่ชน content claim การกันซ้ำระดับใบยังเป็นหน้าที่ Informs ตาม D-05/D-46

**M1.1 ปิดแบบภายใน Mapping วันที่ 2026-11-16 ตาม M-26:** [data/runtime §8](data-runtime-design.md#8-ข้อกำหนด-technical-สำหรับ-implement-m11) ลง types/keys/constraints/raw storage/version pin/transaction/lease และ [หลักฐาน PostgreSQL local 29 checks](m1-1-validation.md) ครบ; ยังไม่ใช่ EF migration/application tests หรือการปิด M0.2/M1.2/Q4/Q5/Q7/Q8

**M1.2 วันที่ 2026-11-16:** ผู้ใช้ยืนยัน Q8 ให้ใช้ prefix `/api/mapping`; [Mapping API/DTO contract ฉบับร่าง](mapping-api-contract.md) ลง routes, envelope/errors, action guards, list/counts และ preview limits แล้ว ส่วน route/DTO ที่กระทบ Web/contract boundary ยังรอ Dev 1 review ก่อนปิด M1.2/ลง controller

## 1. แหล่งอ้างอิงและสถานะที่ตรวจพบ

ลำดับอ้างอิง: [README](README.md), [rules](rules.md), [glossary](glossary.md), [contract](contract-pa-inform.md), [MTLS](mtls-file.md), [Web](web.md) เป็นหลัก; ใช้ [reverse spec](../../specs/mapping_demo_reverse_spec.md) เพื่อค้น feature และโค้ดตัวอย่าง ไม่ยกพฤติกรรมทั้งหมดของ demo เป็นข้อกำหนดธุรกิจ

| แหล่ง | สิ่งที่ตรวจเมื่อ 2026-11-13 | ผลต่อแผน |
|---|---|---|
| เอกสาร mapping `d9bd8d8` | phase 1 / milestone เป็นร่าง รอ kickoff 11-16 | เสนอแผนภายในวันเดิม; เรื่องที่ยังไม่ตกลงระบุ gate ก่อน implement |
| Mapping `78453b3` | มี 4 layer, DI, `Uuid`, `BaseResponse`, initial migration; ยังไม่มี controller/entity/worker ของ feature และไม่มีไฟล์ test case ในสอง test projects | เริ่มจาก scaffold ที่มี ไม่ถือว่า demo ถูกย้ายมาแล้ว |
| Mapping integration test project | .NET 10, xUnit, FluentAssertions, WebApplicationFactory, Testcontainers PostgreSQL และ `init.sql` | ใช้ stack เดิมเพื่อพิสูจน์ API/DB จริง |
| Informs `6ab23ad` | schema และ `PaInformsController` ใช้ `insureds[]`, `BaseResponse`, key ใน request body | ใช้ schema ตัวจริงและ response จริง; ไม่ใช้ fixture เก่าของ demo |
| Informs implementation | `Infrastructure/Messaging/{OutboxDispatcher,StageConsumer}.cs`, `Infrastructure/Products/ProductsClient.cs`, DI ลง hosted services ใน process API | ทำ worker/outbox/client ตามรูปแบบนี้ตาม D-77 |
| Mapping Compose | มี PostgreSQL 15, migrator และ Mapping; ยังไม่มี Kafka | งานเพิ่ม messaging ต้องมี local infrastructure และขอ topic สำหรับบริษัท |
| Web | มี UW/Config และ API caller ของ Products; ยังไม่มี mapping feature | กำหนดข้อมูล API ให้ตรวจได้ก่อน; รอ design ก่อนออกแบบหน้าจอ |
| fixture | Dev 1 เพิ่ม [fixture สมมติ 2,556 แถว](fixtures/README.md) วันที่ 2026-11-16 พร้อม generator; `docs/MTLSPA0220261009.xls` เป็นข้อมูลจริง | ใช้ fixture สมมติในการทดสอบเต็มไฟล์เท่านั้น; Q7 ยังรอ dependency บริษัท ไม่รอ fixture แล้ว |

ตอนจัดทำร่างแผนยังไม่ได้รัน build/test; ต่อมา **2026-11-13** รัน baseline M0.3 ที่ Mapping `78453b3` ด้วย SDK **10.0.404** แล้ว `scripts/check.ps1` จบ exit 0: build ผ่าน, EF model ตรง migration แต่สอง test projects **ไม่มี test case ให้รัน** และมี `NU1903` ของ `SSH.NET 2023.0.0` (4 warnings) รายละเอียด environment/วิธีเลือก SDK อยู่ใน [plan.md ใต้ M0.3](plan.md#3-งาน) สถานะ test ของ demo ใน reverse spec ยังเป็นหลักฐานของ demo เท่านั้น ไม่ใช่ผลทดสอบ feature ของ Mapping API ใหม่

## 2. ขอบเขตและการเทียบ feature

**เป้าหมาย phase 1 ตาม D-70/D-77/D-78:** ไฟล์ MTLS → Source Table ที่แก้ไม่ได้ → JSON `pa-inform/1` → `POST /api/pa/informs` ทีละใบ → delivery log + retry; พร้อม API สำหรับหน้า mapping และรันบน Kubernetes

ตารางนี้ครอบคลุมกลุ่ม feature และ requirement ID ใน reverse spec คำว่า **เสนอเลื่อน** หมายถึงยังเก็บเป็น backlog และรอทบทวนใน Q2 ไม่ใช่ลบ feature ออกจากแผนทั้งหมด

| Feature / reverse spec | แผนสำหรับ API จริง | ช่วง / เงื่อนไข |
|---|---|---|
| Table Catalog `OBS-TBL-001..004` | ผู้ใช้เลือก physical Source Table แยกตาม config พร้อมกำหนด fields/สร้างตาราง/เพิ่ม columns ผ่านหน้า Mapping ตาม Q2.1–Q2.3/M-15–M-17; immutable Source และเพิ่ม column ได้อย่างเดียว | Implement controlled creation/column addition ใน M1.5; naming/types/permissions/definition pin ปิดใน M1.1/M1.2; ห้ามลบ/rename/เปลี่ยนชนิด field เดิม และ verification/impact admin ทั้งชุดยังไม่ถือว่าอนุมัติแล้ว |
| Config `OBS-CFG-001..003,006` | config identity, version immutable, validate, clone เป็น version ใหม่, active version, compare, ปิดรับไฟล์ใหม่, optimistic concurrency | จำเป็นก่อนเปิด intake; validate dependencies และ folder/extension ownership ตอน activate |
| Preview `OBS-CFG-004..005` | text/source-row preview และ workbook upload แบบจำกัดขนาด; ใช้ compiler/evaluator ชุดเดียวกับ runtime; แสดง nested JSON + diagnostics | ทำก่อน activate; ไม่มี job/outbox/HTTP delivery และไม่ archive |
| Excel `OBS-IN-003..004` | `.xls` BIFF5/stream `Book`, บังคับ cp874 แม้ CODEPAGE 1252, sheet แรก, header 19 columns, invariant numeric/date conversion | Dev 1 แก้ข้อเท็จจริงไฟล์และให้ [fixture สมมติ](fixtures/README.md) แล้ว; `.xlsx` ไม่บังคับในช่วงแรก |
| Delimited/Fixed width `OBS-IN-001..002` | เก็บ layout interface ให้ขยายได้ แต่ยังไม่ต้องเปิดตัวอ่านเหล่านี้ | เสนอเลื่อน Q2; ไม่เปิด capability ที่ runtime ยังไม่รองรับ |
| Transformation `OBS-TRN-001..005` | ย้าย declarative Transformation Program ทั้งหมดตามคำตอบผู้ใช้/M-03; เริ่ม source/literal, null/default, convert แบบเก็บ unparsed, case, nested arrays และ metadata bindings ที่ MTLS ต้องใช้ใน M2.2 | Q1 ข้อ 1–5 มีคำตอบผู้ใช้แล้ว; default ผู้รับประโยชน์/ค่าคงที่/P/C อยู่ใน config ตาม M-04/M-05; ช่องว่าง schema ใน Q5 ยังรอ; MTLS ต้องได้ `default` 1 output ต่อแถว แม้ engine รองรับ filter/0..N branches |
| Expressions/predicates เพิ่มเติม | ย้ายให้ครบใน M2.5: expressions 13 แบบ, predicates 17 แบบ, conversion targets 7 ชนิด, filter/branches, nested output, Named Transform 1 รายการ และ Named Lookup 2 รายการ; pinned evaluation time/timezone/limits | อยู่ในขอบเขตการย้ายทั้งหมด โดยทำ MTLS ก่อน; catalog ระบุพร้อมใช้เฉพาะ capability ที่ implement/ทดสอบและ dependency พร้อมแล้ว; lookup adapters ที่ผูก `mti_original`/Table Catalog เดิมรอ Q2/Q7; ไม่คัด `sqlserver-legacy-v1` มาเป็นกฎกลาง |
| File intake `OBS-FILE-001..006` | scan เป็นหลัก, file stability/snapshot/hash, version pin, Source persistence, row outbox, archive/recovery | ไม่พึ่ง FileSystemWatcher; hash duplicate policy ยังรอ Q6 เพราะ BR-001 เป็นข้อเสนอ |
| Retry/reprocess `OBS-FILE-007..008` | แยก import retry, row retry และ delivery retry; มีประวัติการรัน | retry/recovery ที่คง version/identity เดิมทำช่วงแรก; ผู้ใช้เลือกปิด changed-payload reprocess/business resend ตาม Q3/M-22 และไม่ใช้หลบเพดาน 6 attempts |
| API destination `OBS-DST-API-001..004` | pinned schema + server-owned Informs connection profile, final payload validation, encrypted durable bytes, delivery jobs/attempts, bounded concurrency | HTTP ไป Informs เท่านั้น; ไม่มี endpoint URL/credential รับจาก browser |
| Resend/rollout `OBS-DST-API-005..006` | มี pause intake/pause delivery เพื่อ operation; งานที่ pin แล้วไม่เปลี่ยนปลายทาง | business resend ด้วย key ใหม่, Shadow/Canary/Expanded และ generic profile rollout เสนอเลื่อน Q2/Q3 |
| LegacyTable `OBS-DST-LEG-001..002` | ผู้ใช้เลือกเก็บเป็น backlog ใน Q2.7/M-21; ไม่เขียน Normalized Table เป็นขั้นบังคับก่อน API ตามคำตอบ Q1 ข้อ 1 | ช่วงแรกส่ง Informs API; ห้ามเขียน `non_motor.*` ตาม D-02 |
| JsonFile `OBS-DST-JSON-001..006` | ผู้ใช้เลือกเก็บ encrypted staging, deterministic artifact, atomic publish, recovery/operations เป็น backlog ใน Q2.7/M-21 | ไม่เพิ่ม JSON finalizer ในช่วงแรก; preview/อ่าน Delivery Payload แบบ read-only ตาม M-19 ยังทำได้ |
| DatabaseTable `OBS-DST-DB-001..007` | ผู้ใช้เลือกเก็บ profile/schema drift/insert-only/reconciliation/unknown-outcome เป็น backlog ใน Q2.7/M-21 | ไม่เพิ่ม destination DB writer ในช่วงแรกและไม่ใช้เป็นทางส่งเข้า Informs ตาม D-02 |
| Admin `OBS-ADM-001..005` | read-only contract/profile summary, config audit และ safe delivery metrics สำหรับ MTLS; Source catalog/สร้าง physical table ผ่าน Web ตาม M-16 แยกใน M1.5 | generic profile/contract CRUD/publish/retire และ Table verification/impact admin เพิ่มเติมยังเสนอเลื่อน Q2; ไม่รวมการสร้าง Source Table ที่ผู้ใช้เลือกแล้ว; Informs ยังคงเป็นเจ้าของ contract |
| Platform `OBS-PLT-001..003` | capability endpoint, OpenAPI, `/health/live`, `/health/ready`, environment config, migrations ผ่าน migrator | ไม่ทำ auto-migrate ตอน API start ตาม demo; ใช้ deployment flow ของ repo จริง |
| Web `OBS-WEB-001..004` | Config + ผลการส่ง + retry ตามสถานะตาม M-12, กำหนด fields/สร้าง Source Table ตาม M-16, เพิ่ม column ตาม M-17 และ Source/Delivery Payload detail แบบ read-only ตาม M-18/M-19 | layout/interaction รอ Web design; retry ต้องผ่าน state/โควตาที่ตกลง ส่วน reprocess/generic admin เพิ่มเติม และ dashboard ทุก destination ยังรอ Q2/Q3/Q7 |
| Proxy `OBS-WEB-005` / prototype | ใช้ Angular `core/mapping-api.ts` + `environment.mappingApiUrl` ตาม Web repo | ไม่ย้าย Next.js proxy หรือ `/prototype/file-to-json` เข้า production |

เรื่อง product validation, Client API/MCIS และ UW ไม่อยู่ใน Mapping ตาม D-06 ไม่มีการเรียก Products หรือ Client API จาก Mapping

## 3. สถาปัตยกรรมและลำดับการทำงานที่เสนอ

```mermaid
flowchart LR
  Web[Angular mapping ภายหลัง] --> Api[Mapping.Api :5177]
  Share[Share mount / InputRoot] --> Scan[Periodic scan]
  Scan --> Intake[Pin version + File Job]
  Api --> App[Application CQRS]
  Intake --> DB[(schema mapping)]
  App --> DB
  DB --> Outbox[Outbox dispatcher]
  Outbox --> Kafka[Kafka: mapping jobs]
  Kafka --> Import[Import consumer]
  Import --> Source[Immutable Source + Row Job + outbox]
  Source --> DB
  Kafka --> Transform[Transform consumer]
  Transform --> Intent[Validate + encrypt Delivery Payload]
  Intent --> DB
  Kafka --> Deliver[Delivery consumer]
  Deliver --> Informs[HTTP POST /api/pa/informs]
  Informs --> Own[Product / Client validation / UW]
```

Kafka ในภาพเป็นแนวทางที่ผู้ใช้เลือกแล้วใน Q1 ข้อ 9.1 / M-13 สำหรับงานภายใน Mapping ตามรูปแบบ Informs; เริ่ม Kafka local ผ่าน Docker ตามข้อ 9.2 / M-14 เพื่อเดินงานพัฒนา/ทดสอบก่อน ส่วน topics/groups/owner ของบริษัทใน Q7 ยังรอข้อมูล ไม่อ้างว่า infrastructure บริษัทพร้อมจากผล local การส่ง Mapping → Informs ใช้ HTTP ตาม D-03 เสมอ

| Layer / path ใน repo Mapping | งานที่จะเพิ่ม | โค้ด demo ที่ใช้เป็น reference |
|---|---|---|
| `Mapping.Domain/Entities/<Feature>/`, `Repositories/<Feature>/` | config/version, Source definitions/rows, file/row/delivery jobs, attempts, outbox; entity factory + UUID v7 | job identities, destination/transform value models; ไม่ยก Shared project มาทั้งก้อน |
| `Mapping.Application/<Feature>/{Commands,Queries,DTOs}/` | command+handler ไฟล์เดียว, validator แยก, `BaseResponse`; interfaces สำหรับ input, compiler, contract, delivery, storage/clock | compiler/evaluator/input semantics ที่ไม่ผูก Dapper, file IO, Kafka หรือ ASP.NET |
| `Mapping.Infrastructure/Database/Configurations/<Feature>/`, `Repositories/`, `Migrations/` | EF mappings, indexes, transactions, immutable storage, claim/lease; SQL เฉพาะกรณี lock ที่ EF ไม่ครอบคลุม | transaction boundaries และ unique identities; ไม่รัน SQL migration เดิมของ demo |
| `Mapping.Infrastructure/Messaging/`, `Files/`, `Informs/` | hosted scan/import/transform/delivery/sweep, Kafka/outbox, typed HttpClient, encryption/storage adapters | patterns ของ Informs เป็นหลัก แล้วนำ logic demo มาแยกเข้าที่ |
| `Mapping.Api/Controllers/` | inject `IMediator` เท่านั้น, relative routes, HTTP status และ OpenAPI | request/use case เท่านั้น; ไม่ย้าย service orchestration เข้า controller |
| `tests/Mapping.{UnitTests,IntegrationTests}/` | ทดสอบกฎแปลง, commands/queries, API, PostgreSQL constraints, messaging และ HTTP failure | ย้ายเฉพาะ golden/contract cases ที่ยังตรง requirement; เปลี่ยน assertion/test structure ตาม repo |

ไม่มี project Worker/Shared เพิ่มในร่างนี้ ใช้ hosted services ใน process API ตาม Informs; การแยก deployment ภายหลังต้องมีเหตุผลและตกลงก่อนตาม D-77

## 4. ข้อมูลและขอบเขต transaction

รายละเอียดต่อจากตารางสรุปนี้อยู่ใน [data-runtime-design.md](data-runtime-design.md) ครอบคลุม Module/interface, definition versions/Source identity registry, transaction/lease/global budget/attempt outcome, row/output counts และ scenario coverage; §8 ปิดเป็น baseline M1.1 ตาม M-26 แล้วแต่ยังไม่ใช่ schema ของ application ที่ implement/deploy

ตารางต่อไปนี้เป็น **แบบร่างที่ต้องลงรายละเอียด DDL ก่อน M1.3** ทุกตารางอยู่ schema `mapping`; ชื่อ entity ใช้พหูพจน์, ID ระบบสร้างผ่าน `Uuid.NewV7()`, timestamps เป็น UTC และใช้ `row_version` สำหรับข้อมูลที่แก้ได้

| ตารางที่เสนอ | ข้อมูลหลัก / ข้อบังคับ |
|---|---|
| `source_definitions` | table/field catalog ของ physical Source Table ต่อ config (physical table/columns, ชื่อกลาง/ลำดับ/column ที่มา); สร้าง/เพิ่ม columns ผ่านหน้า Mapping ตาม M-16/M-17; config version/job pin definition ที่อ่านได้ย้อนหลังแม้เพิ่ม column ภายหลัง; naming/types/permissions/nullable/default policy ปิดใน M1.1 |
| `mapping_configs` | ชื่อ, relative `input_path`, enabled, active version, `row_version`; ตรวจ path อยู่ใต้ InputRoot และความเป็นเจ้าของ folder+extension ไม่กำกวม |
| `mapping_config_versions` | version number, immutable input layout/transformation/destination, source definition snapshot, contract schema/hash, profile identity/version, evaluation semantics; unique `(config_id, version_number)` |
| `file_jobs` | config/version, original filename, detected time และ `created_at` ซึ่งใช้เป็นเวลารับ File Job ครั้งแรกตาม M-09/M-23 (UTC; resume/retry ไม่เปลี่ยน; ไม่เพิ่ม field เวลาแยก), snapshot reference/hash, import/checkpoint/archive state; path claim แยกจาก content claim `(config_id,hash)` ข้าม versions ตาม M-25 |
| physical Source Tables แยกตาม config | table name สร้างจาก definition UUID ตามแบบ §8; system Source/file/definition IDs ตรวจด้วย FK ผ่าน registry ซึ่งเก็บ Row Number/Physical Line Number; raw fields เป็น `text NULL` ไม่มี business defaults ตาม M-26; UQ `(file_job_id,row_number)` อยู่ registry; ไม่ update/delete ผ่าน API หรือ reprocess |
| `row_jobs`, `row_errors` | source/version/run identity, status/attempt/lease, error path/code; แยก diagnostic จากค่าธุรกิจแปลงไม่ได้ที่ต้องส่ง null + unparsed |
| `delivery_jobs` | logical identity `(file_job_id, source_row_id, output_key)`, config/contract/profile snapshot, encrypted final bytes + key ID, SHA-256, status/next-at/lease, `inform_id`/`existing_inform_id`; unique logical identity สำหรับการส่งปกติ |
| `delivery_attempts` | delivery ID + attempt number, HTTP status, safe message ID/path, latency, replayed/replaced, timestamps/correlation; ไม่ใส่ raw response หรือ PII ใน list/log |
| `outbox` | topic/key/payload ซึ่งมีเฉพาะ job IDs และชนิดงาน, sent state; ไม่มีข้อมูลลูกค้าใน event |
| `mapping_audits` | config activation/disable, retry/reprocess/pause action, fixed actor, reason, request ID และก่อน/หลังเฉพาะ metadata; ไม่ใช้ actor จาก browser เป็นหลักฐานสิทธิ์ |

ผู้ใช้เลือก physical Source Table แยกตาม config ตาม M-15 จึงแทนข้อเสนอ fixed `source_rows` + JSONB เดิม และเลือกสร้างจาก fields ผ่านหน้า Mapping ตาม M-16; เพิ่ม column ได้อย่างเดียวตาม M-17 ส่วนลบ/rename/เปลี่ยนชนิดใช้ table/definition ใหม่ M1.1/M1.5 ต้องปิด metadata, naming/types/constraints, source identity/การอ้างข้าม job, metadata/DDL consistency, concurrent creation/column addition, audit, permissions และ nullable/default policy ของ column ใหม่ที่ไม่เปลี่ยนหลักฐานเดิม โดย config version/งานเดิมต้องอ่าน definition เดิมได้ ไม่ยก Dapper/SQL migration ของ demo มาทั้งชุด และ agent ไม่รันคำสั่งแก้ DB บริษัทเองตามกฎ workspace

**จุด commit ที่ต้องเป็น atomic:**

1. File Job + version pin + import outbox ใน transaction เดียว; multi-pod claim ต้องถูกบังคับด้วย DB ด้วย
2. Source row + Row Job + transform outbox ใน transaction เดียวต่อแถว; resume หลัง crash ไม่เพิ่มแถวซ้ำ
3. final validated encrypted Delivery Payload + Delivery Job + delivery outbox ใน transaction เดียว; ห้ามส่ง HTTP ก่อน durable intent
4. attempt result + job state/next-at ใน transaction เดียว; HTTP อยู่นอก DB transaction และต้องมี lease กันหลาย consumer ส่งพร้อมกัน

Outbox เป็นการส่งอย่างน้อยหนึ่งครั้ง จึงต้องกันซ้ำที่ handler และ unique constraints; commit Kafka offset หลังผลลัพธ์ถูกบันทึกแล้ว การ crash หลัง HTTP สำเร็จแต่ก่อน commit DB ต้อง retry ด้วย bytes/key เดิมเพื่อรับ replay จาก Informs

## 5. API ที่เสนอก่อนมี Web design

prefix **`/api/mapping`** ผู้ใช้ยืนยันแล้วใน Q8; ทุก route ด้านล่างต่อจาก prefix นี้ ยกเว้น health/OpenAPI รายละเอียดล่าสุดอยู่ใน [ร่าง M1.2](mapping-api-contract.md) ที่รอ Dev 1 review ส่วนที่กระทบ Web/contract boundary ก่อนลง controller ไม่คัด route ของ demo ตรง ๆ

| Route ที่เสนอ | ความสามารถ / เงื่อนไข |
|---|---|
| `GET /capabilities` | supported layouts/operators/limits/destinations; ช่วงแรก destination เป็น Informs API (`ApiContract`) ตาม M-21 แสดงพร้อมใช้เฉพาะที่ทำงานจริง และ reason เมื่อปิด feature ไม่เปิด destination อื่นจากการย้าย engine ทั้งหมด |
| `GET /source-definitions`, `GET /source-definitions/{id}` | field catalog สำหรับ config; ไม่มี arbitrary DDL |
| `POST /source-definitions` | สร้าง Source definition และ physical table จาก fields ที่กำหนดผ่าน Web ตาม M-16/M1.5; validate identifiers/types/system columns, metadata/schema consistency และ concurrent creation; path/DTO ปิดใน M1.2 ไม่มี SQL จาก browser |
| `POST /source-definitions/{id}/columns` | เพิ่ม columns ตาม M-17 พร้อม definition version/audit และ conflict guard; งานเก่ายังอ้าง definition เดิม; ไม่เปิดลบ/rename/เปลี่ยนชนิด field เดิม; path/DTO ปิดใน M1.2 |
| `GET /contracts/pa-inform/1` | schema snapshot/hash + path catalog จาก Informs; มีสถานะ availability; ไม่สร้าง schema อีกฉบับเอง |
| `GET /destination-profiles` | safe logical Informs profile; ไม่มี secret/absolute file root และไม่รับ URL ปลายทางจากผู้ใช้ |
| `GET /mapping-configs`, `GET /mapping-configs/{id}` | list/detail, active version, enabled, version number |
| `POST /mapping-configs` | สร้าง config identity พร้อม input path และ source definition |
| `POST /mapping-configs/{id}/versions` | บันทึกทั้ง config definition เป็น immutable version ใหม่; clone ส่งทั้ง definition ไม่แก้ version เก่า |
| `GET /mapping-configs/{id}/versions`, `GET /mapping-configs/{id}/versions/{versionId}` | version history/detail; compare ใช้สอง immutable definitions โดยยังไม่ต้องมี diff engine แยก |
| `POST /mapping-configs/{id}/validate` | ตรวจ draft/paths/required system bindings/dependencies; ไม่สร้าง runtime job |
| `POST /mapping-configs/{id}/preview`, `POST /mapping-configs/{id}/preview-file` | source/text หรือ multipart workbook + draft; in-memory, sample จำกัด 50 records; text จำกัด 1 MiB, workbook compressed/uncompressed/row limits ต้องระบุใน M1.2 |
| `POST /mapping-configs/{id}/activate`, `POST /mapping-configs/{id}/disable` | ส่ง expected `rowVersion`, target version/reason; activate atomic; disable หยุดรับใหม่ ไม่แก้ version ของงานเดิม |
| `GET /file-jobs`, `GET /file-jobs/{id}` | filters/config/status/date, paged rows และผลรวมที่ไม่ปน import/transform/delivery |
| `GET /file-jobs/{id}/errors`, `GET /file-jobs/{id}/rows` | row diagnostics/history และรายการแถว; default คืน metadata ไม่คืน raw PII; เปิด Source detail แยกตาม M-18 |
| `GET /file-jobs/{fileJobId}/rows/{sourceRowId}` | Source ดิบรายแถวแบบ read-only ตาม M-18 พร้อม config version/Row Number/Physical Line Number/สถานะส่ง; resolve physical table/definition จาก job ที่ pin ไว้และตรวจว่า row อยู่ใน file job นี้ ไม่มี table name/SQL จาก browser; path/DTO ปิดใน M1.2 |
| `POST /file-jobs/{id}/retry`, `POST /row-jobs/{id}/retry` | import/row failure เท่านั้น; state guard, reason/request ID, ไม่สร้าง identity ใหม่ |
| `GET /file-jobs/{id}/delivery-jobs`, `GET /delivery-jobs/{id}/attempts` | delivery log, status, reason/path, hash, IDs, attempts;ไม่คืน payload body โดยปริยาย |
| `GET /delivery-jobs/{id}/payload` | Delivery Payload snapshot แบบ read-only ตาม M-19 พร้อม config/lineage/hash/availability; อ่าน durable bytes ที่บันทึกจริง รวม rejected delivery, ไม่ compile ใหม่/ส่ง HTTP/แก้ state และไม่คืน encryption key; path/DTO/response limits ปิดใน M1.2 |
| `POST /delivery-jobs/{id}/retry` | เฉพาะ transient failure ที่ state อนุญาตและยังไม่ครบ 6 attempts ตาม M-20; ใช้ bytes/key เดิมและนับรวมเพดาน; ครบแล้วปฏิเสธตาม state/limit แม้เรียก API โดยตรง ไม่ retry `400/409` แบบเดิม |
| `GET /delivery-operations`, `POST /delivery-operations/pause`, `POST /delivery-operations/resume` | safe counts, backlog/age, intake/delivery pause แยกกัน; ขอบเขต control รอ kickoff Q1 ข้อ 7–8 |
| `POST /file-jobs/{id}/reprocess` | เก็บเป็น backlog ตาม Q3/M-22 ไม่เปิด runtime action สำหรับส่ง payload ใหม่จากแถวเดิมช่วงแรก; preview ไม่ส่งจริงใช้ route preview เดิม |
| `/health/live`, `/health/ready` | live = process; ready ตรวจ DB; messaging/share dependency และ startup gate ต้องตกลงใน M5.1 |

รูปแบบสำเร็จใช้ `BaseResponse<T>` ของ repo เดิม รายการใช้ `result: { items, totalCount, page, pageSize }`, เรียงแบบคงที่พร้อม ID tie-breaker, default 20/max 100 และรองรับ 10/20/50 ของ Web; ชื่อ query filters/sort allowlist ปิดใน M1.2 วันเวลา API เป็น ISO/UTC, date เป็น `yyyy-MM-dd`; Web แสดงตาม [web.md](web.md)

ชุดข้อมูล delivery log ผู้ใช้เลือกตาม M-10 สำหรับ file/row/delivery views และ attempt history โดยกลุ่มผู้ใช้งานคือทีม Mapping / Dev / Ops ตาม M-11; counts นับแถว/unique output แยกจาก attempts พร้อมแยก import/transform/delivery และรวมแถวที่สร้าง Delivery Job ไม่ได้ในยอดยังไม่ส่ง รายการปกติไม่คืน raw Source/payload หรือ raw error response; Source inspection และ Delivery Payload inspection แยกเป็น read-only detail ตาม M-18/M-19 รูปแบบ DTO/นิยาม counts ปิดใน M1.2

การตรวจว่าข้อมูลเข้าครบตาม M-18 ต้องแสดง import state และแยกแถวข้อมูลที่ reader อ่านพบกับ Source ที่บันทึก; ขณะ import ยังไม่จบห้ามใช้ยอดที่อ่านพบปัจจุบันอ้างว่าเป็นยอดทั้งไฟล์ ส่วนยอดส่งแยกสำเร็จ/รอ/ไม่สำเร็จ รวมกรณีสร้าง payload ไม่ได้ การ retry ไม่เพิ่มจำนวนแถว/unique output และ Source detail ไม่มีการแก้ Source/สร้าง job/ส่ง HTTP

ข้อผิดพลาดที่คาดได้: `400` มี `messages[].messageID` เป็น field path; `404` = `NotFound`; `409` = version/state/ownership conflict; `202` = รับ action asynchronous พร้อม job/action ID; global unexpected error ใช้ exception handler เดิม ต้องทดสอบ OpenAPI และ envelope รวมถึง validation จาก `[ApiController]` ไม่สมมติว่าจะได้ envelope เองทุกกรณี

ตัวอย่างบันทึก version และ activate (ข้อมูล config สมมติ ไม่ใช่ DTO ฉบับปิด):

```json
{
  "expectedRowVersion": 3,
  "inputLayout": { "kind": "Excel", "acceptedExtensions": [".xls"], "sheetIndex": 0, "headerRow": 1, "fallbackEncoding": "cp874" },
  "transformationProgram": { "outputs": [{ "outputKey": "default", "assignments": [{ "path": "inform.productCode", "value": { "literal": "MTLSPA" } }] }] },
  "destination": { "kind": "ApiContract", "contractVersion": "pa-inform/1", "profileId": "informs-pa" }
}
```

ตัวอย่างนี้ตัด field assignments อื่นออกเพื่อแสดงโครงสร้าง จึง **ยังไม่ใช่ config ที่ activate ได้**; request activate แยก `{ "versionId": "<uuid-v7>", "expectedRowVersion": 4, "reason": "เปิด config MTLS ที่ผ่านการทดสอบ" }` เมื่อมีคน activate/บันทึกก่อน ให้ตอบ `409` และไม่เขียนทับ version/ค่าที่อีกคนส่ง

## 6. MTLS mapping และ contract ที่ต้องพิสูจน์

ใช้ตาราง column → path ใน [contract §3.1](contract-pa-inform.md#31-mtls-column-ในไฟล์--path) เป็นหลัก; default ผู้รับประโยชน์ ค่าคงที่ และกฎ P/C อยู่ใน versioned config ของ MTLS ผ่าน Transformation Program ตามคำตอบ Q1 ข้อ 4–5 / M-04/M-05 ไม่แยกชื่อ/ที่อยู่ ไม่หา master และไม่เติมเบี้ย/ทุนจาก package (D-06); Q5 เรื่อง field สัดส่วนและค่าที่สร้าง payload ผ่าน schema ไม่ได้ยังรอคำตอบ

ตัวอย่างต่อไปนี้เป็น **ข้อมูลสมมติที่สร้างเพื่ออธิบายกรณีขอบ**; การตรวจเต็มไฟล์ใช้เฉพาะ [fixture สมมติที่ Dev 1 ให้](fixtures/README.md) ไม่ใช้ไฟล์จริง

| ส่วนของแถวต้นทาง | ผลที่คาดหมาย |
|---|---|
| Transaction Type=`C`, หมายเลข PA=`PA-DEMO-C-001`, ชื่อ=`ยกเลิก`, เริ่ม/สิ้นสุด=`-293`, เงิน=`0` | คง Source ดิบ; output `CANCELLED`, date null + raw value ใน `extras.unparsed`; ไม่ทิ้งแถว |
| เลขบัตร Excel เป็น number ที่แสดง 11 หลัก | ส่ง string 11 หลัก ไม่มี `.0`, ไม่เติมเลขนำหน้า/ไม่ตัดแถวด้วย checksum |
| ผู้รับผล1/2 ว่าง | default ตาม BR-035; ประเด็น 100% ที่ไม่มี field ใน schema รอ Q5 ห้ามเพิ่ม field เอง |
| คนเดิมสองแถว แต่ `sourceReference` ต่างกัน | ได้สอง Delivery Jobs; ไม่ deduplicate ด้วยเลขบัตร (BR-003) |

ส่วนของ Delivery Payload จากแถว `C` (ละ field ที่ไม่จำเป็นต่อการอธิบาย):

```json
{
  "contractVersion": "pa-inform/1",
  "fileName": "MTLS-fixture.xls",
  "fileJobId": "<file-job-uuid-v7>",
  "sourceRowId": "<source-row-uuid-v7>",
  "outputKey": "default",
  "sourceRowNo": 2,
  "mappingConfigVersionId": "<config-version-uuid-v7>",
  "inform": {
    "partnerCode": "MTLS", "saleChannel": "FILE",
    "sourceReference": "PA-DEMO-C-001",
    "transactionType": "CANCELLED", "sourceTransactionType": "C",
    "productCode": "MTLSPA", "agentCode": "74000490",
    "effectiveDate": null, "expiryDate": null, "totalPremium": 0,
    "insureds": [{ "fullName": "ยกเลิก", "isPolicyholder": true, "relation": "PRIMARY", "packageCode": "MTL2", "premium": null }],
    "extras": { "unparsed": { "effectiveDate": "-293", "expiryDate": "-293" } }
  }
}
```

system-bound fields ได้แก่ file/source/config IDs, filename, row number และ output key เติมจาก job context ห้าม config เขียนทับ; schema มาจาก Informs ผ่าน endpoint แล้ว pin schema/hash ตอนสร้าง version ไม่ fetch schema ล่าสุดแทนใน retry ไม่มี build/runtime path อ้าง repo เอกสารหรือ project reference ข้าม repo

`sourceRowNo` ใช้ Row Number ที่ persist กับ Source ตาม M-06 ไม่ใช้ `nSeq` หรือ Physical Line Number และไม่เปลี่ยนเมื่อ resume/retry; แถวข้อมูลที่แปลงค่าธุรกิจไม่ได้ยังได้ลำดับและไม่ถูกทิ้งตาม D-04

**ช่องว่าง D-23 ที่ยังรอ Q4:** เวลาอ้างอิงใช้ `FileJob.CreatedAt` เดิมตาม M-09/M-23 โดยไม่เพิ่ม column เวลาใน Mapping แต่ยังไม่มี field รับค่านี้ใน schema/DTO ของ Informs `6ab23ad`; `ReceivePaInformCommandHandler` ใช้เวลาปัจจุบันของ Informs ตั้ง `ReceivedAt` การใช้ CreatedAt ฝั่ง Mapping หรือเริ่ม concurrency 1 จึงยังไม่พิสูจน์การเรียงข้ามไฟล์เมื่อ backlog/retry ต้องให้ Dev 1 ตกลงวิธีรับ/ใช้เวลาก่อนเพิ่ม contract field หรือปิด acceptance ส่วนนี้

**ข้อจำกัดของ D-04:** nullable business field ที่แปลงไม่ได้ใช้ null + unparsed; แต่ schema ยังบังคับ `sourceReference` เป็น nonempty และ `transactionType` เป็น enum หากข้อมูลต้นทางทำให้สร้าง envelope ที่ผ่าน schema ไม่ได้ ต้องเก็บ Source + แสดง row error และรวมในยอดไม่ถูกส่ง ห้ามสร้างค่าอ้างอิง/transaction type ขึ้นเอง ประเด็นการให้ UW เห็นกรณีนี้เสนอ Q5

## 7. Delivery, retry และ reprocess

| ผล Informs / transport | สถานะ Mapping ที่เสนอ | การทำงาน |
|---|---|---|
| `201`, `200 replayed`, `200 replaced` พร้อม response ที่ถูกต้อง | `Delivered` | เก็บ `informId`, flags และ attempt; หมายถึงรับบันทึก ไม่ใช่ผ่าน validation |
| `400` | `Rejected` | เก็บ safe field paths; ไม่ auto retry; แก้ config แล้วทำตามนโยบาย reprocess ที่ตกลง |
| `409` | `Rejected` | เก็บ `DUPLICATE_SOURCE_REFERENCE`/`existingInformId`; ไม่เปลี่ยน key เพื่อหลบ duplicate |
| `408/429`, `5xx`, timeout/connection loss | `RetryableFailed` | next-at + exponential backoff/jitter และเคารพ `Retry-After` ทั้งวินาที/HTTP date; bytes/key เดิม |
| timeout หลัง server อาจ commit แล้ว | `RetryableFailed` | key เดิมเพื่อให้ได้ replay; ไม่สร้าง Delivery Job ใหม่ |
| authentication/endpoint error เช่น `401/403/404` | `Blocked` | ตรวจ server configuration;ไม่วน retry ต่อเนื่องและไม่แก้ URL จากหน้าเว็บ |
| `2xx` แต่ body ผิด/ไม่มี result หรือ inform ID | `Blocked` พร้อม diagnostic | ไม่ถือว่าส่งสำเร็จโดยไม่มีหลักฐาน; recovery ด้วย key เดิมหลังแก้ protocol/config |
| transient ถึงเพดาน 6 attempts รวมครั้งแรกตาม M-08/M-20 | `Failed` | เก็บเหตุสุดท้าย/ประวัติ; หยุด automatic retry และไม่เปิด Web/manual retry หลังครบเพดาน; API/worker ไม่สร้าง attempt 7 ส่วน Source/Payload/history ยังดูได้ |

ค่าเริ่ม HTTP concurrency ที่ **ผู้ใช้เลือกแล้ว**: สูงสุด 1 request ไป Informs ปรับผ่าน config ภายหลังตาม M-07 และต้องปิดวิธีคุมเพดานหลาย pod ใน M1.1/M4.2 โดยไม่เพิ่มเพดานรวมตามจำนวน pod โดยอัตโนมัติ

เพดาน retry ที่ **ผู้ใช้เลือกแล้ว**: delivery รวมสูงสุด 6 attempts (ส่งครั้งแรก 1 + retry สูงสุด 5) ตาม M-08/M-20; persist จำนวนครั้งข้าม restart/redelivery และครบเพดานแล้วหยุดทั้ง automatic และ Web/manual retry; manual retry ก่อนครบเพดานนับรวมใน 6 และต้องผ่าน state guard ไม่รีเซ็ตประวัติหรือสร้าง identity ใหม่เพื่อหลบเพดาน

รายละเอียด retry ที่ **ยังเสนอ**: retry delay/timeout อ่าน environment ได้ ใช้ exponential backoff/jitter และเคารพ `Retry-After`; ไม่ใช้ทั้ง HttpClient retry และ job retry ซ้อนกันโดยไม่รวม attempt count ค่าเหล่านี้เป็น internal configuration ไม่ใช่ SLA ของบริษัท

Row transform สำเร็จหมายถึงมี Delivery Job durable ไม่เท่ากับ `Delivered`; file import เสร็จและ archive แล้วอาจยังมีแถวรอส่ง การ scan/lease recovery ต้องทำงานเมื่อ pod restart หรือ Kafka redeliver

**Retry** ใช้ job identity/config/schema/profile/payload เดิม ส่วน **Reprocess** ที่เลือก version ใหม่และส่ง payload ใหม่จาก Source เดิมเป็น backlog ตามคำตอบ Q3/M-22; D-44 มี key `(fileJobId, sourceRowId, outputKey)` ซึ่งไม่รวม config version การใช้ key เดิมของ output ที่เคย Delivered จะได้ replay ผลเดิม และการสร้าง key ใหม่อาจทำให้ข้อมูลเก่าทับข้อมูลแก้ไขภายหลัง ช่วงแรกจึงไม่เปิด changed-payload reprocess/business resend สำหรับ never-delivered/rejected/Delivered; config ใหม่ใช้กับไฟล์ที่รับใหม่ตาม version pin และ preview แบบไม่ส่งจริงยังทำได้

## 8. ลำดับ implement และหลักฐานปิดงาน

ID ในตารางตรงกับ [plan.md §3](plan.md#3-งาน) เป้าทั้งหมดเป็นร่างเพื่อ review ใน kickoff ไม่เลื่อน milestone ของ Dev 1 โดยปริยาย

| ชุดงาน / เป้า | Dependency / ผลส่งมอบ | หลักฐานก่อนขยับขั้น |
|---|---|---|
| **M0 — วางแผน 11-13 → kickoff 11-16** | M0.1 แผนนี้; M0.2 ตกลง Q1–Q8/fixture/scope; M0.3 baseline build/test | แผน review, คำตอบ Dev 1, baseline ผลจริงของ repo ใหม่ |
| **M1 — foundation / control plane 11-16 → 11-18** | M1.1 domain/state/schema; M1.2 API/DTO/error contract; M1.3 EF migration/config versions/Source storage; M1.4 contract/profile snapshots; M1.5 Source catalog/controlled physical table creation ผ่าน Web ตาม M-16 (เป้า 11-18 เป็นร่างสำหรับ review) | model/DDL + ตัวอย่าง, API contract, DB constraints/concurrency tests รวม metadata/schema consistency และ concurrent table creation; health/readiness เริ่มได้ตั้งแต่ชุดนี้ |
| **M2 — อ่านและแปลง 11-17 → 11-23 สำหรับ MTLS** | M2.1 Excel reader; M2.2 compiler/MTLS config; M2.3 validate/preview; M2.4 read+schema acceptance; M2.5 ย้าย engine ส่วนที่เหลือให้ครบตาม M-03 โดยเป้ารอ review M0.2 | 11-18 อ่าน 2,556 แถว; 11-23 ได้ 2,556 payloads ผ่าน schema ตาม fixture ที่ยืนยันและการตัดสินใจ kickoff; M2.5 ต้องมี traceability/ผลทดสอบครบก่อนปิดงาน engine ทั้งหมด ไม่ถือว่าปิดด้วย MTLS subset หรือเปลี่ยน milestone เอง |
| **M3 — durable pipeline 11-19 → 11-24** | M3.1 outbox/Kafka/local Compose; M3.2 scan/snapshot/import/resume/archive; M3.3 transform claim/lease/errors | crash tests, repeated message, two consumers และ version pinning ไม่ทำข้อมูลซ้ำ; ไม่มี cross-schema writes |
| **M4 — delivery / operations 11-23 → 11-26** | M4.1 HTTP durable delivery; M4.2 retry/recovery; M4.3 logs/counts/actions; M4.4 full local flow | fixture เข้า Informs จริงบน local, source/payload/delivery counts ตรง, `400/409/429/5xx` และ replay/replaced ผ่าน; check.ps1 ผ่าน |
| **M5 — Web handoff / Kubernetes 11-27 → 12-04** | M5.1 Linux paths/env/probes/shutdown; M5.2 API examples/OpenAPI/hand-off; M5.3 หน้า mapping หลังได้ design | API contract ใช้งานจาก Angular ได้; Web ใช้ได้ 12-03; K8s config/secret/mount พร้อม 12-04 |
| **M6 — integration/UAT 12-08 → 12-14** | M6.1 cluster acceptance 12-09; M6.2 regression/UAT evidence | end-to-end บน cluster, recovery/operations runbook, UAT checklist; เริ่ม UAT 12-15 ตามแผนเดิม |

ลำดับ dependency: M0 → M1 → M2; เริ่ม M3 ได้เมื่อ transaction/state/Source model ปิดแล้ว; M4 ใช้ผล M2+M3; M5.2 ทำได้ก่อนมี Web design แต่ M5.3 รอ design และ scope ที่ตกลง หากคำตอบ/fixture/design ไม่ทัน ให้ลงความเสี่ยงใน plan ไม่เปลี่ยนวันเอง

## 9. เกณฑ์ตรวจรับและการทดสอบ

| ID | สถานการณ์และผลที่ต้องตรวจ | ชั้นทดสอบ / งาน |
|---|---|---|
| AC-01 | fixture MTLS ที่รับรองแล้วอ่านครบ 2,556 records, P=2,553/C=3, ไทยไม่เพี้ยน, raw numbers ไม่มี `.0` ที่เกิดจาก formatter | Excel integration/golden, M2.1/M2.4; ยืนยันสถิติ fixture ก่อนยึดเป็น assertion |
| AC-02 | ทุกแถวได้ 1 output `default`, `insureds[0]`, schema ผ่าน; C เก็บ `-293` ใน unparsed ไม่หาย; idNo 11 หลักไม่ถูกแก้ | unit + schema/golden, M2.2/M2.4 |
| AC-03 | beneficiary slots ถูกข้าม/สร้าง default ตามคำตอบ Q5, ไม่มี field ที่ schema ไม่รู้จัก; ไม่มีการแยกชื่อ/ที่อยู่หรือ lookup master | unit/contract, M2.2 |
| AC-04 | validate/preview ทำซ้ำแล้วจำนวน job/source/outbox/attempt ไม่เพิ่ม; ไม่มี outbound delivery หรือ archive | WebApplicationFactory + PostgreSQL + HTTP spy, M2.3 |
| AC-05 | activate N+1 ระหว่าง import แล้วงานเดิมยังใช้ N/schema/hash เดิม; config ที่มี conflict ตอบ 409 ไม่เขียนทับ | DB/API integration, M1.3/M3.2 |
| AC-06 | scan สอง instance และ duplicate Kafka message ไม่เพิ่ม File/Source/Delivery logical identity ซ้ำ; duplicate content เป็นไปตาม Q6 | PostgreSQL/Kafka integration, M3.1–M3.3 |
| AC-07 | crash หลัง insert Source/ก่อน Kafka offset หรือ publish แล้วก่อน mark outbox sent สามารถ resume และนับแถวได้ถูกต้อง | failure injection + restart, M3 |
| AC-08 | `201/200 replayed/200 replaced` เก็บ inform ID/flags ถูก; `400/409` ไม่ auto retry และแถวอื่นยังดำเนินต่อ | fake HTTP + local Informs integration, M4.1/M4.4 |
| AC-09 | `429` เคารพ Retry-After, 5xx/timeout retry ตามเพดาน, restart ไม่สูญ next-at, shutdown ยกเลิกโดยไม่บันทึก Delivered เท็จ | TimeProvider/fake HTTP + PostgreSQL, M4.2 |
| AC-10 | Informs commit แล้ว Mapping crash ก่อนบันทึกผล: retry bytes/hash/key เดิม ได้ replay และไม่สร้างใบเพิ่ม | local integration/failure injection, M4.4 |
| AC-11 | file summary แยก imported/pending/transform blocked/Delivered/Rejected/Failed; ผลรวมไม่ตกหล่นและ totals นับ unique logical outputs ไม่ใช่จำนวน attempts | query/API integration, M4.3 |
| AC-12 | lease หมดหรือหลาย consumer ทำงานพร้อมกันไม่ให้ stale worker เขียนทับผลใหม่; original เปลี่ยนหลัง snapshot ไม่ถูก archive ทับหลักฐาน | PostgreSQL + temporary filesystem, M3/M4.2 |
| AC-13 | Source ใน physical table ต่อ config ตาม M-15 ถูกคงไว้เมื่อ retry/disable/inspection; อ่าน Source detail ของ file job อื่นไม่ได้ผ่านการสลับ row ID; เพิ่ม column ตาม M-17 แล้วงานเก่าอ่าน definition ที่ pin ไว้ได้และไม่เปลี่ยนค่าดิบเก่า; ลบ/rename/เปลี่ยนชนิด field เดิมถูกปฏิเสธ; changed-payload reprocess/business resend ไม่เปิดช่วงแรกตาม M-22 | API/DB integration, M1.1/M1.3/M1.5/M4.3 |
| AC-14 | `..`, absolute path, symlink/real path ที่หลุด root, shared-folder ambiguity และ unsupported workbook ถูกปฏิเสธอย่างมี diagnostic; URL/secret/payload ไม่หลุด list/log | unit + Linux container/API integration, M2/M3/M5.1 |
| AC-15 | migrator/check.ps1 ผ่าน, DB ownership จำกัด schema mapping, `/health/ready` ไม่พร้อมเมื่อ DB ใช้ไม่ได้; config จาก environment และ pod restart ใช้งานได้ | Testcontainers PostgreSQL 15 + deployment smoke, M5/M6 |

ใช้ xUnit `[Fact]`, FluentAssertions, NSubstitute ตาม repo; unit test อยู่ feature เดียวกับ command/entity, integration ใช้ PostgreSQL จริงและ `docker/postgres/init.sql` ไม่ใช้ in-memory provider แทน constraint/transaction tests Messaging ใช้รูปแบบ `tests/Informs.IntegrationTests/KafkaFixture.cs` เป็น reference

ก่อน commit โค้ด Mapping ต้องผ่าน `./scripts/check.ps1` (build + test + model/migration consistency); การทดสอบ end-to-end เพิ่มสำหรับ pipeline/HTTP/restart ไม่ถือว่า script เดิมครอบคลุมเอง ผล full fixture ต้องบันทึก elapsed time, throughput, concurrency, counts และ environment โดยไม่ตั้ง SLA ที่ยังไม่ตกลง

## 10. สิ่งส่งต่อให้ Web และการนำขึ้นระบบ

- ส่ง OpenAPI จาก API ที่ทำงานจริง, ตัวอย่าง BaseResponse/field diagnostics/version conflict, capability catalog, pagination/filter/sort, enum/state/action availability และความหมายของทุก count; เก็บคำอธิบายและตัวอย่างใน repo เอกสารนี้ ไม่สร้าง docs ใน repo โค้ด
- Web ใช้ `core/mapping-api.ts`, `environment.mappingApiUrl` (:5177), `features/mapping/`; route/menu/shared files แยก change ขนาดเล็กตามกฎ Web ไม่ผูก API กับ layout ของ demo Next.js และไม่ลิงก์ข้ามเมนู UW โดยยังไม่ตกลง
- config ผ่าน environment: Mapping DB, Kafka/topics/groups, Informs base URL/timeout, InputRoot/archive/snapshot roots, encryption key references, retry/concurrency, scan/stability interval และ CORS; secret อยู่ Secret ไม่อยู่ image/appsettings ที่ไม่ใช่ Local
- ใช้ internal Informs service ที่ server กำหนด; ไม่คัด network policy ที่บังคับ public HTTPS ของ generic demo มา block internal Kubernetes service URL; protocol/auth/TLS และ network policy ของบริษัทต้องยืนยันใน Q7
- mount share ครั้งเดียวและเก็บ input path เป็น relative path, ตรวจ containment ตอนใช้จริง; archive/snapshot ต้องมี durable storage/access mode ที่ทีม infrastructure ยืนยัน; shutdown หยุดรับงานและคืน lease โดยไม่สูญ durable intent
- DB migration บนบริษัทให้คน/DBA รันเอง; Mapping ไม่แก้ Informs repo/Compose ให้เอง งานรวม compose/deploy ของ Dev 1 ส่งข้อกำหนดผ่าน plan แล้วให้เจ้าของ repo ดำเนินการ

## 11. ประเด็นที่รอตกลงและจุดเริ่ม implement

| Gate | คำถามใน plan | กระทบงาน |
|---|---|---|
| kickoff 9 ข้อ | แนวทางหลัก Q1 มีคำตอบผู้ใช้แล้ว: direct JSON, engine ทั้งหมดโดย MTLS ก่อน, `.xls`, config defaults/constants, row order/time, delivery log, Web scope และ Kafka local ตาม M-03–M-14; ประเด็นที่เป็น D-xx/contract และ dependency บริษัทไม่ถือว่าปิด | M0.2 ยังรอคำตอบที่เหลือใน Q2–Q8 และผลจาก Dev 1 ก่อนปิดงานที่เกี่ยว |
| feature subset และ Source model | Q2.1–Q2.7 เลือก physical Source Table ต่อ config พร้อมสร้าง/เพิ่ม columns ผ่าน Web แบบเพิ่มได้อย่างเดียว, Source/Delivery Payload detail แบบ read-only, ไม่เปิด retry หลังครบเพดาน และ Informs API เป็น destination ช่วงแรกตาม M-15–M-21 แล้ว; รายละเอียด M1.1/M1.5 และ feature เสริมอื่นที่ยังเสนอเลื่อนรอ review ต่อ | M1.1/M1.2/M1.5/M4.3, API/DB และ Web scope; ไม่ใช้ข้อเสนอตารางกลาง + JSONB เดิม |
| changed-payload reprocess/resend | Q3 ปิดสำหรับขอบเขตช่วงแรกตาม M-22: ผู้ใช้เลือกยังไม่เปิด; key/lineage ในอนาคตเก็บเป็น backlog ให้ตกลง Dev 1 ก่อนเปิด | retry/recovery เดิมใช้ bytes/key/version เดิมภายในเพดาน 6 attempts; ไม่ขวาง normal import/delivery ของไฟล์ที่รับใหม่ |
| row ordering | Q4 ฝั่ง Mapping เลือก import order ตาม M-06 และใช้ `FileJob.CreatedAt` ตาม M-23 แล้ว; วิธีให้ Informs รับ/ใช้เวลานี้ยังรอ Dev 1 | M2/M3 และ acceptance ข้าม service ตาม D-23; ไม่เพิ่ม column เวลาแยกใน Mapping |
| business/contract gaps | Q5 พักเรื่อง fields/สัดส่วนตามคำสั่งผู้ใช้/M-24 รอผู้ใช้ส่งรายละเอียด; เดินหน้ากลไก Source/config/compiler/schema validation/diagnostics แบบกลางได้ | actual bindings/defaults/field acceptance ยังรอ input; ไม่เพิ่ม field/เดาค่าเองหรืออ้างว่าทดสอบ payload จริงผ่านแล้ว |
| duplicate file | Q6 ผู้ใช้เลือกแล้วตาม M-25: content hash ต่อ config ข้าม versions/ชื่อไฟล์; bytes เปลี่ยนรับงานใหม่และอ่านทุกแถว; Dev 1 อัปเดต BR-001 ใน rules ตามเจ้าของเอกสาร | M3.2; path claim กันหลาย pod แยกจาก content claim ตามแบบที่ปิดใน M1.1 |
| fixture/infrastructure/security | Q7: certified fixture, topics, DB, share, encryption keys, auth/TLS, fixed actor/log access | full-fixture tests และ deployment readiness |
| Mapping API routes | Q8: prefix/response/concurrency/pagination และขอบเขต controls | ปิด M1.2 ก่อนเริ่ม controller |

**ขั้นถัดไป:** M1.1 ปิดแบบพร้อมหลักฐานแล้ว → ลง M1.2 เป็น generic DTO/state/action contract พร้อมตัวอย่างและตกลง Q8 โดยพัก business fields ใน Q5 ตาม M-24 → ปิด dependency/ข้อตกลงที่เกี่ยวกับ source ชุดที่จะเริ่ม → ระบุรายการไฟล์ source/test/script ให้ผู้ใช้ยืนยันตาม AGENTS.md ก่อนเขียนแต่ละชุด ยังไม่แก้ source ของ Mapping/Web/Informs และยังไม่ commit/push
