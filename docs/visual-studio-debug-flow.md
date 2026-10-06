# คู่มือ Debug Data Flow ผ่าน Visual Studio

เอกสารนี้อธิบายเส้นทางข้อมูลตั้งแต่วางไฟล์ CSV ใน `input/orders` จนข้อมูลถูกบันทึกในตาราง `normalized_orders` พร้อมตำแหน่ง breakpoint และค่าที่ควรสังเกตใน Visual Studio

เลขบรรทัดอ้างอิง source code ปัจจุบัน หากมีการแก้โค้ดภายหลังเลขบรรทัดอาจขยับ ให้ค้นหาจากชื่อ method หรือคำสั่งที่ระบุไว้ประกอบ

## ภาพรวม Data Flow

```text
วาง CSV ใน input/orders
        |
        v
MappingDemo.Api: FileSystemWatcher
        |
        v
PostgreSQL: file_jobs + outbox(mapping.file-import)
        |
        v
MappingDemo.Worker: OutboxDispatcher -> Kafka
        |
        v
FileImportConsumer -> FileImportHandler
        |
        +--> source_orders
        +--> row_jobs
        +--> outbox(mapping.row-normalize)
                 |
                 v
          OutboxDispatcher -> Kafka
                 |
                 v
          RowNormalizeConsumer
                 |
                 v
          RowNormalizeHandler
                 |
                 +--> normalized_orders เมื่อข้อมูลถูกต้อง
                 +--> row_errors เมื่อข้อมูลไม่ถูกต้อง
```

ข้อสังเกตสำคัญ:

- API ไม่ส่ง Kafka โดยตรง แต่บันทึก message ลงตาราง `outbox` ก่อน
- Source row, Row job และ Row-normalize outbox ถูก commit เป็น transaction ต่อหนึ่งแถว
- Normalization เริ่มได้ทันทีหลัง Source row แรกถูก commit โดยไม่ต้องรอ import ทั้งไฟล์เสร็จ
- Normalized row ถูกเขียนและ commit ทีละแถว แต่หลาย consumers อาจประมวลผลพร้อมกัน
- การ archive เริ่มหลัง import CSV ครบทุกแถว แต่ไม่ต้องรอ normalization ครบ

## 1. เตรียม Visual Studio

### 1.1 ตั้ง Multiple Startup Projects

1. เปิด `MappingDemo.sln`
2. คลิกขวาที่ Solution แล้วเลือก `Configure Startup Projects...`
3. เลือก `Multiple startup projects`
4. ตั้งค่า:

| Project | Action |
|---|---|
| `MappingDemo.Api` | `Start` |
| `MappingDemo.Worker` | `Start` |

5. ให้ API ใช้ launch profile `http` ซึ่งฟังที่ `http://localhost:5181`

### 1.2 ตั้ง Worker arguments สำหรับ Debug แบบตามง่าย

ค่า config ปกติมี File Import Consumers 3 ตัวและ Normalize Consumers 3 ตัว หากต้องการดู flow ทีละงาน แนะนำให้ใช้ consumer อย่างละ 1 ตัวเฉพาะตอน debug

คลิกขวา `MappingDemo.Worker` -> `Properties` -> `Debug` -> `Open debug launch profiles UI` แล้วใส่ในช่อง `Command line arguments`:

```text
Import:DelaySeconds=1 Kafka:FileImportConsumers=1 Kafka:NormalizeConsumers=1 Demo:ImportRowDelayMs=100 Demo:NormalizeDelayMs=100
```

ข้อความข้างต้นเป็น arguments ของ Worker ไม่ใช่คำสั่ง PowerShell ห้ามนำไปรันเดี่ยว ๆ ใน Terminal

ถ้าต้องการรัน Worker ผ่าน Terminal ให้ใช้คำสั่งเต็ม:

```powershell
dotnet run --project ./src/MappingDemo.Worker -- Import:DelaySeconds=1 Kafka:FileImportConsumers=1 Kafka:NormalizeConsumers=1 Demo:ImportRowDelayMs=100 Demo:NormalizeDelayMs=100
```

## 2. เตรียมระบบจากสถานะว่าง

1. หยุด API และ Worker ก่อนด้วย `Shift+F5`
2. เปิด `View` -> `Terminal`
3. ตรวจว่า Terminal อยู่ที่ root:

   ```powershell
   Get-Location
   ```

   ควรเป็น `D:\Project\mapping-demo`

