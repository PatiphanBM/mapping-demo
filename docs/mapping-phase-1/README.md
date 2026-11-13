---
title: mapping phase 1 — อ่านก่อน
status: review
last_updated: 2026-11-13
---

# mapping phase 1

เอกสารของงาน mapping สำหรับ UAT 2026-12-15 · กฎการเขียนเอกสาร / ใครแก้ไฟล์ไหน อยู่ใน [`AGENTS.md`](../../AGENTS.md) ที่ root

## ทำไมเอกสารอยู่ที่นี่

เอกสารกลางของโปรเจค Insurance Validation อยู่ใน repo `insurance-validation` ซึ่ง Dev 2 เข้าไม่ได้ · Dev 1 จึงคัดเฉพาะส่วนที่งาน mapping ต้องใช้มาไว้ที่นี่ — **ต้นฉบับยังเป็นเอกสารกลาง** เมื่อเอกสารกลางเปลี่ยน Dev 1 จะอัปเดตไฟล์ที่นี่ให้ตรง

## ใครทำอะไร

| คน | หน้าที่ |
|---|---|
| **Dev 1** (ผู้ดูแล Insurance Validation: repo Informs / Products / Web) | ดูแล `glossary.md` / `rules.md` / ไฟล์นี้ · ตอบคำถามธุรกิจ · ตกลงเรื่องที่กระทบ contract / ขอบเขต · **ตามความคืบหน้าจาก `plan.md`** |
| **Dev 2** (mapping ทั้งก้อน) | เขียน **`plan.md`** (แผน + ความคืบหน้า) เอง · โค้ดใน repo `prjInsuranceValidationMapping` + หน้าเว็บ mapping ใน `prjInsuranceValidationWeb` · ตัดสินเรื่องภายใน mapping (`M-xx`) |

## ไฟล์ในโฟลเดอร์นี้

| ไฟล์ | เนื้อหา | ผู้แก้ |
|---|---|---|
| `README.md` | ไฟล์นี้ — ขอบเขต วันสำคัญ ที่มาของเอกสาร | Dev 1 |
| [`glossary.md`](glossary.md) | ศัพท์ที่ทั้งโปรเจคใช้ร่วม (inform, product / package, sale channel, …) | Dev 1 |
| [`rules.md`](rules.md) | กฎธุรกิจ (`BR-xxx`) + การตัดสินใจของโปรเจค (`D-xx`) ที่ mapping ต้องทำตาม + คำถามที่ต้องตกลงตอน kickoff | Dev 1 |
| [`contract-pa-inform.md`](contract-pa-inform.md) | **contract ที่ mapping ส่งเข้า Informs** — column MTLS → path, ตัวอย่าง JSON, response แต่ละแบบ + mapping ต้องทำอะไร | Dev 1 |
| [`mtls-file.md`](mtls-file.md) | รูปแบบไฟล์ MTLS (encoding, column, แถว `C`) + สิ่งที่ต้องระวังตอนแปลงค่า | Dev 1 |
| [`web.md`](web.md) | หลักร่วมของหน้าเว็บ + กฎของ repo Web ที่หน้า mapping ต้องทำตาม | Dev 1 |
| [`plan.md`](plan.md) | **ไฟล์กลาง** — แผนงาน mapping (Dev 2 วางแผนเป็น bullet) + อัปเดตความคืบหน้า + การตัดสินใจ `M-xx` + คำถามระหว่างกัน | **ทั้งคู่** (ส่วนไหนใครเขียน ดูหัวไฟล์) |

ลำดับอ่านครั้งแรก: ไฟล์นี้ → `glossary.md` → `rules.md` → `contract-pa-inform.md` → `mtls-file.md` → `web.md` (ก่อนทำหน้าเว็บ)

## เปิด workspace

