# AGENTS.md — mapping-demo · workspace เอกสารของ mapping

ไฟล์นี้เป็นกฎสำหรับคน + AI agent (ChatGPT / Codex อ่าน `AGENTS.md` · Claude Code อ่านผ่าน `CLAUDE.md`) · อ่านก่อนเริ่มงานทุกครั้ง · **ถ้าใช้ ChatGPT แบบแชท (ไม่ได้อ่านไฟล์เอง) ให้แนบไฟล์นี้ + เอกสารที่เกี่ยวกับงานไปด้วยทุกครั้ง**

## repo นี้คืออะไร

**workspace เอกสารของ mapping phase 1** — ที่เก็บขอบเขต ศัพท์ กฎ contract แผน และความคืบหน้าของงาน mapping · **โค้ดแยกทำในแต่ละ repo** (ดูตารางข้างล่าง) แต่เปิดและแก้จาก workspace นี้ได้

ทำไมเป็นแบบนี้: เอกสารกลางของโปรเจค Insurance Validation อยู่ใน repo `insurance-validation` ซึ่ง Dev 2 เข้าไม่ได้ · Dev 1 จึงคัดส่วนที่ mapping ต้องใช้มาไว้ที่นี่ และให้ repo นี้ทำหน้าที่เดียวกับ `insurance-validation` ของ Dev 1 (เอกสารอยู่ที่เดียว · repo โค้ดมีแต่โค้ด)

- เปิดงานด้วย **`mapping-demo.code-workspace`** — รวม repo เอกสาร (นี้) + repo โค้ดไว้ในหน้าต่างเดียว · repo โค้ดต้องอยู่ folder ข้างกัน (`../prjInsuranceValidationMapping` ฯลฯ)
- โค้ด demo เดิม (`src/`, `tests/`, `web/`, `scripts/`, `samples/`, `requests/`, `docs/phase-*`, `spec/`, `specs/`) = **reference เท่านั้น ไม่พัฒนาต่อ** — ยกโค้ดไปใช้ได้ แต่ต้องจัดให้เข้าโครงของ repo Mapping (D-77)

## repo ที่ทำงานจาก workspace นี้

| repo | path | ลักษณะ | แก้ได้ไหม |
|---|---|---|---|
| **prjInsuranceValidationMapping** | `../prjInsuranceValidationMapping` | **service Mapping** (.NET 10, schema `mapping`, :5177) — อ่านไฟล์ partner → JSON `pa-inform/1` → ส่งเข้า Informs · Dev 1 ทำโครงไว้ให้แล้ว โค้ดข้างใน Dev 2 ทำ | ✅ แก้ + commit ได้ |
| **prjInsuranceValidationWeb** | `../prjInsuranceValidationWeb` | หน้าเว็บ (Angular 21 + PrimeNG 21) — **Dev 2 ทำเฉพาะหน้า mapping** `src/app/features/mapping/` + `core/mapping-api.ts` · ส่วนอื่น (UW / Config) เป็นของ Dev 1 | ✅ เฉพาะส่วน mapping ตามกฎ `CLAUDE.md` ของ Web |
| **prjInsuranceValidationInforms** | `../prjInsuranceValidationInforms` | service ที่รับใบคำขอ (ของ Dev 1) — มี **JSON Schema ตัวจริง** ของ contract (`src/Informs/Informs.Api/Contracts/PaInform/pa-inform-1.schema.json`) และเป็นตัวอย่างของ worker / outbox / Kafka / HTTP client ที่ Mapping ต้องทำแบบเดียวกัน | ❌ อ่านอย่างเดียว |
| mapping-demo (repo นี้) | `.` | เอกสาร (`docs/mapping-phase-1/`) + โค้ด demo (reference) | เอกสาร ✅ · โค้ด demo ไม่แก้ |

## แก้โค้ดจาก workspace นี้

