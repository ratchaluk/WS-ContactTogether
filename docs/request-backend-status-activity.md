# ขอปรับ `POST /Services/AddNewService`: ให้ `statusId` รับสถานะชุด ACTIVITY ได้

ถึง: ทีม backend (ContactTogetherApi) · จาก: ทีม frontend (GCC 1111 — หน้าเปิดเรื่อง `/services/new`) · 17 พ.ย. 2569

---

## 1. สรุปที่ขอ

หน้าเปิดเรื่องอยากให้ช่อง "สถานะ" ใช้รายการจาก `GET /ListOfValues/GetStatusByStatusTypeActivity`
(เปิด / ปิด / ยกเลิก — `TblStatus.StatusType = 'ACTIVITY'`) แล้วส่ง id นั้นเป็น `statusId` ของ `POST /Services/AddNewService`

ตอนนี้ทำไม่ได้ เพราะ AddNewService ตรวจว่า `statusId` ต้องเป็นชุด `SERVICE` → ส่ง id ชุด ACTIVITY ได้ **400**

**ขอให้ทีม backend พิจารณาและตอบกลับว่าจะทำได้หรือไม่** (ข้อ 4)

---

## 2. สิ่งที่เกิดขึ้นตอนนี้

ส่ง id "เปิด" ของชุด ACTIVITY:

```http
POST /Services/AddNewService
Content-Type: application/json

{ "statusId": "0A5753389CDCD23F173268D8D46AC004", "categoryId": "…", "accountId": "…", "summary": "…" }
```

ได้:

```json
{
  "status": 400,
  "errors": { "StatusId": ["No SERVICE status with Id '0A5753389CDCD23F173268D8D46AC004'."] }
}
```

ตรงตามเอกสาร `GetStatusByStatusTypeActivity.md` ข้อ 4 และ `AddNewService.md` ข้อ 3.3 — เข้าใจว่า backend ตั้งใจกันไว้ ไม่ใช่บั๊ก

| ชุด | `GetStatus…` | รายการ (17 พ.ย. 2569) |
|---|---|---|
| SERVICE | `GetStatus?statusType=SERVICE` | เปิดเรื่อง (อยู่ระหว่างดำเนินการ) / ปิดเรื่อง (Extenally Transfered, Internally Closed) / ยกเลิก (ทำซ้ำ, โทรผิด, … 8 ตัว) |
| ACTIVITY | `GetStatusByStatusTypeActivity` | เปิด `0A5753389CDCD23F173268D8D46AC004` / ปิด `CB0A973609E8D684F2944D92ED4126A1` / ยกเลิก `CD6469896CF379C64A2FB75263FD3362` |

---

## 3. ทำไมฝั่งผู้ใช้อยากได้

- ผู้ใช้ต้องการให้เจ้าหน้าที่เห็นแค่ 3 ตัวเลือก **เปิด / ปิด / ยกเลิก** ตอนเปิดเรื่อง ไม่ต้องเลือกสถานะย่อย
- ชุด SERVICE มีสถานะย่อยหลายตัว และกลุ่ม "ปิดเรื่อง" ไม่มีตัว `IsDefault = 'T'`
  → ตอนนี้ frontend ต้อง map เอง: เปิดเรื่อง → อยู่ระหว่างดำเนินการ, ปิดเรื่อง → Internally Closed (กำหนดในโค้ด), ยกเลิก → ทำซ้ำ (`IsDefault`)

---

## 4. คำถาม / สิ่งที่ขอให้ตัดสินใจ

1. **AddNewService รับ `statusId` ชุด ACTIVITY ได้ไหม?**
   ถ้าได้ ขอให้ปรับการตรวจ `StatusId` ให้รับทั้ง SERVICE และ ACTIVITY แล้วแจ้ง frontend
2. ถ้ารับได้ — **ส่วนที่อ่าน `TblService.StatusId` ต้องปรับตามไหม?** ที่ frontend ใช้อยู่และน่าจะได้รับผลกระทบ:
   - `POST /Report/GetReport01–07` และ `ExportReport01–07` (คอลัมน์ / ตัวกรองสถานะ เช่น `srStatusName`)
   - `GET /Services/GetServices` (`servicesStatus`, ตัวกรอง `ServicesStatus`)
   - `GET /Services/GetInProcessServices`, `GetPendingServices` (`statusNameTh`)
   - เรื่องเก่าทั้งหมดในระบบใช้สถานะชุด SERVICE — เรื่องใหม่ที่เป็นชุด ACTIVITY จะนับรวมกับของเก่าในรายงานได้ถูกไหม
3. ถ้า**ไม่รับ** (คงเดิม) — แนะนำทางไหน:
   - (ก) frontend map เองต่อไปแบบข้อ 3 (ทำอยู่แล้ว ใช้ได้) — ขอยืนยันว่า ปิดเรื่อง → **Internally Closed** ถูกต้องตามธุรกิจ
   - (ข) ตั้ง `IsDefault = 'T'` ให้สถานะย่อย 1 ตัวของกลุ่ม "ปิดเรื่อง" ใน DB แล้ว frontend จะเลิก hard-code
   - (ค) ให้ AddNewService รับ id **สถานะหลัก** ของชุด SERVICE (เปิดเรื่อง / ปิดเรื่อง / ยกเลิก) แล้ว backend เลือกสถานะย่อยให้เอง
4. ยกเลิก → **ทำซ้ำ** (`isType = 'OPEN'` อยู่ใต้กลุ่มยกเลิก) เป็นค่าที่ตั้งใจไหม หรือควรเป็นตัวอื่น

---

## 5. เรื่องที่เกี่ยวข้อง (ถ้าตอบมาพร้อมกันได้จะดีมาก)

- **`dateClosed`**: ถ้าเลือกสถานะปิด / ยกเลิกตอนเปิดเรื่อง frontend ควรส่ง `dateClosed` เองไหม หรือ backend จะตั้งให้ตาม `isType`
  (ตอนนี้ยังไม่ส่ง → `GetInProcessServices` จะยังนับเป็นเรื่องที่ยังไม่ปิด)
- **`GetStatus` ไม่ส่ง `statusType` แล้วได้ 200** (คืนชุด SERVICE) แต่เอกสารบอกว่าบังคับ / ต้องได้ 400 — ตัวไหนถูก
- **`GetStatus` ตอบเป็นรายการแบน** (`refId` ชี้แม่ ไม่มี `children`) ไม่ตรง `GetStatus.md` ข้อ 2 ที่บอกเป็นต้นไม้ — frontend รับได้ทั้งสองแบบแล้ว แต่ขอให้เอกสารตรงกับของจริง
- **`GetOrganization` ได้ 404** บน backend ที่รันในเครื่อง dev — น่าจะยังไม่ได้ deploy build ใหม่

---

## 6. ฝั่ง frontend ตอนนี้

ใช้แบบข้อ 4.3 (ก): ช่องสถานะเห็น 3 ตัว แล้วส่ง id ชุด SERVICE ที่ map ไว้ — **บันทึกได้ปกติ** ไม่ได้ติดขัด
ถ้า backend ตัดสินใจทางไหน frontend ปรับตามได้ทันที (รายการสถานะดึงจาก API อยู่แล้ว)
