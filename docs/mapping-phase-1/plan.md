---
title: mapping phase 1 — แผนและความคืบหน้า (ไฟล์กลาง Dev 1 + Dev 2)
status: draft
last_updated: 2026-11-13
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

- [ ] **M1.1** ปิดแบบ entity/state/ตาราง/unique identities และ transaction boundaries — เสร็จเมื่อ: มี data design พร้อมตัวอย่าง Source/version/jobs/attempts, lineage และแบบป้องกันหลาย pod ใน repo เอกสาร; ไม่มีตารางนอก schema `mapping` · เป้า 11-16 · รอ M0.2/Q2–Q6
- [ ] **M1.2** ปิด Mapping API/DTO/error contract สำหรับ Web ภายหลัง — เสร็จเมื่อ: route/field paths/BaseResponse/version conflict/pagination/action states/preview limits มีตัวอย่าง request-response และตกลง prefix ใน Q8 · เป้า 11-17
- [ ] **M1.3** Implement EF persistence, config/version lifecycle และ immutable Source storage — เสร็จเมื่อ: migration/constraints ผ่าน PostgreSQL integration tests, activate พร้อมกันได้ conflict ชัดเจน, version เก่า/Source rows ไม่ถูกแก้ · เป้า 11-18 · รอ M1.1 และการยืนยันรายการไฟล์ source
- [ ] **M1.4** Implement Informs contract/profile snapshot และ capability catalog — เสร็จเมื่อ: schema/hash/path catalog ตรง Informs `insureds[]`, version pin ได้, ไม่มี latest-schema fallback เปลี่ยนงานเดิม, ไม่คืน secret/เปิด capability ที่ยังไม่ทำ · เป้า 11-18

### M2 — อ่าน MTLS และสร้าง payload

- [ ] **M2.1** Implement Excel reader สำหรับ MTLS — เสร็จเมื่อ: fixture ที่รับรองอ่านได้ครบ **2,556 แถว** จาก sheet แรก/header 19 columns/cp874, มี physical/import row number, ภาษาไทยและ number formatting ถูกต้อง · เป้า 11-18 · รอ Q4/Q7
- [ ] **M2.2** ย้าย Transformation Program ส่วนที่ MTLS ใช้ และสร้าง versioned MTLS config — เสร็จเมื่อ: nested `insureds[]/beneficiaries[]`, constants, P/C, null+unparsed/default ตามคำตอบ kickoff ผ่าน unit/contract tests โดยไม่ตรวจ product/Client API เอง · เป้า 11-20 · รอ Q1/Q5
- [ ] **M2.3** Implement validate/preview/workbook preview — เสร็จเมื่อ: ใช้ runtime compiler เดียวกัน, diagnostics มี path/code, จำกัดขนาด/records และ integration test ยืนยันไม่มี job/source/outbox/HTTP/archive side effects · เป้า 11-23
- [ ] **M2.4** ทดสอบ MTLS เต็มไฟล์กับ schema จริง — เสร็จเมื่อ: 2,556 แถวได้ 2,556 outputs `default` ผ่าน `pa-inform/1`, แถว C ครบและค่าแปลงไม่ได้ยังอยู่ใน unparsed; บันทึกผล/เวลารัน/fixture identity โดยไม่มี PII · เป้า 11-23 · รอ fixture ที่ยืนยันใน Q7

### M3 — รับไฟล์และประมวลผลแบบ durable

- [ ] **M3.1** เพิ่ม outbox/Kafka consumers และ local infrastructure ตาม Informs — เสร็จเมื่อ: transaction/offset/redelivery integration tests ผ่าน, events มี job IDs ไม่มี PII, topic names และบริษัท dependency ได้ข้อสรุป · เป้า 11-20 · รอ Q1 ข้อ 9/Q7
- [ ] **M3.2** Implement scan/snapshot/import/resume/archive — เสร็จเมื่อ: pin version ก่อนรับ, partial-file stability/path containment/ownership ทำงาน, หลาย scanner ไม่รับ occurrence ซ้ำ, Source+Row Job+outbox atomic, archive ไม่ทำหลักฐานหายเมื่อ original เปลี่ยน · เป้า 11-23 · duplicate-content policy รอ Q6
- [ ] **M3.3** Implement row transformation jobs, claim/lease/recovery และ row diagnostics — เสร็จเมื่อ: restart/redelivery ไม่สร้าง Source/output ซ้ำ, business conversion failure ไม่ทิ้งแถว, system/contract failure แสดงในยอดที่ยังไม่ส่ง และแถวอื่นไปต่อได้ · เป้า 11-24

