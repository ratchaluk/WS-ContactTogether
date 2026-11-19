# รายชื่อพนักงาน (`GET /ListOfValues/GetEmployee`) สำหรับ Frontend

ใช้ทำ drop-down **พนักงาน / เจ้าของเรื่อง** ได้ `id` กับชื่อเต็มภาษาไทยของพนักงานที่เปิดใช้งาน
ส่ง `id` ที่เลือกไปเป็น `serviceOwner` ของ `GET /Services/GetServices` ([GetServices.md](GetServices.md))

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetEmployee`), `Dtos/LookupItemResponse.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` |
| Request body / query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetEmployee` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetEmployee` |

## 2. Response (200)

array ของพนักงานที่ `IsEnable = 'T'` เรียงตามชื่อ แล้วนามสกุล (ตัวอย่างข้อมูลจริง ณ 19 พ.ย. 2026 มี 419 คน)

```json
[
  { "id": "5002EEF582684F6C9DEC3A3D06D21268", "nameTh": "Administrator" },
  { "id": "2943504DF75149A484B978B2DD2E7FB8", "nameTh": "banana za" }
]
```

| Field | ความหมาย |
|---|---|
| `id` | `TblEmployee.Id` (ตัวพิมพ์ใหญ่) |
| `nameTh` | คำนำหน้า ชื่อ นามสกุล ภาษาไทย ต่อกันให้แล้ว ข้ามส่วนที่ไม่มีค่า |

| HTTP | เมื่อไร |
|---|---|
| 200 | สำเร็จ (ไม่มีพนักงานให้ได้ `[]`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว |

## 3. ข้อควรรู้

- **ส่งเฉพาะ `id` กับชื่อ:** ไม่ส่งรหัสผ่าน ชื่อผู้ใช้ เบอร์ติดต่อ หรือข้อมูลส่วนตัวอื่นของพนักงาน
- **ไม่กรองวันหมดอายุ:** กรองเฉพาะ `IsEnable = 'T'` ตามที่ระบบเดิมใช้ ใน `DateExpire` ของพนักงานที่เปิดใช้งานอยู่มี 62 คนที่มีค่า (ข้อความวันที่แบบเก่า) แต่ไม่ได้ใช้ตัดออก
- **ชื่ออาจซ้ำกันได้:** ใช้ `id` เป็นค่าที่ส่ง ไม่ใช่ชื่อ
- **ไม่ต้องดึงทุกครั้ง:** เก็บไว้ใน memory ของแอประหว่างที่ใช้งานหน้านั้นได้

## 4. โค้ดตัวอย่าง (TypeScript)

```ts
export interface EmployeeItem { id: string; nameTh: string }

export async function fetchEmployees(token: string): Promise<EmployeeItem[]> {
  const res = await fetch("/api/backend/ListOfValues/GetEmployee", {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`โหลดรายชื่อพนักงานไม่สำเร็จ (${res.status})`);
  return res.json();
}
```
