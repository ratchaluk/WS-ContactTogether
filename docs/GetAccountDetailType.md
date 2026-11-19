# ประเภทการติดต่อของผู้ติดต่อ (`GET /ListOfValues/GetAccountDetailType`) สำหรับ Frontend

ใช้ทำ drop-down **ประเภทเบอร์/อีเมล** ของผู้ติดต่อ (มือถือ, โทรศัพท์บ้าน, ที่ทำงาน, โทรสาร, อีเมล)
ส่ง `id` ที่เลือกกลับไปเป็น `detailType` ของแต่ละรายการใน `contacts` ของ `POST /Account/AddNewAccount`

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetAccountDetailType`), `Helper/AccountDetailTypes.cs`

> **ไม่ได้มาจาก DB:** ไม่มีตารางประเภทนี้ เป็นรายการคงที่ 5 ค่าที่กำหนดในโค้ด และ backend ใช้ชุดเดียวกันตรวจ `detailType`
> ตอนบันทึก ค่าอื่นได้ 400

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` |
| Request body / query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetAccountDetailType` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetAccountDetailType` |

## 2. Response (200)

array เรียงตามที่ควรแสดง ค่าแรก (`MOBILE`) คือค่าเริ่มต้นเมื่อไม่ส่ง `detailType`

```json
[
  { "id": "MOBILE", "nameTh": "มือถือ" },
  { "id": "HOME", "nameTh": "โทรศัพท์บ้าน" },
  { "id": "OFFICE", "nameTh": "ที่ทำงาน" },
  { "id": "FAX", "nameTh": "โทรสาร" },
  { "id": "MAIL", "nameTh": "อีเมล" }
]
```

| HTTP | เมื่อไร |
|---|---|
| 200 | สำเร็จ |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว |

## 3. ข้อควรรู้

- **ค่า `id` เป็นตัวพิมพ์ใหญ่:** ตรงกับ `detailType` ใน `contacts` ของ `GetAccounts`, `GetAccountById`, `GetServiceById`
  ใช้ทำป้ายชื่อแทน `DETAIL_TYPE_LABELS` ได้
- **ไม่ต้องดึงทุกครั้ง:** รายการไม่เปลี่ยน เก็บไว้ใน memory ของแอปได้
- **เลือก `MAIL`:** ช่อง `detail` ของรายการนั้นจะเป็นอีเมล
- **`GetAccountByPhoneNo`:** ค้นเฉพาะ `MOBILE` / `HOME` / `OFFICE` / `FAX` ไม่รวม `MAIL`

## 4. โค้ดตัวอย่าง (TypeScript)

```ts
export interface AccountDetailType { id: string; nameTh: string }

export async function fetchAccountDetailTypes(token: string): Promise<AccountDetailType[]> {
  const res = await fetch("/api/backend/ListOfValues/GetAccountDetailType", {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`โหลดประเภทการติดต่อไม่สำเร็จ (${res.status})`);
  return res.json();
}
```
