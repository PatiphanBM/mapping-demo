# Mapping Demo — แผนหน้าเว็บตั้งค่า Mapping Config (Phase 19)

แผนนี้ต่อจาก [learning-plan.md](learning-plan.md) โดยเพิ่มหน้าเว็บสำหรับตั้งค่า Mapping Config และ Config Version ผ่าน `MappingDemo.Api` แทนการยิง request หรือรัน `configure-demo.ps1`

คำศัพท์ยึดตาม [CONTEXT.md](../CONTEXT.md) และเหตุผลของการตัดสินใจอยู่ใน [demo-plan.md](demo-plan.md) หัวข้อ "ข้อสรุปรอบที่ 7"

---

## วิธีใช้แผนนี้

ใช้กติกาเดียวกับ [learning-plan.md — วิธีใช้แผนนี้](learning-plan.md#วิธีใช้แผนนี้) ทุกข้อ ทั้งการทำทีละ step รูปแบบ **ทำ / เข้าใจ / ตรวจ** และ **AI agent ห้ามสร้าง commit เอง** ส่วนที่เพิ่มสำหรับแผนนี้มีดังนี้

1. ห้ามติดตั้ง npm package นอกเหนือจากที่ `create-next-app` ใส่มาให้โดยไม่ถามก่อน เพราะเป้าหมายคือทำให้เบาที่สุด
2. โค้ดเว็บอยู่ใน `web/` ทั้งหมด แก้ฝั่ง .NET ได้เฉพาะใน step ที่ระบุไว้ (19.1 และ 19.8)
3. ถ้า Next.js เวอร์ชันที่ติดตั้งมีชื่อไฟล์หรือ API ต่างจากแผน (เช่น ชื่อไฟล์ config หรือรูปแบบ `params`) ให้ใช้ตามเวอร์ชันที่ติดตั้ง แล้วบอกว่าต่างตรงไหน

### คำสั่งตั้งต้นสำหรับ AI agent (copy ไปใช้ได้)

```text
อ่าน docs/web-config-plan.md, docs/learning-plan.md (เฉพาะหัวข้อ "วิธีใช้แผนนี้"), docs/demo-plan.md หัวข้อ "ข้อสรุปรอบที่ 7" และ CONTEXT.md
ก่อน implement ให้ตรวจเครื่องตาม "Requirement ของเครื่อง" ในแผนนี้ ถ้าไม่ตรงให้หยุดและบอกฉันก่อน
ถ้าฉันสั่งเริ่ม Phase 19 ให้ทำเฉพาะ step แรกที่ยังไม่เสร็จ แล้วหยุดรอคำสั่งใหม่
ถ้าฉันสั่ง Step ที่ระบุเลข ให้ทำเฉพาะ step นั้น อธิบาย concept ก่อนเขียนโค้ด แล้วบอกวิธีตรวจผล
ห้ามติดตั้ง npm package เพิ่มเองโดยไม่ถาม และห้ามสร้าง git commit เอง
```

## เป้าหมายและขอบเขต

**อยู่ใน scope**

- ดูรายการ Mapping Config และสร้าง Config ใหม่ โดยเลือก Source/Normalized table ที่มีอยู่แล้ว
- ดูรายละเอียด Config, รายการ Config Version และ rule ของแต่ละ version
- สร้าง Config Version ใหม่ ทั้งแบบเริ่มจากว่างและแบบ clone จาก version เดิม
- Activate version
- แสดง validation error จาก API (`ValidationProblem` และ 409) ให้ตรงกับช่องที่ผิด

**นอก scope (ทำทีหลังได้)**

- หน้าจัดการ Tables: ยังใช้สคริปต์หรือ `requests/tables.http` สร้างตารางเหมือนเดิม
- Auto-fill rule จาก header ของไฟล์ CSV ตัวอย่าง
- Validation ซ้ำฝั่ง client: ให้ API เป็นผู้ตัดสินที่เดียว
- แก้ชื่อหรือ input folder ของ Config และลบ Config/Version: API ยังไม่มี endpoint และ version ต้องคงที่ตาม CONTEXT
- หน้าดูสถานะ File Job, retry และ reprocess
- Auth, deploy และ production build

## เครื่องมือที่เลือกและเหตุผล

| เรื่อง | เลือก | เหตุผล |
|---|---|---|
| Framework | Next.js (App Router) + TypeScript | ผู้ใช้สนใจ และเป็นแค่ demo; งานจริงของบริษัทใช้ Angular จึงเน้น concept ที่ย้ายไปใช้ได้ |
| การ render | ทุกหน้าเป็น client component (`"use client"`) ที่ `fetch` ไป API | เบาที่สุด ไม่ใช้ Server Actions, DB หรือ ORM |
| Styling | Tailwind ที่มากับ `create-next-app` | ไม่ต้องลง UI kit |
| State / Form | `useState` ธรรมดา | ฟอร์มมีไม่กี่หน้า ไม่คุ้มที่จะลง library |
| เรียก API | `rewrites` ใน Next.js ให้ `/api/*` ส่งต่อไป `MappingDemo.Api` | เบราว์เซอร์เห็น origin เดียว จึงไม่ต้องเปิด CORS ที่ API |
| API client | `web/lib/api.ts` ไฟล์เดียว ไม่ผูกกับ React | ย้ายแนวคิดไปเป็น Angular service ได้ตรง ๆ |

ถ้าทำเวอร์ชัน Angular ภายหลัง: `rewrites` เทียบได้กับ `proxy.conf.json`, `lib/api.ts` เทียบได้กับ service ที่ใช้ `HttpClient` ส่วนการ map error จาก `ValidationProblem` ใช้ logic เดิมได้เลย

## Requirement ของเครื่อง

| รายการ | ต้องการ | คำสั่งตรวจ | หมายเหตุ |
|---|---|---|---|
| ทุกอย่างใน learning-plan | ตามตารางเดิม | — | API ต้องรันได้ที่ `http://localhost:5181` |
| Node.js | เวอร์ชันที่ Next.js ล่าสุดรองรับ (ดูหน้า installation ของ Next.js ณ วันที่ทำ) | `node -v` | |
| npm | มากับ Node.js | `npm -v` | |
| Port ว่าง | `3000` | `Get-NetTCPConnection -LocalPort 3000` | port ของ `next dev` |
| อินเทอร์เน็ต | เข้าถึง registry.npmjs.org | `npm ping` | ถ้าอยู่หลัง proxy บริษัท อาจต้องตั้ง npm registry |

## เวอร์ชันที่ใช้จริง (กรอกระหว่างทำ)

| รายการ | กรอกใน step | เวอร์ชัน |
|---|---|---|
| Node.js | 19.0 | |
| npm | 19.0 | |
| Next.js | 19.2 | |
| React | 19.2 | |
| TypeScript | 19.2 | |
| Tailwind CSS | 19.2 | |

ดูเวอร์ชันจาก `web/package.json` หรือ `npm ls --depth=0` หลังสร้างโปรเจกต์

## โครงสร้างเมื่อเสร็จ

```text
web/
├─ next.config.ts             # rewrites /api/* → MappingDemo.Api
├─ .env.local.example         # API_BASE_URL=http://localhost:5181
├─ lib/
│  └─ api.ts                  # types ของ contracts + fetch wrapper + ApiError
└─ app/
   ├─ layout.tsx              # header + ลิงก์ไป /configs
   ├─ page.tsx                # redirect ไป /configs
   └─ configs/
      ├─ page.tsx             # รายการ config
      ├─ new/page.tsx         # ฟอร์มสร้าง config
      └─ [id]/
         ├─ page.tsx          # รายละเอียด + versions + activate
         └─ versions/new/page.tsx   # version editor (?from=N เพื่อ clone)
```

## Endpoint ที่หน้าเว็บใช้

| หน้า | Endpoint |
|---|---|
| รายการ config | `GET /mapping-configs`, `GET /tables` |
| สร้าง config | `GET /tables`, `POST /mapping-configs` |
| รายละเอียด | `GET /mapping-configs/{id}`, `GET /tables/{id}` ×2, `POST /mapping-configs/{id}/versions/{n}/activate` |
| Version editor | `GET /mapping-configs/{id}`, `GET /tables/{id}` ×2, `POST /mapping-configs/{id}/versions` |

ไม่ต้องเพิ่ม endpoint ใหม่ฝั่ง API

---

## Phase 19 — หน้าเว็บตั้งค่า Mapping Config

- [ ] **19.0 ตรวจเครื่องและ prerequisite**
  - ทำ: ตรวจตาราง Requirement ด้านบน แล้วกรอก Node.js และ npm ในตาราง "เวอร์ชันที่ใช้จริง" และยืนยันว่า Phase 5–6 ของ learning-plan เสร็จแล้ว (API config ใช้งานได้)
  - เข้าใจ: เว็บเป็น client ของ API ที่มีอยู่แล้ว ถ้า API ยังทำงานไม่ครบ จะแยกไม่ออกว่าปัญหาอยู่ที่เว็บหรือที่ API
  - ตรวจ: `node -v`, `npm -v`, `npm ping` ผ่าน, port 3000 ว่าง และ `GET http://localhost:5181/health` ได้ 200

- [ ] **19.1 API ส่ง enum เป็น string**
  - ทำ: ใน `Program.cs` ของ API เปลี่ยน `AddControllers()` เป็น `AddControllers().AddJsonOptions(...)` และเพิ่ม `JsonStringEnumConverter` แล้วอัปเดต comment ใน `requests/tables.http` ว่ารับได้ทั้งชื่อและตัวเลข
  - เข้าใจ: เว็บจะได้ `"kind": "Source"` และ `"dataType": "Date"` แทน `0` และ `1` อ่านง่ายกว่าและไม่ต้องจำลำดับ enum ฝั่ง TS; `JsonStringEnumConverter` ยังรับค่าตัวเลขเป็นค่าเริ่มต้น `configure-demo.ps1` และ `.http` เดิมจึงใช้ได้ต่อ; การตั้งค่านี้มีผลเฉพาะ JSON ของ MVC ไม่กระทบการ serialize rule ลง `jsonb`
  - ตรวจ: รัน `reset-demo.ps1` → API → `configure-demo.ps1` ผ่าน, `GET /tables` เห็น kind และ dataType เป็นชื่อ และ `dotnet test` เขียว

- [ ] **19.2 สร้างโปรเจกต์ `web/`**
  - ทำ: จาก root รัน `npx create-next-app@latest web` เลือก TypeScript, ESLint, Tailwind, App Router และไม่ใช้ `src/` แล้วกรอกเวอร์ชันลงตาราง
  - เข้าใจ: `create-next-app` สร้าง `.gitignore` ของตัวเองใน `web/` ซึ่งกัน `node_modules/` และ `.next/` ไว้แล้ว; โปรเจกต์นี้อยู่นอก `MappingDemo.sln` เพราะเป็นคนละ toolchain
  - ตรวจ: `cd web; npm run dev` แล้วเปิด <http://localhost:3000> เห็นหน้าเริ่มต้น และ `git status` ไม่เห็น `node_modules`

- [ ] **19.3 Proxy ไป API และ `lib/api.ts`**
  - ทำ:
    - ใน `next.config` เพิ่ม `rewrites` ให้ `/api/:path*` ส่งไป `${API_BASE_URL}/:path*` โดยค่าเริ่มต้นเป็น `http://localhost:5181` และสร้าง `.env.local.example`
    - ใน `lib/api.ts` เขียน type ให้ตรงกับ contracts ฝั่ง .NET: `TableResponse`, `ColumnRequest`, `MappingConfigResponse`, `MappingConfigDetailsResponse`, `MappingConfigVersionDetailsResponse`, `FileToSourceRule`, `SourceToNormalizedRule` และ request ทั้งสองตัว
    - เขียน fetch wrapper ที่โยน `ApiError` ซึ่งเก็บ `status`, `title` และ `errors: Record<string, string[]>` จาก ProblemDetails
  - เข้าใจ: เบราว์เซอร์เรียก `localhost:3000/api/...` ส่วน Next.js dev server ส่งต่อให้ API ฝั่ง server จึงไม่ติด CORS; key ใน `errors` เป็น path เช่น `fileToSource[2].sourceColumn` ซึ่งเป็นสิ่งที่หน้า editor จะใช้ชี้ไปที่ช่องที่ผิด
  - ตรวจ: เปิด <http://localhost:3000/api/health> ได้ผลเดียวกับ API และ `npm run build` ผ่าน type check

- [ ] **19.4 Layout และหน้า `/configs`**
  - ทำ: ใน `layout.tsx` ใส่ header ที่มีลิงก์ไป Mapping Configs ให้ `/` redirect ไป `/configs` และหน้า `/configs` แสดงตาราง name, input folder, ชื่อ Source/Normalized table (จับคู่จาก `GET /tables`), สถานะ active ("มี" / "ยังไม่มี") และปุ่ม "สร้าง Config"
  - เข้าใจ: list endpoint คืนแค่ `activeVersionId` ไม่มีเลข version หน้า list จึงแสดงแค่สถานะ ส่วนเลข version ไปดูที่หน้ารายละเอียด เพื่อไม่ต้องยิง request ทีละ config; ต้องมีสถานะ loading และ error เมื่อ API ไม่ได้รันอยู่
  - ตรวจ: หลังรัน `configure-demo.ps1` เห็น `orders-demo` และเมื่อหยุด API หน้าเว็บขึ้นข้อความ error ไม่ค้างหน้าว่าง

- [ ] **19.5 หน้า `/configs/new`**
  - ทำ: ฟอร์มที่มี name, input folder, dropdown Source table (กรองเฉพาะ `kind = Source`) และ dropdown Normalized table (กรองเฉพาะ `kind = Normalized`) ถ้าสำเร็จให้ไปที่ `/configs/{id}` ถ้าได้ 400 ให้แสดง error ใต้ช่อง ถ้าได้ 409 ให้แสดง `detail` ไว้บนฟอร์ม
  - เข้าใจ: API ตรวจ kind ของตารางอยู่แล้ว การกรอง dropdown เป็นแค่การช่วยผู้ใช้ ไม่ใช่การ validate; key error ของ ASP.NET ตรงกับชื่อ field แบบ camelCase เช่น `inputFolder`
  - ตรวจ: สร้าง config ใหม่แล้วมีโฟลเดอร์ `input/<folder>` เกิดขึ้น, ใส่ `a/b` เป็น input folder เห็น error ใต้ช่อง และสร้าง folder ซ้ำเห็นข้อความ 409

- [ ] **19.6 หน้า `/configs/[id]`**
  - ทำ: แสดงข้อมูล config, ตาราง version (เลข version, badge Active, จำนวน rule ของแต่ละขั้น) และกดขยายเพื่อดู rule ทั้งหมด แต่ละแถวมีปุ่ม Activate (ซ่อนถ้า active อยู่แล้ว) และปุ่ม "สร้าง version ใหม่จาก vN" ด้านบนมีปุ่ม "สร้าง version ใหม่" แบบเริ่มจากว่าง
  - เข้าใจ: Activate มีผลกับ **ไฟล์ใหม่** เท่านั้น และ Watcher จะเห็น version ใหม่หลัง refresh ตาม `Watcher:ConfigRefreshSeconds` ควรเขียนบอกไว้ใต้ปุ่ม; API validate ซ้ำตอน activate เพราะ schema อาจเปลี่ยนหลังสร้าง version ถ้าได้ 400 ให้แสดงรายการ error
  - ตรวจ: activate version อื่นแล้ว badge ย้าย และ `GET /mapping-configs/{id}` ให้ `activeVersionId` ตรงกัน

- [ ] **19.7 Version editor `/configs/[id]/versions/new`**
  - ทำ:
    - โหลด config และตาราง Source/Normalized ถ้ามี `?from=N` ให้เติม rule จาก version N ไว้ก่อน
    - **File → Source**: ตารางที่เพิ่มหรือลบแถวได้ แต่ละแถวมี CSV header (text) และ Source column (dropdown จาก Source table)
    - **Source → Normalized**: แสดงหนึ่งแถวต่อ Normalized column ทุกตัว ไม่ให้เพิ่มหรือลบ แต่ละแถวแสดงชื่อ, dataType และเครื่องหมาย required ให้เลือก Source column หรือ "— ไม่ map —" และช่อง `format` จะแสดงเฉพาะ column ที่เป็น `Date`
    - เมื่อกดบันทึก ส่ง `POST .../versions` ถ้าได้ 201 กลับไปหน้ารายละเอียด (ไม่ activate ให้อัตโนมัติ) ถ้าได้ 400 ให้แสดง error ที่แถวและช่องที่ตรงกัน
  - เข้าใจ:
    - แถว Normalized ที่เลือก "ไม่ map" จะไม่ถูกส่งไป index ใน payload จึงไม่ตรงกับ index บนหน้าจอ ต้องเก็บ map จาก index ของ payload กลับไปหาแถวบนหน้าจอเพื่อวาง error `sourceToNormalized[i].*` ให้ถูกแถว
    - Error ที่ key เป็น `sourceToNormalized` เฉย ๆ (required column ยังไม่ถูก map) ให้แสดงที่หัวของส่วนนั้น
    - ถ้าช่อง `format` ว่าง ต้องส่ง `null` ไม่ใช่ `""` เพราะ validator ถือว่า string ว่างเป็น format ที่ผิด
    - การ map แบบแถวต่อ Normalized column ทำให้ map column เดิมซ้ำไม่ได้ตั้งแต่บนหน้าจอ
  - ตรวจ:
    - clone v1 ของ `orders-demo` แล้วเปลี่ยน format ของ `order_date` เป็น `dd/MM/yyyy` ได้ version ใหม่
    - เลือก "ไม่ map" ที่ `amount` (required) เห็น error ที่หัวส่วน Source → Normalized
    - ใส่ CSV header ซ้ำสองแถว เห็น error ที่แถวที่สอง
    - ใส่ format ที่ใช้ไม่ได้ เช่น `%` เห็น error ที่ช่อง format ของแถวที่ถูกต้องแม้มีแถวก่อนหน้าเป็น "ไม่ map"

- [ ] **19.8 ตรวจรับครบเส้นทางผ่านหน้าเว็บ**
  - ทำ: เพิ่ม switch `-TablesOnly` ให้ `scripts/configure-demo.ps1` เพื่อสร้างแค่ตาราง Source/Normalized แล้วหยุด จากนั้นทำตามลำดับ:
    1. `reset-demo.ps1` → รัน API → `configure-demo.ps1 -TablesOnly`
    2. เปิดเว็บ สร้าง config `orders-demo` / `orders` แล้วสร้าง version 1 ที่มี rule เหมือน `configure-demo.ps1` จากนั้น Activate
    3. รัน Worker → รอให้ Watcher โหลด config → `generate-sample.ps1`
  - เข้าใจ: นี่คือการพิสูจน์ว่าหน้าเว็บแทนสคริปต์ตั้งค่าได้จริง ผลลัพธ์ต้องเหมือนการใช้สคริปต์ทุกอย่าง
  - ตรวจ: `GET /file-jobs` ของ `orders-a.csv` ได้ Source 100, Normalized 98 และ 2 row errors ส่วน `orders-b.csv` ได้ 100 ครบ และ `configure-demo.ps1` แบบไม่ใส่ switch ยังทำงานเหมือนเดิม

- [ ] **19.9 อัปเดตเอกสาร**
  - ทำ: เพิ่มหัวข้อ "หน้าเว็บตั้งค่า Mapping Config" ใน README (`cd web`, `npm install`, `npm run dev`, ต้องเปิด API ก่อน และตั้ง `API_BASE_URL` ได้) และติ๊ก Phase 19 ใน learning-plan
  - ตรวจ: คนที่ไม่เคยเห็น repo ทำตาม README แล้วเปิดหน้าเว็บสร้าง config ได้

- [ ] **19.10 Commit Phase 19**
  - ทำ: สรุปงานให้ผู้ใช้ แล้วให้ผู้ใช้ commit `feat(web): add mapping config web UI` เมื่อสั่งแยกต่างหาก
