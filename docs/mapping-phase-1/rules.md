---
title: กฎและการตัดสินใจที่ mapping ต้องทำตาม
status: review
last_updated: 2026-11-13
---

# กฎและการตัดสินใจที่ mapping ต้องทำตาม

> คัดจากเอกสารกลางของโปรเจค Insurance Validation · **ID ตรงกับต้นฉบับ** (`BR-xxx` = กฎธุรกิจ, `D-xx` = การตัดสินใจของโปรเจค) อ้างด้วย ID ได้เลย · เลขที่ข้ามไป = ไม่เกี่ยวกับ mapping
> **สถานะ:** ✅ ผู้ใช้ / business ยืนยันแล้ว · 🔶 ข้อเสนอ ยังไม่ยืนยัน · 📜 พฤติกรรมของ legacy ที่ยกมา (ยังไม่มีใครยืนยันว่าเป็นกฎธุรกิจ)
> **ขอบเขต:** "ทุก partner" = กฎกลาง · "MTLS" = หลักฐานมาจาก partner เดียว — **อย่าถือว่าเป็นกฎกลาง**
> ข้อไหนไม่ชัดหรือขัดกับที่เห็นในไฟล์ / โค้ด → เขียนลง `plan.md` §คำถามถึง Dev 1 ห้ามตีความเอง

## 1. เส้นแบ่งงาน — อะไรเป็นงาน mapping

### D-06 เส้นแบ่งงาน: mapping / validate / Client API

| สิ่งที่เปลี่ยนตาม | ใครทำ | ตัวอย่าง MTLS |
|---|---|---|
| **partner** | **mapping** | แปลงวันที่, `-` → null, column ผู้รับประโยชน์ 1/2 → array, ค่าคงที่ `MTLSPA` / `MTL2` / agent `74000490` / sale channel `FILE`, `P` / `C` → `NEW` / `CANCELLED`, ความสัมพันธ์ `PRIMARY`, ผู้รับประโยชน์ว่าง → "ทายาทตามกฎหมาย" |
| **product** | Informs (stage product) | เบี้ย ทุน เพดาน ช่วงอายุ commission รหัสแผน AS400 — อ่านจาก config |
| **ข้อมูลตัวลูกค้า / MCIS** | Client API (Informs เรียก) | แยกชื่อ / คำนำหน้า / ที่อยู่, หารหัสพื้นที่, เลขบัตร, อาชีพ, blacklist / AML, ความสัมพันธ์ผู้รับประโยชน์ |
| **ค่าที่ผิด** | UW | แก้ที่ original แล้วตรวจใหม่ |

- **ทำไม:** partner ใหม่ = เพิ่ม mapping config อย่างเดียว ไม่แตะการตรวจ · product เปลี่ยน = แก้ config ไม่แตะ mapping ของทุก partner
- **ห้าม:** mapping แยกชื่อ / ที่อยู่ (legacy แยกผิดแล้วไม่มีใครรู้ — คำนำหน้า `น.ส.` หายเสมอ) · mapping เติมค่าที่ได้จาก package (เบี้ย ทุน) ลงใบ

| ID | กฎ | ขอบเขต | สถานะ | เหตุผล |
|---|---|---|---|---|
| BR-026 | เปลี่ยนตาม partner → mapping · เปลี่ยนตาม product → validate · ข้อมูลตัวลูกค้า → Client API · แก้ค่าที่ผิด → UW (= D-06) | ทุก partner | ✅ | partner ใหม่ไม่แตะ validate, product เปลี่ยนไม่แตะ mapping |
| BR-025 | package ที่ partner ขายเป็นข้อตกลงกับ partner → **mapping config ใส่ค่าคงที่** (MTLS: `MTLSPA` / `MTL2`) | ทุก partner | ✅ | ค่าถูกตรึงตาม version ของ mapping config — ตรวจงานเก่าซ้ำยังได้ package เดิม · legacy ฝังในโค้ดต่อ partner ต้อง compile ใหม่ |
| BR-024 | ค่าที่ได้จาก package (ทุน เบี้ย commission รหัสแผน) มาจาก config **ไม่เขียนลงใบ** | ทุก partner | 📜 | ใบเก็บแค่สิ่งที่ partner ส่ง |
| BR-040 | ใบเก็บชื่อ / ที่อยู่เป็น**ข้อความตามที่ partner ส่ง** — Client API แยกเอง | ทุก partner | ✅ | การแยกต้องใช้ master และต้องให้คนแก้ได้ |
| BR-041 | ข้อความจาก partner (อาชีพ คำนำหน้า ความสัมพันธ์) แปลงเป็น master ผ่าน alias **ที่ Client API** ไม่ใช้กฎเดาจากรูปแบบ | ทุก partner | 🔶 | กฎเดาของ legacy ผิด: MTLS ใช้ "ยาวไม่เกิน 4 ตัว = รหัส" ซึ่งผิดกับคำว่า `ทหาร` |