### M4 — ส่ง Informs และดูแลผลการส่ง

- [ ] **M4.1** Implement encrypted durable Delivery Payload และ typed Informs HttpClient — เสร็จเมื่อ: 1 request/ใบ, bytes/hash/key persist ก่อนส่ง, 201/200/replayed/replaced/400/409 ตรง contract จริง และไม่แตะ `non_motor.*` · เป้า 11-24
- [ ] **M4.2** Implement retry/backoff/Retry-After/concurrency/lease recovery — เสร็จเมื่อ: 429/5xx/timeout และ crash หลัง Informs commit ใช้ bytes/key เดิม, attempt bounded/ไม่ retry ซ้อน, permanent rejection ไม่ auto retry · เป้า 11-25
- [ ] **M4.3** Implement delivery log/file summary/retry และ pause controls ที่ตกลง — เสร็จเมื่อ: counts ต่อ unique output ไม่ปน attempts, แยก import/transform/delivery, field reason/history ดูได้, action state guard/audit ผ่าน; reprocess เปลี่ยน payload/resend ยังปิดจน Q3 ตกลง · เป้า 11-26
- [ ] **M4.4** ทดสอบ end-to-end Mapping → Informs บนเครื่อง — เสร็จเมื่อ: fixture ครบ, source/payload/delivery counts กระทบยอดได้, replay/replaced/rejection/throttle/restart ผ่าน, `scripts/check.ps1` ผ่านและบันทึก repo+commit/ผลจริง · เป้า **11-26**

### M5 — ส่งต่อ Web และเตรียม Kubernetes

- [ ] **M5.1** เตรียม environment config, Linux paths, share mount, probes และ shutdown/recovery — เสร็จเมื่อ: `/health/live` และ `/health/ready` (ตรวจ DB) ใช้ได้, secrets/key references ไม่อยู่ใน image, pod restart ไม่ทำงานหาย, deploy/migration requirements ส่งให้ Dev 1/ทีม infrastructure · เป้า 12-04
- [ ] **M5.2** ส่ง API contract/OpenAPI/examples ให้ทำ Web — เสร็จเมื่อ: เอกสารตัวอย่าง/DTO/states/counts/action availability ตรง API ที่รันจริง, CORS/base URL พร้อม, ไม่มี docs ใน repo โค้ด · เป้า 11-27
- [ ] **M5.3** Implement และเชื่อมหน้า mapping หลังได้รับ Web design — เสร็จเมื่อ: scope ที่ตกลงใช้งานบนเครื่องผ่าน `core/mapping-api.ts` + `features/mapping/`, build/unit tests ของ Web ผ่าน, ไม่แก้เมนูอื่นและไม่เพิ่ม package โดยยังไม่บอก Dev 1 · เป้า **12-03** · รอ design จากผู้ใช้; รายการย่อยเพิ่มหลัง review design

### M6 — ทดสอบร่วมและเตรียม UAT

- [ ] **M6.1** ทดสอบร่วมและ recovery บน cluster — เสร็จเมื่อ: fixture ผ่านเส้นทาง share → Mapping → Informs ครบ, ทดสอบ restart/หลาย consumer/permissions/mount/secrets พร้อมหลักฐาน environment · เป้า 12-09
- [ ] **M6.2** ปิด regression และเตรียมหลักฐาน UAT — เสร็จเมื่อ: acceptance ในแผน API §9 ผ่าน, issue ที่บล็อกถูกปิด, มีวิธีดูยอด/แก้เหตุส่งไม่สำเร็จ/retry และ owner ของข้อจำกัดที่ยังค้าง · เป้า 12-14