- **อ่าน `CLAUDE.md` / `AGENTS.md` ของ repo นั้นก่อนแก้โค้ด** — โครงและวิธีเขียนโค้ดอยู่ที่นั่น (โครงต้องเหมือน Products / Informs — D-77)
- **ก่อนแก้ source code ทุกครั้ง: บอกว่าจะแก้อะไร (ไฟล์ไหน แก้ / สร้าง / ลบอะไร) แล้วรอยืนยันก่อน** — ใช้คำยืนยันของครั้งก่อนแทนไม่ได้
- **commit เมื่อสั่งเท่านั้น** (ทั้ง repo โค้ดและ repo นี้) · ก่อน commit repo Mapping ต้องผ่าน `./scripts/check.ps1`
- **แก้เอกสาร** ใน `docs/mapping-phase-1/` ส่วนที่ Dev 2 ดูแล **ไม่ต้องถามก่อน**
- **ทุกการแก้ (โค้ดหรือเอกสาร) ต้องสรุปตอนท้ายของคำตอบ ว่าแก้อะไร ที่ไฟล์ไหน**
- รัน build / test / `scripts/check.ps1` / `scripts/up.ps1` ได้เลย · **คำสั่งที่ลบ / แก้ DB ของบริษัทให้คนรันเอง** · DB ใน docker บนเครื่อง (port 5433) แก้ได้
- **จบ task ใน repo โค้ด → อัปเดต `docs/mapping-phase-1/plan.md`** (สถานะ + หลักฐาน — ดู §แผนและความคืบหน้า)
- repo โค้ด**ไม่มีเอกสาร** — ห้ามสร้าง `docs/` / `.docs/` ใน repo Mapping หรือ Web · โค้ด / test / script ห้ามอ้าง path ของ repo นี้

## Directory layout

```
mapping-demo/
├── AGENTS.md                      ← ไฟล์นี้ (CLAUDE.md ชี้มาที่นี่)
├── mapping-demo.code-workspace    ← เปิด workspace (เอกสาร + repo โค้ด)
├── docs/mapping-phase-1/          ← **แหล่งความจริงของ mapping phase 1**
│   ├── README.md                  ← ขอบเขต วันสำคัญ (Dev 1)
│   ├── glossary.md                ← ศัพท์ร่วมของโปรเจค (Dev 1)
│   ├── rules.md                   ← กฎธุรกิจ BR-xxx + การตัดสินใจ D-xx + คำถาม kickoff (Dev 1)
│   ├── contract-pa-inform.md      ← contract ที่ mapping ส่งเข้า Informs (Dev 1)
│   ├── mtls-file.md               ← รูปแบบไฟล์ MTLS + ข้อระวังตอนแปลงค่า (Dev 1)
│   ├── web.md                     ← หลักร่วมของหน้าเว็บ + กฎ repo Web (Dev 1)
│   ├── plan.md                    ← **ไฟล์กลาง Dev 1 + Dev 2**: แผน (Dev 2 เขียน) + ความคืบหน้า + M-xx + คำถาม
│   └── <ไฟล์ออกแบบอื่น>.md         ← Dev 2 เพิ่มได้ (data model, sequence, หน้าเว็บ ฯลฯ)
├── CONTEXT.md                     ← ศัพท์ภายในของ mapping (Source Table, Config Version, …) — Dev 2
└── src/ tests/ web/ docs/phase-* spec/ …   ← โค้ด + เอกสาร demo เดิม (reference)
```

## ใครแก้อะไร

| ไฟล์ | ใครแก้ | หมายเหตุ |
|---|---|---|
| `AGENTS.md`, `CLAUDE.md`, `mapping-demo.code-workspace`, `docs/mapping-phase-1/{README,glossary,rules,contract-pa-inform,mtls-file,web}.md` | **Dev 1** (คัดจากเอกสารกลาง) | Dev 2 **ห้ามแก้เอง** — ถ้าเห็นว่าผิด / ขาด / ขัดกับโค้ด ให้เขียนลงหัวข้อ "คำถาม / ข้อเสนอถึง Dev 1" ใน `plan.md` แล้วบอก Dev 1 |
| `docs/mapping-phase-1/plan.md` | **ทั้งคู่** — ไฟล์กลาง | ส่วนไหนใครเขียน ดูตารางหัวไฟล์ · Dev 2 วางแผน + อัปเดตความคืบหน้า · Dev 1 ตามงาน + ตอบคำถาม |
| ไฟล์ออกแบบอื่นในโฟลเดอร์นี้ | **Dev 2** | |
| `CONTEXT.md` | Dev 2 | ต้อง**ไม่ขัด**กับ `glossary.md` |