## 2. การส่งเข้า Informs

### D-02 Insurance Validation เป็นเจ้าของใบคำขอ (original) แต่ผู้เดียว
- **ตัดสิน:** มีแค่ service Informs ที่เขียนตารางใบคำขอ — mapping ส่งผ่าน API · Informs ประกาศ contract (JSON Schema) แล้ว mapping map จาก source → path ใน contract
- **ทำไม:** legacy ให้ mapping เขียนตารางปลายทางเอง ทำให้ mapping ผูกกับ schema ของทุก domain · ใบมี 2 ฝ่ายเขียน (mapping + UW) ต้องมีที่เดียวคุม
- **ห้าม:** เขียนตารางของ Informs ตรง (`non_motor.*`)

### D-03 mapping ส่งทีละใบผ่าน HTTP แบบบันทึกอย่างเดียว
- **ตัดสิน:** `POST /api/pa/informs` 1 request = 1 ใบ · mapping **รอผลการบันทึก** แต่**ไม่รอผลตรวจ** (ตรวจแบบ async) · **mapping คุมความขนานเอง** — Informs ตอบ `429` ถ้าเกิน · ส่ง `sourceRowNo` มาด้วย
- **ทำไม:** mapping ต้องรู้ทันทีว่าแถวไหนรับ / ไม่รับ เพื่อเขียน delivery log · 1 ใบต่อ request retry ง่าย
- **ไม่เลือก:** batch หลายใบต่อ request · ส่งผ่าน Kafka ตั้งแต่ mapping (ต้องมี topic ตอบผล reject กลับ debug ยากช่วง UAT — เปลี่ยนภายหลังได้)

### D-04 API reject ได้แค่ผิด contract กับใบซ้ำ
- **ตัดสิน:** `400` = ผิดโครงสร้าง / ชนิดข้อมูล · `409` = ใบซ้ำที่ผ่านแล้ว · **ข้อมูลผิดทางธุรกิจ Informs รับเข้า** แล้วไปเป็น validation message
- **ผลต่อ mapping:** ค่าที่แปลงชนิดไม่ได้ (เช่น วันที่ `-293`) **ส่งเป็น `null` + ค่าดิบใน `extras.unparsed`** แทนการทิ้งแถว — ใบที่ไม่เข้าระบบ UW มองไม่เห็นและแก้ไม่ได้
- **ห้าม:** mapping ตัดแถวทิ้งเพราะ "ข้อมูลธุรกิจไม่ครบ" — ความครบถ้วนตรวจที่ Informs

### D-05 / D-46 ใบซ้ำ: ยังไม่ผ่าน → แทนที่, ผ่านแล้ว → reject
- **ตัดสิน:** `sourceReference` เดิมของ partner เดิม: ใบเดิม `RECEIVED` / `EXCEPTION` / `TECHNICAL_HOLD` → แทนที่ด้วยข้อมูลใหม่แล้วตรวจใหม่ (`200` + `replaced: true`) · ใบเดิม `PASSED` / `CANCELLED` → `409` · **ส่งซ้ำด้วย idempotency key เดิม → ผลเดิม** (`200` + `replayed: true`)
- **ทำไม:** ทางแก้ใบที่ข้อมูลผิดคือ**ให้ partner ส่งรายการนั้นมาใหม่**

