# Mapping Demo — แผนสร้างทีละ Step (แบบเรียน)

แผนนี้พาสร้าง Mapping Demo ตั้งแต่ solution เปล่าจนครบ flow ใน [แผน Demo](demo-plan.md)
แต่ละ step ย่อยพอที่จะทำเสร็จ ตรวจผล และเข้าใจได้ในรอบเดียว

คำศัพท์ทั้งหมดยึดตาม [CONTEXT.md](../CONTEXT.md) และข้อตกลงการออกแบบยึดตาม [demo-plan.md](demo-plan.md)

---

## วิธีใช้แผนนี้

1. ทำตามลำดับ ห้ามข้าม step เพราะ step ถัดไปใช้สิ่งที่ step ก่อนหน้าสร้างไว้
2. แต่ละ step มี 3 ส่วน
   - **ทำ** — สิ่งที่ต้องสร้างหรือแก้
   - **เข้าใจ** — concept และกลไกเบื้องหลังที่ต้องเข้าใจก่อนติ๊ก
   - **ตรวจ** — วิธีพิสูจน์ว่า step นี้ทำงานจริง
3. เวลาให้ Claude ช่วย ให้สั่งทีละ step เช่น `ทำ Step 4.3` โดย Claude จะ
   - อธิบายสิ่งที่จะทำและเหตุผลก่อนแก้โค้ด
   - implement **เฉพาะ step นั้น** ไม่ทำ step ถัดไปล่วงหน้า
   - อธิบายโค้ดที่เขียนทีละส่วน และบอกวิธีตรวจ
4. ตรวจผ่านและเข้าใจแล้วจึงเปลี่ยน `- [ ]` เป็น `- [x]`
5. ถ้ายังอธิบาย concept ของ step ด้วยคำพูดตัวเองไม่ได้ ให้ถามก่อนติ๊ก
6. Commit ท้ายทุก Phase (มี step กำกับไว้)

## ความคืบหน้าราย Phase

- [ ] Phase 0 — เตรียมความเข้าใจและเครื่อง
- [ ] Phase 1 — โครง solution เปล่า
- [ ] Phase 2 — Infrastructure บน Docker ที่ local (PostgreSQL + Kafka + Kafka UI)
- [ ] Phase 3 — เชื่อม PostgreSQL จาก .NET และระบบ migration
- [ ] Phase 4 — Table Definition และการสร้าง DDL
- [ ] Phase 5 — API จัดการตาราง
- [ ] Phase 6 — API จัดการ Mapping Config และ Config Version
- [ ] Phase 7 — ตารางงานและ Outbox
- [ ] Phase 8 — File Watcher
- [ ] Phase 9 — Worker: Outbox Dispatcher
- [ ] Phase 10 — Worker: รับ File Import Job และหน่วง 30 วินาที
- [ ] Phase 11 — Worker: อ่าน CSV และนำเข้า Source Table
- [ ] Phase 12 — Archive
- [ ] Phase 13 — กฎแปลงข้อมูล (pure logic + unit test)
- [ ] Phase 14 — Worker: Normalize Consumer
- [ ] Phase 15 — API ดูสถานะงาน
- [ ] Phase 16 — Retry
- [ ] Phase 17 — Reprocess
- [ ] Phase 18 — Demo script และตรวจรับ

---

## ภาพรวมระบบที่จะได้

```text
                 ┌──────────────── PostgreSQL ────────────────┐
                 │ metadata · file_jobs · row_jobs · outbox    │
                 │ Source tables · Normalized tables · errors  │
                 └───▲──────────────▲────────────────▲─────────┘
                     │              │                │
  HTTP ──► Config API│   input/ ──► │File Watcher    │ Mapping Worker
          (ตาราง,     │   (partner)  │(job + outbox)  │ ├─ Outbox Dispatcher ──► Kafka
           config,   │              │                │ ├─ File Import consumer ◄── mapping.file-import
           retry)    │              │                │ └─ Normalize consumer   ◄── mapping.row-normalize
                                                     │
                                          data/staging/ (snapshot) · archive/
```

| Process | คุยกับอะไร | ไม่คุยกับอะไร |
|---|---|---|
| Config API | PostgreSQL (รวมเขียน outbox สำหรับ retry/reprocess) | Kafka |
| File Watcher | file system, PostgreSQL (file job + outbox) | Kafka |
| Mapping Worker | PostgreSQL, Kafka, file system (staging/archive) | — |

จุดสำคัญที่จะเข้าใจตลอดแผน: **มีแค่ Worker ที่คุยกับ Kafka** เพราะทุกคำสั่งถูกบันทึกลง outbox ใน PostgreSQL ก่อน แล้ว dispatcher ใน Worker ค่อยส่งต่อ

## เครื่องมือที่เลือกและเหตุผล

| เรื่อง | เลือก | เหตุผล |
|---|---|---|
| Runtime | .NET 10 SDK (เครื่องมี 10.0.401) | LTS ล่าสุด |
| API | ASP.NET Core Web API (controllers) | รูปแบบมาตรฐานของ REST API ใน .NET: `[ApiController]`, routing ด้วย attribute, model binding |
| Watcher / Worker | Worker Service template (`BackgroundService`) | รูปแบบมาตรฐานของ process ที่รันยาว |
| Database | PostgreSQL 17 บน Docker | transactional DDL, `jsonb`, partial unique index |
| Queue | Kafka (image `apache/kafka`, โหมด KRaft) | ตามข้อกำหนด; เห็น partition และ consumer group |
| ดู Kafka | Kafka UI (`kafbat/kafka-ui`) | ดู topic, partition, message ด้วยตา |
| Data access | Npgsql + Dapper | ตารางถูกสร้างแบบ dynamic ผ่าน API จึงต้องเขียน SQL เอง EF Core ไม่เหมาะ |
| Kafka client | Confluent.Kafka | client หลักของ .NET |
| CSV | CsvHelper | จัดการ quote, comma และขึ้นบรรทัดใหม่ในค่าได้ถูกต้อง |
| Test | xUnit | ทดสอบ logic ที่ไม่ต้องพึ่ง DB |

## Infrastructure รันบน Docker ที่ local ทั้งหมด

Infrastructure ทุกตัวรันเป็น container บนเครื่องเราผ่าน `docker-compose.yml` ไฟล์เดียวที่ root ของ repo ไม่ต้องติดตั้ง PostgreSQL หรือ Kafka ลงเครื่องเอง

| Service ใน compose | Image | Port บนเครื่อง | หน้าที่ |
|---|---|---|---|
| `postgres` | `postgres:17` | `5432` | PostgreSQL: metadata, job, outbox, Source/Normalized tables |
| `kafka` | `apache/kafka` (ระบุเวอร์ชัน 4.x) | `9092` | Kafka broker + controller (KRaft) |
| `kafka-ui` | `kafbat/kafka-ui` | `8080` | หน้าเว็บดู topic, partition และ message |

- ใช้ image ทางการทั้งหมดจึงไม่ต้องเขียน `Dockerfile` สำหรับ infra; `docker-compose.yml` คือไฟล์เดียวที่ประกาศ container, port, volume และ healthcheck
- ข้อมูล PostgreSQL อยู่ใน named volume จึงคงอยู่ข้ามการ restart; `docker compose down -v` คือการล้างเริ่มใหม่
- คำสั่งหลัก: `docker compose up -d` (เปิด), `docker compose ps` (ดูสถานะ), `docker compose logs -f <service>` (ดู log), `docker compose down` (ปิด)
- .NET ทั้ง 3 process (API, Watcher, Worker) **รันบนเครื่องด้วย `dotnet run` ไม่อยู่ใน Docker** ตามข้อตกลงใน demo-plan เพื่อให้ตั้ง breakpoint และ debug ได้ตรงๆ จึงต่อ infra ผ่าน `localhost`
- สร้างทีละ service ใน Phase 2 และเรียนรู้แต่ละตัวก่อนเพิ่มตัวถัดไป

## โครงสร้าง repository เมื่อเสร็จ

```text
mapping-demo/
├─ MappingDemo.slnx
├─ global.json
├─ Directory.Build.props
├─ docker-compose.yml
├─ scripts/                 create-topics, generate-sample, reset-demo
├─ requests/                ไฟล์ .http สำหรับเรียก API
├─ src/
│  ├─ MappingDemo.Api/
│  ├─ MappingDemo.Watcher/
│  ├─ MappingDemo.Worker/
│  └─ MappingDemo.Shared/   DB, migrations, contracts, กฎแปลงข้อมูล
├─ tests/
│  └─ MappingDemo.Tests/
├─ input/ archive/ data/    ข้อมูลตอนรัน (อยู่ใน .gitignore แล้ว)
└─ docs/
```

## ข้อสมมติของแผน — ยืนยันเมื่อถึง step ที่เกี่ยวข้อง

ข้อเหล่านี้ไม่ได้ตกลงไว้ใน demo-plan แผนนี้จึงเลือกค่าเริ่มต้นไว้ เปลี่ยนได้

| # | ข้อสมมติ | ใช้ใน |
|---|---|---|
| A1 | ข้อมูลตัวอย่างเป็น `orders.csv`: `order_no`, `customer_name`, `order_date` (date), `amount` (decimal), `is_paid` (boolean), `note` (ไม่ required) | Phase 5–6, 18 |
| A2 | Reprocess ทำทั้งไฟล์ (ทุก Source row ของ file job) ส่วนการเลือกบางแถวยังเปิดอยู่ใน demo-plan | Phase 17 |
| A3 | API เป็นผู้รัน migration ตอนเริ่ม จึงต้องเปิด API ก่อน Watcher และ Worker | Phase 3 |
| A4 | คอลัมน์ใน Normalized table เป็น nullable ใน DB; การบังคับ required ทำใน Worker | Phase 4 |
| A5 | Scan ชดเชยข้าม path ที่ยังมี file job ค้าง (`Queued`/`Delaying`/`Importing`) เพื่อไม่ให้ไฟล์ที่ยังโตอยู่ได้ intake key ใหม่ | Phase 8 |
| A6 | ทั้งสอง topic มี 3 partitions และ Worker เปิด consumer 3 ตัวต่อ topic | Phase 2, 10, 14 |
| A7 | Watcher โหลด active config ใหม่ทุก 10 วินาที และ scan ชดเชยทุก 60 วินาที | Phase 8 |

---

## Phase 0 — เตรียมความเข้าใจและเครื่อง

- [ ] **0.1 อ่านคำศัพท์ของระบบ**
  - ทำ: อ่าน [CONTEXT.md](../CONTEXT.md) ทั้งไฟล์
  - เข้าใจ: ความต่างของ Source Table กับ Normalized Table, File Import Job กับ Row Normalization Job, Retry กับ Reprocess
  - ตรวจ: อธิบายคู่คำทั้ง 3 คู่ด้วยคำพูดตัวเองได้ โดยไม่เปิดเอกสาร

- [ ] **0.2 ไล่ flow หลักจาก sequence diagram**
  - ทำ: เปิด [mapping-demo-sequence.drawio](diagrams/mapping-demo-sequence.drawio) หน้า 01 Main Flow แล้วอ่านคู่กับ "ผลต่อวงจรงาน" ใน demo-plan
  - เข้าใจ: ไฟล์หนึ่งไฟล์เดินทางผ่านอะไรบ้าง และตรงไหนที่ "ทำขนานกัน"
  - ตรวจ: วาด flow บนกระดาษจาก Input Folder ถึง Normalized Table ได้เอง

- [ ] **0.3 ตรวจเครื่องมือ**
  - ทำ: รัน `dotnet --list-sdks`, `docker --version`, `docker compose version`
  - เข้าใจ: SDK ใช้ build/run, Docker ใช้รัน PostgreSQL กับ Kafka, Compose ใช้ประกาศหลาย container ในไฟล์เดียว
  - ตรวจ: เห็น SDK 10.x และ Docker Desktop กำลังรันอยู่

- [ ] **0.4 เตรียม VS Code**
  - ทำ: ติดตั้ง extension C# Dev Kit และ REST Client (ใช้ยิงไฟล์ `.http`)
  - เข้าใจ: C# Dev Kit ให้ IntelliSense และ debugger; ไฟล์ `.http` เก็บ request ไว้ใน repo ทำซ้ำได้
  - ตรวจ: เปิดไฟล์ `.cs` แล้วมี IntelliSense

---

## Phase 1 — โครง solution เปล่า (ยังไม่มี dependency)

