# ค้นหาผู้ติดต่อจากเบอร์โทร (`GET /Account/GetAccountByPhoneNo`) สำหรับ Frontend

ใช้ค้นหา **ผู้ติดต่อ (account)** ที่มีเบอร์โทรตรงกับที่กรอก เบอร์เดียวอาจเป็นของหลายคน
(เช่น เบอร์กลางของหน่วยงาน หรือเบอร์ที่กรอกแทนไว้) API จึงคืนเป็น **รายการ**

> อ้างอิงโค้ด: `Controllers/AccountController.cs` (`GetAccountByPhoneNo`), `Dtos/AccountByPhoneResponse.cs`

> ถ้าต้องการ **เรื่อง (service)** ของเบอร์นี้ ให้ใช้ `GET /Services/SearchService?AccountPhoneNo=...`
> (ดู [SearchService.md](SearchService.md)) endpoint นี้คืนแค่ข้อมูลผู้ติดต่อ

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` (ไม่มี body ส่งเบอร์ทาง query string) |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Backend (dev) | `http://localhost:5018/Account/GetAccountByPhoneNo?phoneNo=0909840822` |
| URL จาก Next.js | `/api/backend/Account/GetAccountByPhoneNo?phoneNo=...` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Request (query string)

| Parameter | Type | บังคับ | ความหมาย |
|---|---|---|---|
| `phoneNo` | string | ✅ | เบอร์โทรที่ต้องการค้น เช่น `0909840822` |

- ค้นแบบ **ตรงทั้งค่า** ไม่ใช่ค้นบางส่วน (`090984` ไม่เจอ `0909840822`)
- ค้นเฉพาะเบอร์ประเภท **มือถือ / บ้าน / ที่ทำงาน / แฟกซ์** (`MOBILE` / `HOME` / `OFFICE` / `FAX`) ไม่ค้นอีเมล
- ได้เฉพาะผู้ติดต่อที่เปิดใช้งาน
- ส่งเป็น **ตัวเลขล้วน** ห้ามมีขีดหรือช่องว่าง (`090-984-0822` ไม่เจอ) เพราะเบอร์ใน DB เก็บเป็นตัวเลขล้วน
- **API ไม่ตัดช่องว่างให้** ต้อง `trim()` ก่อนส่ง (ช่องว่างหน้าเบอร์ทำให้ค้นไม่เจอ)
- ไม่ส่ง หรือส่งค่าว่าง → 400

---

## 3. Response

### 3.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ไม่มี envelope `{ status, message, data }` และ **ไม่แบ่งหน้า** (ได้ทุกแถวที่เจอ)

| Field | Type | ความหมาย |
|---|---|---|
| `phoneNo` | string | เบอร์ที่ค้นเจอ (เท่ากับที่ส่งไป) |
| `fullName` | string | คำนำหน้า + ชื่อ + นามสกุล (ภาษาไทย) ส่วนที่ไม่มีจะถูกข้าม เป็น `""` ถ้าไม่มีเลย |
| `gender` | string \| null | ชื่อเพศภาษาไทย: `ชาย` / `หญิง` / `--ไม่ระบุ--` หรือ `null` ถ้าไม่มีข้อมูล |
| `address` | string \| null | ที่อยู่ ข้อมูลส่วนใหญ่ **ว่าง** (`null` หรือ `""`) |
| `created` | string \| null | วันที่สร้างผู้ติดต่อ เป็น **ข้อความแบบเดิมของระบบเก่า** `เดือน/วัน/ปี ค.ศ. ชั่วโมง:นาที` (ดู 3.3) |

- **เรียงตามชื่อ แล้วตามนามสกุล**
- ไม่เจอเลย → `200` กับ `[]`
- ข้อมูลเดิมส่วนใหญ่ **ไม่มีคำนำหน้า** `fullName` จึงมักเป็นแค่ `ชื่อ นามสกุล`

### 3.2 ตัวอย่าง

`GET /Account/GetAccountByPhoneNo?phoneNo=0909840822`

