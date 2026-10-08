# Notes

## ความชอบของผู้เรียน
- ใช้ภาษาไทย ศัพท์เทคนิคคงเป็นภาษาอังกฤษตามโค้ด
- ต้องการเรียนจากโปรเจกต์ตัวเอง (mapping-demo) ไม่ใช่ตัวอย่างลอย ๆ
- **ระดับภาพรวม** เน้น worker กับ queue (2026-10-08) — แผนภาพและบทบาทมาก่อนโค้ด, ไม่ลงลึก offset/commit/partition เว้นแต่ผู้เรียนถาม
- สำนวนตาม /no-ai-slop (2026-10-08): ไม่ใช้ em dash (—) ในเนื้อหา, ไม่ใช้ "คำนาม: เฉลย", ไม่ใส่ตัวหนากลางประโยค, ไม่มีประโยคชี้นำแบบ "สังเกตว่า/นี่คือประโยชน์หลัก", ข้ออ้างกว้าง ๆ ต้องมีแหล่งหรือตัดทิ้ง
- ศัพท์ technical ใช้ภาษาอังกฤษตามเดิม ไม่แปลและไม่ทับศัพท์ (2026-10-08): เช่น "เปิดเป็น open source", "feature", "cloud service", "background service", "background job", "process"
- ภาษาไทยต้องเป็นธรรมชาติ ไม่ใช่สำนวนแปล (2026-10-08): ใช้ "สิ่งที่จะได้หลังจากจบบทนี้:" แทน "จบบทนี้คุณจะทำได้:", ป้ายในไทม์ไลน์ใช้ "concept ที่ได้:" (ผู้เรียนเลือกเอง), เลี่ยง passive "ถูก…" (ถูกทำ/ถูกส่ง/ถูกนับเป็น) ยกเว้นความหมายเชิงลบที่คนไทยพูดกันจริง (ถูกลบ, ถูกข้าม), เลี่ยง "ใช้มัน", "ภาษาคน", "ซิม"
- ลำดับบท: **1. ปัญหาเกิดจากอะไร → 2. ที่มาของ concept (ไล่คำถามที่ตามมาจากปัญหา แต่ละคำตอบคือ concept หนึ่งข้อ) → 3. concept ที่ใช้แก้ → 4. mapping-demo ใช้อย่างไร** (แก้ไข 2026-10-08) "ที่มา" หมายถึงปัญหาเกิดจากอะไร ไม่ใช่ประวัติศาสตร์ ห้ามทำไทม์ไลน์ปี ค.ศ./ใครคิดค้น ใส่ลิงก์อ้างอิง concept ได้แต่ไม่ต้องใส่ปี (บทที่ 1 เป็นบทประวัติตามคำขอเดิม ยังไม่ได้ถามว่าจะปรับไหม)

## Components (assets/)
- `step-diagram.js` แผนภาพ flow ที่ผู้เรียนกดเดินทีละขั้น (ไม่เล่นอัตโนมัติ) ใช้กับทุกจุดที่มีการเคลื่อนที่/ลำดับเหตุการณ์ (ผู้เรียนขอ 2026-10-08: text เยอะทำให้หลุด focus)
- `job-timeline.js` เล่น snapshot ของ status API + `data-states` ไฮไลต์สถานะปัจจุบัน
- `queue-sim.js`, `quiz.js`; config ของแผนภาพบทที่ 1–4 สร้างจากสคริปต์ใน scratchpad (diagrams.py) แก้ JSON ใน HTML ตรง ๆ ได้