- [ ] **1.1 สร้างโฟลเดอร์ `src` และ `tests`**
  - ทำ: สร้างโฟลเดอร์ว่าง `src/` และ `tests/` ที่ root
  - เข้าใจ: แยกโค้ดที่ส่งขึ้นใช้งานออกจากโค้ดทดสอบ เป็น convention ของ .NET repo ส่วนใหญ่
  - ตรวจ: `ls` เห็นทั้งสองโฟลเดอร์

- [ ] **1.2 สร้าง solution file**
  - ทำ: `dotnet new sln -n MappingDemo`
  - เข้าใจ: solution ไม่มีโค้ด เป็นแค่รายการ project ที่ build ด้วยกัน .NET 10 สร้างไฟล์ `.slnx` (XML อ่านง่าย) แทน `.sln` แบบเก่า
  - ตรวจ: เปิด `MappingDemo.slnx` แล้วเห็นว่ายังว่าง

- [ ] **1.3 ตรึงเวอร์ชัน SDK ด้วย `global.json`**
  - ทำ: `dotnet new globaljson --sdk-version 10.0.401 --roll-forward latestFeature`
  - เข้าใจ: ถ้าเครื่องมีหลาย SDK ไฟล์นี้กำหนดว่า repo ใช้ตัวไหน `latestFeature` ยอมให้ใช้ patch/feature ที่ใหม่กว่าใน major เดียวกัน
  - ตรวจ: `dotnet --version` ที่ root แสดง 10.0.x

- [ ] **1.4 สร้าง `Directory.Build.props`**
  - ทำ: สร้างไฟล์ที่ root กำหนด `Nullable=enable` และ `ImplicitUsings=enable`
  - เข้าใจ: MSBuild อ่านไฟล์ชื่อนี้อัตโนมัติจากโฟลเดอร์แม่ของทุก project จึงตั้งค่ากลางได้ที่เดียว; Nullable ทำให้ compiler เตือนเมื่ออาจใช้ค่า `null`
  - ตรวจ: ยังไม่มี project ให้ตรวจ จะเห็นผลใน 1.5

- [ ] **1.5 สร้าง ASP.NET Core Web API project ที่ไม่มี dependency**
  - ทำ: `dotnet new webapi -n MappingDemo.Api -o src/MappingDemo.Api --use-controllers --no-openapi`
  - เข้าใจ: `--use-controllers` ได้โครงแบบ controller; `--no-openapi` ตัด package OpenAPI ออก project จึงไม่มี NuGet package เลย อ่าน `Program.cs` ทีละบรรทัด: `CreateBuilder` → `AddControllers` (ลงทะเบียน controller ใน DI) → `Build` → `MapControllers` (ผูก route จาก attribute) → `Run`
  - ตรวจ: เปิด `.csproj` แล้วไม่มี `PackageReference`

- [ ] **1.6 เพิ่ม API เข้า solution**
  - ทำ: `dotnet sln add src/MappingDemo.Api`
  - เข้าใจ: การ add ทำให้ `dotnet build` ที่ root build project นี้ด้วย
  - ตรวจ: `dotnet sln list` เห็น project

- [ ] **1.7 รัน API ครั้งแรกและอ่าน controller ตัวอย่าง**
  - ทำ: `dotnet run --project src/MappingDemo.Api` แล้วเรียก `GET /weatherforecast`
  - เข้าใจ: Kestrel คือ web server ใน process; port มาจาก `Properties/launchSettings.json`; อ่าน `WeatherForecastController`: `[ApiController]`, `[Route("[controller]")]`, `[HttpGet]` และการที่ action คืน object แล้ว framework แปลงเป็น JSON ให้
  - ตรวจ: ได้ JSON รายการพยากรณ์อากาศ

- [ ] **1.8 ลบตัวอย่างและสร้าง `HealthController`**
  - ทำ: ลบ `WeatherForecastController.cs` และ `WeatherForecast.cs` แล้วสร้าง `Controllers/HealthController.cs` ที่มี `[HttpGet]` คืน `Ok(new { status = "ok" })` ที่ route `health`
  - เข้าใจ: controller สืบทอด `ControllerBase` (ไม่ต้องใช้ `Controller` ที่มี view); `Ok(...)` คืน status 200; route มาจาก attribute ไม่ใช่ชื่อไฟล์
  - ตรวจ: `curl http://localhost:<port>/health` ได้ `{"status":"ok"}`

- [ ] **1.9 สร้าง Watcher project**
  - ทำ: `dotnet new worker -n MappingDemo.Watcher -o src/MappingDemo.Watcher` แล้ว `dotnet sln add`
  - เข้าใจ: อ่าน `Program.cs` (Generic Host) และ `Worker.cs` (`BackgroundService.ExecuteAsync`) ว่า host เรียก `ExecuteAsync` ตอนเริ่มและยกเลิก `stoppingToken` ตอนหยุด
  - ตรวจ: `dotnet build` ผ่าน

- [ ] **1.10 รัน Watcher และสังเกต graceful shutdown**
  - ทำ: รัน Watcher แล้วกด Ctrl+C
  - เข้าใจ: loop ใน `ExecuteAsync` ตรวจ `stoppingToken` และ `Task.Delay(..., stoppingToken)` ถูกยกเลิกทันทีเมื่อ shutdown นี่คือกลไกเดียวกับที่จะใช้ยกเลิกการหน่วง 30 วินาทีใน Phase 10
  - ตรวจ: เห็น log ทุก 1 วินาที และ process หยุดทันทีเมื่อกด Ctrl+C

- [ ] **1.11 สร้าง Worker project**
  - ทำ: `dotnet new worker -n MappingDemo.Worker -o src/MappingDemo.Worker` แล้ว `dotnet sln add`
  - เข้าใจ: Watcher กับ Worker แยก process กันเพื่อ restart/debug แยกกันได้ และจำลองว่าในระบบจริงอยู่คนละเครื่องได้
  - ตรวจ: `dotnet sln list` เห็น 3 project

- [ ] **1.12 สร้าง Shared class library**
  - ทำ: `dotnet new classlib -n MappingDemo.Shared -o src/MappingDemo.Shared` ลบ `Class1.cs` แล้ว `dotnet sln add`
  - เข้าใจ: โค้ดที่หลาย process ใช้ร่วมกัน (DB, message contract, กฎแปลงข้อมูล) อยู่ที่นี่
  - ตรวจ: build ผ่าน

- [ ] **1.13 อ้างอิง Shared จากทั้ง 3 process**
  - ทำ: `dotnet add src/MappingDemo.Api reference src/MappingDemo.Shared` และทำซ้ำกับ Watcher และ Worker
  - เข้าใจ: ทิศทาง dependency คือ app → Shared เท่านั้น ถ้า Shared อ้างกลับไปหา app จะเกิดวงวน
  - ตรวจ: `.csproj` ของทั้ง 3 มี `ProjectReference`

- [ ] **1.14 สร้าง test project**
  - ทำ: `dotnet new xunit -n MappingDemo.Tests -o tests/MappingDemo.Tests`, add เข้า sln และ reference Shared
  - เข้าใจ: `[Fact]` คือ test หนึ่งกรณี; `dotnet test` หา test ทุกตัวใน solution
  - ตรวจ: `dotnet test` ผ่าน 1 test ตัวอย่าง

- [ ] **1.15 Build ทั้ง solution**
  - ทำ: `dotnet build` ที่ root
  - เข้าใจ: `bin/` เก็บผล build, `obj/` เก็บไฟล์ระหว่างทาง ทั้งสองถูก ignore ใน `.gitignore` แล้ว
  - ตรวจ: build ผ่าน 0 warning, `git status` ไม่เห็น `bin/` หรือ `obj/`

- [ ] **1.16 Commit Phase 1**
  - ทำ: commit ข้อความ `chore: scaffold solution and projects`
  - ตรวจ: `git log` เห็น commit

---

## Phase 2 — Infrastructure บน Docker ที่ local (PostgreSQL + Kafka + Kafka UI)

ทุก service ใน phase นี้อยู่ใน `docker-compose.yml` ไฟล์เดียว และรันบนเครื่องเราทั้งหมด (ดูหัวข้อ "Infrastructure รันบน Docker ที่ local ทั้งหมด")

- [ ] **2.1 สร้าง `docker-compose.yml` ที่มีแค่ PostgreSQL**
  - ทำ: service `postgres` image `postgres:17`, env user/password/db = `mapping`, port `5432:5432`, named volume
  - เข้าใจ: image คือแม่แบบ container คือตัวที่รันอยู่ volume เก็บข้อมูลแยกจาก container port mapping ทำให้เครื่องเราเข้าถึง container ได้ ต้องระบุเวอร์ชัน image เพื่อให้ผลเหมือนเดิมทุกครั้ง; รหัสผ่านนี้ใช้ในเครื่องเท่านั้น
  - ตรวจ: `docker compose config` ไม่ error

- [ ] **2.2 เปิด PostgreSQL**
  - ทำ: `docker compose up -d postgres` แล้ว `docker compose ps` และ `docker compose logs postgres`
  - เข้าใจ: `-d` รันเบื้องหลัง; log บอกว่า database พร้อมรับ connection แล้ว
  - ตรวจ: สถานะ `running` และ log มี `ready to accept connections`

- [ ] **2.3 เข้า `psql` และลองคำสั่งพื้นฐาน**
  - ทำ: `docker compose exec postgres psql -U mapping -d mapping` แล้วลอง `\l`, `\dt`, สร้างและลบตารางทดสอบ
  - เข้าใจ: ลำดับชั้น server → database → schema (`public`) → table
  - ตรวจ: สร้าง `CREATE TABLE t(x int)`, insert, select, drop ได้

- [ ] **2.4 ทดลองความคงทนของ volume**
  - ทำ: สร้างตาราง, `docker compose down`, `up` ใหม่ แล้วดูว่าตารางยังอยู่ จากนั้นลอง `down -v`
  - เข้าใจ: `down` ลบ container แต่เก็บ volume; `down -v` ลบข้อมูลทั้งหมด ใช้ reset demo ภายหลัง
  - ตรวจ: ตารางยังอยู่หลัง `down` และหายหลัง `down -v`

- [ ] **2.5 เพิ่ม healthcheck ให้ PostgreSQL**
  - ทำ: เพิ่ม `healthcheck` ใช้ `pg_isready -U mapping`
  - เข้าใจ: container "running" ไม่ได้แปลว่า database พร้อม healthcheck บอกความพร้อมจริง
  - ตรวจ: `docker compose ps` แสดง `healthy`

- [ ] **2.6 เพิ่ม Kafka แบบ KRaft single node**
  - ทำ: service `kafka` image `apache/kafka` (ระบุเวอร์ชัน 4.x) ตั้ง node เดียวทำหน้าที่ทั้ง broker และ controller พร้อม listener 2 ชุด
  - เข้าใจ:
    - broker เก็บ message; controller จัดการ metadata ของ cluster; KRaft คือโหมดที่ไม่ต้องใช้ ZooKeeper
    - listener ภายใน (`kafka:19092`) ให้ container อื่นใช้ และ listener ภายนอก (`localhost:9092`) ให้ .NET บนเครื่องเราใช้
    - `advertised.listeners` คือที่อยู่ที่ broker บอก client ให้ต่อกลับมา ถ้าตั้งผิด client จะต่อครั้งแรกได้แต่ส่ง message ไม่ได้
  - ตรวจ: `docker compose up -d kafka` แล้ว log ไม่มี error

- [ ] **2.7 เรียนรู้ topic และ partition ด้วย CLI**
  - ทำ: สร้าง topic `demo.test` ที่มี 3 partitions ด้วย `kafka-topics.sh --create` แล้ว `--describe`
  - เข้าใจ: topic คือ log ที่แบ่งเป็น partition; message ในแต่ละ partition มี offset เรียงกัน; ลำดับรับประกันเฉพาะภายใน partition เดียว
  - ตรวจ: describe เห็น 3 partitions

- [ ] **2.8 ส่งและอ่าน message พร้อม key**
  - ทำ: ใช้ `kafka-console-producer.sh` ส่ง `a:1`, `b:2`, `a:3` (เปิด `parse.key`) แล้วอ่านด้วย `kafka-console-consumer.sh` ที่แสดง partition และ key
  - เข้าใจ: Kafka hash key เพื่อเลือก partition key เดียวกันจึงไป partition เดิมเสมอ แผนนี้จะใช้ `fileJobId` และ `sourceRowId` เป็น key
  - ตรวจ: message key `a` ทั้งสองอยู่ partition เดียวกัน

