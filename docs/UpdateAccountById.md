# แก้ไขผู้ติดต่อ (`PUT /Account/UpdateAccountById`) สำหรับ Frontend

ใช้บันทึกฟอร์ม **แก้ไขข้อมูลผู้ติดต่อ (account)**: ชื่อ เพศ วันเกิด ที่อยู่ รหัสไปรษณีย์ หมายเหตุ และแฟล็ก
**ไม่แก้เบอร์โทรและอีเมล** เพราะเก็บแยกไว้ใน `TblAccountDetail` endpoint นี้ไม่แตะเลย ใช้ [AccountDetail.md](AccountDetail.md) แทน

> อ้างอิงโค้ด: `Controllers/AccountController.cs` (`UpdateAccountById`), `Dtos/UpdateAccountRequest.cs`
> (field เดียวกับ `Dtos/CreateAccountRequest.cs`), `Helper/AccountQueries.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `PUT` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `id` (บังคับ) = `TblAccount.Id` ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| Body | JSON (`Content-Type: application/json`) ดูข้อ 2 |
| Backend (dev) | `http://localhost:5018/Account/UpdateAccountById?id=…` |
| URL จาก Next.js | `/api/backend/Account/UpdateAccountById?id=…` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

**ลำดับที่แนะนำ**

1. เปิดฟอร์มด้วย `GET /Account/GetAccountById?id=…` (หรือใช้ `account` จาก `GET /Services/GetServiceById`)
2. ให้ผู้ใช้แก้ไข
3. ส่ง **ทุก field** กลับมาด้วย `PUT` แล้วใช้ response แสดงผลต่อได้เลย

---

## 2. Request body

### 2.1 ส่งครบทุก field (แทนที่ทั้งก้อน)

เป็น `PUT` แบบ **แทนที่ทั้งก้อน**:

- **field ที่ไม่ส่งมา จะถูกบันทึกเป็น `null`:** ยกเว้น `isScret` / `isEnable` ที่จะกลับเป็นค่าเริ่มต้น `"F"` / `"T"`
- **ส่งแค่ field ที่แก้ไม่ได้:** ข้อมูลส่วนอื่นจะหาย
- **วิธีที่ปลอดภัย:** เอา object จาก `GetAccountById` มาแก้ แล้วส่งกลับทั้งก้อน

| Field | Type | บังคับ | ยาวสุด | ความหมาย |
|---|---|---|---|---|
| `firstnameTh` | string | **ใช่** | 200 | ชื่อภาษาไทย |
| `salutationTh` / `lastnameTh` | string \| null | | 100 / 200 | คำนำหน้า / นามสกุล ภาษาไทย |
| `salutationEn` / `firstnameEn` / `lastnameEn` | string \| null | | 100 / 200 / 200 | ชื่อภาษาอังกฤษ |
| `genderId` | string \| null | | 50 | `id` จาก `GET /ListOfValues/GetGender` ส่ง id ที่ไม่มีอยู่ = 400 |
| `birthdate` | string \| null | | 50 | **ข้อความแบบเดิม เดือนขึ้นก่อน** เช่น `5/14/1988 0:00` (ระบบเดิมอ่านรูปแบบนี้) |
| `address` | string \| null | | | ที่อยู่ ข้อความอิสระ |
| `areaId` | string \| null | | 50 | `id` จาก `GET /ListOfValues/GetArea` หรือค่าเดิมที่ได้จาก GET หรือ `null` / `""` ดูข้อ 4 |
| `zipcode` | string \| null | | 100 | รหัสไปรษณีย์ |
| `remark` | string \| null | | | หมายเหตุ |
| `isScret` | `"T"` \| `"F"` | | 10 | แฟล็กปกปิดข้อมูล (สะกดตาม DB) ไม่ส่ง = `"F"` |
| `isEnable` | `"T"` \| `"F"` | | 10 | `"F"` = ปิดใช้งานผู้ติดต่อ (จะหายจาก `GetAccounts` / `GetAccountByPhoneNo`) ไม่ส่ง = `"T"` |

**field ที่ส่งมาแล้วไม่มีผล:** `id`, `fullName`, `gender`, `created`, `contacts` ส่งติดมาได้ (เช่น ส่ง object จาก GET ทั้งก้อน) backend จะไม่สนใจ

**field ที่ backend ตั้งให้เอง:**

- **`Updated` / `UpdatedBy`:** ได้จากเวลาปัจจุบันและพนักงานใน token
- **`Created` / `CreatedBy`:** ไม่เปลี่ยน

### 2.2 ตัวอย่าง

```http
PUT /Account/UpdateAccountById?id=4D7B3F5E81104F349449B50571E72BCE
Content-Type: application/json
Authorization: Bearer <token>

{
  "salutationTh": "นาย",
  "firstnameTh": "อินดี้",
  "lastnameTh": "ทดสอบจ้า",
  "salutationEn": "",
  "firstnameEn": "",
  "lastnameEn": "",
  "genderId": "19C49E66B6F9EF1BCE11A3B93DED12C4",
  "birthdate": "",
  "address": "99/1 ถนนแจ้งวัฒนะ",
  "areaId": "",
  "zipcode": "10210",
  "remark": "",
  "isScret": "F",
  "isEnable": "T"
}
```

