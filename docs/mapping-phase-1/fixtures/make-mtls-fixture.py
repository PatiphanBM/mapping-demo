"""Make a synthetic MTLS file: same shape as the real one, no real personal data.

Shape copied from 01-analysis/mtls-sample-file.md (statistics only): 2,556 rows (P 2,553 / C 3), 1,797 people
(1,041 with 1 policy, 756 with 2), 2 ID numbers cut to 11 digits, premium 180, age = start year - birth year,
19 Thai columns. The file format matches the real one too: Excel 5.0/95 (BIFF5, stream "Book") whose CODEPAGE
record says 1252 while the Thai text is cp874 bytes, so a reader must force cp874.

Names, ID numbers, PA numbers, branches and house numbers are generated. Place names (sub-district / district /
province / post code) are real public names so an address parser has something to match; occupations and
beneficiary relations are the category lists seen in the real file.

Usage: python -I scripts/make-mtls-fixture.py <output.xls> [--start 2026-12-01] [--seed 20261201]
Same seed + start = same bytes.
"""

import argparse
import datetime as dt
import random
import struct
from pathlib import Path

ROWS = 2556
CANCEL_SEQ = (671, 672, 2542)
TWO_POLICY_PEOPLE = 756
SHORT_ID_ROWS = 2
BENEFICIARY2_ROWS = 210
DUP_WORD_ROWS = 20
BRANCHES = 1079

HEADER = ["ลำดับ", "Transaction Type", "ชื่อ-สกุล", "เลขบัตรประชาชน", "หมายเลข PA", "วันเริ่มคุ้มครอง", "วันสิ้นสุด",
          "รหัสสาขา", "สาขา", "จำนวนเงิน", "วันเดือนปีเกิด", "อายุ", "ที่อยู่", "ผู้รับผล1", "สัมพันธ์1", "ผู้รับผล2",
          "สัมพันธ์2", "อาชีพ", "เวลาเริ่มคุ้มครอง"]

# --- category lists with the counts seen in the real file -----------------------------------------------------

OCCUPATIONS = [
    ("พนักงานบริษัท-ประจำ สนง.", 585), ("เกษตรกร ชาวไร่ ชาวนา ชาวสวน", 403), ("คนงาน/ลูกจ้างผู้ใช้แรงงาน", 352),
    ("เจ้าของธุรกิจ/กิจการ/ร้านค้า", 248), ("ค้าขาย (หาบเร่/แผงลอย)", 192), ("พนักงานขับรถ / ส่งเอกสาร", 152),
    ("พนักงานบริษัท-ไม่ประจำ สนง.", 113), ("รับจ้าง", 98), ("ข้าราชการ-ประจำ สนง.", 60), ("พนักงาน", 41),
    ("ค้าขาย", 39), ("อื่นๆ", 35), ("วิศวกร/หัวหน้าช่าง/ผู้รับเหมา", 21), ("พนง.รัฐวิสาหกิจ-ประจำ สนง.", 19),
    ("แม่บ้าน (รับจ้าง)", 19), ("แม่บ้าน(ไม่ได้รับจ้าง)", 19), ("พนักงานรักษาความปลอดภัย", 18),
    ("ครูอาจารย์/นักวิชาการ", 10), ("ช่างฝีมือต่างๆ", 9), ("เกษียณ", 8), ("ชาวประมง / ลูกเรือ", 8),
    ("ลูกจ้าง (พนักงานในสำนักงาน)", 7), ("พนักงานทำความสะอาด (สำนักงาน)", 7), ("พนักงานขับรถ", 6),
    ("พนักงานธนาคาร-ประจำ สนง.", 6), ("พนง.รัฐวิสาหกิจ-ไม่ประจำ สนง.", 5), ("เจ้าของกิจการ", 5),
    ("ทหาร/ตำรวจ (ประจำสำนักงาน)", 5), ("ทหาร", 5), ("เจ้าของธุรกิจ", 5), ("ข้าราชการ", 4), ("ช่างฝีมือต่าง ๆ", 4),
    ("ผู้รับเหมาก่อสร้าง", 3), ("ค้าขาย(ยกเว้นในตลาด)", 3), ("พนักงานธนาคาร-ไม่ประจำ สนง.", 3), ("พ่อครัว", 3),
    ("พนักงานขาย", 3), ("นิสิต", 2), ("ช่างเทคนิค", 2), ("ช่างถ่ายรูป ช่างถ่ายภาพยนตร์", 2), ("พนักงานต้อนรับ", 2),
    ("พนักงานรัฐวิสาหกิจ", 2), ("ผู้ช่วยพยาบาล", 2), ("บุคลากรทางการแพทย์", 2), ("นักบัญชี", 2), ("นักการเมือง", 2),
    ("พ่อบ้าน(ไม่ได้รับจ้าง)", 2), ("พนักงานขายของหน้าร้าน", 2), ("ข้าราชการ-ไม่ประจำ สนง.", 1),
    ("ลูกจ้างชั่วคราว (ในสำนักงาน)", 1), ("ฝ่ายขาย", 1), ("บุรุษไปรษณีย์", 1), ("ผู้ช่วยผู้จัดการ", 1), ("ผช.แพทย์", 1),
    ("พนักงานธนาคาร", 1), ("ค้าขาย (มีแผงประจำในตลาด)", 1), ("พนักงานส่งของ", 1), ("แม่ครัว", 1), ("นักออกแบบ", 1),
]
RELATIONS1 = [("บุตร", 1064), ("ภรรยา", 537), ("มารดา", 380), ("สามี", 221), ("บิดา", 90), ("น้องสาว", 63),
              ("หลาน", 58), ("พี่สาว", 58), ("น้องชาย", 27), ("พี่ชาย", 24), ("น้า", 10), ("ป้า", 6), ("ปู่", 6),
              ("ยาย", 4), ("ย่า", 3), ("อา", 3), ("เหลน", 2)]
