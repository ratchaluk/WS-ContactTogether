# ผู้รับ (`GET /ListOfValues/GetEmployeeByGroup`) สำหรับ Frontend

ใช้ทำ drop-down **ผู้รับ** ในฟอร์มเพิ่มงาน ได้พนักงานที่เปิดใช้งานใน**กลุ่มที่เลือก**
(กลุ่มได้จาก `GET /ListOfValues/GetEmployeeGroup` ([GetEmployeeGroup.md](GetEmployeeGroup.md)))
ส่ง `id` ที่เลือกไปเป็น `ownerId` ของ `POST /Services/AddNewServiceOperations` ([AddNewServiceOperations.md](AddNewServiceOperations.md))

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetEmployeeByGroup`), `Dtos/LookupItemResponse.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` |
| Query | `groupId` (**ต้องส่ง**, ตัวเลข) = `id` ของกลุ่มจาก `GetEmployeeGroup` |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetEmployeeByGroup?groupId=5` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetEmployeeByGroup?groupId=5` |

## 2. Response (200)

array ของพนักงานที่ `IsEnable = 'T'` และอยู่ในกลุ่มนั้น (`TblEmployeeGroup`) เรียงตามชื่อ แล้วนามสกุล
รูปแบบเดียวกับ `GetEmployee` ([GetEmployee.md](GetEmployee.md)) ตัวอย่างข้อมูลจริง ณ 25 พ.ย. 2026:
กลุ่ม 5 (การคลัง… - Agent.) มี 34 คน

```json
[
  { "id": "6CA34F3D2F1B4F4E9607314BB3D64FCC", "nameTh": "กมล พิษณุประชา" },
  { "id": "A7D5DC3EBE374C3A89807A1C73C32DEF", "nameTh": "กรรณิการ์ ศรีเล็กดี" },
  { "id": "1D591AD1C60849BF981CB3D5632B7344", "nameTh": "จิตรมณี โพธิ์นาง" }
]
```

| Field | ความหมาย |
|---|---|
| `id` | `TblEmployee.Id` ส่งเป็น `ownerId` |
| `nameTh` | คำนำหน้า ชื่อ นามสกุล ภาษาไทย ต่อกันให้แล้ว ข้ามส่วนที่ไม่มีค่า |

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ รวมกรณีกลุ่มไม่มีสมาชิก กลุ่มถูกปิด หรือไม่มีกลุ่มนี้ (ได้ `[]`) | array |
| 400 | ไม่ส่ง `groupId` หรือไม่ใช่ตัวเลข | ProblemDetails (`errors.groupId`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |

## 3. ข้อควรรู้

- **เฉพาะสมาชิกของกลุ่มนั้นโดยตรง:** ไม่รวมสมาชิกของกลุ่มย่อย กลุ่มหลักที่มีกลุ่มย่อย (เช่น การคลัง… id 45)
  มักได้ `[]` ให้ผู้ใช้เลือกกลุ่มย่อยแทน (ดู [GetEmployeeGroup.md](GetEmployeeGroup.md) ข้อ 2.2)
- **โหลดใหม่ทุกครั้งที่เปลี่ยนกลุ่ม** และล้างค่าผู้รับที่เลือกไว้
- **ชื่ออาจซ้ำกันได้:** ใช้ `id` เป็นค่าที่ส่ง ไม่ใช่ชื่อ
- **ส่งเฉพาะ `id` กับชื่อ:** ไม่ส่งรหัสผ่านหรือข้อมูลส่วนตัวอื่นของพนักงาน
- **มอบงานให้ตัวเอง:** ถ้าผู้รับคือผู้ที่ login อยู่ งานจะเป็น "รับแล้ว" ทันที (ดู `isReceived` ใน
  [AddNewServiceOperations.md](AddNewServiceOperations.md))

## 4. โค้ดตัวอย่าง (TypeScript)

```ts
export interface EmployeeItem { id: string; nameTh: string }

export async function fetchEmployeesByGroup(token: string, groupId: string): Promise<EmployeeItem[]> {
  const res = await fetch(
    `/api/backend/ListOfValues/GetEmployeeByGroup?groupId=${encodeURIComponent(groupId)}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok) throw new Error(`โหลดรายชื่อผู้รับไม่สำเร็จ (${res.status})`);
  return res.json();
}
```

```tsx
// โหลดผู้รับเมื่อเลือกกลุ่ม
useEffect(() => {
  if (!groupId) { setReceivers([]); return; }
  fetchEmployeesByGroup(token, groupId).then(setReceivers);
}, [groupId]);

<select value={ownerId} disabled={!groupId} onChange={(e) => setOwnerId(e.target.value)}>
  <option value="">-- เลือกผู้รับ --</option>
  {receivers.map((r) => <option key={r.id} value={r.id}>{r.nameTh}</option>)}
</select>
```