4. รีเซ็ต PostgreSQL, Kafka และ runtime folders:

   ```powershell
   pwsh ./scripts/reset-demo.ps1
   ```

5. รอจนเห็น `Demo state reset successfully.`
6. กด `F5` เพื่อเปิด API และ Worker
7. รอหน้า health แสดง:

   ```json
   { "status": "ok" }
   ```

8. กลับมาที่ Terminal แล้วสร้าง schema และ mapping config:

   ```powershell
   pwsh ./scripts/configure-demo.ps1
   ```

   สคริปต์นี้ไม่ได้วาง CSV แต่สร้าง `source_orders`, `normalized_orders`, mapping config และ activate version 1

9. รอ log API:

   ```text
   Watching config 1 at ...\input\orders
   ```

ห้ามรัน `reset-demo.ps1` ขณะที่ API หรือ Worker ยังทำงาน เพราะ PostgreSQL และ Kafka จะถูกสร้างใหม่ระหว่างที่ process เก่ายังเชื่อมต่ออยู่

## 3. Breakpoints ตามลำดับ Data Flow

### ช่วง A: API ตรวจพบไฟล์และสร้าง File Job

| ลำดับ | ไฟล์และบรรทัด | หยุดที่คำสั่ง | ทำให้รู้อะไร / ค่าที่ควรดู |
|---:|---|---|---|
| 1 | `src/MappingDemo.Api/Services/WatcherRegistry.cs:62` | `watcher.Created += ...` | `FileSystemWatcher` ตรวจพบไฟล์ใหม่ ดู `eventArgs.FullPath` |
| 2 | `src/MappingDemo.Api/Services/FileWatcherService.cs:199` | `_detectedFiles.Writer.TryWrite(...)` | event จาก watcher หรือ periodic scan ถูกรวมเข้าคิวเดียวกัน ดู `configId` และ `path` |
| 3 | `src/MappingDemo.Api/Services/FileWatcherService.cs:168` | `_fileIntake.Inspect(...)` | API อ่าน full path, file name, size, last-write time และสร้าง intake key ดู `detectedFile` กับ `intake` |
| 4 | `src/MappingDemo.Api/Services/FileIntake.cs:94` | `ExecuteScalarAsync<long?>` | กำลังสร้าง `file_jobs`; กด `F10` แล้วดู `fileJobId` หากเป็น `null` หมายถึง event ซ้ำและไม่ได้สร้างงานใหม่ |
| 5 | `src/MappingDemo.Api/Services/FileIntake.cs:98` | `OutboxWriter.AddAsync(...)` | กำลังสร้าง outbox topic `mapping.file-import` ดู `fileJobId.Value` |
| 6 | `src/MappingDemo.Api/Services/FileIntake.cs:107` | `transaction.CommitAsync(...)` | `file_jobs` และ file-import outbox ถูก commit พร้อมกัน หลังข้ามบรรทัดนี้ connection อื่นจึงมองเห็นข้อมูล |

`FileSystemWatcher` และ periodic scan อาจตรวจพบไฟล์เดียวกันมากกว่าหนึ่งครั้ง จึงเป็นเรื่องปกติที่ breakpoint ช่วงนี้จะหยุดซ้ำ แต่ควรสร้าง File Job สำเร็จเพียงครั้งเดียว

### ช่วง B: Outbox ส่ง Kafka และ Worker เริ่ม Import