RELATIONS2 = [("บุตร", 144), ("สามี", 18), ("มารดา", 17), ("หลาน", 7), ("น้องชาย", 6), ("ภรรยา", 5), ("พี่สาว", 4),
              ("พี่ชาย", 3), ("น้องสาว", 3), ("บิดา", 3)]
# beneficiary name prefixes as written by MTLS ("" = none, " " after = prefix with a space)
BENEFICIARY_PREFIXES = [("นางสาว", 743), ("นาย", 586), ("", 452), ("นาง", 356), ("เด็กหญิง", 124), ("น.ส.", 118),
                        ("เด็กชาย", 98), ("ด.ญ.", 46), ("ด.ช.", 32), ("นส.", 1)]
AGES = [(20, 9), (21, 15), (22, 17), (23, 20), (24, 16), (25, 31), (26, 52), (27, 33), (28, 50), (29, 65), (30, 57),
        (31, 63), (32, 64), (33, 57), (34, 63), (35, 69), (36, 103), (37, 80), (38, 60), (39, 53), (40, 69), (41, 67),
        (42, 76), (43, 84), (44, 84), (45, 97), (46, 73), (47, 86), (48, 82), (49, 68), (50, 75), (51, 67), (52, 94),
        (53, 59), (54, 59), (55, 54), (56, 66), (57, 60), (58, 59), (59, 44), (60, 24), (61, 35), (62, 30), (63, 32),
        (64, 18), (65, 17)]
HOURS = [(8, 104), (9, 344), (10, 362), (11, 353), (12, 312), (13, 280), (14, 330), (15, 296), (16, 169), (17, 5),
         (18, 1)]
TITLES = [("นาย", 1281), ("นางสาว", 1006), ("นาง", 266)]

# --- generated names (common Thai given names / made-up surnames) ----------------------------------------------

MALE = ["สมชาย", "สมศักดิ์", "ประเสริฐ", "วิชัย", "สุรชัย", "อนุชา", "ธนากร", "กิตติพงษ์", "ณัฐพล", "ชัยวัฒน์", "บุญมี",
        "สมพงษ์", "ประยุทธ", "วีระ", "สุทธิพงษ์", "อภิชาติ", "ศักดิ์ชัย", "ธีรพงษ์", "พงศกร", "จักรพันธ์", "ทองดี", "เกรียงไกร",
        "มานพ", "สมบัติ", "อำนาจ", "ปกรณ์", "ภานุวัฒน์", "วรวุฒิ", "นพดล", "สุริยา"]
