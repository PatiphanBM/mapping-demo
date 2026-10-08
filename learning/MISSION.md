# Mission: ภาพรวม Queue และ Worker ใน mapping-demo

## Why
mapping-demo ถูกสร้างตาม learning plan จนเกือบครบแล้ว แต่ส่วนที่ทำงานผ่าน queue และ worker ยังเป็นกล่องดำ
เป้าหมายคือเห็นภาพรวมว่า queue กับ worker ทำงานร่วมกันอย่างไรในโปรเจกต์นี้ และทำไมถึงออกแบบแบบนี้ โดยไม่ต้องลงลึกถึงกลไกภายในของ Kafka

## Success looks like
- วาด flow ตั้งแต่วางไฟล์ใน `input/` จนได้ Normalized row ได้เอง และชี้ได้ว่าส่วนไหนคือ producer, queue, worker
- อธิบายได้ว่าทำไมงาน import/normalize ถูกส่งต่อให้ Worker แทนที่ API จะทำเอง
- เล่าได้ว่าระบบทำอะไรเมื่อ Worker ปิดอยู่หรืองานล้มเหลว (รอ, retry, reprocess) ในระดับแนวคิด
- อธิบายได้ว่าการเพิ่มจำนวน worker ช่วยให้ระบบทำงานเร็วขึ้นได้อย่างไร

## Constraints
- เรียนเป็นภาษาไทย ใช้ศัพท์เทคนิคภาษาอังกฤษตามโค้ด
- **ระดับภาพรวม**: ใช้แผนภาพและตัวอย่างจากโปรเจกต์ โค้ดใช้เท่าที่ช่วยชี้ว่าสิ่งนั้นอยู่ตรงไหน
- บทสั้น ทำจบได้ในรอบเดียว

## Out of scope
- กลไกภายในของ Kafka ในระดับลึก (offset/commit semantics, partition assignment, delivery guarantees)
- การดูแล Kafka ระดับ production, Kafka Streams/Connect
- message broker อื่น ยกเว้นใช้เทียบให้เห็นภาพ
