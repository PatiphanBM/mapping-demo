---
title: ศัพท์ — mapping phase 1
status: review
last_updated: 2026-11-13
---

# ศัพท์

> คัดจากศัพท์กลางของโปรเจค Insurance Validation เฉพาะที่งาน mapping ใช้ · **หลายคำความหมายในองค์กรไม่ตรงกับความหมายทั่วไป** ให้อ่าน §1 ก่อน
> ใช้ตามนี้ทั้งในเอกสาร ชื่อ class / field / ตาราง / route · ศัพท์ภายในของ mapping (Source Table, Input Layout, Config Version, Delivery Job, …) อยู่ใน [`CONTEXT.md`](../../CONTEXT.md)
> **เปลี่ยนชื่อ 2026-11-05 (D-66):** product group (`PA`) → **product** (campaign เดิม, `MTLSPA`) → **package** (product เดิม, `MTL2`) · เอกสารที่เขียนก่อนวันนั้น (รวมเอกสาร demo) ให้อ่าน **campaign = product, product = package**

## 1. คำที่มักเข้าใจผิด

| คำ | ความหมายที่ใช้ | ที่มักเข้าใจผิด |
|---|---|---|
| **Inform (ใบคำขอ)** | คำของ MTI = การแจ้งงาน · 1 รายการ = ใบคำขอ 1 ใบ · contract `pa-inform/1`, API `/api/pa/informs` | ❌ ห้ามใช้ `proposal` ในงานใหม่ (เอกสาร demo เก่าใช้คำนี้) |
| **Issue** | **การออกกรมธรรม์ (issue policy) เท่านั้น** — นอก phase 1 | ❌ ไม่ใช่ "ปัญหา" — ผลตรวจเรียก **validation message** |
| **Partner code** | รหัสงานของ partner เช่น `MTLS` (legacy `CUSERGROUP`) — partner เดียวมีหลายรหัสได้ | ❌ ไม่ใช่ channel |
| **Sale channel** | ช่องทางที่ข้อมูลใบคำขอเข้ามา: `FILE` / `MAIL` / `ONLINE` — **mapping เติมเป็นค่าคงที่** (MTLS = `FILE`) | ต้องระบุเสมอว่า channel แบบไหน |
| **Distribution channel (DChannel)** | กลุ่มของตัวแทน เช่น `D14` — หาจาก agent (phase 1 ไม่ใช้) | ไม่อยู่ใน contract |
| **Product** | **ของที่ partner ขาย = campaign เดิม** เช่น `MTLSPA` (legacy `CCAMPAIGN`) · contract ส่ง `productCode` | ❌ ไม่ใช่ PA / Motor (นั่นคือ **product group** แม้ business จะเรียก product) |
| **Package** | **กลุ่มความคุ้มครองที่ขายใน product** เช่น `MTL2` · contract ส่ง `packageCode` · เบี้ย / ทุน / commission อยู่ที่นี่ (ใน config ของ service Products) | ❌ `CMTI_PACKAGE` ของ legacy ไม่ใช่ package (คือรหัสแผน AS400) |
| **Original** | ใบคำขอฉบับทำงานใน Insurance Validation (ตารางของ Informs) — **UW แก้ได้** | ❌ ไม่ใช่หลักฐานว่า partner ส่งอะไรมา — หลักฐาน as-received อยู่ที่ **Source Table ของ mapping** |
| **Insurance Validation** | ระบบใหม่ = service **Products** + **Informs** | ❌ ไม่ใช่ `prjInsuranceValidationMotor` (โปรเจคต้นแบบเก่า) |
| **Risk** | สิ่งที่เอาประกัน `PERSON` / `VEHICLE` / `PROPERTY` — อยู่ที่ product group (config) | ❌ ไม่ใช่ **risk type** (อีกเรื่อง ยังไม่มีนิยาม — ห้ามเดา) |
| **risk ของใบ** | ผู้เอาประกัน 1 คนในใบคำขอ = 1 รายการใน `insureds[]` ของ contract (มี `packageCode` + `beneficiaries[]` ของตัวเอง — D-75) · MTLS ส่ง 1 คน | ❌ ไม่ใช่ Risk (`PERSON`) และไม่ใช่ **PackageRisk** (config เบี้ย / ความคุ้มครองของ package ใน service Products) — ห้ามเขียนคำว่า "risk" เฉย ๆ |

## 2. ระบบ

| คำ | ความหมาย |
|---|---|
| **Mapping** (service) | repo `prjInsuranceValidationMapping` — อ่านไฟล์ partner → JSON `pa-inform/1` → ส่งเข้า Informs · schema `mapping` · port 5177 (D-77) |
| **mapping-demo** | demo ของ Dev 2 (repo นี้) — reference ของ service Mapping |
| **Informs** (service) | รับใบคำขอ (`POST /api/pa/informs`) + ตรวจ 3 stage + API ของหน้า UW · schema `non_motor` |
| **Products** (service) | product config + เมนู Config · schema `products` · mapping **ไม่เรียก** service นี้ |
| **Client API** | ทางเดียวไป MCIS: แยกชื่อ / คำนำหน้า / ที่อยู่, หารหัสพื้นที่, อาชีพ, blacklist — Informs เรียก **mapping ไม่เรียก** |
| **MCIS** | ระบบทะเบียนลูกค้าของ MTI |
| **AS400** | core system ที่ออกกรมธรรม์ |
| **CRC0001** | โปรแกรม legacy (Delphi) ที่รับไฟล์ partner ~50 ราย — ระบบที่จะถูกแทนทีละ partner |
| **Strangler-fig** | ย้ายจาก legacy ทีละ partner — partner ที่ยังไม่ย้ายรันบน legacy ต่อ |
| **UW** | underwriter — ผู้แก้ใบที่ไม่ผ่านในหน้าเว็บ (ไม่ได้แก้ config) |
| **Dev / DevSupport** | ผู้ดูแล config (เมนู Config ของ product และหน้าเว็บ mapping) |

