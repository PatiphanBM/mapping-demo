---
title: mapping phase 1 — แผนและความคืบหน้า (ไฟล์กลาง Dev 1 + Dev 2)
status: draft
last_updated: 2026-11-25
---

# mapping phase 1 — แผนและความคืบหน้า

> **ไฟล์กลาง: Dev 1 และ Dev 2 เขียนได้ทั้งคู่** — Dev 2 วางแผนงาน mapping เองแล้วเขียนเป็น bullet ในไฟล์นี้ และอัปเดตว่าทำอะไรไปแล้ว · Dev 1 ใช้ไฟล์นี้ตามว่าส่วนไหนเสร็จแล้ว
>
> | ส่วน | ใครเขียน |
> |---|---|
> | §1 อัปเดตล่าสุด | **Dev 2** (เพิ่มรายการใหม่ไว้บนสุด) |
> | §2 จุดสังเกต | วันที่ = Dev 1 (ตามแผนรวม) · สถานะ = Dev 2 |
> | §3 งาน | **Dev 2** วางแผน / เพิ่ม bullet / ติ๊กเมื่อเสร็จ · Dev 1 ใส่หมายเหตุใต้ bullet ได้ (ขึ้นต้น `Dev 1:`) |
> | §4 การตัดสินใจของ mapping (`M-xx`) | **Dev 2** |
> | §5 คำถาม / ข้อเสนอ | **ทั้งคู่** — ตอบใต้ข้อนั้น |
>
> **กันชนกัน:** pull ก่อนแก้ทุกครั้ง · แก้เสร็จ commit + push ทันที (`docs(plan): ...`) · ห้ามลบหรือเขียนทับข้อความของอีกคน — ถ้าไม่เห็นด้วยให้ตอบใต้ข้อนั้น

## 1. อัปเดตล่าสุด

> Dev 2 เพิ่มรายการใหม่**ไว้บนสุด** ทุกวันที่ทำงาน หรือทุกครั้งที่ปิดงาน · รูปแบบ:
>
> ```markdown
> ### YYYY-MM-DD
> - เสร็จ: M1.1, M1.2 (สั้น ๆ ว่าได้อะไร)
> - กำลังทำ: M1.3
> - ติด / รอ: — (หรือ ติดอะไร รอใคร)
> - ต่อไป: M2.1
> ```

### 2026-11-25 — commit/push การ์ดจับคู่และรวม array (M-46–M-47)
- เสร็จ: ตามคำสั่งผู้ใช้ commit และ push Web ขึ้น `origin/develop` แล้ว 2 commits: **`34414e5`** — `feat(mapping): group array mappings in source destination cards` (โค้ด/tour/tests 8 ไฟล์) และ **`0c2bc9e`** — `style(mapping): style grouped destination mapping cards` (เฉพาะ `src/styles.scss` ตามกฎไฟล์ร่วม)
- เสร็จ: บันทึกเลข commit และสรุปพฤติกรรมรุ่นสุดท้ายในแผนกับ `source-first-mapping-ui-design.md`; รวม array เดียวในการ์ดเดียว คง Index/field/เงื่อนไขแยกรายการ ใช้ source ของแถวใน expression ลูก และถอดปุ่ม/ข้อความซ้ำตามคำยืนยันทั้งหมด
- หลักฐาน source รุ่นที่ commit: `npm run build` ผ่าน; `npm run test:ci` **141/141 ใน 13 suites**; `git diff --check` ผ่าน; ไม่มีการแก้ source หลัง checks ก่อน commit; หลักฐาน browser QA ของ UI ระหว่างพัฒนาดู §1/M-46/M-47 (600/1280/1440 px, grouping/draft/conditions/ยืนยันและยกเลิก)
- ขอบเขต: commit/push เฉพาะ Web และเอกสาร Mapping; repo service Mapping สะอาดที่ `93be537` จึงไม่มี backend commit และไม่อ้างผล external Informs E2E ใหม่; คง dev server ที่ผู้ใช้ใช้งาน
- กำลังทำ: —
- ติด / รอ: — สำหรับงาน UI ชุดนี้; M5.3 ทั้งข้อและ dependency อื่นยังคงสถานะเดิม
- ต่อไป: ผู้ใช้ดึง branch `develop` เพื่อใช้งาน UI รุ่นนี้; commit โค้ดและ styles แยกกันเพื่อ review/ย้อนกลับตามขอบเขตได้

### 2026-11-25 — เอาข้อความมีการจับคู่แล้วออกจากตัวเลือก field
- เสร็จ: ตามคำยืนยันผู้ใช้ แก้เฉพาะ `src/app/features/mapping/source-mapping-editor.ts` ลบข้อความต่อท้าย ` · มีการจับคู่แล้ว` จากรายการปลายทาง/ตัวเลือกปลายทางในการ์ด/ตัวเลือก field ของ array ทั้ง 3 จุด; แสดง path เดิมโดยไม่เปลี่ยน value หรือ assignments
- หลักฐาน: `npm run build` และ `git diff --check` ผ่าน; ตรวจ source ของหน้าต่างแล้วไม่มีข้อความต่อท้ายนี้เหลือ; Web `localhost:4200/mapping/new` ตอบ **HTTP 200** และคง server ที่ผู้ใช้ใช้งาน
- ขอบเขตการตรวจ: เปลี่ยนข้อความ label เท่านั้น ไม่รัน unit tests หรือ browser QA ซ้ำรอบนี้; ผล **141/141** ในรายการก่อนหน้าเป็นของชุดรวมการ์ด ไม่ใช่ผลรันใหม่
- กำลังทำ: —
- ติด / รอ: — สำหรับงานนี้; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ Reload และตรวจข้อความในช่อง field

### 2026-11-25 — รวมรายการของ array เดียวกันในการ์ดเดียว (M-47)
- เสร็จ: ตามคำยืนยันผู้ใช้และ [แบบรวมการ์ด](source-first-mapping-ui-design.md) เพิ่มรายการของ array เดียวกันภายในการ์ดเดิม คนละ array/concrete parent แยกการ์ด; เปิด assignments เดิมแล้วรวมหลาย Index/field โดยไม่ทำ expressions สูญหาย
- เสร็จ: รายการย่อยมี Index/field และเงื่อนไขส่งแยก index; ถ้าหลาย field อยู่ index เดียวแสดงเงื่อนไขส่งครั้งเดียว; เลือก array ซ้ำรวมเข้าการ์ดเดิม เปลี่ยนกลุ่ม/ลบรายการหรือการ์ดคง source อื่น และยืนยัน/ยกเลิกผ่าน draft เดิม
- ไฟล์ Web รอบนี้: `features/mapping/source-mapping-editor.ts`, `source-mapping-editor.html`, `source-mapping-model.ts`, `source-mapping-model.spec.ts` และเฉพาะ Mapping ใน `src/styles.scss`; อัปเดตแบบ UI/แผนใน repo เอกสาร
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **141/141 ใน 13 suites** รวม tests ใหม่ 5 เคส; `git diff --check` ผ่าน; Edge QA array เดียว 3 indices/ต่าง array/เลือกซ้ำ/หลาย field-index/เงื่อนไข/parent ซ้อน/เปิดค่าเดิม/เปลี่ยนกลุ่ม/ลบ/ยืนยันและยกเลิกผ่าน ไม่มี runtime exception หรือคำขอเขียน Mapping API
- หลักฐาน layout: ตรวจภาพแล้วหลายรายการอยู่ใน card เดียว; จอ 1440/1280 px modal 720 px, จอ 600 px modal 570 px ไม่มี horizontal overflow และ footer อยู่ใน viewport; dev server เดิม PID 32656 ยังทำงาน ไม่หยุดหลัง QA
- กำลังทำ: —
- ติด / รอ: — สำหรับงานนี้; ยังไม่ commit/push และไม่ปิด M5.3 ทั้งข้อ
- ต่อไป: ผู้ใช้ Reload/ตรวจการเพิ่มรายการ; รอบนี้ตรวจ frontend ด้วยข้อมูลสมมติ ไม่ใช่ external delivery E2E

### 2026-11-25 — เตรียมรวมรายการของ array เดียวกันในการ์ดเดียว
- เสร็จ: ตรวจ `addCardItem` แล้วพบว่าการเพิ่มรายการสร้าง TargetCard ใหม่ทุกครั้งตามภาพ; เพิ่ม [ข้อกำหนดจัดกลุ่มการ์ด](source-first-mapping-ui-design.md) ให้ array เดียวกันรวมรายการย่อยใน card เดิม คนละ array/concrete parent แยก card และเปิดค่าที่บันทึกไว้เดิมแล้วรวมได้โดยไม่เสีย assignments/เงื่อนไข
- ตัวอย่างสมมติ: `inform.beneficiaries[]` มี Index 1/2 ใน card เดียว และ `inform.drivers[]` Index 1 อีก card → รวม 2 card; คง source `no` และ concrete paths เดิม
- กำลังทำ: เตรียม source Web 5 ไฟล์ตามแบบ: `source-mapping-editor.ts`/`.html`, `source-mapping-model.ts`/`.spec.ts` และเฉพาะ Mapping ใน `src/styles.scss`
- ติด / รอ: คำยืนยันรายการ source รอบนี้ตาม AGENTS.md; ยังไม่แก้ source หรือรัน build/tests ของการรวมการ์ด และยังไม่ commit/push
- ต่อไป: หลังยืนยัน implement และตรวจ array เดียว/คนละ array/ข้อมูลเดิม/เงื่อนไข/ยืนยันและยกเลิก โดยไม่หยุด dev server ที่ผู้ใช้กำลังใช้งาน

### 2026-11-25 — เปิด Web dev server กลับหลัง localhost:4200 connection refused
- เสร็จ: ตรวจ URL เดิมของผู้ใช้แล้วพบ `localhost:4200` connection refused และไม่มี listener ที่พอร์ต 4200; สาเหตุคือ dev server ที่ใช้ตรวจ UI ถูกหยุดหลังทดสอบ เปิด Angular dev server แบบ background กลับที่ `http://localhost:4200/` และคง process ไว้ให้ผู้ใช้ใช้งานต่อ
- หลักฐาน: URL `/mapping/new?config=01a20767-7148-781e-a651-c1f57eca2b44` เปลี่ยนจาก connection refused เป็น **HTTP 200** และมี Angular app root; dev server compile ผ่าน เปิด watch mode และฟังพอร์ต 4200 แล้ว; Mapping API `/health/ready` ตอบ **HTTP 200**
- ขอบเขต: แก้ที่การรัน process ไม่แก้ source/config/DB และไม่รัน build/unit tests ซ้ำ; HTTP 200 ยืนยันว่า Web กลับมารับการเชื่อมต่อ ไม่ใช่หลักฐานทดสอบ workflow ของ config นั้นทั้งชุด
- กำลังทำ: —
- ติด / รอ: — สำหรับการเปิด Web; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ Reload หน้าเดิม; คง dev server ที่เปิดกลับไว้ ไม่หยุดเมื่อจบงานนี้

### 2026-11-25 — ลบปุ่มจัดการการจับคู่ด้านบนตาราง
- เสร็จ: ตามคำยืนยันผู้ใช้ ลบปุ่ม `จัดการการจับคู่` ด้านบนส่วนจับคู่ข้อมูลจากต้นทางใน `src/app/features/mapping/mapping-new.html` และลบขั้น tour `automap` ใน `mapping-new-tour.ts`; ปุ่มจับคู่/จัดการในแต่ละแถวยังใช้งานผ่าน handler เดิม
- เสร็จ: แก้ `mapping-new.ts` ให้ค่าเริ่มต้นของจุดคืน focus ใช้ปุ่ม `manage-mapping-arrays` ที่ยังอยู่ และไม่อ้าง `manage-source-mapping` ที่ลบแล้ว; การเปิดจากแถวยังส่ง ID ของปุ่มแถวนั้นเช่นเดิม
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **136/136 ใน 13 suites**; `git diff --check` ผ่าน; ตรวจ source แล้วไม่มี ID ปุ่มที่ลบหรือ anchor `automap` ค้างในสามไฟล์ที่แก้; ไม่รัน browser QA รอบนี้
- กำลังทำ: —
- ติด / รอ: — สำหรับงานนี้; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจหัวข้อจับคู่ข้อมูลจากต้นทางและเปิดจัดการจากแต่ละแถว

### 2026-11-25 — ลบปุ่มส่งทุกแถวจากส่วนจับคู่ข้อมูลจากต้นทาง
- เสร็จ: ตามคำยืนยันผู้ใช้ ลบปุ่ม `ส่งทุกแถว` / `มีเงื่อนไขเลือกแถว` (`manage-mapping-conditions`) จากส่วน `จับคู่ข้อมูลจากต้นทาง` ใน `src/app/features/mapping/mapping-new.html` และลบขั้น tour ที่อ้าง `source-when` ใน `mapping-new-tour.ts`
- หลักฐาน: `npm run build` และ `git diff --check` ผ่าน; ตรวจ source แล้วไม่มี button ID/anchor/ข้อความปุ่มที่ลบเหลือในสองไฟล์นี้; ปุ่มจัดการ array/ตาราง/ตัวกรองเดิมยังอยู่
- ขอบเขต: เปลี่ยน template และข้อความ tour เท่านั้น ไม่ล้างค่าของ `sourceWhen` หรือแก้โปรแกรม/API; รอบนี้ไม่รัน unit tests หรือ browser QA ซ้ำ
- กำลังทำ: —
- ติด / รอ: — สำหรับงานนี้; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจส่วนจับคู่ข้อมูลจากต้นทาง

### 2026-11-25 — เอาส่วนเงื่อนไขการจับคู่ใต้กรอบ array ออก
- เสร็จ: ตามคำยืนยันผู้ใช้ แก้เฉพาะ `src/app/features/mapping/source-mapping-editor.html` ให้การ์ดที่เลือก array ไม่แสดงส่วน `เงื่อนไขการจับคู่` ใต้กรอบ; เหลือ Index/field/checkbox ส่งเมื่อมีเงื่อนไข/เพิ่มรายการตามแบบที่ตกลง ส่วนปลายทางทั่วไปยังมี editor แปลงค่า
- หลักฐาน: `npm run build` และ `git diff --check` ผ่าน; Edge browser QA ด้วยข้อมูลสมมติผ่านทั้งไม่มีส่วนแปลงค่าของ array/Index/field/เงื่อนไขส่ง/เพิ่มรายการ/ยืนยันและยกเลิก/ปลายทางทั่วไปยังแปลงค่าได้/ค่าแปลง array เดิมไม่ถูกล้าง ไม่มี runtime exception หรือคำขอเขียน Mapping API; ตรวจภาพแล้วว่าส่วนด้านล่างหายจริง
- ขอบเขตการตรวจ: รอบนี้เปลี่ยนเงื่อนไขแสดงผลใน template อย่างเดียว ไม่รัน unit tests ซ้ำ; ผล **136/136** ในรายการก่อนหน้าเป็นของชุดแก้ expression ลูก ไม่ใช่ผลรันใหม่รอบนี้
- กำลังทำ: —
- ติด / รอ: — สำหรับงานนี้; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจหน้าต่างกรณี array; เป็นงาน frontend ไม่ใช่ผล external delivery E2E

### 2026-11-25 — เอาช่องเลือก source ซ้ำออกจาก expression ลูกด้วย
- เสร็จ: ตามคำยืนยันผู้ใช้ แก้ `src/app/features/mapping/value-expression-editor.ts` ให้ส่ง source ของแถวลงไปถึง editor ลูกทุกจุด; ส่วนลูกที่ใช้ source โดยตรงไม่แสดงทั้งชนิด `ค่าจากคอลัมน์` และช่องเลือกชื่อ field รวมถึงไม่เหลือ dropdown ว่างแทนที่; case ที่ใช้ source ของแถวไม่ถามคอลัมน์ซ้ำ
- เสร็จ: ค่าเริ่มต้นของรายการ expression ที่เพิ่มใหม่ใช้ source ของแถว; คง expression/ค่าที่ต้องกรอกสำหรับการแปลงและ case ซ้อน; โปรแกรมเดิมที่อ่านคอลัมน์อื่นแสดงชื่อเป็นข้อความและไม่เปลี่ยนข้อมูลโดยเงียบ ส่วน editor ที่ไม่มี source ผูกไว้ยังทำงานแบบเดิม
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **136/136 ใน 13 suites**; `git diff --check` ผ่าน; Edge browser QA ด้วยข้อมูลสมมติผ่านทั้งไม่มี source controls ทุกระดับ/trim ใช้ source ของแถว/ใช้และเปิดใหม่/case ซ้อนและ fallback/concat เพิ่มและลบค่า/ยกเลิก/คง shared expression เดิม/โหมดค่าอื่น ไม่มี runtime exception หรือคำขอเขียน Mapping API; ตรวจภาพแล้วว่าสองช่องในรูปผู้ใช้หายจริง
- กำลังทำ: —
- ติด / รอ: — สำหรับงานนี้; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจหน้าต่างจัดการการจับคู่; เป็นการปรับ frontend ไม่ใช่ผล external delivery E2E

### 2026-11-25 — เอาตัวเลือกค่าจากคอลัมน์ออกจากเงื่อนไขหลัก
- เสร็จ: ตามคำยืนยันผู้ใช้ เอา `ค่าจากคอลัมน์` ออกจากรายการชนิดระดับหลักของเงื่อนไขพิเศษในหน้าต่างจัดการการจับคู่ที่เลือก source แล้ว; ไม่แสดงตัวเลือกคอลัมน์ซ้ำเมื่อใช้ source ของแถวนั้น และยังเก็บ assignment แบบ source เดิมจนกว่าจะเลือกการแปลงค่า
- เสร็จ: case ใหม่ใช้คอลัมน์ที่กดจับคู่แทน field แรก; ล้างชนิดการแปลงกลับไปจับคู่จาก source ของแถว; editor ลูกของ case/การแปลงยังเลือกคอลัมน์ได้ และโหมดค่าคงที่/ข้อมูลไฟล์ยังใช้ตัวเลือกเดิม; ค่าเดิมที่อ่าน source อื่นยังเปิดให้ตรวจ/แก้ได้
- ไฟล์ Web ที่แก้รอบนี้: `src/app/features/mapping/value-expression-editor.ts` และ `source-mapping-editor.html`; เพิ่ม input ระบุ source โดยใช้เฉพาะ editor ระดับหลักของหน้าต่างที่มี source ให้แล้ว
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **136/136 ใน 13 suites**; `git diff --check` ผ่าน; Edge browser QA ด้วยข้อมูลสมมติผ่านทั้งรายการตัวเลือกจริง/จับคู่ตรง/case ใช้ source ที่เลือก/source ใน expression ลูก/ล้างการแปลง/trim/ยืนยันและยกเลิก/โหมดค่าอื่น ไม่มี runtime exception หรือคำขอเขียน Mapping API
- กำลังทำ: —
- ติด / รอ: — สำหรับงานนี้; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจตัวเลือกในหน้าต่างจัดการการจับคู่; เป็นการปรับ frontend ไม่ใช่ผล external delivery E2E

### 2026-11-25 — การ์ดจับคู่และ checkbox เปิดเงื่อนไขพิเศษ (M-46)
- เสร็จ: ตามคำยืนยันผู้ใช้และ [แบบ UI](source-first-mapping-ui-design.md) เปลี่ยนหน้าต่างจากแถวต้นทางเป็นการ์ด มีชื่อ source ที่หัว เอาแท็บออก และเพิ่มปุ่มปลายทางแบบเส้นประ; checkbox เปิด editor ภายในการ์ด รองรับ case ซ้อน และ array Index/field/checkbox ส่งเมื่อมีเงื่อนไข
- เสร็จ: คง draft ทั้งชุด/ใช้/ยกเลิก/Preview/focus; ยุบส่วนพิเศษไม่ล้างค่าเดิม เพิ่มรายการ array ไม่ทำ assignment/เงื่อนไขก่อนหน้าหาย และเลือก target ที่ถูกใช้อยู่แล้วเปิดค่าเดิมให้ตรวจพร้อมข้อความแจ้ง
- ไฟล์ Web: `features/mapping/source-mapping-editor.html`, `source-mapping-editor.ts`, `source-mapping-model.ts`, `source-mapping-model.spec.ts`, `mapping-new-tour.ts` และเฉพาะ Mapping ใน `src/styles.scss`; อัปเดตแบบ UI และแผนใน repo เอกสาร
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **136/136 ใน 13 suites**; `git diff --check` ผ่าน; Edge QA checkbox/case ซ้อน/one-to-many/shared/array/emitWhen/เพิ่มรายการ/ปลายทางถูกใช้แล้ว/ยืนยันและยกเลิก/Escape/focus ผ่าน ไม่มี runtime exception หรือคำขอเขียน Mapping API
- หลักฐาน layout: จอ 1440/1280 px หน้าต่างกว้าง **720 px**, จอ 600 px กว้าง **570 px**, เลื่อนภายในและ footer อยู่ใน viewport ไม่มี horizontal overflow; ตรวจภาพการ์ดปกติ/case ซ้อน/array แล้ว
- กำลังทำ: —
- ติด / รอ: — สำหรับงาน UI นี้; ยังไม่ commit/push และไม่ปิด M5.3 ทั้งข้อ
- ต่อไป: ผู้ใช้ตรวจ UI; หลักฐานรอบนี้เป็น frontend กับข้อมูลสมมติ ไม่ใช่ external delivery E2E และไม่แก้ service/contract

### 2026-11-25 — แบบการ์ดจับคู่และ checkbox เงื่อนไขพิเศษตามภาพ
- เสร็จ: เพิ่ม [แบบปรับตามภาพ](source-first-mapping-ui-design.md) สำหรับการเปิดจากปุ่มจับคู่ของแถว: เอาแท็บออก แสดงชื่อต้นทาง การ์ดปลายทาง และปุ่มเพิ่มปลายทางแบบเส้นประ; checkbox เปิดส่วนเงื่อนไขการจับคู่แบบซ้อน หรือส่วน array ที่มี Index/field และ checkbox ส่งเมื่อมีเงื่อนไข
- หลักฐาน: ตรวจโค้ด UI/ValueExpression/array/conditions เดิมแล้ว; ตัวอย่างในแบบใช้ข้อมูลสมมติและ path จาก schema ทดสอบ ไม่เปลี่ยน contract; ยังไม่ได้แก้ source หรือรัน build/tests ของแบบใหม่นี้
- กำลังทำ: เตรียมรายการ source Web 6 ไฟล์ในแบบเพื่อให้ผู้ใช้ยืนยัน
- ติด / รอ: คำยืนยันก่อนแก้ source ตาม AGENTS.md; ไม่ใช้คำยืนยัน M-45 แทนรอบนี้ และยังไม่ commit/push
- ต่อไป: หลังยืนยัน ปรับการ์ดและส่วนขยาย ทดสอบ draft/checkbox/case ซ้อน/array/ยืนยันและยกเลิก แล้วบันทึกผลจริง

### 2026-11-25 — commit/push งาน UI Mapping (M-42–M-45)
- เสร็จ: ตามคำสั่งผู้ใช้ commit และ push repo Web ขึ้น `origin/develop` แล้ว 2 commits: **`e6349e4`** — `feat(mapping): manage source mappings and destinations in dialogs` (โค้ด/tour/tests 11 ไฟล์) และ **`a85f618`** — `style(mapping): align mapping dialogs and sticky preview layout` (เฉพาะ `src/styles.scss` ตามกฎไฟล์ร่วม)
- เสร็จ: บันทึกเลข commit ในแผนและแบบ UI 4 ไฟล์ ได้แก่ `map-required-ui-design.md`, `destination-table-ui-design.md`, `destination-modal-ui-design.md`, `source-first-mapping-ui-design.md`; คงผลระหว่างทางไว้เป็นประวัติ ส่วนหลักฐานของ source รุ่นสุดท้ายใช้รายการนี้
- หลักฐาน: source ชุดที่ commit ผ่าน `npm run build`, `npm run test:ci` **132/132 ใน 13 suites**, `git diff --check` และ Edge browser QA ที่ 1280/1440 px ตามรายละเอียด M-45; ไม่มีการแก้ source หลังผลตรวจสุดท้ายก่อน commit
- ขอบเขต: commit/push เฉพาะ Web และเอกสาร Mapping; repo service Mapping สะอาดที่ `93be537` จึงไม่มี backend commit ในชุดนี้ และไม่อ้างผล external Informs E2E ใหม่
- กำลังทำ: —
- ติด / รอ: — สำหรับชุด UI นี้; M5.3 ทั้งข้อและ dependency อื่นยังคงสถานะเดิม
- ต่อไป: ผู้ใช้ตรวจ UI จาก branch `develop`; การย้อน UI ทำได้จาก commits แยกข้างต้นโดยไม่เปลี่ยน API/DB

### 2026-11-25 — ตารางจับคู่จากต้นทางและ modal รวม array/เงื่อนไข (M-45)
- เสร็จ: ตามคำยืนยันและ [แบบจับคู่จากต้นทาง](source-first-mapping-ui-design.md) เปลี่ยนตารางเป็น `ข้อมูลต้นทาง | ปลายทางที่จับคู่ | สถานะ | จัดการ` ไม่มีตัวอย่าง; ค้นหาต้นทาง/target กรองยังไม่ใช้/ต้องแก้ไข และเปิดจัดการจากแต่ละแถวได้ พร้อมค่าอื่น/งานซ่อม/ช่องบังคับที่ยังขาด
- เสร็จ: Modal เดียวจัดการหนึ่ง source ไปหลาย target, shared expressions, ค่าคงที่/metadata/advanced, เลือก/เพิ่ม/ลบ array รวมกลุ่มซ้อน และเงื่อนไขแถว/รายการ; ใช้ editor เดิมและ program/API รูปเดิม
- เสร็จ: draft แยกทั้ง assignments/counts/item conditions/sourceWhen ยืนยันทั้งชุดหรือยกเลิกคงของเดิม; เคสเปลี่ยน source หรือลบรายการ array ไม่ทำ assignment อื่นสูญหาย, เงื่อนไขผิดยืนยันไม่ได้; คืน focus เมื่อยกเลิก/Escape และล้าง Preview เฉพาะเมื่อยืนยันค่าที่เปลี่ยน พร้อมคง sticky 60/40
- ไฟล์ Web: สร้าง `features/mapping/source-mapping-model.ts`, `source-mapping-model.spec.ts`, `source-mapping-editor.ts`, `source-mapping-editor.html`; แก้ `mapping-new.ts`, `mapping-new.html`, `mapping-new.spec.ts`, `mapping-new-tour.ts` และ styles เฉพาะ Mapping ใน `src/styles.scss`; ถอด handlers/editor ของตารางปลายทางเดิมที่ไม่มีผู้เรียกหลังเปลี่ยน UI
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **132/132 ใน 13 suites** (12 model tests + 3 root apply/cancel tests ใหม่); `git diff --check` ผ่าน; Edge QA ไม่มี runtime exception หรือคำขอเขียน Mapping API
- หลักฐาน browser: headers ไม่มีตัวอย่าง, one-to-many/shared concat/advanced case/literal/metadata, draft cancellation/focus/Escape, arrays/เงื่อนไขทั้งชุด, ยกเลิกหลังลบ array, auto-map, ซ่อม source ที่ถูกลบ, กันเงื่อนไขผิด, ค้นหาตาม target/ตัวกรอง ผ่าน; 1280/1440 px ยังเป็น 3:2 ไม่มี page overflow, Preview **top 16 px**, modal **1120 × 801 px** และ footer อยู่ใน viewport
- กำลังทำ: —
- ติด / รอ: — สำหรับ UI นี้; ยังไม่ commit/push และไม่ปิด M5.3 ทั้งข้อ
- ต่อไป: ผู้ใช้ตรวจหน้าหลัก/modal ที่ Local :4200; รอบนี้ตรวจ frontend/โปรแกรมกับข้อมูลสมมติ ไม่ใช่ Preview API ด้วยไฟล์จริงหรือ external delivery E2E

### 2026-11-25 — เตรียมตารางจับคู่จากต้นทางและ modal รวม array
- ปรับตามผู้ใช้: ตัดคอลัมน์ `ตัวอย่าง` และไม่แสดงค่าตัวอย่างใน modal จับคู่; ตารางเหลือข้อมูลต้นทาง/ปลายทางที่จับคู่/สถานะ/จัดการ รายการไฟล์ที่เสนอเดิมไม่เปลี่ยน
- เสร็จ: ตรวจโค้ด assignments/expressions/array เดิม และเขียน [แบบจับคู่จากต้นทาง](source-first-mapping-ui-design.md) ตามผู้ใช้ขอ ให้ตารางหลักเริ่มจาก Source fields เปิด modal เลือกปลายทาง/แปลงค่า และจัดการ array/เงื่อนไขจากหน้าต่างเดียว
- กำลังทำ: ออกแบบรองรับหนึ่งต้นทางไปหลายปลายทาง/หลายต้นทางร่วม expression เดียว/ค่าคงที่และข้อมูลไฟล์ โดยไม่ทำโปรแกรมเดิมสูญหาย; draft ครอบคลุม assignments/counts/item conditions/sourceWhen ยืนยันทั้งชุดหรือยกเลิกคงของเดิม
- ติด / รอ: คำยืนยันรายการไฟล์ก่อนแก้ source ตาม AGENTS.md; ยังไม่แก้ source หรือรัน build/tests ของ UI จับคู่รอบนี้
- ต่อไป: หลังยืนยัน implement และตรวจ compatibility กับโปรแกรมเดิม/array/การยกเลิก/Preview sticky; ไม่เปลี่ยน API/กฎธุรกิจและไม่ commit/push จนผู้ใช้สั่ง

### 2026-11-25 — ตารางปลายทาง 5 คอลัมน์แบบ Mapping Profiles
- เสร็จ: ตามคำยืนยันผู้ใช้ แก้ตารางใน modal เป็น `ชื่อปลายทาง | ช่องทาง | endpoint | สถานะ | จัดการ`; ใช้ `card table-card`/`list-name` และระยะห่างมาตรฐานเหมือน Mapping Profiles แสดงรุ่น/จำนวนช่องใต้ชื่อ มีลิงก์ดูรายละเอียด และปุ่มเลือก/แก้ไขแบบแนวนอน
- เสร็จ: เปิด pagination เหมือนตาราง Mapping Profiles พร้อมตัวเลือก 10/20/50; เรียงชื่อ ช่องทาง และ endpoint ได้; สถานะว่าง/ค้นหาไม่พบครอบคลุม 5 คอลัมน์; คง staging/ยืนยัน/reload ของ M-44
- ไฟล์ Web: `features/mapping/mapping-new.html` และเฉพาะ `.dest-table`/`.dest-table-actions` ใน `src/styles.scss`
- หลักฐาน: `npm run build` และ `git diff --check` ผ่าน; Edge headless QA 1280/1440 px ตรวจหัวตาราง 5 ช่อง ข้อมูลแต่ละคอลัมน์ ลิงก์ดู/แก้ไข กรอบ/pagination sorting ค้นหา/empty colspan เลือกไว้ และ reload ผ่าน; ปุ่มเลือก/แก้ไขอยู่แนวเดียวกัน ไม่มี table/page overflow หรือ runtime exception และไม่มีคำขอเขียน Mapping API
- กำลังทำ: —
- ติด / รอ: —; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจหน้าจอ Local :4200; รอบนี้เปลี่ยน template/CSS ไม่รัน unit tests ซ้ำ ผล **117/117** ด้านล่างเป็นหลักฐานของ logic ชุดก่อนหน้า

### 2026-11-25 — จัดการปลายทางใน modal และย่อหน้าหลัก (M-44)
- เสร็จ: ตามคำยืนยันและ [แบบ modal ปลายทาง](destination-modal-ui-design.md) ย้ายการ์ดชนิด/ตารางค้นหา/reload/สร้าง/แก้ไขเข้า PrimeNG Dialog; หน้าหลักเหลือรายละเอียดรุ่นที่ใช้แบบย่อและปุ่ม `จัดการปลายทาง` พร้อม mapping/Preview sticky 60/40 เดิม
- เสร็จ: แยกสิ่งที่เลือกไว้ใน modal จากสิ่งที่ใช้จริง ต้องกด `ใช้ปลายทางนี้` ก่อนเปลี่ยน; ยกเลิก/ปิด/Escape คง contract/profile/ค่าที่จับคู่/Preview เดิม; cache applied profile snapshot เมื่อ reload ได้รายการรุ่นใหม่ เพื่อไม่ทำให้ปลายทางที่ใช้อยู่หายไปก่อนยืนยัน
- เสร็จ: โหลด schema ล้มเหลวแสดง error และลองใหม่ใน modal ได้; ระหว่างยืนยันมี loading/กันกดซ้ำและกันปิดคั่นกลาง; ปิดแล้วคืน focus ให้ปุ่มจัดการปลายทาง; modal/table มี scroll ภายในและ footer ยังเข้าถึงได้
- ไฟล์ Web: `features/mapping/mapping-new.html`, `mapping-new.ts`, `mapping-new.spec.ts`, `mapping-new-tour.ts` และเฉพาะ styles ของ Mapping ใน `src/styles.scss`
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **117/117 ใน 12 suites** (6 tests ใหม่ของ modal); `git diff --check` ผ่าน; Edge QA staging/cancel/reopen/Escape/focus/reload snapshot/error-retry/confirm/busy guard/edit link/latest revision ผ่าน ไม่มี runtime exception หรือคำขอเขียน Mapping API
- หลักฐาน layout: viewport 1440/1280 × 1000 px ยังเป็น 60/40, summary สูง **147 px**, modal กว้าง **1100 px** สูง **826.5 px** footer อยู่ใน viewport; หลังเลื่อน Preview อยู่ **top 16 px** ตามเดิม
- กำลังทำ: —
- ติด / รอ: — สำหรับ UI รอบนี้; ยังไม่ commit/push และไม่ปิด M5.3 ทั้งข้อ
- ต่อไป: ผู้ใช้ตรวจหน้าหลัก/modal ที่ Local :4200; หลักฐานใช้ข้อมูลสมมติใน browser ไม่ใช่ผลแก้ปลายทางจริงหรือ external delivery E2E

### 2026-11-25 — เตรียมย้ายการจัดการปลายทางเข้า modal
- เสร็จ: เขียน [แบบ modal ปลายทาง](destination-modal-ui-design.md) ตามคำขอผู้ใช้และภาพแนบ ให้หน้าหลักเหลือรายละเอียดที่ใช้แบบย่อพร้อมปุ่ม `จัดการปลายทาง`; ย้ายการ์ดชนิด/ตารางค้นหา/reload/ลิงก์สร้างและแก้ไขเข้า modal และคง mapping/Preview sticky 60/40
- กำลังทำ: ออกแบบให้เลือกใน modal แล้วกด `ใช้ปลายทางนี้` ก่อนเปลี่ยนจริง; ยกเลิกคงปลายทาง/การจับคู่/Preview เดิม และ reload ไม่ทำให้ snapshot ที่ใช้อยู่หายไป
- ติด / รอ: คำยืนยันรายการไฟล์ก่อนแก้ source ตาม AGENTS.md; ยังไม่แก้ source หรือรัน build/tests ของ modal รอบนี้
- ต่อไป: หลังยืนยัน implement และตรวจการยกเลิก/ยืนยัน/loading/error/focus กับตารางและ sticky Preview; ไม่ commit/push จนผู้ใช้สั่ง

### 2026-11-25 — ตารางเลือกปลายทางพร้อม reload และ sticky Preview 60/40 (M-43)
- เสร็จ: ตามคำยืนยันผู้ใช้และ [แบบตารางปลายทาง](destination-table-ui-design.md) เปลี่ยน dropdown เป็นตารางใต้ชนิดปลายทาง ค้นหาชื่อ/ช่องทาง/path เรียงชื่อ แบ่งหน้า เลือกใช้ ดูรายละเอียด และเปิดแก้ไขโปรไฟล์ปลายทางที่ `/mapping/contracts/edit?version=...` ในแท็บใหม่; ข้อมูลรุ่นที่เลือกยังแสดงเมื่อค้นหาแล้วแถวนั้นถูกกรองออก
- เสร็จ: มีปุ่ม `โหลดรายการใหม่` ที่หัวตาราง ดึง catalog/profiles โดยไม่ทิ้ง schema/ค่าที่จับคู่; เห็นปลายทางเพิ่มใหม่และกดใช้รุ่นล่าสุดเองหลังแก้ไขได้; คงค่า path เดิมที่ตรงกัน ผูก profile รุ่นใหม่ และล้าง Preview เก่าหลังเลือกสำเร็จ; โหลดล้มเหลวแสดง error และ retry ได้
- เสร็จ: จัดฝั่งตั้งค่า 60% รวมชนิด/ตารางปลายทาง/การจับคู่ ข้าง Preview 40% แบบ sticky พร้อม scroll ภายใน; เปลี่ยนเฉพาะ UI/logic หน้า Mapping ไม่แก้ API, DB หรือเมนูอื่น
- ไฟล์ Web: `features/mapping/mapping-new.html`, `mapping-new.ts`, `mapping-new.spec.ts`, `mapping-new-tour.ts` และ styles เฉพาะ Mapping ใน `src/styles.scss`
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **111/111 ใน 12 suites** (6 tests ใหม่ของงานตาราง); `git diff --check` ผ่าน; Edge QA ค้นหา/เลือก/ลิงก์แก้ไข/reload เพิ่มรายการและคง mapping/error-retry/ใช้ revision ใหม่/pagination ผ่าน ไม่มี runtime exception หรือคำขอเขียน Mapping API
- หลักฐาน layout: จอ 1440 px ฝั่งซ้าย/ขวา **663/442 px**, จอ 1280 px **567/378 px** เป็น 3:2; เลื่อน `.main` 500 px แล้ว Preview อยู่ **top 16 px** และเลื่อน body ภายในได้ ไม่เกิด page overflow
- กำลังทำ: —
- ติด / รอ: — สำหรับ source batch นี้; ยังไม่ commit/push และไม่ปิด M5.3 ทั้งข้อ
- ต่อไป: ผู้ใช้ตรวจหน้า Local :4200; หลักฐานรอบนี้เป็น unit/browser QA กับข้อมูลสมมติ ไม่ใช่การสร้าง/แก้ไขปลายทางจริงหรือ external delivery E2E

### 2026-11-25 — เตรียมตารางเลือกปลายทางและ sticky Preview 60/40
- เสร็จ: ตรวจข้อมูล/route เดิมและเขียน [แบบตารางปลายทาง](destination-table-ui-design.md) ตามผู้ใช้ขอ: ย้าย dropdown เป็นตารางใต้ชนิดปลายทาง ค้นหา/เลือก/ดูรายละเอียด/เปิดแก้ไขโปรไฟล์ปลายทาง และรวมฝั่งตั้งค่า 60% ข้าง Preview sticky 40%
- กำลังทำ: รอเริ่ม source batch หลังยืนยัน; หน้าแก้ไขปลายทางเดิมคือ `/mapping/contracts/edit?version=...` ตาม D-85 เปิดแท็บใหม่และกลับมาโหลดรายการใหม่ได้
- ติด / รอ: คำยืนยันรายการไฟล์ก่อนแก้ source code ตาม AGENTS.md; ยังไม่แก้ source ของคำขอนี้และยังไม่รัน build/tests สำหรับตารางใหม่
- ต่อไป: ยืนยันแบบ/ไฟล์ แล้ว implement และตรวจ build/tests/browser โดยไม่เปลี่ยน API หรือส่งข้อมูลไปปลายทาง

### 2026-11-25 — เปลี่ยนชนิดปลายทางเป็นการ์ดแบบรูปแบบไฟล์
- เสร็จ: ตามคำยืนยันผู้ใช้ แก้ Web `features/mapping/mapping-new.html` ให้ชนิดปลายทางใช้ `choice-grid`/`choice-card` เช่นเดียวกับรูปแบบไฟล์ มีชื่อ คำอธิบาย กรอบ/พื้นสีฟ้าของรายการที่เลือก และข้อความพร้อมไอคอน `ยังไม่เปิดใช้` ของอีก 2 ตัวเลือก; ใช้ native button ที่มี `aria-pressed`/`disabled`
- เสร็จ: ปรับเฉพาะ CSS ของ Mapping ใน `src/styles.scss` ให้การ์ด 3 ใบเรียงในคอลัมน์ซ้ายและตัดคำได้ โดยคงช่องปลายทางไว้คอลัมน์ขวาตามคำขอก่อนหน้า; ถอด CSS ของ segmented buttons ที่ส่วนนี้ไม่ใช้แล้ว
- หลักฐาน: `npm run build` และ `git diff --check` ผ่าน; Edge headless ตรวจที่ความกว้าง 1440/1280 px พบการ์ด 3 ใบอยู่แถวเดียวกัน มีคำอธิบายครบ `ส่งเข้า API` เลือกอยู่ อีก 2 ใบ disabled และกดแล้วค่าไม่เปลี่ยน; layout 2 คอลัมน์ไม่มี page overflow/runtime exception และไม่มีคำขอเขียน Mapping API ระหว่าง QA
- กำลังทำ: —
- ติด / รอ: —; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจหน้าจอ Local :4200; รอบนี้ปรับ template/CSS ไม่รัน unit tests ซ้ำ ผล **105/105** ด้านล่างเป็นหลักฐานของ logic ชุดก่อนหน้า

### 2026-11-25 — จัดชนิดปลายทางและปลายทางเป็น 2 คอลัมน์
- เสร็จ: ตามคำยืนยันผู้ใช้ ปรับเฉพาะ `.dest-grid` ใน Web `src/styles.scss` เป็น `repeat(2, minmax(0, 1fr))` ให้ชนิดปลายทางอยู่ซ้ายและปลายทางอยู่ขวาในแถวเดียวกัน พร้อมแก้ comment ให้ตรง layout
- หลักฐาน: `npm run build` และ `git diff --check` ผ่าน; Edge headless ตรวจจอกว้าง 1440/1280 px ช่องทั้งสองเริ่มที่ y เดียวกันและแบ่งกว้างเท่ากัน ไม่มี page overflow หรือ runtime exception; QA ใช้ข้อมูลสมมติใน browser ไม่มีคำขอเขียน Mapping API
- กำลังทำ: —
- ติด / รอ: —; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจ layout ที่ Local :4200; รอบนี้เปลี่ยนเฉพาะ CSS ไม่รัน unit tests ซ้ำ ผล **105/105** ด้านล่างเป็นหลักฐานของ logic ชุดก่อนหน้า

### 2026-11-25 — ปรับ Map required เป็นตัวเลือกเงื่อนไขสำหรับผู้ใช้ทั่วไป (M-42)
- เสร็จ: ตามรายการไฟล์ที่ผู้ใช้ยืนยัน เปลี่ยนช่อง JSON ของเงื่อนไขแถว/รายการเป็นตัวเลือกคอลัมน์ → วิธีตรวจ → ค่า พร้อมกลุ่มครบทุกข้อ/อย่างน้อยหนึ่งข้อแบบซ้อน, เพิ่ม/ลบเงื่อนไข/กลุ่ม, ข้อความ/ตัวเลข และสรุปประโยค; ปรับหัวข้อ/ข้อความตาราง/tour ตาม [แบบ Map required](map-required-ui-design.md)
- เสร็จ: คงรูปแบบ `sourceWhen`/`emitWhen` และโปรแกรมขั้นสูงเดิมครบถ้วน; JSON ที่นอกแบบฟอร์มยังเปิดแก้ขั้นสูงได้; ตรวจคอลัมน์ที่ถูกลบ/เปลี่ยนชื่อ กลุ่มว่าง ตัวเลขที่ยังไม่กรอก และกัน Preview/ตรวจทานเมื่อเงื่อนไขผิด
- ไฟล์ Web: แก้ `features/mapping/mapping-new.ts`, `mapping-new.html`, `mapping-new-tour.ts`; สร้าง `predicate-editor.ts`, `predicate-editor-model.ts`, `predicate-editor-model.spec.ts`; ฐานก่อนแก้ `73f89ad`/working tree สะอาด ย้อนเฉพาะชุดนี้ได้ตามที่ผู้ใช้ขอ โดยใช้ข้อมูล/API เดิม
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` **105/105 ใน 12 suites** (14 tests ใหม่); `git diff --check` ผ่าน; Edge headless ใช้ข้อมูลสมมติใน browser ตรวจเพิ่ม/ลบและกลุ่มซ้อน, text/number/zero, invalid JSON/Preview guard, สลับ form/JSON คงค่า, renamed field และ item condition ผ่าน ไม่มี runtime exception หรือคำขอเขียน Mapping API
- กำลังทำ: —
- ติด / รอ: — สำหรับงาน UI นี้; ยังไม่ commit/push และยังไม่ปิด M5.3 ทั้งข้อ
- ต่อไป: ผู้ใช้ตรวจหน้า Local :4200; หลักฐานรอบนี้ไม่ใช่ Preview API ด้วยไฟล์จริงหรือ external delivery E2E

### 2026-11-25 — เตรียมปรับ Map required สำหรับผู้ใช้ทั่วไป
- เสร็จ: ตรวจหน้า Web และ predicates ที่ Mapping รองรับ; เขียน [แบบ Map required](map-required-ui-design.md) จากภาพ Rule Editor ที่ผู้ใช้แนบ ให้เลือกคอลัมน์/วิธีตรวจ/ค่า และรวมกลุ่มครบทุกข้อหรืออย่างน้อยหนึ่งข้อ โดยใช้ `sourceWhen`/`emitWhen` เดิม
- กำลังทำ: เตรียมเปลี่ยนช่อง JSON เป็นตัวเลือกในเงื่อนไขแถวและรายการ พร้อมปรับคำบนหน้าและ tour; แบบยังเป็น draft ไม่ลง M-xx จนสรุปแนวทาง
- ติด / รอ: คำยืนยันรายการไฟล์ก่อนแก้ source code ตาม AGENTS.md; ยังไม่แก้ source code และยังไม่ได้รัน build/test ของการเปลี่ยนแปลงนี้
- ต่อไป: หลังยืนยัน ลงมือใน repo Web และตรวจ build/test/ความเข้ากันได้กับเงื่อนไขเดิม; ไม่ commit/push จนกว่าผู้ใช้สั่ง

### 2026-11-25 — เปิด Web และ Mapping API หลัง pull ล่าสุด
- เสร็จ: เปิด Web `73f89ad` ด้วย `npm start -- --host localhost --port 4200` และ Mapping `93be537` ด้วย `dotnet run --project src/Mapping/Mapping.Api --launch-profile local --no-build` หลัง EF CLI build ผ่าน; เปิดค้างที่ `http://localhost:4200/mapping` และ `http://localhost:5177/scalar/v1`
- เสร็จ: apply migration `20261123155635_AddContractRegistrations` และ `20261124035222_AddMappingConfigDeletedAt` เฉพาะ PostgreSQL Docker บนเครื่อง โดยตรวจ connection เป็น `localhost:5433` ก่อนรัน; migration ตรงโค้ด **11/11** ไม่มีรายการค้าง
- หลักฐาน: Web, `/health/live`, `/health/ready`, `/scalar/v1`, `GET /api/mapping/mapping-configs` และ `GET /api/mapping/contracts` ตอบ HTTP **200**; API ทั้งสองรายการ `success: true` และ CORS อนุญาต `http://localhost:4200`; PostgreSQL และ Kafka ใน Docker ทำงานตามเดิม
- กำลังทำ: —
- ติด / รอ: —
- ต่อไป: ผู้ใช้เปิดหน้า Mapping เพื่อตรวจงาน; งานนี้เป็นการเปิดระบบ Local ไม่ได้รัน external E2E หรือแก้ source code; repo Mapping/Web ไม่มีไฟล์แก้ค้าง และไม่ได้ commit/push

### 2026-11-24 — เปิดระบบ Local ตามคำขอผู้ใช้
- เสร็จ: เปิด Web ด้วย `npm start -- --host localhost --port 4200`; Angular compile ผ่าน และ `http://localhost:4200/mapping` ตอบ HTTP **200**
- เสร็จ: เปิด Mapping API จากโค้ดล่าสุดด้วย `dotnet run --project src/Mapping/Mapping.Api --launch-profile local` ที่ `http://localhost:5177`; PostgreSQL `:5433` และ Kafka `:9094` ใน Docker healthy; `/health/live`, `/health/ready`, `/scalar/v1` และ `GET /api/mapping/mapping-configs` ตอบ HTTP **200**, API `success: true` และ CORS อนุญาต `http://localhost:4200`; migration ในฐานข้อมูล Local ตรงโค้ด **9/9** ไม่มีรายการค้าง
- แก้สภาพแวดล้อม: `scripts/up.ps1` build ล้มที่ `dotnet restore` ด้วย `read-only file system`; ตรวจพบไดรฟ์ C เหลือประมาณ **10 MB** และ Docker มี I/O error; กู้ Docker ด้วยการปิด process ของ Docker และ `wsl --shutdown` แล้วเปิดใหม่ (มีเฉพาะ distro `docker-desktop`); รอบนี้ใช้ API บน host แทนการ build image ซ้ำ
- เสร็จตามคำอนุญาตผู้ใช้: ย้าย NuGet cache ประมาณ **2.13 GB** จาก `C:\Users\ous9741\.nuget\packages` ไป `D:\Caches\NuGet\packages` และสร้าง junction ที่ path เดิม; ตรวจจำนวนไฟล์/ขนาดรวมก่อนและหลังตรงกัน
- เงื่อนไขการรัน: ใช้ค่าเดิมของ profile `Local` ซึ่ง `Messaging`, `FileIntake`, `RowTransformation` และ `Delivery` เป็น disabled; ผลข้างต้นยืนยัน Web/API/ฐานข้อมูล ไม่ใช่การทดสอบ pipeline นำเข้าและส่งปลายทาง
- กำลังทำ: —
- ติด / รอ: —
- ต่อไป: ผู้ใช้เปิดหน้า Mapping เพื่อตรวจงาน; ไม่มีการแก้ source code หรือ commit/push

### 2026-11-23 — Commit/push Source Table และปรับ Source fields
- เสร็จ: ตามคำสั่งผู้ใช้ commit และ push repo Mapping บน `develop` เป็น **`16ba7e7`** — `feat(mapping): support custom source table names`; รวม 16 ไฟล์สำหรับ create/read metadata, validation/conflict/idempotency, runtime resolve table และ tests; local ตรง `origin/develop`, working tree สะอาด
- เสร็จ: commit และ push repo Web บน `develop` เป็น **`e455938`** — `feat(mapping): configure source table names and simplify fields` (9 ไฟล์) และ **`00191b0`** — `style(mapping): align source fields table columns` (เฉพาะ `styles.scss` ตามกฎไฟล์ร่วม); local ตรง `origin/develop`, working tree สะอาด
- หลักฐาน Mapping: `scripts/check.ps1` ผ่าน build, unit **39/39**, integration **178 passed / 9 skipped / 187 total**, เวลา integration **7 นาที 6 วินาที** และ EF model ตรง migration; external E2E 9 ข้อข้ามตามโหมดปกติที่ไม่ได้ตั้งค่า images ไม่อ้างว่ารัน external acceptance ใหม่
- เงื่อนไขการรัน: รอบปกติมี timeout 1 ข้อ `KafkaFileIntakeTests.HostedPipeline_ShouldReadThreeFormatsQueueThroughKafkaAndArchiveWithoutHttpDelivery` ระหว่างนำเข้า 2,556 แถว (อีก 177 ข้อผ่าน); รันข้อเดี่ยวผ่าน **1/1** ในประมาณ **1 นาที 20 วินาที** (`kafka-intake-isolation.trx`); เครื่องเหลือ RAM ราว 2 GB จึงรัน check เต็มซ้ำด้วย `RunSettingsFilePath` ชี้ไฟล์ชั่วคราวที่มี `xUnit.MaxParallelThreads=2`/`ParallelAlgorithm=conservative` ตาม [เอกสาร xUnit](https://xunit.net/docs/config-runsettings#maxparallelthreads) แล้วผ่าน; ไม่เปลี่ยนโค้ด/timeout ไม่ filter tests ใน check และไม่ commit runsettings ลง repo
- หลักฐาน Web: source ชุดที่ commit ผ่าน `npm run build` และ `npm run test:ci` **80/80** ใน 9 suites; ไม่มีการแก้ source หลังผลตรวจนี้
- เอกสารชุดนี้: ปรับ `data-runtime-design.md`, `mapping-api-contract.md` และ `plan.md` ให้ตรง M-38–M-41 พร้อม commit hashes/หลักฐาน โดยไม่แก้ไฟล์ของ Dev 1 หรือ repo Informs
- กำลังทำ: —
- ติด / รอ: Q11 ขอ Dev 1 ปรับ D-84 ตามคำยืนยันล่าสุด; timeout เมื่อรันความขนานปกติยังเป็นข้อจำกัดของการทดสอบบนเครื่องนี้ ไม่ได้แก้ถาวรใน runner configuration
- ต่อไป: ติดตาม Q11 และงาน M5.3 ที่เหลือ; ข้อมูลข้างต้นเป็นหลักฐาน local ไม่ใช่ company deployment

### 2026-11-23 — เอาชื่อที่แสดงออกจาก Source fields ใน wizard
- เสร็จ: ตามการยืนยันของผู้ใช้ เอาคอลัมน์/ช่องกรอก “ชื่อที่แสดง” ออกจากขั้นที่ 1 และปรับ tour ให้ใช้ Key เป็นชื่ออ้างอิงตาม M-41
- เสร็จ: wizard ไม่เก็บชื่อที่แสดงจากการกรอก/อ่านไฟล์ตัวอย่าง/คัดลอก config; ส่ง `displayName: null` สำหรับ Source fields ใหม่ ให้ mapper ใช้ Key แทนตามพฤติกรรมเดิม; ปรับจำนวน grid columns ให้ตรงกันทั้ง Excel/Delimited/Fixed-width
- ไฟล์ Web: `features/mapping/mapping-new.html`, `mapping-new.ts`, `mapping-new-tour.ts` และเฉพาะ `.col-row`/`.col-fixed` ใน `src/styles.scss`
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` ผ่าน **80/80** ใน 9 suites; dev server compile ผ่านและส่ง page reload แล้ว, Web HTTP **200**; `git diff --check` ผ่าน
- กำลังทำ: —
- ติด / รอ: —; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจตารางคอลัมน์ในไฟล์ขั้นที่ 1; field ของ config เดิมยังมี metadata เดิมตาม version ที่บันทึกไว้

### 2026-11-23 — เอาคอลัมน์ชนิดออกจาก Source fields ใน wizard
- เสร็จ: ตามการยืนยันของผู้ใช้ เอาคอลัมน์/ตัวเลือก “ชนิด” ออกจากตารางคอลัมน์ในไฟล์ขั้นที่ 1; แสดงข้อความ “ข้อมูลต้นทางเก็บเป็นข้อความ การแปลงตัวเลขหรือวันที่กำหนดในขั้นที่ 4” และปรับ tour ให้ตรงกันตาม M-40
- เสร็จ: wizard ไม่เก็บหรือคัดลอก type hint และส่ง `logicalTypeHint: null` เมื่อสร้าง Source fields ใหม่; จับคู่อัตโนมัติตามชื่อด้วย `trim(source)` โดยไม่แปลงชนิดจาก hint ที่ผู้ใช้มองไม่เห็น; การแปลงในขั้นที่ 4 ยังตั้งค่าได้
- ไฟล์ Web: `features/mapping/mapping-new.html`, `mapping-new.ts`, `mapping-new-tour.ts` และเฉพาะ `.col-row`/`.col-fixed` ของ Mapping ใน `src/styles.scss`; จำนวน grid columns ตรงกับ header/fields ทั้ง Excel/Delimited/Fixed-width
- หลักฐาน: `npm run build` ผ่าน; `npm run test:ci` ผ่าน **80/80** ใน 9 suites; dev server compile ผ่านและส่ง page reload แล้ว, Web HTTP **200**; `git diff --check` ผ่าน
- กำลังทำ: —
- ติด / รอ: Q11 ขอ Dev 1 ปรับข้อความ D-84 ให้ตรงคำยืนยันล่าสุดของผู้ใช้; ไม่แก้ `rules.md` เอง และไม่ถือว่ารอ Q11 เพื่อหยุดงานที่ผู้ใช้ยืนยัน; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจขั้นที่ 1 และกำหนด convert ที่ขั้นที่ 4 ตาม contract

### 2026-11-23 — จัดข้อมูล Mapping ขั้นที่ 1 เป็นสามคอลัมน์
- เสร็จ: ตามการยืนยันของผู้ใช้ แก้เฉพาะ layout ใน Web `src/app/features/mapping/mapping-new.html` ให้ชื่อ Mapping / Input folder / ชื่อตารางต้นทางอยู่แถวเดียวกันเป็น 3 คอลัมน์กว้างเท่ากัน โดยใช้ grid เดิมและย้ายช่องชื่อตารางเข้าแถวเดียวกัน
- หลักฐาน: `npm run build` ผ่าน; dev server compile ผ่านและส่ง component update แล้ว; Web local HTTP **200**; `git diff --check` ผ่าน
- กำลังทำ: —
- ติด / รอ: —; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ตรวจ layout ใน wizard ขั้นที่ 1 ที่เปิดอยู่

### 2026-11-23 — เปิด Docker กลับมาและตรวจ custom Source Table บน local
- เสร็จ: ตามคำขอผู้ใช้ เปิด Docker Desktop/Engine กลับมา; พบ Desktop/backend ค้างและ WSL `docker-desktop` อยู่สถานะ Stopped, CLI restart timeout จึงปิดเฉพาะ 7 โปรเซสของ Docker ใต้โฟลเดอร์ติดตั้ง แล้ว `wsl --shutdown` และเปิด Desktop ใหม่; Engine **29.8.0** ตอบคำสั่งแล้ว โดยไม่ลบ containers/volumes หรือ reset ข้อมูล
- เสร็จ: เปิด containers เดิม PostgreSQL/Kafka/Informs/Mapping; PostgreSQL และ Kafka healthy, Mapping/Informs `/health/ready` ตอบ **200 Healthy**
- หลักฐาน: รัน integration tests ที่เคยติด Docker ใหม่บน PostgreSQL Testcontainers ผ่าน **15/15**, ไม่มี skip (`custom-source-table-integration.trx`): custom name/keyword, invalid names, table conflicts, concurrent ownership, append/idempotency, import→transform และ Source inspection; ผลนี้แทน run ที่ abort ก่อนหน้า
- เสร็จ: `docker compose build mapping` ผ่านแล้ว และ recreate เฉพาะ Mapping ด้วย `--no-deps --no-build`; ตรวจ app environment และ named volumes ให้ตรงของเดิมก่อนเปลี่ยน container, ไม่รัน migrations; GET Source definitions ของ runtime ใหม่ส่ง `physicalTableName` ครบ **1/1** รายการ และตารางเดิมยังอยู่
- เสร็จ: เปิด Web dev server local กลับมาที่ **http://127.0.0.1:4200/**, compile ผ่านและ HTTP **200**; ช่องตั้งชื่อตารางจากโค้ด M-39 พร้อมใช้งานกับ API local รุ่นใหม่
- กำลังทำ: —
- ติด / รอ: ไม่มีสำหรับ Docker/local API และ integration subset ของ M-39; ยังไม่ได้ตรวจหน้าจอด้วย browser automation, ไม่ถือว่าปิด M5.3 ทั้งข้อหรือผ่าน company deployment; ยังไม่ commit/push
- ต่อไป: ผู้ใช้ทดสอบ wizard ขั้นที่ 1 ด้วยชื่อตารางที่ต้องการ; รัน `scripts/check.ps1` ก่อน commit ตามกฎ repo เมื่อมีคำสั่ง commit

### 2026-11-23 — ให้ผู้ใช้กำหนดชื่อตารางต้นทางเมื่อสร้าง Mapping
- เสร็จ: ตามชุดไฟล์ที่ผู้ใช้ยืนยัน เพิ่มช่องบังคับ “ชื่อตารางต้นทาง” ใน wizard ขั้นที่ 1 เช่น `partner_d_raw` → `mapping.partner_d_raw`; ส่งผ่าน `physicalTableName` ใน Source definition create API และล็อกหลังสร้างสำเร็จตาม M-39
- เสร็จ: Domain/validator/repository ตรวจชื่อ 1–48 ตัว; ใช้ชื่อจาก metadata ใน create/append/import/transform/inspection แทนสูตร `src_<ID>`; quote identifiers, กันชื่อชน metadata/relation และ concurrent creation, hash ของ idempotency รวมชื่อ; API รุ่นเดิมที่ไม่ส่งชื่อและตารางเดิมยังใช้ชื่อเดิม ไม่มี migration ใหม่
- เสร็จ: หากชื่อชนหลังสร้าง config identity แล้ว Web ยังให้เปลี่ยนชื่อตารางและลองใหม่กับ config เดิมได้ ไม่สร้างชื่อ Mapping ซ้ำ; แสดง conflict ข้างช่องกรอก
- หลักฐาน: Mapping Release build ผ่าน **0 errors**, unit tests **39/39** (`custom-source-table-unit.trx`), EF model ตรง migration; ตรวจ entity/validator เพิ่มแบบไม่ใช้ Docker **15/15** กรณี (ชื่อปกติ/keyword/48 ตัว/ชื่อผิด/SQL fragment/newline/legacy fallback); Web `npm run build` ผ่าน และ `npm run test:ci` **80/80** ใน 9 suites
- เพิ่ม tests: API ตั้งชื่อเอง/ชื่อซ้ำ/ชื่อชนตารางระบบ/identifier ผิด, repository create+append+idempotency/concurrent names และ import→transform/Source inspection ด้วย custom table; tests compile ผ่าน แต่ยังไม่อ้างว่าผ่าน PostgreSQL
- ติด / รอ: Docker server ไม่ตอบภายใน 5 วินาทีและ readiness local ไม่ตอบ; integration run `custom-source-table-integration.trx` ถูก abort โดย inactivity timeout 45 วินาทีก่อนมีผล test จึงยังไม่ผ่าน integration gate และยังไม่ได้อัปเดต container local; ไม่แก้ DB บริษัท ไม่ commit/push
- ต่อไป: เมื่อ Docker local พร้อม รัน integration tests ที่เพิ่ม/แก้และ `scripts/check.ps1`, build/recreate Mapping โดยรักษา runtime config แล้วตรวจ wizard กับ API รุ่นใหม่

### 2026-11-23 — แสดงชื่อตารางต้นทางจริงบนหน้า Mapping
- เสร็จ: ตามชุดไฟล์ที่ผู้ใช้ยืนยัน เพิ่ม `physicalTableName` ใน Source definition list/detail API โดยอ่านจาก DB; Web แสดง `mapping.<physicalTableName>` แบบ read-only ใน wizard ขั้นที่ 1 ใต้ชื่อ Mapping/Input folder, ขั้นที่ 5, หน้า Config และ Source fields list/detail ตาม M-38
- เสร็จ: ก่อนสร้าง config แบบร่าง แสดง “ยังไม่ได้สร้างตารางต้นทาง”; ถ้า API รุ่นเก่าไม่ส่งชื่อ แสดง “ยังไม่มีข้อมูลชื่อตาราง” โดยไม่เดาชื่อจาก Key/ชื่อ config/ID; เพิ่ม field ยังคงตารางเดิม ไม่มี migration ใหม่
- หลักฐาน: Mapping Release build ผ่าน **0 errors**; API integration tests `MappingSourceDefinitionsApiTests` + `MappingConfigsApiTests` บน PostgreSQL Testcontainers ผ่าน **2/2**, ไม่มี skip (`source-table-metadata.trx`); Web `npm run build` ผ่าน และ `npm run test:ci` ผ่าน **78/78** ใน 9 suites รวม metadata list/detail, API รุ่นเก่า และการเลือกตารางตาม Source definition ของ config
- กำลังทำ: โค้ดพร้อมแล้ว แต่ยังยืนยันหน้าจอกับ API local รุ่นใหม่ไม่ได้
- ติด / รอ: `docker compose build mapping` ล้มระหว่าง `dotnet publish` ด้วย `Input/output error` / `Read-only file system` ที่ `/app/Mapping.Api`; Docker inspect และ readiness ของ service เดิมไม่ตอบในรอบตรวจถัดมา จึงยังไม่ได้ recreate container หรือ apply migration; ยังไม่ commit/push
- ต่อไป: เมื่อ Docker local กลับมาพร้อม ให้ build/recreate เฉพาะ Mapping โดยรักษา runtime config เดิม แล้วตรวจ `physicalTableName` ผ่าน API และหน้า Web; ไม่ถือว่าติดตั้งรุ่นใหม่แล้ว

### 2026-11-23 — แก้ SDK ของ workspace และ C# Dev Kit
- เสร็จ: ผู้ใช้ขอแก้เส้นแดงของ `ApiContractClientTests.cs`; log ของ C# Dev Kit และคำสั่ง restore ของ solution ชั่วคราวยืนยัน `NETSDK1045` เพราะเลือก SDK 8 สำหรับ projects ที่ target `net10.0`
- เสร็จ: ติดตั้ง Microsoft .NET SDK **10.0.404** ในตำแหน่งระบบเดียวกับ SDK **8.0.400** หลังตรวจ SHA-512 และลายเซ็น Microsoft; installer exit code **0** และไม่ต้อง restart Windows
- เสร็จ: ปรับ `global.json` ที่ราก workspace เป็น SDK ขั้นต่ำ `10.0.100` พร้อม `rollForward: latestFeature` ให้เลือก SDK 10 ที่ติดตั้งอยู่; ปรับสำเนา `global.json` ใน cache ของ C# Dev Kit ให้ตรงกัน เพราะ extension คัดไฟล์ต้นทางนี้เข้า solution ชั่วคราวทุกครั้งที่เปิด workspace
- หลักฐาน: `dotnet --version` แบบปกติใน Mapping และโฟลเดอร์ solution ของ IDE ได้ **10.0.404**; Release build ของ Mapping ผ่าน **0 errors**; tests เฉพาะ `ApiContractClientTests` ผ่าน **9/9** ไม่มี skip; `dotnet restore` ของ `mti-mapping (Workspace).sln` ที่ C# Dev Kit ใช้จริงผ่าน exit code **0** หลังแก้ SDK pin (ก่อนแก้ได้ `NETSDK1045`)
- กำลังทำ: —
- ติด / รอ: ผู้ใช้กด `Developer: Reload Window` ใน VS Code ให้ C# Dev Kit โหลด project ใหม่; log ของ IDE ที่เปิดอยู่ยังเป็นรอบก่อนติดตั้ง จึงยังไม่ยืนยันว่าเส้นแดงบนหน้าจอหายแล้ว
- ต่อไป: ตรวจ Problems หลัง reload; ผู้ใช้สั่ง commit/push configuration `global.json` และหลักฐานใน `plan.md` ใน commit ชุดนี้

### 2026-11-23 — Pull/push โดยรักษางานเดิมและจบ merge ที่ค้าง
- เสร็จ: ตามคำสั่งผู้ใช้ สำรอง Git history/index/merge state และไฟล์งานค้างพร้อมตรวจ SHA-256 ก่อนรวมงาน; แก้ conflict ของ `plan.md` โดยเก็บหลักฐาน M4.4 ของ Dev 2 และหมายเหตุ Dev 1 ครบ ไม่เปลี่ยนเป้าหมายจากข้อเสนอที่ยังไม่ได้ตกลง
- เสร็จ: Mapping merge commit `b001dd6` บน `develop` และ push `origin/develop` สำเร็จ; ตรวจว่างานเดิม `1b5f2f2` และ remote `ee462e2` เป็น ancestors ทั้งคู่; local/remote ตรงกันและ working tree สะอาด
- ไฟล์ที่รวม conflict ตามการยืนยัน: `src/Mapping/Mapping.Application/Capabilities/Queries/GetCapabilitiesQuery.cs`, `tests/Mapping.IntegrationTests/Api/MappingCatalogApiTests.cs`, `tests/Mapping.IntegrationTests/Delivery/ApiContractClientTests.cs`; ใช้ Excel extensions จาก validator และตรวจสถานะ ApiContract โดยตรง เก็บส่วน auto-merge ของทั้งสองฝั่งไว้
- ไฟล์ที่ปรับเพิ่มตามการยืนยัน: `tests/Mapping.IntegrationTests/EndToEnd/DeliveryAcceptanceEndToEndTests.cs` เปลี่ยนเป็น `IDestinationClient`/`DestinationDeliveryOutcome` และส่ง `DeliveryTarget`; `tests/Mapping.IntegrationTests/EndToEnd/InformsEndToEndFixture.cs` ใช้ `Destinations` ผ่าน helper เดิมแทน `Informs:*` เพื่อให้ E2E scenarios เดิมใช้ interface/config หลัง merge ได้
- หลักฐาน: `.NET SDK 10.0.404`; `scripts/test-e2e.ps1 -InformsSource <clean temporary checkout at f1eca8d> -NoBuild -Gate` ผ่าน Release build, unit **39/39**, integration **184/184** ใช้เวลา **14 นาที 50 วินาที**, external E2E **9/9** ไม่มี skip และ EF model ตรง migration; TRX `m4-4-gate.trx` ในทั้งสอง test projects; ชุด MTLS 2,556 ใบได้ Received 2,553 / Cancelled 3
- หลักฐานการรักษางาน: เทียบ backup แล้วไฟล์งานค้าง Mapping **104/107** ไฟล์เหมือนเดิมทุก byte (อีก 3 ไฟล์คือ conflict ที่ยืนยัน); เอกสารเดิม **3/4** ไฟล์เหมือนเดิมทุก byte (อีกไฟล์คือ `plan.md`); tests อีก 2 ไฟล์ที่ปรับมีต้นฉบับใน commit `1b5f2f2` และ bundle สำรองก่อน sync
- เสร็จ: Web fast-forward จาก `d7820d8` เป็น `a8e1762` และ push ตรวจแล้วตรง origin/develop; repo Informs อ่านอย่างเดียว ไม่แก้ checkout หรือ source; เอกสารรวม remote `19927c2` และงานเดิม `d1ed4f6` ใน merge commit ชุดนี้
- กำลังทำ: —
- ติด / รอ: ไม่มีสำหรับ Git sync; ประเด็น acceptance/company deployment เดิมยังติดตามตามหัวข้องาน
- ต่อไป: ทำงาน mapping ต่อจากแผนที่รวมข้อมูลทั้งสองฝั่งแล้ว
### 2026-11-18 — Commit M4.4
- เสร็จ: ตามคำสั่งผู้ใช้ commit source `prjInsuranceValidationMapping` บน develop เป็น `1b5f2f2` — `feat(mapping): verify Informs delivery and enable capabilities`; รวม12ไฟล์ (E2E tests/runner/capabilities/regressions) และ working tree ของโค้ดสะอาดหลังcommit
- เสร็จ: บันทึกsourcecommitใน planและแบบ/ผลทดสอบ M4.4 รวมเอกสาร2ไฟล์ในcommitเอกสารชุดนี้; ยังไม่pushทั้งสองrepo
- หลักฐาน: โค้ดชุดที่commitผ่านcheck.ps1ล่าสุด unit38/38 integration171/171 externalE2E9/9ไม่มีskipและEFmodelกับInforms `f1eca8d`; ไม่มีsourceเปลี่ยนเพิ่มจากชุดที่ผ่านgate
- ติด / รอ: companydeploy/Q7, partnerbusiness/Q5/Q9, M1.2/Q8/Web และM5.1 probesแยกตามเดิม

### 2026-11-18 — ปิด M4.4 Mapping ถึง Informs จริงบน local
- เสร็จ: hosted pipelineของMapping + PostgreSQL/Kafka/Informsจริงที่ pin `f1eca8d` ผ่าน fullMTLS2556แถว/Delimited/FixedWidth; Source/outputs/Deliveredกระทบยอด2556ครบ, ReceiverมีReceived2553/Cancelled3, request hashesและD-80 metadataตรงsnapshotที่บันทึก
- หลักฐาน: `scripts/test-e2e.ps1 -InformsSource <clean checkout at f1eca8d> -NoBuild -Gate` ผ่าน Release build, unit **38/38**, integration **171/171**, EF model และ scriptตรวจexternal **9/9** Passed ไม่มีfilter/skip/abort; integrationใช้เวลา **17นาที5วินาที**; TRX `m4-4-gate.trx` ในทั้ง2test projects
- หลักฐาน: actual201/200/replay/replaced/400/409, lostack/restart, proxy503/ครบ6 และ real429/Retry-Afterผ่าน; regressionstreaming20รอบผ่านในgateหลังแก้100Continue/isolation; FullMTLSในgateใช้4นาที17.40วินาที; [รายการไฟล์/ผลจริง/ขอบเขต](m4-4-informs-end-to-end-design.md)
- เสร็จ: source12ไฟล์บนฐาน `3fd48f8` ตามชุดที่ผู้ใช้ยืนยัน; capabilitiesพร้อมเฉพาะExcel/Delimited/FixedWidth/ApiContract ส่วนworker defaultsยังdisabled; อัปเดตแบบM4.4/M-37/Q4และplanโดยยังไม่commit/push
- ติด / รอ: Q7/company deployment, Q5/Q9/partnerbusiness acceptance, M1.2/Q8/Web review และM5.1 probesแยกตามเดิม; ผลนี้เฉพาะintakeกับInforms `f1eca8d` ไม่อ้างremoteใหม่ `6cb5664` หรือProducts/Client API validationครบทุกstage
- ต่อไป: M1.2สำหรับAPIจัดการconfig และM5.1สำหรับhealth/probesตามแผน

### 2026-11-18 — ตรวจ gate M4.4 และแก้ race ใน test harness
- เสร็จ: actualacceptance/fullMTLS2556/3formatsผ่านในการรันแยก; Gateรวมรอบแรกได้unit38/38 และ integration167/170 แต่recovery3ข้อfail จึงยังไม่ปิดtask
- หลักฐาน: regressionHTTPstreamingแบบวน20รอบโดยไม่เปิดMappingworkerจับholderได้429ใน5วินาที ยืนยันว่าprobeแย่งpermit; requestsเกินในกรณีถัดไปเป็นคนละSourceRowIdจากsharedfixture ไม่ใช่attempt7ของdeliveryเดิม
- กำลังทำ: synchronizeด้วยHTTP100Continueก่อนprobe และแยกfixtureต่อrecoverytest; เพิ่มregressionเป็นexternalรวม9ข้อในsource12ไฟล์เดิม รันfocusedแล้วจะตรวจgateรวมใหม่โดยไม่ลดassertions/เพิ่มquotaหรือแก้productiontransport
- หลักฐานหลังแก้: focusedrecovery4/4ผ่าน ไม่มีskip รวมregression20รอบ; เริ่มcheck.ps1รอบรวมใหม่ด้วยexternal9ข้อจริงแล้ว (unit38/38ผ่าน) รอintegration171ข้อและEFmodelก่อนปิดtask
- ติด / รอ: gateรวมที่ผ่านครบ; Q7/Q5/Q9/M1.2/Q8/Webยังแยกตามเดิม; ยังไม่commit/push

### 2026-11-18 — เริ่ม implement M4.4 ตามชุดที่ผู้ใช้ยืนยัน
- เสร็จ: ผู้ใช้ยืนยัน 12 ไฟล์/public seams/scenarios ใน [แบบ M4.4](m4-4-informs-end-to-end-design.md); เพิ่ม fixture ที่ใช้ Informs API/migrator images จาก `f1eca8d`, isolated PostgreSQL/Kafka และ Mapping host จริง ไม่มี project/assembly reference ข้าม service
- หลักฐานระหว่างทำ: actual intake/replaced/late-old-key replay ผ่าน1ข้อ; Delimited/FixedWidth ผ่าน hosted pipelineถึง Informsจริงอีก1ข้อพร้อม API counts/raw Source/metadata/hash pin และ D-80 file time จาก UW file summary; ไม่ใช้ผลนี้แทน full M4.4 gate
- กำลังทำ: full MTLS2556แถวกับ actualschema/HTTP payload hashes, fault/restart/real429/Retry-After และ runner สำหรับรันซ้ำ; capabilitiesจะเปิดเฉพาะหลังacceptanceผ่าน และยังไม่ติ๊ก M4.4
- ติด / รอ: full E2E scenarios/check.ps1; Q7/Q5/Q9/M1.2/Q8/Web reviewแยกตามเดิม; ยังไม่ commit/push

### 2026-11-18 — เตรียม M4.4 กับ Informs จริง
- เสร็จ: ตรวจ origin/develop ของ Informs พบ `f1eca8d` ใหม่กว่า checkout `6ab23ad`; cloneชั่วคราวแบบอ่านsourceเดิม, buildimagelocalด้วยDockerfileของInforms และรัน HTTP schema preflightได้200พร้อม `fileReceivedAt` ตาม D-80; schema SHA-256 `38b1fc59bf83dda277eb2143e841a1fc34c89ef8f7b245d8a4ba8ab14e7da5bd`; หยุดcontainer preflightแล้วและยังไม่ส่งใบ/ไม่เชื่อม DBในขั้นนี้
- กำลังทำ: เตรียม [แบบ M4.4/public seams/scenarios/source batch](m4-4-informs-end-to-end-design.md) เพื่อยืนยันก่อนเพิ่ม integration tests; จะทดสอบ fullMTLS2556แถว/3formats/actual201-200-400-409/replaced/replay/real429/Retry-After/lostack/restart พร้อม countsจากAPIs
- ติด / รอ: การยืนยัน source batch/seams; Q4มี implementation/schemaใหม่ให้ทดสอบแล้ว แต่ยังไม่ปิดจนยืนยันข้อมูล/เวลารับจริงผ่านruntime; Q7และ Q5/Q9/M1.2/Q8/Web reviewยังแยกตามเดิม
- ต่อไป: เพิ่มtests/runnerตามชุดที่ยืนยัน แล้วรันcheck.ps1แบบเปิดexternalE2Eทุกข้อจริงก่อนติ๊ก M4.4; ยังไม่แก้sourceและไม่commit/pushอัตโนมัติ

### 2026-11-18 — Commit/push M4.3
- เสร็จ: ตามคำสั่งผู้ใช้ commit source `prjInsuranceValidationMapping` บน develop เป็น `3fd48f8` — `feat(mapping): add delivery operations and inspection APIs`; รวม77ไฟล์ของ M4.3 และ push origin/develop สำเร็จ ตรวจ local/remote hash ตรงกันและ working tree สะอาด
- เสร็จ: บันทึก source commit ใน plan/แบบ M4.3/แบบข้อมูล runtime รวมเอกสาร3ไฟล์ใน commit เอกสารชุดนี้
- หลักฐาน: check.ps1 ของโค้ดชุดที่ commit ผ่าน Release build, unit38/38, integration162/162 และ EF model ด้วย temporary sequential-collection RunSettings; ไม่มีการแก้ source เพิ่มจากชุดที่ผ่าน gate
- ติด / รอ: actual Informs Q4/M4.4, company dependencies Q7 และ M1.2/Q8/Web review ยังแยกติดตาม
- ต่อไป: M4.4 end-to-end กับ Informs จริงบน local เมื่อ Q4 พร้อม

### 2026-11-18 — ปิด M4.3 delivery operations API บน local
- เสร็จ: ผู้ใช้ยืนยัน contract subset/source batch M4.3 แล้ว; เพิ่ม 12 routes สำหรับ logs/summary/Source/Payload/retry/pause-resume, read-only snapshot reader, atomic audited commands และ pinned-schema safe diagnostics; migration `20261118031413_AddDeliveryDiagnosticsAndLogIndexes`
- หลักฐาน: `scripts/check.ps1` ผ่าน Release build, unit **38/38**, integration **162/162** และ EF model check; integration ใช้เวลา 8 นาที 31 วินาที มีผล TRX `m4-3-gate-serial.trx` ในทั้ง 2 test projects; ใช้ temporary sequential-collection RunSettings ไม่มี filter/skip และ explicit concurrency tests คงเดิมตามเงื่อนไข M4.2
- หลักฐาน: เพิ่ม unit6/integration24 ตรวจ mixed 0/หลาย outputs/attempts, snapshot ระหว่าง worker commit, partial/Blocked/Duplicate, pinned Source/NULL/empty/3 formats, actual payload/hash/key/availability/no GET writes, audited retry/pause replay/body/version/race/rollback/ครบ6/Retry-After และ safe pinned-schema diagnostics/400 stall/history immutable; [ผลตรวจ/รายการไฟล์](m4-3-delivery-operations-api-design.md)
- เสร็จ: source77ไฟล์บนฐาน `aca67c6`, docs3ไฟล์ (plan/แบบM4.3/แบบข้อมูลruntime); ยังไม่ commit/push; sender ยัง default disabled, ไม่มีบริษัท DB write/HTTP และไม่แก้ Informs/Web/demo
- ติด / รอ: actual Informs Q4/M4.4, company dependencies Q7 และ M1.2/Q8/Web acceptance แยกติดตาม; default parallel gate ยังมีข้อจำกัดเดิม ไม่อ้างว่าแก้ถาวรจากผลรันแบบ sequential
- ต่อไป: M4.4 end-to-end กับ Informs จริงบน local เมื่อ Q4 พร้อม

### 2026-11-18 — เตรียม M4.3 delivery operations API
- เสร็จ: ตรวจ draft M1.2/Q8, counts/inspection M-10/M-18/M-19, retry cap M-20/M-22 และ ledger/controls ของ commit aca67c6; เขียน [แบบ M4.3 พร้อม API contract subset/source batch](m4-3-delivery-operations-api-design.md)
- กำลังทำ: รอ review/ยืนยัน contract เฉพาะ M4.3 พร้อม source batch ก่อนทำ controllers เพราะ Q8 เคยพัก review; รวม file/row/delivery/attempt logs, counts, raw inspections, safe pinned-schema field diagnostics, retry และ pause/resume โดยไม่ถือว่าปิด M1.2/Q8/Web ทั้งชุด
- ติด / รอ: actual Informs T2.6/Q4 และ company dependencies Q7 ตามเดิม; file/row retry/reprocess/business resend/เพิ่มโควตาเกิน6 ไม่อยู่ในรอบนี้
- ต่อไป: หลังยืนยัน implement API/repository/domain guards/diagnostics migration/tests และ check.ps1; ยังไม่แก้ source และไม่ commit/push อัตโนมัติ

### 2026-11-18 — Commit/push M4.1 และ M4.2
- เสร็จ: ตามคำสั่งผู้ใช้ commit source `prjInsuranceValidationMapping` บน develop เป็น `aca67c6` — `feat(mapping): add durable Informs delivery and bounded retries`; รวม 41 ไฟล์ของ M4.1/M4.2 และ push origin/develop สำเร็จแล้ว พร้อม commit M3.3 `7014c83` ที่ยังค้าง; local/remote source ตรงกันและ working tree สะอาดหลัง push
- เสร็จ: บันทึก source commit ในแบบ M4.1/M4.2 และ plan พร้อมแบบข้อมูล runtime รวมเอกสาร 4 ไฟล์ใน commit เอกสารชุดนี้ตามคำสั่ง commit/push
- หลักฐาน: check.ps1 ล่าสุดผ่าน unit 32/32, integration 138/138 และ EF model ด้วย temporary RunSettings รัน collections ตามลำดับ; หลังลบ EF configuration ที่ซ้ำตรวจ build Release/model consistency ผ่านเพิ่มแล้ว; staged whitespace check ผ่าน ไม่มี runtime data/key/company config ใน commit
- ติด / รอ: default parallel gate ยังมีประวัติ Kafka timeout ตามข้อสังเกตเดิม; actual Informs T2.6/Q4, company dependency Q7 และ M4.3/M4.4 ยังแยกติดตาม
- ต่อไป: M4.3 log/summary/inspection/action APIs

### 2026-11-18 — ปิด M4.2: durable delivery runner/retry/recovery บน local
- เสร็จ: worker แบบ opt-in จาก DB NextAt, atomic claim/attempt reservation ก่อน HTTP, global budget/pause guard, dispatch/heartbeat/epoch fencing, bounded retry/backoff/Retry-After และ recovery ตาม M-35; ใช้ immutable bytes/key เดิม จำกัดสูงสุด 6 reservations ไม่มี attempt 7; migration `20261117095253_AddDeliveryRuntime`
- หลักฐาน: check.ps1 ผ่าน build Release, unit **32/32**, integration **138/138** และ EF model check โดยตั้ง temporary RunSettings `ParallelizeTestCollections=false`; ไม่มี filter/skip และไม่เปลี่ยน explicit concurrency ภายใน tests; รอบ default parallel ที่เคย timeout ยังเป็นข้อจำกัดการรันบนเครื่องนี้ ไม่อ้างว่าแก้ถาวรแล้ว
- หลักฐาน: M4.2 เพิ่ม unit **5** และ integration **14** (repository 4/runtime 4/recovery 5/hosted worker 1); ทดสอบ rollback reserve/complete, lease/grace/heartbeat failure/stale owner, pause/budget ข้าม workers, Retry-After, shutdown/cancel, crash/unknown/replay/ครบ 6 และ payload/key/profile pin; tests สอง worker peak HTTP 1 ผ่าน
- หลักฐาน: gate รอบผ่าน integration ใช้เวลา 9 นาที 12 วินาที; Kafka intake เดิมผ่าน 59.73 วินาทีภายใน timeout 3 นาที; full MTLS สังเคราะห์ 2,556 encrypted outputs ผ่าน 2 นาที 35.38 วินาที; heartbeat failure หลัง synchronize ผ่านพร้อมรหัส LeaseHeartbeatLost; [แบบ/รายการไฟล์/เงื่อนไขตรวจ M4.2](m4-2-durable-delivery-design.md)
- เสร็จ: อัปเดตแบบข้อมูล runtime และ M-35; รวม source ค้าง M4.1+M4.2 41 ไฟล์บนฐาน `7014c83` ไม่มี secret/runtime key/company config; Informs/Web ไม่ได้แก้ sender ยัง disabled โดย default และไม่มี HTTP/DB write ไปบริษัท
- ติด / รอ: actual Informs acceptance/deploy T2.6/Q4 และ dependency บริษัท Q7; M4.3 log/summary/inspection/action APIs และ M4.4 end-to-end จริงยังไม่ปิด; การทำให้ default parallel gate เสถียรเป็น follow-up ของ test infrastructure
- ต่อไป: M4.3; source/docs ยังไม่ commit/push ตามขอบเขตคำสั่ง

### 2026-11-18 — ตรวจ gate รวมของ M4.2 ต่อ
- เสร็จ: source M4.2/attempt ledger/worker/recovery และ tests ใหม่ทำแล้วตาม M-35; unit 32/32 และ integration 137/138 ผ่านในการรันรวมสองรอบที่ผ่านมาซึ่งยังไม่ถือเป็น gate สำเร็จ
- กำลังทำ: รัน check.ps1 เดิมด้วย temporary RunSettings ปิด parallel test collections เพื่อแยกการแย่งทรัพยากรของ PostgreSQL/Kafka/full MTLS fixtures; คง Kafka timeout/assertion และ concurrency ภายใน tests ของ worker สองตัว
- เสร็จ: ปรับ DeliveryRecoveryTests.HeartbeatDatabaseFailure ให้ติดตั้ง failure trigger หลัง HTTP request มาถึง stub แทนก่อนเริ่มส่ง และเพิ่ม assertion LastOutcomeCode=LeaseHeartbeatLost; ใช้ HTTP timeout 10 วินาทีเฉพาะ test นี้เพื่อไม่ให้ transport timeout กลบ heartbeat failure; targeted rerun ผ่าน 1/1 โดย production source ไม่เปลี่ยนในรอบตรวจนี้
- ติด / รอ: Kafka intake เดิม timeout ที่ 3 นาทีเมื่อรวมทั้งชุด แต่รันแยกผ่าน 1/1 ใน 2 นาที 5 วินาทีพร้อม checkpoint เดิน/ไม่มี blocked diagnostic; ต้องยืนยันผลรวมครบก่อนติ๊กปิด M4.2; actual Informs T2.6/Q4 และ dependency บริษัท Q7 ยังแยกจาก local gate
- ต่อไป: บันทึกผล gate และเงื่อนไขที่ใช้ให้ทำซ้ำได้ในแบบ M4.2; ไม่ commit/push อัตโนมัติ

### 2026-11-17 — เตรียม M4.2 durable delivery runner
- เสร็จ: ตรวจ encrypted Delivery Jobs/NextAt, typed Informs client ของ M4.1 และ delivery_controls/global budget/attempt guards ของแบบข้อมูล; เขียน [แบบ M4.2 พร้อม source batch](m4-2-durable-delivery-design.md) สำหรับ reserve ก่อน HTTP/เพดาน 6/heartbeat/fencing/Retry-After/recovery และ failure tests
- กำลังทำ: รอยืนยัน source batch ตาม AGENTS.md; เสนอ DB polling จาก Delivery Job ที่ commit แล้ว โดยไม่ให้ Kafka redelivery สร้าง attempt/reset backoff และคง sender ปิดโดย default
- ติด / รอ: Informs schema/deploy T2.6/Q4 และ company dependency Q7 ตามเดิม; local HTTP stub เป็นหลักฐานกลไก ไม่ใช่ acceptance จริง; API controls/inspection แยก M4.3
- ต่อไป: หลังยืนยัน implement/migration/PostgreSQL/HTTP/worker/recovery tests และ check.ps1; source M4.2 ยังไม่แก้ ไม่ commit/push งาน M4.1 แทรกเอง

### 2026-11-17 — ปิด M4.1: typed Informs HTTP client และ durable payload HTTP tests บน local
- เสร็จ: เพิ่ม typed `IInformsClient`/safe outcome DTO/Infrastructure HttpClient ตาม M-34 ใช้ final bytes จาก encrypted output ของ M-33; แปล Created/Replayed/Replaced/400/409/429/5xx/unknown outcome ด้วย bounded strict response parsing และ Retry-After โดยไม่มี internal retry/redirect/cookie reuse
- หลักฐาน: `./scripts/check.ps1` ผ่าน build Release, unit **27/27**, integration **124/124** และ EF model check; M4.1 เพิ่ม integration **11** ข้อ (HTTP client 8 + durable payload HTTP 3) ตรวจ status/flags/UUID/duplicate properties/limits/media/depth, timeout/cancellation/header/body stall, Retry-After delta/date, connection abort, offline config/no hosted sender และ exact UTF-8 bytes
- หลักฐาน: PostgreSQL → committed encrypted output → decrypt → HTTP loopback ผ่าน; โหลดด้วย DbContext ใหม่หลังเปลี่ยน/disable config แล้ว replay ส่ง bytes/hash/idempotency identity เดิม และ ciphertext/key reference/DB state/row version/Source-output-outbox counts ไม่เปลี่ยน; key หายหรือ payload/identity ถูกแก้ไม่มี HTTP request
- หลักฐาน: actual schema เก่าที่ขาด fileReceivedAt ยังคง Blocked/ContractMissingFileReceivedAt ไม่มี intent/HTTP; success mechanics ใช้ synthetic D-80 schema ชัดเจน; [แบบ/รายการไฟล์/ผลตรวจ M4.1](m4-1-informs-http-client-design.md); source เพิ่ม/แก้ 12 ไฟล์ ไม่มี DB migration/การแก้ Informs/Web/demo หรือ key/company configuration ในชุดงาน
- ติด / รอ: Informs T2.6/Q4 ก่อนรับตรวจจริงและ company dependency Q7; runtime ยังไม่มี send API/hosted delivery runner/attempt ledger ไม่อ้าง Delivered หรือเปิด capability ส่งจริง; งานเหล่านี้เป็น M4.2/M4.3/M4.4 ตามเดิม
- ต่อไป: M4.2 durable attempt reservation/retry/backoff/concurrency/lease recovery แล้วเชื่อม client; source/docs รอบนี้ยังไม่ commit/push ตามขอบเขตคำสั่ง

### 2026-11-17 — เตรียม M4.1 typed Informs HTTP client
- เสร็จ: ตรวจ encrypted payload ของ M3.3 commit `7014c83`, controller/response records ของ Informs และ contract §4; เขียน [แบบ M4.1 พร้อม source batch](m4-1-informs-http-client-design.md) สำหรับ exact bytes POST/typed outcomes/bounded safe parsing/timeout/Retry-After และ durable-payload HTTP tests
- กำลังทำ: รอยืนยันชุด source ตาม AGENTS.md; storage/crypto ใช้ของ M-33 เดิม ไม่เพิ่ม sender worker ที่ข้าม attempt ledger; durable retry/concurrency/lease/attempt reservations ยังเป็น M4.2
- ติด / รอ: schema/DTO Informs ปัจจุบันยังขาด fileReceivedAt ตาม T2.6/Q4 และ URL/auth/TLS บริษัทตาม Q7; ทดสอบกลไกด้วย local HTTP stub/ข้อมูลสังเคราะห์ก่อน ไม่เปลี่ยน schema/bytes ของงานเก่า
- ต่อไป: หลังยืนยัน implement/tests และ check.ps1; source ยังไม่แก้ รอบนี้ไม่ commit/push อัตโนมัติ

### 2026-11-17 — Commit M3.3
- เสร็จ: ตามคำสั่งผู้ใช้ commit โค้ด `prjInsuranceValidationMapping` บน develop เป็น `7014c83` — `feat(mapping): add durable row transformation and encrypted outputs`; รวม 46 ไฟล์ของ M3.3 และ working tree ของ Mapping สะอาดหลัง commit
- หลักฐาน: source ตรงกับชุดที่ผ่าน `./scripts/check.ps1` ล่าสุด (unit **27/27**, integration **113/113**, EF model ผ่าน) ไม่มีการแก้ source หลัง gate; staged whitespace check ผ่าน และไม่มี runtime data/key/company configuration ใน commit
- เสร็จ: อัปเดตหลักฐาน commit ใน plan และ [แบบ M3.3](m3-3-row-transformation-design.md) พร้อม commit เอกสาร 3 ไฟล์รวมแบบข้อมูล runtime; pull เอกสารก่อนแก้แล้ว up to date
- ติด / รอ: Informs T2.6/Q4 และ company dependency Q7 ตามเดิม; M4.1 HTTP sender ยังไม่ทำ
- ต่อไป: M4.1; ยังไม่ push ตามขอบเขตคำสั่ง commit ครั้งนี้

### 2026-11-17 — ปิด M3.3: fenced row transformation และ encrypted durable outputs บน local
- เสร็จ: Row Job worker ทำ claim/lease/recovery/epoch fencing, โหลด pinned raw Source/config/schema/profile, ใช้ shared compiler/evaluator, เติม system envelope และ final schema validation; encrypted Delivery Jobs/delivery outbox/Row Errors/row result commit พร้อมกันตาม M-33; migration `20261117081518_AddRowTransformationOutputs`
- หลักฐาน: `./scripts/check.ps1` ผ่าน build, unit **27/27**, integration **113/113** และ EF model check; M3.3 เพิ่ม unit 5 + integration 18 (repository 4, crypto 2, pipeline 10, Kafka 1, full MTLS 1); ทดสอบ rollback/cancel/timeouts/key loss/stale owner หลัง encrypt/config pin/0..N/cardinality/limits และ Kafka replay/worker สองตัวแล้วไม่มี outputs ซ้ำ
- หลักฐาน: actual import → transform → encrypted outputs ของ MTLS สังเคราะห์ **2,556/2,556** ผ่านกับ pinned synthetic schema รองรับ D-80; cancelled 3 แถวเก็บ null/unparsed และ short ID 11 หลัก 2 แถวคงค่าเดิม; actual schema เก่ามี regression ได้ ContractMissingFileReceivedAt/Blocked; [รายการไฟล์/หลักฐาน M3.3](m3-3-row-transformation-design.md)
- หลักฐาน: local delivery topic `mapping.delivery.requested` 3 partitions/init exit 0; compose syntax และ script key generation แบบ opt-in/ไม่ overwrite/env restoration ผ่าน; worker ยังปิดโดย default ไม่มี key/connection บริษัทใน source และไม่ได้แก้ Informs/Web/demo source
- ติด / รอ: Informs schema/deploy T2.6 ตาม Q4/D-80 ก่อนรับตรวจหรือเปิดส่งจริง; key/Kafka/share บริษัทติดตาม Q7; encrypted storage ของ M4.1 ทำแล้วเฉพาะส่วนที่ row transaction ต้องใช้ ยังไม่มี HTTP sender/delivery attempts และไม่ปิด M4.1/M4.2
- ต่อไป: M4.1 typed Informs HttpClient และ HTTP outcome tests; source ใน `prjInsuranceValidationMapping` รวม 46 ไฟล์ที่เพิ่ม/แก้บนฐาน `9198a54` และเอกสารรอบ M3.3 ยังไม่ commit/push ตามขอบเขตคำสั่งนี้

### 2026-11-17 — เตรียม M3.3 row transformation worker
- เสร็จ: ตรวจ Row Job/transform wake-up ของ M-31, Source/version pin ของ M-32, compiler/evaluator/preview และ pattern worker ของ Informs แบบ read-only; เขียน [แบบ M3.3 พร้อม source batch](m3-3-row-transformation-design.md)
- กำลังทำ: รอยืนยัน source batch สำหรับ claim/lease/recovery, shared compile/evaluate, system envelope/final schema validation, Row Errors และ encrypted durable Delivery Jobs/outbox; ทำ storage ที่ M4.1 ใช้ต่อเพราะ Transformed ต้องมี durable outputs แต่ยังไม่ทำ HTTP/delivery attempts
- ติด / รอ: schema Informs ปัจจุบันยังไม่มี fileReceivedAt ตาม D-80/T2.6 (Q4); worker ต้องเก็บ safe contract diagnostic ไม่ละ field/แก้ pinned schemaเอง; success ของกลไกใช้ synthetic test schema ที่ประกาศชัด และไม่ถือว่ารับตรวจ Informs จริงแล้ว
- ต่อไป: หลังยืนยัน implement + PostgreSQL/Kafka/crypto/failure/replay tests และ check.ps1 ก่อนบันทึกผล; source ยังไม่แก้ และรอบนี้ไม่ commit/push อัตโนมัติ

### 2026-11-17 — Commit และ pull เอกสาร Mapping โดยเก็บงานเดิมครบ
- เสร็จ: commit เอกสารค้าง 12 ไฟล์ถึง M3.2 เป็น `adc17fb`; pull/merge origin/develop สำเร็จเป็น `4c26642` รับหมายเหตุ health probes ของ Dev 1 และ glossary ล่าสุดโดยไม่มี conflict
- หลักฐาน: เทียบ Git content ของเอกสารทั้ง 12 ไฟล์กับ snapshot ก่อน pull แล้วตรงทั้งหมด; ประวัติ local/remote อยู่ครบ glossary ตรง origin/develop และหมายเหตุ Dev 1 ปรากฏครั้งเดียวตรงต้นฉบับ
- สำรอง: branch `backup/mapping-demo-pre-pull-20261117-c852319e` ชี้ commit เอกสารก่อน pull; มีสำเนาไฟล์เดิมพร้อม SHA-256 manifest ใน `.git/codex-backups/mapping-demo-pre-pull-20261117-c852319e/`
- ต่อไป: M3.3; ยังไม่ push ตามขอบเขตคำสั่งครั้งนี้ และไม่เปลี่ยนโค้ด demo หรือ repo โค้ดอื่น

### 2026-11-17 — Commit code Mapping ถึง M3.2
- เสร็จ: ตามคำสั่งผู้ใช้ commit โค้ดใน repo `prjInsuranceValidationMapping` branch `develop` เป็น `9198a54` — `feat(mapping): add configurable preview and durable file intake`; รวม catalog/Source storage/preview/transformation/readers/lookups และ durable file intake/Kafka/archive พร้อม tests/migrations/local config ทั้งหมด 188 ไฟล์
- หลักฐาน: ก่อน commit รัน `./scripts/check.ps1` ผ่าน build, unit **22/22**, integration **95/95** และ EF model check; staged tree ตรงกับโค้ดที่ทดสอบ ไม่มี data/build artifacts/config บริษัทหรือ connection details ที่ผู้ใช้เคยให้ และ working tree ของ repo Mapping สะอาดหลัง commit
- ติด / รอ: ยังไม่ push ตามขอบเขตคำสั่ง commit; เอกสารใน mapping-demo ยังไม่ commit และ pull ยังติด plan.md ที่แก้ค้าง จึงไม่ stash/เขียนทับข้อความ Dev 1
- ต่อไป: M3.3; ใช้ commit `9198a54` เป็นหลักฐาน source ของงานที่ทำไว้ถึง M3.2 โดยไม่ปิด acceptance/คำถามธุรกิจอื่นเพิ่มเติม

### 2026-11-17 — ปิด M3.2: scan/snapshot/import/resume/archive บน local
- เสร็จ: Mapping มี scanner ของ active routes, stable fingerprint/version pin/path claim, durable snapshot/content claim, import Kafka event/receipt/DB wake-up, work lease/recovery, import ครบสาม format และ archive snapshot copy โดยคง input ตาม M-32; เพิ่ม migration `20261117062250_AddFileIntakeRuntime`
- เสร็จ: ผู้ใช้ยืนยันเพิ่ม `SourceStorageRepository.cs` เพื่อแก้ NULL parameter แล้ว; raw fields ใช้ typed PostgreSQL text parameter เก็บ SQL NULL ตามเดิม ไม่เติมค่า/แปลงเป็น empty string
- หลักฐาน: `./scripts/check.ps1` ผ่าน build, unit **22/22**, integration **95/95** และ EF model check; M3.2 เพิ่ม unit 2 + integration 21 (repository 5, filesystem 5, pipeline 6, Kafka 3, Excel 2); เทียบ Excel BIFF5/cp874 สมมติ **2,556 แถว × 19 fields** กับ Source ราย cell รวม NULL และ hosted Kafka pipeline ทั้งสาม format รวม **2,560 แถว** ผ่าน; ไม่มี HTTP delivery
- หลักฐาน: import topic local `mapping.import.requested` มี 3 partitions, broker healthy, init exit 0; script default/explicit-disable/env restoration, compose read-only input/default intake disabled และ whitespace/path-leak checks ผ่าน; [แบบ/รายการไฟล์/หลักฐาน M3.2](m3-2-file-intake-design.md)
- ติด / รอ: Kafka/DB/share/permissions/durability บริษัทติดตาม Q7 ก่อน deployment ตามแนวทางปิด local ของ M3.1; partner business fields/acceptance และ Q9 ยังแยกจากกลไก M3.2 ไม่เดาค่าธุรกิจ; source/เอกสารยังไม่ commit/push และไม่ได้ apply DB บริษัท
- ต่อไป: M3.3 row transformation jobs/lease/recovery/diagnostics; pull ยังติด plan.md ที่แก้ค้าง จึงไม่ stash/เขียนทับงานเดิม รักษาหมายเหตุ Dev 1 ล่าสุดไว้และอ่าน glossary ใหม่จาก origin/develop แล้ว

### 2026-11-17 — M3.2 implementation และทดสอบ pipeline local
- เสร็จ: source batch ที่ผู้ใช้ยืนยันเพิ่ม scanner/accepted fingerprint/work lease, stable snapshot/content claim, import event/consumer/receipt/DB wake-up/recovery, shared Excel import และ archive snapshot copy ตาม M-32; migration `20261117062250_AddFileIntakeRuntime` และ local import topic 3 partitions พร้อม
- หลักฐานระหว่างทำ: build ผ่าน; intake core/pipeline ชุดแรก 15/15 ผ่าน PostgreSQL/temporary filesystem และ hosted Kafka pipeline ครบ Excel (2 fields/2,556 แถว)/CSV/Fixed-width รวม 2,560 แถวผ่านแล้ว; script default/explicit-disable/env restoration และ topic init ตรวจแล้ว; [รายละเอียด M3.2](m3-2-file-intake-design.md)
- ติด / รอ: full-fixture Excel 19 fields พบ NULL parameter bug ใน `SourceStorageRepository.cs` เดิม ซึ่งอยู่นอก source batch จึงขอคำยืนยันเพิ่มไฟล์ก่อนแก้ตาม AGENTS.md; ยังไม่ปิด M3.2 หรืออ้างว่า `check.ps1` ผ่านทั้งชุด
- ต่อไป: ตรวจ consumer rollback/offset/restart, แก้ NULL เมื่อได้รับยืนยันเพิ่มไฟล์ แล้วรัน regression/full gate และบันทึกหลักฐานปิดงาน; ไม่มีการแก้ DB/share บริษัทหรือ commit/push; pull ยังติด plan.md ที่แก้ค้าง อ่าน glossary Dev 1 ใหม่จาก origin/develop แล้วและไม่แก้ไฟล์ Dev 1

### 2026-11-17 — เตรียม M3.2 scan/snapshot/import/resume/archive
- เสร็จ: ตรวจ File Job/claims/active routes/import core, triggers/lease, local Kafka และ pattern hosted service ของ Informs แบบ read-only; เขียน [แบบ M3.2 พร้อม source batch](m3-2-file-intake-design.md) และตัวอย่าง duplicate/restart/changed input
- กำลังทำ: รอยืนยัน source batch ของ scanner + filesystem snapshot/archive + durable file-event wake-up/recovery; จะเชื่อม Excel import เข้ากับ reader/preview เดิมเพื่อให้ MTLS BIFF5/cp874 ใช้งานผ่าน intake ได้ด้วย
- ติด / รอ: ยังไม่แก้ source M3.2; ข้อเสนอ archive เป็น immutable snapshot copy โดยคงต้นฉบับ input และใช้ path pointer กันรับ occurrence เดิมซ้ำ ต้องยืนยันพร้อม source; Q7 บริษัทติดตามก่อน deployment; pull ติด plan.md ที่แก้ค้างจึงไม่ stash/เขียนทับงานเดิม
- ต่อไป: หลังยืนยัน implement และรัน PostgreSQL/Kafka/filesystem failure/recovery tests + `check.ps1` ก่อนบันทึกหลักฐานปิด M3.2; ไม่มีการแก้ DB/share บริษัทหรือ commit/push

### 2026-11-17 — ปิด M3.1 ในขอบเขต Kafka local
- เสร็จ: ปิด M3.1 ตามข้อตกลงผู้ใช้ให้ใช้ Kafka local พัฒนา/ทดสอบต่อ; หลักฐานเดิม `check.ps1` ผ่าน build, unit 20/20, integration 74/74 และ EF model check รวม transaction/offset/redelivery ของ M3.1 แล้ว จึงไม่ต้องรอ environment บริษัทเพื่อปิดงาน local
- ติด / รอ: dependency Kafka บริษัทแยกติดตามใน Q7 และต้องพร้อมก่อนเปิด messaging บน environment บริษัท; ไม่เปลี่ยน contract/ขอบเขต phase 1/วันส่ง และยังไม่ถือว่าบริษัทพร้อมใช้งาน
- ต่อไป: M3.2 และ M3.3; รอบนี้แก้เฉพาะแผน/แบบ M3.1 ไม่แก้ source และไม่รันทดสอบซ้ำ; ยังไม่ commit/push ส่วน pull ยังติด plan.md ที่แก้ค้างและรักษาหมายเหตุ Dev 1 ล่าสุดไว้แล้ว

### 2026-11-17 — M3.1 local Kafka/outbox/inbox ผ่าน failure และ offset tests
- เสร็จ: source batch ที่ผู้ใช้ยืนยันเพิ่ม local Kafka/topic init, OutboxDispatcher และ TransformConsumer ผ่าน MediatR; strict event มี UUID 4 ตัวพร้อม header outbox ID เดิม, invalid outbox เป็น blocked และ diagnostic receipt ไม่เก็บ raw body ตาม M-31
- เสร็จ: migration `20261117042328_AddMappingMessagingInbox` เพิ่ม immutable Inbox Receipt; transaction บันทึก receipt และตั้ง `Pending` Row Job `NextAt` ก่อน commit Kafka offset; duplicate/restart/republish ไม่ reset job/backoff; consumer ยังไม่รัน transform ของ M3.3
- หลักฐาน: `./scripts/check.ps1` ผ่าน build, unit **20/20**, integration **74/74** และ EF model check; M3.1 เพิ่ม unit 3 + integration 14 (PostgreSQL 5, Kafka pipeline 7, configuration 2) รวม broker failure, broker ack แล้ว DB rollback, receipt rollback/offset, poison events, restart และหลาย dispatcher/consumer; local `apache/kafka:4.3.1` healthy/topic `mapping.transform.requested` มี 3 partitions
- ติด / รอ: M3.1 ยังไม่ปิดทั้งข้อเพราะ broker/topics/groups/owner/auth/TLS บริษัทใน Q7 ยังไม่ตกลง; import/delivery handlers เป็น M3.2/M4; messaging ปิดโดย default และ company mode ไม่เปิดให้อัตโนมัติ; ไม่มีการแก้ DB บริษัท
- ต่อไป: M3.2 scan/snapshot/import/resume/archive และ M3.3 transform worker ที่ claim เฉพาะ Pending/NextAt ถึงเวลาแล้ว; [แบบและหลักฐาน M3.1](m3-1-messaging-design.md); source/เอกสารยังไม่ commit/push ตามคำสั่งผู้ใช้ ส่วน pull ยังติด plan.md ที่แก้ค้าง แต่รักษาหมายเหตุ Dev 1 จาก origin/develop ไว้ตรงทุกตัวอักษร

### 2026-11-17 — เตรียม M3.1 local Kafka/dispatcher/consumer
- เสร็จ: อ่าน MessagingOptions/OutboxDispatcher/StageConsumer/DI/compose และ Kafka fixture ของ Informs แบบ read-only; ตรวจ outbox/Row Job ของ Mapping แล้วเขียน [แบบ M3.1](m3-1-messaging-design.md) พร้อม event ตัวอย่างและรายการ source
- กำลังทำ: รอยืนยัน source batch สำหรับ local Kafka/topic init, dispatcher, strict IDs-only event, durable Inbox Receipt/Row Job wake-up และ manual offset/redelivery tests ก่อนเชื่อม transform worker M3.3
- ติด / รอ: ยังไม่แก้ source รอบ M3.1; broker/topics/groups/owner/auth/TLS บริษัทใน Q7 ยังรอ; pull ยังติดงานค้าง plan.md และไม่ได้ stash/เขียนทับข้อความเดิม
- ต่อไป: หลังยืนยัน implement + real PostgreSQL/Kafka failure/replay/offset tests และ `check.ps1`; ไม่เปิดส่ง Informs หรืออ้างว่า transform เสร็จจากการรับ Kafka event

### 2026-11-17 — M2.7 Fixed-width ผ่าน import core และ Source จริง
- เสร็จ: Mapping เพิ่ม `FixedWidthInputLayoutValidator` ร่วมใน reader/validate/preview/import; ตรวจ range/overflow ด้วยเลข 64-bit, รองรับ UTF-8/BOM/cp874, รักษา raw padding/zeroes, ตรวจ bytes/records/columns/field/cells limits และปฏิเสธ NUL/surrogate slice ที่ไม่สมบูรณ์ ตาม M-30
- เสร็จ: `FileImportService` dispatch Delimited/FixedWidth จาก pinned layout โดยใช้ hash/size/lease/read count/Source + Row Job + outbox + checkpoint ทางเดิมของ M-29; ไม่มี schema/migration ใหม่ และปรับ capability reason ตามความพร้อมจริง
- หลักฐาน: `./scripts/check.ps1` ผ่าน build, unit **17/17**, integration **60/60** และ EF model check; เพิ่ม 12 tests (reader 6, PostgreSQL/import 5, API 1) ตรวจ raw/Thai/positions/layout/limits, pinned preview เทียบ Source จริง, cp874, resume/concurrency/stale lease, hash/malformed และ outbox rollback; regression CSV/TSV/MTLS/M2.5 ผ่านทั้งหมด
- ติด / รอ: M2.7 ยังไม่ปิดทั้งข้อ — fields/fixture ของ partner รอ Q5/M-24 และ scope/วันส่งรอ Q9; FixedWidth capability ยังคงปิดจน intake/queued processing ของ M3 พร้อม; M2.3 ยังรอ Excel import และ compiler/evaluator ของ transform worker
- ต่อไป: เชื่อม Excel import และ runtime M3 ตาม source batch ที่ยืนยันถัดไป; [แบบและหลักฐาน M2.7](m2-7-fixed-width-import-design.md); source/เอกสารยังไม่ commit/push และไม่มีการเปลี่ยน DB บริษัท

### 2026-11-17 — เตรียม M2.7 Fixed-width import
- เสร็จ: ตรวจ Fixed-width reader/preview และ import core ล่าสุด; เขียน [แบบ M2.7](m2-7-fixed-width-import-design.md) พร้อมตัวอย่างสมมติและรายการ source; ใช้ schema/transaction/lease จาก M-29 เดิม ไม่ต้องเพิ่ม migration
- กำลังทำ: รอยืนยัน source batch เพื่อเพิ่ม shared validator, cp874/limits/raw-slice diagnostics, dispatch FixedWidth ใน import และ API/PostgreSQL regression tests
- ติด / รอ: source รอบ M2.7 ยังไม่แก้; partner fields/fixture รอ Q5/M-24 และ scope/วันส่งรอ Q9; pull ยังติดงานค้าง plan.md และไม่ได้ stash/ทับข้อความเดิม
- ต่อไป: หลังยืนยัน ลงมือทดสอบ reader → preview/import → Source/Row Job/outbox พร้อม `check.ps1`; ไม่เปลี่ยน M1.2 หรือ contract ใหม่ของ Web

### 2026-11-17 — M2.6 CSV/TSV ผ่าน import core และ Source จริง
- เสร็จ: Mapping ใช้ Delimited layout validator ร่วมกันใน reader/validate/preview/import; แก้ cp874 alias, รักษา CR/LF/CRLF ภายใน quoted field, ใช้ limits bytes/records/columns/field/cells และ diagnostics ตาม physical record; ไม่มี fields ของ partner hardcode
- เสร็จ: `FileImportService` อ่าน pinned version ของ File Job ที่ seal แล้ว ตรวจ hash/size ก่อนเขียน, claim/renew/ตรวจ lease ด้วยเวลา DB, บันทึก read count แยก committed count และ resume จาก checkpoint; Source writer commit registry + raw row + Row Job + transform outbox + checkpoint ใน transaction เดียว พร้อม replay/fencing guards ตาม M-29
- หลักฐาน: migration `20261117024834_AddFileImportJobs` เพิ่ม `mapping.row_jobs`, `mapping.outbox`, import diagnostics และ immutable job/message guards; `./scripts/check.ps1` ผ่าน build, unit **17/17**, integration **48/48**, EF model check; เพิ่ม 16 tests (reader 6, import/PostgreSQL 9, API 1) ครอบคลุม UTF-8/cp874, CSV/TSV/headerless, raw/newlines/pins, rollback ที่ outbox, cancellation/resume, concurrent calls, stale lease, hash/malformed/limits และข้อมูลก่อน migration ที่ไม่มี Row Job
- ติด / รอ: M2.6 ยังไม่ปิดทั้งข้อ — fields/fixture ของ partner รอ Q5/M-24 และ scope/วันส่งรอ Q9; directory intake/snapshot/archive, Kafka dispatcher/consumer และ transform worker ยังเป็น M3; Delimited capability ยัง `available=false` และอธิบายว่า sealed-snapshot import core พร้อมแล้ว ไม่อ้างว่า production processing พร้อม
- ต่อไป: ใช้แกน import นี้ต่อ M2.7/M3 และรับตรวจด้วย partner fixtures เมื่อได้รับ; [แบบและหลักฐาน M2.6](m2-6-delimited-import-design.md); ไม่มีการ apply migration/แก้ DB บริษัท โค้ดและเอกสารยังไม่ commit/push

### 2026-11-17 — เตรียม M2.6 CSV/TSV และแกน import
- เสร็จ: ตรวจ reader/preview/Source persistence ปัจจุบัน และเขียน [แบบ M2.6](m2-6-delimited-import-design.md) พร้อมตัวอย่างสมมติ; ระบุแกน import ที่ต้องมี Row Job/outbox/checkpoint atomic ตามแบบ M1.1 ไม่ใช่ test ที่เรียก Source writer แทน import runtime
- กำลังทำ: เตรียมรายการ source สำหรับ layout validation/limits/raw newline, FileImportService/persistence/lease, Row Job/outbox, migration และ tests ก่อนขอยืนยันตาม AGENTS.md
- ติด / รอ: ยังไม่แก้ source รอบ M2.6; partner fields/fixtures รอ Q5/M-24 และ scope/วันส่งรอ Q9; pull ยังติดงานค้างใน plan.md จึงไม่ stash/เขียนทับงานเดิม หมายเหตุ Dev 1 ล่าสุดจาก origin/develop มีในไฟล์แล้ว
- ต่อไป: หลังยืนยันรายการไฟล์ ลงมือ import core + reader/API/PostgreSQL tests และตรวจ `check.ps1`; M2.6 ยังไม่ติ๊กเสร็จ

### 2026-11-17 — ปิด M2.5 SQL Server Named Lookup
- เสร็จ: Mapping เพิ่ม SQL Server adapter สำหรับ `mtiOriginalPolicyExists@1` / `mtiOriginalLoanExists@1` อ่าน `dbo.MTI_ORIGINAL` ตาม M-28; เชื่อม async lookup เข้ากับ validate/raw preview/file preview พร้อม cancellation, dependency diagnostics และ capability ตามความพร้อมของแต่ละ lookup
- หลักฐาน: `./scripts/check.ps1` ผ่าน build, unit **17/17**, integration **32/32** (รวม SQL Server container 6 tests และ MTLS full fixture), EF model check; SQL Server tests ใช้ข้อมูลสมมติ ตรวจทั้งสอง key sets, Thai collation, SQL parameters/overlength keys, เลขแถวข้ามไฟล์, schema/dependency หาย, raw/file preview และ cancellation
- หลักฐาน UAT (read-only): adapter ทั้งสองชื่อ `available=true`, ค้นด้วย keys สมมติได้ `false` ทั้งสองรายการ; API ชั่วคราว `GET /api/mapping/capabilities` คืน Named Lookup 2 ชื่อและ predicates **17 แบบ**; อ่านเฉพาะ metadata และ query ด้วย keys สมมติ ไม่เปลี่ยนข้อมูลบริษัท
- ติด / รอ: ตอนใช้ SqlClient พบ certificate validation ไม่ผ่าน; ผู้ใช้อนุญาต `Encrypt=True;TrustServerCertificate=True` **เฉพาะ UAT นี้** แล้ว smoke test ผ่าน ไม่ใช้เป็น default ใน adapter; deployment ต้อง provision `ConnectionStrings__MtiOriginalDb` ผ่าน secret/environment ของ environment นั้น และ Q7 dependency อื่นยังรอ
- ต่อไป: import runtime M3 เพื่อปิด acceptance ที่เหลือของ M2.3/M2.6/M2.7; โค้ดและเอกสารยังไม่ commit/push ตามกฎ commit เมื่อสั่งเท่านั้น; ดู [traceability M2.5](m2-5-transformation-traceability.md)

### 2026-11-16 — M2.5 operators และ lookup contract
- เสร็จ: ย้าย value expressions 13 แบบ, predicates 17 แบบ, conversion targets 7 ชนิด, `sourceWhen`/`emitWhen`, Named Transform `formatRegNo@1` และเพิ่ม contract/fail-closed สำหรับ Named Lookup สองรายการ; เพิ่ม [traceability](m2-5-transformation-traceability.md) และแก้ `GET /capabilities` ให้รายงาน predicate 15 แบบที่ไม่พึ่ง lookup โดยยังไม่ประกาศสอง lookup predicates
- หลักฐาน: `./scripts/check.ps1` ผ่าน build, unit **13/13**, integration **26/26** (รวม MTLS full fixture), EF model check; test ใหม่ครอบคลุม operator, conversion, branch, limit, lookup dependency และเวลา deterministic
- ติด / รอ: M2.5 ยังไม่ปิด เพราะ `mtiOriginalPolicyExists@1`/`mtiOriginalLoanExists@1` ยังไม่มี lookup adapter และ source identity ที่ตกลงกับ Q2/Q7; ใน runtime ไม่ลงทะเบียน resolver จึงได้ `LookupDependencyUnavailable` และ `namedLookups=[]`
- ต่อไป: ตกลง Q2/Q7 เรื่องแหล่งข้อมูล/adapter แล้ว implement lookup จริงและทดสอบกับ dependency; working tree ยังไม่ commit/push ตามคำสั่งผู้ใช้

### 2026-11-16 — ปิด M2.4 MTLS full fixture กับ schema จริง
- เสร็จ: Mapping integration test อ่านไฟล์ MTLS สมมติแบบ BIFF5/cp874 ครบ 2,556 แถวด้วย Excel adapter, compile program JSON ของ MTLS จาก M2.2, สร้าง envelope `pa-inform/1` สมมติ และตรวจทุก output ด้วย `JsonSchema.Net 7.2.3`/JSON Schema ที่คัดลอกจาก Informs แบบ read-only; ตรวจแถว C และเลขบัตร 11 หลักโดยไม่แก้ Source ดิบ
- หลักฐาน: fixture SHA-256 `CDF70EFDB67641764995D525D97FCB9752F01441CFD9F586A2AD558DD6C29E99`; schema SHA-256 `A351C48FDBF85E08F319596F91C745B102CEFDC9372A131AD886CD975A3D266B`; **2,556 records → 2,556 outputs `default` → schema ผ่านทั้งหมด**, C 3 แถวเก็บ `-293` ใน `extras.unparsed`, เลขบัตร 11 หลัก 2 แถวยังเป็นค่าเดิม; full-file test ใช้ **4,510 ms**; `./scripts/check.ps1` ผ่าน build, unit **7/7**, integration **25/25**, EF model check
- ติด / รอ: ผลนี้ยืนยันโครง/ชนิดตาม schema snapshot ที่ hash ข้างบนด้วยข้อมูลสมมติ; ยังไม่ใช่ business validation ของ Informs, HTTP delivery หรือ UAT partner อื่น; Q5 เรื่อง field สัดส่วน/default และ bindings เพิ่มเติมรอผู้ใช้ส่ง; schema เปลี่ยนต้องตรึง/ตรวจ snapshot ใหม่
- ต่อไป: import runtime M3 เพื่อกลับมาปิด M2.3 และ M2.6/M2.7; working tree Mapping/เอกสารยังไม่ commit/push ตามคำสั่งผู้ใช้

### 2026-11-16 — M2.3 validate/preview/file preview ชุดแรก
- เสร็จ: Mapping เพิ่ม `POST /api/mapping/mapping-configs/{id}/{validate,preview,preview-file}` สำหรับ pinned version หรือ draft; ใช้ compiler/evaluator เดียวกันกับ raw preview และ file preview; เพิ่ม Delimited CSV/TSV กับ Fixed-width adapters เพื่อให้ route เดียวกันอ่าน 3 formats, โดย MTLS `.xls` preview ตัดที่ 50 records; limits เริ่มต้น body 1 MiB, file 20 MiB, 50 records และ Excel 100,000 cells
- หลักฐาน: API/reader tests ตรวจ Excel, CSV, TSV, Fixed-width, quote หลายบรรทัด, physical position, malformed/short/overlap, extension ผิด `400` และ file เกิน limit `413`; PostgreSQL Testcontainers ตรวจ preview ด้วย pinned config แล้วจำนวน File Job/Source row/claims/audit ไม่เพิ่ม; `./scripts/check.ps1` ผ่าน build, unit **7/7**, integration **24/24**, EF model check
- กำลังทำ: M2.3 ยังไม่ปิด — import runtime ยังไม่สร้าง จึงยังพิสูจน์ว่า import ใช้ parser/compiler ชุดเดียวกันไม่ได้; M2.6/M2.7 ยังรอ import/Source raw/UAT acceptance ของ partner อื่น; `GET /capabilities` คง `available=false` เพราะ import/delivery ยังไม่พร้อม และข้อความ reason ของ reader ต้องปรับใน batch source ถัดไป
- ต่อไป: M2.4 full MTLS/schema test และต่อ import runtime ตาม M3; working tree Mapping/เอกสารยังไม่ commit/push ตามคำสั่งผู้ใช้

### 2026-11-16 — ปิด M2.2 Transformation Program ส่วน MTLS
- เสร็จ: Mapping เพิ่ม compiler/evaluator ที่รับ Source fields และ target paths จาก versioned Program JSON; รองรับ `source`, `literal`, `case`, `defaultIfBlank`, `convert`, nested arrays, conditional items และ fallback items; config ตัวอย่าง MTLS เก็บ P/C, constants และ default ผู้รับประโยชน์ใน JSON ไม่ hardcode ใน evaluator
- หลักฐาน: unit tests **5/5** ตรวจ P/C, nested insured/beneficiaries, `-293 → null + extras.unparsed`, default จาก config, unknown transaction ที่ไม่เดา fallback, undeclared Source/target paths และ JSON option ผิด; เทียบ program แบบ read-only กับ JSON Schema จริงของ Informs แล้ว target paths **27/27 มีใน schema**; `./scripts/check.ps1` ผ่าน build, unit 5/5, integration **17/17** และ EF model check
- ติด / รอ: M2.2 ยังไม่ผูก API/งาน import; การตรวจ payload เต็มไฟล์กับ `pa-inform/1` schema จริงอยู่ M2.4; Q5 field สัดส่วนและ business bindings เพิ่มเติมรอผู้ใช้ส่ง; operators ที่เหลืออยู่ M2.5
- ต่อไป: M2.3 validate/preview แล้ว M2.4 full fixture; working tree Mapping/เอกสารยังไม่ commit/push ตามคำสั่งผู้ใช้

### 2026-11-16 — ปิด M2.1 Input Reader และ Excel adapter
- เสร็จ: Mapping เพิ่ม `IInputReader<TLayout>` contract กลาง, Excel `.xls` adapter และ diagnostics/limits; อ่าน BIFF5 sheet แรก, header/field bindings จาก config, แยก `sourceRowNo` กับ physical row, แปลงตัวเลขแบบ invariant และแก้ข้อความไทย cp874 ที่ CODEPAGE ในไฟล์ระบุ 1252
- หลักฐาน: fixture สมมติ `MTLSPA0220261201.xls` SHA-256 `CDF70EFDB67641764995D525D97FCB9752F01441CFD9F586A2AD558DD6C29E99`; อ่านครบ **2,556 records**, header 19, P 2,553/C 3, ค่า `180`/`-293` และภาษาไทยถูกต้อง; `./scripts/check.ps1` ผ่าน build, integration tests **17/17**, EF model check
- ติด / รอ: Excel `.xlsx` ยังไม่อยู่ใน acceptance รอบนี้; Delimited/Fixed-width แยก M2.6/M2.7; API preview/import แยก M2.3/M3; Q9 partner อื่นใน UAT รอ Dev 1/PM ลงผล
- ต่อไป: M2.2 Transformation Program; working tree Mapping/เอกสารยังไม่ commit/push ตามคำสั่งผู้ใช้

### 2026-11-16 — ปิด M1.5 Source catalog/API
- เสร็จ: Mapping เพิ่ม `GET/POST /api/mapping/source-definitions` และ `GET /{id}`, `POST /{id}/columns` ผ่าน CQRS; field metadata มี key/ordinal/displayName/logicalTypeHint โดย physical columns เป็น `text NULL`; create/append ทำ metadata + DDL + audit ใน PostgreSQL transaction เดียว, expected row version และ request ID กัน concurrent/double action; ไม่คืนชื่อ physical table ให้ browser
- หลักฐาน: PostgreSQL/API integration tests **15/15 ผ่าน** รวม concurrent create/append ได้ผู้ชนะหนึ่งราย, DDL ล้มเหลวแล้ว version/audit ไม่ค้าง, schema columns ตรง metadata, idempotent request และ field ชน system column ได้ `400`; `./scripts/check.ps1` ผ่าน build/test/EF model check
- ติด / รอ: Web editor อยู่ M5.3; Q7 สิทธิ์ DDL ของ DB บริษัทต้องตรวจใน environment จริง; Q9 scope partner อื่นใน UAT รอ Dev 1/PM; Dev 1 ตอบ Q4 เป็น D-80 แล้ว
- ต่อไป: M2.1/Input Reader; working tree Mapping/เอกสารยังไม่ commit/push

### 2026-11-16 — ปิด M1.4 contract/profile snapshot และ capability catalog
- เสร็จ: Mapping เพิ่ม Informs schema client, immutable contract/profile snapshot, path catalog และ `GET /api/mapping/{contracts/pa-inform/1,destination-profiles,capabilities}`; pinned snapshot อ่านด้วย ID เดิมโดยไม่ fetch latest แทน; profile response ไม่คืน URL/secret; capability ของ reader/transform/delivery ยัง `available=false` ตาม code ที่มีจริง
- หลักฐาน: HTTP fake + PostgreSQL + API integration tests รวม **11/11 ผ่าน** และ `./scripts/check.ps1` ผ่าน; เรียก `ContractPathCatalog` กับ schema file จริงของ Informs แบบ read-only ได้ **40 paths** รวม `inform.insureds[]`, `inform.insureds[].packageCode`, `inform.insureds[].beneficiaries[].seq`; SHA-256 ของไฟล์ `a351c48fdbf85e08f319596f91c745b102cefdc9372a131ad886cd975a3d266b`
- ติด / รอ: Informs local :5176 ไม่ตอบ จึงยังไม่มี end-to-end HTTP test กับ service ที่รันจริง; Q9 scope partner อื่นใน UAT รอ Dev 1/PM
- ต่อไป: M1.5; working tree repo Mapping/เอกสารยังไม่ commit/push

### 2026-11-16 — ปิด M1.3 EF persistence และ immutable Source storage
- เสร็จ: repo Mapping เพิ่ม `AddMappingRuntime` migration อีก 6 ตาราง รวม catalog/runtime **13 ตาราง + EF history** ใน schema `mapping`; Source physical table สร้างจาก Definition UUID และเพิ่มได้เฉพาะ nullable `text` columns ใน transaction เดียวกับ version metadata; raw row + registry + File Job checkpoint commit ด้วยกัน; immutable trigger ป้องกันแก้ version/Source/content claim/audit และ guard File Job pin/seal; config SaveVersion/activate/disable ใช้ expected row version, activate lock control row และดึง extension จาก pinned Input Layout
- หลักฐาน: PostgreSQL 15 Testcontainers integration tests **7/7 ผ่าน** รวม raw row เดิมหลังเพิ่ม field, unknown field rollback, content claim unique/immutable, old Config Version/File Job pin, concurrent activate ที่ได้ `Applied` 1 กับ `RowVersionConflict` 1; `./scripts/check.ps1` ผ่าน build/test/EF model check และ `public` ไม่มีตาราง
- กำลังทำ: M1.4/M1.5 แยกต่อ — capability/compiler/parser readiness, Source API/DDL audit และ Row Job/outbox ยังไม่ได้เปิดใช้งาน; Q9 ขอบเขต partner อื่นใน UAT รอ Dev 1/PM
- ต่อไป: M1.4 แล้ว M1.5; working tree repo Mapping/เอกสารยังไม่ commit/push ตามคำสั่งผู้ใช้

### 2026-11-16 — M1.3 catalog persistence ชุดแรก
- เสร็จ: repo Mapping เพิ่ม entities/EF configurations/migration `AddMappingCatalog` สำหรับ 7 ตาราง catalog ใน schema `mapping` พร้อม composite FK, row version และ trigger ห้ามแก้ immutable versions/snapshots; PostgreSQL 15 Testcontainers integration tests **4/4 ผ่าน** และ `./scripts/check.ps1` ผ่าน build/test/model check ด้วย .NET 10; ไม่มีตาราง `public`
- กำลังทำ: M1.3 ยังไม่ปิด — ต้องต่อ File Jobs/Source registry/physical raw rows, activation transaction และ concurrent activate tests; ยังไม่ทำ API/Web หรือแตะ Informs
- ติด / รอ: source ชุดถัดไปต้องแจ้งรายการไฟล์/รอยืนยันใหม่ตาม AGENTS.md; Q9 ขอบเขต partner อื่นใน UAT รอ Dev 1/PM ลง D-xx; Q5 fields/fixtures partner อื่นรอผู้ใช้ส่ง
- ต่อไป: ปิดส่วนที่เหลือของ M1.3 ด้วย source batch ใหม่หลังยืนยัน; working tree ทั้ง repo เอกสารและ Mapping ยังไม่ commit/push

### 2026-11-16 — partner อื่นต้องเปิดใช้ใน UAT
- เสร็จ: ผู้ใช้ตอบ Q9 ว่า **partner อื่นจะเปิดใช้ใน UAT**; ขอบเขตที่ขอจึงเป็น API/Web/runtime สำหรับ Excel, Delimited (CSV/TSV), Fixed-width และ config ของหลาย partner ที่ใช้งานจริง ไม่ใช่เพียง parser demo/fixture สมมติ
- กำลังทำ: เตรียมเกณฑ์ตรวจรับและลำดับ implement แบบหลาย partner/format โดยไม่ hardcode fields ของ MTLS
- ติด / รอ: ขอ Dev 1/PM ลงผลแก้ D-01/D-70 และวันส่งใน `rules.md`; รายชื่อ partner, field definitions และไฟล์ตัวอย่างสมมติของแต่ละงานยังรอข้อมูลจากผู้ใช้ตาม M-24/Q5; M1.2 route/DTO review ยังเลื่อนตามคำขอเดิม
- ต่อไป: หลังขอบเขต/วันส่งตกลงแล้ว แจ้งรายการไฟล์ source ของ API/Web/reader และรอยืนยันตาม AGENTS.md ก่อนแก้; เอกสารยังไม่ commit/push

### 2026-11-16 — API/หน้า Mapping ต้องรองรับหลาย format
- เสร็จ: ผู้ใช้แก้ขอบเขตว่า MTLS เป็นเพียงตัวอย่างหนึ่ง; รอบแรกต้องรองรับ **Excel, Delimited (CSV/TSV) และ Fixed-width** จริงผ่าน API/หน้า Web เดียวกัน → [แบบหลาย format](multi-format-api-web-design.md), M-27; เลิกใช้ข้อเสนอเลื่อน Delimited/Fixed-width ใน Q2 เป็นแผนปัจจุบัน
- กำลังทำ: M1.2 ปรับ contract ให้ `inputLayout.kind`, capabilities, preview/jobs และ UI เป็นกลางต่อ format; M2.1/M2.6/M2.7 แยก reader adapters, M5.3 ครอบคลุมหลาย config/format; ยังไม่แก้ source code/Web
- ติด / รอ: Q9 ต้องให้ Dev 1/PM ตกลงการขยายจาก D-70 เรื่อง UAT/partner/เป้าส่ง; M1.2 route/DTO review ยังคงเลื่อนตามผู้ใช้; Q4/Q5/Q7 ที่เหลือคงเดิม
- ต่อไป: ยืนยัน Q9 กับ Dev 1 แล้วจัดชุด source/API/Web files เพื่อขอยืนยันก่อน implement ตาม AGENTS.md; ไม่ commit/push จนผู้ใช้สั่ง

### 2026-11-16 — รวม fixture สมมติและแก้ข้อเท็จจริงไฟล์ MTLS
- เสร็จ: ดึง commit ของ Dev 1 ที่เพิ่ม [fixture MTLS สมมติ 2,556 แถว](fixtures/README.md) และแก้ [mtls-file.md](mtls-file.md) ว่าไฟล์จริงเป็น BIFF5/stream `Book`, CODEPAGE 1252 แต่ข้อความไทยเป็น cp874; รวมข้อความ Q7 ของ Dev 1 กับคำตอบ Dev 2 โดยไม่ทับกัน; ปรับร่าง API/แผน M2 ให้ใช้ BIFF5
- กำลังทำ: M1.2 review ยังเลื่อนตามคำขอผู้ใช้; M2.1 ใช้ fixture สมมติได้เมื่อเริ่ม implement; กำลัง sync commit/push งานเอกสาร
- ติด / รอ: Q7 เฉพาะ dependency บริษัท/keys/share ที่เหลือ, Q4/Q5 และ review route/DTO; ไม่ใช้ไฟล์จริง `docs/MTLSPA0220261009.xls` ทดสอบ
- ต่อไป: ตรวจ diff/links แล้ว push `develop`; M1.3 source ต้องแจ้งรายการไฟล์และรอยืนยันก่อนแก้

### 2026-11-16 — เลื่อน review M1.2 ตามผู้ใช้
- เสร็จ: ผู้ใช้ขอ review [ร่าง Mapping API/DTO contract](mapping-api-contract.md) ภายหลัง; คง prefix `/api/mapping` ที่ยืนยันแล้ว และคง M1.2 เป็นงานค้าง ไม่ตีความว่าร่าง route/DTO ได้รับอนุมัติ
- กำลังทำ: เตรียม M1.3 เฉพาะ entity/EF persistence และ immutable Source storage ภายใน Mapping ตาม M1.1; ยังไม่แก้ source code หรือ API route ที่รอ review
- ติด / รอ: M1.2/Q8 ส่วน route/DTO ที่กระทบ Web/contract boundary รอ Dev 1 review ภายหลัง; Q4/Q5/Q7 คงเดิม
- ต่อไป: ระบุรายการไฟล์ source ของ M1.3 และรอยืนยันตาม AGENTS.md ก่อนแก้; เอกสารยังเป็น working tree ไม่ commit/push

### 2026-11-16 — เริ่ม M1.2 และยืนยัน API prefix
- เสร็จ: ผู้ใช้ยืนยัน Q8 ให้ใช้ `/api/mapping`; จัดทำ [Mapping API/DTO contract](mapping-api-contract.md) ฉบับร่าง มี routes, field paths, BaseResponse, version conflict, pagination, action guards, preview limits และตัวอย่าง request-response โดยไม่แก้ source
- กำลังทำ: M1.2 รอ review แบบ API ที่กระทบ Web/contract boundary; M0.2 ยังไม่ปิด
- ติด / รอ: Q4 วิธีใช้เวลารับไฟล์ใน Informs, Q5 fields จากผู้ใช้, Q7 fixture/keys/dependencies, Q8 ส่วน route/DTO ของ Dev 1; preview limit ต้องพิสูจน์กับ fixture ที่รับรอง
- ต่อไป: ตรวจแบบ M1.2 กับ Dev 1 และระบุไฟล์ source ให้ยืนยันก่อน implement; เอกสารยังเป็น working tree ไม่ commit/push

### 2026-11-16 — ปิดแบบภายใน Mapping ของ M1.1
- เสร็จ: **M1.1** เติม [แบบข้อมูล/runtime §8](data-runtime-design.md#8-ข้อกำหนด-technical-สำหรับ-implement-m11) ครบ types/PK/FK/unique/indexes, raw storage, version pin, states/counts, transaction/lease/global budget และ attempt guards; ตรวจข้อบังคับด้วย PostgreSQL **15.20 local :5433** ผ่าน **29 checks** รวม **6 concurrent sessions** → [หลักฐาน SQL/ผล/ข้อจำกัด](m1-1-validation.md); ปิดแบบภายใน Mapping ตาม M-26 และบันทึก Q6/M-25 ตามคำตอบผู้ใช้
- กำลังทำ: เตรียม M1.2 API/DTO/action contract จากแบบที่ปิดแล้ว; M0.2 ยังไม่ปิด และยังไม่แก้ application source/test/Compose
- ติด / รอ: Q4 วิธีใช้ FileJob.CreatedAt ใน Informs, Q5 business fields ที่ผู้ใช้จะส่ง, Q7 dependencies/fixture และ Q8 API prefix; ไม่ใช้ผล proof schema ย่อแทน EF migrations/application integration tests ใน M1.3/M1.5 หรือผล Kafka/HTTP จริง
- ต่อไป: M1.2 พร้อมตัวอย่าง request-response; ก่อนเริ่ม source ชุด M1.3/M1.5 ต้องระบุไฟล์ให้ยืนยันตาม AGENTS.md; เอกสารยังเป็น working tree ไม่ commit/push

### 2026-11-13 — ออกแบบกลไกกลางและพัก business fields (M0.2 / M1.1 / M-24)
- เสร็จ: บันทึกคำสั่งผู้ใช้ให้ข้ามเรื่อง field/สัดส่วนใน Q5 ไว้รอส่งรายละเอียด และยึดการออกแบบกลางที่ครอบคลุมหลายรูปแบบ → M-24; จัดทำ [ร่างข้อมูลและ runtime](data-runtime-design.md) มี Module/interface, identities/ตาราง, transaction/lease, state/counts และ scenario coverage S01–S36
- กำลังทำ: M1.1 มีร่างสำหรับ review แล้ว แต่ยังไม่ปิดแบบ DDL/API หรือทำเครื่องหมายเสร็จ; กลไกไม่ผูก business field/จำนวน columns/outputs ของ MTLS กับทั้งระบบ และ MTLS ยังเป็นกรณีแรกตามแผนเดิม
- ติด / รอ: fields/defaults จริงจากผู้ใช้ใน Q5, D-23 ข้าม service ใน Q4, duplicate-content policy Q6, dependencies Q7, API contract Q8 และ Web design; รายละเอียด FK/raw types/lease-budget ต้อง review ก่อน implement
- ต่อไป: review แบบ M1.1 และลง M1.2 เป็น generic DTO/state/action contract โดยไม่ถาม field ทีละรายการต่อในรอบนี้; ก่อนแก้ source ยังต้องระบุไฟล์และรอยืนยันตามเดิม

### 2026-11-13 — ใช้ CreatedAt ของ File Job เป็นเวลารับไฟล์ (M0.2 / M-23)
- เสร็จ: บันทึกคำชี้แจงผู้ใช้เรื่อง Q4: ใช้ `CreatedAt` ของ File Job ฝั่ง Mapping เป็นเวลารับไฟล์ครั้งแรก ไม่เพิ่ม field เวลาแยกในตาราง → M-23; ปรับแบบข้อมูลในแผน API ให้ตรงกัน
- กำลังทำ: M0.2 รูปแบบการเก็บเวลาฝั่ง Mapping ชัดเจนแล้ว; ยังไม่แก้ source/DB
- ติด / รอ: Q4 ยังรอ Dev 1 ตัดสินวิธีรับ/ใช้ `FileJob.CreatedAt` ใน Informs เพราะ schema ปัจจุบันยังไม่มี field metadata รับเวลานี้ ไม่ถือว่าการใช้ CreatedAt ฝั่ง Mapping เปลี่ยน ReceivedAt ของ Informs แล้ว
- ต่อไป: ถาม Q5.1 เรื่องสัดส่วนผู้รับประโยชน์ 100% ที่ schema ไม่มี field; Q5 ส่วนค่าที่สร้าง payload ไม่ได้/Q6/Q7/Q8 ยังรอตามเดิม

### 2026-11-13 — ยังไม่เปิด changed-payload reprocess/resend (M0.2 / M-22)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q3 ข้อ 1: ช่วงแรกยังไม่ส่ง payload ใหม่จาก Source แถวเดิมหลังแก้ config; config ใหม่ใช้กับไฟล์ที่รับใหม่ ส่วน retry งานเดิมใช้ bytes/key เดิมภายใน 6 attempts → M-22; ปิด Q3 สำหรับขอบเขตช่วงแรกแบบเลื่อน feature
- กำลังทำ: M0.2 ปิดประเด็น contract gaps ที่เหลือก่อนแบบ M1.1/M1.2; ยังไม่แก้ source/DB
- ติด / รอ: Q4 เรื่องเวลารับไฟล์ข้าม service, Q5 ช่องว่าง business/schema, Q6 duplicate-content policy, dependencies ใน Q7 และ API contract ใน Q8; key/lineage สำหรับ changed-payload resend ในอนาคตยังไม่ได้ออกแบบ/อนุมัติ
- ต่อไป: ถาม Q4 เรื่องข้อเสนอให้ Informs รับและใช้เวลารับไฟล์ครั้งแรกจาก Mapping ตาม D-23

### 2026-11-13 — ปลายทางช่วงแรกเป็น Informs API (M0.2 / M-21)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.7 ข้อ 1: ทำปลายทาง Informs API ก่อน และเก็บ LegacyTable/JsonFile/DatabaseTable เป็น backlog → M-21; ปรับ feature matrix/capability scope ในแผน API
- กำลังทำ: M0.2 แนวทางหลักของ Source/Web/delivery และปลายทางช่วงแรกมีคำตอบแล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source/DB
- ติด / รอ: changed-payload reprocess/resend ใน Q3, contract gaps ใน Q4/Q5, duplicate-content policy ใน Q6, dependency ใน Q7 และ API contract ใน Q8; รายละเอียดแบบข้อมูล/API ยังต้องจัดทำใน M1.1/M1.2
- ต่อไป: ถาม Q3 เรื่องการส่ง payload ใหม่จากแถวเดิมหลังเปลี่ยน config เพื่อคง idempotency/lineage ตาม D-44

### 2026-11-13 — หยุด retry เมื่อครบ 6 attempts (M0.2 / M-20)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.6 ข้อ 2: หยุดที่ 6 attempts และไม่เปิด retry หลังครบเพดานผ่านหน้า Web → M-20; ปรับ API/action guard ให้คุมเพดานเดียวกับ worker และปิดประเด็นโควตาเพิ่มหลังครบเพดาน
- กำลังทำ: M0.2 ขอบเขต Source/Payload inspection และ manual retry หลังครบเพดานมีคำตอบแล้ว; ยังไม่แก้ source/DB
- ติด / รอ: feature scope ส่วนที่เสนอเลื่อนใน Q2, changed-payload reprocess ใน Q3, contract gaps ใน Q4/Q5 และ dependency ใน Q7; Web design ยังรอ
- ต่อไป: ถาม Q2.7 เรื่องปลายทางในช่วงแรกว่าจะทำเฉพาะ Informs API และเก็บ destination อื่นเป็น backlog ก่อนหรือไม่

### 2026-11-13 — ดู Delivery Payload ที่บันทึกจริง (M0.2 / M-19)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.5 ข้อ 1: เปิดดู JSON Delivery Payload แบบ read-only จาก payload ที่บันทึกไว้จริง รวมกรณี Informs reject เพื่อเทียบกับ Source → M-19; ปรับแผน API/Web scope
- กำลังทำ: M0.2 ขอบเขต Source/Delivery Payload inspection มีคำตอบแล้ว; รายละเอียด DTO/availability ลงใน M1.2 และ layout/interaction ยังรอ Web design ยังไม่แก้ source/DB
- ติด / รอ: กติกา manual retry หลังครบเพดาน/feature scope ส่วนที่เหลือใน Q2, Q3–Q8 และ dependency encryption keys ใน Q7
- ต่อไป: ถาม Q2.6 เรื่อง manual retry หลังครบ 6 attempts โดยคงประวัติและ bytes/key เดิม

### 2026-11-13 — ดู Source ดิบรายแถวแบบ read-only (M0.2 / M-18)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.4 ข้อ 1: มี Source view รายแถวแบบ read-only เปิดจากรายการไฟล์/แถว พร้อม config version และสถานะส่ง → M-18; ปรับแผน API/Web scope และหลักฐานตรวจรับเรื่องยอดข้อมูล
- กำลังทำ: M0.2 ขอบเขต Source inspection ตกลงแล้ว; layout/interaction และ DTO/count definitions ลงรายละเอียดภายหลัง ยังไม่แก้ source/DB
- ติด / รอ: การดู Delivery Payload แยกยังไม่ถือว่าตกลง, retry หลังครบเพดาน/feature scope ส่วนที่เหลือใน Q2, Q3–Q8 และ Web design
- ต่อไป: ถาม Q2.5 เรื่องดู JSON Delivery Payload ที่ Mapping เตรียมส่ง Informs แบบ read-only เพื่อเทียบกับ Source

### 2026-11-13 — Source schema เพิ่ม column ได้อย่างเดียว (M0.2 / M-17)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.3 ข้อ 1: เพิ่ม column ใน Source Table เดิมได้ แต่ห้ามลบ/rename/เปลี่ยนชนิด field เดิม; กรณีเปลี่ยนแบบนั้นสร้างตารางใหม่ → M-17; ปรับแผน API/เกณฑ์ตรวจรับและขอบเขต M1.5
- กำลังทำ: M0.2 รูปแบบ Source storage/วิธีสร้างตาราง/กติกาแก้ schema มีคำตอบแล้ว; รายละเอียด definition pin/constraints/concurrency ลงใน M1.1 ต่อไป ยังไม่แก้ source/DB
- ติด / รอ: Source/payload inspection และ feature scope ส่วนที่เหลือใน Q2, contract gaps ใน Q4/Q5 และ dependencies ใน Q7; layout/interaction ยังรอ Web design
- ต่อไป: ถาม Q2.4 เรื่องหน้าดู Source ดิบแบบ read-only แยกจาก delivery log

### 2026-11-13 — สร้าง Source Table ผ่านหน้า Mapping (M0.2 / M-16)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.2 ข้อ 1: กำหนด fields ผ่านหน้า Mapping แล้วระบบสร้าง physical Source Table → M-16; เพิ่ม M1.5 และปรับ Table Catalog/API/Web scope ในแผน API
- กำลังทำ: M0.2 ปิดรายละเอียดการดูแล Source schema; ทำส่วนที่ MTLS ใช้ก่อนตามลำดับเดิม และยังไม่แก้ source/DB
- ติด / รอ: กติกาเปลี่ยน Source schema หลังสร้างตาราง, definition pin/DDL permissions และ Web design; การเลือกสร้างผ่าน Web ยังไม่อนุมัติลบ/rename/เปลี่ยนชนิด columns หรือ Table admin ทั้งชุด
- ต่อไป: ถาม Q2.3 เรื่องการเปลี่ยน fields ของ Source Table โดยคงหลักฐานและ config version เดิม

### 2026-11-13 — Physical Source Table แยกตาม config (M0.2 / M-15)
- เสร็จ: บันทึกคำตอบผู้ใช้ Q2.1 ข้อ 2: ใช้ physical Source Table แยกตาม config → M-15; ปรับ feature matrix/แบบข้อมูล/เกณฑ์ตรวจรับในแผน API แทนข้อเสนอตารางกลาง + JSONB เดิม
- กำลังทำ: M0.2 ปิดรายละเอียด Source storage เพื่อทำแบบ M1.1; ยังไม่แก้ source code/DB หรือสร้างตารางจริง
- ติด / รอ: วิธีสร้าง/ดูแล Source Table, schema evolution เมื่อ config version เปลี่ยน, การ pin table definition และสิทธิ์ DDL บริษัท ต้องลงรายละเอียดต่อ; scope ของ Table admin ผ่าน Web ยังไม่ถือว่าตกลง
- ต่อไป: ถาม Q2.2 ว่าต้องสร้าง/เพิ่ม column ของ Source Table ผ่านหน้า Mapping หรือให้ Dev เตรียม migration/script ก่อน

### 2026-11-13 — เริ่ม Kafka local ผ่าน Docker (M0.2 / M-14)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 9.2: เริ่ม Kafka local ผ่าน Docker เพื่อพัฒนา/ทดสอบระหว่างรอ broker/topics บริษัท → M-14; ปรับแผน API ให้แยกการเลือก environment ออกจากการ provision จริง
- กำลังทำ: M0.2 แนวทางหลักของ kickoff ข้อ 1–9 มีคำตอบผู้ใช้แล้ว; ต่อคำถาม Q2–Q8 ทีละข้อ โดยยังไม่ปิด M0.2 และยังไม่แก้ source/Compose
- ติด / รอ: รายละเอียด broker/topics/groups/owner บริษัทใน Q7, ช่องว่าง D-23 ใน Q4 และขอบเขต/contract/fixture ส่วนที่เหลือ; Kafka local ยังไม่ได้ถูกเพิ่มหรือเริ่มรันในงานนี้
- ต่อไป: ถาม Q2.1 เรื่องรูปแบบ Source storage ก่อนปิดแบบข้อมูล M1.1

### 2026-11-13 — Kafka สำหรับงานภายใน Mapping (M0.2 / M-13)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 9.1: ใช้ Kafka + transactional outbox สำหรับงานภายใน Mapping ตาม pattern Informs → M-13; ปรับแผน API โดย Mapping → Informs ยังเป็น HTTP ตาม D-03
- กำลังทำ: M0.2 แนวทางหลักของ kickoff ข้อ 1–9 มีคำตอบผู้ใช้แล้ว แต่ dependency/topics/groups และประเด็น contract/scope ที่เหลือยังไม่ปิด; ยังไม่แก้ source code
- ติด / รอ: การเตรียม Kafka local และรายละเอียด broker/topics/groups/owner ของบริษัทใน Q7; Q4 เรื่องเวลาข้าม service และ Q2–Q8 ส่วนที่เหลือยังรอข้อสรุป
- ต่อไป: ถาม Q1 / kickoff ข้อ 9.2 ว่าจะเริ่ม Kafka local ระหว่างรอ infrastructure บริษัทหรือมี broker บริษัทให้ใช้แล้ว

### 2026-11-13 — ขอบเขตหน้า Mapping ช่วงแรก (M0.2 / M-12)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 8: หน้า Mapping มี Config + ผลการส่ง + retry ตามสถานะ → M-12; ปรับแผน API และคำตอบส่วน Web scope ใน Q2
- กำลังทำ: M0.2 ขอบเขตหน้า Mapping มีคำตอบแล้ว; layout/interaction และรายการย่อย M5.3 ยังรอ Web design ไม่เริ่มแก้ source code
- ติด / รอ: Q1 ข้อ 9 และประเด็นที่เหลือใน Q2–Q8; retry หลังครบ 6 attempts/raw inspection/reprocess ยังไม่ถือว่าตกลงจากการเลือก Web scope นี้
- ต่อไป: ถาม Q1 / kickoff ข้อ 9 เรื่อง Kafka สำหรับงานภายใน Mapping; Mapping → Informs ยังคง HTTP ตาม D-03

### 2026-11-13 — กลุ่มผู้ใช้งาน delivery log (M0.2 / M-11)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 7.2: delivery log ใช้โดยทีม Mapping / Dev / Ops → M-11; ปรับแผน API และบันทึกคำตอบส่วนผู้ดู log ใน Q7
- กำลังทำ: M0.2 ชุดข้อมูลและกลุ่มผู้ใช้งาน delivery log มีคำตอบแล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 8–9 และประเด็นที่เหลือใน Q2–Q8; raw Source/payload inspection และ dependency ใน Q7 ยังไม่ปิด การกำหนดผู้ใช้งานไม่ได้เพิ่มระบบสิทธิ์
- ต่อไป: ถาม Q1 / kickoff ข้อ 8 เรื่องขอบเขตหน้า Mapping; layout/interaction ยังคงรอ Web design จากผู้ใช้

### 2026-11-13 — ชุดข้อมูล delivery log (M0.2 / M-10)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 7.1: ใช้ชุดข้อมูล delivery log ที่เสนอ โดยรายการปกติไม่แสดง raw Source/payload → M-10; ปรับแผน API ให้ตรงกัน
- กำลังทำ: M0.2 เก็บคำตอบทีละข้อ; ชุดข้อมูล log ตกลงแล้ว แต่ผู้ใช้งานในข้อ 7.2 และขอบเขต Web ในข้อ 8 ยังรอคำตอบ
- ติด / รอ: Q4 เรื่องเวลาข้าม service, Q1 ข้อ 7.2/8–9 และ Q2–Q8; การไม่แสดง raw ในรายการยังไม่ใช่ข้อสรุปว่าจะมีหน้าดู raw แยกหรือไม่
- ต่อไป: ถาม Q1 / kickoff ข้อ 7.2 เรื่องกลุ่มผู้ใช้งาน delivery log โดยไม่เพิ่มระบบสิทธิ์นอกขอบเขต `web.md`

### 2026-11-13 — เวลารับไฟล์ครั้งแรกฝั่ง Mapping (M0.2 / M-09)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 6.4: ใช้เวลาที่ Mapping รับไฟล์และสร้าง File Job ครั้งแรก เก็บคงเดิมเมื่อ resume/retry → M-09; ปรับแผน API และบันทึกหลักฐานช่องว่าง contract ใน Q4
- กำลังทำ: M0.2 ข้อ 1–5 และ 6.1–6.4 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Informs `6ab23ad` ไม่มี field เวลารับไฟล์จาก Mapping ใน schema/DTO และ handler ใช้เวลา HTTP intake ตั้ง `ReceivedAt`; Q4 จึงยังรอ Dev 1 ตัดสินวิธีรักษา D-23 ข้าม service ไม่ถือว่าปิดด้วยการเก็บเวลาเฉพาะ Mapping
- ต่อไป: ถาม Q1 / kickoff ข้อ 7 เรื่องข้อมูลที่ต้องแสดงใน delivery log; Q2–Q8 และ Web design ยังรอตามเดิม

### 2026-11-13 — เพดาน delivery attempts (M0.2 / M-08)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 6.3: delivery รวมสูงสุด 6 attempts รวมครั้งแรก 1 + retry สูงสุด 5 → M-08; ปรับแผน API ให้จำนวน attempts เป็นข้อสรุป ไม่ใช่ข้อเสนอ
- กำลังทำ: M0.2 ข้อ 1–5 และ 6.1–6.3 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: รายละเอียดเวลารับไฟล์ครั้งแรกใน Q4, ข้อ 7–9 และ Q2–Q8; retry delay/timeout และ action หลังครบเพดานยังต้องลงรายละเอียด
- ต่อไป: ถามเรื่องการเก็บเวลารับไฟล์ครั้งแรกให้คงเดิมเมื่อ resume/retry เพื่อปิดส่วนที่เหลือของ Q4

### 2026-11-13 — ค่าเริ่ม HTTP concurrency (M0.2 / M-07)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 6.2: เริ่มส่ง HTTP ไป Informs พร้อมกันสูงสุด 1 request แล้วปรับผ่าน config ภายหลัง → M-07; ปรับแผน API ให้แยกค่าที่เลือกแล้วจากข้อเสนอ retry
- กำลังทำ: M0.2 ข้อ 1–5, 6.1 และ 6.2 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 6 เรื่อง retry/ส่วนที่เหลือ, ข้อ 7–9 และ Q2–Q8; วิธีคุมเพดานเมื่อหลาย pod ต้องลงรายละเอียดใน M1.1/M4.2
- ต่อไป: ถาม Q1 / kickoff ข้อ 6.3 เรื่องจำนวน attempts สำหรับ transient failure

### 2026-11-13 — ลำดับแถวสำหรับ sourceRowNo (M0.2 / M-06)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 6.1: `sourceRowNo` ใช้ลำดับแถวข้อมูลที่อ่านได้ เริ่ม 1 ไม่รวม header/แถวว่าง และคงเดิมเมื่อ retry → M-06; ปรับแผน API และบันทึกคำตอบส่วนลำดับแถวใต้ Q4
- กำลังทำ: M0.2 ข้อ 1–5 และ 6.1 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 6 ส่วนที่เหลือ/ข้อ 7–9 และ Q2–Q8; Q4 ยังรอรายละเอียดการเก็บเวลารับไฟล์ครั้งแรก ไม่ถือว่าปิดทั้งข้อด้วยการเลือกลำดับแถว
- ต่อไป: ถาม Q1 / kickoff ข้อ 6.2 เรื่องค่าเริ่ม HTTP concurrency

### 2026-11-13 — ค่าคงที่และ P/C ใน config MTLS (M0.2 / M-05)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 5: เก็บค่าคงที่ MTLS และกฎแปลง P/C ใน versioned config ผ่าน `literal` / `case` ของ Transformation Program → M-05; ปรับแผน API ให้ตรงกัน
- กำลังทำ: M0.2 ข้อ 1–5 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 6–9 และ Q2–Q8 โดยเฉพาะค่าต้นทางที่สร้าง payload ผ่าน schema ไม่ได้ใน Q5
- ต่อไป: ถาม Q1 / kickoff ข้อ 6 ทีละประเด็น เริ่มจาก `sourceRowNo` ว่าจะใช้ลำดับแถวข้อมูลหรือ `nSeq`

### 2026-11-13 — Default ผู้รับประโยชน์ใน config MTLS (M0.2 / M-04)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 4: เก็บ default ผู้รับประโยชน์ตาม BR-035 ใน versioned config ของ MTLS ผ่าน Transformation Program → M-04; ปรับแผน API ให้ตรงกัน
- กำลังทำ: M0.2 ข้อ 1–4 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 5–9 และ Q2–Q8; Q5 เรื่องสัดส่วน 100% ที่ schema ไม่มี field ยังไม่ปิด
- ต่อไป: ถาม Q1 / kickoff ข้อ 5 เรื่องค่าคงที่ MTLS และการแปลง P/C ว่าเก็บที่ใด

### 2026-11-13 — รูปแบบไฟล์ MTLS สำหรับเริ่มงาน (M0.2)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 3: เริ่มจาก `.xls` BIFF8 ตามตัวอย่างก่อน ใช้ sheet แรก/header 19 columns/cp874 ตาม `mtls-file.md`; `.xlsx` ไม่เป็นเงื่อนไขตรวจรับช่วงแรก
- กำลังทำ: M0.2 ข้อ 1–3 มีคำตอบผู้ใช้แล้ว; ยังไม่ปิด M0.2 และยังไม่แก้ source code
- ติด / รอ: Q1 ข้อ 4–9 และ Q2–Q8; รูปแบบไฟล์ที่เลือกยังไม่ใช่การรับรอง fixture 2,556 แถวหรือคำตอบเรื่อง `sourceRowNo` ใน Q4/Q7
- ต่อไป: ถาม Q1 / kickoff ข้อ 4 เรื่องตำแหน่ง default ผู้รับประโยชน์ตาม BR-035 ใน config mapping

### 2026-11-13 — ขอบเขต Transformation Program (M0.2 / M-03)
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 2: ย้าย Transformation Program ทั้งหมด โดยทำให้ MTLS ใช้งานได้ก่อน; ลง M-03 และเพิ่ม M2.5 สำหรับส่วนที่เหลือ พร้อมปรับ feature matrix ในแผน API
- กำลังทำ: M0.2 ถามคำถามทีละข้อ; ข้อ 1–2 มีคำตอบผู้ใช้แล้ว แต่ยังไม่ปิด M0.2 และยังไม่เริ่ม implement source
- ติด / รอ: Q1 ข้อ 3–9 และ Q2–Q8; Named Lookup ของ demo ผูกกับ `mti_original` และ Table Catalog เดิม ต้องตกลงแหล่งข้อมูล/เส้นแบ่งงานก่อนเปิดใช้จริงตาม Q2/Q7 โดยยังเก็บในขอบเขตการย้ายทั้งหมด
- ต่อไป: ถาม Q1 / kickoff ข้อ 3 เรื่องรูปแบบไฟล์ MTLS; งาน MTLS ทำก่อน ส่วนที่เหลือต้อง review ลำดับและเป้าของ M2.5 โดยไม่เปลี่ยน milestone เดิมเอง

### 2026-11-13 — เก็บคำตอบ review M0.2 ทีละข้อ
- เสร็จ: บันทึกคำตอบผู้ใช้สำหรับ Q1 / kickoff ข้อ 1: หลังเก็บ immutable Source ให้สร้าง JSON `pa-inform/1` โดยตรงแล้วส่ง Informs ผ่าน HTTP; ไม่ใช้ Normalized Table แบบแบนเป็นขั้นกลาง
- กำลังทำ: M0.2 เก็บคำตอบข้อที่เหลือทีละข้อ ตามคำขอผู้ใช้; ยังไม่ปิด M0.2
- ติด / รอ: Q1 ข้อ 2–9 และ Q2–Q8 ยังไม่มีข้อสรุป; ผลที่เป็นเรื่องของ Dev 1 ให้ Dev 1 ลง `D-xx` ใน `rules.md` ตามกติกาเดิม
- ต่อไป: ถาม Q1 / kickoff ข้อ 2 เรื่อง Transformation Program หรือโค้ดเฉพาะ MTLS; ยังไม่มีการแก้ source code หรือ commit

### 2026-11-13 — ผลตรวจ baseline Mapping (M0.3)
- เสร็จ: **M0.3** รัน `./scripts/check.ps1` ใน `prjInsuranceValidationMapping` commit `78453b3cf1693561765db5d2ab7db75f1fd0e07d` ด้วย .NET SDK **10.0.404** แล้วจบ **exit 0 / All checks passed**: restore tools สำเร็จ, Release build สำเร็จ (0 errors / 4 warnings), test command จบแต่ **ไม่มี test case ให้รันทั้ง UnitTests และ IntegrationTests**, EF แจ้ง `No changes have been made to the model since the last migration.` · รายละเอียดและคำสั่งเลือก SDK อยู่ใต้ M0.3 ใน §3
- กำลังทำ: เตรียม review แผน M0.2 และแบบข้อมูล/API M1.1/M1.2; ยังไม่เริ่ม implement source code
- ติด / รอ: Q1–Q8 และ Web design ตามเดิม; build มี `NU1903` ของ `SSH.NET 2023.0.0` ใน integration test dependency (2 advisory IDs, แสดงซ้ำรวม 4 warnings) ต้องตรวจ dependency ก่อนเพิ่ม integration tests · ยังไม่มี Kafka container ที่กำลังรันใน local
- ต่อไป: ปิดคำตอบที่จำเป็นใน M0.2 แล้วลงแบบ M1.1/M1.2; baseline นี้ยืนยัน scaffold build/model เท่านั้น ยังไม่ใช่หลักฐานว่าทดสอบ feature/DB/Kafka ผ่าน

### 2026-11-13
- เสร็จ: **M0.1** จัดทำ [แผน implement API](api-implementation-plan.md) โดยเทียบ feature ทุกกลุ่มใน reverse spec กับขอบเขต phase 1, schema/response จริงของ Informs และ scaffold ของ Mapping; มี architecture, แบบร่างตาราง/API, ลำดับงาน, acceptance criteria และจุดส่งต่อ Web · หลักฐาน: `mapping-demo/docs/mapping-phase-1/api-implementation-plan.md` และรายการงานใน §3; ตรวจ local links, JSON examples, Markdown fences และความเชื่อมโยง **23 task IDs / 8 คำถาม** ผ่าน (เอกสารใน working tree; ยังไม่ commit)
- กำลังทำ: เตรียม review แผนก่อน kickoff 11-16; ยังไม่เริ่ม implement source code
- ติด / รอ: Q1–Q8 ที่เกี่ยวกับ kickoff/ขอบเขต, fixture ทดสอบที่รับรองแล้ว, infrastructure และ Web design จากผู้ใช้ · ประเด็น reprocess เปลี่ยน payload กับ idempotency และ BR-035 ที่ schema ไม่มี field สัดส่วนต้องได้คำตอบก่อนเปิดใช้
- ต่อไป: M0.2 ปิดคำถามที่จำเป็น → M0.3 วัด baseline จริง → M1.1/M1.2 ปิดแบบข้อมูลและ API; ก่อนแก้ source แต่ละชุดต้องระบุไฟล์และรอยืนยันตาม AGENTS.md

## 2. จุดสังเกต (milestone)

> วันที่มาจากแผนรวมของ Dev 1 · **ปรับตาม D-81 (2026-11-19): go / no-go 11-30 · เริ่ม UAT 12-01** (เดิม 12-14 / 12-15) · จะเลื่อนต้องคุยกับ Dev 1 ก่อน แล้วเขียนเหตุผลใน §5

| วันที่ | จุดสังเกต | สถานะ |
|---|---|---|
| 11-16 | kickoff — ตกลงคำถามใน [rules.md](rules.md) §6 · Dev 2 เขียนงานใน §3 | ⬜ |
| 11-18 | อ่านไฟล์ทดสอบ MTLS 2,556 แถวครบทุกแถว | ⬜ |
| 11-23 | JSON ผ่าน schema `pa-inform-1.schema.json` ทุกแถว | ⬜ |
| **11-23** (เดิม 11-26) | **mapping ส่งไฟล์ MTLS เข้า Insurance Validation ได้ และผ่านการตรวจครบ (บนเครื่อง — ทดสอบร่วมกับ Dev 1)** | ⬜ |
| **11-27** (เดิม 12-03) | **หน้าเว็บ mapping ครบตามแบบใช้ได้บนเครื่อง** (Configs · Config editor · Jobs — ไม่ย่อ, §5 Q10) · จุดเช็ค 11-25: Jobs + Configs | ⬜ |
| 11-26 (เดิม 12-04) | Mapping ขึ้น env UAT (Kubernetes หรือ docker บน server — D-81) · mount share folder · config MTLS ติดตั้งแล้ว | ⬜ |
| 11-27 (เดิม 12-09) | ทดสอบร่วมบน env UAT ผ่าน (ไฟล์ทดสอบเข้าทาง mapping ครบ) | ⬜ |
| 11-30 (เดิม 12-14) | พร้อม UAT · go / no-go | ⬜ |
| **12-01** (เดิม 12-15) | **เริ่ม UAT** | ⬜ |

สถานะ: ⬜ ยังไม่ถึง · 🔄 กำลังทำ · ✅ ผ่าน · ⛔ เสี่ยงไม่ทัน (เขียนเหตุผลใน §1)

## 3. งาน

> **Dev 2 วางแผนและเขียนเอง** — แตกงานเป็นกลุ่ม (`M1`, `M2`, …) และ bullet ย่อย · เพิ่ม bullet ได้ตลอด
>
> ```markdown
> - [ ] **M1.1** <งาน> — เสร็จเมื่อ: <สิ่งที่ตรวจได้> · เป้า MM-DD
> - [ ] **M1.2** <งาน> — เสร็จเมื่อ: … · เป้า MM-DD · 🔄
> - [ ] **M1.3** <งาน> — เสร็จเมื่อ: … · เป้า MM-DD · ⛔ รอ <อะไร / ใคร>
> - [x] **M1.4** <งาน> — เสร็จเมื่อ: … · เป้า MM-DD · ✅ MM-DD `prjInsuranceValidationMapping` <commit> · test ผ่าน
>   - Dev 1: <หมายเหตุ>
> ```
>
> - ID ห้ามเปลี่ยน / ห้ามใช้ซ้ำ · ตัดงาน = ~~ขีดฆ่า~~ + เหตุผล (ไม่ลบ)
> - "เสร็จเมื่อ" ต้องตรวจได้ (จำนวนแถว / test ผ่าน / ส่งเข้า API ได้) ไม่ใช่แค่ "ทำเสร็จ"
> - ติ๊ก `[x]` ได้เมื่อมี**หลักฐาน**: ชื่อ repo + commit และ/หรือผลทดสอบ
> - งานที่ผูกกับจุดสังเกตใน §2 ให้เป้าไม่เกินวันของจุดสังเกตนั้น

แผนล่วงหน้า **2026-11-13** สำหรับ review ตอน kickoff 11-16 — วันที่เป็นเป้าร่างที่อยู่ภายใน milestone เดิม ไม่ใช่การยืนยันคำตอบธุรกิจหรือการปรับวันส่ง รายละเอียด/traceability อยู่ใน [แผน API §2–§9](api-implementation-plan.md)

### M0 — วางแผนและปิด dependency

- [x] **M0.1** จัดทำ implementation plan แบบ API ก่อน Web — เสร็จเมื่อ: ครอบคลุม feature ใน reverse spec, แยกสิ่งต้องทำ/ปรับ/เสนอเลื่อน, ระบุ dependency และเกณฑ์ตรวจรับ · เป้า 11-13 · ✅ 11-13 `mapping-demo` [แผน API](api-implementation-plan.md) และ §3–§5 ของไฟล์นี้ (working tree; ยังไม่ commit; ไม่ใช่ผลทดสอบโค้ด)
- [ ] **M0.2** Review แผนและตกลง kickoff/ขอบเขตกับ Dev 1 — เสร็จเมื่อ: Q1–Q8 มีคำตอบสำหรับงานที่จะเริ่ม และผลถูกบันทึกเป็น M-xx/D-xx ตามเจ้าของเรื่อง · เป้า 11-16
- [x] **M0.3** ตรวจ baseline repo Mapping และ local dependencies — เสร็จเมื่อ: บันทึก commit/environment และผล `scripts/check.ps1` จริง รวม Docker/PostgreSQL/Kafka ที่ต้องเตรียม โดยไม่ใช้ผล test ของ demo แทน · เป้า 11-16 · ✅ 11-13 `prjInsuranceValidationMapping` commit `78453b3cf1693561765db5d2ab7db75f1fd0e07d`, script exit 0 (ข้อจำกัดด้าน test/warnings ตามด้านล่าง)
  - รอบแรก shell ใช้ `C:\Program Files\dotnet\dotnet.exe` SDK **8.0.400** → build fail `NETSDK1045` เพราะทุก project target `net10.0`; พบ SDK **10.0.404** ที่ `%USERPROFILE%\.dotnet` จึงเลือกรุ่นนี้เฉพาะ process แล้วรัน script ซ้ำสำเร็จ โดยไม่แก้ PATH ถาวร/source/script
  - ผลรอบที่ใช้ SDK 10: `dotnet-ef 10.0.4` restore สำเร็จ; Release build **0 errors / 4 warnings**; `dotnet test --configuration Release --no-build` ไม่พบ test case ในสอง projects (ตรวจไฟล์พบเพียง `.csproj`); EF model ตรง migration; `All checks passed.` **ไม่ตีความเป็น tests ของ feature ผ่าน**
  - warnings: `NU1903` package `SSH.NET 2023.0.0` ใน dependency ของ `Mapping.IntegrationTests` อ้าง `GHSA-mggc-4xg6-vcxf` และ `GHSA-q939-rpr3-3284`; ยังไม่แก้ package เพราะงานนี้รัน baseline เท่านั้น
  - local dependencies ที่ตรวจ: Docker client/server **29.8.0**, `InsuranceValidation.Postgres` image `postgres:15-alpine` healthy port **5433**, `InsuranceValidation.Mapping` running port **5177**; ไม่พบ Kafka container ที่กำลังรัน จึงยังต้องเตรียมใน M3.1/Q7; รอบนี้ไม่ใช้ `-Db`, ไม่ apply migration และไม่มี integration test เชื่อม DB จริง
  - คำสั่ง PowerShell สำหรับรันซ้ำด้วย SDK ที่ติดตั้งอยู่แล้ว (environment มีผลเฉพาะ shell ที่รัน):

    ```powershell
    $env:DOTNET_ROOT = Join-Path $env:USERPROFILE '.dotnet'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    dotnet --version  # รอบนี้ได้ 10.0.404
    ./scripts/check.ps1
    ```

### M1 — แบบข้อมูลและ API พื้นฐาน

- [x] **M1.1** ปิดแบบ entity/state/ตาราง/unique identities และ transaction boundaries — เสร็จเมื่อ: มี data design พร้อมตัวอย่าง Source/version/jobs/attempts, lineage และแบบป้องกันหลาย pod ใน repo เอกสาร; ไม่มีตารางนอก schema `mapping` · เป้า 11-16 · ✅ 11-16 `mapping-demo` [แบบข้อมูล/runtime](data-runtime-design.md) §8 ตาม M-26 และ [หลักฐาน PostgreSQL local](m1-1-validation.md): 29 checks ผ่าน; working tree ยังไม่ commit (ปิด design ภายใน Mapping ไม่ใช่ปิด EF/API/Kafka/HTTP tests)
  - หลักฐาน 11-16: proof schema **13 tables / 13 FKs** อยู่ `mapping`, public **0 tables**, tables เป็นของ `insurance_user`; 25 constraint/transaction checks + 4 concurrency/fencing assertions ผ่าน บน PostgreSQL 15.20; ฐานทดสอบแยกถูกลบหลังตรวจ ไม่แก้ฐาน `InsuranceValidation`/DB บริษัท
  - ขอบเขตปิดงาน: Q2 ทางเลือกหลักและ Q6 ได้คำตอบแล้ว; Q4/Q5/Q7/Q8 ยังเป็น gate ของ contract/business binding/environment/API ที่เกี่ยว ไม่ทำให้ต้องเดา fields หรืออ้างว่าปิด M0.2/M1.2/M1.3/M1.5 แล้ว
  - ร่าง 11-13: [data-runtime-design.md](data-runtime-design.md) ครอบคลุม Module/interface, Source registry ที่ไม่เก็บ raw กลาง, definition/version pin, transaction/lease/recovery, global budget/attempt cap, row/output counts และ scenarios S01–S36; ยังไม่ปิด M1.1 เพราะ exact DDL/FKs/raw types และประเด็น Q4/Q6/Q7 ที่เกี่ยวต้องได้ข้อสรุป
  - คำตอบผู้ใช้ (11-13), Q2.1: physical Source Table แยกตาม config ตาม M-15 แทนตารางกลาง + JSONB; แบบต้องครอบคลุม table/field metadata, source identity, transaction/constraints และการ pin table definition ให้ config version/งานเดิม รวมถึง schema evolution/วิธีสร้างตารางที่ยังรอคำตอบ
- [ ] **M1.2** ปิด Mapping API/DTO/error contract สำหรับ Web ภายหลัง — เสร็จเมื่อ: route/field paths/BaseResponse/version conflict/pagination/action states/preview limits มีตัวอย่าง request-response และตกลง prefix ใน Q8 · เป้า 11-17 · 🔄 [ร่าง contract](mapping-api-contract.md) และ [แบบหลาย format](multi-format-api-web-design.md); prefix `/api/mapping` ยืนยันแล้ว, config/preview/jobs/capabilities ต้องใช้กับ Excel/Delimited/FixedWidth โดยไม่มี route เฉพาะ MTLS; ผู้ใช้ขอเลื่อน review route/DTO ที่กระทบ Web/contract boundary จึงยังไม่ติ๊กเสร็จ
  - **Dev 1 (2026-11-20): Dev 1 ทำ G1 + route ที่ขาดใน repo Mapping แล้ว (ผู้ใช้สั่ง) — Dev 2 ไม่ต้องทำส่วนนี้ซ้ำ ให้ pull `develop` ก่อนแก้โค้ดต่อ** (Mapping `develop` commit `c72ed90` — ผู้ใช้ push เอง) · สิ่งที่เพิ่ม ตาม [ร่าง contract](mapping-api-contract.md) แถว 35–42 / 46:
    - `GET /mapping-configs` · `GET /mapping-configs/{id}` — คืน `id, name, inputPath, enabled, activeVersionId, activeVersionNumber, latestVersionNumber, inputLayoutKind, extension, sourceDefinitionId, lastFileJob {id, createdAt, importState}, rowVersion` (`inputLayoutKind` / `extension` อ่านจาก version ที่ active หรือล่าสุด)
    - `POST /mapping-configs` `{name, inputPath, requestId, reason?}` → `201` · `inputPath` ต้องเป็น path ย่อย (ไม่มี `/` นำหน้า, `:`, `..`) · `requestId` ซ้ำ + body เดิม = ได้ผลเดิม · body ต่าง = `409 RequestConflict`
    - `POST /mapping-configs/{id}/versions` `{expectedRowVersion, sourceDefinitionVersionId, contractSnapshotId, profileSnapshotId, inputLayout, transformationProgram, evaluationSemantics?}` → `201 {id, versionId, versionNumber, rowVersion}` · **ต่างจากร่าง:** ใช้ `contractSnapshotId` + `profileSnapshotId` (แบบเดียวกับ draft ของ preview) แทน `destination` และไม่มี `requestId` (กันซ้ำด้วย `expectedRowVersion`) · ตรวจด้วยกฎเดียวกับ `validate` ก่อน — มี diagnostic → `400 InvalidVersion` + diagnostic ใน `messages[]` ไม่บันทึก
    - `GET /mapping-configs/{id}/versions` (สรุป ไม่มี program) · `GET /mapping-configs/{id}/versions/{versionId}` (เต็ม: `inputLayout`, `transformationProgram`, `evaluationSemantics`)
    - `POST /mapping-configs/{id}/activate` `{versionId, expectedRowVersion, requestId, reason}` · `/disable` `{expectedRowVersion, requestId, reason}` — ใช้ `ConfigLifecycleRepository` เดิม · `409` = `RowVersionConflict` / `VersionConflict` / `RouteConflict` / `RequestConflict`
    - `POST /file-jobs/{id}/retry` · `POST /row-jobs/{id}/retry` `{expectedRowVersion, requestId, reason}` → `202` · **ความหมาย (ผู้ใช้ 2026-11-20): "ทำทันที" เฉพาะงานที่รอ retry อัตโนมัติอยู่** — row `RetryableFailed` / file ที่ `NextAt` ยังไม่ถึง และไม่มี worker ถือ → ดึง `NextAt` มาเป็นตอนนี้ · `Blocked` → `409 NotRetryable` (config เดิมรันกี่ครั้งก็พังเหมือนเดิม) · ไม่รอ retry → `409 InvalidState` · audit ใน `mapping_audits` (`RetryFileJob` / `RetryRowJob`)
    - `/health/live` + `/health/ready` (ready ตรวจ DB) แบบเดียวกับ Products / Informs
    - **G2 (script ติดตั้ง config MTLS) ไม่ทำ (ผู้ใช้ 2026-11-20)** — ผู้ใช้ตั้ง config MTLS เองตอน manual test ผ่าน API ข้างบน / หน้าเว็บ · Dev 2 ไม่ต้องทำ G2
    - **แก้ test เดิม 2 เรื่อง (ผู้ใช้ยืนยัน — ไม่แตะ logic ของ test):** (1) `RowTransformationPipelineTests.PinnedContext_…` ค่าคาดหวังใช้ `ToString("yyyy-MM-dd")` ตาม culture เครื่อง → เครื่องภาษาไทยได้ปี พ.ศ. (`2569`) ทั้งที่ payload ถูก (`2026`) → ใส่ `CultureInfo.InvariantCulture` (2) 3 class ที่ใช้ Kafka (`KafkaFileIntakeTests` · `KafkaOutboxPipelineTests` · `KafkaRowTransformationTests`) ใส่ `[Collection(KafkaCollection.Name)]` (`KafkaCollection.cs` — `DisableParallelization = true`) ให้รันแยกหลัง class อื่น — `check.ps1` แบบขนานเดิม `HostedPipeline…` timeout 3 นาทีเพราะเครื่องโหลด container หลายตัวพร้อมกัน · ไม่ต้องใช้ RunSettings แบบเรียงลำดับเองอีก
    - **2026-11-20 บ่าย (ผู้ใช้สั่ง Dev 1 ทำ):** (1) **M4.4 ผ่านบน docker** — ไฟล์ MTLS 2,556 แถวผ่าน Mapping เข้า Informs จริงใน docker (ใบคำขอเข้า `non_motor.inform`) — Dev 2 ปิด M4.4 ได้ / ทวนเพิ่มตามแผน (2) `GetCapabilitiesQuery` เปิด `available = true` ให้ Excel / Delimited / FixedWidth / ApiContract (เดิม false รอ Informs จริง) + แก้ `MappingCatalogApiTests` ให้ตรง (3) repo Web: `core/mapping-api.ts` เรียก route จริงทั้งหมดแล้ว · wizard "สร้าง Mapping ใหม่" บันทึกได้จริง — ตัวแก้สูตรแบบเลือกทีละชั้น (ยกแนวจาก `web/components/transformation/value-expression-editor.tsx`) · Preview ครั้งแรก = สร้าง config แบบร่าง (`POST /mapping-configs` + `POST /source-definitions`) แล้ว preview ด้วย draft · ตารางจับคู่ตัดช่องใน `SystemPaths` และช่อง object / array ออก — **Dev 2 ที่ทำหน้าเว็บ mapping ต่อ ให้ pull `develop` ของ Web ก่อน** (Web `f820539` · Mapping `74031b6` — ผู้ใช้ push เอง)
    - **Dev 1 (2026-11-20 เย็น — D-83, ผู้ใช้สั่ง): Mapping ไม่ fix `pa-inform/1` / Informs แล้ว — pull `develop` ของ Mapping + Web ก่อนแก้** (ยังไม่ commit ตอนเขียน — ดู git log)
      - config: section **`Informs` → `Destinations` (array)** ดู [rules.md D-83](rules.md) · `appsettings.Local.json` / `docker-compose.yml` แก้แล้ว
      - `Infrastructure/Informs/` → `Infrastructure/Destinations/` · `InformsClient` → `ApiContractClient` · `InformsSchemaClient` → `ContractSchemaClient` · `IInformsClient` → `IDestinationClient` (`SendAsync(DeliveryTarget, payload, ct, paths)`) · `InformsDelivery*` → `DestinationDelivery*` · `DeliveryOptions.ConnectionKey` ลบ
      - `IContractCatalog`: `RefreshCurrentAsync()` → `RefreshAsync(contractVersion)` + `ContractVersions` · route ใหม่ `GET /contracts` · `GET /contracts/{**version}`
      - `RowTransformationService`: ไม่เช็ค `pa-inform/1` · system field เติมเฉพาะที่ contract มี (code `ContractMissingFileReceivedAt` ไม่มีแล้ว)
      - test: แก้ตามชื่อใหม่ + เพิ่ม `motor-inform/1` / `$id` ไม่ตรง / ส่ง 2 ปลายทาง / contract ไม่มี `fileReceivedAt` · `check.ps1` ผ่าน (unit 38 · integration 169)
      - Web: wizard ② เลือก contract จาก `GET /contracts` · ③ กรองปลายทางตาม contract · หน้า Contract `?version=` · ข้อความ Informs / pa-inform/1 → ค่าจริง
      - **รอบ 2 (ไล่ทุกหน้า):** ป้ายหัว wizard ขึ้นหลังผ่านขั้น · ขั้น ④ ไม่เลือก Excel / `cp874` ให้ (extension จาก capabilities · encoding `utf-8`) · header = ชื่อ field · fixed width ต้องกรอกเอง · `value-expression-editor` รับ `[caps]` (ชนิด / convert จาก capabilities) · **array หลายรายการ:** `core/mapping-program.ts` `buildProgram(rows, conditions)` รับ path แบบ `a[1].b` + `emitWhen` ต่อรายการ, `concretePaths` / `displayPath` / `genericPath` · ขั้น ⑤ มีส่วน "รายการใน array" · repo Mapping: Excel `.xlsx` (`ExcelInputLayoutValidator.Extensions`, `ExcelInputReader` เลือก reader + ตรวจ signature ตามนามสกุล)
      - **D-84 (ผู้ใช้ 2026-11-20):** repo Mapping — `SourceField.Required` · `RequiredFieldRule` (`RequiredFieldBlank`) ใน `RowTransformationService` / `PreviewService` · `IInputReader.InspectAsync` (ทุก reader + stub ใน test ต้องมี) · `POST /api/mapping/inspect-file` (`MappingInspectController`, `InspectFileQuery`) · helper test `IntakeData.SeedAsync(..., requiredFields)` · repo Web — wizard 5 ขั้น ขั้น ① ใหม่ (`ColumnRow`), `mapping-api.ts` `inspectFile()` / `configLayout()`
      - **tour ทุกขั้น** (ผู้ใช้ 2026-11-20): `mapping-new-tour.ts` แยก tour ตามขั้น · `data-tour` ใหม่ใน `mapping-new.html` — ถ้าย้าย / เปลี่ยนชื่อส่วนไหนของ wizard ให้แก้ `data-tour` ตามด้วย
- [x] **M1.3** Implement EF persistence, config/version lifecycle และ immutable Source storage — เสร็จเมื่อ: migration/constraints ผ่าน PostgreSQL integration tests, activate พร้อมกันได้ conflict ชัดเจน, version เก่า/Source rows ไม่ถูกแก้ · เป้า 11-18 · ✅ 11-16 Mapping `AddMappingCatalog` + `AddMappingRuntime`, PostgreSQL integration tests 7/7 และ `scripts/check.ps1` ผ่าน; code ยังไม่ commit/push
  - 🔄 11-16 ชุดแรก: Mapping `AddMappingCatalog` migration สร้าง **7 catalog tables** + EF history ใน `mapping`; 4 integration tests ผ่าน (schema/FK owner/version immutability/stale row version) และ `scripts/check.ps1` ผ่าน; โค้ดและเอกสารยังไม่ commit ส่วน File Job/physical Source/activation transaction/concurrent activate ยังรอ batch ถัดไป จึงไม่ติ๊กเสร็จ
  - ✅ 11-16 ชุดสอง: `AddMappingRuntime` เพิ่ม File Job/path+content claim/Source registry/control/audit รวม 6 ตาราง; physical raw Source table + version เพิ่ม columns atomic, raw row/registry/checkpoint atomic, triggers/guards และ config lifecycle transaction; 7 integration tests ผ่านรวม concurrency/immutability/pin/rollback; API/reader/Row Job/outbox แยก milestone ตามแผน
- [x] **M1.4** Implement Informs contract/profile snapshot และ capability catalog — เสร็จเมื่อ: schema/hash/path catalog ตรง Informs `insureds[]`, version pin ได้, ไม่มี latest-schema fallback เปลี่ยนงานเดิม, ไม่คืน secret/เปิด capability ที่ยังไม่ทำ · เป้า 11-18 · ✅ 11-16 Mapping code + [แบบ M1.4](m1-4-contract-catalog-design.md), PostgreSQL/HTTP fake/API integration tests 11/11 และ `scripts/check.ps1` ผ่าน; code ยังไม่ commit/push
  - คำตอบผู้ใช้ (11-13), Q2.7: destination ช่วงแรกเป็น Informs API (`ApiContract`) ตาม M-21; LegacyTable/JsonFile/DatabaseTable เป็น backlog ไม่แสดงว่าพร้อมใช้จากการย้าย Transformation Program ทั้งหมด
- [x] **M1.5** Implement Source catalog/API สำหรับสร้าง physical Source Table และเพิ่ม columns ผ่าน Web — เสร็จเมื่อ: metadata/physical schema อยู่ `mapping` และตรงกัน, validation ชื่อ/ชนิด columns/system columns ผ่าน, สร้าง/เพิ่ม column พร้อมกันหรือล้มเหลวไม่ทิ้ง table/metadata ค้าง, บันทึก audit และ PostgreSQL integration tests ผ่าน; เพิ่ม columns ได้อย่างเดียวตาม M-17 และงานเก่าอ่าน definition/Source เดิมได้ · เป้า 11-18 (ร่างสำหรับ review M0.2) · ✅ 11-16 Mapping API/repository + PostgreSQL/API integration tests 15/15 และ `scripts/check.ps1` ผ่าน; Web editor แยก M5.3, Q7 บริษัทแยกตรวจ

### M2 — อ่านไฟล์หลาย format และสร้าง payload

- [x] **M2.1** Implement Input Reader contract กลางและ Excel adapter; ใช้ MTLS เป็นหนึ่ง fixture — เสร็จเมื่อ: parser คืน raw record/physical position/diagnostics แบบร่วมกับ format อื่น และ [MTLS fixture สมมติ](fixtures/README.md) อ่านครบ **2,556 แถว** จาก sheet แรก/header 19 columns/cp874 โดยภาษาไทย/number formatting ถูกต้อง · เป้า 11-18 · ✅ 11-16 Mapping reader + fixture สมมติ, integration tests 17/17 และ `scripts/check.ps1` ผ่าน; code ยังไม่ commit/push
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 3: เริ่ม `.xls` BIFF8 ตามตัวอย่างก่อน; `.xlsx` ไม่เป็นเงื่อนไขตรวจรับช่วงแรก และยังไม่ถือว่ายืนยัน fixture ทดสอบใน Q7
  - Dev 1 ตรวจใหม่ (11-16): ไฟล์จริงและ fixture เป็น **BIFF5/stream `Book`** ไม่ใช่ BIFF8; CODEPAGE ระบุ 1252 แต่ข้อความไทยเป็น cp874; acceptance ของ reader ต้องยึด [mtls-file.md](mtls-file.md) ที่แก้แล้วและ [fixture สมมติ](fixtures/README.md)
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.1: `sourceRowNo` เป็นลำดับแถวข้อมูลเริ่ม 1 ไม่รวม header/แถวว่าง; เก็บ Physical Line Number แยก และ retry ใช้เลขเดิมตาม M-06 ไม่อิงค่า `nSeq`
- [x] **M2.2** Implement Transformation Program กลางและ versioned config ตัวอย่าง MTLS — เสร็จเมื่อ: evaluator รับ Source fields/target paths จาก config ไม่ hardcode partner; nested `insureds[]/beneficiaries[]`, constants, P/C, null+unparsed/default ของ MTLS เป็น test case ผ่าน unit/contract tests โดยไม่ตรวจ product/Client API เอง · เป้า 11-20 · ✅ 11-16 Program JSON สมมติ + compiler/evaluator, unit 5/5, target paths 27/27 ตรง JSON Schema จริงของ Informs และ `scripts/check.ps1` ผ่าน; full schema/fixture แยก M2.4, business bindings ที่ผู้ใช้จะส่งรอ Q5; code ยังไม่ commit/push
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 4: default ผู้รับประโยชน์อยู่ใน versioned config ของ MTLS ผ่าน Transformation Program ตาม M-04; เรื่องสัดส่วนใน Q5 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 5: ค่าคงที่ MTLS และกฎ P/C อยู่ใน versioned config ผ่าน `literal` / `case` ตาม M-05; ไม่ hardcode ใน C# และไม่กำหนด fallback ให้ transaction type ที่ยังไม่ตกลงใน Q5
- [ ] **M2.3** Implement validate/preview/file preview ตาม `inputLayout.kind` — เสร็จเมื่อ: Excel/Delimited/FixedWidth ใช้ parser/runtime compiler เดียวกับ import ของ format นั้น, diagnostics มี path/code, จำกัดขนาด/records และ integration test ยืนยันไม่มี job/source/outbox/HTTP/archive side effects · เป้า 11-23 เดิม; ขอบเขตเพิ่มรอ Q9 · 🔄 11-16 routes/3 readers/limits/diagnostics ผ่าน unit 7/7, integration 24/24 และ `scripts/check.ps1`; pinned preview read-only ต่อ PostgreSQL แล้ว counts ไม่เพิ่ม; ยังรอพิสูจน์ import ใช้ชุดเดียวกันและ Q9/UAT
  - 🔄 11-17 M2.6/M2.7 พิสูจน์ shared parser/validator ของ Delimited และ FixedWidth กับ pinned preview → import → Source จริงแล้ว; ยังรอ Excel import dispatch และ transform worker ของ M3.3 ที่ใช้ compiler/evaluator เดียวกับ preview รวมถึง Q9 จึงยังไม่ปิด M2.3
- [x] **M2.4** ทดสอบ MTLS เต็มไฟล์กับ schema จริง — เสร็จเมื่อ: 2,556 แถวได้ 2,556 outputs `default` ผ่าน `pa-inform/1`, แถว C ครบและค่าแปลงไม่ได้ยังอยู่ใน unparsed; บันทึกผล/เวลารัน/fixture identity โดยไม่มี PII · เป้า 11-23 · ✅ 11-16 fixture/schema SHA-256 ตรึงแล้ว, full-file test 4,510 ms: 2,556/2,556 outputs ผ่าน schema, C 3 แถวมี unparsed, `scripts/check.ps1` ผ่าน; Q5/business semantics และ HTTP/UAT แยกต่อ; code ยังไม่ commit/push
- [x] **M2.5** ย้าย Transformation Program ส่วนที่เหลือให้ครบตาม M-03 หลังส่วนที่ MTLS ใช้ — เสร็จเมื่อ: มี traceability และผลทดสอบของ value expressions 13 แบบ, predicates 17 แบบ, conversion targets 7 ชนิด, filter/branches, nested object/array, Named Transform 1 รายการ และ Named Lookup 2 รายการครบ; capability ที่แสดงว่าพร้อมใช้งานต้องมี implementation และ dependency พร้อมจริง · เป้า รอ review M0.2 โดยคง milestone เดิม · ✅ 11-17 Mapping SQL Server adapter + async evaluator/preview/dependency validation/capabilities ตาม M-28; `check.ps1` unit 17/17, integration 32/32 และ EF model check ผ่าน; UAT read-only smoke ผ่านทั้งสอง lookup และ API predicates 17 แบบ; [traceability](m2-5-transformation-traceability.md); code ยังไม่ commit/push
- [ ] **M2.6** Implement Delimited adapter สำหรับ CSV/TSV ตาม Input Reader contract กลาง — เสร็จเมื่อ: config กำหนด delimiter/quote/header/encoding/field bindings ได้; synthetic CSV และ TSV ผ่าน validate/preview/import/Source raw/row numbering/diagnostics โดยไม่ hardcode MTLS และ partner ที่ใช้ format นี้ผ่าน acceptance เมื่อได้รับ fields/fixture · เป้า รอ Dev 1/PM ตกลง Q9 · 🔄 11-17 reader/shared validator/preview/pinned snapshot import/Source raw ผ่านแล้วตาม [หลักฐาน M2.6](m2-6-delimited-import-design.md); `check.ps1` unit 17/17, integration 48/48 และ EF model ผ่าน; acceptance partner จริงยังรอ Q5/Q9 และ intake/queued processing แยก M3; ยังไม่ติ๊กปิดทั้งข้อและยังไม่ commit/push
- [ ] **M2.7** Implement Fixed-width adapter ตาม Input Reader contract กลาง — เสร็จเมื่อ: config กำหนดช่วง start/length/encoding/field bindings ได้; synthetic fixed-width ผ่าน validate/preview/import/Source raw/row numbering/diagnostics และ reject ช่วง field ซ้อน/record สั้นด้วย code/path ชัดเจน; partner ที่ใช้ format นี้ผ่าน acceptance เมื่อได้รับ fields/fixture · เป้า รอ Dev 1/PM ตกลง Q9 · 🔄 11-17 shared validator/reader/preview/pinned import/Source raw/Row Job/outbox ผ่านแล้วตาม [หลักฐาน M2.7](m2-7-fixed-width-import-design.md); `check.ps1` unit 17/17, integration 60/60 และ EF model ผ่าน; partner acceptance ยังรอ Q5/Q9 ไม่ติ๊กปิดทั้งข้อ และยังไม่ commit/push

### M3 — รับไฟล์และประมวลผลแบบ durable

- [x] **M3.1** เพิ่ม outbox/Kafka consumers และ local infrastructure ตาม Informs — เสร็จเมื่อ: transaction/offset/redelivery integration tests ผ่าน, events มี job IDs ไม่มี PII และ local broker/topic/group พร้อม · เป้า 11-20 · ปิด 11-17 ตาม M-13/M-14/M-31 และข้อตกลงใช้ Kafka local ก่อน; dependency บริษัทติดตามแยกใน Q7 ก่อนเปิดใช้งาน environment บริษัท
  - เกณฑ์เดิมรวม “topic names และบริษัท dependency ได้ข้อสรุป”; 11-17 ผู้ใช้ตกลงใช้ local ต่อและถามปิด M3.1 จึงแยกเกณฑ์บริษัทไป Q7 โดยคงเป็นเงื่อนไขก่อนใช้งานบริษัท ไม่ลดข้อกำหนด deployment
  - ✅ 11-17 ส่วน local ตาม M-31: dispatcher + strict typed event + durable inbox/Row Job wake-up + manual offset/redelivery พร้อม; `check.ps1` ผ่าน unit 20/integration 74/EF model, รวม M3.1 integration 14 ข้อ; local Kafka healthy และ topic 3 partitions; [หลักฐาน/รายการไฟล์](m3-1-messaging-design.md) · consumer จัดคิวต่อให้ M3.3 ยังไม่แปลงข้อมูล · ปิด M3.1 ในขอบเขต local แล้ว
  - 🔄 11-17 ส่วนที่ M2.6 ต้องใช้: outbox persistence + IDs-only transform message + transaction/rollback tests ผ่านแล้ว; ยังไม่มี Kafka dispatcher/consumer/offset tests จึงไม่ปิด M3.1 และ topic default เป็นของ local ตาม M-29 ไม่ใช่การตกลง topic บริษัท
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 9.1: เลือก Kafka + transactional outbox ตาม M-13; ยังรอการเตรียม broker local และรายละเอียด topics/groups/owner ของบริษัทใน Q7 ไม่ถือว่าพร้อมใช้งานเพียงเพราะเลือกแนวทางแล้ว
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 9.2: เริ่ม Kafka local ผ่าน Docker ตาม M-14; ต้องแจ้งไฟล์ Compose/config/test ที่จะเพิ่มหรือแก้และรอยืนยันก่อนลงมือ ยังไม่มีการ provision หรือผลทดสอบ Kafka ของ Mapping ในงานนี้
- [x] **M3.2** Implement scan/snapshot/import/resume/archive — เสร็จเมื่อ: pin version ก่อนรับ, partial-file stability/path containment/ownership ทำงาน, หลาย scanner ไม่รับ occurrence ซ้ำ, Source+Row Job+outbox atomic, archive ไม่ทำหลักฐานหายเมื่อ original เปลี่ยน · เป้า 11-23 · ปิด 11-17 ในขอบเขต local ตาม M-25/M-32; dependency บริษัทติดตาม Q7 ก่อน deployment
  - ✅ 11-17 `check.ps1` ผ่าน unit 22/22, integration 95/95 และ EF model; รวม M3.2 integration 21 ข้อ ตรวจ atomic receive/seal/rows, scanners/claims/dedup, fencing, cancel/resume, snapshot/archive DB failure, changed original, Kafka receipt/offset/poison/republish/restart; Excel raw 2,556 × 19 fields รวม NULL และ hosted intake ครบสาม format/2,560 แถวผ่าน [หลักฐานปิดงาน/รายการไฟล์](m3-2-file-intake-design.md); ผู้ใช้ยืนยันเพิ่มไฟล์ Source writer เพื่อแก้ NULL แล้ว โค้ด commit `9198a54` บน develop ยังไม่ push; เอกสารยังไม่ commit
  - 🔄 11-17 เตรียม [แบบ/source batch M3.2](m3-2-file-intake-design.md) แล้ว รอยืนยันก่อนแก้ source; เสนอ snapshot-copy archive ที่คง input เดิม + path pointer เพื่อกันรับซ้ำและรักษาไฟล์ใหม่ที่ partner เขียนทับ ไม่เปลี่ยน contract/day/ขอบเขต business; M3.3/M4 ยังแยกทำ
  - 🔄 11-17 import core สำหรับ File Job/snapshot ที่เตรียมแล้วผ่าน CSV/TSV (M2.6) และ FixedWidth (M2.7) + pinned hash/version + lease/fencing + atomic rows + cancellation/resume; scanner/path stability/snapshot filesystem/archive ยังแยกทำ ไม่ปิด M3.2 ทั้งข้อ
- [x] **M3.3** Implement row transformation jobs, claim/lease/recovery และ row diagnostics — เสร็จเมื่อ: restart/redelivery ไม่สร้าง Source/output ซ้ำ, business conversion failure ไม่ทิ้งแถว, system/contract failure แสดงในยอดที่ยังไม่ส่ง และแถวอื่นไปต่อได้ · เป้า 11-24 · ปิด 11-17 ในขอบเขต worker/local ตาม M-33; Informs acceptance/deploy T2.6 ยังติดตาม Q4 และ company deployment ตาม Q7
  - ✅ 11-17 `check.ps1` ผ่าน unit 27/27, integration 113/113 และ EF model; รวม M3.3 integration 18 ข้อพิสูจน์ DB transaction/lease/recovery/fencing, crypto/tamper/key loss, null/unparsed/schema/cardinality/config pin/limits/rollback/cancel, Kafka replay/worker สองตัวและ full MTLS สังเคราะห์ 2,556 encrypted default outputs; [หลักฐานปิดงาน/รายการไฟล์](m3-3-row-transformation-design.md); ใช้ synthetic D-80 schema เฉพาะ success mechanics และทดสอบ actual schema เก่าได้ Blocked; worker ปิดโดย default ไม่มี HTTP/Delivered; source commit `7014c83` บน develop ยังไม่ push
  - 🔄 11-17 ตรวจแบบและทำ [source batch M3.3](m3-3-row-transformation-design.md) แล้ว รอยืนยันก่อนแก้ source; รวม encrypted output storage ของ M4.1 เท่าที่จำเป็นต่อ atomic row result ไม่ปิด HTTP/attempts ตามไปด้วย; dependency schema fileReceivedAt/T2.6 ติดตาม Q4

### M4 — ส่ง Informs และดูแลผลการส่ง

- [x] **M4.1** Implement encrypted durable Delivery Payload และ typed Informs HttpClient — เสร็จเมื่อ: 1 request/ใบ, bytes/hash/key persist ก่อนส่ง, 201/200/replayed/replaced/400/409 ตรง contract จริง และไม่แตะ `non_motor.*` · เป้า 11-24 · ปิด 11-17 เฉพาะ encrypted storage + HTTP adapter/local tests ตาม M-33/M-34; actual Informs acceptance/deploy T2.6 แยก Q4/M4.4 และ durable delivery runner แยก M4.2
  - ✅ 11-18 source รวมกับ M4.2 ใน commit `aca67c6` และ push develop แล้ว; หลักฐานรวมล่าสุด unit 32/32, integration 138/138 และ EF model ตามเงื่อนไข RunSettings ใน M4.2
  - ✅ 11-17 `check.ps1` ผ่าน unit 27/27, integration 124/124 และ EF model; เพิ่ม M4.1 integration 11 ข้อ ตรวจ exact committed bytes/replay/config change, safe status/strict response/Retry-After/limits/cancel/timeout/no retry/redirect/cookies และ key/tamper/old schema ไม่ส่ง HTTP; [หลักฐานปิดงาน/รายการไฟล์](m4-1-informs-http-client-design.md); capability ยัง false ไม่สร้าง delivery attempt หรือ mark Delivered จาก adapter; source/docs ยังไม่ commit/push
  - 🔄 11-17 ตรวจ response ของ controller จริงและเตรียม [แบบ/source batch M4.1](m4-1-informs-http-client-design.md) แล้ว รอยืนยันก่อนแก้ source; ส่ง exact final bytes หนึ่ง POST ต่อ invocation, safe bounded result/Retry-After/no internal retry; ไม่เปิด delivery runner ก่อน M4.2 และไม่อ้าง actual Informs acceptance ก่อน T2.6/Q4
  - 🔄 11-17 encrypted durable storage/identity-bound AES-GCM/hash/key reference และ delivery outbox ทำพร้อม M3.3 ตาม M-33 แล้ว; เหลือ typed HttpClient/response contract/HTTP tests และรอ Informs T2.6/Q4 ก่อนตรวจ actual fileReceivedAt; ยังไม่ปิด M4.1
- [x] **M4.2** Implement retry/backoff/Retry-After/concurrency/lease recovery — เสร็จเมื่อ: 429/5xx/timeout และ crash หลัง Informs commit ใช้ bytes/key เดิม, attempt bounded/ไม่ retry ซ้อน, permanent rejection ไม่ auto retry · เป้า 11-25 · ปิด 11-18 ในขอบเขต local runner/HTTP loopback ตาม M-35; actual Informs acceptance แยก Q4/M4.4 และ company deployment แยก Q7
  - ✅ 11-18 source commit `aca67c6` และ push develop แล้ว พร้อมแบบ/หลักฐาน M4.1/M4.2 ใน commit เอกสารชุดนี้
  - ✅ 11-18 check.ps1 ผ่าน unit 32/32, integration 138/138 และ EF model โดยใช้ temporary RunSettings รัน test collections ตามลำดับ ไม่มี skip/filter และ concurrency cases ภายใน tests คงเดิม; default parallel gate มี Kafka intake timeout บนเครื่องนี้และรันแยกผ่าน ไม่อ้างว่า default ถูกแก้ถาวร; [หลักฐาน/ไฟล์/เงื่อนไขตรวจ](m4-2-durable-delivery-design.md); worker default disabled, ไม่มี attempt 7, raw payload/key เดิมเมื่อ replay; source/docs ยังไม่ commit/push
  - 🔄 11-17 เตรียม [แบบ/source batch M4.2](m4-2-durable-delivery-design.md) แล้ว รอยืนยันก่อนแก้ source; รวม DB attempt ledger/global budget/pause guard/worker/heartbeat/fencing และ recovery โดย sender default disabled; ใช้ typed client/encrypted payload M4.1 เดิมที่ยังไม่ commit; actual Informs acceptance แยก Q4/M4.4
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.2: เริ่ม HTTP concurrency สูงสุด 1 request ไป Informs และปรับผ่าน config ภายหลังตาม M-07; แบบการคุมเพดานหลาย pod ยังต้องระบุใน M1.1/M4.2 ไม่ถือว่าเพดานเพิ่มอัตโนมัติตามจำนวน pod
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.3: รวมสูงสุด 6 attempts ต่อ delivery รวมครั้งแรก 1 + retry สูงสุด 5 ตาม M-08; จำนวนครั้งต้อง durable ไม่รีเซ็ตเมื่อ restart/redelivery และไม่ให้ HttpClient retry ซ้อนเพิ่มจำนวนโดยไม่ถูกนับ
- [x] **M4.3** Implement delivery log/file summary/retry และ pause controls ที่ตกลง — เสร็จเมื่อ: counts ต่อ unique output ไม่ปน attempts, แยก import/transform/delivery, field reason/history ดูได้, action state guard/audit ผ่าน; changed-payload reprocess/business resend ปิดในช่วงแรกตาม M-22 · เป้า 11-26 · ปิด 11-18 เฉพาะ API/local tests ตาม M-36; actual Informs แยก Q4/M4.4 และ M1.2/Q8/Web review ยังไม่ปิด
  - ✅ 11-18 source commit `3fd48f8` และ push develop แล้ว; unit38/38 integration162/162 และ EF model ผ่านตามเงื่อนไข RunSettings ของ M4.2; แบบ/หลักฐาน M4.3 และ runtime อยู่ใน commit เอกสารชุดนี้
  - ✅ 11-18 ผู้ใช้ยืนยัน contract subset/source batch; implement12routes/migration `20261118031413_AddDeliveryDiagnosticsAndLogIndexes`; check.ps1 ผ่าน unit38/38 integration162/162 และ EF model ด้วย temporary sequential-collection RunSettings ไม่มี filter/skip; เพิ่ม unit6/integration24 รวม race/rollback/inspection no-side-effects/safe field diagnostics; [หลักฐาน/รายการไฟล์](m4-3-delivery-operations-api-design.md); source/docs ยังไม่ commit/push
  - 🔄 11-18 เตรียม [แบบ/API contract subset/source batch M4.3](m4-3-delivery-operations-api-design.md) แล้ว; ต้อง review/ยืนยัน subset นี้ก่อน controllers ตาม Q8/M1.2 ที่เคยพักไว้ ไม่ถือว่า prefix หรือ task request เพียงอย่างเดียวปิด route/DTO review แล้ว; source ยังไม่แก้
  - คำตอบผู้ใช้ (11-13), Q2.6: ครบ 6 attempts แล้วไม่เปิด Web retry ตาม M-20; API/worker ต้องปฏิเสธการส่งครั้งที่ 7 ของ delivery เดิมด้วย ไม่ใช่ปิดปุ่มอย่างเดียว ทดสอบ state/concurrent retry ว่าไม่สร้าง attempt/outbox/HTTP เกินเพดาน
  - Dev 1: (11-19) **ขึ้นโครงหน้าเว็บ mapping ให้แล้ว** ใน repo `prjInsuranceValidationWeb` (`develop` push แล้ว `8e74990`…`2df5e45`) ตาม Web design ของผู้ใช้ (ภาพ design อยู่ใน claude.ai ที่ Dev 2 เปิดไม่ได้ — โค้ดชุดนี้คือ design ฉบับที่ใช้ได้) · เอาเฉพาะส่วนของ phase 1 (ปลายทาง ApiContract → Informs เท่านั้น ไม่มี JsonFile / DB / rollout stage / resend / reprocess) · **ทุกหน้าใช้ข้อมูลสมมติ** จาก `core/mapping-sample.ts` ผ่าน `core/mapping-api.ts` — ต่อ API จริงแล้วแค่ `capabilities()` · method อื่นมี `TODO` บอก route ของ M1.2 ไว้: แทน body ด้วยการเรียกจริง + mapper แล้วลบ block ใน sample (หน้าจอไม่ต้องแก้ เพราะเห็นแค่ model ใน `core/mapping-models.ts`) · หน้า: `/mapping` ภาพรวม · `/mapping/new` สร้างแบบนำทาง 6 ขั้น (Source fields → Contract → ปลายทาง → Config & ไฟล์ → Mapping & preview → ตรวจทาน) · `/mapping/configs` + `/:id` (Versions · Workbench · Activation · File Jobs) · `/mapping/file-jobs` + `/:id` (แถว error · ทุกแถว · Deliveries + ข้อมูลดิบรายแถว + payload) · `/mapping/deliveries` · `/mapping/controls` (pause / resume) · `/mapping/source-definitions` + `/:id` · `/mapping/contracts` · `/mapping/destination-profiles` · `/mapping/capabilities` · ไฟล์กลางที่แก้: `layout/shell.ts` (กลุ่มเมนู Mapping), `app.routes.ts`, `environment.ts` (`mappingApiUrl` :5177), `styles.scss` (class ของ mapping ท้ายไฟล์) · ไม่เพิ่ม package · build + vitest ผ่าน · ปรับ layout / ข้อความได้ตามสะดวก — ถ้าจะเปลี่ยนโครงหน้า (เพิ่ม / ตัดหน้า) บอกใน §5 ก่อน
  - คำตอบผู้ใช้ (11-13), Q2.5: Delivery Payload inspection แบบ read-only ตาม M-19; ใช้ payload snapshot ที่บันทึกจริง ไม่ compile ใหม่ และทดสอบว่าเปิดดูแล้ว bytes/hash/key/job state ไม่เปลี่ยน ไม่ส่ง HTTP หรือสร้าง delivery ใหม่
  - คำตอบผู้ใช้ (11-13), Q2.4: Source inspection รายแถวแบบ read-only ตาม M-18; แยกยอดแถวที่อ่านพบ/Source ที่บันทึกและยอดส่งตามสถานะโดยไม่ปน attempts พร้อมแสดง import state เพื่อไม่อ้างว่าอ่านครบขณะ import ยังไม่จบ
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 7.1: ชุดข้อมูล delivery log ตาม M-10; ผู้ใช้งาน/ขอบเขต action และ raw inspection แยกยังรอข้อสรุป ไม่ถือว่าปิดทั้ง task
- [x] **M4.4** ทดสอบ end-to-end Mapping → Informs บนเครื่อง — เสร็จเมื่อ: fixture ครบ, source/payload/delivery counts กระทบยอดได้, replay/replaced/rejection/throttle/restart ผ่าน, `scripts/check.ps1` ผ่านและบันทึก repo+commit/ผลจริง · เป้า **11-26** · ปิด11-18เฉพาะlocalintake acceptanceกับInforms `f1eca8d` ตาม M-37; company/business/Web acceptanceแยกติดตาม
  - ✅ 11-18 sourcecommit `1b5f2f2` รวม12ไฟล์บนdevelop; unit38/38 integration171/171 external9/9ไม่มีskipและEFmodelผ่าน; แบบ/หลักฐานอยู่ในcommitเอกสารชุดนี้ ยังไม่push
  - ✅ 11-18 source12ไฟล์บนฐานMapping `3fd48f8`; test-e2e.ps1 -Gateเรียกcheck.ps1ผ่านunit38/38 integration171/171 EFmodel และexternal9/9รันจริงไม่มีskip; FullMTLS2556/3formats/replay/replaced/400/409/real429/503/restart/ครบ6/streamingregressionผ่าน; [หลักฐาน/รายการไฟล์](m4-4-informs-end-to-end-design.md); ยังไม่commit/push
  - 🔄 11-18 [แบบ/public seams/source batch M4.4](m4-4-informs-end-to-end-design.md) พร้อมแล้ว; Informs origin/develop `f1eca8d` รองรับ D-80 และ HTTP schema preflightจริงได้200 (checkoutเดิมยังเก่า); รอยืนยันก่อนเพิ่มtests/runner ไม่มี source editsและยังไม่ส่งใบ จึงยังไม่ปิดtask
  - Dev 1: (11-19) **Q4 ไม่บล็อกแล้ว** — Informs รับ `fileReceivedAt` ตั้งแต่ commit `1bc8d1e` (T2.6, 2026-11-16, อยู่ใน `origin/develop` แล้ว) · ให้ pull repo Informs `develop` → `./scripts/up.ps1` ใหม่ → Mapping ดึง schema จาก `GET /api/pa/informs/schema/1` อีกครั้ง (snapshot เดิมน่าจะดึงจาก Informs ก่อน `1bc8d1e`) · Informs + Mapping ใช้ compose project `prjinsurancevalidation` เดียวกัน → `http://informs:8080` ใน container ใช้ได้ · เสนอเป้า M4.4 **11-20** แทน 11-26 (แผนรวมฉบับ 4 ร่าง) แล้ว Dev 1 ทดสอบร่วมด้วยไฟล์ 2,556 แถว 11-23

### M5 — ส่งต่อ Web และเตรียม Kubernetes

- [ ] **M5.1** เตรียม environment config, Linux paths, share mount, probes และ shutdown/recovery — เสร็จเมื่อ: `/health/live` และ `/health/ready` (ตรวจ DB) ใช้ได้, secrets/key references ไม่อยู่ใน image, pod restart ไม่ทำงานหาย, deploy/migration requirements ส่งให้ Dev 1/ทีม infrastructure · เป้า 12-04
  - Dev 1: (11-17) Informs + Products ทำ probe แล้ว — ขอให้ Mapping ใช้แบบเดียวกันเพื่อให้ manifest ของ Kubernetes เหมือนกันทั้ง 3 service: package `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 10.0.4 ใน `Mapping.Api` · `builder.Services.AddHealthChecks().AddDbContextCheck<MappingDbContext>(tags: ["ready"]);` · `app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });` (ไม่ตรวจอะไร = process ยังตอบ) · `app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });` (ตรวจ DB อย่างเดียว — ไม่ใส่ Kafka / Informs API / share folder เพราะตอนนั้นล่ม งานควร retry ไม่ใช่ตัด pod ออก) · ตอบข้อความธรรมดา `Healthy` / `Unhealthy` (`200` / `503`) ไม่ห่อ `BaseResponse` · test: integration test 2 เคส — DB ปกติ live/ready `200` · connection string ชี้ `Host=127.0.0.1;Port=1` → live `200` ready `503`
- [ ] **M5.2** ส่ง API contract/OpenAPI/examples ให้ทำ Web — เสร็จเมื่อ: เอกสารตัวอย่าง/DTO/states/counts/action availability ตรง API ที่รันจริง, CORS/base URL พร้อม, ไม่มี docs ใน repo โค้ด · เป้า 11-27
- [ ] **M5.3** Implement และเชื่อมหน้า mapping หลังได้รับ Web design — เสร็จเมื่อ: scope ที่ตกลงใช้งานบนเครื่องผ่าน `core/mapping-api.ts` + `features/mapping/`, build/unit tests ของ Web ผ่าน, ไม่แก้เมนูอื่นและไม่เพิ่ม package โดยยังไม่บอก Dev 1 · เป้า **12-03** เดิม · [แบบหลาย format](multi-format-api-web-design.md) ให้ Config editor/preview/jobs ใช้กับ Excel/Delimited/FixedWidth โดยไม่มี field MTLS hardcode; ขอบเขต/วันส่งเพิ่มรอ Q9 และ layout รายละเอียดรอ review design
  - [x] **M5.3.1** ปรับ Map required ให้ผู้ใช้ทั่วไปเลือกเงื่อนไขได้ — เสร็จเมื่อ: เงื่อนไขแถว/รายการมีตัวเลือกและกลุ่ม all/any ซ้อน, คงโปรแกรมเดิม, กรอกไม่ครบต้องแก้ก่อน Preview/ตรวจทาน, build/tests และ browser QA ผ่าน · เป้า 11-25 · ✅ 11-25 Web `e6349e4` (โค้ด) + `a85f618` (styles), build ผ่าน, tests **132/132**, Edge browser QA ผ่านตาม §1/M-42
  - Dev 2 (11-23): เพิ่มการแสดงชื่อตารางต้นทางตาม M-38 แล้ว; Mapping build/API tests 2/2 และ Web build/tests 78/78 ผ่าน; ยังรอตรวจ runtime รุ่นใหม่เพราะ Docker local มี filesystem error ตาม §1 ไม่ปิด M5.3 ทั้งข้อ
  - Dev 2 (11-23, เพิ่มเติม): ผู้ใช้ยืนยันให้ตั้งชื่อตารางเองตาม M-39; wizard/API/runtime และ tests แก้แล้ว, build ทั้งสอง repo/unit 39/39/Web 80/80/guard smoke 15/15/EF model ผ่าน; PostgreSQL integration gate และการเปิด runtime ใหม่ยังติด Docker ตาม §1
  - Dev 2 (11-23, หลังเปิด Docker): integration subset ของ M-39 ผ่าน **15/15**, local Mapping recreate เป็นรุ่นใหม่แล้วโดยรักษา environment/volumes เดิม; Mapping/Informs readiness และ Web :4200 ตอบ **200**; ปิดข้อจำกัด Docker ของงานนี้ แต่ไม่ปิด M5.3 ทั้งข้อ
  - Dev 2 (11-23, layout): ชื่อ Mapping / Input folder / ชื่อตารางต้นทางอยู่แถวเดียวกัน 3 คอลัมน์ตามผู้ใช้ยืนยัน; Web build และ dev-server compile ผ่าน
  - Dev 2 (11-23, Source fields): ถอดช่องชนิดในขั้นที่ 1 และการใช้ hint ที่ซ่อนอยู่ตาม M-40; ปรับตาราง/tour แล้ว, Web build และ tests **80/80** ผ่าน
  - Dev 2 (11-23, ชื่อที่แสดง): ถอดช่องชื่อที่แสดงในขั้นที่ 1 ตาม M-41; Source fields ใหม่ใช้ Key แทนและไม่คัดลอก displayName ที่ซ่อนอยู่; Web build/tests **80/80** ผ่าน
  - Dev 2 (11-23, commit/push): Mapping `16ba7e7` และ Web `e455938`/`00191b0` push `origin/develop` แล้ว; full check ผ่าน unit **39/39**, integration **178 passed / 9 skipped**, EF model ผ่าน เมื่อใช้ runsettings ชั่วคราวจำกัด 2 threads ตามหลักฐาน §1; ไม่ปิด M5.3 ทั้งข้อ
  - คำตอบผู้ใช้ (11-13), Q2.5: รวมการเปิด JSON Delivery Payload แบบ read-only เพื่อเทียบ Source ตาม M-19; แสดงเฉพาะ snapshot ที่มีจริง รวม rejected delivery ถ้ายังไม่มี payload ให้แสดงสถานะ/diagnostic ตามจริง
  - คำตอบผู้ใช้ (11-13), Q2.4: รวมการเปิด Source ดิบรายแถวแบบ read-only จากรายการไฟล์/แถวตาม M-18; ไม่ทำหน้าจัดการใบหรือ business validation ซ้ำกับ Informs/UW
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 8: Config (สร้าง version/validate/preview/activate/disable) + ผลการส่ง (ไฟล์/แถว/attempts) + retry ตามสถานะ ตาม M-12; ยังรอ layout/interaction จาก Web design และต้องยืนยันรายการไฟล์ก่อนแก้ source ตามเดิม
  - คำตอบผู้ใช้ (11-13), Q2.2: รวมการกำหนด fields/สร้าง physical Source Table ผ่านหน้า Mapping ตาม M-16 โดยใช้ API จาก M1.5; layout/interaction และกติกาแก้ schema ยังต้องปิดก่อน implement

  - [x] **M5.3.2** ตารางเลือกปลายทางและ Preview sticky — เสร็จเมื่อ: ค้นหา/เลือก/เปิดแก้ไข/reload รายการใหม่ได้โดยคง mapping, เลือก revision ล่าสุดแล้วรักษา path เดิม, layout 60/40 และ sticky scroll ผ่าน browser, build/tests ผ่าน · เป้า 11-25 · ✅ 11-25 Web `e6349e4` (โค้ด) + `a85f618` (styles), build ผ่าน, tests **132/132**, Edge QA 1280/1440 px ผ่านตาม §1/M-43

  - [x] **M5.3.3** จัดการปลายทางใน modal — เสร็จเมื่อ: หน้าหลักแสดงแบบย่อ, เลือกใน modal แล้วต้องยืนยันก่อนใช้, ยกเลิก/reload ไม่เปลี่ยน applied snapshot/mapping/Preview, error/loading/keyboard/focus และ 60/40 sticky ผ่าน browser, build/tests ผ่าน · เป้า 11-25 · ✅ 11-25 Web `e6349e4` (โค้ด) + `a85f618` (styles), build ผ่าน, tests **132/132**, Edge QA 1280/1440 px ผ่านตาม §1/M-44

  - [x] **M5.3.4** ตารางจับคู่จากต้นทางและ modal รวม array — เสร็จเมื่อ: ตารางไม่มีตัวอย่างและเริ่มจาก Source fields, one-to-many/shared/ค่าอื่น/array/เงื่อนไขแก้ผ่าน draft modal เดียว, ยกเลิกคงของเดิม, build/tests/browser ผ่าน · เป้า 11-25 · ✅ 11-25 Web `e6349e4` (โค้ด) + `a85f618` (styles), build ผ่าน, tests **132/132**, Edge QA 1280/1440 px ผ่านตาม §1/M-45

  - [x] **M5.3.5** การ์ดจับคู่และ checkbox เงื่อนไขพิเศษตามภาพ — เสร็จเมื่อ: หน้าต่างจากแถวไม่มีแท็บเดิม มีการ์ด/เพิ่มปลายทาง/checkbox ขยาย case ซ้อนและ array Index/field/emitWhen, draft/ยืนยัน/ยกเลิกและ mapping อื่นคงเดิม, build/tests/browser ผ่าน · เป้า 11-25 · ✅ 11-25 Web `34414e5` (โค้ด) + `0c2bc9e` (styles) push `origin/develop` แล้ว, build ผ่าน, tests รุ่นสุดท้าย **141/141**, Edge QA 600/1280/1440 px ตาม §1/M-46
    - Dev 2 (11-25, เพิ่มเติมตามคำยืนยัน): ถอดตัวเลือก `ค่าจากคอลัมน์` ระดับหลักในหน้าต่างที่เลือก source แล้ว ใช้ source ของแถวอัตโนมัติ; แก้ `value-expression-editor.ts`/`source-mapping-editor.html`, build/tests **136/136** และ Edge QA ผ่านตาม §1; ยังไม่ commit/push
    - Dev 2 (11-25, แก้ expression ลูก): ผู้ใช้ยืนยันให้ถอดทั้งชนิด source และตัวเลือกชื่อ field ที่ซ้ำใน editor ลูกด้วย; ส่ง source ลงทุกระดับและไม่แสดง source leaf controls, แก้เฉพาะ `value-expression-editor.ts`, build/tests **136/136** และ Edge QA พร้อมตรวจภาพผ่านตาม §1; ยังไม่ commit/push
    - Dev 2 (11-25, array): ผู้ใช้ยืนยันให้เอาส่วน `เงื่อนไขการจับคู่` ใต้กรอบ array ออก; แก้เฉพาะ `source-mapping-editor.html`, build/Edge QA/ตรวจภาพผ่านตาม §1 โดย Index/field/เงื่อนไขส่ง/เพิ่มรายการ/ยืนยันและยกเลิกยังใช้งานได้; ไม่รัน unit tests ซ้ำรอบนี้ และยังไม่ commit/push
    - Dev 2 (11-25, ปุ่มส่งทุกแถว): ตามคำยืนยันผู้ใช้ ลบปุ่ม `ส่งทุกแถว` จากส่วนจับคู่ข้อมูลจากต้นทางและขั้น tour ที่ชี้ปุ่มนี้; แก้ `mapping-new.html`/`mapping-new-tour.ts`, build และ `git diff --check` ผ่านตาม §1; ยังไม่ commit/push
    - Dev 2 (11-25, ปุ่มจัดการด้านบน): ตามคำยืนยันผู้ใช้ ลบปุ่ม `จัดการการจับคู่` ด้านบนกับ tour `automap` และปรับค่าเริ่มต้นของจุดคืน focus ให้ตรงปุ่มที่ยังอยู่; แก้ `mapping-new.html`/`mapping-new-tour.ts`/`mapping-new.ts`, build/tests **136/136** และ `git diff --check` ผ่านตาม §1; ยังไม่ commit/push

  - [x] **M5.3.6** รวมรายการของ array เดียวกันในการ์ดเดียว — เสร็จเมื่อ: เพิ่ม/เปิดค่าที่บันทึกใน array เดียวรวม card เดียว ต่างกลุ่ม/concrete parent แยก card, expressions/เงื่อนไข/ยืนยันและยกเลิกคงเดิม, build/tests/browser QA ผ่าน · เป้า 11-25 · ✅ 11-25 Web `34414e5` (โค้ด) + `0c2bc9e` (styles) push `origin/develop` แล้ว, build ผ่าน, tests **141/141**, Edge QA 600/1280/1440 px ตาม §1/M-47
    - Dev 2 (11-25, ข้อความ field): ตามคำยืนยันผู้ใช้ ลบ ` · มีการจับคู่แล้ว` จาก labels ใน `source-mapping-editor.ts` ทั้ง 3 จุด; build/`git diff --check` ผ่านและ Web HTTP 200 ตาม §1; ไม่รัน unit/browser tests ซ้ำสำหรับการแก้ข้อความ และยังไม่ commit/push

### M6 — ทดสอบร่วมและเตรียม UAT

- [ ] **M6.1** ทดสอบร่วมและ recovery บน cluster — เสร็จเมื่อ: fixture ผ่านเส้นทาง share → Mapping → Informs ครบ, ทดสอบ restart/หลาย consumer/permissions/mount/secrets พร้อมหลักฐาน environment · เป้า 12-09
- [ ] **M6.2** ปิด regression และเตรียมหลักฐาน UAT — เสร็จเมื่อ: acceptance ในแผน API §9 ผ่าน, issue ที่บล็อกถูกปิด, มีวิธีดูยอด/แก้เหตุส่งไม่สำเร็จ/retry และ owner ของข้อจำกัดที่ยังค้าง · เป้า 12-14

Source catalog/การกำหนด fields และสร้าง physical Source Table ผ่าน Web ผู้ใช้เลือกแล้วตาม M-16 และเพิ่ม M1.5; ผู้ใช้ขอ reader/API/Web สำหรับ Excel/Delimited/FixedWidth แล้ว จึงเพิ่ม M2.6/M2.7 และ Q9 เพื่อให้ Dev 1/PM ตกลง scope/เป้า ส่วน Table verification/impact admin เพิ่มเติม, destination อื่น, generic contract/profile admin, business resend/rollout ยังอยู่ใน [feature matrix](api-implementation-plan.md#2-ขอบเขตและการเทียบ-feature) และ Q2/Q3

## 4. การตัดสินใจของ mapping (`M-xx`)

> เรื่องภายใน mapping ที่ Dev 2 ตัดสินเองได้ (ตารางใน schema `mapping`, วิธีอ่านไฟล์, retry / ความขนาน, โครง config mapping, หน้าเว็บ mapping) · เรื่องที่กระทบ contract / เส้นแบ่งงาน / ขอบเขต / วันส่งงาน → ไป §5 ก่อน
>
> ```markdown
> ### M-01 <หัวข้อสั้น>
> - **ตัดสิน (YYYY-MM-DD):** ...
> - **ทำไม:** ...
> - **ไม่เลือก:** <ทางอื่นที่คิดไว้ + เหตุผล>
> - **ห้าม:** (ถ้ามี)
> ```

### M-01 วาง implementation plan แบบ API ก่อน Web และตามขอบเขต phase 1
- **ตัดสิน (2026-11-13):** ใช้เอกสารใน `docs/mapping-phase-1/` เป็นหลัก ใช้ reverse spec เป็น feature inventory; แยกงานที่จำเป็นต่อ MTLS → Informs และข้อเสนอ feature ที่เลื่อนลงใน [แผน API](api-implementation-plan.md) เริ่ม API ก่อนและรอ Web design ตามคำสั่งผู้ใช้
- **ทำไม:** ให้แผนครบทั้ง demo แต่ไม่ถือว่า feature ทุก destination และ prototype อยู่ใน phase 1 แล้ว; API สามารถตรวจรับได้ก่อนมีหน้าจอ
- **ไม่เลือก:** ยก demo ทั้ง application หรือกำหนด Web layout ล่วงหน้า เพราะขัด D-77/stack ของ Web และยังไม่มี design
- **ห้าม:** ใช้แผนนี้แทนคำตอบ kickoff หรือเปลี่ยน scope/contract/milestone โดยไม่ตกลงกับ Dev 1; ยังต้องขอยืนยันรายการไฟล์ก่อนแก้ source ทุกชุด

### M-02 ใช้โครง repo จริงและ pattern ของ Informs เป็นจุดตั้งต้น
- **ตัดสิน (2026-11-13):** วาง feature ใน 4 layer เดิมของ Mapping; วาง hosted services/outbox/Kafka/typed HttpClient ตามรูปแบบ Informs โดยยังไม่เพิ่ม Worker/Shared project และใช้ EF migrations/migrator ของ Mapping
- **ทำไม:** D-77 ให้ทีมเปิดแต่ละ repo แล้วพบโครงเดียวกัน; Mapping ปัจจุบันเป็น scaffold ยังไม่มี feature ให้ถือว่าเสร็จแล้ว
- **ไม่เลือก:** ยก Dapper/embedded SQL migrations, .NET 8 หรือ startup auto-migrate ของ demo มาทั้งชุด เพราะไม่ตรง stack และ deployment flow ของ repo จริง
- **ห้าม:** project reference/DB write ข้าม service หรือให้ code/test/script อ้าง path repo เอกสาร; การใช้ Kafka/topics จริงยังรอ Q1 ข้อ 9/Q7

### M-03 ย้าย Transformation Program ทั้งหมด โดยให้ MTLS ใช้งานได้ก่อน
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 2 ให้ย้ายความสามารถ Transformation Program ทั้งหมดจาก demo เข้าสู่โครง Mapping ตาม D-77; M2.2 ทำส่วนที่ MTLS ต้องใช้ก่อน และ M2.5 เก็บส่วนที่เหลือ ไม่ตัด operator ที่ MTLS ยังไม่ใช้ทิ้ง
- **ทำไม:** ผู้ใช้ต้องการ engine ครบ โดยให้เส้นทาง MTLS → Informs เป็นลำดับแรก
- **ไม่เลือก:** เขียน C# เฉพาะ MTLS แทน engine หรือย้ายเพียง MTLS subset แล้วตัดส่วนอื่นออกจากแผน
- **ขอบเขต:** value expressions 13 แบบ, predicates 17 แบบ, conversion targets 7 ชนิด, filter/0..N branches, nested object/array, deterministic evaluation/limits และ Named Transform/Lookup ตาม capability catalog ของ demo; รายการตั้งต้นมี `formatRegNo@1`, `mtiOriginalPolicyExists@1`, `mtiOriginalLoanExists@1`
- **ข้อจำกัด:** Named Lookup เดิมอาศัย `mti_original` และ Table Catalog ของ demo จึงต้องปิดแบบ adapter/แหล่งข้อมูลใน Q2/Q7 ก่อนเปิดใช้งาน; ไม่ถือว่าคำตอบนี้อนุมัติ partner ใหม่, destination อื่น, dynamic Table admin หรือการตรวจ product/Client API ใน Mapping ตาม D-01/D-06 การตรึง semantics ต้อง review ความเหมาะสม ไม่คัด `sqlserver-legacy-v1` เป็นกฎกลางโดยอัตโนมัติ
- **ห้าม:** ใช้ filter/branches ทิ้งแถว MTLS หรือเปลี่ยนข้อกำหนด 1 output `default` ต่อแถวโดยไม่มีข้อตกลง; แสดง capability ว่าพร้อมทั้งที่ยังไม่ implement/ไม่มี dependency, เปลี่ยน milestone เอง หรือแก้ source โดยไม่ยืนยันรายการไฟล์

### M-04 เก็บ default ผู้รับประโยชน์ใน versioned config ของ MTLS
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 4 ให้กำหนด default ตาม BR-035 ใน Transformation Program ของ config MTLS แต่ละ version; งานที่รับแล้วใช้ version ที่ pin ไว้ตามเดิม
- **ทำไม:** default เป็นกฎของ partner ตาม D-06 และต้องตรวจย้อนหลังได้จาก config version โดยไม่แก้ Source ตาม D-07
- **ไม่เลือก:** hardcode default เฉพาะ MTLS ใน C# หรือทำเป็น default กลางของทุก partner
- **ตัวอย่างข้อมูลสมมติ:** Source ที่ช่องชื่อผู้รับประโยชน์ทั้งสองไม่มีค่า → ส่วนของ JSON ที่ config สร้างคือ `"beneficiaries": [{"seq": 1, "fullName": "ทายาทตามกฎหมาย"}]` ภายใน `inform.insureds[0]`; Source เดิมยังคงเดิม ตัวอย่างนี้แสดงเฉพาะส่วนที่เกี่ยวกับ default ไม่ใช่ payload เต็ม
- **ห้าม:** เพิ่ม field สัดส่วน/กำหนดรหัส relationship ขึ้นเอง; เรื่องสัดส่วน 100% ที่ schema ไม่มี field ยังรอคำตอบ Q5 และไม่ถือว่าปิดด้วยการเลือกตำแหน่ง config

### M-05 เก็บค่าคงที่และกฎ P/C ใน versioned config ของ MTLS
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 5 ให้ใช้ `literal` สำหรับค่าคงที่ `MTLSPA`, `MTL2`, `74000490`, `FILE`, `PRIMARY` และ `case` สำหรับ P/C ใน Transformation Program ของ config MTLS แต่ละ version; งานเดิมใช้ config ที่ pin ไว้
- **ทำไม:** กฎของ partner อยู่ใน config ที่ตรวจย้อนหลังได้ตาม D-06/BR-025 ไม่ต้องแก้โค้ดและ deploy เมื่อปรับ definition
- **ไม่เลือก:** hardcode ค่าคงที่หรือเงื่อนไข MTLS ใน C# หรือใช้ค่าปัจจุบันแทน config version ที่งานเดิมอ้าง
- **ตัวอย่างข้อมูลสมมติ:** Source `Transaction Type=P` → `inform.transactionType=NEW`; Source `Transaction Type=C` → `inform.transactionType=CANCELLED`; ทั้งสองใช้ `inform.productCode=MTLSPA`, `inform.agentCode=74000490`, `inform.saleChannel=FILE`, `inform.insureds[0].packageCode=MTL2`, `inform.insureds[0].relation=PRIMARY` จาก config และเก็บ Source เดิมตาม D-07
- **ห้าม:** เติมเบี้ย/ทุนจาก package ตาม D-06; เดา fallback ของ transaction type ที่ว่างหรือไม่ใช่ P/C หรือเพิ่ม contract field เอง ประเด็นค่าที่สร้าง payload ผ่าน schema ไม่ได้ยังรอ Q5

### M-06 ใช้ลำดับแถวข้อมูลสำหรับ sourceRowNo
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 6.1 ใช้ลำดับแถวข้อมูลที่อ่านจาก sheet แรก เริ่ม 1 ไม่รวม header/แถวว่าง เป็น `sourceRowNo`; persist พร้อม Source แล้วใช้เลขเดิมเมื่อ resume/retry ไม่ใช้ลำดับ HTTP arrival ตาม D-23
- **ทำไม:** ลำดับไม่ขึ้นกับคุณภาพของ `nSeq` หรือความขนานในการส่ง; ตรวจกลับตำแหน่งต้นทางได้ด้วย Physical Line Number ที่เก็บแยก
- **ไม่เลือก:** ใช้ค่า `nSeq` เป็น `sourceRowNo`; ค่าว่าง/ซ้ำ/ไม่เรียง/มี gap ของ `nSeq` เก็บตาม Source ดิบและไม่เปลี่ยนลำดับแถวข้อมูล
- **ตัวอย่างข้อมูลสมมติ:** physical row 1 เป็น header; row 2 มีข้อมูลและ `nSeq=10` → `sourceRowNo=1`; row 3 ว่าง → ไม่นับ; row 4 มีข้อมูลและ `nSeq=10` → `sourceRowNo=2` โดย Source ของสองแถวเก็บ Physical Line Number เป็น 2 และ 4 ตามลำดับ
- **ห้าม:** ข้ามแถวข้อมูลเพียงเพราะแปลงค่าธุรกิจไม่ได้ตาม D-04 หรือเปลี่ยนลำดับเมื่อ retry; การจัดเก็บเวลารับไฟล์ครั้งแรกใน Q4 และ duplicate-content policy ใน Q6 ยังรอข้อสรุป

### M-07 เริ่มส่ง Informs พร้อมกันสูงสุด 1 request
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 6.2 กำหนดค่าเริ่ม HTTP concurrency ไป Informs เป็น 1 request และปรับผ่าน config ภายหลังจากผลทดสอบ/ข้อจำกัดของ Informs ตาม D-03
- **ทำไม:** เริ่มตรวจเส้นทาง MTLS และผลการส่งก่อนปรับความขนาน โดยไม่เปลี่ยน logic หรือ deploy โค้ดเพื่อแก้ค่าความขนาน
- **ไม่เลือก:** ส่งหลาย request พร้อมกันตั้งแต่เริ่มหรือ hardcode ค่าใน C#
- **ตัวอย่างข้อมูลสมมติ:** Delivery A/B/C พร้อมส่งพร้อมกัน → เมื่อ A กำลังส่ง B/C ยังรอช่องส่ง; หลัง A จบจึงเริ่ม request ถัดไป โดยลำดับการประเมินของ Informs ยังคงอิง D-23 ไม่ใช่ HTTP arrival
- **ข้อจำกัด:** ต้องปิดวิธีคุมเพดานในกรณีหลาย pod ใน M1.1/M4.2; ไม่ตีความว่าเพิ่ม pod แล้วเพิ่มเพดานรวมได้โดยอัตโนมัติ
- **ห้าม:** เปลี่ยน bytes/key ของ delivery เดิมเพื่อเพิ่ม concurrency หรือวนส่งต่อโดยไม่เคารพ `429`/`Retry-After`; จำนวน attempts/backoff/timeout ยังรอข้อสรุปในส่วนที่เหลือของ Q1 ข้อ 6

### M-08 จำกัด delivery รวมสูงสุด 6 attempts
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 6.3 กำหนดเพดานส่งต่อ delivery เป็น 6 attempts รวมการส่งครั้งแรก 1 และ retry สูงสุด 5 สำหรับ transient failure เช่น `429`/`5xx`/timeout; ครบแล้วหยุด automatic retry และแสดงเหตุที่ต้องตรวจสอบ
- **ทำไม:** จำนวนครั้งมีเพดานตรวจสอบได้ ไม่วนส่งไม่สิ้นสุด และ restart ไม่ทำให้เริ่มนับใหม่
- **ไม่เลือก:** ตีความเป็น retry 6 ครั้งหลังครั้งแรก (รวม 7) หรือรีเซ็ตโควตาเมื่อ pod restart/Kafka redelivery
- **ตัวอย่างข้อมูลสมมติ:** attempt 1 timeout; attempts 2–6 ได้ `503` → หยุดที่ `Failed` พร้อมประวัติ 6 ครั้ง ไม่มี automatic attempt 7; ทุกครั้งใช้ bytes/hash/key เดิมตาม D-44
- **ห้าม:** auto retry `400/409` ตาม D-04/D-05 หรือเพิ่มการส่งซ้อนจาก HttpClient ที่ไม่ถูกนับ; `429` ต้องเคารพ `Retry-After` และหลังครบเพดานไม่เปิด manual retry ตามคำตอบ Q2.6/M-20 ไม่รีเซ็ตจำนวนครั้งเอง

### M-09 ตรึงเวลารับไฟล์ครั้งแรกเมื่อสร้าง File Job
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 6.4 ให้ใช้เวลาที่ Mapping รับไฟล์และสร้าง File Job ครั้งแรกเป็นเวลารับไฟล์ครั้งแรกฝั่ง Mapping; บันทึก UTC พร้อม File Job และ version pin แล้วคงเดิมเมื่อ resume/retry โดยใช้ `FileJob.CreatedAt` ตามคำชี้แจงภายหลังใน M-23 ไม่เพิ่ม field เวลาแยก
- **ทำไม:** มีเวลารับงานที่คงที่สำหรับตรวจย้อนหลังตาม D-23 ไม่เปลี่ยนตามเวลาที่ worker กลับมาทำงานหรือส่ง HTTP ซ้ำ
- **ไม่เลือก:** ใช้เวลาที่ retry/ส่ง HTTP ครั้งล่าสุด หรือเวลาแก้ไขไฟล์ต้นทางแทนเวลารับ File Job เดิม
- **ตัวอย่างข้อมูลสมมติ:** สร้าง File Job A เวลา `2026-11-13T03:00:00Z`; import กลับมาทำต่อเวลา `03:10:00Z` และส่ง HTTP ซ้ำเวลา `03:20:00Z` → เวลารับไฟล์ครั้งแรกของ A ยังเป็น `2026-11-13T03:00:00Z`
- **ข้อจำกัด:** การเก็บเวลานี้ใน Mapping ยังไม่ทำให้ Informs ใช้เวลาเดียวกัน; schema/DTO ปัจจุบันไม่มี field รับเวลานี้ และ `ReceivePaInformCommandHandler` สร้าง `ReceivedAt` จาก `TimeProvider` ของ Informs ต้องตกลงวิธีส่ง/ใช้เวลาใน Q4 ก่อนอ้างว่ารักษา D-23 ข้าม service ได้
- **ห้าม:** เพิ่ม field ใน `pa-inform/1` หรือแก้โค้ด Informs เอง; การรับ occurrence ใหม่/duplicate-content ยังรอ Q6 ไม่เปลี่ยน File Job identity เพื่อรีเซ็ตเวลาโดยปริยาย

### M-10 ชุดข้อมูล delivery log สำหรับช่วงแรก
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 7.1 ให้ delivery log มีชื่อไฟล์/แถว/config version, ยอดทั้งหมด/ส่งสำเร็จ/รอส่ง/ส่งไม่สำเร็จ, สถานะ/HTTP status/error code/path, จำนวน attempts และเวลาส่งแต่ละครั้ง, `informId`/`replayed`/`replaced`; รายการปกติไม่แสดง raw Source/payload
- **ทำไม:** ติดตามแถวที่ Informs รับหรือยังไม่รับได้ตาม D-36 โดยแยกจำนวนผลลัพธ์จากจำนวนครั้งที่ส่ง
- **ไม่เลือก:** ใช้จำนวน HTTP attempts เป็นยอดใบที่ส่งสำเร็จ หรือแสดง raw Source/payload เป็นข้อมูลปกติในรายการ
- **ตัวอย่างข้อมูลสมมติ:** แถวข้อมูล 1 แถวได้ output `default` มี attempt 1 เป็น `503`, attempt 2 เป็น `201` พร้อม `informId` → ยอดส่งสำเร็จ 1, attempts 2; การรับสำเร็จยังไม่หมายถึงผ่าน business validation ตาม D-03
- **ข้อจำกัด:** file summary ต้องแยก import/transform/delivery และรวมแถวที่สร้าง Delivery Job ไม่ได้ในยอดยังไม่ส่งตาม D-36; รายละเอียด DTO/count definitions ปิดใน M1.2 ส่วนผู้ใช้งาน log ในข้อ 7.2, raw inspection แยกและ action scope ใน Q2/Q7 ยังรอคำตอบ
- **ห้าม:** ใส่ PII/raw HTTP response ใน error ที่แสดง/log; ไม่ตีความว่าการตกลงชุดข้อมูลนี้อนุมัติหน้าดู raw หรือระบบสิทธิ์ใหม่แล้ว

### M-11 Delivery log สำหรับทีม Mapping / Dev / Ops
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 7.2 ให้กลุ่มผู้ใช้งาน delivery log ช่วง phase 1 เป็นทีมดูแล Mapping / Dev / Ops โดยใช้ชุดข้อมูล M-10
- **ทำไม:** ให้ทีมดูแลการรับไฟล์/การแปลง/การส่งติดตามและแก้เหตุขัดข้องตาม D-36; ไม่เพิ่ม workflow ของ UW ในหน้า Mapping โดยปริยาย
- **ไม่เลือก:** ขยายกลุ่มผู้ใช้งานที่ต้องออกแบบรองรับให้รวมทีม UW ในช่วงแรก
- **ตัวอย่างการใช้งาน:** Ops เปิดรายการไฟล์ที่ยังส่งไม่สำเร็จ ดูแถว/HTTP status/error code/path และประวัติ attempts แล้วติดตามตาม state/action ที่ตกลง; การดูผล validation ของใบที่รับเข้าแล้วเป็นงานของ Informs/UW ตาม D-06/D-36
- **ห้าม:** ตีความกลุ่มผู้ใช้งานเป็น access control ที่มีอยู่แล้ว; `web.md` กำหนด fixed user ไม่มีสิทธิ์แยกใน phase นี้ และข้อนี้ไม่อนุมัติ raw Source/payload inspection หรือ action เพิ่มเติม

### M-12 หน้า Mapping ช่วงแรกมี Config ผลการส่ง และ retry
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 8 ให้หน้า Mapping ช่วงแรกครอบคลุม Config (สร้าง version, validate/preview, activate/disable), ผลการส่ง (ไฟล์/แถว/attempts ตาม M-10/M-11) และ retry ตามสถานะที่อนุญาต
- **ทำไม:** ทีมดูแล Mapping จัดการ config และติดตามผลการส่งจากหน้า Web ได้ตาม D-78 โดยใช้ API ที่ตรวจรับแยกได้ก่อนมี design
- **ไม่เลือก:** ทำเพียงหน้าผลการส่ง/retry แล้วให้จัดการ config นอก Web ชั่วคราว
- **ตัวอย่างการใช้งาน:** ผู้ใช้สร้าง config version ใหม่ → validate/preview → activate; งานที่รับก่อน activate ยังอ้าง version เดิม แล้วผู้ใช้ดูผลการส่งรายไฟล์/แถว/attempt และเลือก retry ได้เมื่อผ่าน state guard และโควตาที่ตกลง
- **ข้อจำกัด:** layout/interaction และรายการหน้าจอย่อยรอ Web design; ครบ 6 attempts ไม่เปิด retry ตาม M-20 ส่วน Source/Payload read-only detail เลือกแล้วตาม M-18/M-19 และ changed-payload reprocess/business resend ปิดช่วงแรกตาม M-22
- **ห้าม:** แก้ส่วน UW/Config ของ Dev 1 นอกขอบเขต Mapping หรือเพิ่มระบบสิทธิ์นอก `web.md`; ก่อนแก้ source ต้องแจ้งรายการไฟล์และรอยืนยันตาม AGENTS.md

### M-13 ใช้ Kafka และ transactional outbox สำหรับงานภายใน Mapping
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 9.1 ใช้ Kafka + transactional outbox สำหรับงานภายใน Mapping โดยวาง hosted services/dispatcher/consumer ตาม pattern Informs และโครง D-77; การส่ง Mapping → Informs ใช้ HTTP ตาม D-03
- **ทำไม:** งานที่รับและบันทึกแล้วกลับมาประมวลผลต่อได้เมื่อ restart และมี transaction boundary ระหว่าง job กับ event ที่ตรวจสอบได้
- **ไม่เลือก:** ใช้ DB queue เป็นแนวทางหลักแทน Kafka ในช่วงแรก
- **ตัวอย่างข้อมูลสมมติ:** transaction บันทึก Row Job A และ outbox ที่มี job ID A พร้อมกัน → dispatcher publish → consumer อ่านงานจาก ID; หาก event ถูกส่งซ้ำ consumer ใช้ identity/state guard เดิม ไม่สร้าง Source/output ซ้ำ
- **ข้อจำกัด:** PostgreSQL เก็บ job/outbox แบบ atomic; event มีเฉพาะ job IDs/ชนิดงาน ไม่มี PII และ consumer ต้องรองรับ redelivery; broker/topics/groups/owner และวิธีเตรียม local ยังรอข้อ 9.2/Q7
- **ห้าม:** เปลี่ยน Mapping → Informs เป็น Kafka, แก้ repo/Compose ของ Informs เอง, อ้างว่าเลือก Kafka แล้วเท่ากับ provision infrastructure หรือผ่าน integration tests แล้ว; source/test/script/Compose ต้องแจ้งรายการไฟล์และรอยืนยันก่อนแก้ตามเดิม

### M-14 เริ่มพัฒนาและทดสอบด้วย Kafka local ผ่าน Docker
- **ตัดสิน (2026-11-13):** ตามคำตอบผู้ใช้ใน Q1 / kickoff ข้อ 9.2 ใช้ Kafka local ผ่าน Docker สำหรับพัฒนา/ทดสอบ Mapping ระหว่างรอรายละเอียด broker/topics บริษัท; ใช้แนวทาง M-13 และตรวจ pattern ของ Informs ก่อนเพิ่ม config ใน repo Mapping
- **ทำไม:** ทดสอบ transaction/outbox/consumer/redelivery ได้บนเครื่องโดยไม่ต้องรอ infrastructure บริษัท
- **ไม่เลือก:** ผูกการเริ่มพัฒนา/ทดสอบ messaging ไว้กับ broker บริษัทที่ยังไม่มีรายละเอียดให้ใช้
- **ตัวอย่างการใช้งาน:** PostgreSQL local เก็บ Row Job A + outbox → Kafka local รับ event job ID A → Mapping consumer ประมวลผล แล้วทดสอบส่ง event เดิมซ้ำโดย Source/output ไม่เพิ่มซ้ำ; ไม่มีข้อมูลส่วนบุคคลจริงใน fixture/event
- **ข้อจำกัด:** ชื่อ topics/groups, broker image/version/ports และ config local ต้องลงรายละเอียดใน M3.1; broker/topics/groups/owner บริษัทใน Q7 ยังรอข้อมูล และผล local ไม่ใช่หลักฐานว่า environment บริษัทพร้อม
- **ห้าม:** แก้ Compose/config ของ Informs หรือ provision broker บริษัทเอง; ก่อนแก้ source/Compose/test/script ใน Mapping ต้องระบุไฟล์และรอยืนยันตาม AGENTS.md การบันทึกการเลือกนี้ยังไม่ใช่การเพิ่มหรือรัน Kafka local แล้ว

### M-15 ใช้ physical Source Table แยกตาม config
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.1 ข้อ 2 ให้ใช้ physical Source Table แยกตาม config ใน schema `mapping`; เก็บค่าต้นทางก่อนแปลง พร้อม Source identity/File Job/Row Number/Physical Line Number ตาม D-07/M-06 และใช้ field catalog/table definition อ้างอิงย้อนหลัง
- **ทำไม:** ผู้ใช้ระบุว่าข้อมูลจากแต่ละเจ้ามีโครงสร้าง field ต่างกัน จึงเลือกให้ Source มีตารางและ columns แยกตาม config พร้อม Source definition ของตัวเอง; 19 columns เป็นรูปแบบของ MTLS ไม่ใช่ข้อจำกัดของทุก config
- **ไม่เลือก:** ข้อเสนอตารางกลาง `mapping.source_rows` ที่เก็บทุก column ต้นทางไว้ใน JSONB เป็นหลัก
- **ตัวอย่างข้อมูลสมมติ:** config A ผูกตาราง `mapping.source_config_a_rows` (ชื่อเพื่ออธิบาย ยังไม่ใช่แบบ naming ฉบับปิด); แถวข้อมูลมี system columns ของ Source และ raw columns เช่น `transaction_type=P`, `effective_date_raw=20261009`; Transformation Program อ่านแถวนี้แล้วสร้าง `inform.transactionType=NEW`, `inform.effectiveDate=2026-10-09` โดยไม่แก้ค่าใน Source
- **ข้อจำกัด:** M1.1 ต้องปิดชื่อ/ชนิด column, source identity/constraints, สิทธิ์ DDL และการ pin definition ให้ config version/งานเก่า; ผู้ใช้เลือกสร้างผ่าน Web ตาม M-16 และเพิ่ม columns ได้อย่างเดียวตาม M-17 แล้ว ไม่ถือว่าอนุมัติ Table verification/impact admin ทั้งชุดของ demo
- **ห้าม:** update/delete Source เดิมตาม D-07, ใช้ตาราง Source เป็น Normalized Table แทน direct JSON ที่เลือกใน Q1 ข้อ 1, สร้างตารางนอก `mapping` หรือรับ arbitrary SQL จาก browser; การเพิ่มตาราง/DDL ใน DB บริษัทให้คนรันตาม AGENTS.md

### M-16 กำหนด fields และสร้าง Source Table ผ่านหน้า Mapping
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.2 ข้อ 1 ให้กำหนด fields ผ่านหน้า Mapping แล้วระบบตรวจ definition และสร้าง physical Source Table ใน schema `mapping` พร้อม table/field metadata; ใช้ร่วมกับ M-15 โดยทำส่วน MTLS ก่อน
- **ทำไม:** แต่ละเจ้ามี fields ต่างกัน ผู้ใช้จึงตั้ง Source definition สำหรับรูปแบบไฟล์ที่ระบบรองรับได้ผ่านหน้า Mapping
- **ไม่เลือก:** ให้ Dev เตรียม migration/script ต่อ Source Table ทุกครั้งแล้วหน้า Mapping เลือกใช้ได้อย่างเดียว
- **ตัวอย่างข้อมูลสมมติ:** ผู้ใช้กำหนด Source definition A พร้อม fields `transaction_type`, `effective_date_raw` → ระบบตรวจชื่อ/ชนิดและ system columns → สร้าง metadata และ `mapping.source_config_a_rows` (ชื่อเพื่ออธิบาย) → config อ้าง Source definition นี้ ก่อนนำเข้าแถว `P`, `20261009` แล้วแปลงเป็น JSON ตาม M-15
- **ข้อจำกัด:** M1.1/M1.5 ต้องปิด allowed names/types, metadata/DDL consistency, concurrent creation/column addition, audit และ permissions โดยรักษาค่าดิบที่แปลงธุรกิจไม่ได้ตาม D-04; schema evolution ใช้ M-17 และ layout/interaction ยังรอ Web design
- **ห้าม:** รับ arbitrary SQL จาก browser, สร้างตารางนอก `mapping`, ให้ user-defined fields ชน system columns, แก้ Source เดิมตาม D-07 หรือเพิ่มระบบสิทธิ์นอก `web.md`; การเลือกนี้ยังไม่ใช่การแก้ source/รัน DDL ในงานนี้

### M-17 Source Table เพิ่ม column ได้อย่างเดียว
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.3 ข้อ 1 ให้เพิ่ม column ใน Source Table เดิมได้ แต่ห้ามลบ/rename/เปลี่ยนชนิด field เดิม; ถ้าต้องเปลี่ยนแบบนั้นให้สร้าง Source Table ใหม่และให้ config version ใหม่อ้าง definition/table ใหม่ โดยงานเก่ายังใช้ definition/table เดิม
- **ทำไม:** ปรับรับ fields เพิ่มได้โดยคงหลักฐาน Source และการอ่านย้อนหลังตาม D-07 ไม่บังคับย้ายข้อมูลเก่าเมื่อเพิ่ม field
- **ไม่เลือก:** สร้างตารางใหม่ทุกครั้งที่เพิ่ม field หรือแก้โครง field เดิมในตารางที่มีหลักฐานแล้ว
- **ตัวอย่างข้อมูลสมมติ:** definition v1 มี `field_a`, `field_b` → เพิ่ม `field_c` เป็น definition v2 ใน physical table เดิม; Source เก่าไม่ถูกเติมค่าธุรกิจย้อนหลัง และ config/job ที่ pin v1 ยังอ่านตาม v1 หากต้อง rename `field_b` ให้สร้าง table/definition ใหม่แทนการ rename ของเดิม
- **ข้อจำกัด:** M1.1/M1.5 ต้องปิด nullable/default policy ของ column ใหม่ที่ไม่เปลี่ยนค่าหลักฐานเดิม, definition version/snapshot pin, concurrency และ metadata/DDL consistency; การเปลี่ยน Source table ไม่ทำ reprocess/resend งานเก่าโดยอัตโนมัติ และ changed-payload reprocess ปิดช่วงแรกตาม M-22
- **ห้าม:** ลบ/rename/เปลี่ยนชนิด field เดิม, update/delete/backfill ค่าธุรกิจใน Source เก่า หรือเปลี่ยน definition ที่ config/job เก่าอ้างตาม D-07; การเพิ่ม column ต้องผ่าน validation/audit และอยู่ใน schema `mapping`

### M-18 มี Source view รายแถวแบบ read-only
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.4 ข้อ 1 ให้เปิดดู Source ดิบรายแถวแบบ read-only จากรายการไฟล์/แถว พร้อม config version ที่งานอ้าง, Row Number/Physical Line Number และสถานะส่ง; ใช้โดยทีม Mapping / Dev / Ops ตาม M-11
- **ทำไม:** ผู้ใช้ต้องการตรวจว่าข้อมูลเข้าครบและเทียบข้อมูลต่างกันได้; Source คือค่าที่อ่านจากไฟล์ก่อนแปลง ส่วนหน้า inform ของ Informs ดูใบที่รับเข้าแล้ว แถวที่สร้าง payload ไม่ได้หรือส่งไม่ผ่านจึงต้องตรวจฝั่ง Mapping ตาม D-07/D-36
- **ไม่เลือก:** ดูเฉพาะ metadata/error โดยไม่มีช่องทางเปิด Source ดิบ หรือทำหน้าตรวจ/แก้ข้อมูลใบซ้ำกับ Informs/UW
- **ตัวอย่างข้อมูลสมมติ:** import จบแล้วอ่านพบ 10 แถวข้อมูลและบันทึก Source 10 แถว; ส่งสำเร็จ 8 รอส่ง 1 และสร้าง payload ไม่ได้ 1 → Source view เปิดดูได้ครบ 10 แถว ส่วนยอดส่งแยกเหตุและไม่นับ attempts ซ้ำ แถววันที่ดิบ `-293` ยังดูค่าเดิมได้ตาม D-04
- **ข้อจำกัด:** รายการ log ปกติยังใช้ metadata ตาม M-10; Source detail อ่านตาม table/definition/config version ที่ pin ไม่อิง definition ล่าสุดหรือ table name จาก browser; count definitions/import completeness ปิดใน M1.2 และ layout/interaction รอ Web design การดู Delivery Payload แยกใช้ M-19 ตามคำตอบ Q2.5
- **ห้าม:** update/delete Source, reprocess/send จากการเปิดดู หรือส่ง raw values ไป application logs/Kafka events; ไม่เพิ่ม workflow ของ UW หรือระบบสิทธิ์นอก `web.md` การเปิดดู Source ไม่เปลี่ยน original ใน Informs

### M-19 ดู Delivery Payload ที่บันทึกจริงแบบ read-only
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.5 ข้อ 1 ให้เปิดดู JSON Delivery Payload ของ delivery แบบ read-only เพื่อเทียบกับ Source รวม payload ของ delivery ที่ Informs reject; ใช้ durable snapshot/bytes ที่บันทึกจริงตาม M4.1 พร้อม lineage/config version/hash ของ delivery นั้น
- **ทำไม:** ทีม Mapping / Dev / Ops ต้องตรวจค่าก่อน/หลังแปลงและสิ่งที่เตรียมส่งจริงได้โดยไม่พึ่งข้อมูลใบปัจจุบันใน Informs ซึ่งอาจยังไม่รับแถวนั้น
- **ไม่เลือก:** ดูได้เพียง Source/metadata หรือ compile ด้วย config ปัจจุบันแล้วแสดงผลแทน payload ที่บันทึกจริง
- **ตัวอย่างข้อมูลสมมติ:** Source มีวันเริ่ม `-293` → payload snapshot มี `inform.effectiveDate=null` และ `inform.extras.unparsed.effectiveDate=-293`; เปิดดู snapshot เดียวกันได้เมื่อ delivery ถูก reject โดยไม่เปลี่ยน hash/key หรือส่งใหม่
- **ข้อจำกัด:** อ่าน/decrypt snapshot ด้วย server-side key reference ตามแบบ M4.1/Q7 และไม่คืน secret/key ให้ browser; หากยังไม่มี payload หรืออ่าน snapshot ไม่ได้ให้แสดง availability/diagnostic ตามจริง ไม่สร้าง payload ทดแทน; DTO/ขนาด response/availability ปิดใน M1.2 และ layout รอ Web design
- **ห้าม:** แก้ bytes/hash/key/job state, สร้าง delivery/reprocess/resend หรือเรียก Informs จากการเปิดดู; ไม่ใส่ raw payload ในรายการ log ปกติ/application logs/Kafka events ตาม M-10 การดู payload ไม่ใช่การเปิดแก้ข้อมูลใบของ Informs

### M-20 ครบ 6 attempts แล้วไม่เปิด retry เพิ่ม
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.6 ข้อ 2 ให้หยุดที่ 6 attempts รวมครั้งแรกตาม M-08 และไม่เปิด retry หลังครบเพดานผ่าน Web; API/action guard และ worker ใช้เพดานเดียวกันสำหรับ delivery เดิม
- **ทำไม:** ผู้ใช้เลือกคงเพดานเดิมโดยไม่เพิ่มโควตา manual retry หลังครบ และประวัติ/ยอดส่งตรวจสอบได้ตรงกัน
- **ไม่เลือก:** manual retry เพิ่มครั้งละ 1 attempt หลังครบ 6 หรือเริ่ม automatic retry รอบใหม่ด้วยการรีเซ็ตจำนวนครั้ง
- **ตัวอย่างข้อมูลสมมติ:** attempt 6 ได้ `503` → delivery เป็น `Failed` พร้อมประวัติ 6 ครั้ง; Web แสดงเหตุครบเพดานและไม่มี retry ที่ใช้งานได้; เรียก retry API โดยตรงก็ถูกปฏิเสธตาม state/limit โดยไม่สร้าง attempt 7/outbox/HTTP
- **ข้อจำกัด:** manual retry ก่อนครบเพดานยังต้องผ่าน state guard/นับรวมใน 6 attempts และไม่ใช้กับ permanent rejection ตาม contract; เปิด Source/Payload detail และ attempt history เพื่อตรวจสอบได้ตาม M-18/M-19 ส่วน changed-payload reprocess/business resend ปิดช่วงแรกตาม M-22
- **ห้าม:** รีเซ็ต counter/ลบประวัติ/เปลี่ยน bytes/key หรือสร้าง delivery identity ใหม่จาก retry เพื่อหลบเพดาน; restart/redelivery/concurrent action ต้องไม่ทำให้ส่งเกินเพดาน

### M-21 ช่วงแรกส่งเฉพาะ Informs API
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q2.7 ข้อ 1 ทำปลายทาง Informs API ก่อน โดยเก็บ LegacyTable/JsonFile/DatabaseTable เป็น backlog; Mapping สร้าง JSON `pa-inform/1` และส่ง HTTP ทีละใบตาม D-02/D-03
- **ทำไม:** ให้เส้นทาง MTLS → Informs ทำงานก่อนตาม D-70 โดยไม่เพิ่ม destination runtime ที่ไม่จำเป็นต่อเส้นทางนี้
- **ไม่เลือก:** ทำ destination อื่นพร้อม Informs API ในช่วงแรกหรือถือว่าการย้าย Transformation Program ทั้งหมดใน M-03 รวมทุก destination แล้ว
- **ตัวอย่างการใช้งาน:** อ่านแถว MTLS → เก็บ Source → สร้าง durable Delivery Payload → ส่ง `POST /api/pa/informs`; ผู้ใช้ยัง preview/เปิดดู JSON ตาม M-19 ได้ โดยการเปิดดูไม่ใช่ JsonFile destination หรือการเขียน Normalized Table
- **ข้อจำกัด:** เก็บ requirement/feature inventory ของ destination อื่นไว้ใน backlog ไม่ลบ; capability catalog ระบุพร้อมใช้งานเฉพาะสิ่งที่ implement/ทดสอบแล้ว ขอบเขต Source Table ผ่าน Web ตาม M-15–M-17 ยังคงทำตามที่ตกลง
- **ห้าม:** เขียนตาราง Informs ตรงตาม D-02, เพิ่ม runtime/finalizer/DB writer ของ destination อื่นโดยตีความจากคำว่า engine ทั้งหมด หรือเปลี่ยน contract/วันส่งงานเอง

### M-22 ยังไม่เปิด changed-payload reprocess/business resend ช่วงแรก
- **ตัดสิน (2026-11-13):** ผู้ใช้เลือก Q3 ข้อ 1 ช่วงแรกยังไม่ส่ง payload ใหม่จาก Source แถวเดิมหลังเปลี่ยน config; config version ใหม่ใช้กับไฟล์ที่รับใหม่ ส่วนงานที่รับแล้วคง version/schema/profile/identity ที่ pin ไว้ และ delivery retry ใช้ bytes/hash/key เดิมภายในเพดาน M-08/M-20
- **ทำไม:** D-44 ใช้ `(fileJobId, sourceRowId, outputKey)` โดยไม่รวม config version; key เดิมของงานที่ Informs รับแล้วจะ replay ผลเดิม การเปลี่ยน key อาจทำให้ข้อมูลเก่าทับข้อมูลแก้ไขภายหลัง จึงเลื่อน feature ที่ยังไม่มี contract/lineage รองรับ
- **ไม่เลือก:** เปิด changed-payload reprocess/resend หรือเพิ่ม key/config version เข้า idempotency identity เองในช่วงแรก
- **ตัวอย่างข้อมูลสมมติ:** File Job A รับด้วย config v1 และส่งสำเร็จ → activate v2 → retry A ยังใช้ v1 และ bytes/key เดิม ไม่ส่งค่าจาก v2; File Job B ที่รับหลัง activate ใช้ v2 ตาม version pin ปกติ ทั้งนี้ file occurrence/duplicate-content policy ยังต้องเป็นไปตาม Q6
- **ขอบเขต:** ไม่เปิด changed-payload reprocess สำหรับแถวเดิมทั้ง never-delivered/rejected/Delivered ในช่วงแรก; preview แบบไม่ส่งจริงและ retry/recovery ที่คง version/identity เดิมยังทำได้ตาม state guard ไม่สร้าง delivery ใหม่เพื่อหลบเพดาน
- **ห้าม:** ใช้การเปลี่ยน config/Source Table หรือกด retry เพื่อเปลี่ยน payload/key ของ delivery เดิม, ส่งซ้ำงานที่ Delivered เพื่อแทนข้อมูลเดิม หรือข้ามเพดาน 6 attempts; feature/กติกา key-lineage ในอนาคตเก็บเป็น backlog และต้องตกลง Dev 1 ก่อนเปิด

### M-23 ใช้ FileJob.CreatedAt แทน field เวลารับไฟล์แยก
- **ตัดสิน (2026-11-13):** ผู้ใช้ชี้แจง Q4 ว่าใช้เวลา `CreatedAt` และเลือกว่าหมายถึง File Job ฝั่ง Mapping; ใช้ `CreatedAt` (`created_at` ใน DB) เป็นเวลารับ File Job ครั้งแรกตาม M-09 ไม่เพิ่ม column เวลารับไฟล์อีกชุด
- **ทำไม:** ใช้เวลาสร้างงานที่ต้องมีอยู่แล้ว โดยไม่เก็บเวลาความหมายเดียวกันซ้ำและไม่เปลี่ยนเมื่อ resume/retry
- **ไม่เลือก:** เพิ่ม field เวลารับไฟล์แยกใน File Job หรือใช้เวลาที่ Informs รับ HTTP แทนเวลาของ File Job
- **ตัวอย่างข้อมูลสมมติ:** File Job A มี `created_at=2026-11-13T03:00:00Z`; resume เวลา `03:10:00Z` และ retry เวลา `03:20:00Z` → เวลาอ้างอิงของไฟล์ยังอ่านจาก `created_at=2026-11-13T03:00:00Z` เดิม
- **ข้อจำกัด:** M1.1 ต้องตรึง CreatedAt ของ File Job พร้อม version pin และไม่ reset เมื่อกู้คืนงาน; การส่ง/ใช้ค่านี้ข้าม service ยังรอ Dev 1 ใน Q4 เพราะ contract ของ Informs ไม่มี field รับเวลา และ Informs ตั้ง ReceivedAt จากเวลา intake ปัจจุบัน
- **ห้าม:** เปลี่ยน CreatedAt ของ File Job เดิมเพื่อเปลี่ยนลำดับตาม D-23, อ้างว่า CreatedAt ฝั่ง Mapping ถูกส่งให้ Informs แล้ว หรือเพิ่ม contract field/แก้ Informs เอง; คำตอบนี้ไม่ใช่การเลือกเวลา CreatedAt/ReceivedAt ฝั่ง Informs

### M-24 ออกแบบกลไกกลางและรับ business fields ภายหลัง
- **ตัดสิน (2026-11-13):** ผู้ใช้ให้ข้ามประเด็น field/สัดส่วนใน Q5 ไว้ก่อน จะส่ง fields มาให้ภายหลัง และให้ยึดหลักครอบคลุมหลาย scenarios โดยไม่เฉพาะเจาะจง; engine ใช้ Source definition/Input Layout/Transformation Program/contract schema แบบมี version แทนการ hardcode business fields ของ MTLS
- **ทำไม:** แต่ละเจ้ามี fields ต่างกัน แต่การรับไฟล์/รักษาหลักฐาน/แปลง nested data/ส่ง/ตรวจสอบ/retry/recovery ใช้กลไกเดียวกันได้
- **ไม่เลือก:** หยุดการออกแบบกลางเพื่อถาม business field ทีละรายการต่อ หรือเดา field/default/สัดส่วนที่ยังไม่ได้รับ
- **ตัวอย่างข้อมูลสมมติ:** definition A มี `field_a`, `field_b`; เพิ่ม `field_c` ด้วย definition version ใหม่แล้ว compiler อ่าน catalog/version ที่ config pin ไม่แก้ C# เพื่อรู้จักชื่อ field ใหม่; target bindings ของ Informs ใส่เมื่อได้รับ fields/schema ที่เจ้าของรับรอง
- **ข้อจำกัด:** [ร่างข้อมูลและ runtime](data-runtime-design.md) เป็น draft/test obligations ไม่ใช่ผลทดสอบหรือ DDL/API ฉบับปิด; MTLS ยังทำก่อน, destination ช่วงแรกยังเป็น Informs API ตาม M-21 และ engine ทั้งหมดตาม M-03 ไม่ขยาย partner/destination/contract โดยอัตโนมัติ
- **ห้าม:** อ้างว่า cover ทุกกรณีโดยไม่มี scenario/หลักฐาน, แต่ง business rules/contract fields, เปิด capability ที่ dependency ยังไม่พร้อม หรือข้ามการยืนยันรายการไฟล์ก่อนแก้ source

### M-25 กันเนื้อหาไฟล์ซ้ำต่อ config ข้าม versions
- **ตัดสิน (2026-11-16):** ผู้ใช้ตอบตกลง Q6 ให้กันไฟล์เนื้อหาเดิมด้วย content hash ภายใต้ config เดิมข้าม config versions โดยไม่ใช้ชื่อไฟล์เป็นตัวตัดสิน; เนื้อหาเปลี่ยนรับเป็น File Job ใหม่และอ่านทุกแถว คนละ config ไม่อยู่ใน key เดียวกัน
- **ทำไม:** เปลี่ยนชื่อไฟล์ไม่ควรทำให้นำเข้าเนื้อหาเดิมซ้ำ ส่วนไฟล์แก้ไขต้องมี Source/lineage ใหม่ตามการรับปกติ
- **ไม่เลือก:** dedup ตามชื่อไฟล์/config version หรือรับทุกสำเนาชื่อใหม่โดยไม่ตรวจ bytes; ไม่ทำ semantic diff รายแถวแล้วส่งเฉพาะแถวที่เปลี่ยนโดยปริยาย
- **ตัวอย่างข้อมูลสมมติ:** config A/v1 รับ `a.xls` hash H1 → เปลี่ยนชื่อเป็น `b.xls` หรือใช้ A/v2 แต่ hash H1 ยังซ้ำ; แก้ bytes เป็น H2 → รับ File Job ใหม่; config B/hash H1 ไม่ชน claim ของ A การตัดสินใบซ้ำหลังส่งเป็นหน้าที่ Informs ตาม D-05/D-46
- **ข้อกำหนด:** hash = SHA-256 ของ stable snapshot bytes; UQ `(config_id,content_hash)` ใน `file_content_claims` ชี้ canonical File Job; ไม่เพิ่ม version/ชื่อ/ช่วงเวลาใน key และไม่ล้าง claim เพื่อข้ามงานเดิมที่ล้มเหลว ต้อง resume owner เดิม ส่วน path claim แยกตามแบบ §8
- **ห้าม:** ข้าม D-44/เปลี่ยน key เพื่อหลบใบซ้ำ, เปลี่ยน Source เก่า หรือถือว่า hash เปรียบเทียบความหมายใน Excel cells; Dev 1 เป็นผู้แก้สถานะ BR-001 ใน rules ไม่แก้ไฟล์นั้นเอง

### M-26 ปิดแบบ technical ภายใน Mapping ของ M1.1
- **ตัดสิน (2026-11-16):** ใช้ [data-runtime-design.md §8](data-runtime-design.md#8-ข้อกำหนด-technical-สำหรับ-implement-m11) เป็น baseline implement M1.1: raw physical columns เป็น `text NULL`, FK ผ่าน Source registry, immutable definitions/snapshots, content/path claims แยกกัน และ transaction/lease/global budget/attempt guards ตามแบบ
- **ทำไม:** ปิดรายละเอียดที่ implement และทดสอบได้จากคำตอบที่มี โดยไม่ต้องรอ business fields ของ Q5 หรือเปลี่ยน Informs contract
- **ไม่เลือก:** ติ๊ก M1.1 จาก outline อย่างเดียว, ใช้ typed raw columns ที่ทำค่าธุรกิจผิดหลุดก่อนเก็บหลักฐาน หรือถือว่า private process lock คุมหลาย pod ได้
- **ตัวอย่างหลักฐาน:** [29 checks บน PostgreSQL 15.20 local](m1-1-validation.md) ตรวจ hash scope, pin/FKs, Source immutability/เพิ่ม column, CreatedAt, output identity, attempt cap, rollback และ concurrent claims/fencing ผ่าน; proof schema ย่อไม่ใช่ EF model ของ application
- **ข้อจำกัด:** M1.2/M1.3/M1.5 และ full fixture/Kafka/HTTP tests ยังต้องทำ; Q4/Q5/Q7/Q8 และ Web design ติดตามแยก ไม่เปลี่ยน milestone/contract/business semantics หรืออ้าง corporate readiness
- **ห้าม:** ใช้การปิด design แทนการยืนยันรายการไฟล์ก่อนแก้ source หรืออ้างว่าเอกสารถูก commit/deploy แล้ว

### M-27 API/หน้า Mapping ใช้กับหลาย config และหลายรูปแบบไฟล์
- **ตัดสิน (2026-11-16):** ตามคำชี้แจงผู้ใช้ ให้ MTLS เป็นเพียง example/fixture หนึ่ง; [แบบ API/Web หลาย format](multi-format-api-web-design.md) ใช้ config/version, Source definition, Transformation Program, job/inspection/retry และหน้า Web ชุดเดียวกัน โดย Input Layout เลือก adapter `Excel`, `Delimited` (CSV/TSV) หรือ `FixedWidth` ต่อ config
- **ทำไม:** แต่ละงานมีโครง fields และ file format ต่างกัน; การ hardcode MTLS ใน API/UI จะบังคับงานอื่นให้ใช้โครงผิดและต้องแก้ code ทุกครั้งที่รับ format ใหม่
- **ไม่เลือก:** ทำ `/mtls/*`/หน้า MTLS แยกเป็นโครงหลัก หรือแสดง format ว่าพร้อมใช้งานก่อน parser/validation/tests มีจริง
- **ตัวอย่างข้อมูลสมมติ:** config A/Excel อ่าน `R001` จาก sheet row 2; config B/CSV อ่าน `R001,180` จาก physical line 2; config C/FixedWidth อ่าน `R001 000180` จาก physical line 1; ทั้งสามคืน Source fields/row identity และไป flow เดียวกัน แต่ layout options อยู่ใน version ของแต่ละ config
- **ข้อจำกัด:** การขยายจาก D-70 เรื่อง format/partner/วันส่งเป็น Q9 ให้ Dev 1/PM ตกลงก่อน source scope เพิ่ม; destination ยังเป็น Informs API ตาม M-21, business fields Q5 ยังไม่เดา และ M1.2 review route/DTO ที่กระทบ Web ยังเลื่อนตามผู้ใช้

### M-28 Named Lookup อ่าน SQL Server ภายนอกด้วย business keys
- **ตัดสิน (2026-11-17):** ผู้ใช้ระบุ SQL Server และตาราง `NewValidateDB.dbo.MTI_ORIGINAL`; ยืนยันให้ policy lookup เทียบ `SYSTEM_ID + CPRODUCTNAME + CLOANNUMBER` และ loan lookup เทียบ `SYSTEM_ID + CLOANNUMBER` โดยนับทุกแถวที่ตรง keys ตาม collation ของตาราง
- **ทำไม:** ตารางจริงไม่มี `source_row_id`; `ID` เป็น int identity, `TransID` เป็น uniqueidentifier และ primary key เป็น `POLICYTYPE + CLOANNUMBER + SOURCE` ซึ่งไม่ใช่ Source row identity ของ Mapping
- **ไม่เลือก:** ไม่ยกการไม่นับแถวตัวเองของ normalized table ใน demo มาเทียบกับ `sourceRowNo` หรือ UUID ของ Source ใหม่ เพราะเป็นคนละ identity; ไม่สร้าง normalized table หรือเขียนกลับ SQL Server เพื่อเลียนแบบ demo
- **ห้าม:** ไม่เพิ่มกฎสถานะ/กรอง `POLICYTYPE` หรือ `SOURCE` เอง; SQL เป็น parameterized read-only, ไม่คืน connection string/SQL exception ผ่าน API; schema/permission/connection ไม่พร้อมต้องได้ `LookupDependencyUnavailable` ไม่ใช่ `notExists=true`
- **การตั้งค่า:** `ConnectionStrings__MtiOriginalDb` มาจาก secret/environment; `MtiOriginalLookup__CommandTimeoutSeconds` default 10 และรับ 1..60 วินาที; ไม่เก็บ credentials ใน repo ผู้ใช้อนุญาต `Encrypt=True;TrustServerCertificate=True` เฉพาะ connection UAT นี้ ไม่ใช่การอนุญาตทั่วไปสำหรับ production

### M-29 เริ่มแกน import ของ Delimited ด้วย Source/Row Job/outbox atomic
- **ตัดสิน (2026-11-17):** ตามรายการไฟล์และขอบเขตที่ผู้ใช้ยืนยัน ทำ `FileImportService` ภายใน Application สำหรับ File Job ที่มี pinned version และ sealed snapshot แล้ว โดยเริ่ม CSV/TSV; ตรวจ input hash/size, ใช้ parser ตัวเดียวกับ preview และ commit registry/raw Source/Row Job/transform outbox/checkpoint พร้อมกัน
- **ทำไม:** M2.6 ต้องพิสูจน์ถึง Source จริง; แบบ M1.1 กำหนด transaction นี้ไว้แล้ว การมี reader หรือ test ที่เขียน Source โดยไม่มี Row Job/outbox ยังไม่พอสำหรับเส้นทาง import
- **ไม่เลือก:** ไม่สร้าง CSV-specific API, ไม่เขียน raw Source แยก transaction จากงานต่อ และไม่ใช้ latest config แทน version ของ File Job; scanner/snapshot store/archive/Kafka consumers/transform worker ทำตาม M3 แยกต่อ
- **การทำงาน:** lease owner+epoch ตรวจด้วยเวลา DB; read count เก็บเมื่อ parser อ่านครบ แยกจาก committed count; resume ข้ามเฉพาะ prefix ที่ commit แล้ว, fenced replay ต้องตรง raw เดิม; ถ้า registry/Row Job count ไม่ตรง checkpoint คืน `RecoveryRequired` ไม่สร้างงานย้อนหลังหรืออ้างสำเร็จเอง
- **การตั้งค่า:** section `FileImport` ใช้ max bytes 50 MiB, 100,000 records, 2,000,000 cells, lease 120 วินาที/heartbeat 20 วินาที; topic local default `mapping.transform.requested` เปลี่ยนผ่าน environment ได้ บริษัท topics/groups/owner ยังรอ Q7; outbox payload มีเฉพาะ UUID ของ Row Job/Source/File Job/Config Version ตาม pattern Informs
- **ห้าม:** ไม่เปิด Delimited production capability จากการมี import core อย่างเดียว, ไม่ใช้ fields ของ partner ที่ยังไม่ได้รับ, ไม่ถือว่าปิด Q9/M2.6 หรือส่วน M3 ที่ยังไม่มี implementation แล้ว

### M-30 Fixed-width ใช้ shared validator และ import core ของ M-29
- **ตัดสิน (2026-11-17):** ตาม source batch ที่ผู้ใช้ยืนยัน เพิ่ม FixedWidth dispatch ใน FileImportService และใช้ validator/reader ร่วมกับ preview; ช่วง start/length เป็นข้อความหลัง decode ตาม Fixed-width Slice เดิม เริ่มที่ 1 และอิง UTF-16 positions ของ .NET ไม่ใช่ byte offset
- **ทำไม:** ให้ผล preview/Source import ตรงกันสำหรับ UTF-8/cp874 และกัน integer overflow/overlap/short record ก่อนสร้าง Source; hash/pinning/lease/transaction ของ M-29 ใช้ต่อได้โดยไม่เพิ่ม schema
- **ไม่เลือก:** ไม่สร้าง import writer หรือ API เฉพาะ Fixed-width, ไม่ trim/แปลง business values ใน reader และไม่ตัดคู่ surrogate กลางตัวอักษร
- **ข้อจำกัด:** cells ของ Fixed-width นับจำนวน slices ต่อ data record, blank lines นับ physical line แต่ไม่สร้าง Source; จำกัด field spans/record characters ตาม InputReadLimits เดิม; intake/Kafka/transform worker/archive และ Excel import ยังไม่อยู่ใน batch นี้
- **ห้าม:** ไม่เปิด production capability หรือปิด M2.7 ทั้งข้อจาก synthetic tests เมื่อยังไม่มี partner acceptance/Q9; ไม่เดา business bindings ของ partner ที่ยังไม่ส่งมา

### M-31 Outbox แบบ typed และ durable Inbox Receipt ก่อน commit offset
- **ตัดสิน (2026-11-17):** ตามแบบและ source batch ที่ผู้ใช้ยืนยัน คง outbox ของ M-29 ตาม Informs: ordered long ID, topic/key, IDs-only JSON, sent timestamp; เพิ่ม strict parse/canonical publish, blocked code และ header `mapping-event-id` เท่ากับ outbox ID เดิม; dispatcher ใช้ DB transaction/row locks และ idempotent producer/acks all
- **ทำไม:** DB เป็นเจ้าของ job state และต้องรับ publish ซ้ำเมื่อ broker รับแล้วแต่ mark sent ล้มเหลว; Inbox Receipt unique broker coordinates พร้อม immutable guards บันทึกผลรับและ Row Job wake-up ใน transaction เดียวก่อน Kafka offset commit
- **ไม่เลือก:** ไม่ย้ายกลับไป outbox job-kind/generation/lease ในร่าง M1.1 และไม่รัน compiler/evaluator ใน consumer รอบนี้; M3.3 ต้อง claim Pending job ที่ NextAt มีค่าและถึงเวลาแล้ว โดยมี lease/fencing ของ worker ต่อไป
- **ตัวอย่างข้อมูลสมมติ:** outbox ID `41` ส่ง UUID ของ Row Job A/Source B/File Job C/Config Version D → broker รับแต่ DB mark sent rollback → publish ซ้ำ header `41` ที่ offset ใหม่ → receipt ใหม่เป็น AlreadyQueued โดย NextAt/row version ของ A ไม่เปลี่ยน; อ่าน offset เดิมหลัง restart ไม่เพิ่ม receipt
- **ข้อจำกัด:** local topic `mapping.transform.requested`, group prefix `insurance-validation.mapping`, host `localhost:9094`, 3 partitions; company broker/topics/groups/owner/auth/TLS รอ Q7; default ปิด messaging และ script local เปิดเฉพาะเมื่อ caller ยังไม่ตั้งค่า ส่วน company ไม่เปิดให้เอง
- **เกณฑ์ปิด M3.1 (2026-11-17):** ตามข้อตกลงผู้ใช้ใช้ Kafka local ก่อน ปิดงานพัฒนา/ทดสอบ local เมื่อหลักฐาน transaction/offset/redelivery ผ่าน; dependency บริษัทแยกไว้ใน Q7 และเป็นเงื่อนไขก่อนเปิดใช้งาน environment บริษัท ผล local ไม่ใช่ผลรับตรวจ deployment บริษัท
- **ห้าม:** ไม่ส่ง arbitrary raw JSON ออก bus, ไม่เก็บ body/headers/exception text ใน receipt/log, ไม่ mark sent เมื่อ event ผิดรูปแบบ และไม่ถือว่า wake-up คือ transform สำเร็จ

### M-32 รับไฟล์ด้วย pinned occurrence และ archive สำเนา snapshot โดยคง input
- **ตัดสิน (2026-11-17):** ตามแบบ/source batch ที่ผู้ใช้ยืนยัน ทำ scan → stable fingerprint → File Job/version pin/path claim/import outbox → Kafka wake-up → snapshot/content claim → shared import → archive copy; worker อยู่ Infrastructure และเรียก Application ผ่าน MediatR ตาม D-77
- **ทำไม:** การรับหลาย scanner ต้อง serialize ใน DB; snapshot และ Source เป็นหลักฐานเดิม ส่วน path เดิมอาจถูก partner เขียนทับ การตรวจ hash แล้วค่อยย้าย path ไม่พิสูจน์ว่าไฟล์ที่จะย้ายยังเป็น occurrence เดิม
- **ไม่เลือก:** ไม่ย้าย/ลบ input partner อัตโนมัติ; ไม่ใช้ FileSystemWatcher เป็นตัวรับหลัก และไม่ใช้ชื่อไฟล์/config version เป็น content dedup key; เปลี่ยนแบบ archive/release path ในร่าง M1.1 เป็น snapshot-copy + retained occurrence pointer
- **กลไก:** work lease แยกจาก import lease ของ M-29 พร้อม DB clock/epoch fencing; accepted fingerprint/version/CreatedAt/ข้อมูลที่ seal แล้ว immutable; content claim `(config_id,hash)` คงที่ข้ามชื่อ/versionตาม M-25; import event มี GUID 3 ตัว ไม่มี raw fields และ receipt/audit/NextAt commit ก่อน Kafka offset
- **ตัวอย่างข้อมูลสมมติ:** F1 ของ config A รับ `a.csv` hash H1 → import/archive สำเนา H1 แล้วคง input และ path pointer; สแกน H1 ที่ path เดิมคืน AlreadyObserved; เปลี่ยนเป็น H2 หลัง Archived จึงรับ F2 pin active version ใหม่; เปลี่ยน input หลัง seal ของ F1 ให้ RecoveryRequired เก็บ archive H1 และ input ใหม่ไว้ทั้งคู่
- **ข้อจำกัด:** archive สำเร็จไม่ใช่ transform/delivery สำเร็จ; content claim ของงาน blocked ไม่ถูกล้าง; cleanup/retention และ permissions/durability ของ share บริษัทติดตาม Q7 ไม่แฝงคำสั่งลบต้นฉบับใน scanner
- **ห้าม:** ไม่แก้หลักฐานย้อนหลัง ไม่เริ่ม import จาก input path โดยตรง ไม่เก็บ raw payload/exception text ใน file-event receipt/log และไม่เปลี่ยน pinned version/CreatedAt ระหว่าง resume

### M-33 แปลงแถวด้วย fenced worker และ commit encrypted intents พร้อมผลแถว
- **ตัดสิน (2026-11-17):** ตาม source batch ที่ผู้ใช้ยืนยัน ทำ Row Job worker แยกจาก transform consumer ใช้ DB clock/owner/epoch/attempt/lease และ pinned Source/config/schema/profile; ใช้ compiler/evaluator ร่วมกับ preview และเติม system envelope จาก File Job ตาม D-80 โดย config override ไม่ได้
- **ทำไม:** ต้องมี durable final outputs ก่อนบอกว่า Transformed และต้องกู้คืนจาก crash/redelivery โดยไม่เพิ่ม Source/output; จึงทำ encrypted output storage ของ M4.1 เท่าที่จำเป็นพร้อม row result/diagnostics/delivery outbox ใน transaction เดียว แต่ HTTP client/attempts/retry budget ยังเป็น M4
- **กลไก:** generic 0..N (`{}`/outputPolicy many) หรือ pinned `{"outputPolicy":"oneDefault"}`; schema ผิด output เดียว Blocked ทั้งแถวไม่มี partial outputs; conversion แบบ nullable คง null/unparsed และ warning ตาม D-04; technical dependency/key failure ใช้ RetryableFailed/NextAt ไม่ถือเป็น delivery attempt และไม่ reset counter
- **ตัวอย่างข้อมูลสมมติ:** Source S1 ได้ keys `primary`/`secondary` ที่ผ่าน schema → encrypted Delivery Jobs D1/D2 + IDs-only outbox 2 รายการ + Transformed/OutputCount 2 พร้อมกัน; ถ้า secondary ผิด contract → Blocked/OutputCount 0 พร้อม safe diagnostic โดยไม่มี D1/D2; stale owner หลัง encrypt บันทึก intent ไม่ได้
- **การเข้ารหัส:** AES-256-GCM, nonce 12 bytes สุ่มต่อ payload, tag 16 bytes, SHA-256 final bytes, AAD ผูกทุก delivery identity/key reference/version; typed metadata columns แทน JSON; immutable encrypted payload และ unique `(file_job_id,source_row_id,output_key)` ใช้ส่ง bytes เดิมต่อใน M4
- **ไม่เลือก:** ไม่ compile ใน Kafka consumer ไม่ fallback plaintext/latest schema ไม่เรียก HTTP ระหว่าง row transaction และไม่เปลี่ยน encrypted intent หลัง commit; ไม่ใช้ชื่อ partner กำหนด cardinality ใน engine
- **ข้อจำกัด:** worker ปิดโดย default; local key สร้างครั้งเดียวเมื่อเปิดอย่างชัดเจน อยู่ ignored data/keys และ mount read-only; company key/Kafka/share ติดตาม Q7; Informs actual schema ที่ยังขาด fileReceivedAt ให้ ContractMissingFileReceivedAt/Blocked และรอ T2.6/Q4 ก่อนรับตรวจจริง success ของกลไกใช้ synthetic schema ที่ประกาศชัด; ไม่เปิด reprocess เปลี่ยน payload ของงานเก่าตาม M-22
- **หลักฐาน:** [แบบ/ไฟล์/การทดสอบ M3.3](m3-3-row-transformation-design.md); migration `20261117081518_AddRowTransformationOutputs`; สถานะรวมและผล gate ดู §1/§3 ไม่มี commit/push อัตโนมัติ

### M-34 ส่ง durable final bytes ผ่าน typed Informs client ครั้งเดียวต่อ invocation
- **ตัดสิน (2026-11-17):** ตาม source batch M4.1 ที่ผู้ใช้ยืนยัน เพิ่ม Application `IInformsClient`/typed outcomes/result และ Infrastructure typed HttpClient ใช้ POST `/api/pa/informs` กับ final payload bytes เดิมจาก M-33 ไม่ serialize request ใหม่หรือสร้าง idempotency key อีกชุด
- **ทำไม:** ต้องแยก transport result ที่ยืนยันแล้วออกจาก unknown outcome ก่อนทำ delivery runner; HTTP status เพียงอย่างเดียวไม่ยืนยัน inform ID/flags และ automatic retry/redirect อาจข้าม durable attempt budget ตาม M-08/M-20
- **กลไก:** strict bounded BaseResponse parsing ตาม D-61/controller จริง, known Created/Replayed/Replaced/ContractRejected/DuplicateRejected/Throttled/TransientFailure/UnknownOutcome/Blocked; timeout ครอบคลุม headers/body, caller cancellation propagate, Retry-After delta/date คืน retryNotBefore ให้ M4.2 ใช้ต่อ; มีแต่ fixed codes/IDs/flags/status ไม่มี body/messageText/exception text
- **ตัวอย่างข้อมูลสมมติ:** encrypted D1/hash H1 ถูก commit แล้ว → decrypt bytes B1 → POST B1 ได้ 201/informId I1 → caller ทดสอบโหลด D1 หลัง config ใหม่และส่ง B1 ซ้ำกับ stub ได้ 200/replayed/I1; request bytes/hash/key เดิม แต่ Delivery Job ยัง Pending เพราะ adapter ไม่ใช่ runner ที่บันทึก attempt/outcome
- **ไม่เลือก:** ไม่ทำ HTTP retry handler/automatic redirect/cookie reuse ไม่อ่าน latest config/Source เพื่อสร้าง payload ตอนส่ง และไม่ใช้ ProblemDetails template แทน BaseResponse จริง; 400 ไม่คัด field error message ที่อาจมี PII การ inspection/history แยก M4.3
- **ข้อจำกัด:** M4.1 ไม่เพิ่ม send API/hosted sender/consumer หรือ mark Delivered; M4.2 ต้อง reserve durable attempt/claim/global budget ก่อนเรียก client พร้อม fenced result/retry/concurrency/lease recovery; capability คง false จน runner พร้อม; actual Informs acceptance/deploy T2.6 ตาม Q4 และ company URL/auth/TLS/key ตาม Q7 ยังเปิด
- **หลักฐาน:** [แบบ/source batch/ผลตรวจ M4.1](m4-1-informs-http-client-design.md); ทดสอบ local HTTP loopback/PostgreSQL ด้วยข้อมูลสังเคราะห์และ regression old schema โดยไม่แก้ Informs/Web; source ยังไม่ commit/push

### M-35 จอง delivery attempt ก่อน HTTP และใช้ DB budget ร่วมทุก worker
- **ตัดสิน (2026-11-17):** ตาม source batch M4.2 ที่ผู้ใช้ยืนยัน เพิ่ม durable delivery runner แบบ DB polling/NextAt, Delivery Attempts และ owner/epoch/lease/heartbeat; ใช้ typed HTTP client M-34 กับ immutable encrypted bytes ของ M-33 ไม่เพิ่ม retry handler/DeliveryConsumer หรือเปลี่ยน payload/key
- **ทำไม:** ต้องบันทึก reservation/attempt number ก่อนส่งเพื่อให้ crash/restart/หลาย pod ใช้เพดาน 6 ร่วมกันตาม M-08/M-20; Delivery Job มี NextAt จาก row transaction แล้ว จึงกู้คืนจาก DB โดยไม่ต้องให้ broker republish และไม่ให้ event ซ้ำ reset backoff
- **กลไก:** control→job→attempt lock order/DB clock; DeliveryPaused และ persisted MaxParallel default 1 คุมทุก process ไม่คูณเพดานตามจำนวน pod; reserve/insert attempt atomic ก่อน Reserved→Dispatching guard/HTTP; renew/complete ต้องตรง owner/epoch/attempt/lease และรอ heartbeat จบก่อนใช้ DbContext บันทึกผล
- **ผล:** 201/replay/replaced → Delivered พร้อม IDs/flags; 400/409 → Rejected; 429/5xx/unknown → RetryableFailed เมื่อยังเหลือ reservation หรือ Failed เมื่อครบ 6; profile/key/config/protocol failure → Blocked; Retry-After เป็น lower bound ของ durable next-at และไม่ลด counter เพื่อชดเชย reservation ที่ไม่ทราบผล
- **ตัวอย่างข้อมูลสมมติ:** D1/A1 reserve แล้ว HTTP stub รับแต่ DB complete ล้มเหลว → A1 OutcomeUnknown/D1 รอ backoff; restart reserve A2 ส่ง bytes/hash/key เดิมได้ replay I1 → Delivered/attempts_reserved 2; A6 crash แล้วหมด leaseให้ Failed/OutcomeUnknown ไม่ส่ง A7 เพื่อ probe
- **ไม่เลือก:** ไม่คุม concurrency ด้วย semaphore ต่อ pod อย่างเดียว ไม่ reset attempts/key/NextAt จาก redelivery และไม่เปิด manual-send/retry API ข้าม ledger; API controls/inspection แยก M4.3
- **ข้อจำกัด:** default sender ปิด เปิดด้วย Delivery.Enabled อย่างชัดเจน; lease/grace ไม่พิสูจน์ว่า server หยุดรับ HTTP ที่อาจส่งไปแล้ว จึงใช้ D-44/OutcomeUnknown และเพดานเดิม; actual Informs acceptance/deploy T2.6 ตาม Q4/M4.4 และ company dependency Q7 ยังแยกติดตาม ไม่มีบริษัท key generation/DB write/HTTP ในงานนี้
- **หลักฐาน:** [แบบ/source batch/tests M4.2](m4-2-durable-delivery-design.md), migration `20261117095253_AddDeliveryRuntime`; ไม่มี commit/push อัตโนมัติ และไม่แก้ Informs/Web/demo

### M-36 อ่านผลจาก snapshot และควบคุม delivery ด้วย audited commands
- **ตัดสิน (2026-11-18):** ตาม contract subset/source batch M4.3 ที่ผู้ใช้ยืนยัน เปิด 12 routes ใน `/api/mapping` ผ่าน MediatR/BaseResponse เดิม; logs/summary แยก rows, unique outputs, reservations และ dispatched attempts ตาม M-10; Source/Payload inspections ตาม M-18/M-19 เป็น read-only ไม่สร้าง jobs/audit/HTTP หรือ regenerate payload
- **ทำไม:** ทีม Mapping/Dev/Ops ต้องกระทบยอดงาน partial/filtered/blocked/หลาย outputs และตรวจใบที่ Rejected ได้ โดย counts ใน file detail ใช้ read-only repeatable-read snapshot เดียวพร้อมเวลา DB และ duplicate occurrence แสดง canonical ID แยกจากยอดของ occurrence
- **กลไก:** manual retry ใช้ control→job→audit lock/DB clock ตรวจ body hash และ replay ก่อน rowVersion; เฉพาะ RetryableFailed ที่ไม่มี lease และยังไม่ครบ6, NextAt=max(DB now,ค่าเดิม), เพิ่ม rowVersion/audit ใน transaction เดียว ไม่จอง attempt/ส่ง HTTP/reset bytes-key; pause/resume เปลี่ยน flag/rowVersion พร้อม audit ใช้ fixed actor `mapping-api` และไม่ยกเลิกงานที่เริ่มแล้ว/ไม่เปิด worker ที่ disabled
- **Diagnostics:** attempts ใหม่เก็บ `safe_diagnostics_json` JSONB array default `[]`, สูงสุด100/JSON text64KiB และ path512; Informs 400 คัดเฉพาะ path ที่ normalize แล้วอยู่ใน pinned schema catalog ไม่มี messageText/raw value/unknown names; malformed/oversized/stalled detail ยังคง Rejected และ completed history immutable
- **ตัวอย่างข้อมูลสมมติ:** Source3แถว = Filtered1/Blocked1/Transformed1 → outputs2 = Delivered1/Rejected1; reservations3 = Completed2/OutcomeUnknown1 และ dispatched3; retry outputที่ยัง RetryableFailed/NextAtอีก10นาทีคงเวลาเดิมและ attempt count เดิมแม้กดซ้ำ
- **ไม่เลือก:** ไม่รวม attempts เป็นยอด outputs ไม่อ่าน definition/config ล่าสุดเพื่อเปิด Source/payload ไม่ใช้ GET ทำ repair ไม่ส่ง HTTP จาก retry API และไม่เปิด reprocess/business resend/เพิ่มเพดาน/max-parallel editor/Web ในรอบนี้
- **หลักฐาน/ข้อจำกัด:** check.ps1 ผ่าน unit38/38 integration162/162 และ EF model ด้วย temporary sequential-collection RunSettings ตาม M4.2; [แบบ/contract/รายการไฟล์/ผลตรวจ M4.3](m4-3-delivery-operations-api-design.md); actual Informs Q4/M4.4, dependencies Q7 และ M1.2/Q8/Web review ยังแยกติดตาม; ไม่ commit/push อัตโนมัติ

### M-37 ใช้ Informs images ที่ pin revision สำหรับ local end-to-end acceptance
- **ตัดสิน (2026-11-18):** ตามชุด M4.4/public seams ที่ผู้ใช้ยืนยัน ใช้ Informs API/migrator images จาก commitเดียวกัน ตรวจ revision label ก่อนเริ่ม และรันกับ isolated PostgreSQL/Kafka; Mapping ใช้ hosted intake/import/transform/delivery และอ่านผลผ่าน API ของ Mapping/Informs
- **ทำไม:** M4.1–M4.3 พิสูจน์กลไกกับ HTTP stub แล้ว แต่ต้องตรวจ actual intake/response/schema/D-80 และ replay/replaced/409/429 กับบริการเจ้าของจริง; pin dependency ทำให้รันซ้ำได้แม้ remote เปลี่ยนระหว่างงาน
- **ตัวอย่างสมมติ:** Source1/Delivery1 ส่งถึง Informs commitแล้ว responseหาย → attempt1 OutcomeUnknown; restartใช้bytes/hash/keyเดิม → actual200/replayed/informIdเดิม, outputs1/attempts2/Informs inform1/snapshot1; ไฟล์ใหม่แก้150→180ได้replacedและ late old keyไม่ย้อนข้อมูลกลับ
- **กลไก:** helper streaming request ถือ permit ของ limiterจริงและส่ง JSON whitespace ระหว่างรอ; probeยืนยัน429/Retry-Afterก่อนให้Mappingส่ง; proxy injectเฉพาะ503ก่อนforward/drop responseหลังactualcommit ไม่ปลอมsuccess; assertionsแยกunique outputs/reservations/dispatchesตาม M-36 และไม่อ่าน non_motorโดยตรง
- **Gate:** `scripts/test-e2e.ps1 -InformsSource <checkout> -Gate` ตั้งimage/revision/RunSettingsชั่วคราวแล้วเรียกcheck.ps1 และตรวจTRXว่าexternalทุกข้อถูกexecute/Passedทั้งหมด ไม่มีskip; การรันปกติที่ไม่มีimagesจะskipเฉพาะexternaltestsพร้อมเหตุผลและใช้ปิดM4.4ไม่ได้
- **ผลต่อ capability:** หลังactualacceptanceของ3formats/ApiContractผ่าน ให้ available=true เฉพาะfeatureเหล่านี้; เป็นความสามารถของengine ไม่เปิดworkerอัตโนมัติและไม่รับรองpartnerbindings/UAT/company deployment
- **ไม่เลือก:** ไม่อ้าง assembly/project ข้าม service ไม่แก้checkout/schemaของInforms ไม่ใช้synthetic D-80 schemaเป็นsuccess ไม่ปรับstate/อ่านDB Informsโดยตรงเพื่อยืนยันผล และไม่ขยายเป็นProducts/Client API validationครบทุกstage
- **หลักฐาน/ข้อจำกัด:** check.ps1ผ่านunit38/38 integration171/171 EFmodel และexternal9/9ไม่มีskip/abortผ่านscript -Gate; [แบบและผลตรวจ M4.4](m4-4-informs-end-to-end-design.md); dependencyเป้าหมาย `f1eca8d` ตามชุดที่ยืนยัน ไม่อ้างว่าtested remoteใหม่ `6cb5664`; Q7/Q5/Q9/M1.2/Q8/Web และ Kubernetesยังแยกติดตาม; ยังไม่commit/push

### M-38 แสดงชื่อตารางต้นทางจริงแบบอ่านอย่างเดียว
- **ตัดสิน (2026-11-23):** ตามคำขอและการยืนยันของผู้ใช้ ให้ Source definition GET API ส่ง `physicalTableName` ที่เก็บใน DB และแสดงพร้อม schema `mapping` ใน wizard ขั้นที่ 1/5, Config detail และ Source fields list/detail
- **ทำไม:** ผู้ใช้ต้องตรวจได้ว่า field และไฟล์ของ config นี้บันทึกลง physical Source Table ใดตาม M-15/M-16
- **ตัวอย่างข้อมูลสมมติ:** API คืน `{"id":"<source-definition-id>","configId":"<config-id>","physicalTableName":"src_example"}` → หน้าเว็บแสดง `mapping.src_example`; ก่อนสร้างแบบร่างแสดง “ยังไม่ได้สร้างตารางต้นทาง”
- **ไม่เลือก:** เดาชื่อตารางจาก Key/ชื่อ config/ID หรือให้ผู้ใช้เปิด DB เพื่อตรวจชื่อทุกครั้ง
- **ห้าม:** ใช้ชื่อที่แสดงเป็น input SQL/DDL จาก browser หรือเปลี่ยนชื่อตารางผ่านหน้าจอนี้; การรับไฟล์/เพิ่ม field ใช้ตารางตาม M-15/M-17 เดิม

### M-39 กำหนดชื่อตารางต้นทางเองเมื่อสร้าง Source definition
- **ตัดสิน (2026-11-23):** ผู้ใช้ขอและยืนยันเพิ่มช่อง “ชื่อตารางต้นทาง” ในขั้นที่ 1; รับชื่อ table ที่ไม่มี schema prefix แล้วสร้างใต้ `mapping`; หลังสร้างสำเร็จเปลี่ยนชื่อไม่ได้ การแสดงชื่อจริงตาม M-38 ยังใช้ต่อ
- **ทำไม:** ผู้ใช้ต้องกำหนดได้ตั้งแต่ตั้งค่า Mapping ว่าข้อมูลดิบจะลงตารางชื่อใด ไม่ใช่แค่ดูชื่อ UUID ที่ระบบตั้งให้
- **ตัวอย่างข้อมูลสมมติ:** ชื่อ Mapping `partner-d`, Input folder `partner-d/inbox`, ชื่อตาราง `partner_d_raw`, Key `reference`/`amount` → สร้าง `mapping.partner_d_raw`; config นี้รับไฟล์เพิ่มและเพิ่ม fields ตาม M-17 ลงตารางเดิม
- **ขอบเขต:** ชื่อ `[a-z][a-z0-9_]{0,47}`; ตรวจซ้ำกับ metadata และ relations เดิมใน `mapping` ด้วย transaction/lock และคืน `TableNameConflict`; identity/DDL/version/audit atomic และ request hash รวมชื่อ; runtime resolve จาก catalog ของ Source definition ที่ pin เท่านั้น
- **ความเข้ากันได้:** ตารางเดิมไม่ rename; client API เดิมที่ไม่ส่ง `physicalTableName` ยังใช้ชื่ออัตโนมัติเดิม ชื่อเป็นข้อมูลที่ผ่าน validation สำหรับ create เท่านั้น ไม่รับ arbitrary SQL/DDL หรือแชร์ตารางที่มีอยู่ข้าม configs
- **ไม่เลือก:** rename ตารางที่มี Source แล้ว, ต่อชื่อตารางจาก Key/ชื่อ Mapping อัตโนมัติ หรือให้ browser ระบุชื่อตารางตอนอ่าน/นำเข้ารายแถว
- **แทนข้อจำกัดเดิม:** เพิ่มการรับ identifier ที่ตรวจแล้วตอนสร้างใหม่จากข้อจำกัดการแสดงชื่ออย่างเดียวใน M-38; ไม่เปิดให้เลือก SQL หรือ rename ใน Source inspection

### M-40 ไม่ให้เลือกชนิดข้อมูลต้นทางใน wizard
- **ตัดสิน (2026-11-23):** ผู้ใช้ยืนยันให้เอาคอลัมน์ “ชนิด” ออกจากขั้นที่ 1 เพื่อไม่ให้เข้าใจว่าเป็นชนิดของคอลัมน์ใน DB; แสดงข้อความว่าข้อมูลต้นทางเก็บเป็นข้อความ และกำหนดการแปลงในขั้นที่ 4
- **ทำไม:** physical raw columns เป็น `text NULL` ตามแบบเดิม การเลือก hint ในหน้า Source fields ทำให้สับสนว่ากำลังเปลี่ยนชนิด DB
- **ตัวอย่างข้อมูลสมมติ:** ไฟล์มี `amount="00180"` → Source เก็บ `"00180"` และ definition ใหม่มี `logicalTypeHint: null`; หาก contract ต้องการตัวเลข ผู้ใช้กำหนด `convert` ในขั้นที่ 4 → JSON `amount: 180`
- **ขอบเขต:** wizard ไม่คัดลอก `logicalTypeHint` จาก config ต้นฉบับ; auto-map ตามชื่อให้ `trim(source.<key>)` โดยไม่สร้าง convert จาก hint; ปรับ header/row grid และ tour ให้ตรงกัน API/ข้อมูล/config versions เก่ายังคงรองรับ metadata เดิม
- **ไม่เลือก:** ซ่อนช่องเลือกแต่ยังคัดลอกหรือใช้ hint ในการสร้างโปรแกรมโดยที่ผู้ใช้ไม่เห็น
- **ผลต่อเอกสารกลาง:** ขอ Dev 1 ปรับข้อความ D-84 ตาม Q11 โดยเก็บการตัดสินใจเดิมเป็นประวัติ; รอบนี้ไม่แก้ไฟล์ของ Dev 1

### M-41 ใช้ Key เป็นชื่ออ้างอิงของ field ใน wizard
- **ตัดสิน (2026-11-23):** ผู้ใช้ยืนยันให้เอาช่อง “ชื่อที่แสดง” ออกจากตารางคอลัมน์ในไฟล์ขั้นที่ 1; Key เป็นทั้งชื่อ field ที่ระบบใช้และชื่ออ้างอิงบนหน้าจอสำหรับรายการใหม่
- **ทำไม:** ลดช่องกรอกที่ใช้เพื่อแสดงผลอย่างเดียวและไม่กระทบการอ่าน/แปลง/ส่งข้อมูล
- **ตัวอย่างข้อมูลสมมติ:** Header `เลขอ้างอิง` → Key `reference` → definition ใหม่ส่ง `displayName: null` และหน้าอ่านข้อมูลใช้ชื่อ `reference`; ค่าดิบ `SYNTHETIC-001` และ target path ใน contract เป็นไปตาม mapping เดิม
- **ขอบเขต:** ไม่เก็บหรือคัดลอก displayName ใน state ของ wizard และไม่เติมจาก header ของไฟล์ตัวอย่าง; ปรับ header/rows/tour ให้ตรงกัน API และ metadata ของ config versions เก่ายังรองรับ displayName เดิม
- **ไม่เลือก:** ซ่อนช่องกรอกแต่ยังบันทึกชื่อจากไฟล์หรือ config ต้นฉบับโดยที่ผู้ใช้ไม่เห็น

### M-42 ตั้งเงื่อนไขเลือกแถวและรายการด้วยตัวเลือกแทน JSON เป็นค่าเริ่มต้น
- **ตัดสิน (2026-11-25):** ผู้ใช้ยืนยัน [แบบ Map required](map-required-ui-design.md) และไฟล์ Web 6 ไฟล์ ให้ใช้ rule rows เลือกคอลัมน์/วิธีตรวจ/ค่า พร้อมกลุ่มครบทุกข้อ (`and`)/อย่างน้อยหนึ่งข้อ (`or`) ซ้อนกันได้ ตามภาพ Rule Editor ที่ผู้ใช้แนบ; ใช้ทั้ง `sourceWhen` และ `emitWhen`
- **ทำไม:** คนที่ไม่ได้เป็น dev ต้องตั้งเงื่อนไขทั่วไปได้โดยไม่รู้โครงสร้าง JSON และต้องเห็นว่าเงื่อนไขใดอยู่ในกลุ่มใด
- **ตัวอย่างข้อมูลสมมติ:** `reference="SYNTHETIC-001"` + ตัวเลือก `reference ไม่ว่าง` → `{ "kind": "notBlank", "input": { "kind": "source", "field": "reference" } }`; กลุ่มครบทุกข้อสร้าง `and.operands` และใช้ใน program รูปเดิม
- **ขอบเขต:** แสดง operators ตาม capabilities; เปรียบเทียบตัวเลขด้วย `convert` เป็น `decimal` อย่างชัดเจนตาม M-40; ตรวจเงื่อนไขที่ยังไม่ครบ/คอลัมน์ที่เปลี่ยนในขั้นที่ 1; รูปแบบที่ฟอร์มสร้างกลับไม่ได้ครบถ้วนให้คงขั้นสูง JSON; ปรับคำบนหน้าและ tour แต่คง anchors เดิม
- **ไม่เลือก:** บังคับทุกคนเขียน JSON หรือแปลงโปรแกรมซับซ้อนเป็นเงื่อนไขง่ายจนความหมายเปลี่ยน
- **การย้อนกลับ:** ตามคำถามผู้ใช้ก่อนยืนยัน จำกัด diff เฉพาะ UI ชุดนี้บน Web `73f89ad`, ไม่เพิ่ม package/เปลี่ยน API/DB; ย้อนเป็น UI เดิมได้โดยไม่เปลี่ยนรูปแบบข้อมูลและไม่ทับงานอื่น

### M-43 ตารางเลือกปลายทางและ Preview sticky ในหน้า Mapping
- **ตัดสิน (2026-11-25):** ผู้ใช้ยืนยัน [แบบตารางปลายทาง](destination-table-ui-design.md) และเพิ่มคำขอปุ่ม reload ให้ตารางโหลดปลายทางที่เพิ่มใหม่ได้; ใช้ตารางแทน dropdown ค้นหา/เลือก/ดูรายละเอียด/เปิดหน้าแก้ไขโปรไฟล์ปลายทางเดิมตาม D-85 และจัดฝั่งตั้งค่า/Preview 60/40 แบบ sticky
- **ทำไม:** ต้องเห็นรายละเอียดปลายทางก่อนเลือก เพิ่ม/แก้ไขปลายทางในแท็บใหม่แล้วโหลดรายการกลับมาได้ และเห็น Preview ขณะเลื่อนตั้งค่า mapping
- **ตัวอย่างข้อมูลสมมติ:** เลือก `sample-inform` รุ่น 1 อยู่ → อีกแท็บเพิ่ม `sample-archive` และแก้ `sample-inform` เป็นรุ่น 2 → โหลดรายการใหม่เห็นทั้งสองรายการ แต่ schema/ค่าที่จับคู่ยังเป็นรุ่น 1 จนกด `ใช้รุ่นล่าสุด`; จากนั้นคงค่าของ path ที่ยังตรงกันและเพิ่มช่องใหม่ที่ยังไม่ได้จับคู่
- **ขอบเขต:** refresh catalog/profiles โดยคงโปรแกรม; เลือก schema สำเร็จจึงรีเซ็ต Preview และผูก profile ใหม่; เงื่อนไข/ข้อความตรวจ/การส่งจริงใช้กลไกเดิม; ปลายทางอื่นนอกจาก ApiContract ยังไม่เปิดใช้ตาม M-21
- **ไม่เลือก:** เปลี่ยน schema/ทิ้งค่าที่จับคู่ทันทีเมื่อกด reload, เปิดหน้าแก้ไขทับร่างในแท็บเดียวกัน หรือแก้ behavior sticky ของเมนูอื่น

### M-44 จัดการปลายทางใน modal โดยยืนยันก่อนเปลี่ยนรุ่นที่ใช้อยู่
- **ตัดสิน (2026-11-25):** ผู้ใช้ยืนยัน [แบบ modal ปลายทาง](destination-modal-ui-design.md) ให้ส่วนเลือก/จัดการปลายทางของ M-43 อยู่ใน Dialog; หน้าหลักแสดงชื่อ/รุ่น/ช่องทาง/path แบบย่อพร้อมปุ่มจัดการ และคง mapping/Preview sticky 60/40
- **ทำไม:** ลดพื้นที่จัดการ catalog บนหน้าที่ใช้จับคู่ข้อมูล และให้ลองเลือก/ค้นหา/โหลดรายการใหม่ได้ก่อนยืนยันเปลี่ยนปลายทาง
- **ตัวอย่างข้อมูลสมมติ:** ใช้ `sample-inform` รุ่น 2 → เปิด modal เลือก `sample-archive` → ยกเลิก → contract/profile/ค่าที่จับคู่/Preview ยังเป็นชุดเดิม; reload แล้วมี `sample-inform` รุ่น 3 ยังไม่เปลี่ยนจนกดยืนยัน
- **ขอบเขต:** pending selection แยกจาก applied snapshot, cache profile เดิมเมื่อ refresh รายการ, schema load ล้มเหลวยังไม่เปลี่ยนของเดิม, loading กันปิด/กดซ้ำ และคืน focus เมื่อปิด; สร้าง/แก้ไขปลายทางเปิดหน้าเดิมในแท็บใหม่ตาม D-85 และ reload ผ่านปุ่มใน modal
- **ไม่เลือก:** ให้การคลิกเลือกแถวหรือ reload เปลี่ยน contract/mapping ทันที หรือปล่อยให้ catalog ใหม่ทำให้ applied profile เดิมหายก่อนผู้ใช้ตัดสินใจ

### M-45 จับคู่จากต้นทางและแก้ mapping/array/เงื่อนไขใน draft modal เดียว
- **ตัดสิน (2026-11-25):** ผู้ใช้ยืนยัน [แบบจับคู่จากต้นทาง](source-first-mapping-ui-design.md) และให้ตัดตัวอย่างออก; ตารางหลักเริ่มจาก Source fields ส่วนการเลือกปลายทาง/แปลงค่า/array/เงื่อนไขอยู่ใน modal เดียว โดยยืนยัน draft ทั้งชุดก่อนเปลี่ยนค่าที่ใช้จริง
- **ทำไม:** ผู้ใช้เริ่มจากข้อมูลในไฟล์ แล้วเลือกว่าจะส่งไปช่องใดได้สะดวกขึ้น ลดฟอร์ม array และเงื่อนไขบนหน้าหลัก โดยไม่จำกัด mapping ให้เหลือหนึ่งต่อหนึ่ง
- **ตัวอย่างสมมติ:** `first_name` + `last_name` ร่วม expression `concat` ไป `inform.fullName` → ตารางแสดง target นี้ในสอง source rows แต่ serialize assignment ครั้งเดียว; `reference` จับคู่ได้ทั้ง `inform.reference` และ `inform.externalId`; เพิ่ม array item 2/emitWhen แล้วกดยกเลิกไม่เปลี่ยนของเดิม
- **ขอบเขต:** source projection อ่าน nested expressions/case predicates แต่ไม่อ่าน literal data; มีทางจัดการค่าอื่นและ source ที่ถูกลบ; ใช้ schema/Transformation Program เดิม, required targets ยังเป็น gate ของการตรวจทาน/บันทึก, array ว่างไม่ส่งตามกลไกเดิม และคง Preview sticky 60/40
- **ไม่เลือก:** กลับทิศ program/เปลี่ยน API, duplicate assignments ตามแต่ละ source ที่ expression อ่าน, ซ่อนค่าคงที่/metadata หรือแก้ actual state ทันทีที่เปลี่ยนค่าใน modal

### M-46 แสดงการจับคู่ของต้นทางเป็นการ์ดและขยายเงื่อนไขด้วย checkbox
- **ตัดสิน (2026-11-25):** ผู้ใช้ยืนยันแบบภาพและรายการ source รอบนี้; หน้าต่างของ source ไม่มีแท็บเดิม แต่ละการ์ดเลือกปลายทางและใช้ checkbox เปิด editor ของเงื่อนไขการจับคู่/array Index/field/เงื่อนไขส่งได้ เพิ่มหลายปลายทางผ่านปุ่มเส้นประ
- **ทำไม:** แสดงการตั้งค่าที่เกี่ยวกับปลายทางนั้นอยู่ด้วยกัน และให้การจับคู่ตรงมีฟอร์มสั้นตามภาพผู้ใช้
- **ตัวอย่างสมมติ:** `trn_no` → `inform.trn_no`; array `inform.driver[]` Index 1 / field `inform.driver[1].trn_no` เก็บ path ภายใน `inform.driver[0].trn_no`; เงื่อนไขส่งใช้ `emitWhen` ของรายการเดิม ไม่เพิ่ม contract field ตามภาพ
- **ขอบเขต:** ใช้ ValueExpressionEditor/PredicateEditor/Transformation Program เดิม; การยุบส่วนพิเศษเก็บค่าที่ตั้งไว้ เพิ่มรายการใหม่คง mapping ก่อนหน้า และ target ที่ถูกใช้อยู่เปิดค่าเดิมให้ตรวจ; ทางเข้าจัดการทั้งชุด/ค่าอื่นยังใช้ได้
- **ไม่เลือก:** เปลี่ยน API/contract หรือทำให้การยุบการ์ดล้าง expression/เงื่อนไขโดยเงียบ; actual state เปลี่ยนเมื่อยืนยัน draft ทั้งชุดเท่านั้นตาม M-45

### M-47 รวมรายการของ array เดียวกันในหนึ่งการ์ด
- **ตัดสิน (2026-11-25):** ผู้ใช้ยืนยันให้ `เพิ่มรายการ` ของ array เดียวกันอยู่ภายในการ์ดเดิม และแยกการ์ดเมื่อเป็นคนละ array; ใช้ concrete group path เพื่อแยก array ลูกที่อยู่คนละ parent item
- **ทำไม:** ผู้ใช้เห็นชื่อกลุ่มครั้งเดียวและแยก Index/field/เงื่อนไขส่งของแต่ละรายการภายในกลุ่มได้ชัดเจนตามภาพที่ขอ
- **ตัวอย่างสมมติ:** source `no` → `inform.beneficiaries[1].seq` และ `[2].seq` อยู่ card `inform.beneficiaries[]` เดียว; `inform.drivers[1].seq` อีก card; ภายในยังเก็บ zero-based paths เดิม ไม่เพิ่มหรือแก้ contract
- **ขอบเขต:** จัดกลุ่มเฉพาะ view และคง assignments/expressions/emitWhen เดิม; แสดงเงื่อนไขครั้งเดียวต่อ index แม้จับคู่หลาย field, เลือก array ซ้ำรวม card เดิม, ย้ายกลุ่มและลบเฉพาะ mappings ของ source ที่แก้โดยไม่ล้างข้อมูล source อื่น; scalar ยังแยก card และ array ไม่มีส่วนแปลงค่าใต้กรอบตามคำยืนยันก่อนหน้า
- **ไม่เลือก:** สร้าง card ใหม่ทุกครั้งที่เพิ่ม index, รวม array ลูกคนละ parent หรือเปลี่ยน Transformation Program/API เพื่อรองรับรูปแบบหน้าจอ; ยืนยัน/ยกเลิกทั้งชุดตาม M-45 และคง dev server ให้ผู้ใช้

## 5. คำถาม / ข้อเสนอ (Dev 1 ↔ Dev 2)

- [ ] **Q11** (Dev 2, 11-23) ผู้ใช้ยืนยันถอดช่อง “ชนิด” จาก Source fields ใน wizard ตาม M-40 เพราะ raw DB columns เป็น `text NULL`; ขอ Dev 1 ปรับ D-84 ที่ยังระบุช่องชนิดและเสนอ convert จาก hint ให้ตรงพฤติกรรมล่าสุด: ข้อมูลต้นทางเป็นข้อความ, config ใหม่ไม่มี hint และกำหนด convert ในขั้นที่ 4 แทน; API/config เก่ายังรองรับ metadata เดิม

> ทั้งคู่เพิ่มได้ · เลข `Q1`, `Q2`, … ต่อกันไป · ตอบใต้ข้อนั้น · ตกลงแล้วเขียนผลสั้น ๆ + ย้ายไปที่ที่ถูก (`M-xx` ใน §4 หรือ Dev 1 ลง `rules.md` เป็น `D-xx`) แล้วติ๊ก `[x]`
>
> ```markdown
> - [ ] **Q1** (Dev 2, MM-DD) <คำถาม / ข้อเสนอ>
>   - Dev 1 (MM-DD): <คำตอบ>
>   - ผล: <ตกลงอะไร> → M-03 / D-xx
> ```

- [ ] **Q1** (Dev 1, 11-13) คำถาม 9 ข้อสำหรับ kickoff อยู่ใน [rules.md](rules.md) §6 — ตกลงวันที่ 11-16 แล้ว Dev 1 จะลงผลใน `rules.md` ส่วนเรื่องภายใน mapping Dev 2 ลงเป็น `M-xx` ใน §4
  - Dev 2 (11-13): [แผน API §2–§7](api-implementation-plan.md) เสนอ direct nested `ApiContract` + Transformation Program ส่วนที่ MTLS ใช้, versioned constants/defaults, และ hosted workers/outbox ตาม Informs; ทั้งหมดยังเป็นข้อเสนอสำหรับตอบ 9 ข้อ ไม่ใช่ผล kickoff
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 1: เลือก **สร้าง JSON `pa-inform/1` โดยตรงหลังเก็บ immutable Source แล้วส่ง Informs ผ่าน HTTP**; ไม่ใช้ Normalized Table แบบแบนเป็นขั้นกลาง คำตอบนี้ไม่เปลี่ยน contract/เส้นแบ่งงานตาม D-02/D-03/D-07 และไม่ถือว่าตอบเรื่องโครง Source storage ใน Q2 แล้ว; ข้อ 2–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 2: **เอา Transformation Program มาทั้งหมด แต่เน้นให้ MTLS ใช้ได้ก่อน** → M-03; M2.2 ทำ MTLS ก่อน แล้ว M2.5 ย้ายส่วนที่เหลือ ข้อ 3–9 ยังรอคำตอบ; ความสามารถที่ผูก legacy ยังต้องตกลง dependency ไม่ถือว่าเปิด partner/destination อื่นแล้ว
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 3: **ไฟล์ MTLS `.xls` BIFF8 ตามตัวอย่างก่อน**; อ่าน sheet แรก/header 19 columns/cp874 ตาม `mtls-file.md` โดยไม่บังคับ `.xlsx` ในช่วงแรก; ข้อ 4–9 ยังรอคำตอบ และ Q4/Q7 เรื่องลำดับแถว/fixture ยังไม่ปิด
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 4: เลือก **เก็บ default ผู้รับประโยชน์ใน versioned config ของ MTLS ผ่าน Transformation Program** → M-04; ข้อ 5–9 และ Q5 เรื่อง field สัดส่วนยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 5: เลือก **เก็บค่าคงที่ MTLS และกฎ `P → NEW` / `C → CANCELLED` ใน versioned config ผ่าน `literal` / `case`** → M-05; ข้อ 6–9 ยังรอคำตอบ ไม่ถือว่าตอบ fallback ของค่าอื่นใน Q5
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 6.1: เลือก **`sourceRowNo` เป็นลำดับแถวข้อมูลที่อ่านได้ เริ่ม 1 ไม่รวม header/แถวว่าง; retry ใช้เลขเดิม** → M-06; ค่า `nSeq` และ Physical Line Number เก็บแยก ส่วนที่เหลือของข้อ 6 และข้อ 7–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 6.2: เลือก **เริ่มส่ง HTTP ไป Informs พร้อมกันสูงสุด 1 request และปรับผ่าน config ภายหลัง** → M-07; retry/ส่วนที่เหลือของข้อ 6 และข้อ 7–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 6.3: เลือก **6 attempts รวมครั้งแรก** (ครั้งแรก 1 + retry สูงสุด 5) → M-08; retry delay/timeout, กติกา manual retry หลังครบเพดาน และข้อ 7–9 ยังต้องลงรายละเอียด
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 6.4: เลือก **เวลาที่ Mapping รับไฟล์และสร้าง File Job ครั้งแรก แล้วเก็บคงเดิมเมื่อ resume/retry** → M-09; วิธีส่งเวลานี้ให้ Informs ยังรอ Dev 1 ใน Q4 ส่วนข้อ 7–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 7.1: เลือก **ชุดข้อมูล delivery log ที่เสนอ** → M-10; ชื่อไฟล์/แถว/config version, ยอดทั้งหมด/สำเร็จ/รอ/ไม่สำเร็จ, สถานะ/HTTP status/error code/path, attempts/เวลาส่ง, `informId`/`replayed`/`replaced`; รายการปกติไม่แสดง raw Source/payload ส่วนผู้ใช้งานในข้อ 7.2 และข้อ 8–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 7.2: เลือก **ทีมดูแล Mapping / Dev / Ops** เป็นกลุ่มผู้ใช้งาน delivery log ช่วง phase 1 → M-11; ใช้ fixed user ตาม `web.md` โดยไม่เพิ่มระบบสิทธิ์ ข้อ 8–9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 8: เลือก **Config + ผลการส่ง + retry ตามสถานะ** → M-12; สร้าง version/validate/preview/activate/disable, ดูไฟล์/แถว/attempts และ retry ตามสถานะ; layout/interaction ยังรอ Web design และข้อ 9 ยังรอคำตอบ
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 9.1: เลือก **Kafka + transactional outbox** สำหรับงานภายใน Mapping ตาม pattern Informs → M-13; Mapping → Informs ยังคง HTTP; local broker/topics/groups/owner ยังรอข้อ 9.2/Q7
  - คำตอบผู้ใช้ (11-13), kickoff ข้อ 9.2: เลือก **Kafka local ผ่าน Docker ก่อน** ระหว่างรอ broker/topics บริษัท → M-14; แนวทางหลักทั้ง 9 ข้อมีคำตอบผู้ใช้แล้ว แต่รายละเอียด infrastructure ใน Q7, ข้อตกลง D-xx ที่เป็นหน้าที่ Dev 1 และประเด็น Q2–Q8 ยังไม่ถือว่าปิด
- [ ] **Q2** (Dev 2, 11-13) ขอ review feature scope จาก [feature matrix](api-implementation-plan.md#2-ขอบเขตและการเทียบ-feature): เสนอทำ MTLS Excel → Informs API ก่อน; เสนอเลื่อน LegacyTable/JsonFile/DatabaseTable, Delimited/Fixed-width, dynamic Table admin, generic profile/contract admin และ advanced rollout ที่ไม่จำเป็นต่อ MTLS แต่ยังเก็บ backlog ครบ · เสนอ Source Table แบบ fixed `mapping.source_rows` + immutable JSONB values และ Source field catalog แทน physical table ต่อ config · ขอปิดขอบเขต config/job/delivery views รวมถึงการเปิดดู raw Source/payload ตาม D-07/D-36 และวันรับ Web design เพื่อรักษา milestone 12-03
  - คำชี้แจงผู้ใช้ (11-16): **ไม่ใช้ MTLS เป็นโครงหลัก**; API/หน้า Web ต้องครอบคลุมหลายงาน และรอบแรกเลือก **Excel, Delimited (CSV/TSV), Fixed-width** จริง → M-27/Q9; ข้อเสนอเดิมที่จะเลื่อน Delimited/Fixed-width ใช้ไม่ได้แล้ว ส่วน destination อื่นยังเป็น backlog ตาม M-21
  - คำตอบผู้ใช้ (11-13), Q2.1: เลือก **physical Source Table แยกตาม config** → M-15 แทนข้อเสนอตารางกลาง + JSONB ในข้อความเดิม; วิธีสร้าง/เพิ่ม column ผ่าน Web หรือ migration/script, schema evolution, raw inspection และ feature scope ส่วนที่เหลือยังไม่ปิด
  - คำชี้แจงผู้ใช้ (11-13): ข้อมูลจากแต่ละเจ้ามีโครงสร้าง field ต่างกัน จึงต้องให้แต่ละ config อ้าง Source definition/table ของตัวเอง ไม่ hardcode โครง MTLS 19 columns ให้ทุก config; phase 1 ยังทำ MTLS ก่อนตาม D-01 และยังไม่ถือว่าคำชี้แจงนี้เลือกวิธีสร้างตารางใน Q2.2 แล้ว
  - คำตอบผู้ใช้ (11-13), Q2.2: เลือก **กำหนด fields ผ่านหน้า Mapping แล้วระบบสร้าง Source Table** → M-16/M1.5; ต้องปิดกติกาเปลี่ยน schema ใน Q2.3 และ permissions ใน Q7 ก่อน implement ไม่ถือว่าอนุมัติลบ/rename/เปลี่ยนชนิด field เดิมแล้ว
  - คำตอบผู้ใช้ (11-13), Q2.3: เลือก **เพิ่ม column ในตารางเดิมได้ แต่ห้ามลบ/rename/เปลี่ยนชนิด field เดิม; ถ้าต้องเปลี่ยนแบบนั้นให้สร้างตารางใหม่** → M-17; config/job เก่าอ่าน definition/table เดิมได้ ส่วน Source/payload inspection และ feature scope ที่เหลือยังไม่ปิด
  - คำตอบผู้ใช้ (11-13), Q2.4: เลือก **Source view รายแถวแบบ read-only เปิดจากรายการไฟล์/แถว พร้อม config version และสถานะส่ง** → M-18; ใช้ตรวจต้นทางและยอดความครบถ้วน ส่วน Delivery Payload inspection แยกยังรอ Q2.5 ไม่เพิ่มหน้าจัดการใบแทน Informs/UW
  - คำตอบผู้ใช้ (11-13), Q2.5: เลือก **JSON Delivery Payload inspection แบบ read-only จาก snapshot ที่บันทึกจริง รวมกรณี Informs reject** → M-19; ใช้เทียบกับ Source ไม่ compile ใหม่หรือส่งใหม่ การเปิดดู raw ไม่ได้อนุมัติ reprocess/business resend
  - คำตอบผู้ใช้ (11-13), Q2.6: เลือก **หยุดที่ 6 attempts และไม่เปิด retry หลังครบเพดานผ่าน Web** → M-20; API/worker คุมเพดานเดียวกันและไม่เพิ่มโควตาหรือรีเซ็ตประวัติจาก retry
  - คำตอบผู้ใช้ (11-13), Q2.7: เลือก **ช่วงแรกทำเฉพาะ Informs API และเก็บ LegacyTable/JsonFile/DatabaseTable เป็น backlog** → M-21; ไม่รวม destination อื่นในการย้าย Transformation Program ทั้งหมด และยังไม่ลบ requirement เดิมออกจาก backlog
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 8: ขอบเขต Web ช่วงแรกเป็น Config + ผลการส่ง + retry ตามสถานะ ตาม M-12; ยังรอ design/วันส่ง design, กติกา retry หลังครบเพดาน, raw inspection และ feature scope/Source storage ส่วนอื่น จึงยังไม่ปิด Q2 ทั้งข้อ
  - Dev 2 (11-13): ผู้ใช้เลือกเพดานรวม 6 attempts ใน M-08 แล้ว; ขอปิดกติกา manual retry หลังครบเพดานว่าจะอนุญาตเพิ่มโควตาอย่างไร พร้อม audit/reason และใช้ bytes/key เดิม โดยไม่รีเซ็ตประวัติหรือถือว่าอนุมัติ quota เพิ่มแล้ว
- [x] **Q3** (Dev 2, 11-13) Reprocess ด้วย config ใหม่/ส่งใหม่หลังแก้ mapping จะใช้ identity อย่างไร? D-44 key `(fileJobId, sourceRowId, outputKey)` ไม่รวม config version: output ที่เคย Delivered ใช้ key เดิมจะ replay ผลเก่า ส่วน key ใหม่อาจทำให้ข้อมูลเก่าทับฉบับแก้ภายหลัง · เสนอเปิด normal delivery/retry bytes/key เดิมก่อน และปิด changed-payload reprocess/business resend จนตกลง contract/lineage ชัดเจน; แยกคำตอบสำหรับ rejected 400/409, never-delivered และ Delivered
  - คำตอบผู้ใช้ (11-13): เลือก **ยังไม่เปิด changed-payload reprocess/resend ช่วงแรก**; config ใหม่ใช้กับไฟล์ที่รับใหม่ ส่วน retry งานเดิมใช้ bytes/key เดิมภายใน 6 attempts → M-22
  - ผล: ปิดคำถามสำหรับขอบเขตช่วงแรกด้วยการเลื่อน feature ทั้ง never-delivered/rejected/Delivered ไม่ได้อนุมัติสูตร key/lineage สำหรับ resend ในอนาคต; เก็บประเด็น contract นั้นไว้ใน backlog ให้ตกลง Dev 1 ก่อนเปิด
- [x] **Q4** (Dev 2, 11-13) ต่อ Q1 ข้อ 6: `sourceRowNo` ใช้ `nSeq` หรือ import record order? ขอระบุกรณี header/blank row, nSeq ว่าง/ซ้ำ/ไม่เรียง/มี gap และเวลารับไฟล์ครั้งแรกที่ต้องคงไว้ตาม D-23 · เก็บ Row Number และ Physical Line Number แยกกันเสมอ ไม่ใช้ลำดับ HTTP arrival · ปิดด้านcontract/localreceiver11-18ตาม D-80/M4.4; companydeployแยก Q7
  - Dev 2 (11-18, ปิดlocalacceptance M4.4): actualInforms `f1eca8d` รับfileReceivedAt/sourceRowNoจากhostedpipelineจริง; FullMTLS2556/Delimited/FixedWidth, HTTPpayloadhashesและUW file summaryยืนยันFileJob.CreatedAtเดิมและลำดับrecordorder; check.ps1ผ่านunit38/38 integration171/171 external9/9ไม่มีskipและEFmodel; [หลักฐาน](m4-4-informs-end-to-end-design.md) ไม่รวมcompanydeployหรือbusinessvalidationทุกstage
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.1: ใช้ลำดับแถวข้อมูลตาม M-06 ไม่ใช้ `nSeq`; header/แถวว่างไม่นับ, แถวข้อมูลเริ่ม 1, `nSeq` ที่ว่าง/ซ้ำ/ไม่เรียง/มี gap เก็บเป็นค่าดิบตาม Source โดยไม่ใช้จัดลำดับ; retry คงเลขเดิม ยังรอรายละเอียดการเก็บเวลารับไฟล์ครั้งแรกตาม D-23 จึงยังไม่ปิด Q4 ทั้งข้อ
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 6.4: ฝั่ง Mapping ใช้เวลารับไฟล์และสร้าง File Job ครั้งแรก คงเดิมเมื่อ resume/retry ตาม M-09
  - คำชี้แจงผู้ใช้ (11-13): ไม่เพิ่ม field เวลาแยก ใช้ **CreatedAt ของ File Job ฝั่ง Mapping** → M-23; ยังไม่ใช่การใช้เวลาสร้างใบฝั่ง Informs และไม่ถือว่าอนุมัติการเพิ่ม field ใน contract แล้ว
  - Dev 2 (11-13), หลักฐานเพิ่มเติมจาก Informs `6ab23ad`: `src/Informs/Informs.Api/Contracts/PaInform/pa-inform-1.schema.json` และ `PaInformContract.cs` ไม่มี field เวลารับไฟล์; `src/Informs/Informs.Application/Informs/Commands/ReceivePaInformCommand.cs` ใช้ `_timeProvider.GetUtcNow()` ส่งเข้า `Informs.Create` จึงได้ `ReceivedAt` จากเวลา HTTP intake ไม่ใช่เวลารับ File Job ที่ Mapping เลือก การส่งทีละ request ตาม M-07 ไม่แก้กรณี backlog/หลายไฟล์/retry ข้ามไฟล์
  - ข้อเสนอถึง Dev 1: ตกลงวิธีรับและใช้เวลารับไฟล์ครั้งแรกจาก Mapping สำหรับ D-23 (เช่น field metadata ที่ Dev 1 เพิ่มใน contract พร้อม semantics สำหรับผู้เรียกเดิม) หรือวิธีอื่นที่รักษาการเรียงได้; ยังไม่กำหนดชื่อ field/แก้ schema เอง และยังไม่ปิด Q4 ทั้งข้อ
  - รายละเอียดข้อเสนอหลัง M-23: หากส่งเวลาเป็น metadata ให้ใช้ค่า `FileJob.CreatedAt` เดิม ไม่สร้าง field เวลาเพิ่มในตาราง Mapping; วิธีรับ/ใช้ใน Informs ยังเป็นประเด็นของ Dev 1 จึงยังไม่ปิด Q4 ข้าม service
  - Dev 1: (11-16) ตอบ Q4 — **เพิ่ม `fileReceivedAt` (optional, date-time มี offset) ใน `pa-inform/1`** ระดับเดียวกับ `fileName` ส่งค่า `FileJob.CreatedAt` ตามที่เสนอ (M-23) ทุกแถวของไฟล์ค่าเดียวกัน, resume / retry คงค่าเดิม → [rules.md D-80](rules.md) + [contract-pa-inform.md](contract-pa-inform.md) §3 / §5 · Informs จะรับ field นี้ใน T2.6 (deploy Informs ก่อน mapping ส่ง — `additionalProperties: false`) · ส่วน `sourceRowNo` ตาม M-06 เหมือนเดิม — Q4 ปิดได้จากฝั่ง Dev 1
  - Dev 2 (11-17, เตรียม M3.3): ตรวจ schema ใน repo Informs ปัจจุบันและ fixture ที่ pin แล้ว root ยังไม่มี `fileReceivedAt` และ `additionalProperties: false`; ขอ Dev 1 ติดตาม schema/deploy T2.6 ตามคำตอบเดิม ก่อนรับตรวจ runtime กับ Informs จริง ฝั่ง Mapping จะส่ง system field ตาม D-80 และแสดง safe Blocked diagnostic เมื่อ pinned schema ยังไม่รองรับ ไม่ละ field/แก้ Informs/schema snapshot เดิมเอง
  - Dev 2 (11-18, เตรียม M4.4): origin/develop ของ Informsเป็น `f1eca8d` แล้วและมี `fileReceivedAt`; checkoutเดิม `6ab23ad` ยังไม่อัปเดต จึงทดสอบimageจากcloneชั่วคราว ได้ HTTPschema200/hash `38b1fc59bf83dda277eb2143e841a1fc34c89ef8f7b245d8a4ba8ab14e7da5bd` จริง; ไม่ได้แก้Informsและยังไม่ยืนยัน intake/metadata persistence/end-to-end หรือdeployบริษัทจน M4.4ผ่าน
- [ ] **Q5** (Dev 2, 11-13) พบช่องว่างระหว่างกฎกับ schema ตัวจริงของ Informs (`6ab23ad`): BR-035 ระบุทายาทตามกฎหมาย **100%** แต่ `$defs.beneficiary` มีเพียง `seq/fullName/relationship` และ `additionalProperties: false` — ให้ mapping ส่งแค่ชื่อ default แล้วสัดส่วนเป็นหน้าที่ Informs หรือจะเพิ่ม field โดย Dev 1? อีกกรณี D-04 ให้ส่งค่าผิดเป็น null แต่ schema บังคับ `sourceReference` nonempty/`transactionType` enum; ถ้าต้นทางไม่มีค่าที่ส่งได้ จะให้ ops/UW ติดตามอย่างไร? เสนอเก็บ Source + row error/ยอดยังไม่ส่ง โดยไม่แต่งค่าและไม่เพิ่ม contract field เอง
  - คำสั่งผู้ใช้ (11-13): **ข้ามเรื่อง field/สัดส่วนไว้ก่อน ผู้ใช้จะส่ง fields มาให้เอง; ยึดการออกแบบให้ครอบคลุมหลาย scenarios ไม่เฉพาะเจาะจง** → M-24; ยังไม่เลือกข้อเสนอส่งเฉพาะชื่อ/เพิ่ม field สัดส่วน ไม่ปิด Q5 และไม่เปลี่ยน BR-035 เอง
  - ผลต่อการทำงาน: เดินหน้าร่างกลไก catalog/compiler/schema validation/diagnostics/pinning/lineage/recovery ได้ตาม [ร่างข้อมูลและ runtime](data-runtime-design.md); actual business bindings/defaults และ acceptance ที่ต้องใช้ fields จริงยังรอ input ไม่ตีความว่าพร้อมส่ง payload จริงแล้ว
- [x] **Q6** (Dev 2, 11-13) BR-001 กันไฟล์ซ้ำด้วย content hash ยังเป็น 🔶: ยืนยัน scope ของ hash ว่าเป็นต่อ config/partner/ช่วงเวลา/ทุก version และจัดการไฟล์แก้ไขชื่อเดิม/เนื้อหาเดิมที่ reprocess อย่างไร? แยกจาก DB occurrence claim ที่จำเป็นสำหรับกันหลาย pod รับไฟล์เดียวกัน · ยังไม่ถือว่า demo duplicate policy เป็นกฎที่อนุมัติแล้ว
  - คำตอบผู้ใช้ (11-16): ตกลง **config เดิม/hash เดิมซ้ำข้าม versions แม้เปลี่ยนชื่อ; เนื้อหาเปลี่ยนรับ File Job ใหม่และอ่าน/ส่งทุกแถว**; Informs ตัดสินใบซ้ำตามสถานะตาม D-05/D-46 → M-25
  - ผล: ปิด policy ภายใน Mapping ตาม M-25 และใช้ในแบบ M1.1; ขอ Dev 1 อัปเดต BR-001/ผล kickoff ใน rules ตามเจ้าของเอกสาร ไม่ตีความว่าเปลี่ยนกฎในไฟล์ของ Dev 1 ไปแล้ว
- [ ] **Q7** (Dev 2, 11-13) ขอ Dev 1 ระบุตำแหน่ง/identity ของ **fixture MTLS 2,556 แถวที่รับรองว่าเป็นข้อมูลสมมติ** (ไฟล์ `docs/MTLSPA0220261009.xls` ถูกอธิบายว่าเป็นตัวอย่างจริง จึงไม่ใช้ทดแทน) และ dependency: Kafka/topics/groups/owner, Mapping DB บริษัท, share mount/archive/snapshot access, Informs internal URL/auth/TLS, encryption key provisioning/rotation/retention และผู้ดู Source/delivery log/payload; phase นี้ใช้ fixed actor ตาม web.md ไม่สร้างระบบสิทธิ์จาก header ที่ browser กำหนดเอง
  - Dev 2 (11-17, หลังข้อตกลงใช้ local): ปิด M3.1 เฉพาะ local แล้ว; broker/topics/groups/owner/auth/TLS ของ Kafka บริษัทคงเป็นรายการเปิดใน Q7 ที่ต้องตกลง/เตรียม/ตรวจเชื่อมต่อก่อนเปิด messaging บน environment บริษัท ไม่รอรายการนี้เพื่อทำ M3.2/M3.3 ต่อ
  - Dev 2 (11-17): ปิดเฉพาะ dependency/source identity ของ Named Lookup สำหรับ M2.5 ด้วยคำยืนยันผู้ใช้ตาม M-28; SQL Server metadata/query ด้วย keys สมมติและ API capability smoke ผ่านบน UAT เมื่อใช้ TLS setting ที่ผู้ใช้อนุญาต ส่วน Kafka/Mapping DB บริษัท/share/Informs/key/permissions ที่เหลือยังไม่ปิด Q7 ทั้งข้อ
  - Dev 1 (11-16): **fixture พร้อมแล้ว** ที่ [`fixtures/MTLSPA0220261201.xls`](fixtures/README.md) ข้อมูลสมมติทั้งหมด 2,556 แถว (`P` 2,553 / `C` 3 · 1,797 คน · 756 คนมี 2 แถว) สร้างด้วย `fixtures/make-mtls-fixture.py` (seed เดิม = ได้ไฟล์เหมือนเดิมทุก byte) · ⚠️ **ไฟล์จริงเป็น Excel 5.0/95 (BIFF5, stream `Book`) ไม่ใช่ BIFF8** และมี CODEPAGE = 1252 ทั้งที่ข้อความเป็น cp874 ([mtls-file.md](mtls-file.md) §1 แก้แล้ว) — fixture ทำแบบเดียวกัน reader ต้องอ่าน BIFF5 ได้และบังคับ cp874 · `docs/MTLSPA0220261009.xls` เป็นข้อมูลจริง **ห้ามใช้** (ไฟล์อยู่ใน git ของ repo นี้แล้ว — Dev 1 กำลังดูว่าจะเอาออกอย่างไร) · dependency ที่เหลือ (Kafka / DB บริษัท / share mount / URL Informs / key) ตอบตอน kickoff
  - Dev 2 (11-17): local Kafka/topic init และ dispatcher/consumer/inbox/offset tests ผ่านตาม M-31/[M3.1](m3-1-messaging-design.md); ปิดเฉพาะการเตรียม local ใน Q7 ส่วน broker/topics/groups/owner/auth/TLS บริษัทและ dependency อื่นยังรอ ไม่ถือว่าผล local ยืนยัน environment บริษัท
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 9.1: ใช้ Kafka + transactional outbox ตาม M-13; ยังไม่ได้ระบุ broker/topics/groups/owner ของบริษัท และยังไม่ถือว่ามี Kafka local พร้อมใช้
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 9.2: เริ่ม Kafka local ผ่าน Docker ตาม M-14 เพื่อเดินงานพัฒนา/ทดสอบก่อน; broker/topics/groups/owner บริษัทและ dependency อื่นใน Q7 ยังรอข้อมูล
  - คำตอบผู้ใช้ (11-13), Q1 / kickoff ข้อ 7.2: ผู้ใช้งาน delivery log คือทีม Mapping / Dev / Ops ตาม M-11; คำตอบนี้ปิดเฉพาะกลุ่มผู้ใช้งาน log ไม่ครอบคลุม raw Source/payload inspection หรือ dependency อื่นใน Q7 และไม่เพิ่มระบบสิทธิ์นอก `web.md`
- [ ] **Q8** (Dev 2, 11-13) เสนอ prefix API **`/api/mapping`**, BaseResponse เดิม, field-path errors, expected `rowVersion` → 409, list `items/totalCount/page/pageSize` (default 20/max 100), dates ISO/UTC และ route/action subset ใน [แผน API §5](api-implementation-plan.md#5-api-ที่เสนอก่อนมี-web-design) · ขอ review ก่อน M1.2/เขียน controller; pause intake/delivery และ raw payload inspection ให้เปิดเท่าที่ scope Q1/Q2 ตกลง
  - คำตอบผู้ใช้ (11-16): **ใช้ `/api/mapping`** เป็น prefix; บันทึกใน [ร่าง M1.2](mapping-api-contract.md) แล้ว
  - คำตอบผู้ใช้ (11-16): ขอ **review ภายหลัง**; เดินงานแบบข้อมูล/persistence ภายใน Mapping ได้ตาม M1.1 แต่ยังไม่ถือว่า Dev 1 เห็นชอบ route/DTO และไม่ implement API controller จากร่างนี้ก่อน review
  - ยังรอ: Dev 1 review เฉพาะ route/DTO ที่กระทบ Web/contract boundary; prefix ไม่ใช่การอนุมัติ response/error/action contract ทั้งชุด
  - Dev 2 (11-18, M4.3): ผู้ใช้ยืนยัน [contract subset M4.3 พร้อม request/response/counts/action guards](m4-3-delivery-operations-api-design.md) และ source batch แล้ว จึง implement controllers เฉพาะ subset นี้ได้; ใช้ prefix/BaseResponse เดิม ไม่เปลี่ยน Web/Informs และไม่ถือว่าปิด M1.2/Q8 ทั้งชุดหรือ Dev 1 รับรอง Web integration
- [ ] **Q9** (Dev 2, 11-16) ผู้ใช้ชี้แจงว่า **MTLS เป็นเพียงตัวอย่างหนึ่ง ไม่ใช่ไฟล์หลัก** และให้ API/หน้า Mapping รองรับหลายงาน/หลายรูปแบบ; เลือกรอบแรก **Excel, Delimited (CSV/TSV), Fixed-width** ที่ใช้งานได้จริงตาม [แบบหลาย format](multi-format-api-web-design.md) · **ข้อเสนอถึง Dev 1/PM:** ให้ทั้งสาม format และ partner อื่นเป็น acceptance ของ API/Web/runtime ใน UAT และตกลงผลต่อวันส่ง/กำลังคนเทียบ D-01/D-70 ซึ่งเดิมกำหนด MTLS เป็น partner แรกและไม่ทำ partner อื่น; ห้ามถือว่าคำตอบผู้ใช้แก้ D-01/D-70 ใน rules แล้ว
  - คำตอบผู้ใช้ (11-16): **partner อื่นจะเปิดใช้ใน UAT**; ไม่ใช่เพียงเตรียม parser/UI กับ synthetic fixtures แล้วเปิดใช้ภายหลัง
  - ยังรอ: Dev 1/PM ลงผลขอบเขต/วันส่งเป็น `D-xx` ใน `rules.md`; partner identities, field bindings และตัวอย่างไฟล์ที่ใช้ทดสอบจริงผู้ใช้จะส่งภายหลังตาม M-24/Q5; ไม่เดาข้อมูลธุรกิจของ partner ใหม่
  - ผลต่อแผน Dev 2: M2.1 เป็น reader core+Excel; เพิ่ม M2.6 Delimited, M2.7 Fixed-width; M1.2/M2.3/M5.3 เปลี่ยนเป็น format-neutral; MTLS 2,556 แถวยังเป็น regression fixture หนึ่ง ไม่ใช้เป็น schema ของ config ทั้งระบบ

- [ ] **Q10** (Dev 1, 11-19) **deadline ใหม่ D-81 — พร้อม UAT 11-30 · เริ่ม UAT 12-01** ([rules.md](rules.md) D-81) · Dev 1 ทวนโค้ด `prjInsuranceValidationMapping` `3fd48f8` แล้ว พบงานที่ต้องเสร็จก่อน 11-30 ที่ยังไม่อยู่ใน §3 — ขอ Dev 2 แตกเป็น bullet ใน §3 + ให้เป้าตามวันใน §2:
  1. **API จัดการ config** — ตอนนี้มี `IConfigLifecycleRepository` (`SaveVersionAsync` / `ActivateAsync` / `DisableAsync`) แต่ไม่มี command / controller · controller มีแค่ `mapping-configs/{id}/validate` · `preview` · `preview-file` → ยังสร้าง config / activate ใน env ใหม่ไม่ได้ และหน้าเว็บ Configs ไม่มี API ให้เรียก · ต้องมี: รายการ config · สร้าง config · บันทึก version ใหม่ · activate · disable (ตาม M-12) — เป้า **11-20**
  2. **script ติดตั้ง config MTLS** ใน env ใหม่ (ผ่าน API ข้อ 1) — Program JSON ของ MTLS ตอนนี้อยู่ใน test fixture เท่านั้น — เป้า **11-23**
  3. **health probe** `/health/live` + `/health/ready` แบบเดียวกับ Informs / Products (ดูหมายเหตุ Dev 1 ใต้ M5.1) — เป้า **11-20**
  4. **หน้าเว็บ mapping ครบตามแบบ — ผู้ใช้ 11-19 "ไม่ย่อ"** — Configs · Config editor ต่อ format (Source fields · Input Layout · Mapping Program · destination profile · validate / preview · version / activate) · Jobs ตาม [แบบหลาย format §4](multi-format-api-web-design.md) และ M-12 · แบบหน้าจอใช้ mockup เดิมใน `mapping-demo/web` เป็นแนว · ลำดับที่ Dev 1 เสนอ: Jobs → Configs → Config editor · **จุดเช็ค 11-25** (Jobs + Configs ใช้ได้ — ช้าให้บอก Dev 1 ทันที) · **เสร็จ 11-27** · 11-26 เช้า Dev 2 ช่วย Dev 1 deploy Mapping แล้วกลับมาทำหน้าเว็บต่อ (Dev 1 deploy เป็นหลัก)
  5. **สิ่งที่ต้องขอจากบริษัท (Dev 1 ส่งให้ PM 11-19 — ต้องได้ 11-24):** schema `mapping` บน DB บริษัท + สิทธิ์ `CREATE` ใน schema (Source Table ต่อ config สร้างตอนทำงาน — `SourceStorageRepository.cs` `CREATE TABLE mapping.{table}`) · Kafka 3 topic ของ Mapping · ที่เก็บ key เข้ารหัส payload (Secret) · share folder MTLS — ถ้า Dev 2 เห็นว่ามีอย่างอื่นที่ต้องขออีก เขียนตอบใต้ข้อนี้วันนี้
  6. M4.4 — Informs รับ `fileReceivedAt` แล้ว (หมายเหตุ Dev 1 ใต้ M4.4) — เป้า **11-19 / 11-20**

- [ ] **Q11** (Dev 1, 11-23) **D-85 — ผู้ใช้สร้าง contract เองบนหน้าเว็บ** ([rules.md](rules.md) D-85) · Dev 1 แก้โค้ดใน repo Mapping + Web ให้แล้ว (ผู้ใช้สั่ง · **ยังไม่ commit**) — ขอ Dev 2 ทวน แล้วถือเป็นฐานของงานถัดไป:
  - **config:** section `Connections[]` (`Key` · `BaseUrl` · `TimeoutSeconds` · `MaxResponseBytes`) แทน `Destinations` · env `Connections__0__BaseUrl` · Local `informs` → `http://localhost:5176` · docker `http://informs:8080` — manifest Kubernetes ต้องใช้ชื่อใหม่
  - **DB:** ตาราง `contract_registrations` + `contract_revisions` (แก้ไม่ได้ — trigger) · migration `AddContractRegistrations` · รุ่น = snapshot schema + `destination_profile_snapshots` (`profile_key` = ชื่อ contract, `profile_version` = เลขรุ่น) · DB docker เดิมต้องล้าง schema `mapping`
  - **API:** `POST /api/mapping/contracts` · `POST /contracts/{name}/revisions` · `DELETE /contracts/{name}?reason=` · `GET /connections` · `GET /contracts` และ `GET /contracts/{name}?revision=n` **อ่าน DB อย่างเดียว ไม่เรียกปลายทาง** · `ContractCatalogRepository` / `ApiContractClient` (ส่งจริงใช้ path จากรุ่นที่ pin + `BaseUrl` จาก `Connections`) · hash ปลายทางไม่รวม `BaseUrl` (เดิม DB เดียวกันสลับรันบนเครื่อง / docker แล้ว Conflict)
  - **Web:** หน้า Contract สร้าง / แก้ (`contract-form.ts`) / ดูรุ่น / ดึง schema ล่าสุด / ลบ · wizard ② ไม่มี contract = ลิงก์ไปสร้าง
  - ผล: `check.ps1` ผ่าน (unit 39 · integration 178) · ng test 82/82
  - **แก้ 11-23 รอบ 2 (ผู้ใช้):** schema **กำหนดใน Mapping** — ไม่มี schema path / ไม่ดึงจากปลายทาง (legacy ไม่มี endpoint) · body `schema` (JSON object) · ลบ `ContractSchemaClient` · `GET /api/mapping/contract-schemas/{name}?revision=n` · Web หน้า `/mapping/contracts/new` + `/edit` (ตาราง field + วางตัวอย่าง JSON + อัปโหลด JSON Schema — `contract-schema.ts`) แทน modal · migration `AddContractRegistrations` สร้างใหม่ (ยังไม่ commit) · `check.ps1` ผ่าน · ng test 88/88

- [ ] **Q12** (Dev 1, 11-24) **D-86 — Mapping Profile** ([rules.md](rules.md) D-86) · ผู้ใช้ตัดสินแล้ว — ขอ Dev 2 ประเมินว่าทำได้ไหม / ใช้เวลาเท่าไร และแตกเป็น bullet ใน §3 · **ฝั่ง API ที่ต้องมี:**
  1. **ที่เก็บร่างของ profile** — ร่างรวม รูปแบบไฟล์ · รายการ field · ปลายทาง (contract) · Transformation Program · เงื่อนไข — แก้ทับได้ (expectedRowVersion) จนกว่าจะ activate ครั้งแรก · อ่านกลับได้
  2. **Preview / validate ด้วย field ในร่าง** — ไม่ต้องมี Source definition ที่สร้างตารางแล้ว (ตอนนี้ `previewDraftFile` ต้องมี `sourceDefinitionVersionId`)
  3. **Source definition ไม่มีตารางจนกว่าจะ activate ครั้งแรก** — ก่อนนั้นแทนที่รายการ field ได้ (ลบ / เปลี่ยน key) · `CREATE TABLE` ย้ายจาก `CreateSourceDefinition` ไปตอน activate ครั้งแรก · activate version ที่ pin รุ่น field เก่ากว่ารุ่นปัจจุบัน = ไม่ให้
  4. **ลบ profile ที่ยังไม่เคย activate** (+ ตารางว่างที่สร้างแบบเดิม ถ้ามี) — audit พร้อมเหตุผล
  - ฝั่ง Web (Dev 1 ทำ): หน้า Mapping Profile แทน wizard · badge ตรวจข้ามขั้น · เปลี่ยนคำ Config → Mapping Profile
  - **Dev 1 ทำบางส่วนให้แล้ว (11-24 — ผู้ใช้สั่ง · ยังไม่ commit · ขอ Dev 2 ทวน):** `POST /mapping-configs` ไม่บังคับ `inputPath` ("" = ร่างที่ยังไม่มี folder → activate ได้ `InvalidRoute`) · ใหม่ `PUT /api/mapping/mapping-configs/{id}` (ชื่อ / folder) และ `DELETE /api/mapping/mapping-configs/{id}?expectedRowVersion=&requestId=&reason=` (ลบร่าง) — เฉพาะ config ที่ `ActiveVersionId` ว่าง (ไม่งั้น 409 `AlreadyActivated`) · `UpdateMappingConfigCommand` / `DeleteMappingConfigCommand` + validator · `ConfigLifecycleRepository.UpdateAsync` / `DeleteAsync` (`DraftChangeAsync` — request-ID replay + audit `UpdateConfig` / `DeleteConfig` แบบเดียวกับ activate) · **ลบ = ซ่อน**: คอลัมน์ `mapping_configs.deleted_at` (migration `AddMappingConfigDeletedAt`) เพราะ `mapping_config_versions` / `source_definition_versions` มี trigger ห้ามลบ · ซ่อนใน `MappingConfigReader` / `SourceStorageRepository.ListAsync` / `PreviewConfigReader` และ save version / activate / สร้าง Source definition ตอบ NotFound · test: `MappingConfigsApiTests.Draft_ShouldStartWithNameOnly_GetFolderLater_AndBeDeletedBeforeActivation` + ต่อท้าย test เดิม (หลัง activate แก้ / ลบไม่ได้) · ผล unit 39 · integration 179 + skip 9 · ข้อ 1–3 ของ Q12 ยังเป็นของ Dev 2


- [ ] **Q13** (Dev 1, 11-26) **D-87 — ขั้น ② ตั้งค่าตามปลายทาง** ([rules.md](rules.md) D-87) · ผู้ใช้ตัดสินแล้ว · **Dev 1 ทำฝั่ง Web เอง** (repo `prjInsuranceValidationWeb`, `src/app/features/mapping/`) — แจ้งก่อนลงมือ เพราะจะเอางานของ Dev 2 ออก:
  1. **เอาออก:** modal การ์ดตามคอลัมน์ใน `source-mapping-editor.ts/.html` (commit `e6349e4` · `34414e5` · `0c2bc9e`) · ปุ่ม "ค่าอื่น" · ตาราง "จับคู่ข้อมูลจากต้นทาง" · ฟังก์ชัน `groupMappingCards` / `cardAssignmentsFor` / `otherAssignments` / `appendMappingArrayItem` / `moveMappingTarget` ใน `source-mapping-model.ts`
  2. **ใช้ต่อ:** `value-expression-editor.ts` · `predicate-editor*.ts` · `mapping-program.ts` · ฟังก์ชันจัดการ array (`resizeMappingArray` · `removeMappingArrayItem` · `mappingArrayGroups` · `mappingTargetItem`) · `sourceMappingRows` (ทำแผงต้นทาง) · `autoMapSources` · `mappingDraftErrors`
  3. **Mapping API ไม่ต้องแก้** · ขอ**อย่าเอา predicate `equals` ออก** — เว็บใช้บันทึก case + "ถ้าไม่ตรงค่าไหน" เป็น `caseArms` + `else` (`equals` อยู่ใน compiler / evaluator แต่ไม่อยู่ใน `GET /capabilities` `Predicates`)
  - ถ้า Dev 2 กำลังแก้ไฟล์ในข้อ 1 อยู่ บอก Dev 1 ก่อนจะได้ไม่ชนกัน
  - Dev 1 (11-26): ทำแล้ว — repo Web ยังไม่ commit · ไฟล์ใหม่ `destination-tree.ts/.html` · ลบ `source-mapping-editor.ts/.html` · ลบ `mappingDraftErrors` + `mappingTargetItem` ด้วย (ไม่มีที่ใช้แล้ว) · ข้อมูล / program / API ไม่เปลี่ยน