FEMALE = ["สมใจ", "สุดารัตน์", "มาลี", "วิไลวรรณ", "กาญจนา", "นภาพร", "ศิริพร", "จันทร์เพ็ญ", "รัตนา", "พรทิพย์",
          "อรุณี", "ปราณี", "สุภาพร", "ณัฐธิดา", "กัลยา", "ลำดวน", "บุษบา", "วันเพ็ญ", "ธิดารัตน์", "จิราพร", "สายสุนีย์",
          "อัมพร", "เพ็ญศรี", "ดวงใจ", "ปิยะนุช", "ชนิดา", "สุนันทา", "ทองใบ", "ยุพิน", "อารีรัตน์"]
SURNAME_HEAD = ["แก้ว", "ทอง", "ศรี", "บุญ", "ใจ", "สุข", "มั่น", "รัก", "พูล", "จันทร์", "แสง", "เพชร", "ดวง", "สม", "ชัย"]
SURNAME_TAIL = ["ประเสริฐ", "มณี", "สวัสดิ์", "ทวี", "ดี", "เจริญ", "สมบูรณ์", "วงศ์", "สุวรรณ", "พันธ์", "ศรีสุข",
                "อ่อน", "เรือง", "นาค", "ทองคำ"]
THREE_WORD_SURNAMES = ["ณ นคร", "ณ ลำปาง", "ณ สงขลา"]

# --- real place names: (sub-district, district, province, post code) --------------------------------------------

PROVINCIAL = [
    ("ในเมือง", "เมืองขอนแก่น", "ขอนแก่น", "40000"), ("ศรีภูมิ", "เมืองเชียงใหม่", "เชียงใหม่", "50200"),
    ("ช้างเผือก", "เมืองเชียงใหม่", "เชียงใหม่", "50300"), ("หาดใหญ่", "หาดใหญ่", "สงขลา", "90110"),
    ("ในเมือง", "เมืองนครราชสีมา", "นครราชสีมา", "30000"), ("หมากแข้ง", "เมืองอุดรธานี", "อุดรธานี", "41000"),
    ("ตลาด", "เมืองมหาสารคาม", "มหาสารคาม", "44000"), ("บางปลาสร้อย", "เมืองชลบุรี", "ชลบุรี", "20000"),
    ("หนองปรือ", "บางละมุง", "ชลบุรี", "20150"), ("นาเกลือ", "บางละมุง", "ชลบุรี", "20150"),
    ("ในเมือง", "เมืองอุบลราชธานี", "อุบลราชธานี", "34000"), ("ตลาดใหญ่", "เมืองภูเก็ต", "ภูเก็ต", "83000"),
    ("ปากเพรียว", "เมืองสระบุรี", "สระบุรี", "18000"), ("หน้าเมือง", "เมืองราชบุรี", "ราชบุรี", "70000"),
    ("ประตูชัย", "พระนครศรีอยุธยา", "พระนครศรีอยุธยา", "13000"), ("ปากน้ำ", "เมืองสมุทรปราการ", "สมุทรปราการ", "10270"),
    ("บางกระสอ", "เมืองนนทบุรี", "นนทบุรี", "11000"), ("คลองหนึ่ง", "คลองหลวง", "ปทุมธานี", "12120"),
    ("เวียง", "เมืองเชียงราย", "เชียงราย", "57000"), ("ในเมือง", "เมืองพิษณุโลก", "พิษณุโลก", "65000"),
    ("มะขามเตี้ย", "เมืองสุราษฎร์ธานี", "สุราษฎร์ธานี", "84000"),
    ("ในเมือง", "เมืองนครศรีธรรมราช", "นครศรีธรรมราช", "80000"), ("ท่าประดู่", "เมืองระยอง", "ระยอง", "21000"),
]
BANGKOK = [
    ("ลาดยาว", "จตุจักร", "10900"), ("จตุจักร", "จตุจักร", "10900"), ("สามเสนใน", "พญาไท", "10400"),
    ("คลองตันเหนือ", "วัฒนา", "10110"), ("บางนาเหนือ", "บางนา", "10260"), ("หัวหมาก", "บางกะปิ", "10240"),
    ("บางจาก", "พระโขนง", "10260"), ("ทุ่งสองห้อง", "หลักสี่", "10210"), ("อนุสาวรีย์", "บางเขน", "10220"),
    ("บางซื่อ", "บางซื่อ", "10800"), ("ดินแดง", "ดินแดง", "10400"), ("วงศ์สว่าง", "บางซื่อ", "10800"),
]
BANGKOK_PEOPLE = 320  # ~454 rows like the real file (some people have 2 rows)


