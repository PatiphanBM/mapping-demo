---
title: หลักฐานตรวจแบบ M1.1 บน PostgreSQL local
status: approved
last_updated: 2026-11-16
---

# หลักฐานตรวจแบบ M1.1 บน PostgreSQL local

ตรวจวันที่ 2026-11-16 สำหรับ [แบบข้อมูลและ runtime](data-runtime-design.md) §8 โดยใช้ proof schema ย่อและข้อมูลสมมติ ไม่ใช่ EF migration/application tests ของ repo Mapping และไม่ใช้แทน M1.3/M1.5/M3/M4

## 1. Environment และผล

- Docker `InsuranceValidation.Postgres`, image `postgres:15-alpine`, host port **5433**, PostgreSQL **15.20**
- ฐานใหม่ชั่วคราวแยกจาก `InsuranceValidation`; admin สร้าง schema แล้วให้ `insurance_user` เพียง USAGE/CREATE บน schema `mapping` ตามรูปแบบ init ของ repo; proof tables ทั้งหมดเป็นของ role นี้
- **25 constraint/transaction checks + 4 concurrency/fencing assertions = 29 ผ่าน**, SQL execution exit 0; concurrency ใช้ **6 sessions** (2 ต่อกรณี hash/budget/attempt cap)
- สร้าง **13 proof tables** ใน schema `mapping` และ **0 tables ใน public**; ไม่แก้ตารางของ application เดิม/DB บริษัท
- คำว่า approved ใช้กับหลักฐานผลตรวจแบบที่บันทึกนี้ ไม่ใช่การอนุมัติ business fields/contract ของ Dev 1 หรือการ deploy schema

| กลุ่ม checks | จำนวน | ผลที่ยืนยัน |
|---|---:|---|
| File content scope | 4 | เปลี่ยนชื่อ/hash เดิมซ้ำ, ข้าม config versions ซ้ำ, hash ใหม่รับได้, คนละ config ไม่ชน |
| Version/source lineage | 6 | config/source version ต้องเป็นเจ้าของเดียวกัน, File Job pin ตรงกัน, row/file/definition ไม่สลับกัน, row number ไม่ซ้ำ |
| Immutability/schema expansion | 6 | Source update/delete/truncate ถูกปฏิเสธ, definition และ File Job.CreatedAt ไม่เปลี่ยน, เพิ่ม nullable column ไม่แก้ raw/definition เก่า |
| Delivery identity/attempts | 7 | source/file pair ตรง, output key ไม่ซ้ำ, attempt 7/attempt number ซ้ำ/counter reset/counter เกิน 6 ถูกปฏิเสธ, Delivered ต้องมี inform ID |
| Atomicity/schema scope | 2 | registry/raw/job/outbox/checkpoint rollback พร้อมกัน และไม่มี public tables |
| Concurrent claims/fencing | 4 | สอง senders ได้ global budget รวม 1; hash มี owner เดียว; reserve attempt สุดท้ายได้ครั้งเดียว; stale epoch update ได้ 0 rows |

## 2. ขอบเขตของผลตรวจ

- ตรวจ constraint mechanics ของ proof schema ที่ลด columns ซึ่งไม่เกี่ยวกับแต่ละ invariant; ตาราง schema/profile/audit/config activation ฉบับเต็มยังต้อง implement/test ผ่าน EF migrations ใน M1.3/M1.5
- Source expansion ตรวจ `ADD COLUMN text NULL`, raw values และ pinned definition เดิม; การปิด rename/delete/type-change ผ่าน API ยังเป็น integration tests ของ M1.5 ไม่อ้างว่า PostgreSQL table owner ถูกห้าม ALTER TABLE โดยตัวมันเอง
- Concurrent sender proof ตรวจ DB claim/reservation ไม่ส่ง HTTP จริง; ไม่ใช่ผลทดสอบ lease timing/network cancellation/Kafka/end-to-end Informs
- ชุด proof ไม่ทดสอบ cryptography, full file fixture, corporate permissions หรือ D-23 ข้าม service; ติดตาม Q4/Q5/Q7 ตามเดิม
- การลบฐานชั่วคราวตรวจชื่อ prefix และ database comment ของงานก่อนดำเนินการ ผล cleanup บันทึกท้ายเอกสาร