## ข้อเท็จจริงของโค้ดที่บทเรียนอ้าง (ตรวจแล้ว 2026-10-08)
- Topics: `mapping.file-import`, `mapping.row-normalize` (สร้างโดย `scripts/create-topics.ps1`, 3 partitions, RF 1, auto-create ปิด)
- file-import: key = fileJobId, payload `{"fileJobId":N}`; group `mapping-file-import`
- row-normalize: key = sourceRowId, payload `{"rowJobId":N}`; group `mapping-row-normalize`
- ผู้เขียน outbox: `FileIntake` (API), `FileJobService` retry/reprocess (API), `RowJobService` retry (API), `FileImportHandler` (Worker)
- ผู้ produce ไป Kafka จริงมีตัวเดียว: `OutboxDispatcher` (Worker, poll ทุก 500ms, ทีละ 100)
- Consumer: `EnableAutoCommit=false`, commit หลัง handler, `AutoOffsetReset=Earliest`; FileImportConsumer `Pause` partition เมื่อ handler คืน false
- `Import:DelaySeconds` ใน appsettings ตอนนี้ = 1 (README อ้าง 30)
- สถานะ: import Queued→Delaying→Importing→Imported|ImportFailed|Duplicate (Retry: ImportFailed→Queued); archive NotArchived→Archived|ArchiveFailed|ChangedAfterRead; normalization คำนวณจาก row job ล่าสุด; row job Pending→Done|Invalid|Failed (Failed หลังลอง 4 รอบ เว้น 1,2,4 วินาที); handler ข้าม row job ที่ terminal แล้ว
- Idempotency: Source `ON CONFLICT (file_job_id,row_number) DO NOTHING`; Normalized upsert ตาม source_row_id (เฉพาะ Success); unique index pending row job ต่อ source_row; Retry file ใช้ snapshot เดิม; archive retry 3 รอบ เว้น 1 วินาที; `FileImportHandler` ไม่มีทางคืน false จริง (Pause path ไม่ถูกใช้)
- Consumers ต่อ topic: `Kafka:FileImportConsumers`/`Kafka:NormalizeConsumers` (= 1 ใน appsettings.Development.json, เดิมเป็น 3) แต่ละตัวเป็น Kafka consumer แยกใน thread `LongRunning` ใน Worker process เดียว; import หน่วงด้วย `Task.Delay` ใน handler จึงบล็อก consumer ตัวนั้น
- `OutboxDispatcher` SELECT `sent_at IS NULL` โดยไม่ล็อก ถ้าเปิด Worker 2 process อาจส่ง message ซ้ำ (บทที่ 5 พูดถึงสั้น ๆ, ใช้ต่อในบทที่ 6)
- acceptance-test-guide A1 ยังเขียนว่า `Import:DelaySeconds` = 30 ถ้าไม่ override แต่ appsettings.Development.json ตั้งไว้ 1 และ A3 คาดว่า A/B ทำพร้อมกัน ซึ่งต้องใช้ FileImportConsumers > 1
- โปรเจกต์ API ไม่อ้าง Confluent.Kafka เลย (มีแค่ Worker); `OutboxWriter.AddAsync` บังคับรับ transaction; ไม่มีโค้ดลบแถว outbox ที่ส่งแล้ว; dispatcher produce ก่อนแล้วจึง set `sent_at` (ดับตรงกลาง = ส่งซ้ำ), `ProduceException` → `break`
- `GET /file-jobs` เรียง id DESC; endpoint history คือ `/file-jobs/{id}/rows/{rowNumber}/history` (README เขียนว่า `/history` ซึ่งไม่ตรง)

## แผนบทเรียนคร่าว ๆ (ระดับภาพรวม; ปรับได้ตาม learning records)
1. ที่มาและ concept ของ queue (Erlang → MQSeries → EIP/AMQP → SQS → Resque → Kafka; 5 concept) ✅
2. Queue กับ Worker ใน mapping-demo + เฉลย concept → โปรเจกต์ ✅
3. Lifecycle ของ file job: concept 06 job record, 07 terminal/non-terminal, 08 status endpoint + polling; ไทม์ไลน์ orders-a ✅
4. เมื่อมีอะไรพัง: concept 09 delivery semantics, 10 idempotent receiver, 11 transient/permanent, 12 งานพังถาวรให้คนตัดสินใจ; ไทม์ไลน์ Worker ดับกลาง import (B2); Retry vs Reprocess ✅
5. หลาย worker ช่วยกัน: concept 13 partition, 14 consumer group, 15 message key, 16 rebalance (03 competing consumers มีแล้วในบท 1–2); ไทม์ไลน์ไฟล์ A/B กับ consumer 1 vs 3 ✅
6. กล่องจดหมายขาออก (outbox): concept 17 dual write, 18 transactional outbox, 19 message relay (polling publisher), 20 outbox ส่งซ้ำได้ → ฝั่งรับ idempotent; ไทม์ไลน์ import ทีละแถวทำให้ normalize เริ่มก่อน import จบ ✅ (จบแผน 6 บท)
