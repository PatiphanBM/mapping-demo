# คู่มือทดสอบเกณฑ์ตรวจรับ Mapping Demo

คู่มือนี้ครอบคลุมเกณฑ์ตรวจรับทั้ง 14 ข้อใน Phase 18 ของ `docs/learning-plan.md` โดยใช้ไฟล์ต้นฉบับใน `samples/acceptance/` ทดสอบบน Windows PowerShell 7

## สรุป scenario และเกณฑ์ที่ครอบคลุม

| Scenario | เกณฑ์ที่พิสูจน์ |
|---|---|
| A — Main flow และ concurrency | schema/config ผ่าน API, Source เป็น text, delay 30 วินาที, normalize อิสระ, normalize ก่อน import จบ, A/B overlap, 100/98/2, archive ขณะ normalize ยังทำงาน |
| B — Outbox และ restart | งานค้างใน outbox ไม่หาย, หยุดกลาง import แล้วไม่เพิ่ม Source/Normalized ซ้ำ, ส่ง row message ซ้ำแล้วผลไม่เพิ่ม |
| C — Partial import retry | ล้มที่แถว 50, retry ด้วย snapshot/config version เดิม, Source ไม่มีแถวซ้ำ |
| D — Config pinning และ reprocess | งานเดิมใช้ version ที่ตรึงไว้, reprocess ด้วย version ที่เลือก, มี history แยก, reprocess ที่ invalid ไม่ลบผลสำเร็จเดิม |
| E — Duplicate content | เนื้อหาเดิมแต่ชื่อใหม่ได้สถานะ `Duplicate` |
| F — ChangedAfterRead | แก้ไฟล์หลัง snapshot แล้วไม่ archive และเก็บทั้ง original/snapshot |
| G — Normalize failure และ retry | row job เป็น `Failed`, ไฟล์เป็น `CompletedWithErrors`, retry แล้วคำนวณสถานะใหม่ |

แต่ละ scenario ที่เขียนว่า **เริ่มใหม่** ต้องหยุด API/Worker เดิมด้วย `Ctrl+C` แล้วทำขั้นตอนเตรียมระบบใหม่ทั้งหมด ห้ามนำ state จาก scenario ก่อนหน้ามาใช้

## ไฟล์ที่ใช้

| ไฟล์ | จำนวน records | จุดประสงค์ |
|---|---:|---|
| `01-orders-a-invalid-2.csv` | 100 | แถว 25 มี `2026-02-30`; แถว 75 มี `not-a-number` |
| `01-orders-b-valid.csv` | 100 | ข้อมูลถูกทั้งหมดและใช้รันคาบเกี่ยวกับ A |
| `02-outbox-restart.csv` | 100 | หยุด Worker กลาง import และทดสอบ message ซ้ำ |
| `03-partial-retry.csv` | 100 | fault injection ที่แถว 50 |
| `04-version-pinning.csv` | 3 | วันที่เป็น `dd/MM/yyyy` เพื่อให้ v1 fail, v2 pass, v3 fail |
| `05-duplicate-original.csv` | 3 | ไฟล์แรกสำหรับคำนวณ content hash |
| `05-duplicate-renamed.csv` | 3 | เนื้อหาเหมือนไฟล์ก่อนหน้าทุก byte แต่ชื่อไม่เหมือนกัน |
| `06-changed-original.csv` | 50 | เนื้อหาตอนสร้าง snapshot |
| `06-changed-after-snapshot.csv` | 50 | ใช้เขียนทับ original หลัง snapshot |
| `07-normalize-failure.csv` | 20 | fault injection ที่ row 10 |

อย่าแก้ไฟล์ใน `samples/acceptance/` โดยตรง เพราะจะทำให้ hash และผลคาดหวังเปลี่ยน ให้คัดลอกไป `input/orders` เท่านั้น

## คำสั่งพื้นฐาน