### D-44 idempotency key
- **ตัดสิน:** key = `(fileJobId, sourceRowId, outputKey)` · Informs จำ**ทุก key ที่เคยรับ** (ไม่ใช่แค่ล่าสุด)
- **ทำไม (ตัวอย่าง):** ไฟล์ 1024 ส่ง POL001 เบี้ยผิด → ไฟล์ 1030 ส่งฉบับแก้ (แทนที่) → retry ค้างของไฟล์ 1024 มาถึงทีหลัง → ต้องได้ `200 replayed` ไม่ใช่แทนที่กลับด้วยข้อมูลเก่า
- **ผลต่อ mapping:** retry ของแถวเดิมต้องใช้ key เดิมเสมอ · key ต้องคงที่ข้ามการรันใหม่

### D-07 หลักฐาน as-received อยู่ที่ mapping
- ใบใน Informs ชี้กลับหลักฐานด้วย `fileJobId` / `sourceRowId` · **Source Table ของ mapping = หลักฐานว่า partner ส่งอะไรมา** (Informs ไม่เก็บสำเนา) — ห้ามลบ / แก้แถวใน Source Table
- Informs ไม่เก็บว่าค่าใดมาจาก partner หรือ mapping เติม — ดูได้จาก Source Table + mapping config version (D-06)

### D-23 `sourceRowNo` ต้องเป็นลำดับในไฟล์ (Informs ใช้นับเพดานกรมธรรม์)
- **ตัดสิน:** Informs นับใบของคนเดียวกันเพื่อตรวจเพดานกรมธรรม์ต่อคน โดยเรียงตาม **(เวลารับไฟล์ครั้งแรก, `sourceRowNo`)** ไม่ใช่ตามลำดับที่ API ได้รับ
- **ทำไม:** mapping ส่งขนาน ลำดับที่ API ได้รับจึงไม่ตรงกับไฟล์ — ต้องได้ผลเดียวกันทุกครั้งและตรงกับ legacy
- **ผลต่อ mapping:** `sourceRowNo` ต้อง**คงที่และสะท้อนลำดับแถวในไฟล์** (ส่งซ้ำ / retry ใช้เลขเดิม) — จะใช้ลำดับแถวที่อ่าน หรือ `nSeq` ในไฟล์ = คำถามข้อ 6 ใน §6

### D-36 ยอดแถวที่ส่งไม่สำเร็จดูที่ mapping
- **ตัดสิน:** หน้า UW มีสรุปต่อไฟล์ (นับต่อ `fileJobId` แยกตามสถานะของใบ — นับเฉพาะใบที่รับเข้าแล้ว) · **ยอดแถวทั้งไฟล์ / แถวที่ส่งไม่สำเร็จ (`409` / `400`) อยู่ที่ delivery log ของ mapping** · mapping ส่ง `fileName` มาด้วยเพื่อแสดงชื่อไฟล์
- **ผลต่อ mapping:** delivery log ต้องตอบได้ว่าไฟล์หนึ่งมีกี่แถว ส่งสำเร็จ / ไม่สำเร็จเท่าไร แถวไหน เพราะอะไร (คำถามข้อ 7 ใน §6)

### D-15 id = uuid v7
- id ทุกตัวที่ระบบสร้างใช้ **uuid v7** (เรียงตามเวลา) ห้าม v4 · โครง repo Mapping มี `Uuid.NewV7()` ให้แล้ว

## 3. กฎของข้อมูล (การรับงานและกันซ้ำ)