## Workflow

1. **เริ่มงาน** — pull repo นี้ · อ่าน `plan.md` (§1 อัปเดตล่าสุด, §5 คำถามที่ Dev 1 ตอบ / ถามมา) + `rules.md` ข้อที่เกี่ยว
2. **ออกแบบ** — เรื่องที่ต้องคิดก่อนเขียนโค้ด (ตาราง, รูปแบบ config, หน้าเว็บ) เขียนเป็นไฟล์ใน `docs/mapping-phase-1/` พร้อมตัวอย่างข้อมูล · ตัดสินแล้วบันทึก `M-xx` ใน `plan.md`
3. **เรื่องที่กระทบ contract / เส้นแบ่งงาน / ขอบเขต / วันส่งงาน** → เขียนเป็นข้อเสนอใน `plan.md` แล้วตกลงกับ Dev 1 ก่อนแก้โค้ด — Dev 1 ลงผลเป็น `D-xx` ใน `rules.md`
4. **เขียนโค้ด** ใน repo Mapping / Web ตาม §แก้โค้ดจาก workspace นี้
5. **จบ task** → ติ๊ก bullet ใน `plan.md` §3 + เพิ่มรายการใน §1 → commit + push

## ภาษาและรูปแบบเอกสาร

- เนื้อหาภาษาไทย · ศัพท์เทคนิค / ชื่อระบบ / ชื่อ field ใช้ภาษาอังกฤษ
- **ภาษาธรรมดา** — ไม่ใช้คำย่อไทยปนอังกฤษที่คนนอกไม่รู้ · ศัพท์เทคนิคต้องอธิบายตอนใช้ครั้งแรก
- เอกสารใหม่ทุกไฟล์มี frontmatter:
  ```yaml
  ---
  title: <ชื่อเอกสาร>
  status: draft | review | approved
  last_updated: YYYY-MM-DD
  ---
  ```
- ข้อเสนอการออกแบบต้องมี**ตัวอย่างข้อมูล** (แถวในไฟล์ → JSON ที่ได้ / แถวในตาราง) — อธิบายด้วยตัวอย่างดีกว่าคำอธิบายยาว
- diagram ใช้ **Mermaid** ใน `.md` เป็นค่าเริ่มต้น
- อ้างกฎและการตัดสินใจด้วย ID: `BR-xxx` (กฎธุรกิจ), `D-xx` (การตัดสินใจของโปรเจค), `M-xx` (การตัดสินใจภายใน mapping) · ห้ามคัดเนื้อหากฎมาเขียนใหม่เป็นอีกแบบ
- ข้อมูลทดสอบ / ตัวอย่างในเอกสาร **ห้ามใช้ข้อมูลส่วนบุคคลจริง** (D-70) — ใช้ไฟล์ทดสอบ 2,556 แถวที่ Dev 1 ให้

## ศัพท์ที่ต้องใช้ให้ถูก (เต็มใน [`glossary.md`](docs/mapping-phase-1/glossary.md))

ใช้ทุกที่ ทั้งเอกสาร ชื่อ class / field / ตาราง / route:

- **inform** = ใบคำขอ 1 ใบ · ห้ามใช้ `proposal` ในงานใหม่ (เอกสาร demo เก่ายังใช้ ให้อ่านเป็น inform)
- **product group** (`PA`) → **contract type** (`PAC`) → **product** (`MTLSPA`) → **package** (`MTL2`) · ห้ามใช้คำ `campaign` ในงานใหม่
- **partner code** (`MTLS`) ≠ channel · **sale channel** = `FILE` / `MAIL` / `ONLINE`
- **issue** = ออกกรมธรรม์เท่านั้น · ผลตรวจเรียก **validation message**
- **relation** (`PRIMARY` / `SPOUSE` / `CHILD`) ≠ **role** (`INSURED` / `POLICYHOLDER`)
- **risk ของใบ** (1 คนใน `insureds[]`) ≠ **Risk** (`PERSON` — สิ่งที่เอาประกัน) ≠ **PackageRisk** (config ของ package)

เจอคำใหม่ที่ทั้งโปรเจคต้องใช้ร่วม → เสนอใน `plan.md` ให้ Dev 1 เพิ่มใน glossary · คำที่ใช้แค่ข้างใน mapping → เพิ่มใน `CONTEXT.md` ทันที