เปิด PowerShell ที่ root ของ repository แล้วกำหนด URL:

```powershell
$api = "http://localhost:5181"
```

ดูรายการและรายละเอียด file job:

```powershell
Invoke-RestMethod "$api/file-jobs" | Format-Table
Invoke-RestMethod "$api/file-jobs/<FILE_JOB_ID>" | Format-List
```

รัน SQL ใน PostgreSQL container:

```powershell
docker compose exec -T postgres psql -U mapping -d mapping -c "SELECT now();"
```

ถ้าต้องใช้ SQL หลายบรรทัด ให้ใช้ here-string:

```powershell
$sql = @'
SELECT id, file_name, import_status, archive_status, total_rows
FROM file_jobs
ORDER BY id;
'@
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

## ขั้นตอนเตรียมระบบใหม่

ทำทุกครั้งก่อน scenario ที่ระบุว่า **เริ่มใหม่**

1. หยุด API และ Worker ทุกหน้าต่างด้วย `Ctrl+C`

2. รีเซ็ต PostgreSQL/Kafka และล้าง runtime folders:

   ```powershell
   pwsh ./scripts/reset-demo.ps1
   ```

3. เปิด API ใน terminal ที่ 1:

   ```powershell
   dotnet run --project src/MappingDemo.Api --launch-profile http
   ```

4. รอจนเห็น `Now listening on: http://localhost:5181` แล้วเปิด terminal ที่ 2:

   ```powershell
   $api = "http://localhost:5181"
   Invoke-RestMethod "$api/health"
   pwsh ./scripts/configure-demo.ps1
   ```

5. รอประมาณ 10 วินาทีจน log API แสดงข้อความต่อไปนี้:

   ```text
   Watching config 1 at ...\input\orders
   ```

6. ตรวจ config ตั้งต้น:

   ```powershell
   Invoke-RestMethod "$api/mapping-configs/1" |
       ConvertTo-Json -Depth 10
   ```

   ผลที่ต้องได้คือ `inputFolder = "orders"`, active version เป็น version 1 และ format ของ `order_date` คือ `yyyy-MM-dd`

---

## Scenario A — Main flow, delay, concurrency และผล 100/98/2

**เริ่มใหม่** แล้วทำตามขั้นตอนนี้

### A1. เปิด Worker แบบเห็นช่วงเวลาต่าง ๆ ชัดเจน

เปิด terminal ที่ 3:

```powershell
dotnet run --project src/MappingDemo.Worker -- `
    Demo:ImportRowDelayMs=200 `
    Demo:NormalizeDelayMs=1000
```

- `Import:DelaySeconds` ไม่ได้ override จึงยังเป็น 30 วินาทีจริง
- import แต่ละ Source row ช้า 200 ms ทำให้เห็น normalize เริ่มก่อน import จบ
- normalize แต่ละ row ช้า 1 วินาที ทำให้เห็นไฟล์ archive แล้วแต่ยังมี row jobs ค้าง

รอจน log แสดง partition assignment ของ import consumers และ normalize consumers

### A2. วางไฟล์ A และ B ติดต่อกัน

ใน terminal ที่ 2:

```powershell
Copy-Item -LiteralPath ./samples/acceptance/01-orders-a-invalid-2.csv `
    -Destination ./input/orders/orders-a.csv
Copy-Item -LiteralPath ./samples/acceptance/01-orders-b-valid.csv `
    -Destination ./input/orders/orders-b.csv
```

อ่าน ID โดยไม่สมมติลำดับ:

```powershell
Start-Sleep -Seconds 2
$jobs = Invoke-RestMethod "$api/file-jobs"
$jobA = $jobs | Where-Object fileName -eq "orders-a.csv"
$jobB = $jobs | Where-Object fileName -eq "orders-b.csv"
$jobA
$jobB
```

### A3. ตรวจ delay 30 วินาที

ภายใน 30 วินาทีแรกให้รัน:

```powershell
Invoke-RestMethod "$api/file-jobs/$($jobA.id)" | Format-List
Invoke-RestMethod "$api/file-jobs/$($jobB.id)" | Format-List

$sql = "SELECT file_job_id, count(*) FROM source_orders GROUP BY file_job_id;"
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ผลที่ต้องได้:

- ทั้งสองงานเป็น `Delaying`
- Source ยังไม่มีแถวของงานทั้งสอง
- log Worker มี `is delaying for 30 seconds`
- log `Consumed mapping.file-import` และ `Dispatched ... partition` แสดง partition ของ A/B; จากฐานใหม่ file job 1 และ 2 ควรอยู่คนละ partition และเริ่ม delay ห่างกันเล็กน้อย

normalize consumers ยังทำงานเป็น consumer group แยกต่างหาก จึงไม่ถูกบล็อกด้วย import consumer ที่กำลัง delay

### A4. ตรวจ normalize เริ่มก่อน import จบ

หลัง log `completed its delay` ประมาณ 3–5 วินาที ให้รัน:

```powershell
$sql = @'
SELECT
    f.id,
    f.file_name,
    f.import_status,
    (SELECT count(*) FROM source_orders s WHERE s.file_job_id = f.id) AS source_rows,
    (SELECT count(*) FROM normalized_orders n WHERE n.file_job_id = f.id) AS normalized_rows
FROM file_jobs f
ORDER BY f.id;
'@
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ผลที่ต้องได้อย่างน้อยหนึ่งงาน:

- `import_status = Importing`
- `source_rows` มากกว่า 0 แต่น้อยกว่า 100
- `normalized_rows` มากกว่า 0

นี่พิสูจน์ว่า Source row/outbox commit เป็นรายแถว และ normalize ไม่ต้องรอ File Import Job จบ

### A5. ตรวจ archive ขณะที่ normalize ยังไม่จบ

หลัง import เริ่มประมาณ 20–25 วินาที:

```powershell
Invoke-RestMethod "$api/file-jobs" | Format-Table `
    id,fileName,importStatus,archiveStatus,totalRows,normalizationStatus,pendingRows,doneRows,invalidRows
```

ผลที่ต้องเห็นชั่วคราวคือ `importStatus=Imported`, `archiveStatus=Archived` แต่ `pendingRows` ยังมากกว่า 0 หรือ `normalizationStatus=InProgress`

ตรวจไฟล์ archive:

```powershell
Get-ChildItem ./archive -File -Recurse

$archiveDateFolder = ([DateTime]$jobA.createdAt).ToUniversalTime().ToString("ddMMyyyy")
$datedArchive = Join-Path ./archive $archiveDateFolder
Test-Path (Join-Path $datedArchive "orders-a.csv")
Test-Path (Join-Path $datedArchive "orders-b.csv")
```

ทั้งสอง `Test-Path` ต้องได้ `True` เพื่อยืนยันว่าใช้โฟลเดอร์วันที่ `ddMMyyyy` และเก็บชื่อไฟล์เดิมโดยไม่มี `{fileJobId}_` นำหน้า

### A6. ตรวจผลสุดท้าย

รอจน `pendingRows=0` ทั้งคู่ แล้วรัน:

```powershell
$jobs = Invoke-RestMethod "$api/file-jobs"
$jobs | Format-Table id,fileName,totalRows,normalizationStatus,doneRows,invalidRows,failedRows
Invoke-RestMethod "$api/file-jobs/$($jobA.id)/errors" | Format-Table
```

ผลที่ต้องได้:

| ไฟล์ | Source/totalRows | Done/Normalized | Invalid | สถานะ |
|---|---:|---:|---:|---|
| A | 100 | 98 | 2 | `CompletedWithErrors` |
| B | 100 | 100 | 0 | `Completed` |

errors ของ A ต้องเป็น:

```text
row 25  order_date  InvalidDate
row 75  amount      InvalidDecimal
```

### A7. ตรวจว่า Source เก็บค่าดิบเป็น text

```powershell
$sql = @"
SELECT row_number, amount, pg_typeof(amount), order_date, pg_typeof(order_date)
FROM source_orders
WHERE file_job_id = $($jobA.id) AND row_number IN (25, 75)
ORDER BY row_number;
"@
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ต้องเห็น `pg_typeof = text` รวมถึงค่า `2026-02-30` และ `not-a-number` ถูกเก็บใน Source ได้

