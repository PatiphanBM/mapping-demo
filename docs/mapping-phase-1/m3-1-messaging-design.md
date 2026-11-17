---
title: M3.1 — Local Kafka, outbox dispatcher และ consumer
status: review
last_updated: 2026-11-17
---

# M3.1 — Local Kafka, outbox dispatcher และ consumer

ต่อจาก outbox/Row Job ของ M-29/M-30 ตาม M-13/M-14 และโครง Informs (D-77): hosted services อยู่ Infrastructure, consumer เรียก Application ผ่าน MediatR, DB เป็นเจ้าของ job state และ Kafka commit offset หลังผล DB durable

## ขอบเขตรอบนี้

1. Local Kafka/topic initialization ใช้ image/config/ports เดียวกับ Informs: `apache/kafka:4.3.1`, host `localhost:9094`, containers ใช้ `kafka:19092`, topic default `mapping.transform.requested`, 3 partitions สำหรับ local; group prefix `insurance-validation.mapping` ปรับผ่าน configuration ได้ บริษัท broker/topics/groups/owner/auth/TLS ยังรอ Q7
2. Dispatcher อ่าน unsent outbox ในลำดับ ID ด้วย transaction + `FOR UPDATE SKIP LOCKED`, producer ใช้ idempotence/acks all; หลัง broker ยืนยันจึง mark sent ใน DB; publish แล้ว process หยุดก่อน mark sent ส่งซ้ำด้วย event identity เดิมได้
3. Consumer ของ transform topic ตรวจ message schema/key/event header และ lineage กับ Row Job; บันทึก Inbox Receipt พร้อมจัดคิว Row Job ใน transaction เดียว ก่อน commit Kafka offset
4. ขณะนี้ยังไม่มี transform worker M3.3: consumer ทำให้ `Pending` Row Job ที่ยังไม่มี `NextAt` พร้อมประมวลผลด้วย DB timestamp โดยไม่สร้าง Source/Row Job ใหม่ และไม่ถือว่าแปลงข้อมูลเสร็จ; M3.3 ต้อง claim งานที่มี `NextAt` ถึงเวลาแล้วต่อ
5. DB/transport ใช้ไม่ได้ไม่ advance offset; อ่าน record เดิมซ้ำหลัง delay; restart/rebalance/duplicate publish ต้องไม่จัดคิวงานเชิงตรรกะซ้ำหรือเลื่อน backoff เดิม
6. Event ผิดรูปแบบหรืออ้าง ID ไม่ตรงต้องมี durable diagnostic receipt โดยเก็บเฉพาะ code/coordinates/IDs ที่ปลอดภัย ไม่เก็บ raw Kafka body/headers หรือข้อความ exception ที่อาจมีข้อมูลส่วนบุคคล

## ตัวอย่าง event สมมติ

Body คง `TransformMessage` เดิมที่มี UUID เพียง 4 ตัว:

```json
{
  "rowJobId": "019a9000-0000-7000-8000-000000000001",
  "sourceRowId": "019a9000-0000-7000-8000-000000000002",
  "fileJobId": "019a9000-0000-7000-8000-000000000003",
  "configVersionId": "019a9000-0000-7000-8000-000000000004"
}
```

Kafka key คือ Row Job ID; header `mapping-event-id` คือ outbox ID เดิม เช่น `41` ไม่สร้างใหม่เมื่อ publish ซ้ำ Body ที่ส่งสร้างจาก typed allowlist หลัง validate ไม่ส่ง arbitrary JSON ใน DB ออก bus โดยตรง; ผิด topic/schema/key ให้ mark outbox ว่า blocked พร้อม safe code ไม่ปลอมว่า sent

Inbox Receipt มี unique `(consumer_group,topic,partition,offset)` พร้อม outcome/event ID/job ID/created time; ไม่เก็บ body การรับ record เดิมซ้ำคืนผลเดิม ส่วน event เดิมที่ publish ซ้ำเป็น offset ใหม่ก็ไม่เปลี่ยน `NextAt`/row version ของงานที่จัดคิวแล้ว และไม่ reset งาน running/completed/blocked หรือ backoff ที่มีอยู่

## เส้นแบ่งกับ M3.3 และแบบเดิม

