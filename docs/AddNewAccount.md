# เพิ่มผู้ติดต่อใหม่ (`POST /Account/AddNewAccount`) สำหรับ Frontend

ใช้บันทึกฟอร์ม **เพิ่มผู้ติดต่อ (account)** ได้ชื่อ เพศ วันเกิด ที่อยู่ และ **เบอร์โทร/อีเมลได้หลายรายการ** ในครั้งเดียว
ผู้ติดต่อที่สร้างแล้วเอาไป `AddNewService` ต่อได้ด้วย `id` ใน response

> อ้างอิงโค้ด: `Controllers/AccountController.cs` (`AddNewAccount`), `Dtos/CreateAccountRequest.cs`,
> `Dtos/AccountResponse.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `POST` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Body | JSON (`Content-Type: application/json`) ดูข้อ 2 |
| Backend (dev) | `http://localhost:5018/Account/AddNewAccount` |
| URL จาก Next.js | `/api/backend/Account/AddNewAccount` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

**ก่อนยิง:** ถ้าผู้ใช้ป้อนเบอร์โทร ควรเรียก `GET /Account/GetAccountByPhoneNo?phoneNo=…` ก่อน
เพื่อดูว่ามีผู้ติดต่อเบอร์นี้อยู่แล้วหรือไม่ endpoint นี้ **ไม่ตรวจชื่อหรือเบอร์ซ้ำ** ยิงซ้ำจะได้ผู้ติดต่อใหม่ทุกครั้ง

---

## 2. Request body

| Field | Type | บังคับ | ยาวสุด | ความหมาย |
|---|---|---|---|---|
| `firstnameTh` | string | **ใช่** | 200 | ชื่อภาษาไทย |
| `salutationTh` / `lastnameTh` | string \| null | | 100 / 200 | คำนำหน้า / นามสกุล ภาษาไทย |
| `salutationEn` / `firstnameEn` / `lastnameEn` | string \| null | | 100 / 200 / 200 | ชื่อภาษาอังกฤษ |
| `genderId` | string \| null | | 50 | `id` จาก `GET /ListOfValues/GetGender` ส่ง id ที่ไม่มีอยู่ = 400 |
| `birthdate` | string \| null | | 50 | **ข้อความแบบเดิม เดือนขึ้นก่อน** เช่น `5/14/1988 0:00` (ระบบเดิมอ่านรูปแบบนี้) |
| `address` | string \| null | | | ที่อยู่ ข้อความอิสระ |
| `areaId` | string \| null | | 50 | **อย่าส่ง** ตารางพื้นที่ใน DB ว่าง ส่งค่าอะไรมาก็ได้ 400 ดู [AreaId.md](AreaId.md) |
| `zipcode` | string \| null | | 100 | รหัสไปรษณีย์ |
| `remark` | string \| null | | | หมายเหตุ |
| `isScret` | `"T"` \| `"F"` | | 10 | แฟล็กปกปิดข้อมูล (สะกดตาม DB) ไม่ส่ง = `"F"` |
| `isEnable` | `"T"` \| `"F"` | | 10 | ไม่ส่ง = `"T"` ปกติไม่ต้องส่ง |
| `contacts` | `{ detailType?, detail }[]` | | | เบอร์โทร/อีเมลของผู้ติดต่อ **สูงสุด 10 รายการ** ไม่ส่ง/ว่าง = ไม่สร้างรายการติดต่อ ดูข้อ 2.1 |

**backend ตั้งให้เอง (ห้ามส่ง):** `id`, `created` / `updated`, `createdBy` / `updatedBy` (จากพนักงานใน token)

### 2.1 `contacts`

ผู้ติดต่อ 1 คนมีได้หลายแถว (ในระบบเดิม 3,812 คนมี 2 แถว และมีบางคนถึง 9 แถว) เช่น มือถือ 2 เบอร์ + อีเมล 1 อัน

| Field | Type | บังคับ | ยาวสุด | ความหมาย |
|---|---|---|---|---|
| `detail` | string | **ใช่** | 100 | เบอร์โทร หรืออีเมลเมื่อ `detailType` = `MAIL` ตัดช่องว่างหน้า/หลังให้ |
| `detailType` | string \| null | | 50 | `MOBILE` (ค่าเริ่มต้นเมื่อว่าง), `HOME`, `OFFICE`, `FAX`, `MAIL` พิมพ์เล็กก็ได้ ค่าอื่น = 400 รายการนี้ดึงได้จาก `GET /ListOfValues/GetAccountDetailType` ([GetAccountDetailType.md](GetAccountDetailType.md)) |

- **ชนิดเดียวกันมีหลายรายการได้:** เช่น `MOBILE` 2 เบอร์
- **รายการซ้ำนับเป็นอันเดียว:** ชนิดและค่าเดียวกันซ้ำ backend เก็บแถวเดียว
- **เกิน 10 รายการ = 400**
- **แก้ภายหลัง:** เพิ่ม/แก้/ลบรายการด้วย [AccountDetail.md](AccountDetail.md) (`PUT UpdateAccountById` ไม่สนใจ field นี้)

### ตัวอย่าง

```http
POST /Account/AddNewAccount
Content-Type: application/json
Authorization: Bearer <token>

{
  "salutationTh": "นางสาว",
  "firstnameTh": "ศิรประภา",
  "lastnameTh": "ทดสอบ",
  "genderId": "9CF68DA7874E672EFCA3A6F0824FE543",
  "address": "123 ถนนแจ้งวัฒนะ กรุงเทพฯ",
  "zipcode": "10210",
  "isScret": "F",
  "isEnable": "T",
  "contacts": [
    { "detailType": "MOBILE", "detail": "0812345678" },
    { "detailType": "MAIL", "detail": "sirapa@example.com" }
  ]
}
```