---

## Scenario B — Outbox durability, restart กลาง import และ message ซ้ำ

**เริ่มใหม่** แต่ยังไม่เปิด Worker

### B1. พิสูจน์ว่างานไม่หายเมื่อ Worker ปิด

```powershell
Copy-Item -LiteralPath ./samples/acceptance/02-outbox-restart.csv `
    -Destination ./input/orders/orders-restart.csv
Start-Sleep -Seconds 2

$job = Invoke-RestMethod "$api/file-jobs" |
    Where-Object fileName -eq "orders-restart.csv"
$job

$sql = "SELECT id, topic, message_key, sent_at FROM outbox ORDER BY id;"
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ต้องมี `mapping.file-import` ที่ `sent_at` เป็น `NULL` เพราะ Worker/dispatcher ยังไม่เปิด

### B2. หยุด Worker กลาง import แล้วเปิดใหม่

เปิด Worker:

```powershell
dotnet run --project src/MappingDemo.Worker -- `
    Import:DelaySeconds=1 `
    Demo:ImportRowDelayMs=200
```

รอให้ Source มีประมาณ 10–90 แถว:

```powershell
$sql = "SELECT count(*) FROM source_orders WHERE file_job_id = $($job.id);"
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

กด `Ctrl+C` ที่ Worker ขณะ count ยังไม่ถึง 100 แล้วเปิด Worker ใหม่ด้วยคำสั่งเดิม

เมื่อจบ ตรวจ:

```powershell
$sql = @"
SELECT count(*) AS rows, count(DISTINCT row_number) AS distinct_rows
FROM source_orders
WHERE file_job_id = $($job.id);
SELECT count(*) AS normalized_rows
FROM normalized_orders
WHERE file_job_id = $($job.id);
"@
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ทั้ง `rows` และ `distinct_rows` ต้องเป็น 100 และ normalized ต้องเป็น 100 ไม่มีผลซ้ำ

### B3. ส่ง row outbox ซ้ำ

บันทึกจำนวน normalized ก่อน:

```powershell
$sql = "SELECT count(*) FROM normalized_orders WHERE file_job_id = $($job.id);"
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ตั้ง outbox ของ row หนึ่งกลับเป็น unsent:

```powershell
$sql = @"
UPDATE outbox
SET sent_at = NULL
WHERE id = (
    SELECT id FROM outbox
    WHERE topic = 'mapping.row-normalize'
    ORDER BY id
    LIMIT 1
);
"@
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

รอ 2 วินาทีแล้ว query count เดิมอีกครั้ง ต้องยังเป็น 100 และ log Worker ต้องแสดงว่า row job ที่ `Done` ถูก skip

---

## Scenario C — Partial import failure และ retry ด้วย version เดิม

**เริ่มใหม่** แล้วเปิด Worker ด้วย fault ที่แถว 50:

```powershell
dotnet run --project src/MappingDemo.Worker -- `
    Import:DelaySeconds=1 `
    Demo:FailImportAtRow=50
```

คัดลอกไฟล์:

```powershell
Copy-Item -LiteralPath ./samples/acceptance/03-partial-retry.csv `
    -Destination ./input/orders/orders-partial-retry.csv
Start-Sleep -Seconds 12
$job = Invoke-RestMethod "$api/file-jobs" |
    Where-Object fileName -eq "orders-partial-retry.csv"
