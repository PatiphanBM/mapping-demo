# Phase 29 DatabaseTable Delivery Operations Runbook

คู่มือนี้ใช้กับ `DatabaseTable` destination ที่เขียนแบบ `INSERT` ไปยัง PostgreSQL หรือ SQL Server ผ่าน server-owned logical profile เท่านั้น ระบบไม่สร้าง Entity/ORM model, ไม่รับ raw SQL จากผู้ใช้ และไม่ทำ DDL ที่ฐานข้อมูลปลายทาง

## 1. Safety model

- Config Version ตรึง profile ID, provider, schema/table, schema snapshot/hash, duplicate policy และ mapped fields
- connection string อยู่ใน `ConnectionStrings` ของ API/Worker; browser และ Mapping Config เห็นเฉพาะ logical profile ID
- ก่อน activate และก่อนทุก write ระบบอ่าน catalog จริงผ่าน `information_schema` ร่วมกับ provider catalog แล้วเทียบ snapshot
- ค่า INSERT ทุกค่าผ่าน typed parameter; identifier มาจาก inspected snapshot และถูก quote ตาม provider
- Row Normalize persist encrypted mapped values, hash, job และ outbox ใน Mapping PostgreSQL transaction เดียว; network write เกิดภายหลังใน Worker
- feature/runtime gates เริ่มปิด: `DatabaseTableDestinationActivation=false`, `DatabaseTableDelivery=false`, `database_table_activation_enabled=false`, `database_delivery_paused=true`

## 2. Configuration

ตัวอย่าง Worker/API configuration:

```json
{
  "Features": {
    "DatabaseTableDestinationCreation": false,
    "DatabaseTableDestinationActivation": false,
    "DatabaseTableDelivery": false,
    "DatabaseTableSchemaMonitor": false
  },
  "DestinationDatabases": {
    "Profiles": [
      {
        "id": "customer-postgres",
        "provider": "PostgreSql",
        "connectionStringName": "CustomerTarget",
        "allowedSchemas": ["public"],
        "commandTimeoutSeconds": 30,
        "schemaCacheSeconds": 60,
        "writeEnabled": false
      }
    ]
  },
  "ConnectionStrings": {
    "CustomerTarget": "set by environment/secret store"
  }
}
```

ห้าม commit credential. Account เป้าหมายควรมีสิทธิ์เฉพาะอ่าน catalog, `SELECT` เฉพาะตาราง/key ที่ต้อง reconcile และ `INSERT` เฉพาะตารางที่อนุมัติ; ไม่ให้ `ALTER`, `DROP`, broad owner role หรือ access ข้าม allowlist schema

Schema monitor ใช้ `DatabaseDelivery:SchemaMonitorSeconds` (30–86400) และ `SchemaMonitorMaximumTargets` (1–500) โดย default 300 วินาที/100 targets ต่อรอบ

## 3. Rollout order

1. เจ้าของ target ยืนยัน schema/table, unique key, least-privilege account, backup และ incident contact
2. ตั้ง profile โดย `WriteEnabled=false`; เปิด creation และ schema monitor เพื่อ inspect/preview เท่านั้น
3. สร้าง Config Version แล้วตรวจ mapped fields, required/default/generated rules และ duplicate policy
4. เปิด `database_table_activation_enabled`; activate version หลัง fresh schema check ผ่าน
5. เปิด `WriteEnabled=true`, `Features:DatabaseTableDelivery=true` แต่ยังคง `database_delivery_paused=true`
6. เลือก canary file/config ที่อนุมัติ แล้ว resume database delivery ผ่าน audited operations control
7. เฝ้าดู `/delivery`, schema checks, queue age, conflicts, unknown outcome และ target metrics
8. ขยาย rollout หลัง observation window และ owner sign-off เท่านั้น

การปิด gate ไม่ reroute งานไป destination อื่น งานที่ pin แล้วยังคง profile/snapshot เดิม

## 4. Status และ operator action

| Status | ความหมาย | Automatic action | Operator action |
|---|---|---|---|
| `Pending` | รอ scheduler/write gate | queue เมื่อ gate เปิด | ตรวจ queue age/gates |
| `Delivering` | มี lease กำลังเขียน | รอ completion/lease recovery | ห้าม retry ซ้อน |
| `Delivered` | commit สำเร็จ | ไม่มี | ไม่มี |
| `AlreadyDelivered` | unique key พบ row และ mapped values ตรง | ไม่มี | ไม่มี |
| `RetryableFailed` | transient failure ก่อนพิสูจน์ commit | retry bounded policy | ตรวจ target/network |
| `PermanentFailed` | type/constraint/payload/retry limit | ไม่มี | แก้ config/data แล้ว authorized retry หากเหมาะสม |
| `DeliveryConflict` | key เดิมแต่ mapped values ต่าง | ไม่มี | ตรวจ row เจ้าของข้อมูล; mark failed เท่านั้น ห้าม overwrite |
| `BlockedSchemaDrift` | schema/profile เปลี่ยนแบบ breaking | ไม่มี | สร้าง Config Version ใหม่หรือ restore schema; retry หลังอนุมัติ |
| `UnknownOutcome` | พิสูจน์ผลหลัง commit/lease ไม่ได้ | ไม่มี | ตรวจ target แล้ว mark delivered/failed; retry no-key ต้องยืนยัน duplicate risk |

