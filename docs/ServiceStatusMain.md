# สถานะของเรื่อง: ใช้ 3 ตัว เปิดเรื่อง / ปิดเรื่อง / ยกเลิก (สำหรับ Frontend)

ตอบ [request-backend-status-activity.md](request-backend-status-activity.md) · 17 พ.ย. 2569

---

## 1. สรุป

- **ไม่ใช้สถานะชุด ACTIVITY กับเรื่อง:** `AddNewService` ยังรับ `statusId` เฉพาะชุด `SERVICE` เหมือนเดิม (เหตุผลดูข้อ 5)
- **ให้หน้าเปิดเรื่องใช้สถานะหลัก 3 ตัวของชุด SERVICE แทน:** ส่ง id ของตัวหลักเป็น `statusId` ได้เลย
- **backend ไม่ต้องแก้ ใช้ได้ทันที:** frontend เลิก map ไปเป็นสถานะย่อย (Internally Closed / ทำซ้ำ) ได้

---

## 2. สถานะหลัก 3 ตัว

| แสดงในหน้าจอ | `id` (ส่งเป็น `statusId`) | `isType` | `isDefault` |
|---|---|---|---|
| เปิดเรื่อง | `60CD725EA4455BA6770FC94BC78F466F` | `OPEN` | `T` |
| ปิดเรื่อง | `67F7D634C1D74FDE512C164D6A2A1EF9` | `CLOSE` | `F` |
| ยกเลิก | `1B751556C1458196BA0EB37037415A25` | `CANCEL` | `F` |

id ข้างบนใช้อ้างอิงเท่านั้น **อย่า hard-code** ให้ดึงจาก API ตามข้อ 3

---

## 3. วิธีดึงรายการ

เรียก `GET /ListOfValues/GetStatus` (ไม่ส่ง `statusType` = ชุด SERVICE) แล้ว **กรองเฉพาะแถวที่ `refId === id`**
(สถานะหลักชี้ `refId` ที่ตัวเอง ส่วนสถานะย่อยชี้ไปที่ตัวหลัก)

```ts
const all = await fetchStatuses({ token });            // GET /ListOfValues/GetStatus
const mainStatuses = all.filter((s) => s.refId === s.id); // เหลือ เปิดเรื่อง / ปิดเรื่อง / ยกเลิก
const defaultStatus = mainStatuses.find((s) => s.isDefault === "T"); // เปิดเรื่อง
```

```tsx
<select value={statusId} onChange={(e) => setStatusId(e.target.value)}>
  {mainStatuses.map((s) => <option key={s.id} value={s.id}>{s.nameTh}</option>)}
</select>
```

ถ้าต้องรู้ว่าเลือกปิดหรือยกเลิก ให้ดูจาก `isType` (`OPEN` / `CLOSE` / `CANCEL`) ไม่ใช่จากชื่อ

---

## 4. ส่งเข้า `POST /Services/AddNewService`

```json
{
  "statusId": "67F7D634C1D74FDE512C164D6A2A1EF9",
  "dateClosed": "2026-11-17T14:30:00",
  "categoryId": "…",
  "accountId": "…",
  "summary": "…"
}
```

- **`dateClosed`:** ถ้าเลือกสถานะที่ `isType` เป็น `CLOSE` หรือ `CANCEL` ให้ **ส่ง `dateClosed` เอง** (เวลาไทย `yyyy-MM-ddTHH:mm:ss`) เพราะ backend ไม่ได้ตั้งให้อัตโนมัติ ถ้าไม่ส่ง เรื่องนั้นจะยังขึ้นใน `GetInProcessServices` เหมือนเรื่องที่ยังไม่ปิด
- **เปิดเรื่อง:** ไม่ต้องส่ง `dateClosed`

---

## 5. ทำไมไม่ใช้สถานะชุด ACTIVITY (ตอบข้อ 4.1–4.2)

ถ้าเก็บ id ชุด ACTIVITY ลง `TblService.StatusId` จะกระทบดังนี้:

| จุด | ผลกระทบ |
|---|---|
| **รายงาน 01** | ตัดเรื่องที่ยกเลิกออกด้วยเงื่อนไข "สถานะอยู่ในกลุ่มยกเลิกของชุด SERVICE" ทำให้ "ยกเลิก" ของชุด ACTIVITY ไม่ถูกตัด เรื่องที่ยกเลิกจึงถูกนับเข้ารายงานผิด |
| **ข้อมูลปนกันสองชุด** | เรื่องเก่าทั้งหมดใช้ชุด SERVICE เวลาค้นหรือนับตามสถานะจะต้องรวมสองชุดเอง |
| **ระบบ GCC เดิม** | ถ้ายังอ่าน DB นี้อยู่ จะเจอเรื่องที่ใช้สถานะของงาน |

ถ้าใช้สถานะหลักของชุด SERVICE แทน ทุกจุดข้างบนไม่มีปัญหา:
- **รายงาน 01:** "ยกเลิก" ตัวหลักอยู่ในกลุ่มยกเลิก จึงถูกตัดออกถูกต้อง
- **รายงานอื่น:** แสดงชื่อสถานะเป็น "เปิดเรื่อง" / "ปิดเรื่อง" / "ยกเลิก"

---

## 6. ข้อควรรู้

| เรื่อง | ผลกับ frontend |
|---|---|
| **เรื่องเก่าใช้สถานะย่อย** | เรื่องเก่าเก็บเป็น "อยู่ระหว่างดำเนินการ" (101 เรื่อง) และ "Internally Closed" (83 เรื่อง) ถ้าหน้าค้นหา (`SearchService`) ส่ง `ServicesStatus` เป็น id ของ "ปิดเรื่อง" จะเจอเฉพาะเรื่องใหม่ ไม่เจอเรื่องเก่าที่เป็นสถานะย่อย ถ้าต้องการให้ค้นแบบรวมทั้งกลุ่ม แจ้ง backend |
| **ดรอปดาวน์หน้าค้นหา** | ถ้าจะค้นเรื่องเก่าได้ด้วย ให้แสดงรายการ `GetStatus` เต็ม (รวมสถานะย่อย) ในหน้าค้นหา ส่วนหน้าเปิดเรื่องใช้ 3 ตัว |
| **ห้ามปิดสถานะย่อยใน DB** | ไม่ต้องตั้ง `IsEnable = 'F'` ให้สถานะย่อย เพราะเรื่องเก่ายังอ้างอยู่ ให้ frontend กรองเหลือ 3 ตัวเอง |
| **สถานะชุด ACTIVITY** | `GetStatusByStatusTypeActivity` ยังมีไว้ใช้กับงาน (activity) เท่านั้น ส่งเป็น `statusId` ของ `AddNewService` = 400 |

---

## 7. ตอบเรื่องที่เกี่ยวข้อง (ข้อ 5 ของคำขอ)

| ที่ถาม | คำตอบ |
|---|---|
| `dateClosed` ใครตั้ง | frontend ส่งเองเมื่อ `isType` เป็น `CLOSE` / `CANCEL` (ข้อ 4) |
| ข้อความ error `No SERVICE status with Id …` | มาจาก backend เวอร์ชันเก่า เวอร์ชันปัจจุบันตอบ `No row with Id '…'` ([AddNewService.md](AddNewService.md)) |
| `GetStatus` ไม่ส่ง `statusType` แล้วได้ 200 | ถูกต้อง `statusType` ไม่บังคับ ไม่ส่ง = SERVICE ส่งค่าที่ไม่ใช่ `SERVICE` / `ACTIVITY` = 400 ([GetStatus.md](GetStatus.md) ฉบับปัจจุบัน) |
| `GetStatus` เป็นรายการแบน ไม่มี `children` | ถูกต้อง เอกสาร [GetStatus.md](GetStatus.md) ฉบับปัจจุบันเขียนเป็นรายการแบนแล้ว |
| `GetOrganization` ได้ 404 | backend ในเครื่อง dev ยังเป็น build เก่า ต้อง restart แอปก่อน ([GetOrganization.md](GetOrganization.md)) |

## 8. ยังรอคำตอบจากฝ่ายธุรกิจ / ทีม DB

- **"ทำซ้ำ" ดูผิดปกติ:** อยู่ใต้กลุ่มยกเลิก แต่ `isType = OPEN`, `isDefault = T` และ `nameEn = "In Process"` น่าจะเป็นข้อมูลผิด เมื่อใช้ 3 ตัวหลักแล้ว frontend ไม่ต้องใช้ตัวนี้
- **ระบบ GCC เดิม:** ยังใช้ DB นี้อยู่ไหม และมีหน้าจอที่ต้องการสถานะย่อยไหม