$job | Format-List
```

ผลก่อน retry:

- `importStatus = ImportFailed`
- `lastError` ระบุ row 50
- `configVersionId` เป็น version 1

```powershell
$sql = "SELECT count(*) FROM source_orders WHERE file_job_id = $($job.id);"
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ต้องได้ 49 แถว

หยุด Worker ด้วย `Ctrl+C` แล้วเปิดใหม่โดยเอา fault ออก:

```powershell
dotnet run --project src/MappingDemo.Worker -- Import:DelaySeconds=1
```

สั่ง retry:

```powershell
Invoke-WebRequest -Method Post "$api/file-jobs/$($job.id)/retry"
```

รอจนจบแล้วตรวจ:

```powershell
Invoke-RestMethod "$api/file-jobs/$($job.id)" | Format-List
$sql = @"
SELECT count(*) AS rows, count(DISTINCT row_number) AS distinct_rows
FROM source_orders
WHERE file_job_id = $($job.id);
"@
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ต้องได้ `Imported`, `Archived`, rows=100, distinct_rows=100 และ `configVersionId` ยังเป็น version 1

---

## Scenario D — Config pinning, reprocess สำเร็จ และ reprocess ไม่ผ่าน

**เริ่มใหม่** แต่ยังไม่เปิด Worker

### D1. สร้างงานขณะ v1 ยัง active

```powershell
Copy-Item -LiteralPath ./samples/acceptance/04-version-pinning.csv `
    -Destination ./input/orders/orders-versioned.csv
Start-Sleep -Seconds 2
$job = Invoke-RestMethod "$api/file-jobs" |
    Where-Object fileName -eq "orders-versioned.csv"
$job | Format-List
```

ต้องเห็น `configVersionId` ชี้ v1

### D2. สร้าง v2/v3 และ activate v2 หลังรับไฟล์แล้ว

```powershell
pwsh ./scripts/configure-acceptance-versions.ps1
$config = Invoke-RestMethod "$api/mapping-configs/1"
$v1 = $config.versions | Where-Object versionNo -eq 1
$v2 = $config.versions | Where-Object versionNo -eq 2
$v3 = $config.versions | Where-Object versionNo -eq 3
$config.versions | Select-Object id,versionNo,isActive

Invoke-WebRequest -Method Post `
    "$api/mapping-configs/1/versions/2/activate"
```

เปิด Worker:

```powershell
dotnet run --project src/MappingDemo.Worker -- Import:DelaySeconds=1
```

แม้ active version เปลี่ยนเป็น v2 แล้ว งานเดิมต้องยัง log ว่าใช้ config version ID ของ `$v1.id` และจบด้วย Invalid 3 แถว เพราะ v1 คาด `yyyy-MM-dd` แต่ไฟล์เป็น `dd/MM/yyyy`

```powershell
Start-Sleep -Seconds 8
Invoke-RestMethod "$api/file-jobs/$($job.id)" | Format-List
Invoke-RestMethod "$api/file-jobs/$($job.id)/errors" | Format-Table
```

### D3. Reprocess ด้วย v2 ที่ถูกต้อง

```powershell
$body = @{ versionId = $v2.id } | ConvertTo-Json
Invoke-RestMethod -Method Post `
    -Uri "$api/file-jobs/$($job.id)/reprocess" `
    -ContentType "application/json" `
    -Body $body
Start-Sleep -Seconds 5
```

ตรวจผลและ history:

```powershell
Invoke-RestMethod "$api/file-jobs/$($job.id)" | Format-List
Invoke-RestMethod "$api/file-jobs/$($job.id)/rows/1/history" |
    ConvertTo-Json -Depth 10
```

ต้องได้ Done 3, Invalid 0 และ history ของ row 1 มีอย่างน้อยสองรายการ:

- `Initial`, v1, `Invalid`
- `Reprocess`, v2, `Done`

