# เพิ่ม/แก้/ลบ เบอร์และอีเมลของผู้ติดต่อ (`AddAccountDetail` / `UpdateAccountDetail` / `DeleteAccountDetail`) สำหรับ Frontend

เบอร์โทรและอีเมลของผู้ติดต่อเก็บแยกเป็นรายการ (`TblAccountDetail`) ผู้ติดต่อ 1 คนมีได้หลายรายการ
ใช้ 3 endpoint นี้จัดการทีละรายการ **หลังจากสร้างผู้ติดต่อแล้ว** (ตอนสร้างใช้ `contacts[]` ของ `AddNewAccount`)

> อ้างอิงโค้ด: `Controllers/AccountController.cs` (`AddAccountDetail`, `UpdateAccountDetail`, `DeleteAccountDetail`),
> `Dtos/AccountContactRequest.cs`, `Dtos/AccountContactResponse.cs`

---

## 1. ภาพรวม

| Endpoint | ทำอะไร | query |
|---|---|---|
| `POST /Account/AddAccountDetail` | เพิ่ม 1 รายการ | `accountId` = `TblAccount.Id` |
| `PUT /Account/UpdateAccountDetail` | แก้ชนิดและค่าของ 1 รายการ | `id` = `contacts[].id` |
| `DELETE /Account/DeleteAccountDetail` | ลบ 1 รายการ (ปิดใช้งาน) | `id` = `contacts[].id` |

- **Auth:** ต้องใช้ token `Authorization: Bearer <token>`
- **Backend (dev):** `http://localhost:5018/Account/…` และผ่าน Next.js ที่ `/api/backend/Account/…`
- **`contacts[].id`** ได้จาก `GetAccountById`, `GetAccounts`, `account` ของ `GetServiceById` และ response ของ `AddNewAccount`
- **ทั้ง 3 ตัวตอบเป็นผู้ติดต่อหลังแก้ทั้งก้อน** รูปแบบเดียวกับ `GetAccountById` (มี `contacts` ล่าสุด) เอาไปแสดงต่อได้เลย ไม่ต้องยิง GET ซ้ำ

## 2. Request body (POST และ PUT)

| Field | Type | บังคับ | ความหมาย |
|---|---|---|---|
| `detail` | string | **ใช่** (ไม่เกิน 100 ตัวอักษร) | เบอร์โทร หรืออีเมลเมื่อ `detailType` = `MAIL` ตัดช่องว่างหน้า/หลังให้ |
| `detailType` | string \| null | | `MOBILE` (ค่าเริ่มต้นเมื่อว่าง), `HOME`, `OFFICE`, `FAX`, `MAIL` พิมพ์เล็กก็ได้ ([GetAccountDetailType.md](GetAccountDetailType.md)) |

`PUT` เป็นการแทนที่ **ทั้ง `detailType` และ `detail`** ของรายการนั้น ให้ส่งครบทั้งสองค่าเสมอ (ไม่ส่ง `detailType` = `MOBILE`)

```http
POST /Account/AddAccountDetail?accountId=5BAAE3446A7E456697166D7A59EE7558
Content-Type: application/json
Authorization: Bearer <token>

{ "detailType": "HOME", "detail": "021234567" }
```

```http
PUT /Account/UpdateAccountDetail?id=D80AB27D94E54C13989443116242C986
{ "detailType": "OFFICE", "detail": "027654321" }
```

```http
DELETE /Account/DeleteAccountDetail?id=D80AB27D94E54C13989443116242C986
```

