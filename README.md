# Mapping Demo

โปรเจกต์สาธิต pipeline นำข้อมูล CSV เข้า Source Table แล้ว normalize ผ่าน Kafka โดยมี PostgreSQL เก็บ config, job, outbox และผลลัพธ์

```text
input/ → API (File Watcher) → outbox → Worker → Kafka
          Source rows ← File Import ←┘       └→ Row Normalize → Normalized rows
```

Source เก็บค่าจาก CSV เป็นข้อความก่อน จากนั้นแต่ละแถวถูกส่งผ่าน transactional outbox ไป normalize โดยไม่ต้องรอให้ทั้งไฟล์ import เสร็จ รองรับ duplicate detection, retry และ reprocess ด้วย config version ที่เลือก

## สิ่งที่ต้องมี

- .NET 8 SDK (โปรเจกต์กำหนด SDK `8.0.400` และอนุญาต latest feature roll-forward)
- Docker Desktop พร้อม Docker Compose
- PowerShell 7 (`pwsh`)

## รันเดโมจากสถานะว่าง

คำสั่งทั้งหมดให้รันจาก root ของ repository และเปิด API กับ Worker คนละ terminal

1. รีเซ็ตข้อมูล เปิด PostgreSQL/Kafka/Kafka UI และสร้าง topics:

   ```powershell
   pwsh ./scripts/reset-demo.ps1
   ```

   คำสั่งนี้ลบ Docker volumes ของเดโมและล้าง `input/`, `archive/`, `data/` จึงควรหยุด API/Worker ก่อนใช้

2. เปิด API ซึ่งจะ apply migrations และเริ่ม File Watcher ด้วย:

   ```powershell
   dotnet run --project src/MappingDemo.Api --launch-profile http
   ```

   ตรวจ health ได้ที่ <http://localhost:5181/health> และดู API reference ที่ <http://localhost:5181/scalar/v1>

3. จาก terminal ใหม่ สร้าง Source/Normalized schema, mapping config และ version ผ่าน API:

   ```powershell
   pwsh ./scripts/configure-demo.ps1
   ```

4. เปิด Worker ใน terminal อีกหน้าหนึ่ง:

   ```powershell
   dotnet run --project src/MappingDemo.Worker
   ```

5. รอประมาณ 10 วินาทีให้ Watcher โหลด config แล้วสร้างไฟล์ตัวอย่างลง `input/orders`:

   ```powershell
   pwsh ./scripts/generate-sample.ps1
   ```

   `orders-a.csv` มี 100 records โดยตั้งใจให้แถวข้อมูล 25 เป็นวันที่ผิดและแถว 75 เป็นจำนวนเงินผิด จึงควรได้ Source 100, Normalized 98 และ 2 row errors ส่วน `orders-b.csv` มี 100 records ที่ถูกทั้งหมด หลัง import ไฟล์จะย้ายไป `archive/<ddMMyyyy>/<file-name>` โดยสร้างโฟลเดอร์วันที่เมื่อยังไม่มีและใช้โฟลเดอร์เดิมเมื่อมีอยู่แล้ว

6. ดูสถานะผ่าน API:

   ```text
   GET http://localhost:5181/file-jobs
   GET http://localhost:5181/file-jobs/{id}
   GET http://localhost:5181/file-jobs/{id}/errors
   GET http://localhost:5181/file-jobs/{id}/history
   ```

Kafka UI อยู่ที่ <http://localhost:8080> การ import จะรอ 30 วินาทีตาม `Import:DelaySeconds`; ช่วงทดสอบสามารถ override ได้ เช่น `dotnet run --project src/MappingDemo.Worker -- Import:DelaySeconds=1`

## สคริปต์ช่วยเดโม

- `scripts/reset-demo.ps1` — ล้าง state และเริ่ม infrastructure ใหม่
- `scripts/create-topics.ps1` — สร้าง Kafka topics แบบเรียกซ้ำได้
- `scripts/configure-demo.ps1` — สร้าง schema/config ตัวอย่างผ่าน API หลัง reset
- `scripts/generate-sample.ps1` — สร้าง CSV สองไฟล์; ใช้ `-OutputDirectory` เพื่อเลือกปลายทางอื่นได้

## เอกสาร

- [Learning plan](docs/learning-plan.md) — ขั้นตอนลงมือและเกณฑ์ตรวจรับ
- [Acceptance test guide](docs/acceptance-test-guide.md) — ไฟล์ทดสอบและวิธีพิสูจน์เกณฑ์ตรวจรับทุกข้อทีละขั้น
- [Demo plan](docs/demo-plan.md) — ขอบเขตและการตัดสินใจของเดโม
- [Sequence diagram](docs/diagrams/mapping-demo-sequence.drawio) — เปิดด้วย diagrams.net หรือ draw.io Desktop
- [Domain context](CONTEXT.md) — คำศัพท์ของระบบ

รายละเอียด demo ปัจจุบันให้ยึด learning plan และ demo plan เอกสารบริบทระบบเดิมคือ [Current Program Workflow](current-program-workflow.md) และ [Mapping API Modernization Proposal](plan-mapping-api-modernization.md)