(ตัดมาแสดง 2 จาก 8 แถว เพศและรูปแบบ `created` เป็นข้อมูลจริง ณ 16 พ.ย. 2026 ส่วนชื่อและที่อยู่เป็นค่าสมมุติ)

```json
[
  {
    "phoneNo": "0909840822",
    "fullName": "สมหญิง ทดสอบ",
    "gender": "หญิง",
    "address": null,
    "created": "07/02/2026 14:37"
  },
  {
    "phoneNo": "0909840822",
    "fullName": "นาย สมชาย ตัวอย่าง",
    "gender": "ชาย",
    "address": "99/1 ถนนแจ้งวัฒนะ กรุงเทพฯ",
    "created": "06/30/2026 09:50"
  }
]
```

**ไม่พบข้อมูล**

```json
[]
```

**400: ไม่ได้ส่งเบอร์** (ProblemDetails ข้อความใน `errors` เป็นภาษาอังกฤษ ไม่ควรแสดงให้ผู้ใช้ตรง ๆ)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "phoneNo": ["A phone number is required."]
  },
  "traceId": "00-..."
}
```

ถ้าไม่มี `phoneNo` ใน URL เลย ข้อความจะเป็น `The phoneNo field is required.` แต่ key ยังเป็น `phoneNo` เหมือนกัน

### 3.3 รูปแบบของ `created`

`created` ยังเป็นข้อความตามที่ระบบเดิมบันทึกไว้ ไม่ใช่ ISO 8601

| ตัวอย่าง | ความหมาย |
|---|---|
| `07/02/2026 14:37` | **2 ก.ค. 2026** 14:37 (เดือนขึ้นก่อน ไม่ใช่ 7 ก.พ.) |
| `11/16/2026 9:05` | 16 พ.ย. 2026 09:05 (ผู้ติดต่อที่สร้างผ่าน API ใหม่จะไม่มีเลข 0 นำหน้า) |

ปีเป็น ค.ศ. และเวลาเป็นเวลาไทย ถ้าจะแสดงแบบ พ.ศ. หรือ วว/ดด/ปปปป ให้แปลงที่หน้าเว็บ (ดู `parseLegacyDate` ใน 4.1)

### 3.4 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่พบข้อมูล `[]`) | `AccountByPhone[]` |
| 400 | ไม่ส่ง `phoneNo` หรือส่งค่าว่าง | ProblemDetails |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

### 4.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/account.ts
export interface AccountByPhone {
  phoneNo: string;
  fullName: string;
  gender: string | null;
  address: string | null;
  created: string | null; // "MM/dd/yyyy HH:mm" แบบเดิม เดือนขึ้นก่อน
}

export class AccountApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export async function fetchAccountsByPhone(
  phoneNo: string,
  opts: { token: string; signal?: AbortSignal },
): Promise<AccountByPhone[]> {
  const phone = phoneNo.trim(); // API ไม่ trim ให้
  if (phone === "") throw new AccountApiError(400, "กรุณากรอกเบอร์โทร");

  const qs = new URLSearchParams({ phoneNo: phone });
  const res = await fetch(`/api/backend/Account/GetAccountByPhoneNo?${qs}`, {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });

  if (res.ok) return res.json();
  if (res.status === 401) throw new AccountApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 400) throw new AccountApiError(400, "กรุณากรอกเบอร์โทร");
  throw new AccountApiError(res.status, "ค้นหาผู้ติดต่อไม่สำเร็จ");
}

/** แปลง "07/02/2026 14:37" หรือ "7/2/2026 9:05" (เดือน/วัน/ปี ค.ศ.) เป็น Date เวลาท้องถิ่น */
export function parseLegacyDate(text: string | null): Date | null {
  const m = text?.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4}) (\d{1,2}):(\d{2})$/);
  if (!m) return null;
  const [month, day, year, hour, minute] = m.slice(1).map(Number);
  return new Date(year, month - 1, day, hour, minute);
}
```

### 4.2 ใช้ในหน้าจอ

