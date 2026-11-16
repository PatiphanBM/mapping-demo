---
title: mapping phase 1 — แผนและความคืบหน้า (ไฟล์กลาง Dev 1 + Dev 2)
status: draft
last_updated: 2026-11-16
---

# mapping phase 1 — แผนและความคืบหน้า

> **ไฟล์กลาง: Dev 1 และ Dev 2 เขียนได้ทั้งคู่** — Dev 2 วางแผนงาน mapping เองแล้วเขียนเป็น bullet ในไฟล์นี้ และอัปเดตว่าทำอะไรไปแล้ว · Dev 1 ใช้ไฟล์นี้ตามว่าส่วนไหนเสร็จแล้ว
>
> | ส่วน | ใครเขียน |
> |---|---|
> | §1 อัปเดตล่าสุด | **Dev 2** (เพิ่มรายการใหม่ไว้บนสุด) |
> | §2 จุดสังเกต | วันที่ = Dev 1 (ตามแผนรวม) · สถานะ = Dev 2 |
> | §3 งาน | **Dev 2** วางแผน / เพิ่ม bullet / ติ๊กเมื่อเสร็จ · Dev 1 ใส่หมายเหตุใต้ bullet ได้ (ขึ้นต้น `Dev 1:`) |
> | §4 การตัดสินใจของ mapping (`M-xx`) | **Dev 2** |
> | §5 คำถาม / ข้อเสนอ | **ทั้งคู่** — ตอบใต้ข้อนั้น |
>
> **กันชนกัน:** pull ก่อนแก้ทุกครั้ง · แก้เสร็จ commit + push ทันที (`docs(plan): ...`) · ห้ามลบหรือเขียนทับข้อความของอีกคน — ถ้าไม่เห็นด้วยให้ตอบใต้ข้อนั้น

## 1. อัปเดตล่าสุด

> Dev 2 เพิ่มรายการใหม่**ไว้บนสุด** ทุกวันที่ทำงาน หรือทุกครั้งที่ปิดงาน · รูปแบบ:
>
> ```markdown
> ### YYYY-MM-DD
> - เสร็จ: M1.1, M1.2 (สั้น ๆ ว่าได้อะไร)
> - กำลังทำ: M1.3
> - ติด / รอ: — (หรือ ติดอะไร รอใคร)
> - ต่อไป: M2.1
> ```

### 2026-11-16 — รวม fixture สมมติและแก้ข้อเท็จจริงไฟล์ MTLS
- เสร็จ: ดึง commit ของ Dev 1 ที่เพิ่ม [fixture MTLS สมมติ 2,556 แถว](fixtures/README.md) และแก้ [mtls-file.md](mtls-file.md) ว่าไฟล์จริงเป็น BIFF5/stream `Book`, CODEPAGE 1252 แต่ข้อความไทยเป็น cp874; รวมข้อความ Q7 ของ Dev 1 กับคำตอบ Dev 2 โดยไม่ทับกัน; ปรับร่าง API/แผน M2 ให้ใช้ BIFF5
- กำลังทำ: M1.2 review ยังเลื่อนตามคำขอผู้ใช้; M2.1 ใช้ fixture สมมติได้เมื่อเริ่ม implement; กำลัง sync commit/push งานเอกสาร
- ติด / รอ: Q7 เฉพาะ dependency บริษัท/keys/share ที่เหลือ, Q4/Q5 และ review route/DTO; ไม่ใช้ไฟล์จริง `docs/MTLSPA0220261009.xls` ทดสอบ
- ต่อไป: ตรวจ diff/links แล้ว push `develop`; M1.3 source ต้องแจ้งรายการไฟล์และรอยืนยันก่อนแก้

### 2026-11-16 — เลื่อน review M1.2 ตามผู้ใช้
- เสร็จ: ผู้ใช้ขอ review [ร่าง Mapping API/DTO contract](mapping-api-contract.md) ภายหลัง; คง prefix `/api/mapping` ที่ยืนยันแล้ว และคง M1.2 เป็นงานค้าง ไม่ตีความว่าร่าง route/DTO ได้รับอนุมัติ
- กำลังทำ: เตรียม M1.3 เฉพาะ entity/EF persistence และ immutable Source storage ภายใน Mapping ตาม M1.1; ยังไม่แก้ source code หรือ API route ที่รอ review
- ติด / รอ: M1.2/Q8 ส่วน route/DTO ที่กระทบ Web/contract boundary รอ Dev 1 review ภายหลัง; Q4/Q5/Q7 คงเดิม
- ต่อไป: ระบุรายการไฟล์ source ของ M1.3 และรอยืนยันตาม AGENTS.md ก่อนแก้; เอกสารยังเป็น working tree ไม่ commit/push

### 2026-11-16 — เริ่ม M1.2 และยืนยัน API prefix
- เสร็จ: ผู้ใช้ยืนยัน Q8 ให้ใช้ `/api/mapping`; จัดทำ [Mapping API/DTO contract](mapping-api-contract.md) ฉบับร่าง มี routes, field paths, BaseResponse, version conflict, pagination, action guards, preview limits และตัวอย่าง request-response โดยไม่แก้ source
- กำลังทำ: M1.2 รอ review แบบ API ที่กระทบ Web/contract boundary; M0.2 ยังไม่ปิด
- ติด / รอ: Q4 วิธีใช้เวลารับไฟล์ใน Informs, Q5 fields จากผู้ใช้, Q7 fixture/keys/dependencies, Q8 ส่วน route/DTO ของ Dev 1; preview limit ต้องพิสูจน์กับ fixture ที่รับรอง
- ต่อไป: ตรวจแบบ M1.2 กับ Dev 1 และระบุไฟล์ source ให้ยืนยันก่อน implement; เอกสารยังเป็น working tree ไม่ commit/push

