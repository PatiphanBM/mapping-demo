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
ชุดข้อกำหนดที่ผูก folder รับไฟล์กับ Source Table และ Normalized Table อย่างละหนึ่งตาราง โดย Config Version แต่ละรุ่นเป็นเจ้าของ Input Layout และ Transformation Program ที่ immutable

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

**Duplicate File**:
ไฟล์ที่มีเนื้อหาเหมือนกับไฟล์ที่รับไว้แล้วภายใต้ Mapping Config เดียวกัน แม้ใช้ชื่อไฟล์ต่างกัน โดยไม่สร้างข้อมูลนำเข้าซ้ำ

**Current Normalized Result**:
ชุดผล normalization ล่าสุดที่ประมวลผลสำเร็จของ Source แถวหนึ่งพร้อม Config Version จาก Row Normalization Job ที่สร้างผล ชุดนี้มีได้ `0..N` outputs ตาม `outputKey`; ผล `Filtered` คือ successful empty set และลบ outputs เดิมทั้งหมด ส่วน reprocess ที่เป็น `Invalid` หรือ `Failed` ไม่เปลี่ยนชุดผลสำเร็จเดิม โดยสถานะการรันล่าสุดยังแยกจาก Current Normalized Result