ทุก manual action ต้องเปิด `ApiDelivery:AllowOperationsControl`, ส่ง `X-Operator-Id`, reason 1–500 ตัวอักษร และ correlation ID การเปลี่ยนสถานะกับ `database_delivery_resolution_audit` commit ใน transaction เดียว

## 5. Schema changes

- `Healthy`: hash/contract ตรง
- `CompatibleChange`: เพิ่ม nullable/defaulted/generated field; งานเดิมทำต่อได้ และ UI แจ้งให้ user เลือกสร้าง version ใหม่หากต้อง mapping field ใหม่
- `BreakingChange`: mapped field หาย/เปลี่ยน, required field ไม่มี default ถูกเพิ่ม, selected unique key เปลี่ยน; block activation/write
- `Unavailable`: inspect ไม่สำเร็จ; block/fail closed

ระบบไม่แก้ schema อัตโนมัติและไม่แก้ mapping version เดิม ถ้า field ที่ config mapping อยู่หายต้องให้ user แก้ target หรือสร้าง Config Version ใหม่ให้ตรง

## 6. Duplicate and ambiguous outcomes

`existingUniqueKey` ต้องเลือก full unique/primary constraint ที่ catalog ยืนยัน เมื่อ unique violation หรือ commit ไม่ชัด Worker query ด้วย composite key และเทียบ canonical mapped values:

- row ตรงทั้งหมด → `AlreadyDelivered`
- key ตรงแต่ค่าแตกต่าง → `DeliveryConflict`
- query/pิสูจน์ไม่ได้ → `UnknownOutcome`

`manualUnknownOutcome` ใช้เมื่อไม่มี unique key ระบบไม่สร้าง `mapping_delivery_id` และไม่สร้างตารางที่ target หลัง crash/timeout ที่อาจ commit แล้วจะหยุดที่ `UnknownOutcome`; retry มีโอกาสสร้าง duplicate จึงต้องรับความเสี่ยงผ่าน audited action

## 7. Incident playbooks

### Target outage / timeout

1. ตั้ง `database_delivery_paused=true`
2. ตรวจ target/network/TLS/permission โดยไม่คัดลอก credential ลง ticket
3. แยก `RetryableFailed` ออกจาก `UnknownOutcome`
4. กู้ target แล้ว resume canary; ห้าม bulk retry unknown outcomes

### Breaking drift

1. pause delivery และเก็บ latest schema check/hash
2. target owner ยืนยัน change/rollback
3. ถ้า restore schema เดิมไม่ได้ ให้สร้างและ preview Config Version ใหม่
4. activate ด้วย fresh inspection; authorized retry เฉพาะ blocked jobs ที่ยังใช้ snapshot/config ที่ถูกต้อง

### Delivery conflict

1. pause profile หาก conflict มากกว่าศูนย์
2. เปรียบเทียบ business record ที่ target ด้วยสิทธิ์ของ target owner; UI ของ Mapping Demo ไม่เปิด payload
3. ห้าม update/delete row อัตโนมัติ
4. mark failed พร้อม incident/correlation; แก้ต้นเหตุแล้ว reprocess เป็น identity ใหม่เมื่อได้รับอนุมัติ

### Unknown outcome

1. ห้าม automatic retry
2. ถ้ามี key ให้ตรวจ reconciliation/permission; ถ้าไม่มี keyให้ target owner ตรวจด้วย business fields/time window
3. พบ row ที่ถูกต้อง → mark delivered; ยืนยันว่าไม่ commit → retry; พิสูจน์ไม่ได้ → mark failed หรือยอมรับ duplicate risk อย่างชัดเจน

## 8. Verification and rollback

ก่อน Gate I ต้องบังคับรันทั้งสอง provider:

```powershell
$env:MAPPING_POSTGRES_TARGET_INTEGRATION = "..."
$env:MAPPING_REQUIRE_POSTGRES_TARGET_INTEGRATION = "true"
$env:MAPPING_SQLSERVER_TARGET_INTEGRATION = "..."
$env:MAPPING_REQUIRE_SQLSERVER_TARGET_INTEGRATION = "true"
dotnet test MappingDemo.sln
```

Rollback ปลอดภัยที่สุดคือ pause delivery → ปิด profile `WriteEnabled` → ปิด activation feature/runtime gate → activate prior Config Version สำหรับไฟล์ใหม่ งานเดิมไม่ถูก reroute และระบบไม่ rollback row ที่ commit ไป target อัตโนมัติ การคืนข้อมูล target ใช้ backup/restore หรือ procedure ของ target owner เท่านั้น
