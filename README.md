# Mapping Demo

โปรเจกต์สาธิต pipeline นำข้อมูล Delimited/CSV หรือ UTF-8 fixed-width เข้า Source Table แล้ว normalize ผ่าน Kafka โดยมี PostgreSQL เก็บ config, job, outbox และผลลัพธ์

```text
input/ → API (File Watcher) → outbox → Worker → Kafka
          Source rows ← File Import ←┘       └→ Row Normalize → Normalized rows
```

Source เก็บค่าดิบจาก Input Layout เป็นข้อความก่อน จากนั้นแต่ละแถวถูกส่งผ่าน transactional outbox ไปประเมิน Transformation Program โดยไม่ต้องรอให้ทั้งไฟล์ import เสร็จ รองรับหลาย output branches, named transform/lookup, duplicate detection, retry และ reprocess ด้วย config version ที่เลือก

## สิ่งที่ต้องมี

- .NET 8 SDK (โปรเจกต์กำหนด SDK `8.0.400` และอนุญาต latest feature roll-forward)
- Docker Desktop พร้อม Docker Compose
- PowerShell 7 (`pwsh`)
- Node.js 20.9 ขึ้นไปและ npm สำหรับหน้าเว็บ Next.js

## รันเดโมจากสถานะว่าง

คำสั่งทั้งหมดให้รันจาก root ของ repository โดยใช้ terminal แยกสำหรับ API, Worker และ Web

1. หยุด API, Worker และ Web ที่รันจาก repo นี้ด้วย `Ctrl+C` ก่อน reset แล้วจึงล้างข้อมูล เปิด PostgreSQL/Kafka/Kafka UI และสร้าง topics:

   ```powershell
   pwsh ./scripts/reset-demo.ps1
   ```

   คำสั่งนี้ลบ Docker volumes ของเดโมและล้าง `input/`, `archive/`, `data/` แบบกู้คืนไม่ได้ อย่าใช้กับข้อมูลที่ต้องเก็บ

2. เปิด API ซึ่งจะ apply migrations และเริ่ม File Watcher ด้วย:

   ```powershell
   dotnet run --project src/MappingDemo.Api --launch-profile http
   ```

   ตรวจ health ได้ที่ <http://localhost:5181/health> และดู API reference ที่ <http://localhost:5181/scalar/v1>

3. จาก terminal ใหม่ สร้าง Source/Normalized schema และ Config Version ตัวอย่างสำหรับ direct CSV กับ PMIB lookup ผ่าน API:

   ```powershell
   pwsh ./scripts/configure-demo.ps1
   ```

4. เปิดหน้าเว็บใน terminal ใหม่:

   ```powershell
   Set-Location ./web
   npm ci
   npm run dev
   ```

   เปิด <http://localhost:3000/configs> เว็บส่ง API ผ่าน same-origin `/api` ไป `http://localhost:5181` ตามค่าเริ่มต้นใน `next.config.ts`; ถ้า API อยู่ URL อื่นให้คัดลอก `web/.env.local.example` เป็น `web/.env.local` แล้วแก้ `API_BASE_URL` หากพอร์ต 3000 ถูกใช้อยู่ให้รัน `npm run dev -- --port 3001` และเปิด URL ตามพอร์ตนั้น

5. เปิด Worker ใน terminal อีกหน้าหนึ่งจาก root ของ repository:

   ```powershell
   dotnet run --project src/MappingDemo.Worker
   ```

6. รอประมาณ 10 วินาทีให้ Watcher โหลด config แล้วสร้างไฟล์ตัวอย่างลง `input/orders`:

   ```powershell
   pwsh ./scripts/generate-sample.ps1
   ```

   `orders-a.csv` มี 100 records โดยตั้งใจให้แถวข้อมูล 25 เป็นวันที่ผิดและแถว 75 เป็นจำนวนเงินผิด จึงควรได้ Source 100, Normalized 98 และ 2 row errors ส่วน `orders-b.csv` มี 100 records ที่ถูกทั้งหมด หลัง import ไฟล์จะย้ายไป `archive/<ddMMyyyy>/<file-name>` โดยสร้างโฟลเดอร์วันที่เมื่อยังไม่มีและใช้โฟลเดอร์เดิมเมื่อมีอยู่แล้ว

7. ดูสถานะผ่าน API:

   ```text
   GET http://localhost:5181/file-jobs
   GET http://localhost:5181/file-jobs/{id}
   GET http://localhost:5181/file-jobs/{id}/errors
   GET http://localhost:5181/file-jobs/{id}/rows/{rowNumber}/history
   ```

Kafka UI อยู่ที่ <http://localhost:8080> ค่า delay อ่านจาก `Import:DelaySeconds` (Development config ของ repo ตั้งไว้ 1 วินาทีเพื่อทดสอบเร็ว; baseline ทางธุรกิจคือ 30 วินาที) และ override ตอนรันได้ เช่น `dotnet run --project src/MappingDemo.Worker -- Import:DelaySeconds=30`

## สร้างและ activate Config Version ผ่านหน้าเว็บ

หลังทำขั้นตอน reset/configure และเปิด API กับ Web แล้ว:

1. เปิด `/configs`; เลือก config ตัวอย่าง หรือเปิด `/configs/new` เพื่อสร้าง Mapping Config โดยเลือก Source/Normalized tables ที่มีอยู่และกำหนด input folder ที่ไม่ซ้ำ
2. ในหน้า config เลือก Delimited หรือ Fixed width แล้วแก้ Input Layout กับ Transformation Program ผ่าน editor; รุ่นเดิมเป็น immutable การแก้จึงเป็น draft จนกว่าจะสร้างรุ่นใหม่
3. วาง sample text หรือเลือกไฟล์ UTF-8 แล้วกด `Preview with API`; preview ทำงานใน memory และไม่เพิ่ม file jobs, Source rows หรือไฟล์ runtime
4. กด `Create version`; เว็บ validate draft ก่อนบันทึก Config Version ใหม่ หากต้องการสำเนาที่ไม่แก้ไขให้ใช้ `Clone version`
5. ตรวจรุ่นใหม่ใน Version history แล้วกด `Activate`; ไฟล์ที่ Watcher รับหลังโหลด config รอบถัดไปจะ pin active version นี้ ส่วนงานเดิมไม่เปลี่ยน version ย้อนหลัง

หน้าเว็บไม่เปิดช่องให้แก้ JSON, SQL, credential, connection string หรือ physical lookup table โดยตรง Named Transform/Named Lookup เลือกได้เฉพาะ capability ที่ API ประกาศ

## สคริปต์ช่วยเดโม

- `scripts/reset-demo.ps1` — ล้าง state และเริ่ม infrastructure ใหม่
- `scripts/create-topics.ps1` — สร้าง Kafka topics แบบเรียกซ้ำได้
- `scripts/configure-demo.ps1` — สร้าง schema/config ตัวอย่างผ่าน API หลัง reset
- `scripts/generate-sample.ps1` — สร้าง CSV สองไฟล์; ใช้ `-OutputDirectory` เพื่อเลือกปลายทางอื่นได้

## Backend Config contract

Config Version เก็บเฉพาะ `inputLayout` และ `transformationProgram`; payload รุ่นเก่า `fileToSource`/`sourceToNormalized` ใช้ไม่ได้แล้ว

Phase 19 เป็น clean breaking baseline cutover สำหรับ demo นี้ ไม่มี data-preserving down migration ถ้าจะเปลี่ยนไป revision ก่อน Phase 19 หรือกลับมายัง revision ปัจจุบัน ให้หยุด API/Worker/Web, checkout revision ที่ต้องการ แล้วรัน `reset-demo.ps1` → เปิด API → `configure-demo.ps1` ใหม่ การ checkout code อย่างเดียวไม่ใช่ rollback schema และข้อมูล Config Version เดิมจะใช้ข้าม baseline ไม่ได้ หากต้องเก็บข้อมูลจริงต้อง backup และออกแบบ migration แยก; workflow นี้ไม่ใช่แนวทาง production rollback

Endpoint สำหรับหน้าเว็บมีดังนี้:

- `GET /mapping-capabilities` — catalog ของ format, encoding, operators, functions, lookups และ conversion formats
- `POST /mapping-configs/{id}/versions/validate` — compile draft โดยไม่บันทึก
- `POST /mapping-configs/{id}/versions/preview` — preview `textFile` หรือ `sourceRows` ใน memory โดยไม่บันทึก
- `POST /mapping-configs/{id}/versions` — สร้าง immutable Config Version
- `POST /mapping-configs/{id}/versions/{version}/activate` — ตรวจ dependency แล้ว activate

Preview file รับ UTF-8 text ไม่เกิน 1 MiB/50 parsed records และ request body ไม่เกิน 3 MiB; `evaluationAt` ต้องเป็น ISO 8601 ที่มี `Z` หรือ UTC offset รายละเอียด semantics และ coverage gate อยู่ใน [Backend config handoff](docs/backend-config-handoff.md)

## เอกสาร

- [Learning plan](docs/learning-plan.md) — ขั้นตอนลงมือและเกณฑ์ตรวจรับ
- [Web config plan](docs/web-config-plan.md) — แผน backend Phase 19 และหน้าเว็บ Phase 20
- [Backend config handoff](docs/backend-config-handoff.md) — contract, invariants และหลักฐานตรวจรับ Phase 19
- [Phase 19 golden cases](docs/phase-19-golden-cases.md) — semantics profile และ coverage matrix
- [Acceptance test guide](docs/acceptance-test-guide.md) — ไฟล์ทดสอบและวิธีพิสูจน์เกณฑ์ตรวจรับทุกข้อทีละขั้น
- [Demo plan](docs/demo-plan.md) — ขอบเขตและการตัดสินใจของเดโม
- [Sequence diagram](docs/diagrams/mapping-demo-sequence.drawio) — เปิดด้วย diagrams.net หรือ draw.io Desktop
- [Domain context](CONTEXT.md) — คำศัพท์ของระบบ

รายละเอียด demo ปัจจุบันให้ยึด learning plan และ demo plan เอกสารบริบทระบบเดิมคือ [Current Program Workflow](current-program-workflow.md) และ [Mapping API Modernization Proposal](plan-mapping-api-modernization.md)