## 3. Response และ status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 201 (POST) / 200 (PUT, DELETE) | สำเร็จ | ผู้ติดต่อ + `contacts` ล่าสุด (เหมือน `GetAccountById`) |
| 400 | ไม่ส่ง `accountId` / `id`, `detail` ว่างหรือยาวเกิน, `detailType` ผิด, **POST ตอนมีครบ 10 รายการแล้ว** | ProblemDetails (`errors.<field>`) |
| 401 | ไม่มี token, หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | POST: ไม่มีผู้ติดต่อ `accountId` นี้ · PUT/DELETE: ไม่มีรายการ `id` นี้ หรือถูกลบไปแล้ว | `{ "statusCode": 404, "message": "ไม่พบข้อมูลผู้ติดต่อ" }` หรือ `"ไม่พบรายการติดต่อ"` |
| 409 | POST/PUT: ผู้ติดต่อคนนี้มีรายการชนิดและค่าเดียวกันอยู่แล้ว | `{ "statusCode": 409, "message": "มีรายการติดต่อนี้อยู่แล้ว" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

ตัวอย่าง 200 หลังเพิ่ม HOME (ตัดมาเฉพาะ `contacts`):

```json
"contacts": [
  { "id": "D80AB27D94E54C13989443116242C986", "detailType": "HOME", "detail": "021234567" },
  { "id": "3FC33A4B46DC457DA527405CDE684AF7", "detailType": "MAIL", "detail": "multi-test@example.com" },
  { "id": "3530C1C0656C4D41AC73F786EDE3F3C2", "detailType": "MOBILE", "detail": "0800000001" }
]
```

## 4. ข้อควรรู้ (ณ 18 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **DELETE = ปิดใช้งาน** | ระบบเดิมไม่ลบแถวจริง แถวถูกตั้ง `IsEnable = 'F'` แล้วหายจาก `contacts` ลบซ้ำหรือแก้ตัวที่ลบแล้ว = 404 ยังไม่มีทางกู้คืนจาก API |
| **ไม่ซ้ำ** | ชนิดและค่าเดียวกันในผู้ติดต่อคนเดียวเพิ่มซ้ำไม่ได้ (409) แก้ค่าตัวเองเป็นค่าเดิมได้ (200) |
| **สูงสุด 10 รายการต่อคน** | นับเฉพาะที่เปิดใช้งาน (ข้อมูลเดิมมากสุด 9) |
| **ลบรายการสุดท้ายได้** | ผู้ติดต่อจะเหลือ `contacts: []` ถ้าอยากบังคับให้มีอย่างน้อย 1 รายการ ให้ฟอร์มกันเอง |
| **มีผลกับรายงานและการค้นหาทันที** | รายงานที่แสดง `ContactNo` ของเรื่อง (มือถือ → บ้าน → ที่ทำงาน), `GetAccountByPhoneNo` และตัวกรองเบอร์ของ `GetServices` อ่านรายการเหล่านี้ แก้หรือลบเบอร์แล้วเรื่องเก่าก็เห็นเบอร์ใหม่ |
| **ไม่กระทบประวัติการรับสาย** | `TblContact` (`AddNewContact`) เก็บเบอร์ที่โทรเข้าเป็นข้อความ ณ ตอนนั้นและไม่ผูกกับผู้ติดต่อ จึงไม่เปลี่ยน |
| **ไม่ตรวจการแก้ชนกัน** | ถ้าสองคนแก้รายการเดียวกัน คนที่บันทึกทีหลังทับของคนแรก |
| **ข้อมูลส่วนบุคคล** | อย่าเก็บเบอร์/อีเมลลง `localStorage` หรือ log |

## 5. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/account.ts  (AccountSearchResult, AccountApiError มีอยู่แล้วจาก GetAccounts.md)
export interface AccountContactBody {
  detailType?: "MOBILE" | "HOME" | "OFFICE" | "FAX" | "MAIL" | null;
  detail: string;
}

async function call(
  method: "POST" | "PUT" | "DELETE",
  path: string,
  token: string,
  body?: AccountContactBody,
): Promise<AccountSearchResult> {
  const res = await fetch(`/api/backend/Account/${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body ? { "Content-Type": "application/json" } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (res.ok) return res.json();
  if (res.status === 400) throw new AccountApiError(400, "ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบ");
  if (res.status === 401) throw new AccountApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 404) throw new AccountApiError(404, "ไม่พบรายการ อาจถูกลบไปแล้ว");
  if (res.status === 409) throw new AccountApiError(409, "มีเบอร์/อีเมลนี้อยู่แล้ว");
  throw new AccountApiError(res.status, "บันทึกไม่สำเร็จ");
}

export const addAccountDetail = (accountId: string, body: AccountContactBody, token: string) =>
  call("POST", `AddAccountDetail?accountId=${encodeURIComponent(accountId)}`, token, body);
export const updateAccountDetail = (id: string, body: AccountContactBody, token: string) =>
  call("PUT", `UpdateAccountDetail?id=${encodeURIComponent(id)}`, token, body);
export const deleteAccountDetail = (id: string, token: string) =>
  call("DELETE", `DeleteAccountDetail?id=${encodeURIComponent(id)}`, token);
```

## 6. ทดสอบด้วย REST Client

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (**เขียนลง DB จริง**)
