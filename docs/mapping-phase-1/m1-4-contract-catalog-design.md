---
title: แบบ contract snapshot, destination profile และ capability catalog ของ M1.4
status: draft
last_updated: 2026-11-16
---

# M1.4 — contract/profile snapshot และ capability catalog

อ้าง [plan.md](plan.md) M1.4, [contract-pa-inform.md](contract-pa-inform.md), [data-runtime-design.md](data-runtime-design.md) §8 และ D-02/D-06/M-21. ตัวจริงของ `pa-inform/1` คือ `GET /api/pa/informs/schema/1` ของ Informs ซึ่งคืน `application/schema+json` จากไฟล์ JSON Schema ที่ฝังใน Informs. Mapping อ่านผ่าน HTTP เท่านั้น ไม่อ้าง code หรือ DB ของ Informs.

## Snapshot และ version pin

1. รับ bytes ของ schema ที่ endpoint ส่งจริง แล้วตรวจ `$id = insurance-validation/pa-inform/1`, `$schema`, root `type=object` และ `$defs.inform` ก่อนรับเข้า catalog. คำนวณ SHA-256 จาก **bytes เดิม**; ไม่ hash JSON ที่ deserialize/serialize ใหม่.
2. เก็บ `contract_snapshots(contract_version, schema_bytes, schema_hash)` แบบ immutable. `(contract_version, schema_hash)` เดิมใช้ snapshot เดิม; bytes ใหม่ภายใต้ `pa-inform/1` สร้าง snapshot ใหม่ ไม่เขียนทับ.
3. `mapping_config_versions.contract_snapshot_id` ระบุ snapshot ที่ใช้ตอน save version. งานที่ pin Config Version แล้วอ่าน snapshot ด้วย ID นั้น; ไม่เรียก current schema ของ Informs มาแทนเมื่อ retry/inspect.
4. Profile เป็น server-owned logical key/version. เก็บเฉพาะ settings ที่แสดงได้, hash ของ versioned connection settings และ key/reference สำหรับ resolve จริง; ห้ามส่ง base URL, credential หรือ secret ผ่าน API. Profile key/version เดิมแต่ hash ต่างตอบ conflict ไม่ overwrite.

ตัวอย่างข้อมูลสมมติ:

| รายการ | ID/ค่า | ผล |
|---|---|---|
| schema A | `pa-inform/1`, hash `H1` | snapshot `S1` |
| Config A/V1 | `contract_snapshot_id=S1`, profile `P1` | File Job F1 pin A/V1 |
| schema B หลัง Informs เปลี่ยน | `pa-inform/1`, hash `H2` | snapshot `S2`; A/V1/F1 ยังใช้ S1 |
| Config A/V2 | `contract_snapshot_id=S2`, profile `P1` | File Job ใหม่จึงใช้ S2 |

## Path catalog

เดิน `properties` และ `$ref` ภายใน snapshot นั้น; array ใช้ `[]` ใน path, nullable/required ดูจาก schema ของ node และ parent. ไม่สร้าง field list แบบ hardcode ตาม MTLS. ตัวอย่างจาก schema Informs ปัจจุบัน:

| Path | Type/ข้อสังเกต |
|---|---|
| `inform.partnerCode` | `string`, required ใน `inform` |
| `inform.insureds[]` | array item อ้าง `$defs.insured` |
| `inform.insureds[].packageCode` | `string|null`, package ต่อ risk |
| `inform.insureds[].beneficiaries[].seq` | `integer`, required ใน beneficiary |

Path ที่อ้าง `$ref` ต้อง resolve ภายใน `$defs` ของ **snapshot เดียวกัน**; วงอ้างอิง/ชนิดที่ไม่รองรับตอบ diagnostic ไม่วนไม่สิ้นสุด. Hash/path catalog ของ snapshot เก่าคงเดิมแม้ Informs เผย schema ใหม่.

## Capability และ API read model

`GET /api/mapping/contracts/pa-inform/1`, `/destination-profiles`, `/capabilities` ใช้ shape ใน [mapping-api-contract.md](mapping-api-contract.md). Capability `available=true` ได้ต่อเมื่อ implementation + dependency + tests พร้อมจริง. ณ M1.4 ยังไม่มี parser Excel/Delimited/FixedWidth, Transformation Program หรือ delivery HTTP จึงแสดงรายการเหล่านี้เป็น unavailable พร้อม reason; destination `ApiContract` ไม่อ้างว่าพร้อมส่งเพียงเพราะอ่าน schema ได้. `LegacyTable`/`JsonFile`/`DatabaseTable` เป็น backlog ตาม M-21.

## ตรวจรับ

- HTTP fake ที่คืน schema สองชุดต่าง bytes → snapshot/hash สองชุด; pin A/V1 ยังคืน path/hash จาก S1.
- Path catalog พบ `insureds[]`, `insureds[].packageCode`, `beneficiaries[].seq` และ required/nullability ตาม schema; malformed `$ref` ปฏิเสธ.
- Profile key/version เดิมแต่ settings/hash ต่าง conflict; API ไม่คืน URL/secret.
- API capability ไม่โฆษณา parser/transform/delivery ว่าพร้อมก่อน implementation และ tests; PostgreSQL integration tests + `scripts/check.ps1` ผ่าน.