- [ ] **2.9 ทดลอง consumer group**
  - ทำ: เปิด consumer 2 ตัวใน group เดียวกัน ส่ง message หลาย key แล้วเปิดอีกตัวใน group ใหม่
  - เข้าใจ: consumer ใน group เดียวกันแบ่ง partition กัน (1 partition มีเจ้าของ 1 ตัว) ส่วนคนละ group ต่างได้ message ครบทุกตัว; group จำ offset ที่ commit แล้ว
  - ตรวจ: สองตัวใน group เดียวได้ message คนละส่วน ตัวใน group ใหม่ได้ครบ

- [ ] **2.10 ปิด auto-create topic และเพิ่ม Kafka UI**
  - ทำ: ตั้ง `KAFKA_AUTO_CREATE_TOPICS_ENABLE=false` และเพิ่ม service `kafka-ui` (`kafbat/kafka-ui`) ต่อผ่าน `kafka:19092` เปิด port 8080
  - เข้าใจ: ถ้าเปิด auto-create การพิมพ์ชื่อ topic ผิดจะสร้าง topic ใหม่แบบเงียบๆ พร้อมจำนวน partition ที่ไม่ได้ตั้งใจ
  - ตรวจ: เปิด `http://localhost:8080` เห็น topic `demo.test`

- [ ] **2.11 Script สร้าง topic ของระบบ**
  - ทำ: `scripts/create-topics.ps1` สร้าง `mapping.file-import` และ `mapping.row-normalize` อย่างละ 3 partitions (A6) ใช้ `--if-not-exists`
  - เข้าใจ: script ที่รันซ้ำได้ผลเดิม (idempotent) ใช้ตอน reset demo ได้; จำนวน partition คือเพดานของ consumer ที่ทำงานขนานใน group เดียว
  - ตรวจ: รัน script 2 ครั้งไม่ error และ Kafka UI เห็นทั้งสอง topic

- [ ] **2.12 ลบ topic ทดสอบและ commit**
  - ทำ: ลบ `demo.test` แล้ว commit `chore: add docker infrastructure`
  - ตรวจ: Kafka UI เหลือ 2 topic

---

## Phase 3 — เชื่อม PostgreSQL จาก .NET และระบบ migration

- [ ] **3.1 เพิ่ม Npgsql เข้า Shared**
  - ทำ: `dotnet add src/MappingDemo.Shared package Npgsql`
  - เข้าใจ: Npgsql คือ ADO.NET provider ของ PostgreSQL; `PackageReference` ใน Shared ถูกส่งต่อให้ project ที่อ้าง Shared (transitive)
  - ตรวจ: `dotnet build` ผ่าน

- [ ] **3.2 ใส่ connection string ใน configuration ของ API**
  - ทำ: เพิ่ม `ConnectionStrings:Mapping` ใน `appsettings.Development.json`
  - เข้าใจ: configuration ซ้อนเป็นชั้น (`appsettings.json` → `appsettings.{Environment}.json` → environment variable) ชั้นหลังทับชั้นก่อน; `ASPNETCORE_ENVIRONMENT` เลือกไฟล์
  - ตรวจ: อ่านค่าด้วย `builder.Configuration.GetConnectionString("Mapping")` แล้ว log ออกมาได้

- [ ] **3.3 ลงทะเบียน `NpgsqlDataSource` ใน DI**
  - ทำ: เขียน extension method ใน Shared เช่น `AddMappingDatabase(configuration)` ที่ register `NpgsqlDataSource` แบบ singleton
  - เข้าใจ: DI lifetime (singleton, scoped, transient); data source ถือ connection pool จึงเป็น singleton ส่วน connection เปิดสั้นๆ แล้วคืน pool
  - ตรวจ: API เริ่มได้โดยไม่ error

- [ ] **3.4 Action `GET /health/db`**
  - ทำ: ใน `HealthController` รับ `NpgsqlDataSource` ผ่าน constructor แล้วเพิ่ม action `[HttpGet("db")]` ที่เปิด connection และรัน `SELECT 1`
  - เข้าใจ: DI ส่ง dependency เข้า constructor ของ controller ทุก request; `await using` คืน connection ให้ pool เมื่อจบ scope; ถ้า DB ปิด จะเห็น exception จริงจาก Npgsql
  - ตรวจ: ได้ 200 เมื่อ DB เปิด และได้ error เมื่อ `docker compose stop postgres`

- [ ] **3.5 เพิ่ม Dapper**
  - ทำ: `dotnet add src/MappingDemo.Shared package Dapper` แล้วเขียน query ใน 3.4 ใหม่ด้วย `ExecuteScalarAsync`
  - เข้าใจ: Dapper เป็น extension บน `DbConnection` ที่ map ผลลัพธ์เข้า object และส่ง parameter (`@name`) แยกจาก SQL จึงกัน SQL injection ของค่าข้อมูลได้
  - ตรวจ: `/health/db` ยังได้ผลเดิม

- [ ] **3.6 ออกแบบ migration runner**
  - ทำ: สร้าง `MigrationRunner` ใน Shared ที่สร้างตาราง `schema_migrations(version text primary key, applied_at timestamptz)` ถ้ายังไม่มี
  - เข้าใจ: migration คือไฟล์ SQL เรียงลำดับ แต่ละไฟล์รันครั้งเดียว ตาราง `schema_migrations` จำว่ารันไฟล์ไหนแล้ว
  - ตรวจ: เรียก runner แล้วเห็นตารางใน `psql`

- [ ] **3.7 ฝังไฟล์ SQL เป็น embedded resource**
  - ทำ: สร้างโฟลเดอร์ `src/MappingDemo.Shared/Database/Migrations/` และตั้ง `<EmbeddedResource Include="Database/Migrations/*.sql" />`
  - เข้าใจ: embedded resource อยู่ใน `.dll` จึงไม่ต้องกังวลเรื่อง path ตอนรัน อ่านด้วย `Assembly.GetManifestResourceStream`
  - ตรวจ: runner list ชื่อ resource ออกมาได้ (ตอนนี้ยังว่าง)

- [ ] **3.8 รัน migration ที่ยังไม่เคยรันใน transaction**
  - ทำ: เรียงไฟล์ตามชื่อ ข้ามไฟล์ที่มีใน `schema_migrations` แล้วรันแต่ละไฟล์พร้อม insert version ใน transaction เดียว
  - เข้าใจ: PostgreSQL รองรับ transactional DDL ถ้าไฟล์ล้มกลางทาง ทุกอย่างในไฟล์ rollback ไม่เหลือ schema ครึ่งๆ
  - ตรวจ: ยังไม่มีไฟล์ จะตรวจจริงใน Phase 4

- [ ] **3.9 กันรัน migration ซ้อนด้วย advisory lock**
  - ทำ: เรียก `pg_advisory_lock(<ตัวเลขคงที่>)` ก่อนรัน และ unlock หลังจบ
  - เข้าใจ: advisory lock เป็น lock ตามตัวเลขที่ application ตกลงกันเอง ถ้าสอง process เริ่มพร้อมกัน ตัวที่สองจะรอ
  - ตรวจ: อ่านโค้ดแล้วอธิบายได้ว่าเกิดอะไรถ้ารัน API 2 ตัวพร้อมกัน

- [ ] **3.10 เรียก runner ตอน API เริ่ม (A3)**
  - ทำ: ใน `Program.cs` เรียก runner หลัง `Build()` ก่อน `Run()`
  - เข้าใจ: ถ้า migration ล้ม API ต้องไม่เริ่มรับ request; ลำดับการเปิดระบบจึงเป็น Docker → API → Watcher → Worker
  - ตรวจ: เปิด API แล้ว `schema_migrations` ถูกสร้าง

- [ ] **3.11 Commit Phase 3**
  - ทำ: commit `feat: connect to postgres and add migration runner`

---

## Phase 4 — Table Definition และการสร้าง DDL

Concept ของ phase นี้: ระบบเก็บ **metadata** (คำอธิบายว่าตารางมีคอลัมน์อะไร ชนิดอะไร required ไหม) แยกจาก **ตารางจริง** ที่เก็บข้อมูล API เขียนทั้งสองอย่างพร้อมกัน

- [ ] **4.1 Migration `0001_table_definitions.sql`**
  - ทำ: ตาราง `table_definitions(id, name unique, kind check in ('Source','Normalized'), created_at)` และ `table_columns(id, table_id fk, name, data_type check, is_required, ordinal, unique(table_id, name))`
  - เข้าใจ: `CHECK` กันค่าผิดที่ระดับ DB, `UNIQUE` กันชื่อซ้ำแม้ application มี bug, foreign key กันคอลัมน์ที่ไม่มีตารางแม่
  - ตรวจ: เปิด API แล้ว `\d table_columns` เห็นโครงสร้าง และ `schema_migrations` มี `0001`

- [ ] **4.2 C# model ของ table definition**
  - ทำ: ใน Shared สร้าง `enum TableKind`, `enum ColumnDataType { Text, Date, Decimal, Boolean }`, `record TableDefinition`, `record ColumnDefinition`
  - เข้าใจ: `record` เปรียบเทียบด้วยค่าและเหมาะกับข้อมูลที่ไม่ควรถูกแก้หลังสร้าง
  - ตรวจ: build ผ่าน

- [ ] **4.3 เขียน test ของกฎชื่อ identifier ก่อน**
  - ทำ: test ว่า `orders`, `order_no` ผ่าน; `Orders`, `1abc`, `a-b`, `a;drop`, ชื่อยาวเกิน 63 ตัว และชื่อที่ขึ้นต้นด้วย `pg_` ไม่ผ่าน
  - เข้าใจ: ชื่อตาราง/คอลัมน์ส่งเป็น parameter ไม่ได้ ต้องต่อเข้า SQL ตรงๆ จึงต้องจำกัดรูปแบบก่อน นี่คือจุดที่ SQL injection เกิดได้ถ้าไม่ตรวจ
  - ตรวจ: `dotnet test` แดง (ยังไม่มี implementation)

- [ ] **4.4 Implement `IdentifierRules`**
  - ทำ: regex `^[a-z][a-z0-9_]{0,62}$` และรายการชื่อสงวน (คอลัมน์ metadata เช่น `id`, `file_job_id`, `row_number`)
  - เข้าใจ: 63 คือความยาวสูงสุดของ identifier ใน PostgreSQL; ชื่อสงวนกันคอลัมน์ข้อมูลชนกับคอลัมน์ระบบ
  - ตรวจ: test ใน 4.3 เขียวทั้งหมด

- [ ] **4.5 Helper `SqlIdentifier.Quote`**
  - ทำ: ครอบชื่อด้วย `"..."` และแทน `"` ภายในด้วย `""` พร้อม test
  - เข้าใจ: defense in depth — ต่อให้ validation หลุด การ quote ยังกันการปิด identifier ก่อนกำหนด
  - ตรวจ: test ผ่าน

- [ ] **4.6 สร้าง DDL ของ Source table**
  - ทำ: `DdlBuilder.CreateSourceTable(definition)` คืน SQL ที่มีคอลัมน์ระบบ `id bigint generated always as identity primary key`, `file_job_id bigint not null`, `row_number int not null`, `imported_at timestamptz not null default now()`, `unique(file_job_id, row_number)` ตามด้วยคอลัมน์ข้อมูลที่เป็น `text` ทั้งหมด
  - เข้าใจ: Source คือ landing zone เก็บค่าดิบเป็นข้อความเพื่อไม่ให้การนำเข้าล้มเพราะชนิดข้อมูล; `unique(file_job_id, row_number)` คือกุญแจที่ทำให้ retry นำเข้าไม่สร้างแถวซ้ำ
  - ตรวจ: unit test เทียบ SQL ที่ได้

- [ ] **4.7 สร้าง DDL ของ Normalized table**
  - ทำ: คอลัมน์ระบบ `id`, `source_row_id bigint not null unique`, `file_job_id`, `row_number`, `row_job_id`, `config_version_id`, `normalized_at` และคอลัมน์ข้อมูลตามชนิด text→`text`, date→`date`, decimal→`numeric`, boolean→`boolean` ทั้งหมด nullable (A4)
  - เข้าใจ: `source_row_id unique` ทำให้ Source หนึ่งแถวมีผลปัจจุบันได้แถวเดียว (Current Normalized Result); ไม่ใช้ `NOT NULL` ตาม required เพราะการเพิ่มคอลัมน์ในตารางที่มีข้อมูลแล้วจะล้ม และ Worker ต้องบันทึก required error เป็น Row Error อยู่แล้ว
  - ตรวจ: unit test เทียบ SQL