## การตัดสินใจ

| เรื่อง | ใครตัดสิน | บันทึกที่ไหน |
|---|---|---|
| contract `pa-inform/1`, เส้นแบ่งงาน mapping / Informs / Client API (D-06), ขอบเขต phase 1, วันส่งงาน | **ต้องตกลงกับ Dev 1 ก่อน** — Dev 2 เสนอใน `plan.md` | Dev 1 ลง `rules.md` เป็น `D-xx` |
| เรื่องภายใน mapping: ตารางใน schema `mapping`, วิธีอ่านไฟล์, retry / ความขนานตอนส่ง, โครงของ config mapping, หน้าเว็บ mapping | **Dev 2 ตัดสินเองได้** | `plan.md` หัวข้อ "การตัดสินใจของ mapping" เลข `M-01`, `M-02`, … |

รูปแบบการบันทึก (เหมือน `D-xx` ของโปรเจค):

```markdown
### M-01 <หัวข้อสั้น>
- **ตัดสิน (YYYY-MM-DD):** ...
- **ทำไม:** ...
- **ไม่เลือก:** <ทางอื่นที่คิดไว้ + เหตุผล>
- **ห้าม:** (ถ้ามี)
```

- **ห้ามเดาข้อมูลธุรกิจ** — ไม่แน่ใจให้เขียนเป็นคำถามใน `plan.md` แล้วถาม Dev 1
- กฎธุรกิจที่เจอใหม่ (จากไฟล์ partner, legacy, ผู้ใช้) → เสนอพร้อม**เหตุผล + ที่มา** · ถ้ามีหลักฐานจาก partner เดียว ให้ถือเป็นกฎของ partner นั้น (เขียนว่า "อาจเป็นกฎกลาง?") ห้ามเดาว่าเป็นกฎกลาง

## แผนและความคืบหน้า — [`plan.md`](docs/mapping-phase-1/plan.md) (ไฟล์กลาง Dev 1 + Dev 2)

**ไฟล์เดียวที่ทั้งสองคนเขียน** — Dev 2 วางแผนงาน mapping เองแล้วเขียนเป็น bullet ที่นี่ · Dev 1 ตามงานจากไฟล์นี้ไฟล์เดียว · กติกาเต็ม (ส่วนไหนใครเขียน, รูปแบบ bullet, กันชนกัน) อยู่หัวไฟล์ plan.md — ย่อ:

- **§1 อัปเดตล่าสุด** — Dev 2 เพิ่มรายการวันที่ไว้บนสุด **ทุกวันที่ทำงาน** หรือทุกครั้งที่ปิดงาน: เสร็จ · กำลังทำ · ติด / รอ · ต่อไป
- **§2 จุดสังเกต** — วันที่จาก Dev 1 · Dev 2 อัปเดตสถานะ · จะเลื่อนต้องคุยกับ Dev 1 ก่อน
- **§3 งาน** — Dev 2 แตกงานเป็น bullet `- [ ] **M1.1** <งาน> — เสร็จเมื่อ: <ตรวจได้> · เป้า MM-DD` · ติ๊ก `[x]` เมื่อมีหลักฐาน (repo + commit / ผลทดสอบ) · ห้ามเปลี่ยน / ลบ ID (ตัดงาน = ขีดฆ่า + เหตุผล)
- **§4 การตัดสินใจของ mapping** (`M-xx`) — Dev 2
- **§5 คำถาม / ข้อเสนอ** — ทั้งคู่ · คำถามธุรกิจ, ข้อเสนอแก้ glossary / rules / contract, เรื่องที่ต้องตกลงร่วม
- **pull ก่อนแก้ · commit + push ทันทีหลังแก้** · ห้ามลบ / เขียนทับข้อความของอีกคน

## Git

- repo นี้: commit message แบบ conventional commits เหมือนที่ใช้อยู่ เช่น `docs(mapping-phase-1): ...` · branch `develop`
- repo โค้ด: ตามกฎใน `CLAUDE.md` ของ repo นั้น
- อัปเดต `plan.md` ในวันเดียวกับ commit ที่ปิด task