| ID | กฎ | ขอบเขต | สถานะ | เหตุผล / ที่มา |
|---|---|---|---|---|
| BR-001 | ไฟล์เดิมห้ามรับซ้ำ — ใช้ content hash | ทุก partner แบบไฟล์ | 🔶 | กันใบคำขอซ้ำทั้งชุด · legacy เช็คด้วยชื่อไฟล์ |
| BR-002 | ใบซ้ำ (partner + `sourceReference`) ยังไม่ผ่าน → แทนที่ (ข้อมูล partner ทับที่ UW แก้) · ผ่าน / ยกเลิกแล้ว → reject (= D-05) | ทุก partner | ✅ | เลขอ้างอิงระบุใบได้ 1 ใบ |
| BR-003 | กันซ้ำด้วยเลขอ้างอิงใบคำขอ **ไม่ใช่เลขบัตร** | ทุก partner | ✅ | 1 คนมีหลายใบได้ — ไฟล์ตัวอย่าง MTLS มี 756 คนที่มี 2 ใบในไฟล์เดียว |
| BR-010 | แถวยกเลิก (`C`) ส่งเป็นใบ `CANCELLED` — Informs เก็บไว้ ไม่ตรวจ ไม่เข้าคิว UW | MTLS (อาจเป็นกลาง?) | ✅ | ฝ่ายขายออกหมายเลข PA ที่หน้าร้านแล้วลูกค้ายกเลิก ต้องมี record ว่าเลขนี้ถูกยกเลิก · แถว `C` ข้อมูลไม่ครบ (ชื่อ "ยกเลิก", วันที่ `-293`, เงิน 0) |
| BR-011 | แถว `C` ที่หมายเลข PA เคยรับเป็นงานใหม่แล้ว → reject | MTLS | 🔶 | เป็นการยกเลิกภายหลัง (นอก phase 1) · ❓ MTLS ยกเลิกภายหลังผ่านช่องทางไหน |
| BR-035 | ชื่อผู้รับประโยชน์ว่าง → **mapping เติม "ทายาทตามกฎหมาย" 100%** | **MTLS เท่านั้น** | ✅ | กฎใหม่ (legacy ปล่อยชื่อว่างแต่ insert ผู้รับประโยชน์คนที่ 1 เสมอ) · ค่า default ของ partner เป็นงาน mapping |
| BR-021 | ค่าคงที่ของ MTLS: product `MTLSPA`, package `MTL2`, agent `74000490` | MTLS | 📜 | legacy hard code ในโปรแกรม MTLS · ตัวเลขเบี้ย / ทุนไม่ใช่งาน mapping |

## 4. ข้อมูลไฟล์ MTLS (ใช้ประกอบ ไม่ใช่กฎ)

- ไฟล์ Excel `.xls` (encoding cp874) 1 sheet, header ภาษาไทย 19 column · 1 แถว = 1 ใบคำขอ = ผู้เอาประกัน 1 คน (= ผู้ถือกรมธรรม์), ผู้รับประโยชน์สูงสุด 2 คน
- `sourceReference` = หมายเลข PA (`PF` + 6 หลัก) · ไม่มีไฟล์ confirm / cancel
- ชื่อผู้เอาประกันมีคำนำหน้า `นาย` / `นางสาว` / `นาง` ติดกับชื่อไม่มีช่องว่าง · ชื่อผู้รับประโยชน์ไม่สม่ำเสมอ (`น.ส.` / `นส.` / `ด.ญ.` / ไม่มีคำนำหน้า) — **mapping ส่งตามที่เป็น ไม่แยก** (D-06)
- ที่อยู่ลงท้ายด้วยรหัสไปรษณีย์ทุกแถว · อาชีพเป็นข้อความ 59 ค่า ไม่มีรหัส · ความสัมพันธ์ผู้รับประโยชน์ 17 ค่า (บุตร, ภรรยา, มารดา, …) — ส่งเป็นข้อความ
- ไฟล์ตัวอย่าง: 2,556 แถว (`P` 2,553 / `C` 3) · เบี้ย 180 ทุกแถว · คุ้มครอง 1 ปี

## 5. ขอบเขตและโครงของงาน

### D-01 phase 1 = PA / `MTLS`
- product group PA, partner แรก `MTLS` · ไม่รวมออกกรมธรรม์ / confirm / cancel
- **ห้าม:** ใส่ field ของ partner อื่นล่วงหน้าโดยไม่มีหลักฐาน · ตั้งชื่อ field ผูกกับ MTLS (เช่น `cPol_no`) — ใช้ชื่อกลาง (`sourceReference`)