- [ ] **4.8 Validation ของ table definition**
  - ทำ: `TableDefinitionValidator` ตรวจชื่อ, คอลัมน์อย่างน้อย 1, ชื่อคอลัมน์ไม่ซ้ำ, Source ต้องเป็น text ทุกคอลัมน์และไม่มี required
  - เข้าใจ: แยก validation เป็น pure function (รับข้อมูล คืนรายการ error) เพื่อ test ได้โดยไม่ต้องมี DB
  - ตรวจ: unit test ครอบคลุมแต่ละกฎ

- [ ] **4.9 Commit Phase 4**
  - ทำ: commit `feat: add table definition model and DDL builder`

---

## Phase 5 — API จัดการตาราง

- [ ] **5.1 DTO ของ request/response**
  - ทำ: `CreateTableRequest(name, kind, columns[])`, `ColumnRequest(name, dataType, isRequired)`, `TableResponse`
  - เข้าใจ: DTO คือรูปร่างของข้อมูลที่ข้าม HTTP แยกจาก model ภายใน เพื่อเปลี่ยนภายในได้โดยไม่ทำ contract พัง; `[ApiController]` รู้เองว่า parameter ที่เป็น class มาจาก JSON body (`[FromBody]`)
  - ตรวจ: build ผ่าน

- [ ] **5.2 สร้าง `TablesController`**
  - ทำ: `Controllers/TablesController.cs` ที่มี `[ApiController]`, `[Route("tables")]` และ action `[HttpGet]` ชั่วคราวคืน `Ok(Array.Empty<TableResponse>())`
  - เข้าใจ: หนึ่ง controller ต่อหนึ่งกลุ่ม resource; action คือ method สาธารณะที่มี attribute HTTP verb
  - ตรวจ: `GET /tables` คืน `[]`

- [ ] **5.3 `POST /tables` — validation เท่านั้น**
  - ทำ: เรียก validator จาก 4.8 ถ้าผิดให้ใส่ error ลง `ModelState` แล้วคืน `ValidationProblem(ModelState)` ถ้าถูกคืน 501 ชั่วคราว
  - เข้าใจ: `[ApiController]` คืน 400 อัตโนมัติเมื่อ JSON bind ไม่ได้; กฎธุรกิจที่เราเขียนเองต้องเติมลง `ModelState` เอง; ProblemDetails (RFC 9457) คือรูปแบบมาตรฐานของ error ใน HTTP API
  - ตรวจ: ส่งชื่อ `Bad-Name` ได้ 400 พร้อมรายการ error

- [ ] **5.4 ไฟล์ `requests/tables.http`**
  - ทำ: เขียน request สำหรับ create/list/get/add column ใช้ตัวแปร `@baseUrl`
  - เข้าใจ: เก็บ request ไว้ใน repo ทำให้ทดสอบซ้ำได้และเป็นเอกสารของ API ไปในตัว
  - ตรวจ: กด Send Request ใน VS Code ได้ผล

- [ ] **5.5 บันทึก metadata และสร้างตารางใน transaction เดียว**
  - ทำ: `TableService.CreateAsync` insert `table_definitions` + `table_columns` แล้วรัน DDL จาก 4.6/4.7 ใน transaction เดียว; controller คืน `CreatedAtAction(...)` (201 พร้อม header `Location`)
  - เข้าใจ: controller ควรบางและส่งงานให้ service; เพราะ DDL อยู่ใน transaction ได้ ถ้าการสร้างตารางล้ม metadata ก็ rollback ด้วย metadata กับตารางจริงจึงตรงกันเสมอ
  - ตรวจ: สร้างตารางแล้ว `\d <name>` ใน `psql` เห็นคอลัมน์ครบ

- [ ] **5.6 ชื่อซ้ำคืน 409**
  - ทำ: จับ `PostgresException` ที่ `SqlState == "23505"` (unique violation) แล้วคืน `Conflict(...)`
  - เข้าใจ: การตรวจก่อน insert มี race ถ้าสอง request มาพร้อมกัน ให้ DB constraint เป็นตัวตัดสินสุดท้าย แล้วแปลง error เป็น HTTP status
  - ตรวจ: สร้างชื่อเดิมซ้ำได้ 409

- [ ] **5.7 `GET /tables` และ `GET /tables/{id}`**
  - ทำ: query metadata คืนตารางพร้อมคอลัมน์ เรียงตาม `ordinal`
  - เข้าใจ: การ map ผล join หลายแถวเป็น object ซ้อน (one-to-many) ด้วย Dapper
  - ตรวจ: ได้ตารางที่สร้างใน 5.5; id ที่ไม่มีได้ 404

- [ ] **5.8 `POST /tables/{id}/columns`**
  - ทำ: validate แล้ว insert `table_columns` + `ALTER TABLE ... ADD COLUMN` ใน transaction เดียว
  - เข้าใจ: คอลัมน์ใหม่ในตารางที่มีข้อมูลแล้วมีค่า `NULL` ในแถวเดิม; ไม่เปิดให้ลบ/เปลี่ยนชื่อคอลัมน์ เพราะ config version เก่าอ้างถึงชื่อเดิมอยู่
  - ตรวจ: `\d` เห็นคอลัมน์ใหม่ และ `GET /tables/{id}` แสดงด้วย

- [ ] **5.9 สร้างตารางตัวอย่าง (A1)**
  - ทำ: ผ่าน `.http` สร้าง `src_orders` (Source, text ทุกคอลัมน์) และ `norm_orders` (Normalized: `order_no` text required, `customer_name` text required, `order_date` date required, `amount` decimal required, `is_paid` boolean required, `note` text)
  - ตรวจ: `\d src_orders` และ `\d norm_orders` ตรงกับที่คาด

- [ ] **5.10 (ไม่บังคับ) OpenAPI document**
  - ทำ: เพิ่ม package `Microsoft.AspNetCore.OpenApi` พร้อม `AddOpenApi()` และ `MapOpenApi()` (ส่วนที่ตัดออกไปด้วย `--no-openapi` ใน 1.5)
  - เข้าใจ: framework สร้างคำอธิบาย API จาก controller และ action ที่มีอยู่
  - ตรวจ: เปิด `/openapi/v1.json` เห็น `/tables`

- [ ] **5.11 Commit Phase 5**
  - ทำ: commit `feat(api): manage table definitions`

---

## Phase 6 — API จัดการ Mapping Config และ Config Version

- [ ] **6.1 Migration `0002_mapping_configs.sql`**
  - ทำ: `mapping_configs(id, name unique, input_folder unique, source_table_id fk, normalized_table_id fk, active_version_id null, created_at)` และ `mapping_config_versions(id, config_id fk, version_no, file_to_source jsonb, source_to_normalized jsonb, created_at, activated_at null, unique(config_id, version_no))` แล้วเพิ่ม fk ของ `active_version_id` หลังสร้างทั้งสองตาราง
  - เข้าใจ: version เก็บ rule เป็น `jsonb` ก้อนเดียวที่ไม่ถูกแก้อีก (immutable snapshot) ทำให้งานที่ตรึง version ไว้ได้กติกาเดิมแน่นอน; fk ที่อ้างกันไปมาต้องเพิ่มทีหลัง
  - ตรวจ: `\d mapping_config_versions`

- [ ] **6.2 C# model ของ rule**
  - ทำ: `FileToSourceRule(csvHeader, sourceColumn)` และ `SourceToNormalizedRule(sourceColumn, normalizedColumn, format?)`
  - เข้าใจ: `format` เป็น optional เพื่อ override ค่าเริ่มต้น (เช่น date `yyyy-MM-dd`); System.Text.Json แปลง record ↔ JSON ด้วย camelCase
  - ตรวจ: unit test serialize แล้ว deserialize กลับได้ค่าเดิม

- [ ] **6.3 ตั้ง `InputRoot` ใน configuration**
  - ทำ: เพิ่ม `Paths:InputRoot` (เช่น `../../input` หรือ path เต็ม) และ bind เข้า `PathOptions` ด้วย Options pattern
  - เข้าใจ: `IOptions<T>` ทำให้ config เป็น object ที่มีชนิด ตรวจค่าได้ตอนเริ่ม แทนการอ่าน string กระจายทั่วโค้ด
  - ตรวจ: log ค่า path เต็มตอนเริ่ม

- [ ] **6.4 `POST /mapping-configs`**
  - ทำ: รับ `name`, `inputFolder` (ชื่อโฟลเดอร์ย่อยใต้ `InputRoot`), `sourceTableId`, `normalizedTableId` ตรวจว่าตารางมีจริงและ kind ถูก สร้างโฟลเดอร์ด้วย `Directory.CreateDirectory` แล้วคืน 201
  - เข้าใจ: เก็บชื่อโฟลเดอร์แบบ relative ไม่ใช่ path เต็ม เพื่อไม่ผูกกับเครื่อง; หนึ่ง config ผูกหนึ่งโฟลเดอร์ (unique)
  - ตรวจ: มีโฟลเดอร์ `input/orders` เกิดขึ้น

- [ ] **6.5 Test ของ `ConfigVersionValidator` ก่อน**
  - ทำ: test กรณี: sourceColumn ไม่มีในตาราง, normalizedColumn ไม่มี, csvHeader ซ้ำ, map ไป normalized คอลัมน์เดียวกันสองครั้ง, required column ไม่ถูก map, format ของ date ใช้ไม่ได้
  - เข้าใจ: validator รับ rule + table definition ปัจจุบัน แล้วคืนรายการ error เป็น pure function
  - ตรวจ: test แดง

- [ ] **6.6 Implement `ConfigVersionValidator`**
  - ทำ: เขียนให้ test ใน 6.5 ผ่าน
  - ตรวจ: test เขียว

- [ ] **6.7 `POST /mapping-configs/{id}/versions`**
  - ทำ: validate แล้ว insert version ใหม่ `version_no = max + 1` ส่ง JSON ด้วย `@rules::jsonb`
  - เข้าใจ: ถ้าสอง request คำนวณ `max + 1` พร้อมกัน `unique(config_id, version_no)` จะกันไว้ ให้คืน 409 แล้ว client ลองใหม่
  - ตรวจ: `select file_to_source from mapping_config_versions` เห็น JSON

- [ ] **6.8 `POST /mapping-configs/{id}/versions/{version}/activate`**
  - ทำ: โหลด table definition ปัจจุบัน รัน validator ซ้ำ แล้วตั้ง `active_version_id` และ `activated_at`
  - เข้าใจ: schema อาจเปลี่ยนระหว่างสร้าง version กับ activate จึงตรวจอีกครั้งตอน activate; active version ใช้กับ **ไฟล์ใหม่** เท่านั้น
  - ตรวจ: activate version ที่อ้างคอลัมน์ไม่มีได้ 400

- [ ] **6.9 `GET /mapping-configs` และ `GET /mapping-configs/{id}`**
  - ทำ: คืน config พร้อมรายการ version และบอกว่า version ไหน active
  - ตรวจ: เห็นข้อมูลครบ

- [ ] **6.10 สร้าง config ตัวอย่าง**
  - ทำ: ใน `requests/configs.http` สร้าง config `orders` ผูก `src_orders`/`norm_orders`, สร้าง version 1 ที่ map ครบทุกคอลัมน์ แล้ว activate
  - ตรวจ: `GET /mapping-configs/{id}` แสดง version 1 active

- [ ] **6.11 Commit Phase 6**
  - ทำ: commit `feat(api): manage mapping configs and versions`

---

## Phase 7 — ตารางงานและ Outbox

- [ ] **7.1 เข้าใจปัญหา dual write ก่อนเขียนโค้ด**
  - ทำ: อ่านและตอบคำถาม: ถ้า Watcher insert `file_jobs` สำเร็จแล้วส่ง Kafka ไม่สำเร็จ หรือส่ง Kafka สำเร็จแล้ว DB rollback จะเกิดอะไร
  - เข้าใจ: การเขียนสองระบบ (DB + Kafka) ทำให้เป็น atomic ไม่ได้ Transactional Outbox แก้โดยเขียนงานที่ต้องส่งลงตาราง `outbox` ใน transaction เดียวกับข้อมูล แล้วให้ process อื่นอ่าน outbox ไปส่ง ผลคือได้ at-least-once (ส่งแน่แต่อาจซ้ำ) ปลายทางจึงต้องกันผลซ้ำเอง
  - ตรวจ: อธิบายได้ว่าทำไม consumer ต้อง idempotent

