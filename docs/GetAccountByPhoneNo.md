# ค้นหาผู้ติดต่อจากเบอร์โทร (`GET /Account/GetAccountByPhoneNo`) สำหรับ Frontend

ใช้ค้นหา **ผู้ติดต่อ (account)** ที่มีเบอร์โทรตรงกับที่กรอก เบอร์เดียวอาจเป็นของหลายคน
(เช่น เบอร์กลางของหน่วยงาน หรือเบอร์ที่กรอกแทนไว้) API จึงคืนเป็น **รายการ**

> อ้างอิงโค้ด: `Controllers/AccountController.cs` (`GetAccountByPhoneNo`), `Dtos/AccountByPhoneResponse.cs`

> ถ้าต้องการ **เรื่อง (service)** ของผู้ติดต่อที่ผู้ใช้เลือก ให้ใช้ `GET /Services/SearchService?AccountId=<id ของคนนั้น>`
> ไม่ใช่ `AccountPhoneNo` เพราะเบอร์เดียวอาจมีหลายคน และจะได้เรื่องของทุกคนปนกัน
> (ดู [SearchService.md ข้อ 2.4](SearchService.md)) endpoint นี้คืนแค่ข้อมูลผู้ติดต่อ

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
| `accountId` | string | id ของผู้ติดต่อ (`TblAccount.Id` ตัวพิมพ์ใหญ่) ส่งต่อเป็น `AccountId` ของ `SearchService` หรือ `accountId` ของ `AddNewService` |
| `phoneNo` | string | เบอร์ที่ค้นเจอ (เท่ากับที่ส่งไป) |
| `fullName` | string | คำนำหน้า + ชื่อ + นามสกุล (ภาษาไทย) ส่วนที่ไม่มีจะถูกข้าม เป็น `""` ถ้าไม่มีเลย |
| `gender` | string \| null | ชื่อเพศภาษาไทย: `ชาย` / `หญิง` / `--ไม่ระบุ--` หรือ `null` ถ้าไม่มีข้อมูล |
| `address` | string \| null | ที่อยู่ ข้อมูลส่วนใหญ่ **ว่าง** (`null` หรือ `""`) |
| `created` | string \| null | วันที่สร้างผู้ติดต่อ เป็น **ข้อความแบบเดิมของระบบเก่า** `เดือน/วัน/ปี ค.ศ. ชั่วโมง:นาที` (ดู 3.3) |

- **เรียงตามชื่อ แล้วตามนามสกุล**
- **คนละ 1 แถว:** ผู้ติดต่อที่บันทึกเบอร์นี้ไว้หลายแถว (เช่น ทั้งมือถือและบ้าน) ได้แถวเดียว `accountId` ไม่ซ้ำกันในผลเดียว
- ไม่เจอเลย → `200` กับ `[]`
- ข้อมูลเดิมส่วนใหญ่ **ไม่มีคำนำหน้า** `fullName` จึงมักเป็นแค่ `ชื่อ นามสกุล`

### 3.2 ตัวอย่าง

`GET /Account/GetAccountByPhoneNo?phoneNo=0909840822`

(ตัดมาแสดง 2 จาก 8 แถว เพศและรูปแบบ `created` เป็นข้อมูลจริง ณ 16 พ.ย. 2026 ส่วน id ชื่อ และที่อยู่เป็นค่าสมมุติ)

```json
[
  {
    "accountId": "9F8E7D6C5B4A43219F8E7D6C5B4A4321",
    "phoneNo": "0909840822",
    "fullName": "สมหญิง ทดสอบ",
    "gender": "หญิง",
    "address": null,
    "created": "07/02/2026 14:37"
  },
  {
    "accountId": "1A2B3C4D5E6F47809A1B2C3D4E5F6A7B",
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
  accountId: string; // ส่งต่อเป็น AccountId ของ SearchService / accountId ของ AddNewService
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
          {rows?.map((r) => (
            <tr key={r.accountId}>
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

## 5. ข้อควรรู้ (สถานะ ณ 19 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ใช้ `accountId` ต่อหลังผู้ใช้เลือกคน** | หน้ารับสาย: ให้ผู้ใช้เลือกคนที่โทรเข้า แล้วค้นเรื่องด้วย `SearchService?AccountId=<accountId>` ([SearchService.md ข้อ 2.4](SearchService.md)) และเปิดเรื่องด้วย `accountId` เดียวกัน ([AddNewService.md](AddNewService.md)) อย่าค้นเรื่องด้วยเบอร์ เพราะจะได้เรื่องของทุกคนที่ใช้เบอร์นั้น |
| **คนเดียวกันไม่ขึ้นซ้ำแล้ว** | ตั้งแต่ 19 พ.ย. 2026 ได้คนละ 1 แถว แม้จะบันทึกเบอร์นี้ไว้หลายแถว (เช่น `0184939775` เดิมได้ 2 แถว ตอนนี้ได้ 1) ใช้ `accountId` เป็น key ได้ |
| **ไม่แบ่งหน้า ไม่จำกัดจำนวน** | เบอร์ที่ใช้แทนกันบ่อยได้หลายแถว เช่น `0999999999` ได้ 19 คน ถ้ารายการยาว ให้แบ่งหน้าหรือเลื่อนดูที่หน้าเว็บเอง |
| **ไม่บอกประเภทเบอร์** | response ไม่มีว่าเบอร์ที่เจอเป็นมือถือหรือเบอร์บ้าน |
| **`created` เป็นข้อความ เดือน/วัน/ปี** | อย่า `new Date(created)` ตรง ๆ ให้ใช้ `parseLegacyDate` |

ถ้า backend ปรับข้อใดข้างบน จะแจ้งและอัปเดตเอกสารนี้

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