## 3. SQL ที่ใช้ตรวจ constraints และ transactions

ใช้เฉพาะฐานทดสอบใหม่ที่มี schema `mapping` และ role ตามข้อ 1 ห้ามรัน SQL นี้บนฐานบริษัทหรือฐาน application ที่มีตารางชื่อเดียวกัน

```sql
SET ROLE insurance_user;
SET TIME ZONE 'UTC';
CREATE FUNCTION mapping.uid(n integer) RETURNS uuid LANGUAGE sql IMMUTABLE AS $$ SELECT ('019a0000-0000-7000-8000-' || lpad(to_hex(n),12,'0'))::uuid $$;
CREATE FUNCTION mapping.assert_ok(ok boolean, label text) RETURNS void LANGUAGE plpgsql AS $$ BEGIN IF ok IS DISTINCT FROM true THEN RAISE EXCEPTION 'FAIL: %',label; END IF; RAISE NOTICE 'PASS: %',label; END $$;
CREATE FUNCTION mapping.expect_error(label text, statement text, expected_state text) RETURNS void LANGUAGE plpgsql AS $$ DECLARE actual_state text; BEGIN BEGIN EXECUTE statement; EXCEPTION WHEN OTHERS THEN GET STACKED DIAGNOSTICS actual_state = RETURNED_SQLSTATE; END; IF actual_state IS DISTINCT FROM expected_state THEN RAISE EXCEPTION 'FAIL: %, expected %, got %',label,expected_state,actual_state; END IF; RAISE NOTICE 'PASS: %',label; END $$;
CREATE FUNCTION mapping.reject_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'immutable evidence' USING ERRCODE='23514'; END $$;
CREATE TABLE mapping.mapping_configs(id uuid PRIMARY KEY);
CREATE TABLE mapping.source_definitions(id uuid PRIMARY KEY, config_id uuid NOT NULL REFERENCES mapping.mapping_configs ON DELETE RESTRICT, UNIQUE(id,config_id));
CREATE TABLE mapping.source_definition_versions(id uuid PRIMARY KEY, source_definition_id uuid NOT NULL, config_id uuid NOT NULL, version_number integer NOT NULL CHECK(version_number>0), fields jsonb NOT NULL CHECK(jsonb_typeof(fields)='array'), UNIQUE(source_definition_id,version_number), UNIQUE(id,config_id), UNIQUE(id,source_definition_id), FOREIGN KEY(source_definition_id,config_id) REFERENCES mapping.source_definitions(id,config_id) ON DELETE RESTRICT);
CREATE TABLE mapping.mapping_config_versions(id uuid PRIMARY KEY, config_id uuid NOT NULL REFERENCES mapping.mapping_configs ON DELETE RESTRICT, source_definition_version_id uuid NOT NULL, version_number integer NOT NULL CHECK(version_number>0), UNIQUE(config_id,version_number), UNIQUE(id,config_id,source_definition_version_id), FOREIGN KEY(source_definition_version_id,config_id) REFERENCES mapping.source_definition_versions(id,config_id) ON DELETE RESTRICT);
CREATE TABLE mapping.file_jobs(id uuid PRIMARY KEY,config_id uuid NOT NULL,config_version_id uuid NOT NULL,source_definition_version_id uuid NOT NULL,content_hash bytea NOT NULL CHECK(octet_length(content_hash)=32),created_at timestamptz NOT NULL,committed_count bigint NOT NULL DEFAULT 0 CHECK(committed_count>=0),UNIQUE(id,config_id,content_hash),UNIQUE(id,source_definition_version_id),FOREIGN KEY(config_version_id,config_id,source_definition_version_id) REFERENCES mapping.mapping_config_versions(id,config_id,source_definition_version_id) ON DELETE RESTRICT);
CREATE FUNCTION mapping.guard_file_identity() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF (NEW.id,NEW.config_id,NEW.config_version_id,NEW.source_definition_version_id,NEW.created_at,NEW.content_hash) IS DISTINCT FROM (OLD.id,OLD.config_id,OLD.config_version_id,OLD.source_definition_version_id,OLD.created_at,OLD.content_hash) THEN RAISE EXCEPTION 'file pin is immutable' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;
CREATE TRIGGER freeze_file BEFORE UPDATE ON mapping.file_jobs FOR EACH ROW EXECUTE FUNCTION mapping.guard_file_identity();
CREATE TABLE mapping.file_content_claims(config_id uuid NOT NULL,content_hash bytea NOT NULL CHECK(octet_length(content_hash)=32),canonical_file_job_id uuid NOT NULL,PRIMARY KEY(config_id,content_hash),FOREIGN KEY(canonical_file_job_id,config_id,content_hash) REFERENCES mapping.file_jobs(id,config_id,content_hash) ON DELETE RESTRICT);
CREATE TABLE mapping.source_row_refs(id uuid PRIMARY KEY,file_job_id uuid NOT NULL,source_definition_id uuid NOT NULL,source_definition_version_id uuid NOT NULL,row_number bigint NOT NULL CHECK(row_number>0),physical_line_number bigint NOT NULL CHECK(physical_line_number>0),UNIQUE(file_job_id,row_number),UNIQUE(id,file_job_id),UNIQUE(id,file_job_id,source_definition_id),FOREIGN KEY(file_job_id,source_definition_version_id) REFERENCES mapping.file_jobs(id,source_definition_version_id) ON DELETE RESTRICT,FOREIGN KEY(source_definition_version_id,source_definition_id) REFERENCES mapping.source_definition_versions(id,source_definition_id) ON DELETE RESTRICT);
CREATE TABLE mapping.src_example_rows(id uuid PRIMARY KEY,file_job_id uuid NOT NULL,source_definition_id uuid NOT NULL CHECK(source_definition_id=mapping.uid(11)),field_a text NULL,field_b text NULL,FOREIGN KEY(id,file_job_id,source_definition_id) REFERENCES mapping.source_row_refs(id,file_job_id,source_definition_id) ON DELETE RESTRICT);
CREATE TABLE mapping.row_jobs(id uuid PRIMARY KEY,source_row_id uuid NOT NULL UNIQUE,file_job_id uuid NOT NULL,FOREIGN KEY(source_row_id,file_job_id) REFERENCES mapping.source_row_refs(id,file_job_id) ON DELETE RESTRICT);
CREATE TABLE mapping.delivery_jobs(id uuid PRIMARY KEY,source_row_id uuid NOT NULL,file_job_id uuid NOT NULL,output_key text NOT NULL CHECK(length(output_key)>0),state text NOT NULL DEFAULT 'Pending' CHECK(state IN('Pending','Sending','Delivered','Rejected','Blocked','RetryableFailed','Failed')),attempts_reserved smallint NOT NULL DEFAULT 0 CHECK(attempts_reserved BETWEEN 0 AND 6),lease_epoch bigint NOT NULL DEFAULT 0 CHECK(lease_epoch>=0),lease_until timestamptz NULL,inform_id uuid NULL,CHECK(state<>'Delivered' OR inform_id IS NOT NULL),UNIQUE(file_job_id,source_row_id,output_key),FOREIGN KEY(source_row_id,file_job_id) REFERENCES mapping.source_row_refs(id,file_job_id) ON DELETE RESTRICT);
CREATE FUNCTION mapping.guard_attempt_counter() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.attempts_reserved<OLD.attempts_reserved THEN RAISE EXCEPTION 'attempt counter cannot decrease' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;
CREATE TRIGGER monotonic_attempts BEFORE UPDATE ON mapping.delivery_jobs FOR EACH ROW EXECUTE FUNCTION mapping.guard_attempt_counter();
CREATE TABLE mapping.delivery_attempts(id uuid PRIMARY KEY,delivery_job_id uuid NOT NULL REFERENCES mapping.delivery_jobs ON DELETE RESTRICT,attempt_number smallint NOT NULL CHECK(attempt_number BETWEEN 1 AND 6),UNIQUE(delivery_job_id,attempt_number));
CREATE TABLE mapping.delivery_controls(scope text PRIMARY KEY CHECK(scope='mapping'),max_parallel integer NOT NULL DEFAULT 1 CHECK(max_parallel>0));
CREATE TABLE mapping.outbox(id uuid PRIMARY KEY,row_job_id uuid NOT NULL REFERENCES mapping.row_jobs ON DELETE RESTRICT);
DO $$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['source_definition_versions','mapping_config_versions','source_row_refs','src_example_rows','file_content_claims'] LOOP EXECUTE format('CREATE TRIGGER immutable_rows BEFORE UPDATE OR DELETE ON mapping.%I FOR EACH ROW EXECUTE FUNCTION mapping.reject_rewrite()',t); EXECUTE format('CREATE TRIGGER immutable_truncate BEFORE TRUNCATE ON mapping.%I FOR EACH STATEMENT EXECUTE FUNCTION mapping.reject_rewrite()',t); END LOOP; END $$;
INSERT INTO mapping.mapping_configs VALUES(mapping.uid(1)),(mapping.uid(2));
INSERT INTO mapping.source_definitions VALUES(mapping.uid(11),mapping.uid(1)),(mapping.uid(12),mapping.uid(2));
INSERT INTO mapping.source_definition_versions VALUES(mapping.uid(21),mapping.uid(11),mapping.uid(1),1,'["field_a","field_b"]'),(mapping.uid(22),mapping.uid(12),mapping.uid(2),1,'["field_x"]');
INSERT INTO mapping.mapping_config_versions VALUES(mapping.uid(51),mapping.uid(1),mapping.uid(21),1),(mapping.uid(52),mapping.uid(1),mapping.uid(21),2),(mapping.uid(53),mapping.uid(2),mapping.uid(22),1);
INSERT INTO mapping.file_jobs(id,config_id,config_version_id,source_definition_version_id,content_hash,created_at) VALUES(mapping.uid(61),mapping.uid(1),mapping.uid(51),mapping.uid(21),decode(repeat('aa',32),'hex'),'2026-11-16T03:00:00Z'),(mapping.uid(62),mapping.uid(1),mapping.uid(52),mapping.uid(21),decode(repeat('bb',32),'hex'),'2026-11-16T03:10:00Z'),(mapping.uid(63),mapping.uid(1),mapping.uid(52),mapping.uid(21),decode(repeat('aa',32),'hex'),'2026-11-16T03:20:00Z'),(mapping.uid(64),mapping.uid(2),mapping.uid(53),mapping.uid(22),decode(repeat('aa',32),'hex'),'2026-11-16T03:30:00Z');
INSERT INTO mapping.file_content_claims VALUES(mapping.uid(1),decode(repeat('aa',32),'hex'),mapping.uid(61)),(mapping.uid(1),decode(repeat('bb',32),'hex'),mapping.uid(62)),(mapping.uid(2),decode(repeat('aa',32),'hex'),mapping.uid(64));
SELECT mapping.expect_error('same hash under renamed occurrence is duplicate','INSERT INTO mapping.file_content_claims VALUES(mapping.uid(1),decode(repeat(''aa'',32),''hex''),mapping.uid(61))','23505');
SELECT mapping.expect_error('same hash across config versions is duplicate','INSERT INTO mapping.file_content_claims VALUES(mapping.uid(1),decode(repeat(''aa'',32),''hex''),mapping.uid(63))','23505');
SELECT mapping.assert_ok((SELECT count(*)=2 FROM mapping.file_content_claims WHERE config_id=mapping.uid(1)),'changed bytes accepted under same config');
SELECT mapping.assert_ok((SELECT count(*)=2 FROM mapping.file_content_claims WHERE content_hash=decode(repeat('aa',32),'hex')),'same bytes allowed under another config');
SELECT mapping.expect_error('config version cannot use other config source','INSERT INTO mapping.mapping_config_versions VALUES(mapping.uid(54),mapping.uid(1),mapping.uid(22),3)','23503');
SELECT mapping.expect_error('file pin cannot mismatch source version','INSERT INTO mapping.file_jobs(id,config_id,config_version_id,source_definition_version_id,content_hash,created_at) VALUES(mapping.uid(65),mapping.uid(1),mapping.uid(51),mapping.uid(22),decode(repeat(''cc'',32),''hex''),now())','23503');
INSERT INTO mapping.source_row_refs VALUES(mapping.uid(71),mapping.uid(61),mapping.uid(11),mapping.uid(21),1,2),(mapping.uid(72),mapping.uid(62),mapping.uid(11),mapping.uid(21),1,2),(mapping.uid(73),mapping.uid(64),mapping.uid(12),mapping.uid(22),1,2);
INSERT INTO mapping.src_example_rows VALUES(mapping.uid(71),mapping.uid(61),mapping.uid(11),'12.50',''),(mapping.uid(72),mapping.uid(62),mapping.uid(11),'-293','');
SELECT mapping.expect_error('source cannot change pinned definition','INSERT INTO mapping.source_row_refs VALUES(mapping.uid(74),mapping.uid(61),mapping.uid(12),mapping.uid(22),2,3)','23503');
SELECT mapping.expect_error('row number unique within file','INSERT INTO mapping.source_row_refs VALUES(mapping.uid(74),mapping.uid(61),mapping.uid(11),mapping.uid(21),1,3)','23505');
SELECT mapping.expect_error('physical source cannot use wrong file','INSERT INTO mapping.src_example_rows VALUES(mapping.uid(73),mapping.uid(61),mapping.uid(11),''x'',''y'')','23503');
SELECT mapping.expect_error('physical source cannot use wrong definition','INSERT INTO mapping.src_example_rows VALUES(mapping.uid(73),mapping.uid(64),mapping.uid(12),''x'',''y'')','23514');
SELECT mapping.expect_error('raw source update blocked','UPDATE mapping.src_example_rows SET field_a=''changed'' WHERE id=mapping.uid(71)','23514');
SELECT mapping.expect_error('raw source delete blocked','DELETE FROM mapping.src_example_rows WHERE id=mapping.uid(71)','23514');
SELECT mapping.expect_error('raw source truncate blocked','TRUNCATE mapping.src_example_rows','23514');
SELECT mapping.expect_error('pinned definition rewrite blocked','UPDATE mapping.source_definition_versions SET fields=''[]'' WHERE id=mapping.uid(21)','23514');
SELECT mapping.expect_error('file CreatedAt cannot change','UPDATE mapping.file_jobs SET created_at=now() WHERE id=mapping.uid(61)','23514');
ALTER TABLE mapping.src_example_rows ADD COLUMN field_c text NULL;
INSERT INTO mapping.source_definition_versions VALUES(mapping.uid(23),mapping.uid(11),mapping.uid(1),2,'["field_a","field_b","field_c"]');
SELECT mapping.assert_ok((SELECT field_a='12.50' AND field_c IS NULL FROM mapping.src_example_rows WHERE id=mapping.uid(71)) AND (SELECT fields='["field_a","field_b"]'::jsonb FROM mapping.source_definition_versions WHERE id=mapping.uid(21)),'adding nullable column preserves raw and old definition');
INSERT INTO mapping.row_jobs VALUES(mapping.uid(81),mapping.uid(71),mapping.uid(61)),(mapping.uid(82),mapping.uid(72),mapping.uid(62)),(mapping.uid(83),mapping.uid(73),mapping.uid(64));
INSERT INTO mapping.delivery_jobs(id,source_row_id,file_job_id,output_key) VALUES(mapping.uid(91),mapping.uid(71),mapping.uid(61),'default'),(mapping.uid(92),mapping.uid(72),mapping.uid(62),'default'),(mapping.uid(93),mapping.uid(73),mapping.uid(64),'default');
SELECT mapping.expect_error('delivery cannot bind source from other file','INSERT INTO mapping.delivery_jobs(id,source_row_id,file_job_id,output_key) VALUES(mapping.uid(94),mapping.uid(71),mapping.uid(62),''default'')','23503');
SELECT mapping.expect_error('one logical output per file source key','INSERT INTO mapping.delivery_jobs(id,source_row_id,file_job_id,output_key) VALUES(mapping.uid(94),mapping.uid(71),mapping.uid(61),''default'')','23505');
INSERT INTO mapping.delivery_attempts SELECT mapping.uid(100+n),mapping.uid(93),n FROM generate_series(1,6) n;
UPDATE mapping.delivery_jobs SET attempts_reserved=6,state='Failed' WHERE id=mapping.uid(93);
SELECT mapping.expect_error('attempt seven blocked','INSERT INTO mapping.delivery_attempts VALUES(mapping.uid(107),mapping.uid(93),7)','23514');
SELECT mapping.expect_error('attempt number duplicate blocked','INSERT INTO mapping.delivery_attempts VALUES(mapping.uid(108),mapping.uid(93),6)','23505');
SELECT mapping.expect_error('attempt counter reset blocked','UPDATE mapping.delivery_jobs SET attempts_reserved=0 WHERE id=mapping.uid(93)','23514');
SELECT mapping.expect_error('attempt counter above six blocked','UPDATE mapping.delivery_jobs SET attempts_reserved=7 WHERE id=mapping.uid(93)','23514');
SELECT mapping.expect_error('Delivered requires inform ID','UPDATE mapping.delivery_jobs SET state=''Delivered'' WHERE id=mapping.uid(91)','23514');
DO $$ BEGIN BEGIN INSERT INTO mapping.source_row_refs VALUES(mapping.uid(79),mapping.uid(61),mapping.uid(11),mapping.uid(21),2,3); INSERT INTO mapping.src_example_rows(id,file_job_id,source_definition_id,field_a) VALUES(mapping.uid(79),mapping.uid(61),mapping.uid(11),'rollback'); INSERT INTO mapping.row_jobs VALUES(mapping.uid(89),mapping.uid(79),mapping.uid(61)); INSERT INTO mapping.outbox VALUES(mapping.uid(99),mapping.uid(89)); UPDATE mapping.file_jobs SET committed_count=2 WHERE id=mapping.uid(61); RAISE EXCEPTION 'injected crash'; EXCEPTION WHEN raise_exception THEN NULL; END; PERFORM mapping.assert_ok(NOT EXISTS(SELECT 1 FROM mapping.source_row_refs WHERE id=mapping.uid(79)) AND NOT EXISTS(SELECT 1 FROM mapping.src_example_rows WHERE id=mapping.uid(79)) AND NOT EXISTS(SELECT 1 FROM mapping.row_jobs WHERE id=mapping.uid(89)) AND NOT EXISTS(SELECT 1 FROM mapping.outbox WHERE id=mapping.uid(99)) AND (SELECT committed_count=0 FROM mapping.file_jobs WHERE id=mapping.uid(61)),'registry raw job outbox checkpoint roll back atomically'); END $$;
INSERT INTO mapping.delivery_controls VALUES('mapping',1);
SELECT mapping.assert_ok((SELECT count(*)=1 FROM pg_namespace WHERE nspname='mapping') AND NOT EXISTS(SELECT 1 FROM pg_tables WHERE schemaname='public'),'proof creates no public tables');
SELECT version();
```