- [ ] **7.2 Migration `0003_file_jobs_outbox.sql`**
  - ทำ:
    - `file_jobs(id, config_id, config_version_id, original_path, file_name, intake_key, import_status, archive_status, content_hash, snapshot_path, total_rows, archive_path, last_error, created_at, updated_at)` พร้อม `unique(config_id, intake_key)`
    - partial unique index `(config_id, content_hash) where content_hash is not null and import_status <> 'Duplicate'`
    - `outbox(id bigint identity, topic, message_key, payload jsonb, created_at, sent_at null)` พร้อม index บน `id where sent_at is null`
  - เข้าใจ: partial index บังคับ unique เฉพาะแถวที่ตรงเงื่อนไข ไฟล์ Duplicate หลายไฟล์จึงมี hash เดียวกันได้ แต่ไฟล์ที่นำเข้าจริงมี hash ซ้ำไม่ได้; index `where sent_at is null` ทำให้ dispatcher หางานค้างได้เร็วแม้ outbox โต
  - ตรวจ: `\d file_jobs` เห็น index ทั้งสอง

- [ ] **7.3 ค่าคงที่ของสถานะและ topic**
  - ทำ: class `ImportStatus`, `ArchiveStatus`, `RowJobStatus` และ `Topics.FileImport = "mapping.file-import"`, `Topics.RowNormalize = "mapping.row-normalize"`
  - เข้าใจ: เก็บ string ที่ใช้หลายที่ไว้ที่เดียว กันพิมพ์ผิดแล้วบั๊กเงียบ
  - ตรวจ: build ผ่าน

- [ ] **7.4 Message contract**
  - ทำ: `record FileImportRequested(long FileJobId)` และ `record RowNormalizeRequested(long RowJobId)`
  - เข้าใจ: message เป็น "ตัวชี้" ไป record ใน DB ไม่ใช่ข้อมูลทั้งหมด (thin message) consumer อ่านสถานะล่าสุดจาก DB เสมอ จึงไม่เจอข้อมูลเก่าใน message
  - ตรวจ: build ผ่าน

- [ ] **7.5 `OutboxWriter.AddAsync(connection, transaction, topic, key, message)`**
  - ทำ: serialize message เป็น JSON แล้ว insert `outbox` โดยใช้ connection และ transaction ที่ผู้เรียกส่งมา
  - เข้าใจ: ถ้า writer เปิด connection ของตัวเอง outbox จะไม่อยู่ใน transaction เดียวกับข้อมูล pattern จะพังทันที
  - ตรวจ: อ่าน signature แล้วอธิบายได้ว่าทำไมต้องรับ transaction

- [ ] **7.6 Commit Phase 7**
  - ทำ: commit `feat: add job tables and transactional outbox writer`

---

## Phase 8 — File Watcher

- [ ] **8.1 เตรียม configuration ของ Watcher**
  - ทำ: connection string, `Paths:InputRoot`, `Watcher:ConfigRefreshSeconds=10`, `Watcher:ScanIntervalSeconds=60` (A7) และเรียก `AddMappingDatabase`
  - เข้าใจ: Watcher ใช้ extension จาก Shared ชุดเดียวกับ API จึงต่อ DB แบบเดียวกัน
  - ตรวจ: Watcher เริ่มได้และ log ค่า options

- [ ] **8.2 ทดลอง `FileSystemWatcher` แบบดิบ**
  - ทำ: ใน `Worker.cs` สร้าง watcher ที่โฟลเดอร์ `input/orders` log ทุก event (`Created`, `Changed`, `Renamed`, `Deleted`)
  - เข้าใจ: OS แจ้ง event ผ่าน callback บน thread pool ไม่ใช่ thread ของ `ExecuteAsync`
  - ตรวจ: สร้าง, แก้, rename, ลบไฟล์ แล้วเห็น event ตรงกัน

- [ ] **8.3 สังเกตว่า Created ไม่ได้แปลว่าไฟล์เขียนเสร็จ**
  - ทำ: copy ไฟล์ขนาดใหญ่ (เช่น 200 MB) เข้าโฟลเดอร์แล้วดู event
  - เข้าใจ: `Created` เกิดตอนเริ่ม copy แล้วตามด้วย `Changed` หลายครั้ง นี่คือเหตุผลที่ระบบใช้ event เป็นแค่ "สัญญาณพบไฟล์" แล้วหน่วง 30 วินาทีก่อนอ่าน
  - ตรวจ: เห็น `Created` หนึ่งครั้งตามด้วย `Changed` หลายครั้ง

- [ ] **8.4 กรองเฉพาะ `Created` และ `Renamed` ของ `.csv`**
  - ทำ: ตั้ง `Filter = "*.csv"`, `NotifyFilter = FileName | LastWrite | Size`, subscribe `Created` และ `Renamed` (ตรวจชื่อใหม่ลงท้าย `.csv`) และ `Error`
  - เข้าใจ: `Renamed` จำเป็นเพราะบางโปรแกรมเขียนชื่ออื่นก่อนแล้ว rename; `Error` เกิดเมื่อ buffer event ล้น (`InternalBufferSize`) แปลว่า event หายไปแล้ว
  - ตรวจ: สร้าง `.txt` ไม่เห็น log, rename `.txt` เป็น `.csv` เห็น log

- [ ] **8.5 ส่ง event เข้า `Channel<T>`**
  - ทำ: handler เรียกแค่ `channel.Writer.TryWrite(new FileDetected(configId, path))`; `ExecuteAsync` อ่านด้วย `await foreach (... ReadAllAsync(stoppingToken))`
  - เข้าใจ: event handler ต้องจบเร็วและไม่ await DB; Channel คือคิวใน memory แยกผู้ผลิต (event) กับผู้บริโภค (intake) และเปลี่ยน callback ให้เป็นลำดับงานที่ควบคุมได้
  - ตรวจ: log ฝั่งอ่าน channel แสดงไฟล์ที่ drop

- [ ] **8.6 `ActiveConfigProvider` อ่าน config จาก DB**
  - ทำ: query config ทั้งหมดพร้อม `active_version_id` แล้วคำนวณ path เต็มจาก `InputRoot`
  - เข้าใจ: Watcher ไม่ hardcode โฟลเดอร์ แต่ถาม DB ว่ามีโฟลเดอร์ไหนต้องเฝ้า
  - ตรวจ: log รายการ config ที่โหลดได้

- [ ] **8.7 `WatcherRegistry` — หนึ่ง watcher ต่อหนึ่ง config**
  - ทำ: เก็บ `Dictionary<configId, FileSystemWatcher>` สร้างตาม config ที่ active และ `Dispose` ทั้งหมดเมื่อ service หยุด
  - เข้าใจ: `FileSystemWatcher` ถือ handle ของ OS ต้อง dispose ไม่เช่นนั้น resource รั่ว
  - ตรวจ: drop ไฟล์ใน `input/orders` แล้วเห็น event พร้อม configId ที่ถูก

- [ ] **8.8 โหลด config ใหม่เป็นระยะ**
  - ทำ: ใช้ `PeriodicTimer` ทุก 10 วินาที เพิ่ม watcher ให้ config ใหม่ และลบ watcher ของ config ที่ไม่มี active version แล้ว; config ที่ไม่มี active version ให้ log warning
  - เข้าใจ: `PeriodicTimer` ไม่ยิงซ้อนถ้ารอบก่อนยังไม่จบ ต่างจาก `System.Threading.Timer`
  - ตรวจ: สร้าง config ใหม่ผ่าน API แล้วภายใน 10 วินาที Watcher เริ่มเฝ้าโฟลเดอร์ใหม่

- [ ] **8.9 คำนวณ intake key**
  - ทำ: `FileIntake` อ่าน `FileInfo` แล้วสร้าง key `path|size|lastWriteTimeUtc.Ticks`
  - เข้าใจ: key นี้กัน event ซ้ำของไฟล์เดียวกัน แต่ถ้า partner ส่งไฟล์ชื่อเดิมทับด้วยเนื้อหาใหม่ size/เวลาจะเปลี่ยน จึงได้งานใหม่ (ข้อตกลง Step 1 รอบที่ 6) การตรวจเนื้อหาซ้ำจริงเป็นหน้าที่ของ content hash ใน Worker
  - ตรวจ: log key ของไฟล์ที่ drop

- [ ] **8.10 สร้าง File Import Job + outbox ใน transaction เดียว**
  - ทำ: `INSERT INTO file_jobs (..., config_version_id = active version ตอนนี้, import_status='Queued', archive_status='NotArchived') ON CONFLICT (config_id, intake_key) DO NOTHING RETURNING id` ถ้าได้ id ให้เรียก `OutboxWriter` (topic file-import, key = fileJobId) แล้ว commit
  - เข้าใจ: การตรึง `config_version_id` ตรงนี้คือข้อตกลง ★ — ถ้า activate version ใหม่ระหว่างหน่วง งานนี้ยังใช้ version เดิม; `ON CONFLICT DO NOTHING` ทำให้ event ซ้ำไม่สร้างงานซ้ำ
  - ตรวจ: drop ไฟล์ แล้วเห็น 1 แถวใน `file_jobs` และ 1 แถวใน `outbox` ที่ `sent_at is null`

- [ ] **8.11 Scan ตอนเริ่ม**
  - ทำ: ตอน Watcher เริ่ม ให้ enumerate `*.csv` ในทุกโฟลเดอร์ที่เฝ้าแล้วส่งเข้า channel เดียวกัน
  - เข้าใจ: ไฟล์ที่มาถึงตอน Watcher ปิดอยู่ไม่มี event; ผ่าน intake ชุดเดียวกันจึงกันซ้ำด้วยกลไกเดิม
  - ตรวจ: ปิด Watcher, drop ไฟล์, เปิดใหม่ แล้วเกิด job

- [ ] **8.12 ข้าม path ที่ยังมีงานค้าง (A5)**
  - ทำ: ก่อน insert ตรวจว่ามี job ของ `config_id + original_path` ที่สถานะ `Queued`/`Delaying`/`Importing` หรือไม่ ถ้ามีให้ข้าม
  - เข้าใจ: ระหว่างหน่วง 30 วินาที ไฟล์อาจยังโตอยู่ scan จะเห็น size ใหม่และได้ key ใหม่ ถ้าไม่ข้ามจะเกิดสอง job จากไฟล์เดียว — ยืนยันข้อสมมตินี้ก่อนทำ
  - ตรวจ: drop ไฟล์แล้ว append ข้อมูลเพิ่มก่อน scan รอบถัดไป ยังมี job เดียว

- [ ] **8.13 Scan ชดเชยเป็นระยะและเมื่อ `Error`**
  - ทำ: scan ทุก 60 วินาที และสั่ง scan ทันทีเมื่อได้ `Error` event
  - ตรวจ: log แสดงรอบ scan และไม่มี job ซ้ำ

- [ ] **8.14 Structured logging ด้วย scope**
  - ทำ: ใช้ `logger.BeginScope` ใส่ `ConfigId`, `FileJobId`, `Path` และ log message แบบ template (`"Created job {FileJobId}"`)
  - เข้าใจ: template เก็บค่าเป็น field แยก ค้นและกรอง log ได้ ต่างจาก string interpolation
  - ตรวจ: log อ่านแล้วรู้ว่าไฟล์ไหนได้ job อะไร

- [ ] **8.15 Commit Phase 8**
  - ทำ: commit `feat(watcher): detect csv files and create file import jobs`

---

## Phase 9 — Worker: Outbox Dispatcher

- [ ] **9.1 เพิ่ม Confluent.Kafka เฉพาะ Worker**
  - ทำ: `dotnet add src/MappingDemo.Worker package Confluent.Kafka` และตั้ง connection string + `Kafka:BootstrapServers=localhost:9092`
  - เข้าใจ: Confluent.Kafka ห่อ librdkafka (native library) ไว้; ใส่ใน Worker เท่านั้นเพราะ API และ Watcher ไม่คุยกับ Kafka
  - ตรวจ: build ผ่าน

- [ ] **9.2 ลงทะเบียน producer**
  - ทำ: `IProducer<string, string>` singleton ด้วย `Acks = Acks.All`, `EnableIdempotence = true`
  - เข้าใจ: `Acks.All` รอให้ broker ยืนยันว่าเขียนแล้ว; idempotent producer กัน message ซ้ำจากการ retry ภายใน client (แต่ไม่กันซ้ำจากการที่เราส่งใหม่เอง)
  - ตรวจ: Worker เริ่มได้

- [ ] **9.3 ทดลองส่ง message หนึ่งตัว**
  - ทำ: ตอนเริ่ม ส่ง message ทดสอบเข้า `mapping.file-import` แล้วดูใน Kafka UI จากนั้นลบโค้ดทดสอบ
  - เข้าใจ: `ProduceAsync` คืน `DeliveryResult` ที่บอก partition และ offset
  - ตรวจ: Kafka UI เห็น message