feature ที่เสนอเลื่อนหลังเส้นทาง MTLS API (Table admin แบบ dynamic, input อื่น, destination อื่น, generic contract/profile admin, business resend/rollout) ยังอยู่ใน [feature matrix](api-implementation-plan.md#2-ขอบเขตและการเทียบ-feature) และ Q2/Q3; เพิ่มงาน/เป้าเมื่อได้รับข้อตกลง ไม่ถือว่าได้รับอนุมัติให้ implement หรือถูกตัดทิ้งแล้ว

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
- [ ] **Q2** (Dev 2, 11-13) ขอ review feature scope จาก [feature matrix](api-implementation-plan.md#2-ขอบเขตและการเทียบ-feature): เสนอทำ MTLS Excel → Informs API ก่อน; เสนอเลื่อน LegacyTable/JsonFile/DatabaseTable, Delimited/Fixed-width, dynamic Table admin, generic profile/contract admin และ advanced rollout ที่ไม่จำเป็นต่อ MTLS แต่ยังเก็บ backlog ครบ · เสนอ Source Table แบบ fixed `mapping.source_rows` + immutable JSONB values และ Source field catalog แทน physical table ต่อ config · ขอปิดขอบเขต config/job/delivery views รวมถึงการเปิดดู raw Source/payload ตาม D-07/D-36 และวันรับ Web design เพื่อรักษา milestone 12-03
- [ ] **Q3** (Dev 2, 11-13) Reprocess ด้วย config ใหม่/ส่งใหม่หลังแก้ mapping จะใช้ identity อย่างไร? D-44 key `(fileJobId, sourceRowId, outputKey)` ไม่รวม config version: output ที่เคย Delivered ใช้ key เดิมจะ replay ผลเก่า ส่วน key ใหม่อาจทำให้ข้อมูลเก่าทับฉบับแก้ภายหลัง · เสนอเปิด normal delivery/retry bytes/key เดิมก่อน และปิด changed-payload reprocess/business resend จนตกลง contract/lineage ชัดเจน; แยกคำตอบสำหรับ rejected 400/409, never-delivered และ Delivered
- [ ] **Q4** (Dev 2, 11-13) ต่อ Q1 ข้อ 6: `sourceRowNo` ใช้ `nSeq` หรือ import record order? ขอระบุกรณี header/blank row, nSeq ว่าง/ซ้ำ/ไม่เรียง/มี gap และเวลารับไฟล์ครั้งแรกที่ต้องคงไว้ตาม D-23 · เก็บ Row Number และ Physical Line Number แยกกันเสมอ ไม่ใช้ลำดับ HTTP arrival
- [ ] **Q5** (Dev 2, 11-13) พบช่องว่างระหว่างกฎกับ schema ตัวจริงของ Informs (`6ab23ad`): BR-035 ระบุทายาทตามกฎหมาย **100%** แต่ `$defs.beneficiary` มีเพียง `seq/fullName/relationship` และ `additionalProperties: false` — ให้ mapping ส่งแค่ชื่อ default แล้วสัดส่วนเป็นหน้าที่ Informs หรือจะเพิ่ม field โดย Dev 1? อีกกรณี D-04 ให้ส่งค่าผิดเป็น null แต่ schema บังคับ `sourceReference` nonempty/`transactionType` enum; ถ้าต้นทางไม่มีค่าที่ส่งได้ จะให้ ops/UW ติดตามอย่างไร? เสนอเก็บ Source + row error/ยอดยังไม่ส่ง โดยไม่แต่งค่าและไม่เพิ่ม contract field เอง
- [ ] **Q6** (Dev 2, 11-13) BR-001 กันไฟล์ซ้ำด้วย content hash ยังเป็น 🔶: ยืนยัน scope ของ hash ว่าเป็นต่อ config/partner/ช่วงเวลา/ทุก version และจัดการไฟล์แก้ไขชื่อเดิม/เนื้อหาเดิมที่ reprocess อย่างไร? แยกจาก DB occurrence claim ที่จำเป็นสำหรับกันหลาย pod รับไฟล์เดียวกัน · ยังไม่ถือว่า demo duplicate policy เป็นกฎที่อนุมัติแล้ว
- [ ] **Q7** (Dev 2, 11-13) ขอ Dev 1 ระบุตำแหน่ง/identity ของ **fixture MTLS 2,556 แถวที่รับรองว่าเป็นข้อมูลสมมติ** (ไฟล์ `docs/MTLSPA0220261009.xls` ถูกอธิบายว่าเป็นตัวอย่างจริง จึงไม่ใช้ทดแทน) และ dependency: Kafka/topics/groups/owner, Mapping DB บริษัท, share mount/archive/snapshot access, Informs internal URL/auth/TLS, encryption key provisioning/rotation/retention และผู้ดู Source/delivery log/payload; phase นี้ใช้ fixed actor ตาม web.md ไม่สร้างระบบสิทธิ์จาก header ที่ browser กำหนดเอง
- [ ] **Q8** (Dev 2, 11-13) เสนอ prefix API **`/api/mapping`**, BaseResponse เดิม, field-path errors, expected `rowVersion` → 409, list `items/totalCount/page/pageSize` (default 20/max 100), dates ISO/UTC และ route/action subset ใน [แผน API §5](api-implementation-plan.md#5-api-ที่เสนอก่อนมี-web-design) · ขอ review ก่อน M1.2/เขียน controller; pause intake/delivery และ raw payload inspection ให้เปิดเท่าที่ scope Q1/Q2 ตกลง