---

## 3. Response

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | บันทึกสำเร็จ | ผู้ติดต่อหลังแก้ **รูปแบบเดียวกับ `GetAccountById`** (มี `fullName`, `gender`, `contacts`) |
| 400 | ไม่ส่ง `id`, ไม่มี `firstnameTh`, ข้อความยาวเกิน, `genderId` / `areaId` ไม่มีอยู่, JSON ผิดรูป | ProblemDetails (`errors.<field>`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีผู้ติดต่อ `id` นี้ | `{ "statusCode": 404, "message": "ไม่พบข้อมูลผู้ติดต่อ" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

ตัวอย่าง 400 (ข้อความเป็นภาษาอังกฤษ ไม่ควรแสดงให้ผู้ใช้ตรง ๆ ให้ map จากชื่อ field):

```json
{
  "status": 400,
  "errors": {
    "FirstnameTh": ["The FirstnameTh field is required."],
    "GenderId": ["No TblGender row with Id 'NOPE'."]
  }
}
```

---

## 4. ข้อควรรู้ (ณ 18 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **เบอร์โทร / อีเมลแก้ที่อื่น** | ส่ง `contacts` มาก็ไม่มีผล ใช้ `AddAccountDetail` / `UpdateAccountDetail` / `DeleteAccountDetail` ([AccountDetail.md](AccountDetail.md)) |
| **`areaId`** | เลือกใหม่จาก `GetArea` ได้ ([AreaId.md](AreaId.md)) ส่งค่าเดิมกลับมาได้เสมอ แม้ค่าเดิมจะไม่อยู่ในตารางพื้นที่ (ข้อมูลเก่า 2 คน) ส่ง id อื่นที่ไม่มีอยู่ = 400 |
| **แถวซ้ำในข้อมูลเดิม** | ถ้า `id` นี้มีหลายแถวในระบบเดิม ทุกแถวถูกแก้พร้อมกัน frontend ไม่ต้องทำอะไร |
| **ปิดใช้งาน = ส่ง `isEnable: "F"`** | ยังไม่มี endpoint ลบผู้ติดต่อ ผู้ติดต่อที่ปิดแล้วยังเปิดดูได้ด้วย `GetAccountById` และยังแสดงในเรื่องเก่า |
| **ไม่มีตรวจการแก้ชนกัน** | ถ้าสองคนแก้คนเดียวกันพร้อมกัน คนที่บันทึกทีหลังจะทับของคนแรก |
| **ข้อความไม่ถูกตัดช่องว่าง** | ตัดช่องว่างหน้า/หลังก่อนส่ง |

---

## 5. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/account.ts  (AccountSearchResult, AccountApiError มีอยู่แล้วจาก GetAccounts.md)
export interface UpdateAccountBody {
  salutationTh: string | null; firstnameTh: string; lastnameTh: string | null;
  salutationEn: string | null; firstnameEn: string | null; lastnameEn: string | null;
  genderId: string | null; birthdate: string | null;
  address: string | null; areaId: string | null; zipcode: string | null;
  remark: string | null; isScret: string; isEnable: string;
}

/** เอาข้อมูลจาก GetAccountById มาเป็นค่าเริ่มต้นของฟอร์ม ให้ทุก field ครบ */
export function toUpdateBody(a: AccountSearchResult): UpdateAccountBody {
  return {
    salutationTh: a.salutationTh, firstnameTh: a.firstnameTh ?? "", lastnameTh: a.lastnameTh,
    salutationEn: a.salutationEn, firstnameEn: a.firstnameEn, lastnameEn: a.lastnameEn,
    genderId: a.genderId, birthdate: a.birthdate,
    address: a.address, areaId: a.areaId, zipcode: a.zipcode,
    remark: a.remark, isScret: a.isScret ?? "F", isEnable: a.isEnable ?? "T",
  };
}

export async function updateAccount(
  id: string,
  body: UpdateAccountBody,
  opts: { token: string; signal?: AbortSignal },
): Promise<AccountSearchResult> {
  const res = await fetch(`/api/backend/Account/UpdateAccountById?id=${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { Authorization: `Bearer ${opts.token}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 400) throw new AccountApiError(400, "ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบ");
  if (res.status === 401) throw new AccountApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 404) throw new AccountApiError(404, "ไม่พบข้อมูลผู้ติดต่อ");
  throw new AccountApiError(res.status, "บันทึกข้อมูลผู้ติดต่อไม่สำเร็จ");
}
```

> `toUpdateBody` ส่ง `isEnable` เดิมกลับไป ผู้ติดต่อที่ปิดใช้งานอยู่จึงไม่ถูกเปิดกลับโดยไม่ตั้งใจ
> ถ้าฟอร์มมีช่อง "ปิดใช้งาน" ให้ส่งค่าที่ผู้ใช้เลือกแทน