- [ ] **9.4 โครง `OutboxDispatcher`**
  - ทำ: `BackgroundService` ใช้ `PeriodicTimer` 500ms
  - ตรวจ: log ทุกรอบ (ปิด log นี้หลังตรวจเสร็จ)

- [ ] **9.5 อ่านงานค้าง**
  - ทำ: `SELECT id, topic, message_key, payload FROM outbox WHERE sent_at IS NULL ORDER BY id LIMIT 100`
  - เข้าใจ: batch จำกัด 100 กันการโหลดมากเกินในรอบเดียว
  - ตรวจ: log จำนวนงานที่พบ

- [ ] **9.6 ส่งแล้ว mark `sent_at`**
  - ทำ: ทีละรายการ `await ProduceAsync` แล้ว `UPDATE outbox SET sent_at = now() WHERE id = @id`; ถ้าส่งไม่ได้ให้หยุด batch แล้วลองรอบหน้า
  - เข้าใจ: ถ้า process ตายหลัง broker ack แต่ก่อน update รอบหน้าจะส่งซ้ำ — นี่คือ at-least-once; ไม่ลบแถวที่ส่งแล้วเพื่อดูย้อนหลังตอน demo
  - ตรวจ: job จาก Phase 8 ถูกส่ง, `sent_at` มีค่า, Kafka UI เห็น key = fileJobId

- [ ] **9.7 ทดสอบความคงทน**
  - ทำ: ปิด Worker, drop 2 ไฟล์, เปิด Worker
  - เข้าใจ: งานไม่หายเพราะรออยู่ใน outbox; Watcher ไม่ต้องรู้ว่า Worker หรือ Kafka ทำงานอยู่ไหม
  - ตรวจ: outbox ค้าง 2 แถวตอนปิด และถูกส่งหมดหลังเปิด

- [ ] **9.8 Commit Phase 9**
  - ทำ: commit `feat(worker): dispatch outbox messages to kafka`

---

## Phase 10 — Worker: รับ File Import Job และหน่วง 30 วินาที

- [ ] **10.1 Consumer ตัวแรก**
  - ทำ: `ConsumerConfig` group `mapping-file-import`, `EnableAutoCommit = false`, `AutoOffsetReset = Earliest` subscribe `mapping.file-import` แล้ว log partition/offset/key
  - เข้าใจ: ปิด auto commit เพื่อ commit offset เองหลังงานถูกบันทึกถาวรแล้วเท่านั้น; `Earliest` ทำให้ group ใหม่อ่านตั้งแต่ต้น topic
  - ตรวจ: เห็น message ที่ dispatcher ส่งไว้

- [ ] **10.2 รัน consumer บน thread ของตัวเอง**
  - ทำ: ห่อ loop ด้วย `Task.Factory.StartNew(..., TaskCreationOptions.LongRunning)` และ `consumer.Close()` ตอนหยุด
  - เข้าใจ: `Consume()` block thread จนมี message ถ้ารันบน thread pool ตรงๆ จะกิน thread ของงานอื่น; `Close()` แจ้ง group ให้ rebalance ทันทีแทนที่จะรอ timeout
  - ตรวจ: Ctrl+C แล้ว Worker หยุดเรียบร้อย

- [ ] **10.3 Commit offset ด้วยมือ**
  - ทำ: เรียก `consumer.Commit(result)` หลัง log
  - เข้าใจ: offset ที่ commit คือจุดที่ group จะเริ่มอ่านต่อหลัง restart ถ้าไม่ commit message จะถูกส่งมาใหม่
  - ตรวจ: restart Worker แล้วไม่เห็น message เก่าซ้ำ

- [ ] **10.4 เพิ่มเป็น 3 consumer ใน group เดียว (A6)**
  - ทำ: สร้าง consumer ตาม `Kafka:FileImportConsumers=3` แต่ละตัวมี thread ของตัวเอง และ log ใน `SetPartitionsAssignedHandler`
  - เข้าใจ: consumer ของ Confluent.Kafka ไม่ thread-safe หนึ่ง instance ต่อหนึ่ง thread; group แบ่ง 3 partitions ให้ 3 ตัว (rebalance)
  - ตรวจ: log แสดงแต่ละ consumer ได้ partition คนละตัว

- [ ] **10.5 `FileImportHandler` — โหลดงานและกันซ้ำ**
  - ทำ: deserialize message โหลด `file_jobs` ถ้าสถานะเป็น `Imported` หรือ `Duplicate` แล้วให้ข้ามและ commit offset
  - เข้าใจ: เพราะ outbox ส่งซ้ำได้ handler ทุกตัวต้องถามว่า "งานนี้ทำไปแล้วหรือยัง" ก่อนเสมอ
  - ตรวจ: ส่ง message เดิมซ้ำด้วย console producer แล้วเห็น log ว่าข้าม

- [ ] **10.6 เปลี่ยนเป็น `Delaying` แล้วหน่วง 30 วินาที**
  - ทำ: update `import_status='Delaying'` (commit ทันที) แล้ว `await Task.Delay(TimeSpan.FromSeconds(Import:DelaySeconds), stoppingToken)`
  - เข้าใจ: เริ่มนับ 30 วินาทีเมื่อ Worker รับงาน ไม่ใช่ตอน Watcher พบไฟล์; ถ้า shutdown ระหว่างรอ delay ถูกยกเลิก offset ไม่ถูก commit งานจึงกลับมาใหม่ตอน restart; ระหว่างนี้ consumer ตัวนี้ไม่อ่าน partition ของตัวเองต่อ — นี่คือเหตุผลที่ต้องมีหลาย partition
  - ตรวจ: `file_jobs` เป็น `Delaying` นาน 30 วินาที

- [ ] **10.7 พิสูจน์ว่าไฟล์ A และ B หน่วงพร้อมกัน**
  - ทำ: drop 2 ไฟล์พร้อมกัน ดู log partition ของแต่ละไฟล์; ถ้าลงคนละ partition ต้อง `Delaying` พร้อมกัน ถ้าลง partition เดียวกันจะต่อกัน
  - เข้าใจ: key = fileJobId ถูก hash ไป partition จึงไม่ได้คาบเกี่ยวทุกครั้ง นี่คือเกณฑ์ demo "Log partition ของไฟล์ A และ B"
  - ตรวจ: ได้อย่างน้อยหนึ่งรอบที่ A และ B อยู่คนละ partition และเริ่ม `Delaying` ห่างกันไม่ถึงวินาที

- [ ] **10.8 Copy snapshot**
  - ทำ: เปลี่ยนเป็น `Importing` แล้ว copy ไฟล์ไป `data/staging/{fileJobId}.csv` บันทึก `snapshot_path`; ถ้า snapshot มีอยู่แล้วจากรอบก่อนให้ใช้ของเดิม
  - เข้าใจ: อ่านจาก snapshot แทนไฟล์จริงทำให้ข้อมูลไม่เปลี่ยนระหว่างอ่าน และ retry ได้ rownumber ชุดเดิม; ถ้า partner ยังล็อกไฟล์อยู่ `File.Copy` จะโยน `IOException`
  - ตรวจ: มีไฟล์ใน `data/staging/`

- [ ] **10.9 Content hash**
  - ทำ: `SHA256.HashDataAsync(stream)` บน snapshot แล้วบันทึก `content_hash` เป็น hex
  - เข้าใจ: hash เนื้อหาเดียวกันได้ค่าเดียวกันเสมอไม่ว่าชื่อไฟล์อะไร
  - ตรวจ: ไฟล์เนื้อหาเดียวกันสองชื่อได้ hash ตรงกัน

- [ ] **10.10 ตรวจ Duplicate File**
  - ทำ: ถ้ามี job อื่นใน config เดียวกันที่ hash ตรงและไม่ใช่ `Duplicate` ให้ตั้ง `Duplicate`; จับ unique violation จาก partial index ของ 7.2 ในกรณีสองไฟล์ชนกันพร้อมกัน
  - เข้าใจ: การตรวจด้วย query มี race ส่วน index เป็นตัวตัดสินสุดท้าย; ไฟล์ Duplicate จะถูก archive ใน Phase 12
  - ตรวจ: drop ไฟล์เดิมด้วยชื่อใหม่ ได้ `Duplicate` และไม่มีแถวเพิ่มใน `src_orders`

- [ ] **10.11 ตรวจว่า version ถูกตรึงจริง**
  - ทำ: drop ไฟล์ แล้ว activate version 2 ระหว่างหน่วง
  - ตรวจ: `file_jobs.config_version_id` ยังเป็น version 1

- [ ] **10.12 Commit Phase 10**
  - ทำ: commit `feat(worker): consume file import jobs with delay and snapshot`

---

## Phase 11 — Worker: อ่าน CSV และนำเข้า Source Table

- [ ] **11.1 เพิ่ม CsvHelper**
  - ทำ: เพิ่ม package ใน Shared แล้วลองอ่าน CSV ตัวอย่างพิมพ์ header และค่า
  - เข้าใจ: CSV มีกติกา quote (RFC 4180) ค่า `"a,b"` คือค่าเดียว และค่าข้ามบรรทัดได้ `string.Split(',')` จึงผิด
  - ตรวจ: อ่านไฟล์ที่มี comma ใน quote ได้ถูก

- [ ] **11.2 Test ของ `CsvRecordReader` ก่อน**
  - ทำ: test ว่า record แรกหลัง header ได้ rownumber 1, ค่าที่มีขึ้นบรรทัดใหม่ใน quote ยังนับเป็น record เดียว, บรรทัดว่างท้ายไฟล์ไม่นับ
  - เข้าใจ: Row Number คือลำดับ record ไม่ใช่เลขบรรทัดจริง (ตาม CONTEXT.md)
  - ตรวจ: test แดง

- [ ] **11.3 Implement `CsvRecordReader`**
  - ทำ: `IAsyncEnumerable<CsvRecord>` ที่ให้ `(RowNumber, IReadOnlyDictionary<string,string> Values)`
  - เข้าใจ: `IAsyncEnumerable` อ่านทีละ record ไม่โหลดทั้งไฟล์เข้า memory
  - ตรวจ: test เขียว

- [ ] **11.4 โหลด rule ของ version ที่ตรึงไว้**
  - ทำ: อ่าน `file_to_source` ของ `file_jobs.config_version_id` และชื่อ Source table
  - ตรวจ: log rule ที่ใช้

- [ ] **11.5 ตรวจ header**
  - ทำ: ถ้า CSV ไม่มี header ที่ rule ต้องใช้ ให้ตั้ง `ImportFailed` พร้อม `last_error` ที่บอกชื่อ header ที่ขาด แล้ว commit offset
  - เข้าใจ: นี่คือ error ระดับไฟล์ ไม่ใช่ Row Error
  - ตรวจ: drop ไฟล์ที่ขาดคอลัมน์ได้ `ImportFailed` พร้อมเหตุผล

- [ ] **11.6 Migration `0004_row_jobs.sql`**
  - ทำ: `row_jobs(id, file_job_id, source_row_id, row_number, config_version_id, kind check in ('Initial','Reprocess'), status, attempts, last_error, created_at, finished_at)` พร้อม partial unique index `(source_row_id) where status = 'Pending'` และ `row_errors(id, row_job_id, file_job_id, row_number, field, reason, created_at)`
  - เข้าใจ: index นี้ทำให้ Source แถวหนึ่งมีงานที่กำลังรอได้แค่งานเดียว ใช้กันการ reprocess แถวเดียวกันซ้อนใน Phase 17; `row_jobs` ทุกแถวคือประวัติการรันของ Source แถวนั้น
  - ตรวจ: `\d row_jobs`

- [ ] **11.7 `SourceRowWriter` — INSERT แบบ dynamic**
  - ทำ: สร้าง SQL จาก rule: `INSERT INTO "src_orders" (file_job_id, row_number, "order_no", ...) VALUES (@fileJobId, @rowNumber, @p0, ...) ON CONFLICT (file_job_id, row_number) DO NOTHING RETURNING id` ใช้ `DynamicParameters`
  - เข้าใจ: ชื่อตาราง/คอลัมน์มาจาก metadata ที่ผ่าน validation และถูก quote ส่วนค่าข้อมูลส่งเป็น parameter เสมอ; ถ้าไม่ได้ id กลับมาแปลว่าแถวนี้เคยนำเข้าแล้ว
  - ตรวจ: unit test ของ SQL ที่สร้าง