| ลำดับ | ไฟล์และบรรทัด | หยุดที่คำสั่ง | ทำให้รู้อะไร / ค่าที่ควรดู |
|---:|---|---|---|
| 7 | `src/MappingDemo.Worker/OutboxDispatcher.cs:92` | `_producer.ProduceAsync(...)` | Outbox กำลังถูกส่งเข้า Kafka ดู `outboxMessage.Topic`, `MessageKey`, `Payload` |
| 8 | `src/MappingDemo.Worker/OutboxDispatcher.cs:101` | `MarkSentAsync(...)` | Kafka ตอบรับ message แล้ว ระบบกำลังตั้ง `outbox.sent_at` |
| 9 | `src/MappingDemo.Worker/FileImportConsumer.cs:69` | `consumer.Consume(...)` | Consumer รับ topic `mapping.file-import` ดู topic, partition, offset, key และ value ใน `result` |
| 10 | `src/MappingDemo.Worker/FileImportConsumer.cs:80` | `HandleAsync(...)` | Payload ถูกส่งต่อให้ `FileImportHandler` |
| 11 | `src/MappingDemo.Worker/FileImportHandler.cs:223` | `HandleAsync(...)` | จุดเริ่ม import file message ดู `payload` และ request หลัง deserialize |
| 12 | `src/MappingDemo.Worker/FileImportHandler.cs:284` | `SetDelayingAsync(...)` | File Job เปลี่ยนจาก `Queued` เป็น `Delaying` |
| 13 | `src/MappingDemo.Worker/FileImportHandler.cs:295` | `SetImportingAsync(...)` | Delay จบและ File Job กำลังเปลี่ยนเป็น `Importing` |
| 14 | `src/MappingDemo.Worker/FileImportHandler.cs:310` | `File.Copy(...)` | สร้าง snapshot ใน `data/staging/{fileJobId}.csv`; หลังจากนี้ Worker อ่าน snapshot ไม่ใช่ original โดยตรง |
| 15 | `src/MappingDemo.Worker/FileImportHandler.cs:382` | `ReadHeaderAsync(...)` | อ่าน CSV headers เพื่อตรวจว่าครบตาม File-to-Source rules |
| 16 | `src/MappingDemo.Worker/FileImportHandler.cs:412` | `_csvRecordReader.ReadAsync(...)` | เริ่มวนอ่าน records จาก CSV |
| 17 | `src/MappingDemo.Worker/FileImportHandler.cs:422` | `ImportRecordAsync(...)` | กำลังนำเข้า record ปัจจุบัน ดู `record.RowNumber` และ `record.Values` |

Breakpoint ลำดับ 17 จะถูกเรียกทุก record แนะนำให้ตั้ง Condition:

```csharp
record.RowNumber == 1 || record.RowNumber == 25 || record.RowNumber == 75
```

ตั้งได้โดยคลิกขวาที่ breakpoint -> `Conditions...` -> `Conditional Expression`

### ช่วง C: เขียน Source, Row Job และ Row-normalize Outbox

| ลำดับ | ไฟล์และบรรทัด | หยุดที่คำสั่ง | ทำให้รู้อะไร / ค่าที่ควรดู |
|---:|---|---|---|
| 18 | `src/MappingDemo.Worker/SourceRowWriter.cs:72` | `ExecuteScalarAsync<long?>` | Execute `INSERT INTO source_orders` ดู `sourceTableName`, `record.RowNumber`, `record.Values`; กด `F10` เพื่อดู `sourceRowId` ที่คืนกลับ |
| 19 | `src/MappingDemo.Worker/FileImportHandler.cs:616` | `SourceRowWriter.InsertAsync(...)` | เห็นความสัมพันธ์ระหว่าง `fileJob.Id`, CSV record และ Source row |
| 20 | `src/MappingDemo.Worker/FileImportHandler.cs:641` | `ExecuteScalarAsync<long>(rowJobCommand)` | สร้าง `row_jobs` สถานะ `Pending`; กด `F10` แล้วดู `rowJobId` |
| 21 | `src/MappingDemo.Worker/FileImportHandler.cs:643` | `OutboxWriter.AddAsync(...)` | สร้าง outbox topic `mapping.row-normalize` โดย payload อ้าง `rowJobId` |
| 22 | `src/MappingDemo.Worker/FileImportHandler.cs:652` | `transaction.CommitAsync(...)` | Commit Source row + Row job + Row-normalize outbox พร้อมกันต่อหนึ่ง CSV record |

ก่อนข้ามบรรทัด 652 ข้อมูลของแถวนั้นยังอยู่ใน transaction และ connection อื่นยังมองไม่เห็น หลัง commit แล้ว `OutboxDispatcher` สามารถหยิบ Row-normalize message ไปส่งได้ทันที

เมื่อ import ครบทุก record ระบบตั้งสถานะ `Imported` ที่ `src/MappingDemo.Worker/FileImportHandler.cs:435` แล้วเริ่ม archive ที่บรรทัด 461 โดยไม่รอ normalization ครบ

### ช่วง D: รับ Row-normalize Message และ Normalize