### 2026-11-16 — ปิดแบบภายใน Mapping ของ M1.1
- เสร็จ: **M1.1** เติม [แบบข้อมูล/runtime §8](data-runtime-design.md#8-ข้อกำหนด-technical-สำหรับ-implement-m11) ครบ types/PK/FK/unique/indexes, raw storage, version pin, states/counts, transaction/lease/global budget และ attempt guards; ตรวจข้อบังคับด้วย PostgreSQL **15.20 local :5433** ผ่าน **29 checks** รวม **6 concurrent sessions** → [หลักฐาน SQL/ผล/ข้อจำกัด](m1-1-validation.md); ปิดแบบภายใน Mapping ตาม M-26 และบันทึก Q6/M-25 ตามคำตอบผู้ใช้
- กำลังทำ: เตรียม M1.2 API/DTO/action contract จากแบบที่ปิดแล้ว; M0.2 ยังไม่ปิด และยังไม่แก้ application source/test/Compose
- ติด / รอ: Q4 วิธีใช้ FileJob.CreatedAt ใน Informs, Q5 business fields ที่ผู้ใช้จะส่ง, Q7 dependencies/fixture และ Q8 API prefix; ไม่ใช้ผล proof schema ย่อแทน EF migrations/application integration tests ใน M1.3/M1.5 หรือผล Kafka/HTTP จริง
- ต่อไป: M1.2 พร้อมตัวอย่าง request-response; ก่อนเริ่ม source ชุด M1.3/M1.5 ต้องระบุไฟล์ให้ยืนยันตาม AGENTS.md; เอกสารยังเป็น working tree ไม่ commit/push

### 2026-11-13 — ออกแบบกลไกกลางและพัก business fields (M0.2 / M1.1 / M-24)
- เสร็จ: บันทึกคำสั่งผู้ใช้ให้ข้ามเรื่อง field/สัดส่วนใน Q5 ไว้รอส่งรายละเอียด และยึดการออกแบบกลางที่ครอบคลุมหลายรูปแบบ → M-24; จัดทำ [ร่างข้อมูลและ runtime](data-runtime-design.md) มี Module/interface, identities/ตาราง, transaction/lease, state/counts และ scenario coverage S01–S36
- กำลังทำ: M1.1 มีร่างสำหรับ review แล้ว แต่ยังไม่ปิดแบบ DDL/API หรือทำเครื่องหมายเสร็จ; กลไกไม่ผูก business field/จำนวน columns/outputs ของ MTLS กับทั้งระบบ และ MTLS ยังเป็นกรณีแรกตามแผนเดิม
- ติด / รอ: fields/defaults จริงจากผู้ใช้ใน Q5, D-23 ข้าม service ใน Q4, duplicate-content policy Q6, dependencies Q7, API contract Q8 และ Web design; รายละเอียด FK/raw types/lease-budget ต้อง review ก่อน implement
- ต่อไป: review แบบ M1.1 และลง M1.2 เป็น generic DTO/state/action contract โดยไม่ถาม field ทีละรายการต่อในรอบนี้; ก่อนแก้ source ยังต้องระบุไฟล์และรอยืนยันตามเดิม

### 2026-11-13 — ใช้ CreatedAt ของ File Job เป็นเวลารับไฟล์ (M0.2 / M-23)
- เสร็จ: บันทึกคำชี้แจงผู้ใช้เรื่อง Q4: ใช้ `CreatedAt` ของ File Job ฝั่ง Mapping เป็นเวลารับไฟล์ครั้งแรก ไม่เพิ่ม field เวลาแยกในตาราง → M-23; ปรับแบบข้อมูลในแผน API ให้ตรงกัน
- กำลังทำ: M0.2 รูปแบบการเก็บเวลาฝั่ง Mapping ชัดเจนแล้ว; ยังไม่แก้ source/DB
- ติด / รอ: Q4 ยังรอ Dev 1 ตัดสินวิธีรับ/ใช้ `FileJob.CreatedAt` ใน Informs เพราะ schema ปัจจุบันยังไม่มี field metadata รับเวลานี้ ไม่ถือว่าการใช้ CreatedAt ฝั่ง Mapping เปลี่ยน ReceivedAt ของ Informs แล้ว
- ต่อไป: ถาม Q5.1 เรื่องสัดส่วนผู้รับประโยชน์ 100% ที่ schema ไม่มี field; Q5 ส่วนค่าที่สร้าง payload ไม่ได้/Q6/Q7/Q8 ยังรอตามเดิม

### 2026-11-13 — ยังไม่เปิด changed-payload reprocess/resend (M0.2 / M-22)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q3 ข้อ 1: ช่วงแรกยังไม่ส่ง payload ใหม่จาก Source แถวเดิมหลังแก้ config; config ใหม่ใช้กับไฟล์ที่รับใหม่ ส่วน retry งานเดิมใช้ bytes/key เดิมภายใน 6 attempts → M-22; ปิด Q3 สำหรับขอบเขตช่วงแรกแบบเลื่อน feature
- กำลังทำ: M0.2 ปิดประเด็น contract gaps ที่เหลือก่อนแบบ M1.1/M1.2; ยังไม่แก้ source/DB
- ติด / รอ: Q4 เรื่องเวลารับไฟล์ข้าม service, Q5 ช่องว่าง business/schema, Q6 duplicate-content policy, dependencies ใน Q7 และ API contract ใน Q8; key/lineage สำหรับ changed-payload resend ในอนาคตยังไม่ได้ออกแบบ/อนุมัติ
- ต่อไป: ถาม Q4 เรื่องข้อเสนอให้ Informs รับและใช้เวลารับไฟล์ครั้งแรกจาก Mapping ตาม D-23

### 2026-11-13 — ปลายทางช่วงแรกเป็น Informs API (M0.2 / M-21)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.7 ข้อ 1: ทำปลายทาง Informs API ก่อน และเก็บ LegacyTable/JsonFile/DatabaseTable เป็น backlog → M-21; ปรับ feature matrix/capability scope ในแผน API
- กำลังทำ: M0.2 แนวทางหลักของ Source/Web/delivery และปลายทางช่วงแรกมีคำตอบแล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source/DB
- ติด / รอ: changed-payload reprocess/resend ใน Q3, contract gaps ใน Q4/Q5, duplicate-content policy ใน Q6, dependency ใน Q7 และ API contract ใน Q8; รายละเอียดแบบข้อมูล/API ยังต้องจัดทำใน M1.1/M1.2
- ต่อไป: ถาม Q3 เรื่องการส่ง payload ใหม่จากแถวเดิมหลังเปลี่ยน config เพื่อคง idempotency/lineage ตาม D-44

### 2026-11-13 — หยุด retry เมื่อครบ 6 attempts (M0.2 / M-20)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.6 ข้อ 2: หยุดที่ 6 attempts และไม่เปิด retry หลังครบเพดานผ่านหน้า Web → M-20; ปรับ API/action guard ให้คุมเพดานเดียวกับ worker และปิดประเด็นโควตาเพิ่มหลังครบเพดาน
- กำลังทำ: M0.2 ขอบเขต Source/Payload inspection และ manual retry หลังครบเพดานมีคำตอบแล้ว; ยังไม่แก้ source/DB
- ติด / รอ: feature scope ส่วนที่เสนอเลื่อนใน Q2, changed-payload reprocess ใน Q3, contract gaps ใน Q4/Q5 และ dependency ใน Q7; Web design ยังรอ
- ต่อไป: ถาม Q2.7 เรื่องปลายทางในช่วงแรกว่าจะทำเฉพาะ Informs API และเก็บ destination อื่นเป็น backlog ก่อนหรือไม่

### 2026-11-13 — ดู Delivery Payload ที่บันทึกจริง (M0.2 / M-19)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.5 ข้อ 1: เปิดดู JSON Delivery Payload แบบ read-only จาก payload ที่บันทึกไว้จริง รวมกรณี Informs reject เพื่อเทียบกับ Source → M-19; ปรับแผน API/Web scope
- กำลังทำ: M0.2 ขอบเขต Source/Delivery Payload inspection มีคำตอบแล้ว; รายละเอียด DTO/availability ลงใน M1.2 และ layout/interaction ยังรอ Web design ยังไม่แก้ source/DB
- ติด / รอ: กติกา manual retry หลังครบเพดาน/feature scope ส่วนที่เหลือใน Q2, Q3–Q8 และ dependency encryption keys ใน Q7
- ต่อไป: ถาม Q2.6 เรื่อง manual retry หลังครบ 6 attempts โดยคงประวัติและ bytes/key เดิม

### 2026-11-13 — ดู Source ดิบรายแถวแบบ read-only (M0.2 / M-18)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.4 ข้อ 1: มี Source view รายแถวแบบ read-only เปิดจากรายการไฟล์/แถว พร้อม config version และสถานะส่ง → M-18; ปรับแผน API/Web scope และหลักฐานตรวจรับเรื่องยอดข้อมูล
- กำลังทำ: M0.2 ขอบเขต Source inspection ตกลงแล้ว; layout/interaction และ DTO/count definitions ลงรายละเอียดภายหลัง ยังไม่แก้ source/DB
- ติด / รอ: การดู Delivery Payload แยกยังไม่ถือว่าตกลง, retry หลังครบเพดาน/feature scope ส่วนที่เหลือใน Q2, Q3–Q8 และ Web design
- ต่อไป: ถาม Q2.5 เรื่องดู JSON Delivery Payload ที่ Mapping เตรียมส่ง Informs แบบ read-only เพื่อเทียบกับ Source

### 2026-11-13 — Source schema เพิ่ม column ได้อย่างเดียว (M0.2 / M-17)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.3 ข้อ 1: เพิ่ม column ใน Source Table เดิมได้ แต่ห้ามลบ/rename/เปลี่ยนชนิด field เดิม; กรณีเปลี่ยนแบบนั้นสร้างตารางใหม่ → M-17; ปรับแผน API/เกณฑ์ตรวจรับและขอบเขต M1.5
- กำลังทำ: M0.2 รูปแบบ Source storage/วิธีสร้างตาราง/กติกาแก้ schema มีคำตอบแล้ว; รายละเอียด definition pin/constraints/concurrency ลงใน M1.1 ต่อไป ยังไม่แก้ source/DB
- ติด / รอ: Source/payload inspection และ feature scope ส่วนที่เหลือใน Q2, contract gaps ใน Q4/Q5 และ dependencies ใน Q7; layout/interaction ยังรอ Web design
- ต่อไป: ถาม Q2.4 เรื่องหน้าดู Source ดิบแบบ read-only แยกจาก delivery log

### 2026-11-13 — สร้าง Source Table ผ่านหน้า Mapping (M0.2 / M-16)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.2 ข้อ 1: กำหนด fields ผ่านหน้า Mapping แล้วระบบสร้าง physical Source Table → M-16; เพิ่ม M1.5 และปรับ Table Catalog/API/Web scope ในแผน API
- กำลังทำ: M0.2 ปิดรายละเอียดการดูแล Source schema; ทำส่วนที่ MTLS ใช้ก่อนตามลำดับเดิม และยังไม่แก้ source/DB
- ติด / รอ: กติกาเปลี่ยน Source schema หลังสร้างตาราง, definition pin/DDL permissions และ Web design; การเลือกสร้างผ่าน Web ยังไม่อนุมัติลบ/rename/เปลี่ยนชนิด columns หรือ Table admin ทั้งชุด
- ต่อไป: ถาม Q2.3 เรื่องการเปลี่ยน fields ของ Source Table โดยคงหลักฐานและ config version เดิม

### 2026-11-13 — Physical Source Table แยกตาม config (M0.2 / M-15)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.1 ข้อ 2: ใช้ physical Source Table แยกตาม config → M-15; ปรับ feature matrix/แบบข้อมูล/เกณฑ์ตรวจรับในแผน API แทนข้อเสนอตารางกลาง + JSONB เดิม
- กำลังทำ: M0.2 ปิดรายละเอียด Source storage เพื่อทำแบบ M1.1; ยังไม่แก้ source code/DB หรือสร้างตารางจริง
- ติด / รอ: วิธีสร้าง/ดูแล Source Table, schema evolution เมื่อ config version เปลี่ยน, การ pin table definition และสิทธิ์ DDL บริษัท ต้องลงรายละเอียดต่อ; scope ของ Table admin ผ่าน Web ยังไม่ถือว่าตกลง
- ต่อไป: ถาม Q2.2 ว่าต้องสร้าง/เพิ่ม column ของ Source Table ผ่านหน้า Mapping หรือให้ Dev เตรียม migration/script ก่อน

### 2026-11-13 — เริ่ม Kafka local ผ่าน Docker (M0.2 / M-14)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 9.2: เริ่ม Kafka local ผ่าน Docker เพื่อพัฒนา/ทดสอบระหว่างรอ broker/topics บริษัท → M-14; ปรับแผน API ให้แยกการเลือก environment ออกจากการ provision จริง
- กำลังทำ: M0.2 แนวทางหลักของ kickoff ข้อ 1–9 มีคำตอบผู้ใช้แล้ว; ต่อคำถาม Q2–Q8 ทีละข้อ โดยยังไม่ปิด M0.2 และยังไม่แก้ source/Compose
- ติด / รอ: รายละเอียด broker/topics/groups/owner บริษัทใน Q7, ช่องว่าง D-23 ใน Q4 และขอบเขต/contract/fixture ส่วนที่เหลือ; Kafka local ยังไม่ได้ถูกเพิ่มหรือเริ่มรันในงานนี้
- ต่อไป: ถาม Q2.1 เรื่องรูปแบบ Source storage ก่อนปิดแบบข้อมูล M1.1

### 2026-11-13 — Kafka สำหรับงานภายใน Mapping (M0.2 / M-13)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 9.1: ใช้ Kafka + transactional outbox สำหรับงานภายใน Mapping ตาม pattern Informs → M-13; ปรับแผน API โดย Mapping → Informs ยังเป็น HTTP ตาม D-03
- กำลังทำ: M0.2 แนวทางหลักของ kickoff ข้อ 1–9 มีคำตอบผู้ใช้แล้ว แต่ dependency/topics/groups และประเด็น contract/scope ที่เหลือยังไม่ปิด; ยังไม่แก้ source code
- ติด / รอ: การเตรียม Kafka local และรายละเอียด broker/topics/groups/owner ของบริษัทใน Q7; Q4 เรื่องเวลาข้าม service และ Q2–Q8 ส่วนที่เหลือยังรอข้อสรุป
- ต่อไป: ถาม Q1 / kickoff ข้อ 9.2 ว่าจะเริ่ม Kafka local ระหว่างรอ infrastructure บริษัทหรือมี broker บริษัทให้ใช้แล้ว

### 2026-11-13 — ขอบเขตหน้า Mapping ช่วงแรก (M0.2 / M-12)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 8: หน้า Mapping มี Config + ผลการส่ง + retry ตามสถานะ → M-12; ปรับแผน API และคำตอบส่วน Web scope ใน Q2
- กำลังทำ: M0.2 ขอบเขตหน้า Mapping มีคำตอบแล้ว; layout/interaction และรายการย่อย M5.3 ยังรอ Web design ไม่เริ่มแก้ source code
- ติด / รอ: Q1 ข้อ 9 และประเด็นที่เหลือใน Q2–Q8; retry หลังครบ 6 attempts/raw inspection/reprocess ยังไม่ถือว่าตกลงจากการเลือก Web scope นี้
- ต่อไป: ถาม Q1 / kickoff ข้อ 9 เรื่อง Kafka สำหรับงานภายใน Mapping; Mapping → Informs ยังคง HTTP ตาม D-03

### 2026-11-13 — กลุ่มผู้ใช้งาน delivery log (M0.2 / M-11)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 7.2: delivery log ใช้โดยทีม Mapping / Dev / Ops → M-11; ปรับแผน API และบันทึกคำตอบส่วนผู้ดู log ใน Q7
- กำลังทำ: M0.2 ชุดข้อมูลและกลุ่มผู้ใช้งาน delivery log มีคำตอบแล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 8–9 และประเด็นที่เหลือใน Q2–Q8; raw Source/payload inspection และ dependency ใน Q7 ยังไม่ปิด การกำหนดผู้ใช้งานไม่ได้เพิ่มระบบสิทธิ์
- ต่อไป: ถาม Q1 / kickoff ข้อ 8 เรื่องขอบเขตหน้า Mapping; layout/interaction ยังคงรอ Web design จากผู้ใช้

### 2026-11-13 — ชุดข้อมูล delivery log (M0.2 / M-10)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 7.1: ใช้ชุดข้อมูล delivery log ที่เสนอ โดยรายการปกติไม่แสดง raw Source/payload → M-10; ปรับแผน API ให้ตรงกัน
- กำลังทำ: M0.2 เก็บคำตอบทีละข้อ; ชุดข้อมูล log ตกลงแล้ว แต่ผู้ใช้งานในข้อ 7.2 และขอบเขต Web ในข้อ 8 ยังรอคำตอบ
- ติด / รอ: Q4 เรื่องเวลาข้าม service, Q1 ข้อ 7.2/8–9 และ Q2–Q8; การไม่แสดง raw ในรายการยังไม่ใช่ข้อสรุปว่าจะมีหน้าดู raw แยกหรือไม่
- ต่อไป: ถาม Q1 / kickoff ข้อ 7.2 เรื่องกลุ่มผู้ใช้งาน delivery log โดยไม่เพิ่มระบบสิทธิ์นอกขอบเขต `web.md`

### 2026-11-13 — เวลารับไฟล์ครั้งแรกฝั่ง Mapping (M0.2 / M-09)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 6.4: ใช้เวลาที่ Mapping รับไฟล์และสร้าง File Job ครั้งแรก เก็บคงเดิมเมื่อ resume/retry → M-09; ปรับแผน API และบันทึกหลักฐานช่องว่าง contract ใน Q4
- กำลังทำ: M0.2 ข้อ 1–5 และ 6.1–6.4 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Informs `6ab23ad` ไม่มี field เวลารับไฟล์จาก Mapping ใน schema/DTO และ handler ใช้เวลา HTTP intake ตั้ง `ReceivedAt`; Q4 จึงยังรอ Dev 1 ตัดสินวิธีรักษา D-23 ข้าม service ไม่ถือว่าปิดด้วยการเก็บเวลาเฉพาะ Mapping
- ต่อไป: ถาม Q1 / kickoff ข้อ 7 เรื่องข้อมูลที่ต้องแสดงใน delivery log; Q2–Q8 และ Web design ยังรอตามเดิม

### 2026-11-13 — เพดาน delivery attempts (M0.2 / M-08)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 6.3: delivery รวมสูงสุด 6 attempts รวมครั้งแรก 1 + retry สูงสุด 5 → M-08; ปรับแผน API ให้จำนวน attempts เป็นข้อสรุป ไม่ใช่ข้อเสนอ
- กำลังทำ: M0.2 ข้อ 1–5 และ 6.1–6.3 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: รายละเอียดเวลารับไฟล์ครั้งแรกใน Q4, ข้อ 7–9 และ Q2–Q8; retry delay/timeout และ action หลังครบเพดานยังต้องลงรายละเอียด
- ต่อไป: ถามเรื่องการเก็บเวลารับไฟล์ครั้งแรกให้คงเดิมเมื่อ resume/retry เพื่อปิดส่วนที่เหลือของ Q4

### 2026-11-13 — ค่าเริ่ม HTTP concurrency (M0.2 / M-07)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 6.2: เริ่มส่ง HTTP ไป Informs พร้อมกันสูงสุด 1 request แล้วปรับผ่าน config ภายหลัง → M-07; ปรับแผน API ให้แยกค่าที่เลือกแล้วจากข้อเสนอ retry
- กำลังทำ: M0.2 ข้อ 1–5, 6.1 และ 6.2 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 6 เรื่อง retry/ส่วนที่เหลือ, ข้อ 7–9 และ Q2–Q8; วิธีคุมเพดานเมื่อหลาย pod ต้องลงรายละเอียดใน M1.1/M4.2
- ต่อไป: ถาม Q1 / kickoff ข้อ 6.3 เรื่องจำนวน attempts สำหรับ transient failure

### 2026-11-13 — ลำดับแถวสำหรับ sourceRowNo (M0.2 / M-06)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 6.1: `sourceRowNo` ใช้ลำดับแถวข้อมูลที่อ่านได้ เริ่ม 1 ไม่รวม header/แถวว่าง และคงเดิมเมื่อ retry → M-06; ปรับแผน API และบันทึกคำตอบส่วนลำดับแถวใต้ Q4
- กำลังทำ: M0.2 ข้อ 1–5 และ 6.1 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 6 ส่วนที่เหลือ/ข้อ 7–9 และ Q2–Q8; Q4 ยังรอรายละเอียดการเก็บเวลารับไฟล์ครั้งแรก ไม่ถือว่าปิดทั้งข้อด้วยการเลือกลำดับแถว
- ต่อไป: ถาม Q1 / kickoff ข้อ 6.2 เรื่องค่าเริ่ม HTTP concurrency

### 2026-11-13 — ค่าคงที่และ P/C ใน config MTLS (M0.2 / M-05)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 5: เก็บค่าคงที่ MTLS และกฎแปลง P/C ใน versioned config ผ่าน `literal` / `case` ของ Transformation Program → M-05; ปรับแผน API ให้ตรงกัน
- กำลังทำ: M0.2 ข้อ 1–5 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 6–9 และ Q2–Q8 โดยเฉพาะค่าต้นทางที่สร้าง payload ผ่าน schema ไม่ได้ใน Q5
- ต่อไป: ถาม Q1 / kickoff ข้อ 6 ทีละประเด็น เริ่มจาก `sourceRowNo` ว่าจะใช้ลำดับแถวข้อมูลหรือ `nSeq`

### 2026-11-13 — Default ผู้รับประโยชน์ใน config MTLS (M0.2 / M-04)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 4: เก็บ default ผู้รับประโยชน์ตาม BR-035 ใน versioned config ของ MTLS ผ่าน Transformation Program → M-04; ปรับแผน API ให้ตรงกัน
- กำลังทำ: M0.2 ข้อ 1–4 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 5–9 และ Q2–Q8; Q5 เรื่องสัดส่วน 100% ที่ schema ไม่มี field ยังไม่ปิด
- ต่อไป: ถาม Q1 / kickoff ข้อ 5 เรื่องค่าคงที่ MTLS และการแปลง P/C ว่าเก็บที่ใด

### 2026-11-13 — รูปแบบไฟล์ MTLS สำหรับเริ่มงาน (M0.2)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 3: เริ่มจาก `.xls` BIFF8 ตามตัวอย่างก่อน ใช้ sheet แรก/header 19 columns/cp874 ตาม `mtls-file.md`; `.xlsx` ไม่เป็นเงื่อนไขตรวจรับช่วงแรก
- กำลังทำ: M0.2 ข้อ 1–3 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 4–9 และ Q2–Q8; รูปแบบไฟล์ที่เลือกยังไม่ใช่การรับรอง fixture 2,556 แถวหรือคำตอบเรื่อง `sourceRowNo` ใน Q4/Q7
- ต่อไป: ถาม Q1 / kickoff ข้อ 4 เรื่องตำแหน่ง default ผู้รับประโยชน์ตาม BR-035 ใน config mapping

### 2026-11-13 — ขอบเขต Transformation Program (M0.2 / M-03)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 2: ย้าย Transformation Program ทั้งหมด โดยทำให้ MTLS ใช้งานได้ก่อน; ลง M-03 และเพิ่ม M2.5 สำหรับส่วนที่เหลือ พร้อมปรับ feature matrix ในแผน API
- กำลังทำ: M0.2 ถามคำถามทีละข้อ; ข้อ 1–2 มีคำตอบผู้ใช้แล้ว แต่ยังไม่ปิด M0.2 และยังไม่เริ่ม implement source
- ติด / รอ: Q1 ข้อ 3–9 และ Q2–Q8; Named Lookup ของ demo ผูกกับ `mti_original` และ Table Catalog เดิม ต้องตกลงแหล่งข้อมูล/เส้นแบ่งงานก่อนเปิดใช้จริงตาม Q2/Q7 โดยยังเก็บในขอบเขตการย้ายทั้งหมด
- ต่อไป: ถาม Q1 / kickoff ข้อ 3 เรื่องรูปแบบไฟล์ MTLS; งาน MTLS ทำก่อน ส่วนที่เหลือต้อง review ลำดับและเป้าของ M2.5 โดยไม่เปลี่ยน milestone เดิมเอง

### 2026-11-13 — เก็บคำตอบ review M0.2 ทีละข้อ
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 1: หลังเก็บ immutable Source ให้สร้าง JSON `pa-inform/1` โดยตรงแล้วส่ง Informs ผ่าน HTTP; ไม่ใช้ Normalized Table แบบแบนเป็นขั้นกลาง
- กำลังทำ: M0.2 เก็บคำตอบข้อที่เหลือทีละข้อ ตามคำขอผู้ใช้; ยังไม่ปิด M0.2
- ติด / รอ: Q1 ข้อ 2–9 และ Q2–Q8 ยังไม่มีข้อสรุป; ผลที่เป็นเรื่องของ Dev 1 ให้ Dev 1 ลง `D-xx` ใน `rules.md` ตามกติกาเดิม
- ต่อไป: ถาม Q1 / kickoff ข้อ 2 เรื่อง Transformation Program หรือโค้ดเฉพาะ MTLS; ยังไม่มีการแก้ source code หรือ commit

### 2026-11-13 — ผลตรวจ baseline Mapping (M0.3)
- เสร็จ: **M0.3** รัน `./scripts/check.ps1` ใน `prjInsuranceValidationMapping` commit `78453b3cf1693561765db5d2ab7db75f1fd0e07d` ด้วย .NET SDK **10.0.404** แล้วจบ **exit 0 / All checks passed**: restore tools สำเร็จ, Release build สำเร็จ (0 errors / 4 warnings), test command จบแต่ **ไม่มี test case ให้รันทั้ง UnitTests และ IntegrationTests**, EF แจ้ง `No changes have been made to the model since the last migration.` · รายละเอียดและคำสั่งเลือก SDK อยู่ใต้ M0.3 ใน §3
- กำลังทำ: เตรียม review แผน M0.2 และแบบข้อมูล/API M1.1/M1.2; ยังไม่เริ่ม implement source code
- ติด / รอ: Q1–Q8 และ Web design ตามเดิม; build มี `NU1903` ของ `SSH.NET 2023.0.0` ใน integration test dependency (2 advisory IDs, แสดงซ้ำรวม 4 warnings) ต้องตรวจ dependency ก่อนเพิ่ม integration tests · ยังไม่มี Kafka container ที่กำลังรันใน local
- ต่อไป: ปิดคำตอบที่จำเป็นใน M0.2 แล้วลงแบบ M1.1/M1.2; baseline นี้ยืนยัน scaffold build/model เท่านั้น ยังไม่ใช่หลักฐานว่าทดสอบ feature/DB/Kafka ผ่าน

### 2026-11-13
- เสร็จ: **M0.1** จัดทำ [แผน implement API](api-implementation-plan.md) โดยเทียบ feature ทุกกลุ่มใน reverse spec กับขอบเขต phase 1, schema/response จริงของ Informs และ scaffold ของ Mapping; มี architecture, แบบร่างตาราง/API, ลำดับงาน, acceptance criteria และจุดส่งต่อ Web · หลักฐาน: `mapping-demo/docs/mapping-phase-1/api-implementation-plan.md` และรายการงานใน §3; ตรวจ local links, JSON examples, Markdown fences และความเชื่อมโยง **23 task IDs / 8 คำถาม** ผ่าน (เอกสารใน working tree; ยังไม่ commit)
- กำลังทำ: เตรียม review แผนก่อน kickoff 11-16; ยังไม่เริ่ม implement source code
- ติด / รอ: Q1–Q8 ที่เกี่ยวกับ kickoff/ขอบเขต, fixture ทดสอบที่รับรองแล้ว, infrastructure และ Web design จากผู้ใช้ · ประเด็น reprocess เปลี่ยน payload กับ idempotency และ BR-035 ที่ schema ไม่มี field สัดส่วนต้องได้คำตอบก่อนเปิดใช้
- ต่อไป: M0.2 ปิดคำถามที่จำเป็น → M0.3 วัด baseline จริง → M1.1/M1.2 ปิดแบบข้อมูลและ API; ก่อนแก้ source แต่ละชุดต้องระบุไฟล์และรอยืนยันตาม AGENTS.md

## 2. จุดสังเกต (milestone)

> วันที่มาจากแผนรวมของ Dev 1 (🟡 ทบทวนตอน kickoff) · จะเลื่อนต้องคุยกับ Dev 1 ก่อน แล้วเขียนเหตุผลใน §5

| วันที่ | จุดสังเกต | สถานะ |
|---|---|---|
| 11-16 | kickoff — ตกลงคำถามใน [rules.md](rules.md) §6 · Dev 2 เขียนงานใน §3 | ⬜ |
| 11-18 | อ่านไฟล์ทดสอบ MTLS 2,556 แถวครบทุกแถว | ⬜ |
| 11-23 | JSON ผ่าน schema `pa-inform-1.schema.json` ทุกแถว | ⬜ |
| **11-26** | **mapping ส่งไฟล์ MTLS เข้า Insurance Validation ได้ (บนเครื่อง)** | ⬜ |
| **12-03** | **หน้าเว็บ mapping ใช้ได้บนเครื่อง** | ⬜ |
| 12-04 | พร้อม deploy บน Kubernetes (mount share folder, ConfigMap / Secret) | ⬜ |
| 12-09 | ทดสอบร่วมบน cluster ผ่าน (ไฟล์ทดสอบเข้าทาง mapping ครบ) | ⬜ |
| 12-14 | พร้อม UAT | ⬜ |
| **12-15** | **เริ่ม UAT** | ⬜ |

สถานะ: ⬜ ยังไม่ถึง · 🔄 กำลังทำ · ✅ ผ่าน · ⛔ เสี่ยงไม่ทัน (เขียนเหตุผลใน §1)

## 3. งาน

> **Dev 2 วางแผนและเขียนเอง** — แตกงานเป็นกลุ่ม (`M1`, `M2`, …) และ bullet ย่อย · เพิ่ม bullet ได้ตลอด
>
> ```markdown
> - [ ] **M1.1** <งาน> — เสร็จเมื่อ: <สิ่งที่ตรวจได้> · เป้า MM-DD
> - [ ] **M1.2** <งาน> — เสร็จเมื่อ: … · เป้า MM-DD · 🔄
> - [ ] **M1.3** <งาน> — เสร็จเมื่อ: … · เป้า MM-DD · ⛔ รอ <อะไร / ใคร>
> - [x] **M1.4** <งาน> — เสร็จเมื่อ: … · เป้า MM-DD · ✅ MM-DD `prjInsuranceValidationMapping` <commit> · test ผ่าน
>   - Dev 1: <หมายเหตุ>
> ```
>
> - ID ห้ามเปลี่ยน / ห้ามใช้ซ้ำ · ตัดงาน = ~~ขีดฆ่า~~ + เหตุผล (ไม่ลบ)
> - "เสร็จเมื่อ" ต้องตรวจได้ (จำนวนแถว / test ผ่าน / ส่งเข้า API ได้) ไม่ใช่แค่ "ทำเสร็จ"
> - ติ๊ก `[x]` ได้เมื่อมี**หลักฐาน**: ชื่อ repo + commit และ/หรือผลทดสอบ
> - งานที่ผูกกับจุดสังเกตใน §2 ให้เป้าไม่เกินวันของจุดสังเกตนั้น

แผนล่วงหน้า **2026-11-13** สำหรับ review ตอน kickoff 11-16 — วันที่เป็นเป้าร่างที่อยู่ภายใน milestone เดิม ไม่ใช่การยืนยันคำตอบธุรกิจหรือการปรับวันส่ง รายละเอียด/traceability อยู่ใน [แผน API §2–§9](api-implementation-plan.md)

### M0 — วางแผนและปิด dependency

- [x] **M0.1** จัดทำ implementation plan แบบ API ก่อน Web — เสร็จเมื่อ: ครอบคลุม feature ใน reverse spec, แยกสิ่งต้องทำ/ปรับ/เสนอเลื่อน, ระบุ dependency และเกณฑ์ตรวจรับ · เป้า 11-13 · ✅ 11-13 `mapping-demo` [แผน API](api-implementation-plan.md) และ §3–§5 ของไฟล์นี้ (working tree; ยังไม่ commit; ไม่ใช่ผลทดสอบโค้ด)
- [ ] **M0.2** Review แผนและตกลง kickoff/ขอบเขตกับ Dev 1 — เสร็จเมื่อ: Q1–Q8 มีคำตอบสำหรับงานที่จะเริ่ม และผลถูกบันทึกเป็น M-xx/D-xx ตามเจ้าของเรื่อง · เป้า 11-16
- [x] **M0.3** ตรวจ baseline repo Mapping และ local dependencies — เสร็จเมื่อ: บันทึก commit/environment และผล `scripts/check.ps1` จริง รวม Docker/PostgreSQL/Kafka ที่ต้องเตรียม โดยไม่ใช้ผล test ของ demo แทน · เป้า 11-16 · ✅ 11-13 `prjInsuranceValidationMapping` commit `78453b3cf1693561765db5d2ab7db75f1fd0e07d`, script exit 0 (ข้อจำกัดด้าน test/warnings ตามด้านล่าง)
  - รอบแรก shell ใช้ `C:\Program Files\dotnet\dotnet.exe` SDK **8.0.400** → build fail `NETSDK1045` เพราะทุก project target `net10.0`; พบ SDK **10.0.404** ที่ `%USERPROFILE%\.dotnet` จึงเลือกรุ่นนี้เฉพาะ process แล้วรัน script ซ้ำสำเร็จ โดยไม่แก้ PATH ถาวร/source/script
  - ผลรอบที่ใช้ SDK 10: `dotnet-ef 10.0.4` restore สำเร็จ; Release build **0 errors / 4 warnings**; `dotnet test --configuration Release --no-build` ไม่พบ test case ในสอง projects (ตรวจไฟล์พบเพียง `.csproj`); EF model ตรง migration; `All checks passed.` **ไม่ตีความเป็น tests ของ feature ผ่าน**
  - warnings: `NU1903` package `SSH.NET 2023.0.0` ใน dependency ของ `Mapping.IntegrationTests` อ้าง `GHSA-mggc-4xg6-vcxf` และ `GHSA-q939-rpr3-3284`; ยังไม่แก้ package เพราะงานนี้รัน baseline เท่านั้น
  - local dependencies ที่ตรวจ: Docker client/server **29.8.0**, `InsuranceValidation.Postgres` image `postgres:15-alpine` healthy port **5433**, `InsuranceValidation.Mapping` running port **5177**; ไม่พบ Kafka container ที่กำลังรัน จึงยังต้องเตรียมใน M3.1/Q7; รอบนี้ไม่ใช้ `-Db`, ไม่ apply migration และไม่มี integration test เชื่อม DB จริง
  - คำสั่ง PowerShell สำหรับรันซ้ำด้วย SDK ที่ติดตั้งอยู่แล้ว (environment มีผลเฉพาะ shell ที่รัน):

    ```powershell
    $env:DOTNET_ROOT = Join-Path $env:USERPROFILE '.dotnet'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    dotnet --version  # รอบนี้ได้ 10.0.404
    ./scripts/check.ps1
    ```

### M1 — แบบข้อมูลและ API พื้นฐาน

- [x] **M1.1** ปิดแบบ entity/state/ตาราง/unique identities และ transaction boundaries — เสร็จเมื่อ: มี data design พร้อมตัวอย่าง Source/version/jobs/attempts, lineage และแบบป้องกันหลาย pod ใน repo เอกสาร; ไม่มีตารางนอก schema `mapping` · เป้า 11-16 · ✅ 11-16 `mapping-demo` [แบบข้อมูล/runtime](data-runtime-design.md) §8 ตาม M-26 และ [หลักฐาน PostgreSQL local](m1-1-validation.md): 29 checks ผ่าน; working tree ยังไม่ commit (ปิด design ภายใน Mapping ไม่ใช่ปิด EF/API/Kafka/HTTP tests)
  - หลักฐาน 11-16: proof schema **13 tables / 13 FKs** อยู่ `mapping`, public **0 tables**, tables เป็นของ `insurance_user`; 25 constraint/transaction checks + 4 concurrency/fencing assertions ผ่าน บน PostgreSQL 15.20; ฐานทดสอบแยกถูกลบหลังตรวจ ไม่แก้ฐาน `InsuranceValidation`/DB บริษัท
  - ขอบเขตปิดงาน: Q2 ทางเลือกหลักและ Q6 ได้คำตอบแล้ว; Q4/Q5/Q7/Q8 ยังเป็น gate ของ contract/business binding/environment/API ที่เกี่ยว ไม่ทำให้ต้องเดา fields หรืออ้างว่าปิด M0.2/M1.2/M1.3/M1.5 แล้ว
  - ร่าง 11-13: [data-runtime-design.md](data-runtime-design.md) ครอบคลุม Module/interface, Source registry ที่ไม่เก็บ raw กลาง, definition/version pin, transaction/lease/recovery, global budget/attempt cap, row/output counts และ scenarios S01–S36; ยังไม่ปิด M1.1 เพราะ exact DDL/FKs/raw types และประเด็น Q4/Q6/Q7 ที่เกี่ยวต้องได้ข้อสรุป
  - คำตอบผู้ใช้ (11-13), Q2.1: physical Source Table แยกตาม config ตาม M-15 แทนตารางกลาง + JSONB; แบบต้องครอบคลุม table/field metadata, source identity, transaction/constraints และการ pin table definition ให้ config version/งานเดิม รวมถึง schema evolution/วิธีสร้างตารางที่ยังรอคำตอบ
- [ ] **M1.2** ปิด Mapping API/DTO/error contract สำหรับ Web ภายหลัง — เสร็จเมื่อ: route/field paths/BaseResponse/version conflict/pagination/action states/preview limits มีตัวอย่าง request-response และตกลง prefix ใน Q8 · เป้า 11-17 · 🔄 [ร่าง contract](mapping-api-contract.md) 11-16; prefix `/api/mapping` ยืนยันแล้ว, ผู้ใช้ขอเลื่อน review route/DTO ที่กระทบ Web/contract boundary จึงยังไม่ติ๊กเสร็จ
- [ ] **M1.3** Implement EF persistence, config/version lifecycle และ immutable Source storage — เสร็จเมื่อ: migration/constraints ผ่าน PostgreSQL integration tests, activate พร้อมกันได้ conflict ชัดเจน, version เก่า/Source rows ไม่ถูกแก้ · เป้า 11-18 · รอ M1.1 และการยืนยันรายการไฟล์ source
- [ ] **M1.4** Implement Informs contract/profile snapshot และ capability catalog — เสร็จเมื่อ: schema/hash/path catalog ตรง Informs `insureds[]`, version pin ได้, ไม่มี latest-schema fallback เปลี่ยนงานเดิม, ไม่คืน secret/เปิด capability ที่ยังไม่ทำ · เป้า 11-18
  - คำตอบผู้ใช้ (11-13), Q2.7: destination ช่วงแรกเป็น Informs API (`ApiContract`) ตาม M-21; LegacyTable/JsonFile/DatabaseTable เป็น backlog ไม่แสดงว่าพร้อมใช้จากการย้าย Transformation Program ทั้งหมด
- [ ] **M1.5** Implement Source catalog/API สำหรับสร้าง physical Source Table และเพิ่ม columns ผ่าน Web — เสร็จเมื่อ: metadata/physical schema อยู่ `mapping` และตรงกัน, validation ชื่อ/ชนิด columns/system columns ผ่าน, สร้าง/เพิ่ม column พร้อมกันหรือล้มเหลวไม่ทิ้ง table/metadata ค้าง, บันทึก audit และ PostgreSQL integration tests ผ่าน; เพิ่ม columns ได้อย่างเดียวตาม M-17 และงานเก่าอ่าน definition/Source เดิมได้ · เป้า 11-18 (ร่างสำหรับ review M0.2) · รอ M1.1/M1.2, DDL permissions ใน Q7 และการยืนยันรายการไฟล์ source

### M2 — อ่าน MTLS และสร้าง payload

- [ ] **M2.1** Implement Excel reader สำหรับ MTLS — เสร็จเมื่อ: fixture ที่รับรองอ่านได้ครบ **2,556 แถว** จาก sheet แรก/header 19 columns/cp874, มี physical/import row number, ภาษาไทยและ number formatting ถูกต้อง · เป้า 11-18 · [fixture สมมติพร้อมแล้ว](fixtures/README.md); รอยืนยันรายการไฟล์ source ก่อน implement
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 3: เริ่ม `.xls` BIFF8 ตามตัวอย่างก่อน; `.xlsx` ไม่เป็นเงื่อนไขตรวจรับช่วงแรก และยังไม่ถือว่ายืนยัน fixture ทดสอบใน Q7
  - Dev 1 ตรวจใหม่ (11-16): ไฟล์จริงและ fixture เป็น **BIFF5/stream `Book`** ไม่ใช่ BIFF8; CODEPAGE ระบุ 1252 แต่ข้อความไทยเป็น cp874; acceptance ของ reader ต้องยึด [mtls-file.md](mtls-file.md) ที่แก้แล้วและ [fixture สมมติ](fixtures/README.md)
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.1: `sourceRowNo` เป็นลำดับแถวข้อมูลเริ่ม 1 ไม่รวม header/แถวว่าง; เก็บ Physical Line Number แยก และ retry ใช้เลขเดิมตาม M-06 ไม่อิงค่า `nSeq`
- [ ] **M2.2** ย้าย Transformation Program ส่วนที่ MTLS ใช้ และสร้าง versioned MTLS config — เสร็จเมื่อ: nested `insureds[]/beneficiaries[]`, constants, P/C, null+unparsed/default ตามคำตอบ kickoff ผ่าน unit/contract tests โดยไม่ตรวจ product/Client API เอง · เป้า 11-20 · รอ Q1/Q5
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 4: default ผู้รับประโยชน์อยู่ใน versioned config ของ MTLS ผ่าน Transformation Program ตาม M-04; เรื่องสัดส่วนใน Q5 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 5: ค่าคงที่ MTLS และกฎ P/C อยู่ใน versioned config ผ่าน `literal` / `case` ตาม M-05; ไม่ hardcode ใน C# และไม่กำหนด fallback ให้ transaction type ที่ยังไม่ตกลงใน Q5
- [ ] **M2.3** Implement validate/preview/workbook preview — เสร็จเมื่อ: ใช้ runtime compiler เดียวกัน, diagnostics มี path/code, จำกัดขนาด/records และ integration test ยืนยันไม่มี job/source/outbox/HTTP/archive side effects · เป้า 11-23
- [ ] **M2.4** ทดสอบ MTLS เต็มไฟล์กับ schema จริง — เสร็จเมื่อ: 2,556 แถวได้ 2,556 outputs `default` ผ่าน `pa-inform/1`, แถว C ครบและค่าแปลงไม่ได้ยังอยู่ใน unparsed; บันทึกผล/เวลารัน/fixture identity โดยไม่มี PII · เป้า 11-23 · [fixture สมมติพร้อมแล้ว](fixtures/README.md); Q5/schema semantics ติดตามแยก
- [ ] **M2.5** ย้าย Transformation Program ส่วนที่เหลือให้ครบตาม M-03 หลังส่วนที่ MTLS ใช้ — เสร็จเมื่อ: มี traceability และผลทดสอบของ value expressions 13 แบบ, predicates 17 แบบ, conversion targets 7 ชนิด, filter/branches, nested object/array, Named Transform 1 รายการ และ Named Lookup 2 รายการครบ; capability ที่แสดงว่าพร้อมใช้งานต้องมี implementation และ dependency พร้อมจริง · เป้า รอ review M0.2 โดยคง milestone เดิม · รอ M2.2 และคำตอบ Q2/Q7 สำหรับ lookup adapters ที่ผูก legacy

### M3 — รับไฟล์และประมวลผลแบบ durable

- [ ] **M3.1** เพิ่ม outbox/Kafka consumers และ local infrastructure ตาม Informs — เสร็จเมื่อ: transaction/offset/redelivery integration tests ผ่าน, events มี job IDs ไม่มี PII, topic names และบริษัท dependency ได้ข้อสรุป · เป้า 11-20 · แนวทาง Q1 ข้อ 9 ตอบแล้วตาม M-13/M-14; รายละเอียดบริษัทใน Q7 ยังรอ
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 9.1: เลือก Kafka + transactional outbox ตาม M-13; ยังรอการเตรียม broker local และรายละเอียด topics/groups/owner ของบริษัทใน Q7 ไม่ถือว่าพร้อมใช้งานเพียงเพราะเลือกแนวทางแล้ว
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 9.2: เริ่ม Kafka local ผ่าน Docker ตาม M-14; ต้องแจ้งไฟล์ Compose/config/test ที่จะเพิ่มหรือแก้และรอยืนยันก่อนลงมือ ยังไม่มีการ provision หรือผลทดสอบ Kafka ของ Mapping ในงานนี้
- [ ] **M3.2** Implement scan/snapshot/import/resume/archive — เสร็จเมื่อ: pin version ก่อนรับ, partial-file stability/path containment/ownership ทำงาน, หลาย scanner ไม่รับ occurrence ซ้ำ, Source+Row Job+outbox atomic, archive ไม่ทำหลักฐานหายเมื่อ original เปลี่ยน · เป้า 11-23 · duplicate-content policy ตามคำตอบ Q6/M-25 และแบบ M1.1
- [ ] **M3.3** Implement row transformation jobs, claim/lease/recovery และ row diagnostics — เสร็จเมื่อ: restart/redelivery ไม่สร้าง Source/output ซ้ำ, business conversion failure ไม่ทิ้งแถว, system/contract failure แสดงในยอดที่ยังไม่ส่ง และแถวอื่นไปต่อได้ · เป้า 11-24

### M4 — ส่ง Informs และดูแลผลการส่ง

- [ ] **M4.1** Implement encrypted durable Delivery Payload และ typed Informs HttpClient — เสร็จเมื่อ: 1 request/ใบ, bytes/hash/key persist ก่อนส่ง, 201/200/replayed/replaced/400/409 ตรง contract จริง และไม่แตะ `non_motor.*` · เป้า 11-24
- [ ] **M4.2** Implement retry/backoff/Retry-After/concurrency/lease recovery — เสร็จเมื่อ: 429/5xx/timeout และ crash หลัง Informs commit ใช้ bytes/key เดิม, attempt bounded/ไม่ retry ซ้อน, permanent rejection ไม่ auto retry · เป้า 11-25
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.2: เริ่ม HTTP concurrency สูงสุด 1 request ไป Informs และปรับผ่าน config ภายหลังตาม M-07; แบบการคุมเพดานหลาย pod ยังต้องระบุใน M1.1/M4.2 ไม่ถือว่าเพดานเพิ่มอัตโนมัติตามจำนวน pod
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.3: รวมสูงสุด 6 attempts ต่อ delivery รวมครั้งแรก 1 + retry สูงสุด 5 ตาม M-08; จำนวนครั้งต้อง durable ไม่รีเซ็ตเมื่อ restart/redelivery และไม่ให้ HttpClient retry ซ้อนเพิ่มจำนวนโดยไม่ถูกนับ
- [ ] **M4.3** Implement delivery log/file summary/retry และ pause controls ที่ตกลง — เสร็จเมื่อ: counts ต่อ unique output ไม่ปน attempts, แยก import/transform/delivery, field reason/history ดูได้, action state guard/audit ผ่าน; changed-payload reprocess/business resend ปิดในช่วงแรกตาม M-22 · เป้า 11-26
  - คำตอบผู้ใช้ (11-13), Q2.6: ครบ 6 attempts แล้วไม่เปิด Web retry ตาม M-20; API/worker ต้องปฏิเสธการส่งครั้งที่ 7 ของ delivery เดิมด้วย ไม่ใช่ปิดปุ่มอย่างเดียว ทดสอบ state/concurrent retry ว่าไม่สร้าง attempt/outbox/HTTP เกินเพดาน
  - คำตอบผู้ใช้ (11-13), Q2.5: Delivery Payload inspection แบบ read-only ตาม M-19; ใช้ payload snapshot ที่บันทึกจริง ไม่ compile ใหม่ และทดสอบว่าเปิดดูแล้ว bytes/hash/key/job state ไม่เปลี่ยน ไม่ส่ง HTTP หรือสร้าง delivery ใหม่
  - คำตอบผู้ใช้ (11-13), Q2.4: Source inspection รายแถวแบบ read-only ตาม M-18; แยกยอดแถวที่อ่านพบ/Source ที่บันทึกและยอดส่งตามสถานะโดยไม่ปน attempts พร้อมแสดง import state เพื่อไม่อ้างว่าอ่านครบขณะ import ยังไม่จบ
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 7.1: ชุดข้อมูล delivery log ตาม M-10; ผู้ใช้งาน/ขอบเขต action และ raw inspection แยกยังรอข้อสรุป ไม่ถือว่าปิดทั้ง task
- [ ] **M4.4** ทดสอบ end-to-end Mapping → Informs บนเครื่อง — เสร็จเมื่อ: fixture ครบ, source/payload/delivery counts กระทบยอดได้, replay/replaced/rejection/throttle/restart ผ่าน, `scripts/check.ps1` ผ่านและบันทึก repo+commit/ผลจริง · เป้า **11-26**

### M5 — ส่งต่อ Web และเตรียม Kubernetes

- [ ] **M5.1** เตรียม environment config, Linux paths, share mount, probes และ shutdown/recovery — เสร็จเมื่อ: `/health/live` และ `/health/ready` (ตรวจ DB) ใช้ได้, secrets/key references ไม่อยู่ใน image, pod restart ไม่ทำงานหาย, deploy/migration requirements ส่งให้ Dev 1/ทีม infrastructure · เป้า 12-04
- [ ] **M5.2** ส่ง API contract/OpenAPI/examples ให้ทำ Web — เสร็จเมื่อ: เอกสารตัวอย่าง/DTO/states/counts/action availability ตรง API ที่รันจริง, CORS/base URL พร้อม, ไม่มี docs ใน repo โค้ด · เป้า 11-27
- [ ] **M5.3** Implement และเชื่อมหน้า mapping หลังได้รับ Web design — เสร็จเมื่อ: scope ที่ตกลงใช้งานบนเครื่องผ่าน `core/mapping-api.ts` + `features/mapping/`, build/unit tests ของ Web ผ่าน, ไม่แก้เมนูอื่นและไม่เพิ่ม package โดยยังไม่บอก Dev 1 · เป้า **12-03** · รอ design จากผู้ใช้; รายการย่อยเพิ่มหลัง review design
  - คำตอบผู้ใช้ (11-13), Q2.5: รวมการเปิด JSON Delivery Payload แบบ read-only เพื่อเทียบ Source ตาม M-19; แสดงเฉพาะ snapshot ที่มีจริง รวม rejected delivery ถ้ายังไม่มี payload ให้แสดงสถานะ/diagnostic ตามจริง
  - คำตอบผู้ใช้ (11-13), Q2.4: รวมการเปิด Source ดิบรายแถวแบบ read-only จากรายการไฟล์/แถวตาม M-18; ไม่ทำหน้าจัดการใบหรือ business validation ซ้ำกับ Informs/UW
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 8: Config (สร้าง version/validate/preview/activate/disable) + ผลการส่ง (ไฟล์/แถว/attempts) + retry ตามสถานะ ตาม M-12; ยังรอ layout/interaction จาก Web design และต้องยืนยันรายการไฟล์ก่อนแก้ source ตามเดิม
  - คำตอบผู้ใช้ (11-13), Q2.2: รวมการกำหนด fields/สร้าง physical Source Table ผ่านหน้า Mapping ตาม M-16 โดยใช้ API จาก M1.5; layout/interaction และกติกาแก้ schema ยังต้องปิดก่อน implement

### M6 — ทดสอบร่วมและเตรียม UAT

- [ ] **M6.1** ทดสอบร่วมและ recovery บน cluster — เสร็จเมื่อ: fixture ผ่านเส้นทาง share → Mapping → Informs ครบ, ทดสอบ restart/หลาย consumer/permissions/mount/secrets พร้อมหลักฐาน environment · เป้า 12-09
- [ ] **M6.2** ปิด regression และเตรียมหลักฐาน UAT — เสร็จเมื่อ: acceptance ในแผน API §9 ผ่าน, issue ที่บล็อกถูกปิด, มีวิธีดูยอด/แก้เหตุส่งไม่สำเร็จ/retry และ owner ของข้อจำกัดที่ยังค้าง · เป้า 12-14

Source catalog/การกำหนด fields และสร้าง physical Source Table ผ่าน Web ผู้ใช้เลือกแล้วตาม M-16 และเพิ่ม M1.5; ส่วน feature ที่เสนอเลื่อนหลังเส้นทาง MTLS API (Table verification/impact admin เพิ่มเติม, input อื่น, destination อื่น, generic contract/profile admin, business resend/rollout) ยังอยู่ใน [feature matrix](api-implementation-plan.md#2-ขอบเขตและการเทียบ-feature) และ Q2/Q3; เพิ่มงาน/เป้าเมื่อได้รับข้อตกลง ไม่ถือว่าได้รับอนุมัติให้ implement หรือถูกตัดทิ้งแล้ว

## 4. การตัดสินใจของ mapping (`M-xx`)

> เรื่องภายใน mapping ที่ Dev 2 ตัดสินเองได้ (ตารางใน schema `mapping`, วิธีอ่านไฟล์, retry / ความขนาน, โครง config mapping, หน้าเว็บ mapping) · เรื่องที่กระทบ contract / เส้นแบ่งงาน / ขอบเขต / วันส่งงาน → ไป §5 ก่อน
>
> ```markdown
> ### M-01 <หัวข้อสั้น>
> - **ตัดสิน (YYYY-MM-DD):** ...
> - **ทำไม:** ...
> - **ไม่เลือก:** <ทางอื่นที่คิดไว้ + เหตุผล>
> - **ห้าม:** (ถ้ามี)
> ```

### M-01 วาง implementation plan แบบ API ก่อน Web และตามขอบเขต phase 1
- **ตัดสิน (2026-11-13):** ใช้เอกสารใน `docs/mapping-phase-1/` เป็นหลัก ใช้ reverse spec เป็น feature inventory; แยกงานที่จำเป็นต่อ MTLS → Informs และข้อเสนอ feature ที่เลื่อนลงใน [แผน API](api-implementation-plan.md) เริ่ม API ก่อนและรอ Web design ตามคำสั่งผู้ใช้
- **ทำไม:** ให้แผนครบทั้ง demo แต่ไม่ถือว่า feature ทุก destination และ prototype อยู่ใน phase 1 แล้ว; API สามารถตรวจรับได้ก่อนมีหน้าจอ
- **ไม่เลือก:** ยก demo ทั้ง application หรือกำหนด Web layout ล่วงหน้า เพราะขัด D-77/stack ของ Web และยังไม่มี design
- **ห้าม:** ใช้แผนนี้แทนคำตอบ kickoff หรือเปลี่ยน scope/contract/milestone โดยไม่ตกลงกับ Dev 1; ยังต้องขอยืนยันรายการไฟล์ก่อนแก้ source ทุกชุด

### M-02 ใช้โครง repo จริงและ pattern ของ Informs เป็นจุดตั้งต้น
- **ตัดสิน (2026-11-13):** วาง feature ใน 4 layer เดิมของ Mapping; วาง hosted services/outbox/Kafka/typed HttpClient ตามรูปแบบ Informs โดยยังไม่เพิ่ม Worker/Shared project และใช้ EF migrations/migrator ของ Mapping
- **ทำไม:** D-77 ให้ทีมเปิดแต่ละ repo แล้วพบโครงเดียวกัน; Mapping ปัจจุบันเป็น scaffold ยังไม่มี feature ให้ถือว่าเสร็จแล้ว
- **ไม่เลือก:** ยก Dapper/embedded SQL migrations, .NET 8 หรือ startup auto-migrate ของ demo มาทั้งชุด เพราะไม่ตรง stack และ deployment flow ของ repo จริง
- **ห้าม:** project reference/DB write ข้าม service หรือให้ code/test/script อ้าง path repo เอกสาร; การใช้ Kafka/topics จริงยังรอ Q1 ข้อ 9/Q7

### M-03 ย้าย Transformation Program ทั้งหมด โดยให้ MTLS ใช้งานได้ก่อน
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 2 ให้ย้ายความสามารถ Transformation Program ทั้งหมดจาก demo เข้าสู่โครง Mapping ตาม D-77; M2.2 ทำส่วนที่ MTLS ต้องใช้ก่อน และ M2.5 เก็บส่วนที่เหลือ ไม่ตัด operator ที่ MTLS ยังไม่ใช้ทิ้ง
- **ทำไม:** ผู้ใช้ต้องการ engine ครบ โดยให้เส้นทาง MTLS → Informs เป็นลำดับแรก
- **ไม่เลือก:** เขียน C# เฉพาะ MTLS แทน engine หรือย้ายเพียง MTLS subset แล้วตัดส่วนอื่นออกจากแผน
- **ขอบเขต:** value expressions 13 แบบ, predicates 17 แบบ, conversion targets 7 ชนิด, filter/0..N branches, nested object/array, deterministic evaluation/limits และ Named Transform/Lookup ตาม capability catalog ของ demo; รายการตั้งต้นมี `formatRegNo@1`, `mtiOriginalPolicyExists@1`, `mtiOriginalLoanExists@1`
- **ข้อจำกัด:** Named Lookup เดิมอาศัย `mti_original` และ Table Catalog ของ demo จึงต้องปิดแบบ adapter/แหล่งข้อมูลใน Q2/Q7 ก่อนเปิดใช้งาน; ไม่ถือว่าคำตอบนี้อนุมัติ partner ใหม่, destination อื่น, dynamic Table admin หรือการตรวจ product/Client API ใน Mapping ตาม D-01/D-06 การตรึง semantics ต้อง review ความเหมาะสม ไม่คัด `sqlserver-legacy-v1` เป็นกฎกลางโดยอัตโนมัติ
- **ห้าม:** ใช้ filter/branches ทิ้งแถว MTLS หรือเปลี่ยนข้อกำหนด 1 output `default` ต่อแถวโดยไม่มีข้อตกลง; แสดง capability ว่าพร้อมทั้งที่ยังไม่ implement/ไม่มี dependency, เปลี่ยน milestone เอง หรือแก้ source โดยไม่ยืนยันรายการไฟล์

### M-04 เก็บ default ผู้รับประโยชน์ใน versioned config ของ MTLS
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 4 ให้กำหนด default ตาม BR-035 ใน Transformation Program ของ config MTLS แต่ละ version; งานที่รับแล้วใช้ version ที่ pin ไว้ตามเดิม
- **ทำไม:** default เป็นกฎของ partner ตาม D-06 และต้องตรวจย้อนหลังได้จาก config version โดยไม่แก้ Source ตาม D-07
- **ไม่เลือก:** hardcode default เฉพาะ MTLS ใน C# หรือทำเป็น default กลางของทุก partner
- **ตัวอย่างข้อมูลสมมติ:** Source ที่ช่องชื่อผู้รับประโยชน์ทั้งสองไม่มีค่า → ส่วนของ JSON ที่ config สร้างคือ `"beneficiaries": [{"seq": 1, "fullName": "ทายาทตามกฎหมาย"}]` ภายใน `inform.insureds[0]`; Source เดิมยังคงเดิม ตัวอย่างนี้แสดงเฉพาะส่วนที่เกี่ยวกับ default ไม่ใช่ payload เต็ม
- **ห้าม:** เพิ่ม field สัดส่วน/กำหนดรหัส relationship ขึ้นเอง; เรื่องสัดส่วน 100% ที่ schema ไม่มี field ยังรอคำตอบ Q5 และไม่ถือว่าปิดด้วยการเลือกตำแหน่ง config

### M-05 เก็บค่าคงที่และกฎ P/C ใน versioned config ของ MTLS
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 5 ให้ใช้ `literal` สำหรับค่าคงที่ `MTLSPA`, `MTL2`, `74000490`, `FILE`, `PRIMARY` และ `case` สำหรับ P/C ใน Transformation Program ของ config MTLS แต่ละ version; งานเดิมใช้ config ที่ pin ไว้
- **ทำไม:** กฎของ partner อยู่ใน config ที่ตรวจย้อนหลังได้ตาม D-06/BR-025 ไม่ต้องแก้โค้ดและ deploy เมื่อปรับ definition
- **ไม่เลือก:** hardcode ค่าคงที่หรือเงื่อนไข MTLS ใน C# หรือใช้ค่าปัจจุบันแทน config version ที่งานเดิมอ้าง
- **ตัวอย่างข้อมูลสมมติ:** Source `Transaction Type=P` → `inform.transactionType=NEW`; Source `Transaction Type=C` → `inform.transactionType=CANCELLED`; ทั้งสองใช้ `inform.productCode=MTLSPA`, `inform.agentCode=74000490`, `inform.saleChannel=FILE`, `inform.insureds[0].packageCode=MTL2`, `inform.insureds[0].relation=PRIMARY` จาก config และเก็บ Source เดิมตาม D-07
- **ห้าม:** เติมเบี้ย/ทุนจาก package ตาม D-06; เดา fallback ของ transaction type ที่ว่างหรือไม่ใช่ P/C หรือเพิ่ม contract field เอง ประเด็นค่าที่สร้าง payload ผ่าน schema ไม่ได้ยังรอ Q5

### M-06 ใช้ลำดับแถวข้อมูลสำหรับ sourceRowNo
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 6.1 ใช้ลำดับแถวข้อมูลที่อ่านจาก sheet แรก เริ่ม 1 ไม่รวม header/แถวว่าง เป็น `sourceRowNo`; persist พร้อม Source แล้วใช้เลขเดิมเมื่อ resume/retry ไม่ใช้ลำดับ HTTP arrival ตาม D-23
- **ทำไม:** ลำดับไม่ขึ้นกับคุณภาพของ `nSeq` หรือความขนานในการส่ง; ตรวจกลับตำแหน่งต้นทางได้ด้วย Physical Line Number ที่เก็บแยก
- **ไม่เลือก:** ใช้ค่า `nSeq` เป็น `sourceRowNo`; ค่าว่าง/ซ้ำ/ไม่เรียง/มี gap ของ `nSeq` เก็บตาม Source ดิบและไม่เปลี่ยนลำดับแถวข้อมูล
- **ตัวอย่างข้อมูลสมมติ:** physical row 1 เป็น header; row 2 มีข้อมูลและ `nSeq=10` → `sourceRowNo=1`; row 3 ว่าง → ไม่นับ; row 4 มีข้อมูลและ `nSeq=10` → `sourceRowNo=2` โดย Source ของสองแถวเก็บ Physical Line Number เป็น 2 และ 4 ตามลำดับ
- **ห้าม:** ข้ามแถวข้อมูลเพียงเพราะแปลงค่าธุรกิจไม่ได้ตาม D-04 หรือเปลี่ยนลำดับเมื่อ retry; การจัดเก็บเวลารับไฟล์ครั้งแรกใน Q4 และ duplicate-content policy ใน Q6 ยังรอข้อสรุป

### M-07 เริ่มส่ง Informs พร้อมกันสูงสุด 1 request
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 6.2 กำหนดค่าเริ่ม HTTP concurrency ไป Informs เป็น 1 request และปรับผ่าน config ภายหลังจากผลทดสอบ/ข้อจำกัดของ Informs ตาม D-03
- **ทำไม:** เริ่มตรวจเส้นทาง MTLS และผลการส่งก่อนปรับความขนาน โดยไม่เปลี่ยน logic หรือ deploy โค้ดเพื่อแก้ค่าความขนาน
- **ไม่เลือก:** ส่งหลาย request พร้อมกันตั้งแต่เริ่มหรือ hardcode ค่าใน C#
- **ตัวอย่างข้อมูลสมมติ:** Delivery A/B/C พร้อมส่งพร้อมกัน → เมื่อ A กำลังส่ง B/C ยังรอช่องส่ง; หลัง A จบจึงเริ่ม request ถัดไป โดยลำดับการประเมินของ Informs ยังคงอิง D-23 ไม่ใช่ HTTP arrival
- **ข้อจำกัด:** ต้องปิดวิธีคุมเพดานในกรณีหลาย pod ใน M1.1/M4.2; ไม่ตีความว่าเพิ่ม pod แล้วเพิ่มเพดานรวมได้โดยอัตโนมัติ
- **ห้าม:** เปลี่ยน bytes/key ของ delivery เดิมเพื่อเพิ่ม concurrency หรือวนส่งต่อโดยไม่เคารพ `429`/`Retry-After`; จำนวน attempts/backoff/timeout ยังรอข้อสรุปในส่วนที่เหลือของ Q1 ข้อ 6

### M-08 จำกัด delivery รวมสูงสุด 6 attempts
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 6.3 กำหนดเพดานส่งต่อ delivery เป็น 6 attempts รวมการส่งครั้งแรก 1 และ retry สูงสุด 5 สำหรับ transient failure เช่น `429`/`5xx`/timeout; ครบแล้วหยุด automatic retry และแสดงเหตุที่ต้องตรวจสอบ
- **ทำไม:** จำนวนครั้งมีเพดานตรวจสอบได้ ไม่วนส่งไม่สิ้นสุด และ restart ไม่ทำให้เริ่มนับใหม่
- **ไม่เลือก:** ตีความเป็น retry 6 ครั้งหลังครั้งแรก (รวม 7) หรือรีเซ็ตโควตาเมื่อ pod restart/Kafka redelivery
- **ตัวอย่างข้อมูลสมมติ:** attempt 1 timeout; attempts 2–6 ได้ `503` → หยุดที่ `Failed` พร้อมประวัติ 6 ครั้ง ไม่มี automatic attempt 7; ทุกครั้งใช้ bytes/hash/key เดิมตาม D-44
- **ห้าม:** auto retry `400/409` ตาม D-04/D-05 หรือเพิ่มการส่งซ้อนจาก HttpClient ที่ไม่ถูกนับ; `429` ต้องเคารพ `Retry-After` และหลังครบเพดานไม่เปิด manual retry ตามคำตอบ Q2.6/M-20 ไม่รีเซ็ตจำนวนครั้งเอง

### M-09 ตรึงเวลารับไฟล์ครั้งแรกเมื่อสร้าง File Job
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 6.4 ให้ใช้เวลาที่ Mapping รับไฟล์และสร้าง File Job ครั้งแรกเป็นเวลารับไฟล์ครั้งแรกฝั่ง Mapping; บันทึก UTC พร้อม File Job และ version pin แล้วคงเดิมเมื่อ resume/retry โดยใช้ `FileJob.CreatedAt` ตามคำชี้แจงภายหลังใน M-23 ไม่เพิ่ม field เวลาแยก
- **ทำไม:** มีเวลารับงานที่คงที่สำหรับตรวจย้อนหลังตาม D-23 ไม่เปลี่ยนตามเวลาที่ worker กลับมาทำงานหรือส่ง HTTP ซ้ำ
- **ไม่เลือก:** ใช้เวลาที่ retry/ส่ง HTTP ครั้งล่าสุด หรือเวลาแก้ไขไฟล์ต้นทางแทนเวลารับ File Job เดิม
- **ตัวอย่างข้อมูลสมมติ:** สร้าง File Job A เวลา `2026-11-13T03:00:00Z`; import กลับมาทำต่อเวลา `03:10:00Z` และส่ง HTTP ซ้ำเวลา `03:20:00Z` → เวลารับไฟล์ครั้งแรกของ A ยังเป็น `2026-11-13T03:00:00Z`
- **ข้อจำกัด:** การเก็บเวลานี้ใน Mapping ยังไม่ทำให้ Informs ใช้เวลาเดียวกัน; schema/DTO ปัจจุบันไม่มี field รับเวลานี้ และ `ReceivePaInformCommandHandler` สร้าง `ReceivedAt` จาก `TimeProvider` ของ Informs ต้องตกลงวิธีส่ง/ใช้เวลาใน Q4 ก่อนอ้างว่ารักษา D-23 ข้าม service ได้
- **ห้าม:** เพิ่ม field ใน `pa-inform/1` หรือแก้โค้ด Informs เอง; การรับ occurrence ใหม่/duplicate-content ยังรอ Q6 ไม่เปลี่ยน File Job identity เพื่อรีเซ็ตเวลาโดยปริยาย

### M-10 ชุดข้อมูล delivery log สำหรับช่วงแรก
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 7.1 ให้ delivery log มีชื่อไฟล์/แถว/config version, ยอดทั้งหมด/ส่งสำเร็จ/รอส่ง/ส่งไม่สำเร็จ, สถานะ/HTTP status/error code/path, จำนวน attempts และเวลาส่งแต่ละครั้ง, `informId`/`replayed`/`replaced`; รายการปกติไม่แสดง raw Source/payload
- **ทำไม:** ติดตามแถวที่ Informs รับหรือยังไม่รับได้ตาม D-36 โดยแยกจำนวนผลลัพธ์จากจำนวนครั้งที่ส่ง
- **ไม่เลือก:** ใช้จำนวน HTTP attempts เป็นยอดใบที่ส่งสำเร็จ หรือแสดง raw Source/payload เป็นข้อมูลปกติในรายการ
- **ตัวอย่างข้อมูลสมมติ:** แถวข้อมูล 1 แถวได้ output `default` มี attempt 1 เป็น `503`, attempt 2 เป็น `201` พร้อม `informId` → ยอดส่งสำเร็จ 1, attempts 2; การรับสำเร็จยังไม่หมายถึงผ่าน business validation ตาม D-03
- **ข้อจำกัด:** file summary ต้องแยก import/transform/delivery และรวมแถวที่สร้าง Delivery Job ไม่ได้ในยอดยังไม่ส่งตาม D-36; รายละเอียด DTO/count definitions ปิดใน M1.2 ส่วนผู้ใช้งาน log ในข้อ 7.2, raw inspection แยกและ action scope ใน Q2/Q7 ยังรอคำตอบ
- **ห้าม:** ใส่ PII/raw HTTP response ใน error ที่แสดง/log; ไม่ตีความว่าการตกลงชุดข้อมูลนี้อนุมัติหน้าดู raw หรือระบบสิทธิ์ใหม่แล้ว

### M-11 Delivery log สำหรับทีม Mapping / Dev / Ops
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 7.2 ให้กลุ่มผู้ใช้งาน delivery log ช่วง phase 1 เป็นทีมดูแล Mapping / Dev / Ops โดยใช้ชุดข้อมูล M-10
- **ทำไม:** ให้ทีมดูแลการรับไฟล์/การแปลง/การส่งติดตามและแก้เหตุขัดข้องตาม D-36; ไม่เพิ่ม workflow ของ UW ในหน้า Mapping โดยปริยาย
- **ไม่เลือก:** ขยายกลุ่มผู้ใช้งานที่ต้องออกแบบรองรับให้รวมทีม UW ในช่วงแรก
- **ตัวอย่างการใช้งาน:** Ops เปิดรายการไฟล์ที่ยังส่งไม่สำเร็จ ดูแถว/HTTP status/error code/path และประวัติ attempts แล้วติดตามตาม state/action ที่ตกลง; การดูผล validation ของใบที่รับเข้าแล้วเป็นงานของ Informs/UW ตาม D-06/D-36
- **ห้าม:** ตีความกลุ่มผู้ใช้งานเป็น access control ที่มีอยู่แล้ว; `web.md` กำหนด fixed user ไม่มีสิทธิ์แยกใน phase นี้ และข้อนี้ไม่อนุมัติ raw Source/payload inspection หรือ action เพิ่มเติม

### M-12 หน้า Mapping ช่วงแรกมี Config ผลการส่ง และ retry
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 8 ให้หน้า Mapping ช่วงแรกครอบคลุม Config (สร้าง version, validate/preview, activate/disable), ผลการส่ง (ไฟล์/แถว/attempts ตาม M-10/M-11) และ retry ตามสถานะที่อนุญาต
- **ทำไม:** ทีมดูแล Mapping จัดการ config และติดตามผลการส่งจากหน้า Web ได้ตาม D-78 โดยใช้ API ที่ตรวจรับแยกได้ก่อนมี design
- **ไม่เลือก:** ทำเพียงหน้าผลการส่ง/retry แล้วให้จัดการ config นอก Web ชั่วคราว
- **ตัวอย่างการใช้งาน:** ผู้ใช้สร้าง config version ใหม่ → validate/preview → activate; งานที่รับก่อน activate ยังอ้าง version เดิม แล้วผู้ใช้ดูผลการส่งรายไฟล์/แถว/attempt และเลือก retry ได้เมื่อผ่าน state guard และโควตาที่ตกลง
- **ข้อจำกัด:** layout/interaction และรายการหน้าจอย่อยรอ Web design; ครบ 6 attempts ไม่เปิด retry ตาม M-20 ส่วน Source/Payload read-only detail เลือกแล้วตาม M-18/M-19 และ changed-payload reprocess/business resend ปิดช่วงแรกตาม M-22
- **ห้าม:** แก้ส่วน UW/Config ของ Dev 1 นอกขอบเขต Mapping หรือเพิ่มระบบสิทธิ์นอก `web.md`; ก่อนแก้ source ต้องแจ้งรายการไฟล์และรอยืนยันตาม AGENTS.md

### M-13 ใช้ Kafka และ transactional outbox สำหรับงานภายใน Mapping
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 9.1 ใช้ Kafka + transactional outbox สำหรับงานภายใน Mapping โดยวาง hosted services/dispatcher/consumer ตาม pattern Informs และโครง D-77; การส่ง Mapping → Informs ใช้ HTTP ตาม D-03
- **ทำไม:** งานที่รับและบันทึกแล้วกลับมาประมวลผลต่อได้เมื่อ restart และมี transaction boundary ระหว่าง job กับ event ที่ตรวจสอบได้
- **ไม่เลือก:** ใช้ DB queue เป็นแนวทางหลักแทน Kafka ในช่วงแรก
- **ตัวอย่างข้อมูลสมมติ:** transaction บันทึก Row Job A และ outbox ที่มี job ID A พร้อมกัน → dispatcher publish → consumer อ่านงานจาก ID; หาก event ถูกส่งซ้ำ consumer ใช้ identity/state guard เดิม ไม่สร้าง Source/output ซ้ำ
- **ข้อจำกัด:** PostgreSQL เก็บ job/outbox แบบ atomic; event มีเฉพาะ job IDs/ชนิดงาน ไม่มี PII และ consumer ต้องรองรับ redelivery; broker/topics/groups/owner และวิธีเตรียม local ยังรอข้อ 9.2/Q7
- **ห้าม:** เปลี่ยน Mapping → Informs เป็น Kafka, แก้ repo/Compose ของ Informs เอง, อ้างว่าเลือก Kafka แล้วเท่ากับ provision infrastructure หรือผ่าน integration tests แล้ว; source/test/script/Compose ต้องแจ้งรายการไฟล์และรอยืนยันก่อนแก้ตามเดิม

### M-14 เริ่มพัฒนาและทดสอบด้วย Kafka local ผ่าน Docker
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 9.2 ใช้ Kafka local ผ่าน Docker สำหรับพัฒนา/ทดสอบ Mapping ระหว่างรอรายละเอียด broker/topics บริษัท; ใช้แนวทาง M-13 และตรวจ pattern ของ Informs ก่อนเพิ่ม config ใน repo Mapping
- **ทำไม:** ทดสอบ transaction/outbox/consumer/redelivery ได้บนเครื่องโดยไม่ต้องรอ infrastructure บริษัท
- **ไม่เลือก:** ผูกการเริ่มพัฒนา/ทดสอบ messaging ไว้กับ broker บริษัทที่ยังไม่มีรายละเอียดให้ใช้
- **ตัวอย่างการใช้งาน:** PostgreSQL local เก็บ Row Job A + outbox → Kafka local รับ event job ID A → Mapping consumer ประมวลผล แล้วทดสอบส่ง event เดิมซ้ำโดย Source/output ไม่เพิ่มซ้ำ; ไม่มีข้อมูลส่วนบุคคลจริงใน fixture/event
- **ข้อจำกัด:** ชื่อ topics/groups, broker image/version/ports และ config local ต้องลงรายละเอียดใน M3.1; broker/topics/groups/owner บริษัทใน Q7 ยังรอข้อมูล และผล local ไม่ใช่หลักฐานว่า environment บริษัทพร้อม
- **ห้าม:** แก้ Compose/config ของ Informs หรือ provision broker บริษัทเอง; ก่อนแก้ source/Compose/test/script ใน Mapping ต้องระบุไฟล์และรอยืนยันตาม AGENTS.md การบันทึกการเลือกนี้ยังไม่ใช่การเพิ่มหรือรัน Kafka local แล้ว

### M-15 ใช้ physical Source Table แยกตาม config
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.1 ข้อ 2 ให้ใช้ physical Source Table แยกตาม config ใน schema `mapping`; เก็บค่าต้นทางก่อนแปลง พร้อม Source identity/File Job/Row Number/Physical Line Number ตาม D-07/M-06 และใช้ field catalog/table definition อ้างอิงย้อนหลัง
- **ทำไม:** ผู้ใช้ระบุว่าข้อมูลจากแต่ละเจ้ามีโครงสร้าง field ต่างกัน จึงเลือกให้ Source มีตารางและ columns แยกตาม config พร้อม Source definition ของตัวเอง; 19 columns เป็นรูปแบบของ MTLS ไม่ใช่ข้อจำกัดของทุก config
- **ไม่เลือก:** ข้อเสนอตารางกลาง `mapping.source_rows` ที่เก็บทุก column ต้นทางไว้ใน JSONB เป็นหลัก
- **ตัวอย่างข้อมูลสมมติ:** config A ผูกตาราง `mapping.source_config_a_rows` (ชื่อเพื่ออธิบาย ยังไม่ใช่แบบ naming ฉบับปิด); แถวข้อมูลมี system columns ของ Source และ raw columns เช่น `transaction_type=P`, `effective_date_raw=20261009`; Transformation Program อ่านแถวนี้แล้วสร้าง `inform.transactionType=NEW`, `inform.effectiveDate=2026-10-09` โดยไม่แก้ค่าใน Source
- **ข้อจำกัด:** M1.1 ต้องปิดชื่อ/ชนิด column, source identity/constraints, สิทธิ์ DDL และการ pin definition ให้ config version/งานเก่า; ผู้ใช้เลือกสร้างผ่าน Web ตาม M-16 และเพิ่ม columns ได้อย่างเดียวตาม M-17 แล้ว ไม่ถือว่าอนุมัติ Table verification/impact admin ทั้งชุดของ demo
- **ห้าม:** update/delete Source เดิมตาม D-07, ใช้ตาราง Source เป็น Normalized Table แทน direct JSON ที่เลือกใน Q1 ข้อ 1, สร้างตารางนอก `mapping` หรือรับ arbitrary SQL จาก browser; การเพิ่มตาราง/DDL ใน DB บริษัทให้คนรันตาม AGENTS.md

### M-16 กำหนด fields และสร้าง Source Table ผ่านหน้า Mapping
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.2 ข้อ 1 ให้กำหนด fields ผ่านหน้า Mapping แล้วระบบตรวจ definition และสร้าง physical Source Table ใน schema `mapping` พร้อม table/field metadata; ใช้ร่วมกับ M-15 โดยทำส่วน MTLS ก่อน
- **ทำไม:** แต่ละเจ้ามี fields ต่างกัน ผู้ใช้จึงตั้ง Source definition สำหรับรูปแบบไฟล์ที่ระบบรองรับได้ผ่านหน้า Mapping
- **ไม่เลือก:** ให้ Dev เตรียม migration/script ต่อ Source Table ทุกครั้งแล้วหน้า Mapping เลือกใช้ได้อย่างเดียว
- **ตัวอย่างข้อมูลสมมติ:** ผู้ใช้กำหนด Source definition A พร้อม fields `transaction_type`, `effective_date_raw` → ระบบตรวจชื่อ/ชนิดและ system columns → สร้าง metadata และ `mapping.source_config_a_rows` (ชื่อเพื่ออธิบาย) → config อ้าง Source definition นี้ ก่อนนำเข้าแถว `P`, `20261009` แล้วแปลงเป็น JSON ตาม M-15
- **ข้อจำกัด:** M1.1/M1.5 ต้องปิด allowed names/types, metadata/DDL consistency, concurrent creation/column addition, audit และ permissions โดยรักษาค่าดิบที่แปลงธุรกิจไม่ได้ตาม D-04; schema evolution ใช้ M-17 และ layout/interaction ยังรอ Web design
- **ห้าม:** รับ arbitrary SQL จาก browser, สร้างตารางนอก `mapping`, ให้ user-defined fields ชน system columns, แก้ Source เดิมตาม D-07 หรือเพิ่มระบบสิทธิ์นอก `web.md`; การเลือกนี้ยังไม่ใช่การแก้ source/รัน DDL ในงานนี้

### M-17 Source Table เพิ่ม column ได้อย่างเดียว
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.3 ข้อ 1 ให้เพิ่ม column ใน Source Table เดิมได้ แต่ห้ามลบ/rename/เปลี่ยนชนิด field เดิม; ถ้าต้องเปลี่ยนแบบนั้นให้สร้าง Source Table ใหม่และให้ config version ใหม่อ้าง definition/table ใหม่ โดยงานเก่ายังใช้ definition/table เดิม
- **ทำไม:** ปรับรับ fields เพิ่มได้โดยคงหลักฐาน Source และการอ่านย้อนหลังตาม D-07 ไม่บังคับย้ายข้อมูลเก่าเมื่อเพิ่ม field
- **ไม่เลือก:** สร้างตารางใหม่ทุกครั้งที่เพิ่ม field หรือแก้โครง field เดิมในตารางที่มีหลักฐานแล้ว
- **ตัวอย่างข้อมูลสมมติ:** definition v1 มี `field_a`, `field_b` → เพิ่ม `field_c` เป็น definition v2 ใน physical table เดิม; Source เก่าไม่ถูกเติมค่าธุรกิจย้อนหลัง และ config/job ที่ pin v1 ยังอ่านตาม v1 หากต้อง rename `field_b` ให้สร้าง table/definition ใหม่แทนการ rename ของเดิม
- **ข้อจำกัด:** M1.1/M1.5 ต้องปิด nullable/default policy ของ column ใหม่ที่ไม่เปลี่ยนค่าหลักฐานเดิม, definition version/snapshot pin, concurrency และ metadata/DDL consistency; การเปลี่ยน Source table ไม่ทำ reprocess/resend งานเก่าโดยอัตโนมัติ และ changed-payload reprocess ปิดช่วงแรกตาม M-22
- **ห้าม:** ลบ/rename/เปลี่ยนชนิด field เดิม, update/delete/backfill ค่าธุรกิจใน Source เก่า หรือเปลี่ยน definition ที่ config/job เก่าอ้างตาม D-07; การเพิ่ม column ต้องผ่าน validation/audit และอยู่ใน schema `mapping`

### M-18 มี Source view รายแถวแบบ read-only
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.4 ข้อ 1 ให้เปิดดู Source ดิบรายแถวแบบ read-only จากรายการไฟล์/แถว พร้อม config version ที่งานอ้าง, Row Number/Physical Line Number และสถานะส่ง; ใช้โดยทีม Mapping / Dev / Ops ตาม M-11
- **ทำไม:** ผู้ใช้ต้องการตรวจว่าข้อมูลเข้าครบและเทียบข้อมูลต่างกันได้; Source คือค่าที่อ่านจากไฟล์ก่อนแปลง ส่วนหน้า inform ของ Informs ดูใบที่รับเข้าแล้ว แถวที่สร้าง payload ไม่ได้หรือส่งไม่ผ่านจึงต้องตรวจฝั่ง Mapping ตาม D-07/D-36
- **ไม่เลือก:** ดูเฉพาะ metadata/error โดยไม่มีช่องทางเปิด Source ดิบ หรือทำหน้าตรวจ/แก้ข้อมูลใบซ้ำกับ Informs/UW
- **ตัวอย่างข้อมูลสมมติ:** import จบแล้วอ่านพบ 10 แถวข้อมูลและบันทึก Source 10 แถว; ส่งสำเร็จ 8 รอส่ง 1 และสร้าง payload ไม่ได้ 1 → Source view เปิดดูได้ครบ 10 แถว ส่วนยอดส่งแยกเหตุและไม่นับ attempts ซ้ำ แถววันที่ดิบ `-293` ยังดูค่าเดิมได้ตาม D-04
- **ข้อจำกัด:** รายการ log ปกติยังใช้ metadata ตาม M-10; Source detail อ่านตาม table/definition/config version ที่ pin ไม่อิง definition ล่าสุดหรือ table name จาก browser; count definitions/import completeness ปิดใน M1.2 และ layout/interaction รอ Web design การดู Delivery Payload แยกใช้ M-19 ตามคำตอบ Q2.5
- **ห้าม:** update/delete Source, reprocess/send จากการเปิดดู หรือส่ง raw values ไป application logs/Kafka events; ไม่เพิ่ม workflow ของ UW หรือระบบสิทธิ์นอก `web.md` การเปิดดู Source ไม่เปลี่ยน original ใน Informs

### M-19 ดู Delivery Payload ที่บันทึกจริงแบบ read-only
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.5 ข้อ 1 ให้เปิดดู JSON Delivery Payload ของ delivery แบบ read-only เพื่อเทียบกับ Source รวม payload ของ delivery ที่ Informs reject; ใช้ durable snapshot/bytes ที่บันทึกจริงตาม M4.1 พร้อม lineage/config version/hash ของ delivery นั้น
- **ทำไม:** ทีม Mapping / Dev / Ops ต้องตรวจค่าก่อน/หลังแปลงและสิ่งที่เตรียมส่งจริงได้โดยไม่พึ่งข้อมูลใบปัจจุบันใน Informs ซึ่งอาจยังไม่รับแถวนั้น
- **ไม่เลือก:** ดูได้เพียง Source/metadata หรือ compile ด้วย config ปัจจุบันแล้วแสดงผลแทน payload ที่บันทึกจริง
- **ตัวอย่างข้อมูลสมมติ:** Source มีวันเริ่ม `-293` → payload snapshot มี `inform.effectiveDate=null` และ `inform.extras.unparsed.effectiveDate=-293`; เปิดดู snapshot เดียวกันได้เมื่อ delivery ถูก reject โดยไม่เปลี่ยน hash/key หรือส่งใหม่
- **ข้อจำกัด:** อ่าน/decrypt snapshot ด้วย server-side key reference ตามแบบ M4.1/Q7 และไม่คืน secret/key ให้ browser; หากยังไม่มี payload หรืออ่าน snapshot ไม่ได้ให้แสดง availability/diagnostic ตามจริง ไม่สร้าง payload ทดแทน; DTO/ขนาด response/availability ปิดใน M1.2 และ layout รอ Web design
- **ห้าม:** แก้ bytes/hash/key/job state, สร้าง delivery/reprocess/resend หรือเรียก Informs จากการเปิดดู; ไม่ใส่ raw payload ในรายการ log ปกติ/application logs/Kafka events ตาม M-10 การดู payload ไม่ใช่การเปิดแก้ข้อมูลใบของ Informs

### M-20 ครบ 6 attempts แล้วไม่เปิด retry เพิ่ม
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.6 ข้อ 2 ให้หยุดที่ 6 attempts รวมครั้งแรกตาม M-08 และไม่เปิด retry หลังครบเพดานผ่าน Web; API/action guard และ worker ใช้เพดานเดียวกันสำหรับ delivery เดิม
- **ทำไม:** ผู้ใช้เลือกคงเพดานเดิมโดยไม่เพิ่มโควตา manual retry หลังครบ และประวัติ/ยอดส่งตรวจสอบได้ตรงกัน
- **ไม่เลือก:** manual retry เพิ่มครั้งละ 1 attempt หลังครบ 6 หรือเริ่ม automatic retry รอบใหม่ด้วยการรีเซ็ตจำนวนครั้ง
- **ตัวอย่างข้อมูลสมมติ:** attempt 6 ได้ `503` → delivery เป็น `Failed` พร้อมประวัติ 6 ครั้ง; Web แสดงเหตุครบเพดานและไม่มี retry ที่ใช้งานได้; เรียก retry API โดยตรงก็ถูกปฏิเสธตาม state/limit โดยไม่สร้าง attempt 7/outbox/HTTP
- **ข้อจำกัด:** manual retry ก่อนครบเพดานยังต้องผ่าน state guard/นับรวมใน 6 attempts และไม่ใช้กับ permanent rejection ตาม contract; เปิด Source/Payload detail และ attempt history เพื่อตรวจสอบได้ตาม M-18/M-19 ส่วน changed-payload reprocess/business resend ปิดช่วงแรกตาม M-22
- **ห้าม:** รีเซ็ต counter/ลบประวัติ/เปลี่ยน bytes/key หรือสร้าง delivery identity ใหม่จาก retry เพื่อหลบเพดาน; restart/redelivery/concurrent action ต้องไม่ทำให้ส่งเกินเพดาน

### M-21 ช่วงแรกส่งเฉพาะ Informs API
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.7 ข้อ 1 ทำปลายทาง Informs API ก่อน โดยเก็บ LegacyTable/JsonFile/DatabaseTable เป็น backlog; Mapping สร้าง JSON `pa-inform/1` และส่ง HTTP ทีละใบตาม D-02/D-03
- **ทำไม:** ให้เส้นทาง MTLS → Informs ทำงานก่อนตาม D-70 โดยไม่เพิ่ม destination runtime ที่ไม่จำเป็นต่อเส้นทางนี้
- **ไม่เลือก:** ทำ destination อื่นพร้อม Informs API ในช่วงแรกหรือถือว่าการย้าย Transformation Program ทั้งหมดใน M-03 รวมทุก destination แล้ว
- **ตัวอย่างการใช้งาน:** อ่านแถว MTLS → เก็บ Source → สร้าง durable Delivery Payload → ส่ง `POST /api/pa/informs`; ผู้ใช้ยัง preview/เปิดดู JSON ตาม M-19 ได้ โดยการเปิดดูไม่ใช่ JsonFile destination หรือการเขียน Normalized Table
- **ข้อจำกัด:** เก็บ requirement/feature inventory ของ destination อื่นไว้ใน backlog ไม่ลบ; capability catalog ระบุพร้อมใช้งานเฉพาะสิ่งที่ implement/ทดสอบแล้ว ขอบเขต Source Table ผ่าน Web ตาม M-15–M-17 ยังคงทำตามที่ตกลง
- **ห้าม:** เขียนตาราง Informs ตรงตาม D-02, เพิ่ม runtime/finalizer/DB writer ของ destination อื่นโดยตีความจากคำว่า engine ทั้งหมด หรือเปลี่ยน contract/วันส่งงานเอง

### M-22 ยังไม่เปิด changed-payload reprocess/business resend ช่วงแรก
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q3 ข้อ 1 ช่วงแรกยังไม่ส่ง payload ใหม่จาก Source แถวเดิมหลังเปลี่ยน config; config version ใหม่ใช้กับไฟล์ที่รับใหม่ ส่วนงานที่รับแล้วคง version/schema/profile/identity ที่ pin ไว้ และ delivery retry ใช้ bytes/hash/key เดิมภายในเพดาน M-08/M-20
- **ทำไม:** D-44 ใช้ `(fileJobId, sourceRowId, outputKey)` โดยไม่รวม config version; key เดิมของงานที่ Informs รับแล้วจะ replay ผลเดิม การเปลี่ยน key อาจทำให้ข้อมูลเก่าทับข้อมูลแก้ไขภายหลัง จึงเลื่อน feature ที่ยังไม่มี contract/lineage รองรับ
- **ไม่เลือก:** เปิด changed-payload reprocess/resend หรือเพิ่ม key/config version เข้า idempotency identity เองในช่วงแรก
- **ตัวอย่างข้อมูลสมมติ:** File Job A รับด้วย config v1 และส่งสำเร็จ → activate v2 → retry A ยังใช้ v1 และ bytes/key เดิม ไม่ส่งค่าจาก v2; File Job B ที่รับหลัง activate ใช้ v2 ตาม version pin ปกติ ทั้งนี้ file occurrence/duplicate-content policy ยังต้องเป็นไปตาม Q6
- **ขอบเขต:** ไม่เปิด changed-payload reprocess สำหรับแถวเดิมทั้ง never-delivered/rejected/Delivered ในช่วงแรก; preview แบบไม่ส่งจริงและ retry/recovery ที่คง version/identity เดิมยังทำได้ตาม state guard ไม่สร้าง delivery ใหม่เพื่อหลบเพดาน
- **ห้าม:** ใช้การเปลี่ยน config/Source Table หรือกด retry เพื่อเปลี่ยน payload/key ของ delivery เดิม, ส่งซ้ำงานที่ Delivered เพื่อแทนข้อมูลเดิม หรือข้ามเพดาน 6 attempts; feature/กติกา key-lineage ในอนาคตเก็บเป็น backlog และต้องตกลง Dev 1 ก่อนเปิด

### M-23 ใช้ FileJob.CreatedAt แทน field เวลารับไฟล์แยก
- **ตัดสิน (2026-11-13):** ผู้ใช้ชี้แจง Q4 ว่าใช้เวลา `CreatedAt` และเลือกว่าหมายถึง File Job ฝั่ง Mapping; ใช้ `CreatedAt` (`created_at` ใน DB) เป็นเวลารับ File Job ครั้งแรกตาม M-09 ไม่เพิ่ม column เวลารับไฟล์อีกชุด
- **ทำไม:** ใช้เวลาสร้างงานที่ต้องมีอยู่แล้ว โดยไม่เก็บเวลาความหมายเดียวกันซ้ำและไม่เปลี่ยนเมื่อ resume/retry
- **ไม่เลือก:** เพิ่ม field เวลารับไฟล์แยกใน File Job หรือใช้เวลาที่ Informs รับ HTTP แทนเวลาของ File Job
- **ตัวอย่างข้อมูลสมมติ:** File Job A มี `created_at=2026-11-13T03:00:00Z`; resume เวลา `03:10:00Z` และ retry เวลา `03:20:00Z` → เวลาอ้างอิงของไฟล์ยังอ่านจาก `created_at=2026-11-13T03:00:00Z` เดิม
- **ข้อจำกัด:** M1.1 ต้องตรึง CreatedAt ของ File Job พร้อม version pin และไม่ reset เมื่อกู้คืนงาน; การส่ง/ใช้ค่านี้ข้าม service ยังรอ Dev 1 ใน Q4 เพราะ contract ของ Informs ไม่มี field รับเวลา และ Informs ตั้ง ReceivedAt จากเวลา intake ปัจจุบัน
- **ห้าม:** เปลี่ยน CreatedAt ของ File Job เดิมเพื่อเปลี่ยนลำดับตาม D-23, อ้างว่า CreatedAt ฝั่ง Mapping ถูกส่งให้ Informs แล้ว หรือเพิ่ม contract field/แก้ Informs เอง; คำตอบนี้ไม่ใช่การเลือกเวลา CreatedAt/ReceivedAt ฝั่ง Informs

### M-24 ออกแบบกลไกกลางและรับ business fields ภายหลัง
- **ตัดสิน (2026-11-13):** ผู้ใช้ให้ข้ามประเด็น field/สัดส่วนใน Q5 ไว้ก่อน จะส่ง fields มาให้ภายหลัง และให้ยึดหลักครอบคลุมหลาย scenarios โดยไม่เฉพาะเจาะจง; engine ใช้ Source definition/Input Layout/Transformation Program/contract schema แบบมี version แทนการ hardcode business fields ของ MTLS
- **ทำไม:** แต่ละเจ้ามี fields ต่างกัน แต่การรับไฟล์/รักษาหลักฐาน/แปลง nested data/ส่ง/ตรวจสอบ/retry/recovery ใช้กลไกเดียวกันได้
- **ไม่เลือก:** หยุดการออกแบบกลางเพื่อถาม business field ทีละรายการต่อ หรือเดา field/default/สัดส่วนที่ยังไม่ได้รับ
- **ตัวอย่างข้อมูลสมมติ:** definition A มี `field_a`, `field_b`; เพิ่ม `field_c` ด้วย definition version ใหม่แล้ว compiler อ่าน catalog/version ที่ config pin ไม่แก้ C# เพื่อรู้จักชื่อ field ใหม่; target bindings ของ Informs ใส่เมื่อได้รับ fields/schema ที่เจ้าของรับรอง
- **ข้อจำกัด:** [ร่างข้อมูลและ runtime](data-runtime-design.md) เป็น draft/test obligations ไม่ใช่ผลทดสอบหรือ DDL/API ฉบับปิด; MTLS ยังทำก่อน, destination ช่วงแรกยังเป็น Informs API ตาม M-21 และ engine ทั้งหมดตาม M-03 ไม่ขยาย partner/destination/contract โดยอัตโนมัติ
- **ห้าม:** อ้างว่า cover ทุกกรณีโดยไม่มี scenario/หลักฐาน, แต่ง business rules/contract fields, เปิด capability ที่ dependency ยังไม่พร้อม หรือข้ามการยืนยันรายการไฟล์ก่อนแก้ source

### M-25 กันเนื้อหาไฟล์ซ้ำต่อ config ข้าม versions
- **ตัดสิน (2026-11-16):** ผู้ใช้ตอบตกลง Q6 ให้กันไฟล์เนื้อหาเดิมด้วย content hash ภายใต้ config เดิมข้าม config versions โดยไม่ใช้ชื่อไฟล์เป็นตัวตัดสิน; เนื้อหาเปลี่ยนรับเป็น File Job ใหม่และอ่านทุกแถว คนละ config ไม่อยู่ใน key เดียวกัน
- **ทำไม:** เปลี่ยนชื่อไฟล์ไม่ควรทำให้นำเข้าเนื้อหาเดิมซ้ำ ส่วนไฟล์แก้ไขต้องมี Source/lineage ใหม่ตามการรับปกติ
- **ไม่เลือก:** dedup ตามชื่อไฟล์/config version หรือรับทุกสำเนาชื่อใหม่โดยไม่ตรวจ bytes; ไม่ทำ semantic diff รายแถวแล้วส่งเฉพาะแถวที่เปลี่ยนโดยปริยาย
- **ตัวอย่างข้อมูลสมมติ:** config A/v1 รับ `a.xls` hash H1 → เปลี่ยนชื่อเป็น `b.xls` หรือใช้ A/v2 แต่ hash H1 ยังซ้ำ; แก้ bytes เป็น H2 → รับ File Job ใหม่; config B/hash H1 ไม่ชน claim ของ A การตัดสินใบซ้ำหลังส่งเป็นหน้าที่ Informs ตาม D-05/D-46
- **ข้อกำหนด:** hash = SHA-256 ของ stable snapshot bytes; UQ `(config_id,content_hash)` ใน `file_content_claims` ชี้ canonical File Job; ไม่เพิ่ม version/ชื่อ/ช่วงเวลาใน key และไม่ล้าง claim เพื่อข้ามงานเดิมที่ล้มเหลว ต้อง resume owner เดิม ส่วน path claim แยกตามแบบ §8
- **ห้าม:** ข้าม D-44/เปลี่ยน key เพื่อหลบใบซ้ำ, เปลี่ยน Source เก่า หรือถือว่า hash เปรียบเทียบความหมายใน Excel cells; Dev 1 เป็นผู้แก้สถานะ BR-001 ใน rules ไม่แก้ไฟล์นั้นเอง

### M-26 ปิดแบบ technical ภายใน Mapping ของ M1.1
- **ตัดสิน (2026-11-16):** ใช้ [data-runtime-design.md §8](data-runtime-design.md#8-ข้อกำหนด-technical-สำหรับ-implement-m11) เป็น baseline implement M1.1: raw physical columns เป็น `text NULL`, FK ผ่าน Source registry, immutable definitions/snapshots, content/path claims แยกกัน และ transaction/lease/global budget/attempt guards ตามแบบ
- **ทำไม:** ปิดรายละเอียดที่ implement และทดสอบได้จากคำตอบที่มี โดยไม่ต้องรอ business fields ของ Q5 หรือเปลี่ยน Informs contract
- **ไม่เลือก:** ติ๊ก M1.1 จาก outline อย่างเดียว, ใช้ typed raw columns ที่ทำค่าธุรกิจผิดหลุดก่อนเก็บหลักฐาน หรือถือว่า private process lock คุมหลาย pod ได้
- **ตัวอย่างหลักฐาน:** [29 checks บน PostgreSQL 15.20 local](m1-1-validation.md) ตรวจ hash scope, pin/FKs, Source immutability/เพิ่ม column, CreatedAt, output identity, attempt cap, rollback และ concurrent claims/fencing ผ่าน; proof schema ย่อไม่ใช่ EF model ของ application
- **ข้อจำกัด:** M1.2/M1.3/M1.5 และ full fixture/Kafka/HTTP tests ยังต้องทำ; Q4/Q5/Q7/Q8 และ Web design ติดตามแยก ไม่เปลี่ยน milestone/contract/business semantics หรืออ้าง corporate readiness
- **ห้าม:** ใช้การปิด design แทนการยืนยันรายการไฟล์ก่อนแก้ source หรืออ้างว่าเอกสารถูก commit/deploy แล้ว

## 5. คำถาม / ข้อเสนอ (Dev 1 ↔ Dev 2)

> ทั้งคู่เพิ่มได้ · เลข `Q1`, `Q2`, … ต่อกันไป · ตอบใต้ข้อนั้น · ตกลงแล้วเขียนผลสั้น ๆ + ย้ายไปที่ที่ถูก (`M-xx` ใน §4 หรือ Dev 1 ลง `rules.md` เป็น `D-xx`) แล้วติ๊ก `[x]`
>
> ```markdown
> - [ ] **Q1** (Dev 2, MM-DD) <คำถาม / ข้อเสนอ>
>   - Dev 1 (MM-DD): <คำตอบ>
>   - ผล: <ตกลงอะไร> → M-03 / D-xx
> ```

- [ ] **Q1** (Dev 1, 11-13) คำถาม 9 ข้อสำหรับ kickoff อยู่ใน [rules.md](rules.md) §6 — ตกลงวันที่ 11-16 แล้ว Dev 1 จะลงผลใน `rules.md` ส่วนเรื่องภายใน mapping Dev 2 ลงเป็น `M-xx` ใน §4
  - Dev 2 (11-13): [แผน API §2–§7](api-implementation-plan.md) เสนอ direct nested `ApiContract` + Transformation Program ส่วนที่ MTLS ใช้, versioned constants/defaults, และ hosted workers/outbox ตาม Informs; ทั้งหมดยังเป็นข้อเสนอสำหรับตอบ 9 ข้อ ไม่ใช่ผล kickoff
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 1: เลือก **สร้าง JSON `pa-inform/1` โดยตรงหลังเก็บ immutable Source แล้วส่ง Informs ผ่าน HTTP**; ไม่ใช้ Normalized Table แบบแบนเป็นขั้นกลาง คำตอบนี้ไม่เปลี่ยน contract/เส้นแบ่งงานตาม D-02/D-03/D-07 และไม่ถือว่าตอบเรื่องโครง Source storage ใน Q2 แล้ว; ข้อ 2–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 2: **เอา Transformation Program มาทั้งหมด แต่เน้นให้ MTLS ใช้ได้ก่อน** → M-03; M2.2 ทำ MTLS ก่อน แล้ว M2.5 ย้ายส่วนที่เหลือ ข้อ 3–9 ยังรอคำตอบ; ความสามารถที่ผูก legacy ยังต้องตกลง dependency ไม่ถือว่าเปิด partner/destination อื่นแล้ว
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 3: **ไฟล์ MTLS `.xls` BIFF8 ตามตัวอย่างก่อน**; อ่าน sheet แรก/header 19 columns/cp874 ตาม `mtls-file.md` โดยไม่บังคับ `.xlsx` ในช่วงแรก; ข้อ 4–9 ยังรอคำตอบ และ Q4/Q7 เรื่องลำดับแถว/fixture ยังไม่ปิด
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 4: เลือก **เก็บ default ผู้รับประโยชน์ใน versioned config ของ MTLS ผ่าน Transformation Program** → M-04; ข้อ 5–9 และ Q5 เรื่อง field สัดส่วนยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 5: เลือก **เก็บค่าคงที่ MTLS และกฎ `P → NEW` / `C → CANCELLED` ใน versioned config ผ่าน `literal` / `case`** → M-05; ข้อ 6–9 ยังรอคำตอบ ไม่ถือว่าตอบ fallback ของค่าอื่นใน Q5
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 6.1: เลือก **`sourceRowNo` เป็นลำดับแถวข้อมูลที่อ่านได้ เริ่ม 1 ไม่รวม header/แถวว่าง; retry ใช้เลขเดิม** → M-06; ค่า `nSeq` และ Physical Line Number เก็บแยก ส่วนที่เหลือของข้อ 6 และข้อ 7–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 6.2: เลือก **เริ่มส่ง HTTP ไป Informs พร้อมกันสูงสุด 1 request และปรับผ่าน config ภายหลัง** → M-07; retry/ส่วนที่เหลือของข้อ 6 และข้อ 7–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 6.3: เลือก **6 attempts รวมครั้งแรก** (ครั้งแรก 1 + retry สูงสุด 5) → M-08; retry delay/timeout, กติกา manual retry หลังครบเพดาน และข้อ 7–9 ยังต้องลงรายละเอียด
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 6.4: เลือก **เวลาที่ Mapping รับไฟล์และสร้าง File Job ครั้งแรก แล้วเก็บคงเดิมเมื่อ resume/retry** → M-09; วิธีส่งเวลานี้ให้ Informs ยังรอ Dev 1 ใน Q4 ส่วนข้อ 7–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 7.1: เลือก **ชุดข้อมูล delivery log ที่เสนอ** → M-10; ชื่อไฟล์/แถว/config version, ยอดทั้งหมด/สำเร็จ/รอ/ไม่สำเร็จ, สถานะ/HTTP status/error code/path, attempts/เวลาส่ง, `informId`/`replayed`/`replaced`; รายการปกติไม่แสดง raw Source/payload ส่วนผู้ใช้งานในข้อ 7.2 และข้อ 8–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 7.2: เลือก **ทีมดูแล Mapping / Dev / Ops** เป็นกลุ่มผู้ใช้งาน delivery log ช่วง phase 1 → M-11; ใช้ fixed user ตาม `web.md` โดยไม่เพิ่มระบบสิทธิ์ ข้อ 8–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 8: เลือก **Config + ผลการส่ง + retry ตามสถานะ** → M-12; สร้าง version/validate/preview/activate/disable, ดูไฟล์/แถว/attempts และ retry ตามสถานะ; layout/interaction ยังรอ Web design และข้อ 9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 9.1: เลือก **Kafka + transactional outbox** สำหรับงานภายใน Mapping ตาม pattern Informs → M-13; Mapping → Informs ยังคง HTTP; local broker/topics/groups/owner ยังรอข้อ 9.2/Q7
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 9.2: เลือก **Kafka local ผ่าน Docker ก่อน** ระหว่างรอ broker/topics บริษัท → M-14; แนวทางหลักทั้ง 9 ข้อมีคำตอบผู้ใช้แล้ว แต่รายละเอียด infrastructure ใน Q7, ข้อตกลง D-xx ที่เป็นหน้าที่ Dev 1 และประเด็น Q2–Q8 ยังไม่ถือว่าปิด
- [ ] **Q2** (Dev 2, 11-13) ขอ review feature scope จาก [feature matrix](api-implementation-plan.md#2-ขอบเขตและการเทียบ-feature): เสนอทำ MTLS Excel → Informs API ก่อน; เสนอเลื่อน LegacyTable/JsonFile/DatabaseTable, Delimited/Fixed-width, dynamic Table admin, generic profile/contract admin และ advanced rollout ที่ไม่จำเป็นต่อ MTLS แต่ยังเก็บ backlog ครบ · เสนอ Source Table แบบ fixed `mapping.source_rows` + immutable JSONB values และ Source field catalog แทน physical table ต่อ config · ขอปิดขอบเขต config/job/delivery views รวมถึงการเปิดดู raw Source/payload ตาม D-07/D-36 และวันรับ Web design เพื่อรักษา milestone 12-03
  - คำตอบผู้ใช้ (11-13), Q2.1: เลือก **physical Source Table แยกตาม config** → M-15 แทนข้อเสนอตารางกลาง + JSONB ในข้อความเดิม; วิธีสร้าง/เพิ่ม column ผ่าน Web หรือ migration/script, schema evolution, raw inspection และ feature scope ส่วนที่เหลือยังไม่ปิด
  - คำชี้แจงผู้ใช้ (11-13): ข้อมูลจากแต่ละเจ้ามีโครงสร้าง field ต่างกัน จึงต้องให้แต่ละ config อ้าง Source definition/table ของตัวเอง ไม่ hardcode โครง MTLS 19 columns ให้ทุก config; phase 1 ยังทำ MTLS ก่อนตาม D-01 และยังไม่ถือว่าคำชี้แจงนี้เลือกวิธีสร้างตารางใน Q2.2 แล้ว
  - คำตอบผู้ใช้ (11-13), Q2.2: เลือก **กำหนด fields ผ่านหน้า Mapping แล้วระบบสร้าง Source Table** → M-16/M1.5; ต้องปิดกติกาเปลี่ยน schema ใน Q2.3 และ permissions ใน Q7 ก่อน implement ไม่ถือว่าอนุมัติลบ/rename/เปลี่ยนชนิด field เดิมแล้ว
  - คำตอบผู้ใช้ (11-13), Q2.3: เลือก **เพิ่ม column ในตารางเดิมได้ แต่ห้ามลบ/rename/เปลี่ยนชนิด field เดิม; ถ้าต้องเปลี่ยนแบบนั้นให้สร้างตารางใหม่** → M-17; config/job เก่าอ่าน definition/table เดิมได้ ส่วน Source/payload inspection และ feature scope ที่เหลือยังไม่ปิด
  - คำตอบผู้ใช้ (11-13), Q2.4: เลือก **Source view รายแถวแบบ read-only เปิดจากรายการไฟล์/แถว พร้อม config version และสถานะส่ง** → M-18; ใช้ตรวจต้นทางและยอดความครบถ้วน ส่วน Delivery Payload inspection แยกยังรอ Q2.5 ไม่เพิ่มหน้าจัดการใบแทน Informs/UW
  - คำตอบผู้ใช้ (11-13), Q2.5: เลือก **JSON Delivery Payload inspection แบบ read-only จาก snapshot ที่บันทึกจริง รวมกรณี Informs reject** → M-19; ใช้เทียบกับ Source ไม่ compile ใหม่หรือส่งใหม่ การเปิดดู raw ไม่ได้อนุมัติ reprocess/business resend
  - คำตอบผู้ใช้ (11-13), Q2.6: เลือก **หยุดที่ 6 attempts และไม่เปิด retry หลังครบเพดานผ่าน Web** → M-20; API/worker คุมเพดานเดียวกันและไม่เพิ่มโควตาหรือรีเซ็ตประวัติจาก retry
  - คำตอบผู้ใช้ (11-13), Q2.7: เลือก **ช่วงแรกทำเฉพาะ Informs API และเก็บ LegacyTable/JsonFile/DatabaseTable เป็น backlog** → M-21; ไม่รวม destination อื่นในการย้าย Transformation Program ทั้งหมด และยังไม่ลบ requirement เดิมออกจาก backlog
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 8: ขอบเขต Web ช่วงแรกเป็น Config + ผลการส่ง + retry ตามสถานะ ตาม M-12; ยังรอ design/วันส่ง design, กติกา retry หลังครบเพดาน, raw inspection และ feature scope/Source storage ส่วนอื่น จึงยังไม่ปิด Q2 ทั้งข้อ
  - Dev 2 (11-13): ผู้ใช้เลือกเพดานรวม 6 attempts ใน M-08 แล้ว; ขอปิดกติกา manual retry หลังครบเพดานว่าจะอนุญาตเพิ่มโควตาอย่างไร พร้อม audit/reason และใช้ bytes/key เดิม โดยไม่รีเซ็ตประวัติหรือถือว่าอนุมัติ quota เพิ่มแล้ว
- [x] **Q3** (Dev 2, 11-13) Reprocess ด้วย config ใหม่/ส่งใหม่หลังแก้ mapping จะใช้ identity อย่างไร? D-44 key `(fileJobId, sourceRowId, outputKey)` ไม่รวม config version: output ที่เคย Delivered ใช้ key เดิมจะ replay ผลเก่า ส่วน key ใหม่อาจทำให้ข้อมูลเก่าทับฉบับแก้ภายหลัง · เสนอเปิด normal delivery/retry bytes/key เดิมก่อน และปิด changed-payload reprocess/business resend จนตกลง contract/lineage ชัดเจน; แยกคำตอบสำหรับ rejected 400/409, never-delivered และ Delivered
  - คำตอบผู้ใช้ (11-13): เลือก **ยังไม่เปิด changed-payload reprocess/resend ช่วงแรก**; config ใหม่ใช้กับไฟล์ที่รับใหม่ ส่วน retry งานเดิมใช้ bytes/key เดิมภายใน 6 attempts → M-22
  - ผล: ปิดคำถามสำหรับขอบเขตช่วงแรกด้วยการเลื่อน feature ทั้ง never-delivered/rejected/Delivered ไม่ได้อนุมัติสูตร key/lineage สำหรับ resend ในอนาคต; เก็บประเด็น contract นั้นไว้ใน backlog ให้ตกลง Dev 1 ก่อนเปิด
- [ ] **Q4** (Dev 2, 11-13) ต่อ Q1 ข้อ 6: `sourceRowNo` ใช้ `nSeq` หรือ import record order? ขอระบุกรณี header/blank row, nSeq ว่าง/ซ้ำ/ไม่เรียง/มี gap และเวลารับไฟล์ครั้งแรกที่ต้องคงไว้ตาม D-23 · เก็บ Row Number และ Physical Line Number แยกกันเสมอ ไม่ใช้ลำดับ HTTP arrival
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.1: ใช้ลำดับแถวข้อมูลตาม M-06 ไม่ใช้ `nSeq`; header/แถวว่างไม่นับ, แถวข้อมูลเริ่ม 1, `nSeq` ที่ว่าง/ซ้ำ/ไม่เรียง/มี gap เก็บเป็นค่าดิบตาม Source โดยไม่ใช้จัดลำดับ; retry คงเลขเดิม ยังรอรายละเอียดการเก็บเวลารับไฟล์ครั้งแรกตาม D-23 จึงยังไม่ปิด Q4 ทั้งข้อ
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.4: ฝั่ง Mapping ใช้เวลารับไฟล์และสร้าง File Job ครั้งแรก คงเดิมเมื่อ resume/retry ตาม M-09
  - คำชี้แจงผู้ใช้ (11-13): ไม่เพิ่ม field เวลาแยก ใช้ **CreatedAt ของ File Job ฝั่ง Mapping** → M-23; ยังไม่ใช่การใช้เวลาสร้างใบฝั่ง Informs และไม่ถือว่าอนุมัติการเพิ่ม field ใน contract แล้ว
  - Dev 2 (11-13), หลักฐานเพิ่มเติมจาก Informs `6ab23ad`: `src/Informs/Informs.Api/Contracts/PaInform/pa-inform-1.schema.json` และ `PaInformContract.cs` ไม่มี field เวลารับไฟล์; `src/Informs/Informs.Application/Informs/Commands/ReceivePaInformCommand.cs` ใช้ `_timeProvider.GetUtcNow()` ส่งเข้า `Informs.Create` จึงได้ `ReceivedAt` จากเวลา HTTP intake ไม่ใช่เวลารับ File Job ที่ Mapping เลือก การส่งทีละ request ตาม M-07 ไม่แก้กรณี backlog/หลายไฟล์/retry ข้ามไฟล์
  - ข้อเสนอถึง Dev 1: ตกลงวิธีรับและใช้เวลารับไฟล์ครั้งแรกจาก Mapping สำหรับ D-23 (เช่น field metadata ที่ Dev 1 เพิ่มใน contract พร้อม semantics สำหรับผู้เรียกเดิม) หรือวิธีอื่นที่รักษาการเรียงได้; ยังไม่กำหนดชื่อ field/แก้ schema เอง และยังไม่ปิด Q4 ทั้งข้อ
  - รายละเอียดข้อเสนอหลัง M-23: หากส่งเวลาเป็น metadata ให้ใช้ค่า `FileJob.CreatedAt` เดิม ไม่สร้าง field เวลาเพิ่มในตาราง Mapping; วิธีรับ/ใช้ใน Informs ยังเป็นประเด็นของ Dev 1 จึงยังไม่ปิด Q4 ข้าม service
- [ ] **Q5** (Dev 2, 11-13) พบช่องว่างระหว่างกฎกับ schema ตัวจริงของ Informs (`6ab23ad`): BR-035 ระบุทายาทตามกฎหมาย **100%** แต่ `$defs.beneficiary` มีเพียง `seq/fullName/relationship` และ `additionalProperties: false` — ให้ mapping ส่งแค่ชื่อ default แล้วสัดส่วนเป็นหน้าที่ Informs หรือจะเพิ่ม field โดย Dev 1? อีกกรณี D-04 ให้ส่งค่าผิดเป็น null แต่ schema บังคับ `sourceReference` nonempty/`transactionType` enum; ถ้าต้นทางไม่มีค่าที่ส่งได้ จะให้ ops/UW ติดตามอย่างไร? เสนอเก็บ Source + row error/ยอดยังไม่ส่ง โดยไม่แต่งค่าและไม่เพิ่ม contract field เอง
  - คำสั่งผู้ใช้ (11-13): **ข้ามเรื่อง field/สัดส่วนไว้ก่อน ผู้ใช้จะส่ง fields มาให้เอง; ยึดการออกแบบให้ครอบคลุมหลาย scenarios ไม่เฉพาะเจาะจง** → M-24; ยังไม่เลือกข้อเสนอส่งเฉพาะชื่อ/เพิ่ม field สัดส่วน ไม่ปิด Q5 และไม่เปลี่ยน BR-035 เอง
  - ผลต่อการทำงาน: เดินหน้าร่างกลไก catalog/compiler/schema validation/diagnostics/pinning/lineage/recovery ได้ตาม [ร่างข้อมูลและ runtime](data-runtime-design.md); actual business bindings/defaults และ acceptance ที่ต้องใช้ fields จริงยังรอ input ไม่ตีความว่าพร้อมส่ง payload จริงแล้ว
- [x] **Q6** (Dev 2, 11-13) BR-001 กันไฟล์ซ้ำด้วย content hash ยังเป็น 🔶: ยืนยัน scope ของ hash ว่าเป็นต่อ config/partner/ช่วงเวลา/ทุก version และจัดการไฟล์แก้ไขชื่อเดิม/เนื้อหาเดิมที่ reprocess อย่างไร? แยกจาก DB occurrence claim ที่จำเป็นสำหรับกันหลาย pod รับไฟล์เดียวกัน · ยังไม่ถือว่า demo duplicate policy เป็นกฎที่อนุมัติแล้ว
  - คำตอบผู้ใช้ (11-16): ตกลง **config เดิม/hash เดิมซ้ำข้าม versions แม้เปลี่ยนชื่อ; เนื้อหาเปลี่ยนรับ File Job ใหม่และอ่าน/ส่งทุกแถว**; Informs ตัดสินใบซ้ำตามสถานะตาม D-05/D-46 → M-25
  - ผล: ปิด policy ภายใน Mapping ตาม M-25 และใช้ในแบบ M1.1; ขอ Dev 1 อัปเดต BR-001/ผล kickoff ใน rules ตามเจ้าของเอกสาร ไม่ตีความว่าเปลี่ยนกฎในไฟล์ของ Dev 1 ไปแล้ว
- [ ] **Q7** (Dev 2, 11-13) ขอ Dev 1 ระบุตำแหน่ง/identity ของ **fixture MTLS 2,556 แถวที่รับรองว่าเป็นข้อมูลสมมติ** (ไฟล์ `docs/MTLSPA0220261009.xls` ถูกอธิบายว่าเป็นตัวอย่างจริง จึงไม่ใช้ทดแทน) และ dependency: Kafka/topics/groups/owner, Mapping DB บริษัท, share mount/archive/snapshot access, Informs internal URL/auth/TLS, encryption key provisioning/rotation/retention และผู้ดู Source/delivery log/payload; phase นี้ใช้ fixed actor ตาม web.md ไม่สร้างระบบสิทธิ์จาก header ที่ browser กำหนดเอง
  - Dev 1 (11-16): **fixture พร้อมแล้ว** ที่ [`fixtures/MTLSPA0220261201.xls`](fixtures/README.md) ข้อมูลสมมติทั้งหมด 2,556 แถว (`P` 2,553 / `C` 3 · 1,797 คน · 756 คนมี 2 แถว) สร้างด้วย `fixtures/make-mtls-fixture.py` (seed เดิม = ได้ไฟล์เหมือนเดิมทุก byte) · ⚠️ **ไฟล์จริงเป็น Excel 5.0/95 (BIFF5, stream `Book`) ไม่ใช่ BIFF8** และมี CODEPAGE = 1252 ทั้งที่ข้อความเป็น cp874 ([mtls-file.md](mtls-file.md) §1 แก้แล้ว) — fixture ทำแบบเดียวกัน reader ต้องอ่าน BIFF5 ได้และบังคับ cp874 · `docs/MTLSPA0220261009.xls` เป็นข้อมูลจริง **ห้ามใช้** (ไฟล์อยู่ใน git ของ repo นี้แล้ว — Dev 1 กำลังดูว่าจะเอาออกอย่างไร) · dependency ที่เหลือ (Kafka / DB บริษัท / share mount / URL Informs / key) ตอบตอน kickoff
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 9.1: ใช้ Kafka + transactional outbox ตาม M-13; ยังไม่ได้ระบุ broker/topics/groups/owner ของบริษัท และยังไม่ถือว่ามี Kafka local พร้อมใช้
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 9.2: เริ่ม Kafka local ผ่าน Docker ตาม M-14 เพื่อเดินงานพัฒนา/ทดสอบก่อน; broker/topics/groups/owner บริษัทและ dependency อื่นใน Q7 ยังรอข้อมูล
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 7.2: ผู้ใช้งาน delivery log คือทีม Mapping / Dev / Ops ตาม M-11; คำตอบนี้ปิดเฉพาะกลุ่มผู้ใช้งาน log ไม่ครอบคลุม raw Source/payload inspection หรือ dependency อื่นใน Q7 และไม่เพิ่มระบบสิทธิ์นอก `web.md`
- [ ] **Q8** (Dev 2, 11-13) เสนอ prefix API **`/api/mapping`**, BaseResponse เดิม, field-path errors, expected `rowVersion` → 409, list `items/totalCount/page/pageSize` (default 20/max 100), dates ISO/UTC และ route/action subset ใน [แผน API §5](api-implementation-plan.md#5-api-ที่เสนอก่อนมี-web-design) · ขอ review ก่อน M1.2/เขียน controller; pause intake/delivery และ raw payload inspection ให้เปิดเท่าที่ scope Q1/Q2 ตกลง
  - คำตอบผู้ใช้ (11-16): **ใช้ `/api/mapping`** เป็น prefix; บันทึกใน [ร่าง M1.2](mapping-api-contract.md) แล้ว
  - คำตอบผู้ใช้ (11-16): ขอ **review ภายหลัง**; เดินงานแบบข้อมูล/persistence ภายใน Mapping ได้ตาม M1.1 แต่ยังไม่ถือว่า Dev 1 เห็นชอบ route/DTO และไม่ implement API controller จากร่างนี้ก่อน review
  - ยังรอ: Dev 1 review เฉพาะ route/DTO ที่กระทบ Web/contract boundary; prefix ไม่ใช่การอนุมัติ response/error/action contract ทั้งชุด