def expand(pairs):
    return [value for value, count in pairs for _ in range(count)]


def weighted(rng, pairs):
    values, weights = zip(*pairs)
    return rng.choices(values, weights)[0]


def thai_id(rng, used):
    while True:
        digits = [rng.randint(1, 8)] + [rng.randint(0, 9) for _ in range(11)]
        check = (11 - sum(d * (13 - i) for i, d in enumerate(digits)) % 11) % 10
        number = int("".join(map(str, digits + [check])))
        if number not in used:
            used.add(number)
            return number


def unique_code(rng, used, make):
    while True:
        code = make()
        if code not in used:
            used.add(code)
            return code


def person_name(rng, title):
    first = rng.choice(MALE if title == "นาย" else FEMALE)
    last = rng.choice(SURNAME_HEAD) + rng.choice(SURNAME_TAIL)
    return f"{title}{first} {last}"


def beneficiary_name(rng):
    prefix = weighted(rng, BENEFICIARY_PREFIXES)
    female = prefix in ("นางสาว", "นาง", "เด็กหญิง", "น.ส.", "ด.ญ.", "นส.") or (prefix == "" and rng.random() < .5)
    first = rng.choice(FEMALE if female else MALE)
    last = rng.choice(SURNAME_HEAD) + rng.choice(SURNAME_TAIL)
    space = " " if prefix and rng.random() < .3 else ""
    return f"{prefix}{space}{first} {last}"


def address(rng, bangkok):
    house = f"{rng.randint(1, 999)}" + (f"/{rng.randint(1, 99)}" if rng.random() < .3 else "")
    if bangkok:
        sub, dist, post = rng.choice(BANGKOK)
        soi = f" ซ.{rng.randint(1, 60)}" if rng.random() < .5 else ""
        return f"{house}{soi} แขวง{sub} เขต{dist} กรุงเทพมหานคร {post}"
    sub, dist, prov, post = rng.choice(PROVINCIAL)
    moo = f" ม.{rng.randint(1, 15)}" if rng.random() < .95 else ""
    return f"{house}{moo} ต.{sub} อ.{dist} จ.{prov} {post}"