- เปิด `mapping-demo.code-workspace` ที่ root (VS Code: File → Open Workspace from File) — รวม repo นี้ + `prjInsuranceValidationMapping` + `prjInsuranceValidationWeb` + `prjInsuranceValidationInforms` (อ่านอย่างเดียว)
- ทุก repo ต้อง clone ไว้ **folder เดียวกัน** (เช่น `C:\SourceProjects\mapping-demo`, `C:\SourceProjects\prjInsuranceValidationMapping`, …)
- AI agent (ChatGPT / Codex) อ่าน `AGENTS.md` ของ repo ที่ทำงานอยู่ — ถ้าใช้ ChatGPT แบบแชท ให้แนบ `AGENTS.md` + ไฟล์ในโฟลเดอร์นี้ที่เกี่ยวกับงานไปด้วยทุกครั้ง

## ขอบเขต phase 1 (D-70, D-77, D-78)

ทำ:
- อ่านไฟล์ partner **MTLS** (partner แรกและ partner เดียวของ phase 1)
- config mapping ของ MTLS → JSON ตาม contract **`pa-inform/1`**
- ส่งเข้า service Informs `POST /api/pa/informs` **ทีละใบ** + idempotency key + retry + **delivery log**
- **หน้าเว็บ mapping** ใน repo Web (ตั้งค่า mapping / ดูผลการส่ง — ขอบเขตละเอียดตกลงตอน kickoff)
- deploy บน **Kubernetes** ของบริษัท (แทน IIS ที่เขียนไว้ใน D-70)

ไม่ทำ:
- partner อื่น
- แผน `plan-mapping-api-modernization.md` (Windows Service / ตาราง FileProcessing)
- เขียนตารางของ Informs ตรง — ส่งผ่าน API เท่านั้น (D-02)

## วันสำคัญ (ร่างจากแผนของ Dev 1 — 🟡 ทบทวนตอน kickoff 11-16)

> สถานะของแต่ละจุดติดตามที่ [`plan.md`](plan.md) §2

| วันที่ | จุดสังเกต |
|---|---|
| 11-13 | Dev 1 ส่งไฟล์ทดสอบ MTLS 2,556 แถว (ไม่ใช่ข้อมูลจริง) |
| **11-16 (จ.)** | **kickoff** Dev 1 + Dev 2 — ตกลงคำถามใน [`rules.md`](rules.md) §คำถามที่ต้องตกลง |
| 11-18 | อ่านไฟล์ทดสอบครบทุกแถว |
| 11-23 | JSON ผ่าน schema `pa-inform-1.schema.json` ทุกแถว |
| **11-26** | **mapping ส่งไฟล์ MTLS เข้า Insurance Validation ได้ (บนเครื่อง)** |
| **12-03** | **หน้าเว็บ mapping ใช้ได้บนเครื่อง** |
| 12-04 | เตรียม deploy บน Kubernetes (mount share folder, ConfigMap / Secret) |
| 12-08 – 12-09 | ทดสอบร่วมบน docker → deploy บน cluster → ทดสอบร่วมบน cluster |
| 12-11 | เผื่อ · แก้ปัญหาจากทดสอบร่วม |
| 12-14 | ตรวจความพร้อม UAT |
| **12-15 (อ.)** | **เริ่ม UAT** — ครบทั้งระบบ รวมหน้าเว็บ mapping |

## repo ที่เกี่ยว

| repo | ใช้ทำอะไร |
|---|---|
| `mapping-demo` (repo นี้) | demo + reference · เอกสาร mapping phase 1 |
| `prjInsuranceValidationMapping` | **โค้ด service Mapping** (.NET 10, schema `mapping`, port 5177) — โครงทำไว้แล้ว อ่าน `AGENTS.md` ของ repo ก่อนเขียน |
| `prjInsuranceValidationWeb` | หน้าเว็บ (Angular 21 + PrimeNG 21) — หน้า mapping อยู่ `features/mapping/` |
| `prjInsuranceValidationInforms` | service ที่รับใบ — ไฟล์ contract ตัวจริง `src/Informs/Informs.Api/Contracts/PaInform/pa-inform-1.schema.json` |

> ⚠️ `src/MappingDemo.Api/ContractFixtures/pa-inform-v1.json` ใน demo อาจเก่ากว่า contract ตัวจริง (contract เปลี่ยนเป็น `insureds[]` วันที่ 2026-11-12) — ให้ใช้ไฟล์จาก repo Informs เสมอ