- [ ] **11.8 หนึ่ง transaction ต่อหนึ่งแถว**
  - ทำ: ในแต่ละ record เปิด transaction → insert Source row → ถ้าได้ id ใหม่ insert `row_jobs` (`Initial`, `Pending`) + outbox (topic row-normalize, key = sourceRowId) → commit
  - เข้าใจ: commit ทีละแถวทำให้ dispatcher ส่งงานแถวได้ทันทีโดยไม่รอทั้งไฟล์ แลกกับความเร็วนำเข้าที่ช้ากว่า batch; key = sourceRowId ทำให้แถวต่างๆ กระจายหลาย partition
  - ตรวจ: `src_orders` จำนวนแถวเท่ากับ record และ outbox มีงานแถวครบ

- [ ] **11.9 Demo throttle**
  - ทำ: เพิ่ม `Demo:ImportRowDelayMs` (ค่าเริ่ม 0) หน่วงหลังแต่ละแถว
  - เข้าใจ: ไฟล์ 100 แถวนำเข้าเร็วมากจนมองไม่เห็นการทำขนาน ค่านี้ใช้เพื่อสาธิตเท่านั้น
  - ตรวจ: ตั้ง 200ms แล้วเห็นแถวค่อยๆ เพิ่มใน `src_orders`

- [ ] **11.10 ปิดงานนำเข้า**
  - ทำ: เมื่ออ่านครบให้ตั้ง `Imported` และ `total_rows` แล้ว commit offset
  - เข้าใจ: `total_rows` บันทึกครั้งเดียวตอนนี้ เพราะเป็นจุดแรกที่รู้จำนวนแถวทั้งหมดแน่นอน
  - ตรวจ: `total_rows` ตรงกับจำนวน record

- [ ] **11.11 นำเข้าล้มกลางทาง + fault injection**
  - ทำ: เพิ่ม `Demo:FailImportAtRow` (ค่าเริ่ม null) ให้โยน exception ที่แถวนั้น; จับ exception ใน handler ตั้ง `ImportFailed` + `last_error` แล้ว commit offset
  - เข้าใจ: ถ้าไม่ commit offset message เดิมจะวนกลับมาล้มซ้ำไม่จบและบล็อก partition (poison message) จึงบันทึกเป็นสถานะแล้วให้คนสั่ง retry; แถวที่ commit แล้วยังอยู่และ normalize ต่อได้
  - ตรวจ: ตั้งให้ล้มที่แถว 50 ได้ `ImportFailed` และ `src_orders` มี 49 แถวของไฟล์นี้

- [ ] **11.12 ทดสอบ process ตายกลางการนำเข้า**
  - ทำ: ตั้ง throttle แล้ว Ctrl+C Worker ระหว่างนำเข้า จากนั้นเปิดใหม่
  - เข้าใจ: offset ยังไม่ commit message จึงกลับมา; handler อ่านจาก snapshot เดิม และ `ON CONFLICT` ข้ามแถวที่มีแล้ว
  - ตรวจ: จำนวนแถวสุดท้ายเท่ากับ record ในไฟล์พอดี ไม่มีซ้ำ

- [ ] **11.13 Commit Phase 11**
  - ทำ: commit `feat(worker): import csv rows into source table`

---

## Phase 12 — Archive

- [ ] **12.1 ตั้ง `ArchiveRoot` และตรวจตอนเริ่ม**
  - ทำ: `Paths:ArchiveRoot` ถ้าอยู่ใต้ `InputRoot` ให้ Worker หยุดพร้อม error
  - เข้าใจ: ถ้า archive อยู่ใต้โฟลเดอร์ที่เฝ้า การย้ายไฟล์จะกลายเป็นไฟล์ "ใหม่" ให้ Watcher เจอซ้ำ
  - ตรวจ: ตั้งค่าผิดแล้ว Worker ไม่เริ่ม

- [ ] **12.2 Hash ไฟล์จริงแล้วเทียบกับ snapshot**
  - ทำ: หลัง `Imported` หรือ `Duplicate` hash ไฟล์ที่ `original_path` แล้วเทียบกับ `content_hash`
  - เข้าใจ: ถ้าไม่ตรงแปลว่าไฟล์เปลี่ยนหลัง copy snapshot คือกรณีที่ 30 วินาทีไม่พอ การตรวจนี้ทำให้ "เห็น" ปัญหา แต่ไม่ได้ป้องกัน เพราะแถวจาก snapshot เข้า Source ไปแล้ว
  - ตรวจ: log ผลการเทียบ

- [ ] **12.3 ย้ายไป Archive เมื่อ hash ตรง**
  - ทำ: ย้ายไป `archive/{configId}/{yyyyMMdd}/{fileJobId}_{ชื่อเดิม}` สร้างโฟลเดอร์ก่อน ลบ snapshot แล้วตั้ง `Archived` และ `archive_path`
  - เข้าใจ: ใส่ fileJobId ข้างหน้าชื่อเพื่อไม่ให้ไฟล์ชื่อเดียวกันจากคนละวันทับกัน
  - ตรวจ: ไฟล์หายจาก `input/orders` ไปอยู่ใน archive และ snapshot ถูกลบ

- [ ] **12.4 ทำให้การย้ายทำซ้ำได้**
  - ทำ: ถ้าไฟล์ต้นทางไม่มีแต่ปลายทางมีอยู่แล้ว ให้ตั้งสถานะอย่างเดียว
  - เข้าใจ: ถ้า process ตายหลังย้ายแต่ก่อน update สถานะ รอบหน้าต้องไม่ล้ม
  - ตรวจ: ย้ายไฟล์เองด้วยมือก่อน แล้วรัน logic ได้ `Archived`

- [ ] **12.5 ไฟล์เปลี่ยนหลังอ่าน**
  - ทำ: hash ไม่ตรงให้ตั้ง `ChangedAfterRead` ไม่ย้ายไฟล์และเก็บ snapshot ไว้
  - ตรวจ: ตั้ง throttle แล้วแก้ไฟล์ใน `input/orders` ระหว่างนำเข้า ได้ `ChangedAfterRead` และไฟล์ยังอยู่

- [ ] **12.6 ย้ายไม่ได้**
  - ทำ: retry 3 ครั้งพร้อมหน่วง แล้วตั้ง `ArchiveFailed`
  - เข้าใจ: บน Windows ไฟล์ที่โปรแกรมอื่นเปิดอยู่ย้ายไม่ได้; สถานะ archive แยกจาก normalization จึงไม่กระทบงานแถว
  - ตรวจ: เปิดไฟล์ค้างใน Excel ระหว่างนำเข้า ได้ `ArchiveFailed` แต่ normalize ยังเดินต่อ

- [ ] **12.7 Commit Phase 12**
  - ทำ: commit `feat(worker): archive imported files`

---

## Phase 13 — กฎแปลงข้อมูล (pure logic + unit test)

phase นี้ไม่แตะ DB และ Kafka เลย เขียน test ก่อนทุก step

- [ ] **13.1 ชนิดผลลัพธ์ `ConversionResult`**
  - ทำ: record ที่เป็นได้ทั้ง "สำเร็จพร้อมค่า" หรือ "ผิดพร้อมเหตุผล"
  - เข้าใจ: ข้อมูลผิดเป็นเรื่องที่คาดไว้ได้ จึงคืนเป็นค่า ไม่ใช้ exception ซึ่งช้าและทำให้ flow อ่านยาก
  - ตรวจ: build ผ่าน

- [ ] **13.2 Trim และ required**
  - ทำ: test แล้ว implement: ค่าถูก trim เสมอ; ว่างหลัง trim + required → error `Required`; ว่าง + ไม่ required → `null`
  - ตรวจ: test เขียว

- [ ] **13.3 Text**
  - ทำ: test แล้ว implement การคืนค่าที่ trim แล้ว
  - ตรวจ: test เขียว

- [ ] **13.4 Date**
  - ทำ: `DateOnly.TryParseExact(value, format ?? "yyyy-MM-dd", CultureInfo.InvariantCulture, ...)`; test `2026-01-31` ผ่าน, `2026-02-30`, `31/01/2026` ไม่ผ่าน, format override `dd/MM/yyyy` ผ่าน
  - เข้าใจ: ต้องระบุ culture เสมอ ไม่เช่นนั้นผลขึ้นกับการตั้งค่าเครื่อง (เครื่องไทยอาจตีความเป็นปีพุทธศักราช)
  - ตรวจ: test เขียว

- [ ] **13.5 Decimal**
  - ทำ: `decimal.TryParse` ด้วย `NumberStyles.AllowLeadingSign | AllowDecimalPoint` และ `InvariantCulture`; test `1234.50`, `-3` ผ่าน, `1,234.50`, `12a` ไม่ผ่าน
  - เข้าใจ: ไม่เปิด `AllowThousands` ตามข้อตกลง; ใช้ `decimal` ไม่ใช้ `double` เพราะเงินต้องแม่นยำ
  - ตรวจ: test เขียว

- [ ] **13.6 Boolean**
  - ทำ: รับ `true/false/1/0` ไม่สนตัวพิมพ์; test `TRUE`, `0` ผ่าน, `yes` ไม่ผ่าน
  - ตรวจ: test เขียว

- [ ] **13.7 `RowNormalizer` เก็บ error ทุก field**
  - ทำ: รับ Source row + rules + normalized column definitions คืนค่าที่แปลงแล้ว หรือรายการ `(field, reason)` ทั้งหมด
  - เข้าใจ: ไม่หยุดที่ error แรก เพราะข้อตกลงให้เก็บ Row Error ทุก field ของแถว
  - ตรวจ: test แถวที่ผิด 2 field ได้ 2 error

- [ ] **13.8 Commit Phase 13**
  - ทำ: commit `feat: add normalization conversion rules`

---

## Phase 14 — Worker: Normalize Consumer

- [ ] **14.1 Consumer group ที่สอง**
  - ทำ: group `mapping-row-normalize` subscribe `mapping.row-normalize` จำนวน `Kafka:NormalizeConsumers=3` ใช้โครงเดียวกับ Phase 10
  - เข้าใจ: คนละ group กับ import จึงทำงานอิสระ ไม่ติดการหน่วง 30 วินาทีของไฟล์
  - ตรวจ: log แสดง partition assignment ของ group ที่สอง

- [ ] **14.2 ล็อกงานแถวและกันซ้ำ**
  - ทำ: เปิด transaction แล้ว `SELECT ... FROM row_jobs WHERE id = @id FOR UPDATE` ถ้าเป็น `Done` หรือ `Invalid` ให้ commit และข้าม
  - เข้าใจ: `FOR UPDATE` ล็อกแถวไว้จนจบ transaction ถ้ามี message ซ้ำเข้ามาพร้อมกัน ตัวที่สองจะรอ แล้วเห็นว่าทำเสร็จแล้ว
  - ตรวจ: log แสดงการข้ามเมื่อส่ง message ซ้ำ

- [ ] **14.3 โหลด rule และ Source row**
  - ทำ: อ่าน `source_to_normalized` ของ `row_jobs.config_version_id` และ `SELECT * FROM "src_orders" WHERE id = @sourceRowId`
  - เข้าใจ: ใช้ version ที่ผูกกับงานแถว ไม่ใช่ active version ปัจจุบัน
  - ตรวจ: log ค่าที่อ่านได้

- [ ] **14.4 เรียก `RowNormalizer`**
  - ทำ: ส่งค่าเข้า logic จาก Phase 13
  - ตรวจ: log ผลสำเร็จ/ผิด

- [ ] **14.5 สำเร็จ — upsert Normalized**
  - ทำ: `INSERT ... ON CONFLICT (source_row_id) DO UPDATE SET <คอลัมน์ข้อมูล>, row_job_id, config_version_id, normalized_at` แล้วตั้ง row job `Done`
  - เข้าใจ: upsert ทำให้ message ซ้ำหรือ reprocess เขียนทับผลเดิมของแถวเดียวกัน ไม่เพิ่มแถว
  - ตรวจ: `norm_orders` มีค่าชนิดถูกต้อง เช่น `amount` เป็น numeric

- [ ] **14.6 ผิด — Row Error**
  - ทำ: insert `row_errors` ทุก field พร้อม `row_number` แล้วตั้ง row job `Invalid`
  - เข้าใจ: ข้อมูลผิดไม่ retry อัตโนมัติ เพราะรันซ้ำก็ผิดเหมือนเดิม แก้ได้ด้วย reprocess
  - ตรวจ: แถวผิดมีใน `row_errors` และไม่มีใน `norm_orders`