SHA-256 ของ SQL block: `5119dffafacae090f45845e88e2b39caad66b41c8218d270b7b39ef91717514c`.

## 4. วิธีตรวจ concurrency และ fencing

ทั้ง 3 คู่เปิด transaction พร้อมกันด้วย role `insurance_user`, ถือ lock อย่างน้อย 1 วินาที แล้ว commit; ทุก process จบ exit 0:

1. **Budget:** sessions เลือก delivery คนละรายการ; lock control row ด้วย `SELECT ... FOR UPDATE` เป็น statement แรก จากนั้น statement ใหม่ตรวจ Sending count < max_parallel แล้ว reserve/update/insert attempt; ได้ Sending รวม 1 และ attempt ใหม่รวม 1
2. **Hash:** สอง File Jobs ของ config/hash เดียวกัน `INSERT ... ON CONFLICT DO NOTHING RETURNING canonical_file_job_id`; มี canonical claim รวม 1
3. **Last attempt:** delivery มี counter 5/history 5; ทั้งสอง sessions ทำ conditional `UPDATE ... WHERE attempts_reserved < 6` และ insert attempt จาก `RETURNING` ใน transaction เดียว; สุดท้าย counter/history/max attempt เท่ากับ 6
4. **Fencing:** เพิ่ม lease epoch ของ job ที่ชนะเป็น 2 แล้วพยายาม completion ด้วย epoch 1; affected rows = 0 และ state/epoch ใหม่คงอยู่

SQL และ command outputs ชั่วคราวเก็บใน TEMP ของ shell ที่ตรวจ; SQL constraints ฉบับที่ใช้จริงฝังไว้ใน §3 เพื่ออ่านและตรวจซ้ำได้ แม้ลบฐานทดสอบแล้ว

## 5. Cleanup

ตรวจชื่อ prefix/marker แล้วลบฐานชั่วคราวสำเร็จ; ตรวจซ้ำพบฐานชื่อนี้ 0 รายการ ไม่ลบ container/volume หรือฐาน `InsuranceValidation`