def make_rows(start, seed):
    rng = random.Random(seed)
    end = start.replace(year=start.year + 1) - dt.timedelta(days=1)
    ymd = lambda d: int(d.strftime("%Y%m%d"))

    # people: 2 rows for 756, 1 row for the rest of the P rows
    p_rows = ROWS - len(CANCEL_SEQ)
    one_policy = p_rows - 2 * TWO_POLICY_PEOPLE
    ids, pas = set(), set()
    codes = sorted(rng.sample(range(100, 10000), BRANCHES))  # 3–4 digit codes, one name per code
    branches = {code: f"สาขาทดสอบ {code:04d}" for code in codes}
    titles = expand(TITLES)
    rng.shuffle(titles)
    occupations = [name for name, _ in OCCUPATIONS]  # every occupation at least once, then by weight
    people = []
    for i in range(one_policy + TWO_POLICY_PEOPLE):
        title = titles[i % len(titles)]
        age = weighted(rng, AGES)
        born = dt.date(start.year - age, rng.randint(1, 12), rng.randint(1, 28))
        branch = codes[i] if i < len(codes) else rng.choice(codes)
        occupation = occupations[i] if i < len(occupations) else weighted(rng, OCCUPATIONS)
        people.append({
            "name": person_name(rng, title), "id": thai_id(rng, ids), "born": born, "age": age,
            "address": address(rng, False), "branch": branch, "occupation": occupation,
            "ben1": (beneficiary_name(rng), weighted(rng, RELATIONS1)), "policies": 2 if i >= one_policy else 1,
        })
    rng.shuffle(people)  # branches / occupations were handed out in order
    for person in rng.sample([p for p in people if p["policies"] == 1], 3):  # 3 rows with three words
        person["name"] = person["name"].split(" ")[0] + " " + rng.choice(THREE_WORD_SURNAMES)
    for person in rng.sample([p for p in people if p["policies"] == 1], SHORT_ID_ROWS):
        person["id"] = int(str(person["id"])[:11])  # cut to 11 digits — fails the checksum like the real file
    for person in rng.sample(people, BANGKOK_PEOPLE):
        person["address"] = address(rng, True)
    for person in rng.sample(people, DUP_WORD_ROWS):  # repeated words, the kind legacy RemoveDupWords cleaned
        words = person["address"].split(" ")
        k = rng.randrange(1, len(words) - 1)
        person["address"] = " ".join(words[:k + 1] + [words[k]] + words[k + 1:])

    # order: most second policies right after the first (bought together), some later in the file
    slots = []
    later = []
    for person in people:
        slots.append(person)
        if person["policies"] == 2:
            (slots if rng.random() < .8 else later).append(person)
    for person in later:
        slots.insert(rng.randrange(slots.index(person) + 1, len(slots) + 1), person)

    ben2_rows = set(rng.sample(range(p_rows), BENEFICIARY2_ROWS))
    rows, seen = [], set()
    p_index = 0
    for seq in range(1, ROWS + 1):
        pa = unique_code(rng, pas, lambda: f"PF{rng.randint(0, 999999):06d}")
        time = f"{weighted(rng, HOURS):02d}:{rng.randint(0, 59):02d}"
        if seq in CANCEL_SEQ:
            age = weighted(rng, AGES)
            branch = rng.choice(codes)
            rows.append([seq, "C", "ยกเลิก", None, pa, -293, -293, branch, branches[branch], 0,
                         ymd(dt.date(start.year - age, rng.randint(1, 12), rng.randint(1, 28))), age, "",
                         beneficiary_name(rng), weighted(rng, RELATIONS1), "", "", weighted(rng, OCCUPATIONS), time])
            continue
        person = slots[p_index]
        ben1 = person["ben1"]
        if id(person) in seen and rng.random() < .15:  # some second policies name another beneficiary
            ben1 = (beneficiary_name(rng), weighted(rng, RELATIONS1))
        seen.add(id(person))
        ben2 = (beneficiary_name(rng), weighted(rng, RELATIONS2)) if p_index in ben2_rows else ("", "")
        rows.append([seq, "P", person["name"], person["id"], pa, ymd(start), ymd(end), person["branch"],
                     branches[person["branch"]], 180, ymd(person["born"]), person["age"], person["address"],
                     ben1[0], ben1[1], ben2[0], ben2[1], person["occupation"], time])
        p_index += 1
    return rows


# --- BIFF5 writer (Excel 5.0/95 — the format of the real file) --------------------------------------------------

def record(rid, body=b""):
    return struct.pack("<HH", rid, len(body)) + body


def text(value):
    return value.encode("cp874")  # what MTLS writes — while CODEPAGE says 1252


# workbook globals copied from the real file: formatting only (fonts Arial / Angsana New, number formats, XF)
GLOBALS = [
    (0x42, "e404"),  # CODEPAGE 1252 (the Thai text is cp874 anyway)
    (0x3D, "00000000bc25721538000000000001005802"),
    (0x22, "0000"),
    *[(0x31, "c8000000ff7f900100000000000005417269616c")] * 5,
    (0x31, "c80000000c00900100000100000005417269616c"),
    (0x31, "180100000800bc020000000000000b416e6773616e61204e6577"),
    (0x31, "18010000080090010000000000000b416e6773616e61204e6577"),
    (0x31, "18010000ff7f90010000000000000b416e6773616e61204e6577"),
    (0x31, "c8000000ff7fbc0200000000000005417269616c"),
    (0x41E, "a4000d30303030303030303030303030"),
    (0x41E, "a50008232c2323302e3030"),
    *[(0xE0, "00000000f5ff2040c020000000000000")] * 15,
    (0xE0, "0000000001002040c020000000000000"),
    (0xE0, "0600000001002048c020000000000000"),
    (0xE0, "0700000001001648c020000000000000"),
    (0xE0, "0800000001001148c020000000000000"),
    (0xE0, "0800000001001248c020000000000000"),
    (0xE0, "0900000001002248c020000000000000"),
    (0xE0, "0000a40001002244c020000000000000"),
    (0xE0, "0000a50001002044c020000000000000"),
    (0xE0, "0a00a5000100204cc020000000000000"),
    (0x293, "008000ff"),
]
# XF index per column as in the real file: header 17 · text 18 · type 19 · seq / age 20 · ID 21 · amount 22
HEADER_XF = 17
COLUMN_XF = [20, 19, 18, 21, 18, 18, 18, 18, 18, 22, 18, 20, 18, 18, 18, 18, 18, 18, 18]
NUMBER_COLUMNS = {0, 3, 5, 6, 7, 9, 10, 11}


