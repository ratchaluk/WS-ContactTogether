# ข้อมูลผู้ติดต่อตาม id (`GET /Account/GetAccountById`) สำหรับ Frontend

ใช้ดึง **ผู้ติดต่อ (account) หนึ่งคน** ด้วย `id` ได้ชื่อ เพศ วันเกิด ที่อยู่ และเบอร์/อีเมลทั้งหมดของคนนั้น
เหมาะกับหน้าที่มี `accountId` อยู่แล้ว เช่น หน้าข้อมูลผู้ติดต่อ หรือฟอร์มแก้ไขผู้ติดต่อ

> อ้างอิงโค้ด: `Controllers/AccountController.cs` (`GetAccountById`), `Helper/AccountQueries.cs`,
> `Dtos/AccountSearchResponse.cs`, `Dtos/AccountContactResponse.cs`

> **หน้ารายละเอียดเรื่องไม่ต้องเรียก endpoint นี้:** `GET /Services/GetServiceById` ส่ง `account` แบบเดียวกันมาให้แล้ว
> ([GetServiceById.md](GetServiceById.md)) ทั้งสองที่ใช้โค้ดดึงข้อมูลตัวเดียวกัน ข้อมูลจึงตรงกันเสมอ

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `id` (บังคับ) = `TblAccount.Id` hex 32 ตัว ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| Backend (dev) | `http://localhost:5018/Account/GetAccountById?id=…` |
| URL จาก Next.js | `/api/backend/Account/GetAccountById?id=…` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

**ได้ `id` มาจากไหน**

- `id` ของแถวใน `GET /Account/GetAccounts`
- `accountId` ของเรื่อง (`GetServices` ไม่มี field นี้ ต้องใช้ `GetServiceById` หรือ response ของ `AddNewService`)
- `id` ใน response ของ `POST /Account/AddNewAccount`

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **object เดียว** รูปแบบ **เดียวกับแต่ละแถวของ `GET /Account/GetAccounts`** ทุก field
ใช้ type `AccountSearchResult` ตัวเดิมได้เลย ความหมายของแต่ละ field อยู่ใน [GetAccounts.md ข้อ 3](GetAccounts.md) สรุปได้ดังนี้

| Field | ความหมาย |
|---|---|
| `id` | id ของผู้ติดต่อ (ตัวพิมพ์ใหญ่) |
| `fullName` | คำนำหน้า ชื่อ นามสกุล ภาษาไทย ต่อกันให้แล้ว |
| `salutationTh` / `firstnameTh` / `lastnameTh`, `…En` | ชื่อแยกส่วน |
| `genderId` / `gender` | id และชื่อเพศ |
| `birthdate`, `created` | **ข้อความแบบเดิม เดือนขึ้นก่อน** เช่น `5/19/2026 0:00` ใช้ `parseLegacyDate` ใน GetAccounts.md |
| `address`, `zipcode` | ที่อยู่เป็นข้อความอิสระ และรหัสไปรษณีย์ |
| `areaId` | ค่าพื้นที่จากระบบเดิม ใช้ต่อไม่ได้ ([AreaId.md](AreaId.md)) |
| `remark`, `isScret` | หมายเหตุ และแฟล็กปกปิดข้อมูล |
| `isEnable` | `"T"` = เปิดใช้งาน, `"F"` = ปิดใช้งาน (endpoint นี้ส่งคนที่ปิดใช้งานมาด้วย) |
| `contacts` | `{ id, detailType, detail }[]` เบอร์/อีเมลที่เปิดใช้งาน (`MOBILE` / `HOME` / `OFFICE` / `FAX` / `MAIL`) `id` ใช้กับ [AccountDetail.md](AccountDetail.md) (แก้/ลบรายการ) |

### 2.2 ตัวอย่าง (ข้อมูลจริง ณ 18 พ.ย. 2026)

`GET /Account/GetAccountById?id=F05AB03D7CF74CEFA75504047FD52343`

```json
{
  "id": "F05AB03D7CF74CEFA75504047FD52343",
  "salutationTh": "นาย",
  "firstnameTh": "อิน",
  "lastnameTh": "ทดสอบ",
  "salutationEn": "",
  "firstnameEn": "",
  "lastnameEn": "",
  "fullName": "นาย อิน ทดสอบ",
  "genderId": "C833A9E6D354B2AC350A50CFB41852A8",
  "gender": "ชาย",
  "birthdate": "5/19/2026 0:00",
  "address": "456/456",
  "areaId": "16AC86F1FC2255923683F632CAC454DA",
  "zipcode": "10210",
  "remark": "",
  "isScret": "F",
  "isEnable": "T",
  "created": "05/28/2026 11:06",
  "contacts": [
    { "id": "<TblAccountDetail.Id ตัวพิมพ์ใหญ่>", "detailType": "MOBILE", "detail": "0934153699" }
  ]
}
```

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | พบผู้ติดต่อ | `AccountSearchResult` |
| 400 | ไม่ส่ง `id` หรือส่งค่าว่าง | ProblemDetails (`errors.id`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีผู้ติดต่อ `id` นี้ | `{ "statusCode": 404, "message": "ไม่พบข้อมูลผู้ติดต่อ" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/account.ts  (AccountSearchResult, AccountApiError มีอยู่แล้วจาก GetAccounts.md)
export async function fetchAccountById(
  id: string,
  opts: { token: string; signal?: AbortSignal },
): Promise<AccountSearchResult | null> {
  const res = await fetch(`/api/backend/Account/GetAccountById?id=${encodeURIComponent(id)}`, {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 404) return null; // แสดง "ไม่พบข้อมูลผู้ติดต่อ"
  if (res.status === 401) throw new AccountApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  throw new AccountApiError(res.status, "โหลดข้อมูลผู้ติดต่อไม่สำเร็จ");
}
```

แสดงเบอร์และอีเมลได้ด้วย `sortContacts` และ `DETAIL_TYPE_LABELS` จาก [GetAccounts.md ข้อ 4.1](GetAccounts.md)

---

## 4. ข้อควรรู้ (ณ 18 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ผู้ติดต่อที่ถูกปิดใช้งานก็ได้ 200** | ต่างจาก `GetAccounts` ที่ค้นเฉพาะคนที่เปิดใช้งาน เพื่อให้เรื่องเก่ายังแสดงคนที่ติดต่อเข้ามาได้ ดูจาก `isEnable` แล้วแสดงป้าย "ปิดใช้งาน" ได้ |
| **แก้ไขผู้ติดต่อ** | ใช้ `PUT /Account/UpdateAccountById` ([UpdateAccountById.md](UpdateAccountById.md)) |
| **`id` ซ้ำในข้อมูลเดิม** | `id` ที่ซ้ำกันในระบบเดิมมีข้อมูลเหมือนกันทุกแถว endpoint นี้ส่งมาแถวเดียวเสมอ |
| **`contacts` เฉพาะที่เปิดใช้งาน** | เรียงตาม `detailType` แบบตัวอักษร ถ้าอยากให้มือถือขึ้นก่อนใช้ `sortContacts` |
| **ข้อมูลส่วนบุคคล** | มีชื่อ เบอร์ อีเมล ที่อยู่ วันเกิด อย่าเก็บลง `localStorage` หรือ log |

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/Account/GetAccountById?id=F05AB03D7CF74CEFA75504047FD52343
Authorization: Bearer <token>
```

```bash
curl "http://localhost:5018/Account/GetAccountById?id=F05AB03D7CF74CEFA75504047FD52343" -H "Authorization: Bearer <token>"
```