```tsx
"use client";

import { useState } from "react";
import {
  AccountApiError, fetchAccountsByPhone, parseLegacyDate, type AccountByPhone,
} from "@/lib/api/account";

export function AccountByPhoneSearch({ token }: { token: string }) {
  const [phone, setPhone] = useState("");
  const [rows, setRows] = useState<AccountByPhone[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function search() {
    setError(null);
    try {
      setRows(await fetchAccountsByPhone(phone, { token }));
    } catch (e) {
      setError(e instanceof AccountApiError ? e.message : "ค้นหาผู้ติดต่อไม่สำเร็จ");
    }
  }

  return (
    <div>
      <input
        inputMode="numeric"
        placeholder="เบอร์โทร"
        value={phone}
        // เก็บเฉพาะตัวเลข ตัดขีดและช่องว่างที่ผู้ใช้พิมพ์หรือวางมา
        onChange={(e) => setPhone(e.target.value.replace(/\D/g, ""))}
      />
      <button disabled={phone === ""} onClick={search}>ค้นหา</button>

      {error && <p role="alert">{error}</p>}
      {rows?.length === 0 && <p>ไม่พบข้อมูล</p>}

      <table>
        <tbody>
          {rows?.map((r, i) => (
            // ยังไม่มี id ของผู้ติดต่อ (ดูข้อ 5) จึงใช้ index เป็น key ไปก่อน
            <tr key={i}>
              <td>{r.fullName || "-"}</td>
              <td>{r.gender ?? "-"}</td>
              <td>{r.address || "-"}</td>
              <td>{parseLegacyDate(r.created)?.toLocaleString("th-TH") ?? "-"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

`toLocaleString("th-TH")` แสดงปีเป็น พ.ศ. ให้อยู่แล้ว เช่น `2/7/2569 14:37:00`

---

## 5. ข้อควรรู้ (สถานะ ณ 16 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ยังไม่คืน `id` ของผู้ติดต่อ** | เลือกคนจากรายการนี้แล้วเอาไปใช้ต่อ (เช่น ใส่ `accountId` ตอนเปิดเรื่องด้วย `AddNewService`) ยังทำไม่ได้ ถ้าต้องใช้ แจ้ง backend ให้เพิ่ม field `accountId` |
| **คนเดียวกันอาจขึ้นซ้ำ** | ถ้าผู้ติดต่อคนหนึ่งบันทึกเบอร์เดียวกันไว้มากกว่า 1 แถว (เช่น ทั้งมือถือและบ้าน) จะได้คนนั้น 2 แถว ใน DB มีกรณีนี้ราว 140 กรณี และยังกรองซ้ำด้วย id ไม่ได้ เพราะไม่มี id |
| **ไม่แบ่งหน้า ไม่จำกัดจำนวน** | เบอร์ที่ใช้แทนกันบ่อยได้หลายแถว เช่น `0999999999` ได้ 19 คน ถ้ารายการยาว ให้แบ่งหน้าหรือเลื่อนดูที่หน้าเว็บเอง |
| **ไม่บอกประเภทเบอร์** | response ไม่มีว่าเบอร์ที่เจอเป็นมือถือหรือเบอร์บ้าน |
| **`created` เป็นข้อความ เดือน/วัน/ปี** | อย่า `new Date(created)` ตรง ๆ ให้ใช้ `parseLegacyDate` |

ถ้า backend เพิ่ม `accountId` หรือแก้เรื่องแถวซ้ำ จะแจ้งและอัปเดตเอกสารนี้

---

## 6. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/Account/GetAccountByPhoneNo?phoneNo=0909840822
Authorization: Bearer <token>
```

```bash
curl "http://localhost:5018/Account/GetAccountByPhoneNo?phoneNo=0909840822" \
  -H "Authorization: Bearer <token>"
```

| เบอร์ | ผลที่ควรได้ (ข้อมูล ณ 16 พ.ย. 2026) |
|---|---|
| `0909840822` | 8 แถว |
| `0999999999` | 19 แถว |
| `0000000000` | `[]` |
