# Mapping Demo

โปรเจกต์สาธิตการนำข้อมูล CSV เข้า Source Table และแปลงเป็น Normalized Table ผ่าน Kafka

**สถานะ:** เริ่มสร้าง solution แล้ว โดย API มี Watcher เป็น hosted service; Worker และ Docker Compose ยังอยู่ในแผน

## เอกสารหลัก

- [แผน Demo](docs/demo-plan.md)
- [Sequence Diagram — draw.io](docs/diagrams/mapping-demo-sequence.drawio)
- [คำศัพท์ของระบบ](CONTEXT.md)

ดาวน์โหลดไฟล์ `.drawio` แล้วเปิดด้วย diagrams.net หรือ draw.io Desktop เพื่อดูและแก้ไข diagram
หน้า 01 Main Flow เรียง lifeline ซ้าย→ขวาตามลำดับงาน: Input Folder → Watcher → Kafka file → Import → Source/Outbox → Dispatcher → Kafka row → Normalize → Normalized → Archive; ตามด้วยหน้า retry/reprocess และ configuration

## Flow ที่ตกลงไว้

```text
Partner folder → Watcher → Kafka (1 file = 1 job)
  → Import Worker delay 30 วินาที
  → Source Table: field จากไฟล์เป็น string
      → Outbox → Kafka (1 row = 1 job)
          → Normalize → Normalized Table / Row Error
  → Archive หลังนำเข้า Source ครบ
```

Normalize เริ่มทำงานได้ตั้งแต่แต่ละ Source row commit โดยไม่ต้องรอนำเข้าทั้งไฟล์จบ
การหน่วง 30 วินาทีเป็นเวลารอตายตัว ไม่ได้ยืนยันว่า partner เขียนไฟล์เสร็จแล้ว

## โครงสร้างที่วางแผน

- **Config API:** ASP.NET Core Web API จัดการ mapping ทั้งสองขั้น รวมการสร้างตารางและเพิ่มคอลัมน์
- **File Watcher:** hosted service ใน API เฝ้าดู folder ที่ผูกกับ mapping config และส่งงานไฟล์
- **Mapping Worker:** import, outbox dispatcher และ normalize ภายใน process เดียว
- **Infrastructure:** Kafka และ PostgreSQL บน Docker; .NET ทั้ง 2 process (API และ Worker) รันบนเครื่องเพื่อ debug

ใช้ config version ที่ตรึงเมื่อรับไฟล์ พร้อมป้องกันข้อมูลซ้ำ รองรับ retry ด้วย version เดิม และ reprocess ด้วย version ที่เลือก

## เอกสารบริบทเดิม

- [Current Program Workflow](current-program-workflow.md)
- [Mapping API Modernization Proposal](plan-mapping-api-modernization.md)

เอกสารสองฉบับนี้เป็นบริบทระบบเดิมและข้อเสนอเดิม รายละเอียด demo ปัจจุบันให้ยึดแผน Demo
ลิงก์ไปโค้ดหรือไฟล์ตัวอย่างในเอกสารเดิมอาจอ้างถึงไฟล์ที่ไม่ได้อยู่ใน repository นี้
