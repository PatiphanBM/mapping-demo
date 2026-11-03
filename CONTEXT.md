# Mapping Demo

คำศัพท์สำหรับกระบวนการนำข้อมูลจากไฟล์เข้าตารางต้นทาง แล้วแปลงเป็นข้อมูลปลายทาง

## Language

**Source Table**:
ตารางต้นทางที่เก็บข้อมูลจากไฟล์ตามการจับคู่ field โดย field ข้อมูลจากไฟล์ทุกตัวเป็นข้อความ ก่อนแปลงข้อมูลในขั้นถัดไป
_Avoid_: Normalized Table, ตารางปลายทาง

**Normalized Table**:
ตารางปลายทางที่รับข้อมูลจาก Source Table หลังใช้กฎแปลงชนิดข้อมูล ตัดช่องว่าง และตรวจข้อมูลที่จำเป็น
_Avoid_: Source Table, nomalized_table

**Input Layout**:
ข้อกำหนดแบบมีรุ่นที่อธิบายการอ่าน input record และนำค่าดิบไปวางใน field ของ Source Table โดยเป็น Delimited หรือ Fixed Width
_Avoid_: File-to-Source Mapping, parser config

**Fixed-width Slice**:
ช่วงข้อความของ fixed-width record ที่ระบุด้วยตำแหน่งเริ่มต้นแบบ 1-based และความยาว เพื่อนำค่าดิบไปยัง Source field
_Avoid_: Transformation Substring, byte offset

**Transformation Program**:
กฎแบบ declarative ที่มีรุ่นสำหรับแปลง Source row หนึ่งแถวเป็น Normalized outputs ตั้งแต่ศูนย์ถึงหลายรายการ
_Avoid_: Source-to-Normalized Mapping, mapping rules

**Output Branch**:
ทางเลือกผลลัพธ์ที่มี Output Key คงที่ พร้อมเงื่อนไข emit และชุด field assignments ของตัวเอง
_Avoid_: Normalized row type

**Named Transform**:
การแปลงค่าที่ระบบเป็นเจ้าของและระบุรุ่นชัดเจน ซึ่ง Transformation Program เรียกด้วยชื่อ capability
_Avoid_: custom function, raw code

**Named Lookup**:
คำถามต่อข้อมูลอ้างอิงที่ระบบเป็นเจ้าของและระบุรุ่นชัดเจน ซึ่ง predicate เรียกโดยไม่ฝัง query
_Avoid_: raw SQL, lookup query

**Mapping Config**:
ชุดข้อกำหนดที่ผูก folder รับไฟล์กับ Source Table โดย Config Version แต่ละรุ่นเป็นเจ้าของ Input Layout, Transformation Program และ Destination Definition ที่ immutable; Normalized Table จำเป็นเฉพาะ `LegacyTable`

**Row Error**:
รายละเอียดข้อผิดพลาดของแถวที่ไม่ผ่านการแปลงหรือตรวจสอบข้อมูล ประกอบด้วยเลขแถว field และสาเหตุ โดยแถวที่ผ่านยังบันทึกลงตารางปลายทางได้

**Archive Folder**:
โฟลเดอร์เก็บไฟล์ต้นฉบับที่ย้ายออกจากโฟลเดอร์รับไฟล์หลังนำเข้า Source Table ครบ โดยงาน normalize อาจยังไม่เสร็จ

**File Import Job**:
งานนำข้อมูลจากไฟล์หนึ่งไฟล์เข้า Source Table โดยแต่ละแถวที่บันทึกแล้วสามารถเริ่มขั้น normalize ได้ก่อนนำเข้าทั้งไฟล์ครบ
_Avoid_: Row Normalization Job

**Row Normalization Job**:
งานแปลงและตรวจสอบข้อมูลหนึ่งแถวจาก Source Table เพื่อบันทึกลง Normalized Table โดยอ้างอิงงานไฟล์ที่เป็นต้นกำเนิด
_Avoid_: File Import Job

**Row Number**:
ลำดับ record ที่ Input Layout กำหนด action เป็น `Import` เริ่มที่ 1 โดยไม่นับ record ที่ `Ignore` เช่น header/trailer และใช้เลขเดิมตลอดทั้งสองขั้น; เลขบรรทัดจริงของไฟล์เก็บแยกเป็น Physical Line Number
_Avoid_: เลขบรรทัดจริงของไฟล์

**Physical Line Number**:
ลำดับ physical record ในไฟล์ เริ่มที่ 1 และนับทั้ง record ที่ `Import` และ `Ignore` ใช้ระบุตำแหน่ง input validation error โดยไม่แทนที่ Row Number
_Avoid_: Row Number

**Config Version**:
รุ่น immutable ของ Mapping Config ที่เป็นเจ้าของ Input Layout และ Transformation Program โดยตรึงไว้ตั้งแต่รับไฟล์ การ reprocess อาจเลือกใช้รุ่นใหม่ได้