## 3. การส่งข้อมูล (mapping → Informs)

| คำ | ความหมาย |
|---|---|
| **Inform contract (`pa-inform/1`)** | JSON Schema ที่ Informs ประกาศ — mapping map ข้อมูลไปที่ path ใน contract · ไฟล์ตัวจริงอยู่ในโค้ด Informs `src/Informs/Informs.Api/Contracts/PaInform/pa-inform-1.schema.json` |
| **`sourceReference`** | เลขอ้างอิงใบคำขอของ partner ใช้กันใบซ้ำ (MTLS = หมายเลข PA `PF` + 6 หลัก) |
| **`sourceRowNo`** | ลำดับแถวในไฟล์ที่ mapping ส่งมาด้วย (ใช้ชี้กลับหลักฐาน) |
| **Idempotency key** | `(fileJobId, sourceRowId, outputKey)` — ส่งซ้ำด้วย key เดิมได้ผลเดิม (`200` + `replayed: true`) · Informs จำทุก key ที่เคยรับ (D-44) |
| **Replace (แทนที่)** | ส่ง `sourceReference` เดิมขณะใบเดิมยังไม่ผ่าน → Informs แทนที่ด้วยข้อมูลใหม่ (`200` + `replaced: true`) — เป็นทางแก้ข้อมูลผิดของระบบใหม่ (D-05) |
| **Delivery log** | บันทึกของ mapping ต่อแถว: payload ที่ส่ง, ผล Delivered / Rejected, เหตุผล — ยอดแถวที่ส่งไม่สำเร็จดูที่ mapping |
| **`extras`** | ที่เก็บค่าที่ contract ยังไม่มี field · `extras.unparsed` = ค่าดิบจากไฟล์ที่ mapping แปลงชนิดไม่ได้ (ส่ง field นั้นเป็น `null`) — D-04 |
| **Outbox** | ตารางที่เขียนใน transaction เดียวกับข้อมูล แล้วค่อยส่งขึ้น Kafka — กันข้อมูลกับ message หลุดกัน |

## 4. ใบคำขอและผลตรวจ (ฝั่ง Informs — mapping ควรรู้)

| คำ | ความหมาย |
|---|---|
| **Role (บทบาท)** | หน้าที่ในกรมธรรม์: `INSURED` ผู้เอาประกัน / `POLICYHOLDER` ผู้ถือกรมธรรม์ — 1 คนมีได้หลาย role (MTLS: ผู้เอาประกัน = ผู้ถือกรมธรรม์) |
| **Relation (ความสัมพันธ์)** | ความเกี่ยวข้องกับผู้เอาประกันหลัก: `PRIMARY` (ตัวผู้เอาประกันหลัก) / `SPOUSE` / `CHILD` — ❌ ไม่ใช่ role · MTLS ส่งคนเดียว = `PRIMARY` |
| **`*_text` / `*_id`** | ค่ากำกวมเก็บคู่: ข้อความตามที่ partner ส่ง + รหัส master ที่ UW เลือก · ใน contract ส่งชื่อไม่มี suffix (เช่น `occupation`) เป็นข้อความ |
| **Validation message** | ผลตรวจ 1 ข้อ: `code`, `severity` (`ERROR` / `WARNING`), `path`, `message` |
| **Status ของใบ** | `RECEIVED` / `PASSED` / `EXCEPTION` / `TECHNICAL_HOLD` / `CANCELLED` |
| **Cancelled** | MTLS แถว `C` = ออกเลขหน้าร้านแล้วลูกค้ายกเลิก → เก็บเป็น record ไม่ตรวจ (BR-010) |
| **ExceptionCase** | ใบที่สถานะ `EXCEPTION` / `TECHNICAL_HOLD` = งานของ UW (ไม่ใช่ตารางแยก) |

## 5. Product config (ที่ mapping อ้างถึง)

| คำ | ความหมาย |
|---|---|
| **Product group** | `PA` / `HEALTH` / `MOTOR` — phase 1 = `PA` |
| **Contract type** | ประเภทสัญญา เช่น `PAC` — อยู่ระหว่าง product group กับ product |
| **Product → package** | คู่ที่ใช้หา package: `MTLSPA` + `MTL2` — mapping ใส่เป็นค่าคงที่ของ partner (BR-025) |
| **Plan code** | รหัสแผน AS400 — อยู่ใน config ของ package **ไม่ใช่งาน mapping** |

## 6. คนและรหัสของฝั่งขาย

| คำ | ความหมาย |
|---|---|
| **Agent number** | รหัสตัวแทนของ MTI เช่น `74000490` (ค่าคงที่ของ MTLS) |
| **SaleId** | รหัสพนักงานขายฝั่ง partner ที่บาง partner ส่งมาแทน agent (MTLS ไม่ส่ง) |