| ลำดับ | ไฟล์และบรรทัด | หยุดที่คำสั่ง | ทำให้รู้อะไร / ค่าที่ควรดู |
|---:|---|---|---|
| 23 | `src/MappingDemo.Worker/OutboxDispatcher.cs:92` | `_producer.ProduceAsync(...)` | คราวนี้ `outboxMessage.Topic` เป็น `mapping.row-normalize` |
| 24 | `src/MappingDemo.Worker/RowNormalizeConsumer.cs:78` | `consumer.Consume(...)` | Normalize consumer รับ message ดู topic, partition, offset, key และ payload |
| 25 | `src/MappingDemo.Worker/RowNormalizeConsumer.cs:89` | `HandleAsync(...)` | Message ถูกส่งต่อให้ `RowNormalizeHandler` |
| 26 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:132` | `Deserialize<RowNormalizeRequested>` | Payload ถูกแปลงเป็น request ดู `request.RowJobId` |
| 27 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:200` | `QuerySingleOrDefaultAsync<RowJob>` | โหลด Row job และ config version ดู `rowJob.RowNumber`, `SourceRowId`, `ConfigVersionId`, `SourceTableName`, `NormalizedTableName` |
| 28 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:235` | `LoadSourceRowAsync(...)` | โหลดค่าดิบจาก `source_orders`; หลัง `F10` ดู `sourceRow`, `rules`, `normalizedColumns` |
| 29 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:251` | `RowNormalizer.Normalize(...)` | จุดก่อนแปลงค่า สามารถกด `F11` เข้าไปดูแต่ละ field |
| 30 | `src/MappingDemo.Shared/Normalization/RowNormalizer.cs:39` | `ValueConverter.Convert(...)` | ดู `sourceValue`, data type, required flag และ date format ของ field ปัจจุบัน |
| 31 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:257` | `switch (result)` | แยกว่าแถวเป็น `Success` หรือ `Failure` ดู `result` |

เนื่องจากการ normalize เกิดกับทุก Source row แนะนำ Condition ที่ breakpoint ลำดับ 27-31:

```csharp
rowJob.RowNumber == 1 || rowJob.RowNumber == 25 || rowJob.RowNumber == 75
```

ความหมายของตัวอย่างสามแถว:

- Row 1: ข้อมูลถูกต้อง ไปทาง `Success`
- Row 25: `order_date = 2026-02-30` ได้ `InvalidDate`
- Row 75: `amount = not-a-number` ได้ `InvalidDecimal`

### ช่วง E: บันทึก Normalized Result หรือ Row Error

| ลำดับ | ไฟล์และบรรทัด | หยุดที่คำสั่ง | ทำให้รู้อะไร / ค่าที่ควรดู |
|---:|---|---|---|
| 32 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:268` | `NormalizedRowWriter.UpsertAsync(...)` | เส้นทาง `Success` กำลังส่งค่าที่แปลงแล้วไปเขียน `normalized_orders` ดู `normalizedValues` |
| 33 | `src/MappingDemo.Worker/NormalizedRowWriter.cs:89` | `connection.ExecuteAsync(command)` | Execute `INSERT ... ON CONFLICT (source_row_id) DO UPDATE`; ณ จุดนี้ SQL ถูกส่งแล้วแต่ transaction ยังไม่ commit |
| 34 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:283` | `InsertErrorsAsync(...)` | เส้นทาง `Failure` บันทึก `row_errors` และจะไม่เขียน normalized row สำหรับรอบนี้ |
| 35 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:297` | `CompleteRowJobAsync(...)` | เปลี่ยน Row job เป็น `Done` หรือ `Invalid` |
| 36 | `src/MappingDemo.Worker/RowNormalizeHandler.cs:304` | `transaction.CommitAsync(...)` | Commit normalized row หรือ row errors พร้อมสถานะ Row job หลังข้ามบรรทัดนี้ connection อื่นจึงเห็นผล |
| 37 | `src/MappingDemo.Worker/RowNormalizeConsumer.cs:92` | `consumer.Commit(result)` | Handler จบแล้ว Consumer commit Kafka offset |

ความแตกต่างสำคัญ:

- `NormalizedRowWriter.cs:89` คือจุด execute SQL ภายใน transaction
- `RowNormalizeHandler.cs:304` คือจุดที่ข้อมูลถูก commit และมองเห็นจากภายนอกจริง

## 4. Breakpoints ชุดสั้น

หากไม่ต้องการวางครบทุกจุด ให้ใช้ 10 จุดนี้:

1. `src/MappingDemo.Api/Services/FileWatcherService.cs:199`
2. `src/MappingDemo.Api/Services/FileIntake.cs:94`
3. `src/MappingDemo.Worker/OutboxDispatcher.cs:92`
4. `src/MappingDemo.Worker/FileImportConsumer.cs:69`
5. `src/MappingDemo.Worker/FileImportHandler.cs:422`
6. `src/MappingDemo.Worker/SourceRowWriter.cs:72`
7. `src/MappingDemo.Worker/FileImportHandler.cs:652`
8. `src/MappingDemo.Worker/RowNormalizeHandler.cs:251`
9. `src/MappingDemo.Worker/NormalizedRowWriter.cs:89`
10. `src/MappingDemo.Worker/RowNormalizeHandler.cs:304`

## 5. เริ่มทดสอบด้วยไฟล์ตัวอย่าง

หลังตั้ง breakpoint และเห็น log `Watching config 1 at ...\input\orders` ให้รัน:

```powershell
Copy-Item `
    -LiteralPath ./samples/acceptance/01-orders-a-invalid-2.csv `
    -Destination ./input/orders/orders-a.csv
```

อย่าแก้หรือย้ายไฟล์ต้นฉบับใน `samples/acceptance` โดยตรง เพราะจะกระทบ hash และผลทดสอบ ให้คัดลอกเข้า `input/orders` เสมอ

## 6. ตรวจข้อมูลใน PostgreSQL

เปิด Terminal แล้วเข้า `psql`:

```powershell
docker compose exec -it postgres psql -U mapping -d mapping
```

ตรวจ File Job และ Archive:

```sql
SELECT id, file_name, import_status, archive_status, total_rows, archive_path
FROM file_jobs
ORDER BY id;
```

ตรวจ Source rows ที่ใช้เดิน debugger:

```sql
SELECT file_job_id, row_number, order_date, amount
FROM source_orders
WHERE row_number IN (1, 25, 75)
ORDER BY row_number;
```

ตรวจ Normalized rows:

```sql
SELECT file_job_id, row_number, order_date, amount
FROM normalized_orders
WHERE row_number IN (1, 25, 75)
ORDER BY row_number;
```

ตรวจ Row errors:

```sql
SELECT file_job_id, row_number, field, reason
FROM row_errors
ORDER BY row_number;
```

ตรวจ Outbox:

```sql
SELECT id, topic, message_key, payload, created_at, sent_at
FROM outbox
ORDER BY id DESC
LIMIT 20;
```

ตรวจจำนวนผลรวม:

```sql
SELECT count(*) AS source_count FROM source_orders;
SELECT count(*) AS normalized_count FROM normalized_orders;
SELECT count(*) AS error_count FROM row_errors;
```

ผลสุดท้ายของ `01-orders-a-invalid-2.csv` ควรเป็น:

| รายการ | จำนวน |
|---|---:|
| Source rows | 100 |
| Normalized rows | 98 |
| Row errors | 2 |

Row 25 และ 75 จะไม่มีใน `normalized_orders` แต่มีใน `row_errors`

ออกจาก `psql` ด้วย:

```sql
\q
```

## 7. จุดที่ควรจำระหว่าง Debug

- หาก Visual Studio หยุดก่อน `CommitAsync` การ query จาก Terminal จะยังไม่เห็นข้อมูลของ transaction นั้น
- เมื่อ debugger อยู่ใน Break Mode ทุก thread อาจถูกหยุด ทำให้หน้า `/health` รอหรือ timeout ได้ ให้กด `F5` เพื่อทำงานต่อ
- `OutboxDispatcher` ตรวจ outbox ทุก 500 ms ดังนั้น `sent_at IS NULL` อาจเห็นเพียงช่วงสั้น ๆ
- ถ้า Worker มี consumers มากกว่าหนึ่งตัว breakpoint อาจถูกเรียกจากหลาย threads และลำดับ Row job อาจไม่เรียงตาม `row_number`
- Kafka ใน flow นี้เป็น at-least-once delivery ระบบจึงใช้ `ON CONFLICT` และการตรวจสถานะ job เพื่อป้องกันผลลัพธ์ซ้ำ
- คู่มือนี้ใช้สำหรับ local development และ debugging ไม่ใช่แนวทาง deployment หรือ security configuration สำหรับ production

## 8. ลำดับเริ่มใหม่สำหรับทดสอบซ้ำ

```text
Shift+F5
    -> pwsh ./scripts/reset-demo.ps1
    -> F5
    -> pwsh ./scripts/configure-demo.ps1
    -> รอ Watching config...
    -> Copy-Item CSV เข้า input/orders
```