### D4. Reprocess ด้วย v3 ที่ผิด และพิสูจน์ว่าผล v2 ยังอยู่

```powershell
$body = @{ versionId = $v3.id } | ConvertTo-Json
Invoke-RestMethod -Method Post `
    -Uri "$api/file-jobs/$($job.id)/reprocess" `
    -ContentType "application/json" `
    -Body $body
Start-Sleep -Seconds 5

Invoke-RestMethod "$api/file-jobs/$($job.id)/errors" | Format-Table
Invoke-RestMethod "$api/file-jobs/$($job.id)/rows/1/history" |
    ConvertTo-Json -Depth 10
```

latest row jobs ต้องเป็น Invalid ของ v3 แต่ผล normalized เดิมต้องยังชี้ v2:

```powershell
$sql = @"
SELECT row_number, order_date, config_version_id
FROM normalized_orders
WHERE file_job_id = $($job.id)
ORDER BY row_number;
"@
docker compose exec -T postgres psql -U mapping -d mapping -c $sql
```

ต้องมี 3 แถว และ `config_version_id = $($v2.id)` ทุกแถว

---

## Scenario E — เนื้อหาเดิม ชื่อใหม่ เป็น Duplicate

**เริ่มใหม่** แล้วเปิด Worker:

```powershell
dotnet run --project src/MappingDemo.Worker -- Import:DelaySeconds=1
```

ยืนยันก่อนว่า fixture เหมือนกันจริง:

```powershell
Get-FileHash ./samples/acceptance/05-duplicate-original.csv
Get-FileHash ./samples/acceptance/05-duplicate-renamed.csv
```

hash ต้องเท่ากัน จากนั้นส่งไฟล์แรกและรอให้ `Imported` ก่อน:

```powershell
Copy-Item ./samples/acceptance/05-duplicate-original.csv `
    ./input/orders/orders-original.csv
Start-Sleep -Seconds 7
Copy-Item ./samples/acceptance/05-duplicate-renamed.csv `
    ./input/orders/orders-renamed.csv
Start-Sleep -Seconds 7

Invoke-RestMethod "$api/file-jobs" |
    Format-Table id,fileName,importStatus,archiveStatus,totalRows
```

ผลที่ต้องได้:

- `orders-original.csv` เป็น `Imported`
- `orders-renamed.csv` เป็น `Duplicate`
- ทั้งคู่เป็น `Archived`
- Source มีเพียง 3 แถวจากไฟล์แรก

```powershell
docker compose exec -T postgres psql -U mapping -d mapping `
    -c "SELECT file_job_id, count(*) FROM source_orders GROUP BY file_job_id;"
```

---

## Scenario F — ChangedAfterRead และไม่ย้ายไฟล์

**เริ่มใหม่** แต่ยังไม่เปิด Worker

### F1. ให้ Watcher สร้าง job แล้วหยุด API

```powershell
Copy-Item ./samples/acceptance/06-changed-original.csv `
    ./input/orders/orders-changing.csv
Start-Sleep -Seconds 2
$job = Invoke-RestMethod "$api/file-jobs" |
    Where-Object fileName -eq "orders-changing.csv"
$job
```

กด `Ctrl+C` ที่ API เพื่อป้องกัน Watcher สร้าง job ใหม่ตอนเขียนทับไฟล์

### F2. เปิด Worker และเขียนทับหลัง snapshot

เปิด Worker:

```powershell
dotnet run --project src/MappingDemo.Worker -- `
    Import:DelaySeconds=1 `
    Demo:ImportRowDelayMs=200
```

ใน terminal อีกหน้า รอจน snapshot ถูกสร้าง แล้วเขียนทับ original ทันที:

```powershell
$snapshot = "./data/staging/$($job.id).csv"
while (-not (Test-Path -LiteralPath $snapshot)) {
    Start-Sleep -Milliseconds 100
}