**Retry**:
การทำงานที่ไม่สำเร็จซ้ำด้วยข้อมูลและ config version เดิม โดยไม่เพิ่มข้อมูลผลลัพธ์ซ้ำ
_Avoid_: Reprocess

**Reprocess**:
การประมวลผล Source ใหม่โดยเลือก config version ได้ เพื่อให้ข้อมูลผ่านกฎ normalization ของรุ่นที่เลือก
_Avoid_: Retry

**Destination Definition**:
ส่วน immutable ของ Config Version ที่เลือกอย่างใดอย่างหนึ่งระหว่าง `LegacyTable`, `ApiContract` และ `JsonFile`; งานที่ pin แล้วห้ามเปลี่ยน destination กลางทางและห้ามส่ง output เดียวไปหลาย destination

**JsonFile Destination**:
ปลายทางระดับ File Job ที่รวม Mapped Documents ตามลำดับ `rowNumber/outputOrdinal` เป็น JSON array หนึ่ง artifact ผ่าน server-owned File Destination Profile; ไม่เขียน Normalized Table และไม่สร้าง API Delivery Job

**File Destination Profile**:
profile ที่ server เป็นเจ้าของสำหรับ Output Root, quota, minimum free space, encryption policy, contract catalog และ payload bindings; browser เห็นเฉพาะ logical ID กับ safe summary ไม่เห็น absolute root

**JSON Export Job**:
งานระดับ File Job ที่ตรึง Config Version, profile, artifact revision, relative path, formatting และ staged payload identity ก่อน finalizer publish; retry ใช้ identity เดิม ส่วน reprocess สร้าง revision ใหม่ที่เชื่อม audit กับ revision ก่อนหน้า

**JSON Artifact**:
ไฟล์ UTF-8 no BOM ที่เป็น JSON array แบบ deterministic และ publish ด้วย create-no-overwrite atomic move; metadata ที่เปิดเผยได้มี relative path, object/byte count, SHA-256 และ timestamps แต่ไม่มี payload body หรือ absolute Output Root

**JSON Finalizer**:
worker ที่ claim JSON Export Job ด้วย lease, ตรวจ latest Row Jobs, stream staged objects ตามลำดับ, คำนวณ hash ระหว่างเขียน และทำให้ final path มองเห็นได้ต่อเมื่อ artifact ครบเท่านั้น

**Contract Snapshot**:
canonical JSON Schema, hash, path catalog และ system payload bindings ที่ตรึงตอนสร้าง `ApiContract` หรือ `JsonFile` Config Version; runtime ห้าม fetch schema ล่าสุดแทน snapshot

**Mapped Document**:
nested typed business document จาก Transformation Program ก่อนเติม System-bound Paths

**Delivery Payload**:
Mapped Document หลังเติม System-bound Paths และผ่าน final contract validation แล้ว; bytes/hash ถูก persist แบบเข้ารหัสเพื่อ retry โดยไม่ rebuild

**Delivery Job**:
งานส่ง mapped output หนึ่งรายการไป ApiContract destination พร้อม immutable payload hash, idempotency key, contract/config/output identity และ lifecycle แยกจาก Row Transformation Job

**Delivery Attempt**:
การเรียก partner endpoint หนึ่งครั้งของ Delivery Job เดิม; เก็บเฉพาะ status, duration, HTTP/correlation metadata และ redacted error ไม่เก็บ response body โดย default

**Business Resend**:
การส่งเอกสารที่ Delivered แล้วซ้ำโดยผู้มีสิทธิ์พร้อมเหตุผล/audit และ idempotency suffix ใหม่; ไม่ใช่ Retry หรือ Reprocess

**Delivery Rollout**:
allowlist ราย Mapping Config ที่มี stage `Shadow`, `Canary` หรือ `Expanded`; ห้าม percentage-route destination ภายใน config เดียว

**Duplicate File**:
ไฟล์ที่มีเนื้อหาเหมือนกับไฟล์ที่รับไว้แล้วภายใต้ Mapping Config เดียวกัน แม้ใช้ชื่อไฟล์ต่างกัน โดยไม่สร้างข้อมูลนำเข้าซ้ำ

**Current Normalized Result**:
ชุดผล normalization ล่าสุดที่ประมวลผลสำเร็จของ Source แถวหนึ่งพร้อม Config Version จาก Row Normalization Job ที่สร้างผล ชุดนี้มีได้ `0..N` outputs ตาม `outputKey`; ผล `Filtered` คือ successful empty set และลบ outputs เดิมทั้งหมด ส่วน reprocess ที่เป็น `Invalid` หรือ `Failed` ไม่เปลี่ยนชุดผลสำเร็จเดิม โดยสถานะการรันล่าสุดยังแยกจาก Current Normalized Result