- ยังไม่รัน compiler/evaluator หรือสร้าง Delivery Payload/HTTP ใน M3.1; durable wake-up กับการประมวลผลแยกกันและต้องมีผลตรวจคนละชุด ไม่ทำ handler ที่ตอบ success โดยไม่บันทึก intent
- รับเข้า DB ก่อน commit offset ตาม pattern Informs; เพิ่ม Inbox Receipt เพราะ Mapping รอบนี้จัดคิว Row Job ต่อไปยัง M3.3 แทนการรัน stage จนเสร็จใน consumer ของ Informs
- outbox ที่ implement ใน M-29 ใช้ ordered identity + typed IDs-only JSON ตาม Informs; รอบนี้คง shape นั้นแล้วเพิ่ม strict dispatch/parse guard, blocked diagnostic และ stable event header แทน schema `job_kind/job_id/generation` ในร่าง M1.1 ตาม M-31; ปรับ `data-runtime-design.md` ให้ตรง implementation แล้ว
- การกันหลาย dispatcher ใช้ transaction/row locks แบบ Informs ไม่เพิ่ม outbox lease อีกชุด; network timeout/batch size ต้อง bounded และ duplicate event ยังเป็นสิ่งที่ consumer ต้องรองรับ
- import/delivery topics/handlers ยังเป็นส่วน M3.2/M4 เมื่อ use cases พร้อม ไม่แสดงว่ามี consumer เหล่านั้นแล้ว

## การตั้งค่าและการรัน

- section `Messaging`: `Enabled`, `Kafka.BootstrapServers`, `Kafka.TransformTopic`, `Kafka.GroupIdPrefix`, `Kafka.ConsumersPerTopic`, `Kafka.RedeliveryDelaySeconds`, `Outbox.PollMilliseconds`, `Outbox.BatchSize`; รับ environment variables ได้ และตรวจ topic ให้ตรง `FileImport.TransformTopic` เมื่อเปิด messaging
- default ของ API ปิด messaging; local compose/script เปิดแบบ explicit เมื่อเริ่ม local stack ส่วน company mode ไม่เปิด messaging ให้อัตโนมัติ และไม่ apply migration บริษัท
- `appsettings.Local.json` เตรียม localhost broker/group/topic โดยไม่มี credential; compose ใช้ชื่อ broker ภายใน Docker; script คืนค่า environment ของ caller หลังรัน
- Confluent.Kafka 2.15.1 และ Testcontainers.Kafka 3.10.0 รุ่นเดียวกับ Informs; integration fixture ใช้ `confluentinc/cp-kafka:6.1.9` ตาม fixture Informs และตรวจ local Apache Kafka แยกจาก compose readiness/topic initialization

## ไฟล์ source ที่เพิ่ม/แก้ตามคำยืนยันผู้ใช้ (2026-11-17)

ทุก path อยู่ใต้ `prjInsuranceValidationMapping`:

- เพิ่ม `src/Mapping/Mapping.Infrastructure/Messaging/{MessagingOptions,OutboxDispatcher,TransformConsumer}.cs`
- เพิ่ม `src/Mapping/Mapping.Application/Messaging/Commands/ReceiveTransformMessageCommand.cs` และ `Messaging/DTOs/TransformWakeupResultDto.cs`
- เพิ่ม `src/Mapping/Mapping.Domain/Repositories/RowJobs/ITransformWakeupRepository.cs` และ `Infrastructure/Repositories/TransformWakeupRepository.cs`
- เพิ่ม `src/Mapping/Mapping.Domain/Entities/Inbox/InboxMessages.cs`
- แก้ `src/Mapping/Mapping.Domain/Entities/RowJobs/RowJobs.cs`, `Domain/Entities/Outbox/{TransformMessage,OutboxMessages}.cs`
- แก้ `src/Mapping/Mapping.Infrastructure/Database/MappingDbContext.cs`; เพิ่ม `Database/Configurations/Inbox/InboxMessagesConfiguration.cs`; แก้ `Database/Configurations/Outbox/OutboxMessagesConfiguration.cs`
- เพิ่ม migration `AddMappingMessagingInbox`/Designer และแก้ model snapshot: Inbox Receipt + immutable guards และ blocked outbox code/index
- แก้ `src/Mapping/Mapping.Infrastructure/DependencyInjection.cs` และ `Mapping.Infrastructure.csproj`
- แก้ `docker-compose.yml`, `scripts/up.ps1`, `src/Mapping/Mapping.Api/appsettings.Local.json`
- เพิ่ม `tests/Mapping.IntegrationTests/KafkaFixture.cs`, `Messaging/KafkaOutboxPipelineTests.cs`, `Messaging/MessagingConfigurationTests.cs`, `Database/MappingMessagingTests.cs`
- เพิ่ม `tests/Mapping.UnitTests/Messaging/TransformMessageTests.cs`; แก้ `Mapping.IntegrationTests.csproj` และ `Database/MappingCatalogTests.cs` ให้ตรง schema