def cell(row, col, value, xf):
    if value is None:
        return record(0x201, struct.pack("<HHH", row, col, xf))
    if col in NUMBER_COLUMNS and not isinstance(value, str):
        return record(0x203, struct.pack("<HHHd", row, col, xf, float(value)))
    data = text(value)
    return record(0x204, struct.pack("<HHHH", row, col, xf, len(data)) + data)


def sheet_stream(rows):
    table = [(HEADER, True)] + [(r, False) for r in rows]
    out = [record(0x809, bytes.fromhex("000510006c09c907")), record(0x55, struct.pack("<H", 8)),
           record(0x0000, struct.pack("<HHHHH", 0, len(table), 0, len(HEADER), 0))]
    for block in range(0, len(table), 32):
        chunk = table[block:block + 32]
        for i in range(len(chunk)):
            out.append(record(0x208, struct.pack("<HHHHHHHH", block + i, 0, len(HEADER), 0x14A, 0, 0, 0x1C0, 0x0F)))
        for i, (values, header) in enumerate(chunk):
            for col, value in enumerate(values):
                out.append(cell(block + i, col, value, HEADER_XF if header else COLUMN_XF[col]))
    out += [record(0x23E, bytes.fromhex("b6060000000000000000")), record(0x0A)]
    return b"".join(out)


def book_stream(rows):
    bof = record(0x809, bytes.fromhex("000505006c09c907"))
    globals_ = b"".join(record(rid, bytes.fromhex(body)) for rid, body in GLOBALS)
    sheet_name = b"Test"
    boundsheet_len = 4 + 4 + 2 + 1 + len(sheet_name)
    offset = len(bof) + len(globals_) + boundsheet_len + 4  # + EOF
    boundsheet = record(0x85, struct.pack("<IH", offset, 0) + bytes([len(sheet_name)]) + sheet_name)
    return bof + globals_ + boundsheet + record(0x0A) + sheet_stream(rows)


# --- OLE2 compound file with one stream "Book" -------------------------------------------------------------------

def compound_file(stream):
    sector = 512
    end, free, fat_mark = 0xFFFFFFFE, 0xFFFFFFFF, 0xFFFFFFFD
    data = stream + b"\0" * (-len(stream) % sector)
    n_stream = len(data) // sector
    n_fat = 1
    while n_fat * 128 < n_stream + 1 + n_fat:
        n_fat += 1
    assert n_fat <= 109
    dir_sector = n_stream
    fat = list(range(1, n_stream)) + [end, end] + [fat_mark] * n_fat
    fat += [free] * (n_fat * 128 - len(fat))

    def entry(name, kind, child, start, size):
        raw = name.encode("utf-16-le") + b"\0\0" if name else b""
        return (raw.ljust(64, b"\0") + struct.pack("<HBB", len(raw), kind, 1 if name else 0)
                + struct.pack("<III", free, free, child) + b"\0" * 16 + b"\0" * 4 + b"\0" * 16
                + struct.pack("<IIi", start, size, 0))

    directory = (entry("Root Entry", 5, 1, end, 0) + entry("Book", 2, free, 0, len(stream))
                 + entry("", 0, free, 0, 0) * 2)
    difat = list(range(n_stream + 1, n_stream + 1 + n_fat)) + [free] * (109 - n_fat)
    header = (bytes.fromhex("d0cf11e0a1b11ae1") + b"\0" * 16
              + struct.pack("<HHHHH", 0x3E, 3, 0xFFFE, 9, 6) + b"\0" * 6
              + struct.pack("<IIIIIIIII", 0, n_fat, dir_sector, 0, 4096, end, 0, end, 0)
              + struct.pack("<109I", *difat))
    return header + data + directory + struct.pack(f"<{len(fat)}I", *fat)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("output", type=Path)
    parser.add_argument("--start", type=dt.date.fromisoformat, default=dt.date(2026, 12, 1))
    parser.add_argument("--seed", type=int, default=20261201)
    args = parser.parse_args()
    rows = make_rows(args.start, args.seed)
    args.output.write_bytes(compound_file(book_stream(rows)))
    print(f"{args.output}: {len(rows)} rows")


if __name__ == "__main__":
    main()