Copy-Item -LiteralPath ./samples/acceptance/06-changed-after-snapshot.csv `
    -Destination ./input/orders/orders-changing.csv `
    -Force
```

รอประมาณ 15 วินาทีแล้วตรวจผ่าน DB เพราะ API ยังปิดอยู่:

```powershell
$sql = "SELECT id, import_status, archive_status, snapshot_path, archive_path FROM file_jobs WHERE id = $($job.id);"
docker compose exec -T postgres psql -U mapping -d mapping -c $sql

Test-Path ./input/orders/orders-changing.csv
Test-Path $snapshot
Get-ChildItem ./archive -File -Recurse
```

ผลที่ต้องได้:

- `import_status = Imported`
- `archive_status = ChangedAfterRead`
- original ใน `input/orders` ยังอยู่
- snapshot ใน `data/staging` ยังอยู่
- ไม่มี archive ของ job นี้

หมายเหตุ: ถ้าเปิด API อีกครั้งขณะที่ original ยังอยู่ Watcher อาจรับไฟล์ที่แก้แล้วเป็น job ใหม่ ซึ่งเป็นพฤติกรรมปกติ ไม่ได้เปลี่ยนผลของ job แรก

---

## Scenario G — Normalize Failed, CompletedWithErrors และ row retry

**เริ่มใหม่** แล้วเปิด Worker พร้อม fault ที่ row 10:

```powershell
dotnet run --project src/MappingDemo.Worker -- `
    Import:DelaySeconds=1 `
    Demo:FailNormalizeRowNumbers:0=10
```

คัดลอกไฟล์:

```powershell
Copy-Item ./samples/acceptance/07-normalize-failure.csv `
    ./input/orders/orders-normalize-failure.csv
Start-Sleep -Seconds 15

$job = Invoke-RestMethod "$api/file-jobs" |
    Where-Object fileName -eq "orders-normalize-failure.csv"
$job | Format-List
```

ผลที่ต้องได้ก่อน retry:

- `importStatus = Imported`
- `archiveStatus = Archived`
- `doneRows = 19`
- `failedRows = 1`
- `normalizationStatus = CompletedWithErrors`

อ่าน row job ID และยืนยันว่าลองครบ 4 attempts:

```powershell
$history = Invoke-RestMethod `
    "$api/file-jobs/$($job.id)/rows/10/history"
$history | ConvertTo-Json -Depth 10
$rowJobId = $history[-1].id
```

รายการของ row 10 ต้องเป็น `Failed`, `attempts=4` และใช้ config version เดิม

หยุด Worker ด้วย `Ctrl+C` แล้วเปิดใหม่โดยไม่มี fault:

```powershell
dotnet run --project src/MappingDemo.Worker -- Import:DelaySeconds=1
```

สั่ง retry row job เดิม:

```powershell
Invoke-WebRequest -Method Post "$api/row-jobs/$rowJobId/retry"
Start-Sleep -Seconds 5

Invoke-RestMethod "$api/file-jobs/$($job.id)" | Format-List
Invoke-RestMethod "$api/file-jobs/$($job.id)/rows/10/history" |
    ConvertTo-Json -Depth 10
```

ผลสุดท้ายต้องเป็น Done 20, Failed 0, `Completed` และ row job ID/config version ยังเป็นค่าเดิม

## ตรวจปิดงาน

หลังทดสอบครบ:

1. หยุด API/Worker ด้วย `Ctrl+C`
2. คืนระบบเป็นสถานะสะอาด:

   ```powershell
   pwsh ./scripts/reset-demo.ps1
   ```

3. รัน unit tests:

   ```powershell
   dotnet test MappingDemo.sln --no-restore --disable-build-servers -m:1
   ```

4. ตรวจว่า runtime folders ว่าง:

   ```powershell
   Get-ChildItem ./input,./archive,./data -Recurse
   ```

ผลลัพธ์ควรไม่มีไฟล์ และ Docker services ทั้งสามตัวยัง healthy
