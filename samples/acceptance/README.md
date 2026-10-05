# Acceptance-test fixtures

ไฟล์ในโฟลเดอร์นี้เป็นต้นฉบับสำหรับทดสอบเกณฑ์ตรวจรับ Phase 18 อย่าแก้หรือย้ายไฟล์ต้นฉบับ ให้ใช้ `Copy-Item` คัดลอกไปที่ `input/orders` ตาม [คู่มือทดสอบ](../../docs/acceptance-test-guide.md)

| ไฟล์ | ใช้ทดสอบ |
|---|---|
| `01-orders-a-invalid-2.csv` | 100 records; แถว 25 วันที่ผิด และแถว 75 decimal ผิด |
| `01-orders-b-valid.csv` | 100 records ที่ถูกต้อง; ใช้ทดสอบ overlap กับไฟล์ A |
| `02-outbox-restart.csv` | หยุด/เริ่ม Worker ระหว่าง import และส่ง outbox ซ้ำ |
| `03-partial-retry.csv` | fault ที่แถว 50 แล้ว retry โดยไม่สร้าง Source ซ้ำ |
| `04-version-pinning.csv` | วันที่รูปแบบ `dd/MM/yyyy` สำหรับ pinning และ reprocess v1/v2/v3 |
| `05-duplicate-original.csv` / `05-duplicate-renamed.csv` | เนื้อหา byte-for-byte เหมือนกันแต่ชื่อไฟล์ต่างกัน |
| `06-changed-original.csv` / `06-changed-after-snapshot.csv` | แทนที่ไฟล์หลัง snapshot เพื่อให้ได้ `ChangedAfterRead` |
| `07-normalize-failure.csv` | 20 records สำหรับ fault ที่ row 10 แล้ว retry row job |

ทุกไฟล์ใช้ header เดียวกับ config ที่สร้างโดย `scripts/configure-demo.ps1`