## เกณฑ์ตรวจ

- PostgreSQL + Kafka จริง: outbox → broker → consumer → receipt/Row Job `NextAt`; event body/header ไม่มี raw fields/PII และไม่สร้าง Source/Row Job เพิ่ม
- publish แล้ว mark sent ล้มเหลว: outbox ยัง unsent, publish ซ้ำใช้ event ID เดิม, logical wake-up เกิดครั้งเดียว
- DB receipt/write ล้มเหลว: job/receipt rollback, committed offset ยังไม่ขยับ; DB กลับมาแล้ว redelivery สำเร็จ
- duplicate coordinates/event publish, consumer restart และหลาย dispatcher/consumer: ไม่มี intent ซ้ำ ไม่ reset job state/next-at/backoff
- malformed/unknown-field/oversize/key mismatch/lineage mismatch มี safe durable outcome; invalid outbox blocked และไม่ส่ง body ออก bus
- messaging disabled ไม่สร้าง producer/hosted services หรือเชื่อม broker; enabled แต่ configuration ผิดต้อง fail ชัดเจน; cancellation/shutdown ไม่ commit งานที่ยังไม่ durable
- compose config/readiness/topic init และ `./scripts/check.ps1` ผ่าน; ตามข้อตกลงผู้ใช้ 2026-11-17 ปิด M3.1 ในขอบเขต local ส่วน company Kafka dependencies แยกติดตาม Q7 และต้องพร้อมก่อนเปิดใช้งาน environment บริษัท

## หลักฐาน implementation (2026-11-17)

- migration `20261117042328_AddMappingMessagingInbox` เพิ่ม `blocked_code`/partial outbox index และ `inbox` ที่มี unique broker coordinates พร้อม immutable UPDATE/DELETE/TRUNCATE guards; ทดสอบบน PostgreSQL ชั่วคราว ไม่ apply DB บริษัท
- Messaging integration tests **14/14 ผ่าน**: PostgreSQL 5, Kafka pipeline 7, configuration 2; ใช้ event/Source สมมติทั้งหมด
- `./scripts/check.ps1` ผ่าน build, unit **20/20**, integration **74/74** และ EF model check; unit ของ strict transform message เพิ่ม 3 tests; มี NU1903 ของ SSH.NET 2023.0.0 จาก Testcontainers รุ่นเดิม ไม่มี compile error
- broker timeout คง outbox unsent แล้ว recovery ส่ง event ID เดิมได้; broker ack แล้ว DB mark sent ล้มเหลวส่งซ้ำเป็น offset ใหม่โดย Row Job เพิ่ม version ครั้งเดียว; DB receipt ล้มเหลว rollback `NextAt` และไม่ commit offset จน recovery
- ทดสอบหลาย dispatcher/consumer, malformed schema/header/key/lineage, blocked outbox, restart โดย rewind offset, durable receipt และไม่ reset backoff/งาน running/completed/blocked; canonical Kafka body มี UUID เพียง 4 ตัวและไม่มี raw Source marker
- local compose broker `apache/kafka:4.3.1` healthy และ topic `mapping.transform.requested` มี **3 partitions / replication factor 1**; `kafka-init` จบสำเร็จ; compose syntax และ PowerShell script parse ผ่าน
- local consumer จัดคิวงานด้วย `NextAt` เท่านั้น; compiler/evaluator และผล transform ยังเป็น M3.3; ปิด M3.1 ส่วน local แล้วตามข้อตกลงผู้ใช้ 2026-11-17 โดย company broker/topics/groups/owner/auth/TLS ยังรอ Q7 ก่อนใช้งานบน environment บริษัท
