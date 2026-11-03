# Phase 26.5 — API Destination Profile Catalog Cutover Handoff

วันที่: 2026-11-02  
สถานะ: Step 26.5 complete; Gate F ยัง pending Step 26.6

## ผลลัพธ์

- `0015_api_destination_profile_cutover.sql` seed `pa-mtls`, `pa-mtls-inform` และ `claims-envelope` เป็น Active version 1 แบบ idempotent พร้อม canonical content hash และ append-only activation audit
- migration backfill `mapping_config_versions.api_destination_profile_version_id` และ `delivery_jobs.destination_profile_version_id` จาก logical profile key ไป exact seeded version; missing/ambiguous/mismatched mapping fail migration
- API/Worker รองรับ `Configuration`, `Compare`, `Postgres`; `Compare` เทียบ canonical profile content และต้องระบุ authoritative side เป็น `Configuration` หรือ `Postgres`
- `Compare` mode ปิด profile mutation แต่ยังอนุญาต read เพื่อ freeze catalog ระหว่าง mixed deployment
- default และ application config ปัจจุบันใช้ `Postgres`; duplicated `ApiDestinationProfiles` definitions ถูกลบ
- API/Worker load safe catalog ตอน startup; catalog unavailable/parity mismatch ทำให้ startup fail closed
- `Configuration` เหลือเฉพาะ bootstrap/local test และ rollback ก่อนมี DB-only profile use ตาม design

## Evidence

| Check | Result |
|---|---|
| Compare/parity/mismatch/pinned cache/source parser | 10/10 ผ่าน |
| Cutover migration structure + PostgreSQL idempotence/backfill/canonical hash | 2/2 ผ่าน โดยบังคับ `MAPPING_POSTGRES_INTEGRATION` |
| Admin create/update/validate/activate hash + Compare mutation freeze regression | 9/9 ผ่าน |
| PostgreSQL catalog contract + 0014 migration regression | 6/6 ผ่าน |
| API restart หลัง apply migration บน local database | ผ่าน; catalog load และ HTTP host start สำเร็จ |
| Worker restart ด้วย Postgres source | ผ่าน; host และ consumers start สำเร็จ |
| Build | ผ่าน 0 warnings / 0 errors |

## Full regression diagnostic สำหรับ Step 26.6

รอบบังคับ PostgreSQL ผ่าน 431/439. แปด failure ที่ยังต้องจัดการใน Step 26.6 อยู่ใน tests นอก cutover path:

- Contract snapshot fixture สร้าง profile ที่ไม่ผ่าน validation ปัจจุบัน 2 cases
- destination migration fixture ไม่มีคอลัมน์จาก migration 0014 จำนวน 1 case
- PostgreSQL query-plan assertion ต่างจาก planner output 1 case
- Row normalization integration state/lookup failures 3 cases
- API scheduling fixture profile validation 1 case

`dotnet format --verify-no-changes` ยังพบ whitespace เดิมใน `ApiDestinationProfileAdministrationService.cs` และ `ExcelWorkbookReader.cs`; ไฟล์ใหม่ของ Step 26.5 ไม่มี formatter diagnostic. ยังไม่แก้กว้างเพื่อเลี่ยงเปลี่ยนงานเดิมนอก scope

## Rollback boundary

- ก่อน DB-only profile/version ถูกใช้: ตั้งทั้ง API/Worker เป็น `Compare` โดย `CompareAuthoritativeSource=Configuration`, ยืนยัน parity แล้วจึงย้อน `Configuration` ได้
- หลัง DB-only use: ห้ามย้อน source เป็น Configuration; คง Postgres authoritative, ปิด Admin mutation และ rollback ด้วยการ reactivate historical Profile Version
- schema/seed/audit/pins เป็น expand-only; rollback application ห้าม drop profile rows หรือ exact pins