- [ ] **14.7 Commit DB ก่อน commit offset**
  - ทำ: commit transaction แล้วค่อย `consumer.Commit(result)`
  - เข้าใจ: ถ้าสลับลำดับ แล้ว process ตายระหว่างกลาง จะเสียงานแถวนั้นไปถาวร ลำดับนี้ทำให้แย่สุดคือทำซ้ำ ซึ่งกันไว้แล้วใน 14.2
  - ตรวจ: อธิบายได้ว่าแต่ละลำดับเสียหายแบบไหน

- [ ] **14.8 ระบบล้ม — retry แล้ว `Failed`**
  - ทำ: เพิ่ม `Demo:FailNormalizeRowNumbers` สำหรับจำลอง; exception ที่ไม่ใช่ข้อมูลผิดให้ retry 3 ครั้งแบบ backoff (1s, 2s, 4s) แล้วตั้ง `Failed` + `last_error` ใน transaction ใหม่ และ commit offset
  - เข้าใจ: แยก error ชั่วคราวของระบบ (DB หลุด) ออกจากข้อมูลผิด; หลัง retry หมดต้อง commit offset ไม่เช่นนั้น partition ติด
  - ตรวจ: ตั้งให้แถว 10 ล้ม ได้ `Failed` และแถวอื่นเดินต่อ

- [ ] **14.9 ตรวจเกณฑ์ 100 แถว ผิด 2**
  - ทำ: เตรียมไฟล์ 100 records ที่ผิด 2 records แล้วรันครบ flow
  - ตรวจ: `src_orders` 100 แถว, `norm_orders` 98 แถว, `row_errors` ค้นด้วย rownumber/field/สาเหตุได้

- [ ] **14.10 ตรวจว่า normalize เริ่มก่อนนำเข้าจบ**
  - ทำ: ตั้ง throttle 200ms แล้วเทียบ `min(norm_orders.normalized_at)` กับเวลาที่ file job เป็น `Imported`
  - ตรวจ: normalize แถวแรกเกิดก่อนนำเข้าทั้งไฟล์จบ

- [ ] **14.11 ตรวจการส่งซ้ำ**
  - ทำ: ส่ง `RowNormalizeRequested` ของแถวที่ `Done` แล้วซ้ำด้วย console producer และ reset outbox บางแถวให้ `sent_at = null`
  - ตรวจ: จำนวนแถวใน `norm_orders` ไม่เปลี่ยน

- [ ] **14.12 Commit Phase 14**
  - ทำ: commit `feat(worker): normalize source rows`

---

## Phase 15 — API ดูสถานะงาน

- [ ] **15.1 Test ของการคำนวณ `normalization_status` ก่อน**
  - ทำ: pure function รับ `import_status` และยอดตามสถานะ; test: ยังไม่ `Imported` → `InProgress`; มี `Pending` → `InProgress`; ทุกแถว `Done` → `Completed`; มี `Invalid` หรือ `Failed` → `CompletedWithErrors`
  - เข้าใจ: คำนวณตอนอ่านจาก row jobs แทนการเก็บ counter บน file job เพื่อไม่ให้ row jobs ที่ทำขนานแย่ง lock แถวเดียวกัน
  - ตรวจ: test แดงแล้วเขียว

- [ ] **15.2 `GET /file-jobs` และ `GET /file-jobs/{id}`**
  - ทำ: คืน import/archive status, total_rows, ยอด `Pending/Done/Invalid/Failed` นับจากงานล่าสุดของแต่ละ Source row ด้วย `DISTINCT ON (source_row_id) ... ORDER BY source_row_id, id DESC`
  - เข้าใจ: หลัง reprocess Source แถวหนึ่งมีหลาย row job ต้องนับเฉพาะงานล่าสุด `DISTINCT ON` ของ PostgreSQL เลือกแถวแรกของแต่ละกลุ่มตาม `ORDER BY`
  - ตรวจ: ยอดตรงกับ 14.9 และสถานะเป็น `CompletedWithErrors`

- [ ] **15.3 `GET /file-jobs/{id}/errors`**
  - ทำ: คืน `rowNumber`, `field`, `reason` ของงานล่าสุดแต่ละแถว เรียงตาม rowNumber
  - ตรวจ: เห็น 2 แถวที่ผิด

- [ ] **15.4 `GET /file-jobs/{id}/rows/{rowNumber}/history`**
  - ทำ: คืนทุก row job ของแถวนั้นพร้อม version, kind, status, error
  - เข้าใจ: นี่คือประวัติที่ใช้ดูผลของ reprocess
  - ตรวจ: แถวปกติมี 1 รายการ `Initial`

- [ ] **15.5 Commit Phase 15**
  - ทำ: commit `feat(api): query file job status and errors`

---

## Phase 16 — Retry

- [ ] **16.1 `POST /file-jobs/{id}/retry`**
  - ทำ: อนุญาตเฉพาะ `ImportFailed` ใน transaction เดียวตั้ง `Queued` + เขียน outbox topic file-import; สถานะอื่นคืน 409
  - เข้าใจ: API ไม่ส่ง Kafka เอง ใช้ outbox แบบเดียวกับ Watcher คำสั่งจึงไม่หายแม้ Worker ปิดอยู่
  - ตรวจ: outbox มีแถวใหม่

- [ ] **16.2 Worker รับงาน retry**
  - ทำ: ให้ handler รับ `Queued` ที่มี snapshot อยู่แล้วโดยใช้ snapshot เดิม
  - เข้าใจ: retry ใช้ version เดิมและ snapshot เดิม rownumber จึงชี้ข้อมูลชุดเดิม
  - ตรวจ: ล้มที่แถว 50 (11.11) ปิด fault injection แล้ว retry ได้ `Imported`, Source 100 แถว ไม่มีซ้ำ

- [ ] **16.3 `POST /row-jobs/{id}/retry`**
  - ทำ: อนุญาตเฉพาะ `Failed` ตั้ง `Pending` + outbox ด้วย row job id เดิม
  - เข้าใจ: retry ไม่สร้าง row job ใหม่ และใช้ `config_version_id` เดิม
  - ตรวจ: แถวที่ `Failed` จาก 14.8 เป็น `Done` และสถานะไฟล์คำนวณใหม่เป็น `Completed` หรือ `CompletedWithErrors` ตาม `Invalid` ที่เหลือ

- [ ] **16.4 Commit Phase 16**
  - ทำ: commit `feat: retry file import and row normalization`

---

## Phase 17 — Reprocess

- [ ] **17.1 ยืนยันขอบเขต (A2)**
  - ทำ: ตัดสินว่า reprocess ทั้งไฟล์พอหรือต้องเลือกบางแถว แล้วบันทึกผลใน demo-plan
  - ตรวจ: มีข้อสรุปเป็นลายลักษณ์อักษร

- [ ] **17.2 ตรวจ version ที่เลือก**
  - ทำ: version ต้องเป็นของ config เดียวกัน และ sourceColumn ทุกตัวใน `source_to_normalized` ต้องมีใน Source table
  - เข้าใจ: reprocess อ่านจาก Source ไม่อ่าน CSV ใหม่ จึงใช้ได้เฉพาะ field ที่นำเข้ามาแล้ว
  - ตรวจ: เลือก version ของ config อื่นได้ 400

- [ ] **17.3 `POST /file-jobs/{id}/reprocess`**
  - ทำ: รับ `versionId` ใน transaction เดียวสร้าง row job `Reprocess`/`Pending` ต่อทุก Source row ของไฟล์พร้อม outbox; ถ้าชน partial unique index ให้ rollback และคืน 409
  - เข้าใจ: index `where status = 'Pending'` ทำหน้าที่เป็น lock ระดับ DB กันแถวเดียวกัน reprocess ซ้อน โดยไม่ต้องเขียน lock เอง
  - ตรวจ: row_jobs เพิ่ม 100 แถว

- [ ] **17.4 Reprocess สำเร็จ**
  - ทำ: สร้าง version 2 ที่ตั้ง format ต่างออกไปจนแถวที่เคยผิดผ่าน แล้ว reprocess
  - เข้าใจ: upsert เดิมใน 14.5 เขียนทับ Current Normalized Result พร้อม `config_version_id` ใหม่
  - ตรวจ: `norm_orders` 100 แถว และ history ของแถวที่เคยผิดมี 2 รายการ

- [ ] **17.5 Reprocess ไม่ผ่าน**
  - ทำ: สร้าง version 3 ที่ทำให้บางแถวผิด แล้ว reprocess
  - เข้าใจ: ผิดแล้วไม่แตะ `norm_orders` ผลสำเร็จเดิมพร้อม version เดิมจึงยังอยู่ ส่วน error เป็นของการรันล่าสุดแยกกัน
  - ตรวจ: แถวนั้นยังมีใน `norm_orders` ด้วย version 2 และ `/errors` แสดง error ของ version 3

- [ ] **17.6 กันรันซ้อน**
  - ทำ: ตั้ง `Demo:NormalizeDelayMs` ให้ normalize ช้า แล้วยิง reprocess สองครั้งติดกัน
  - ตรวจ: ครั้งที่สองได้ 409

- [ ] **17.7 Commit Phase 17**
  - ทำ: commit `feat: reprocess file with selected config version`

---

## Phase 18 — Demo script และตรวจรับ

- [ ] **18.1 Script สร้างไฟล์ตัวอย่าง**
  - ทำ: `scripts/generate-sample.ps1` สร้าง `orders-a.csv` 100 records (ผิด 2) และ `orders-b.csv`
  - ตรวจ: เปิดไฟล์แล้วหาแถวที่ผิดเจอ

- [ ] **18.2 Script reset**
  - ทำ: `scripts/reset-demo.ps1` สั่ง `docker compose down -v`, `up -d`, สร้าง topic และล้าง `input/`, `archive/`, `data/`
  - ตรวจ: รันแล้วระบบกลับสู่สถานะเริ่มต้น

- [ ] **18.3 ไล่เกณฑ์ตรวจรับครบทุกข้อ** (ตารางด้านล่าง)

- [ ] **18.4 อัปเดต README**
  - ทำ: เพิ่มวิธีรัน: ลำดับ Docker → API → Watcher → Worker และลิงก์แผนนี้
  - ตรวจ: คนที่ไม่เคยเห็น repo ทำตาม README แล้วรันได้

- [ ] **18.5 Commit สุดท้าย**
  - ทำ: commit `docs: add demo scripts and run instructions`

### เกณฑ์ตรวจรับ (จาก demo-plan)

| ตรวจ | เกณฑ์ | พิสูจน์ใน step |
|---|---|---|
| - [ ] | สร้าง schema/config ผ่าน API และ Source เก็บเป็นข้อความ | 5.9, 6.10, 11.8 |
| - [ ] | Worker ไม่อ่านไฟล์ก่อนครบ 30 วินาที และ normalize ทำงานได้ระหว่างนั้น | 10.6, 14.1 |
| - [ ] | Row job เริ่ม normalize ก่อน File Import Job จบ | 14.10 |
| - [ ] | ไฟล์ A และ B ประมวลผลคาบเกี่ยวกัน และ log partition ยืนยัน | 10.7 |
| - [ ] | 100 records ผิด 2: Source 100, Normalized 98, ค้น error ได้ | 14.9, 15.3 |
| - [ ] | Archive หลังเข้า Source ครบ ขณะ normalize อาจยังทำอยู่ | 12.3, 14.10 |
| - [ ] | หยุดหลัง Source/outbox commit แล้วเริ่มใหม่ ส่งต่อได้ ไม่เพิ่มผลซ้ำ | 9.7, 11.12, 14.11 |
| - [ ] | นำเข้าล้มกลางทางแล้ว retry ไม่เพิ่มข้อมูลซ้ำ | 11.11, 16.2 |
| - [ ] | แก้ config ระหว่างงาน งานเดิมใช้ version ที่ตรึงไว้ | 10.11 |
| - [ ] | Retry ใช้ version เดิม; Reprocess ใช้ version ที่เลือกและมีประวัติแยก | 16.3, 17.4 |
| - [ ] | ไฟล์เนื้อหาเดิมชื่อใหม่เป็น Duplicate | 10.10 |
| - [ ] | Reprocess ไม่ผ่าน ผลเดิมและ version เดิมยังอยู่ | 17.5 |
| - [ ] | แก้ไฟล์หลัง snapshot ได้ `ChangedAfterRead` และไม่ย้าย | 12.5 |
| - [ ] | แถว `Failed` ทำให้ไฟล์เป็น `CompletedWithErrors`; retry แล้วคำนวณใหม่ | 14.8, 16.3 |