### D-70 UAT 2026-12-15 · mapping อยู่ในขอบเขต
- ขอบเขตขั้นต่ำ: อ่านไฟล์ MTLS · config mapping ของ MTLS → JSON `pa-inform/1` · ส่งเข้า `POST /api/pa/informs` + delivery log + retry
- ไม่ทำ: partner อื่น · แผน modernization แบบ Windows Service
- **ห้าม:** ใช้ข้อมูลส่วนบุคคลจริงในไฟล์ทดสอบ · รับงานแทรกหลัง 2026-12-04 ที่ไม่บล็อก UAT โดยไม่คุยกับ PM
- **เปลี่ยนภายหลัง:** deploy บน **Kubernetes** แทน IIS · หน้าเว็บ mapping ทำก่อน UAT (D-78)

### D-77 service Mapping เป็น repo ที่ 4 โครงเดียวกับ Products / Informs
- repo `prjInsuranceValidationMapping` — namespace `InsuranceValidation.Mapping.*` · port 5177 · schema `mapping` · container `InsuranceValidation.Mapping[.Migrate]`
- **โครง project ต้องเหมือน 3 repo** (`src/Mapping/Mapping.{Domain,Application,Infrastructure,Api}` · `tests/Mapping.{UnitTests,IntegrationTests}` · `scripts/check|up|migrate.ps1`) — Dev 1 ทำแค่โครง **โค้ดข้างใน Dev 2 ทำ** · ความเหมือนกันคุมด้วยกฎใน `AGENTS.md` ของ repo
- ไฟล์ partner: mount share หลักครั้งเดียว (`Paths:InputRoot`) · `InputPath` ของ config = path ย่อย · วน scan เป็นหลัก (ร่าง)
- **ไม่เลือก:** พัฒนาต่อใน mapping-demo ตรง ๆ (โครงไม่ตรงมาตรฐานของอีก 3 repo)
- **ค้าง:** repo บน GitLab บริษัท · DB บริษัทของ mapping

### D-78 หน้าเว็บ mapping อยู่ใน repo Web · Angular 21 + PrimeNG 21
- หน้าเว็บ mapping ทำใน `prjInsuranceValidationWeb` (`features/mapping/`) **ก่อน UAT**
- **ห้าม:** ขึ้น `primeng` 22+ หรือใช้ version `-lts` (ต้องมี license) · `npm update` ที่ข้าม major

## 6. คำถามที่ต้องตกลงตอน kickoff (2026-11-16)

ยังไม่มีคำตอบ — ตกลงแล้ว Dev 1 จะลงเป็น `D-xx` ที่ไฟล์นี้ · ข้อที่เป็นเรื่องภายใน mapping Dev 2 บันทึกเป็น `M-xx` ใน `plan.md`

1. **ผลลัพธ์ของ mapping** — สร้าง JSON ตาม contract `pa-inform/1` ตรง ๆ หรือเขียนตาราง normalized แบนก่อนแล้วแปลง? (contract เป็น JSON ซ้อน: ผู้เอาประกัน `insureds[]` + ผู้รับประโยชน์ — ไม่ใช่แถวแบน)
2. **การแปลงค่าเขียนแบบไหน** — ใช้ Transformation Program ของ demo (Phase 19) หรือโค้ดเฉพาะ MTLS ไปก่อน
3. **รูปแบบไฟล์ MTLS** ที่อ่านจริง
4. **เติมทายาทตามกฎหมาย** (BR-035) อยู่ตรงไหนของ config mapping ของ MTLS
5. **ค่าคงที่ของ MTLS** (`MTLSPA` / `MTL2` / `74000490` / `FILE` / `PRIMARY`) + แปลง `P` / `C` — เก็บที่ไหน
6. **idempotency key** + `sourceRowNo` (ลำดับแถว หรือ `nSeq` ในไฟล์) · ความขนานตอนส่ง + จัดการ `429` / `409` / `400`
7. **delivery log** — เก็บอะไร ใครดู (ยอดแถวที่ส่งไม่สำเร็จดูที่ mapping)
8. **ขอบเขตหน้าเว็บ mapping** — ตั้งค่า mapping / ดูผลการส่ง แค่ไหน
9. **Kafka topic ของ mapping** (ถ้าใช้) ที่ต้องขอบริษัท · การรันบน Kubernetes (อ่าน folder บน share, worker ทำงานตลอด)
