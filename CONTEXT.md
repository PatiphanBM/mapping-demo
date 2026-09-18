# Mapping Demo

คำศัพท์สำหรับกระบวนการนำข้อมูลจากไฟล์เข้าตารางต้นทาง แล้วแปลงเป็นข้อมูลปลายทาง

## Language

**Source Table**:
ตารางต้นทางที่เก็บข้อมูลจากไฟล์ตามการจับคู่ field โดย field ข้อมูลจากไฟล์ทุกตัวเป็นข้อความ ก่อนแปลงข้อมูลในขั้นถัดไป
_Avoid_: Normalized Table, ตารางปลายทาง

**Normalized Table**:
ตารางปลายทางที่รับข้อมูลจาก Source Table หลังใช้กฎแปลงชนิดข้อมูล ตัดช่องว่าง และตรวจข้อมูลที่จำเป็น
_Avoid_: Source Table, nomalized_table

**File-to-Source Mapping**:
การจับคู่ field จากไฟล์ข้อมูลกับ field ใน Source Table ตาม configuration

**Source-to-Normalized Mapping**:
การจับคู่ field จาก Source Table กับ field ใน Normalized Table พร้อมกฎแปลงและตรวจสอบข้อมูล

**Mapping Config**:
ชุดข้อกำหนดที่ผูกกับ folder รับไฟล์ และระบุการจับคู่ข้อมูลทั้งจากไฟล์ไป Source Table หนึ่งตาราง และจาก Source Table ไป Normalized Table หนึ่งตาราง

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
ลำดับ record ข้อมูลภายในไฟล์ เริ่มที่ 1 โดยไม่นับ header และใช้เลขเดิมตลอดทั้งสองขั้น
_Avoid_: เลขบรรทัดจริงของไฟล์

**Config Version**:
รุ่นของ Mapping Config ที่ตรึงไว้ตั้งแต่รับไฟล์ โดยงานนำเข้าและงาน normalize ครั้งแรกใช้กติกาจากรุ่นเดียวกัน การ reprocess อาจเลือกใช้รุ่นใหม่ได้

**Retry**:
การทำงานที่ไม่สำเร็จซ้ำด้วยข้อมูลและ config version เดิม โดยไม่เพิ่มข้อมูลผลลัพธ์ซ้ำ
_Avoid_: Reprocess

**Reprocess**:
การประมวลผล Source ใหม่โดยเลือก config version ได้ เพื่อให้ข้อมูลผ่านกฎ normalization ของรุ่นที่เลือก
_Avoid_: Retry

**Duplicate File**:
ไฟล์ที่มีเนื้อหาเหมือนกับไฟล์ที่รับไว้แล้วภายใต้ Mapping Config เดียวกัน แม้ใช้ชื่อไฟล์ต่างกัน โดยไม่สร้างข้อมูลนำเข้าซ้ำ

**Current Normalized Result**:
ผล normalization ที่สำเร็จล่าสุดของ Source แถวหนึ่งพร้อม config version ที่สร้างผลนั้น หาก reprocess ครั้งใหม่ไม่ผ่าน ผลนี้ยังคงอยู่โดยแยกจากสถานะการรันล่าสุด