---

## 3. Response

| HTTP | เมื่อไร | Body |
|---|---|---|
| 201 | สร้างสำเร็จ | `AccountResponse` ดูข้อ 3.1 |
| 400 | ไม่มี `firstnameTh`, ข้อความยาวเกิน, `genderId` / `areaId` ไม่มีอยู่, `detailType` ผิด, JSON ผิดรูป | ProblemDetails (`errors.<field>`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

ผู้ติดต่อกับรายการติดต่อทั้งหมดถูกบันทึกใน transaction เดียวกัน ถ้าบันทึกไม่สำเร็จจะไม่มีอะไรถูกสร้างเลย

### 3.1 ตัวอย่าง 201

```json
{
  "statusCode": 201,
  "id": "4D7B3F5E81104F349449B50571E72BCE",
  "salutationTh": "นางสาว",
  "firstnameTh": "ศิรประภา",
  "lastnameTh": "ทดสอบ",
  "salutationEn": null,
  "firstnameEn": null,
  "lastnameEn": null,
  "genderId": "9CF68DA7874E672EFCA3A6F0824FE543",
  "birthdate": null,
  "address": "123 ถนนแจ้งวัฒนะ กรุงเทพฯ",
  "areaId": null,
  "zipcode": "10210",
  "remark": null,
  "isScret": "F",
  "isEnable": "T",
  "created": "11/18/2026 9:30",
  "createdBy": "A279A78DFDF442F6B6CD1D2BE13555B4",
  "updated": "11/18/2026 9:30",
  "updatedBy": "A279A78DFDF442F6B6CD1D2BE13555B4",
  "contacts": [
    { "id": "<id>", "detailType": "MOBILE", "detail": "0812345678" },
    { "id": "<id>", "detailType": "MAIL", "detail": "sirapa@example.com" }
  ]
}
```

(ค่า `id` และเวลาเป็นตัวอย่าง) `id` ใช้เป็น `accountId` ตอน `AddNewService` และเรียก
`GET /Account/GetAccountById?id=…` / `PUT /Account/UpdateAccountById?id=…` ต่อได้
**response นี้ไม่มี `fullName` / `gender`** ถ้าต้องการให้เรียก `GetAccountById`

ตัวอย่าง 400 (ข้อความเป็นภาษาอังกฤษ ไม่ควรแสดงให้ผู้ใช้ตรง ๆ ให้ map จากชื่อ field):

```json
{
  "status": 400,
  "errors": {
    "FirstnameTh": ["The FirstnameTh field is required."],
    "Contacts[1].DetailType": ["DetailType must be one of MOBILE, HOME, OFFICE, FAX, MAIL."]
  }
}
```

---

## 4. ข้อควรรู้ (ณ 18 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ไม่ตรวจซ้ำ** | ชื่อหรือเบอร์เดียวกันสร้างซ้ำได้ ให้ค้นด้วย `GetAccountByPhoneNo` / `GetAccounts` ก่อน แล้วถามผู้ใช้ |
| **`areaId` ใช้ไม่ได้** | ไม่ต้องทำ drop-down พื้นที่ เก็บที่อยู่เป็นข้อความใน `address` |
| **ไม่ส่ง `contacts`** | สร้างผู้ติดต่อโดยไม่มีเบอร์ (ผู้ติดต่อเดิมทุกคนมีอย่างน้อย 1 รายการ แนะนำให้ฟอร์มบังคับกรอกอย่างน้อย 1 ช่อง) |
| **ข้อความไม่ถูกตัดช่องว่าง** | ยกเว้น `detail` ใน `contacts` ที่ตัดให้ ฟิลด์อื่นให้ตัดช่องว่างก่อนส่ง |
| **ข้อมูลส่วนบุคคล** | อย่าเก็บ response ลง `localStorage` หรือ log |

---

## 5. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/account.ts  (AccountApiError มีอยู่แล้วจาก GetAccounts.md)
export interface AddAccountBody {
  salutationTh?: string | null; firstnameTh: string; lastnameTh?: string | null;
  salutationEn?: string | null; firstnameEn?: string | null; lastnameEn?: string | null;
  genderId?: string | null; birthdate?: string | null;   // "M/d/yyyy H:mm"
  address?: string | null; zipcode?: string | null; remark?: string | null;
  isScret?: "T" | "F"; isEnable?: "T" | "F";
  contacts?: { detailType?: "MOBILE" | "HOME" | "OFFICE" | "FAX" | "MAIL" | null; detail: string }[];
}

export interface AddAccountResult {
  statusCode: number; id: string;
  firstnameTh: string | null; lastnameTh: string | null;
  contacts: { id: string; detailType: string; detail: string }[];
  // field อื่นเหมือน AccountSearchResult ยกเว้นไม่มี fullName / gender
}

export async function addAccount(
  body: AddAccountBody,
  opts: { token: string; signal?: AbortSignal },
): Promise<AddAccountResult> {
  const res = await fetch("/api/backend/Account/AddNewAccount", {
    method: "POST",
    headers: { Authorization: `Bearer ${opts.token}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal: opts.signal,
  });
  if (res.status === 201) return res.json();
  if (res.status === 400) throw new AccountApiError(400, "ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบ");
  if (res.status === 401) throw new AccountApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  throw new AccountApiError(res.status, "บันทึกผู้ติดต่อไม่สำเร็จ");
}
```

---

## 6. ทดสอบด้วย REST Client

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (**เขียนลง DB จริง**)
