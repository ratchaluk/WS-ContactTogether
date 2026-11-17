# Activity Status API (`GET /ListOfValues/GetStatusByStatusTypeActivity`) สำหรับ Frontend

ใช้ดึงรายการ **สถานะของงาน** (`TblStatus` ที่ `StatusType = "ACTIVITY"`) ไปทำ drop-down สถานะของงาน/บันทึกการทำงาน (activity)

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetStatusByStatusTypeActivity`), `Dtos/StatusResponse.cs`
>
> ได้ผลเหมือน `GET /ListOfValues/GetStatus?statusType=ACTIVITY` ส่วน **สถานะของเรื่อง** (SERVICE) ให้ใช้ `GetStatus` (ดู [GetStatus.md](GetStatus.md))
> **อย่าเอา id จาก endpoint นี้ไปส่งเป็น `statusId` ของ `POST /Services/AddNewService`** เพราะจะได้ 400

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetStatusByStatusTypeActivity` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetStatusByStatusTypeActivity` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ของสถานะ ACTIVITY ที่เปิดใช้งาน (`IsEnable = "T"`) **เรียงตามชื่อ (`nameTh`)**

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblStatus.Id` ใช้เป็น `value` ของ option |
| `refId` | string | สถานะแม่ ตอนนี้ทุกตัวของ ACTIVITY ชี้ที่ตัวเอง (เป็นสถานะหลักทั้งหมด ไม่มีสถานะย่อย) |
| `nameTh` | string | ชื่อที่ใช้แสดงผล |
| `nameEn` | string \| null | ชื่อภาษาอังกฤษ |
| `isType` | string | กลุ่มของสถานะ: `OPEN` / `CLOSE` / `CANCEL` ใช้ตัดสินใจ logic ได้ (เช่น ปิดงานแล้วหรือยัง) |
| `isDefault` | string | `"T"` = ค่าเริ่มต้น, `"F"` = ไม่ใช่ |

### 2.2 ตัวอย่าง (ข้อมูลจริง ณ 17 พ.ย. 2026 มี 3 รายการ)

```json
[
  { "id": "CB0A973609E8D684F2944D92ED4126A1", "refId": "CB0A973609E8D684F2944D92ED4126A1", "nameTh": "ปิด",    "nameEn": "Closed", "isType": "CLOSE",  "isDefault": "T" },
  { "id": "0A5753389CDCD23F173268D8D46AC004", "refId": "0A5753389CDCD23F173268D8D46AC004", "nameTh": "เปิด",   "nameEn": "Open",   "isType": "OPEN",   "isDefault": "F" },
  { "id": "CD6469896CF379C64A2FB75263FD3362", "refId": "CD6469896CF379C64A2FB75263FD3362", "nameTh": "ยกเลิก", "nameEn": "Cancel", "isType": "CANCEL", "isDefault": "F" }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ ถ้าต้องหาสถานะ "เปิด"/"ปิด" ให้หาจาก `isType`

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `StatusItem[]` |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/status.ts
export interface StatusItem {
  id: string;
  refId: string;
  nameTh: string;
  nameEn: string | null;
  isType: "OPEN" | "CLOSE" | "CANCEL" | string;
  isDefault: "T" | "F" | string;
}

export async function fetchActivityStatuses(opts: { token: string; signal?: AbortSignal }): Promise<StatusItem[]> {
  const res = await fetch("/api/backend/ListOfValues/GetStatusByStatusTypeActivity", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("โหลดรายการสถานะงานไม่สำเร็จ");
}
```

```tsx
<select value={statusId} onChange={(e) => setStatusId(e.target.value)}>
  <option value="">-- เลือกสถานะ --</option>
  {statuses.map((s) => <option key={s.id} value={s.id}>{s.nameTh}</option>)}
</select>
```

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้

---

## 4. ข้อควรรู้ (สถานะ ณ 17 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ใช้กับงาน (activity) เท่านั้น** | ส่ง id เหล่านี้เป็น `statusId` ของ `AddNewService` จะได้ 400 (`StatusId: No row with Id '…'`) |
| **ค่าเริ่มต้นคือ "ปิด"** | `isDefault = "T"` อยู่ที่ "ปิด" ใน DB ถ้าฟอร์มสร้างงานใหม่ควรเริ่มที่ "เปิด" ให้เลือกจาก `isType === "OPEN"` แทน (ยืนยันกับทีมอีกครั้ง) |
| **ยังไม่มี endpoint บันทึกงาน** | ตอนนี้ API ยังไม่มี endpoint สร้าง/แก้ `TblActivity` รายการนี้ใช้แสดงผลหรือเตรียมฟอร์มไว้ก่อน |

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetStatusByStatusTypeActivity
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetStatusByStatusTypeActivity -H "Authorization: Bearer <token>"
```
