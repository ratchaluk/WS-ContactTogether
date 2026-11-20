# ค้นหาผู้ติดต่อ (`GET /Account/GetAccounts`) สำหรับ Frontend

ใช้ค้นหา **ผู้ติดต่อ (account)** จากชื่อ นามสกุล และ/หรือข้อมูลติดต่อ (เบอร์โทร อีเมล ที่อยู่)
แบบ **ค้นบางส่วน** ได้ข้อมูลผู้ติดต่อครบทุก field พร้อม **`id`** และรายการเบอร์/อีเมลของแต่ละคน

> อ้างอิงโค้ด: `Controllers/AccountController.cs` (`GetAccounts`), `Dtos/AccountSearchResponse.cs`,
> `Dtos/AccountContactResponse.cs`

> **ต่างจาก `GetAccountByPhoneNo`** ([GetAccountByPhoneNo.md](GetAccountByPhoneNo.md)):
> endpoint นี้ค้นบางส่วนได้ ค้นด้วยชื่อหรืออีเมลได้ และ **คืน `id`** ถ้าต้องการ `accountId` ไปเปิดเรื่อง
> (`POST /Services/AddNewService`) จากเบอร์โทร ให้ใช้ `GetAccounts?contactInfo=<เบอร์>`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` (ไม่มี body ส่งทุกอย่างทาง query string) |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Backend (dev) | `http://localhost:5018/Account/GetAccounts?firstName=สม` |
| URL จาก Next.js | `/api/backend/Account/GetAccounts?...` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Request (query string)

### 2.1 Parameter

ทุกตัวไม่บังคับ แต่ **ต้องส่งอย่างน้อย 1 ตัว** ส่งหลายตัวได้ ผลลัพธ์ต้องตรง **ทุกตัวที่ส่ง** (AND)

| Parameter | ค้นใน | ตัวอย่าง |
|---|---|---|
| `firstName` | ชื่อภาษาไทย **หรือ** ชื่อภาษาอังกฤษ | `สม` เจอ สมชาย, สมหญิง, อุดมสม |
| `lastName` | นามสกุลภาษาไทย **หรือ** นามสกุลภาษาอังกฤษ | `ใจดี` |
| `contactInfo` | ที่อยู่ **หรือ** เบอร์/อีเมลใดก็ได้ของผู้ติดต่อ (มือถือ บ้าน ที่ทำงาน แฟกซ์ อีเมล) | `0909840822`, `0909`, `@gmail.com`, `แจ้งวัฒนะ` |

### 2.2 กติกาที่ควรรู้

- ทุกตัวค้นแบบ **"มีคำนี้อยู่"** (ค้นบางส่วน) ไม่ต้องพิมพ์ครบ
- backend **ตัดช่องว่างหน้า/หลังให้** ค่าว่างหรือมีแต่ช่องว่างถือว่าไม่ได้ส่ง
- **แยกตัวพิมพ์เล็ก/ใหญ่** (collation ของ DB) `somchai` ไม่เจอ `Somchai`
- เบอร์ใน DB เก็บเป็นตัวเลขล้วน ถ้าผู้ใช้พิมพ์เบอร์มีขีด (`090-984-0822`) ให้ตัดขีดออกก่อนส่ง
- `contactInfo` ค้นเฉพาะเบอร์/อีเมลที่ **เปิดใช้งาน** และได้เฉพาะผู้ติดต่อที่ **เปิดใช้งาน**
- `contactInfo` ค้นในที่อยู่ด้วย ค้นตัวเลขสั้น ๆ (เช่น `10`) อาจเจอคนที่มีเลขนี้ในบ้านเลขที่หรือรหัสไปรษณีย์

---

## 3. Response

### 3.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ไม่มี envelope และไม่แบ่งหน้า

- **ได้สูงสุด 100 รายการ** และ **ไม่บอกจำนวนทั้งหมด** ถ้าได้ครบ 100 แปลว่าอาจมีมากกว่านั้น
  ให้บอกผู้ใช้ระบุเงื่อนไขเพิ่ม (ดูตัวอย่างใน 4.2)
- **เรียงตามชื่อภาษาไทย แล้วตามนามสกุลภาษาไทย**
- ไม่เจอเลย → `200` กับ `[]`

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | id ของผู้ติดต่อ ใช้เป็น `accountId` ตอนเปิดเรื่อง (**อาจซ้ำกันได้** ดูข้อ 5) |
| `fullName` | string | คำนำหน้า + ชื่อ + นามสกุล (ภาษาไทย) ส่วนที่ไม่มีจะถูกข้าม เป็น `""` ถ้าไม่มีเลย |
| `salutationTh` / `firstnameTh` / `lastnameTh` | string \| null | คำนำหน้า / ชื่อ / นามสกุล ภาษาไทย |
| `salutationEn` / `firstnameEn` / `lastnameEn` | string \| null | คำนำหน้า / ชื่อ / นามสกุล ภาษาอังกฤษ ส่วนใหญ่ว่าง |
| `genderId` | string \| null | id เพศ (ตรงกับ `id` ของ `GET /ListOfValues/GetGender`) |
| `gender` | string \| null | ชื่อเพศภาษาไทย: `ชาย` / `หญิง` / `--ไม่ระบุ--` หรือ `null` |
| `birthdate` | string \| null | วันเกิด **ข้อความแบบเดิม** เดือน/วัน/ปี ค.ศ. เช่น `5/14/1988 0:00` (ดู 3.3) |
| `address` | string \| null | ที่อยู่ ส่วนใหญ่ว่าง |
| `areaId` | string \| null | id พื้นที่ (`TblArea`) จังหวัด/อำเภอ/ตำบล ([AreaId.md](AreaId.md)) |
| `areaPath` | null | **เป็น `null` เสมอใน endpoint นี้** ชื่อพื้นที่ได้จาก `GetAccountById` |
| `zipcode` | string \| null | รหัสไปรษณีย์ |
| `remark` | string \| null | หมายเหตุ |
| `isScret` | string \| null | แฟล็กปกปิดข้อมูล `"T"` / `"F"` (สะกดตาม DB) ตอนนี้ทุกคนเป็น `"F"` |
| `isEnable` | string \| null | `"T"` เสมอ (endpoint นี้ค้นเฉพาะผู้ติดต่อที่เปิดใช้งาน) เพิ่มเมื่อ 18 พ.ย. 2026 |
| `created` | string \| null | วันที่สร้าง **ข้อความแบบเดิม** เดือน/วัน/ปี ค.ศ. เช่น `10/22/2012 15:07` (ดู 3.3) |
| `contacts` | array | เบอร์/อีเมลที่เปิดใช้งานของคนนี้ (ดู 3.2) เป็น `[]` ถ้าไม่มี |

### 3.2 `contacts`

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | id ของรายการ ใช้แก้/ลบด้วย [AccountDetail.md](AccountDetail.md) |
| `detailType` | string | ประเภท (ตารางด้านล่าง) |
| `detail` | string | เบอร์หรืออีเมล |

| `detailType` | แสดงเป็น | จำนวนใน DB (16 พ.ย. 2026) |
|---|---|---|
| `MOBILE` | มือถือ | 19,258 |
| `HOME` | โทรศัพท์บ้าน | 2,371 |
| `MAIL` | อีเมล | 1,845 |
| `FAX` | โทรสาร | 1,080 |
| `OFFICE` | ที่ทำงาน | 29 |

- เรียงตาม `detailType` แบบตัวอักษร (`FAX`, `HOME`, `MAIL`, `MOBILE`, `OFFICE`) แล้วตาม `detail`
  ถ้าอยากให้มือถือขึ้นก่อน ให้เรียงใหม่ที่หน้าเว็บ
- คนเดียวมีหลายแถวประเภทเดียวกันได้ (เช่น มือถือ 2 เบอร์) และอาจมีเบอร์เดียวกันซ้ำ 2 แถว

### 3.3 วันที่เป็นข้อความแบบเดิม

`birthdate` และ `created` เป็นข้อความตามที่ระบบเดิมบันทึก ไม่ใช่ ISO 8601 **เดือนขึ้นก่อนเสมอ** และปีเป็น ค.ศ.

| ตัวอย่าง | ความหมาย |
|---|---|
| `5/14/1988 0:00` | 14 พ.ค. 1988 (วันเกิดมีเวลา `0:00` ติดมา ไม่ต้องแสดง) |
| `10/22/2012 15:07` | 22 ต.ค. 2012 15:07 |
| `07/02/2026 14:37` | 2 ก.ค. 2026 (ไม่ใช่ 7 ก.พ.) |

อย่า `new Date(text)` ตรง ๆ ให้ใช้ `parseLegacyDate` ใน 4.1

### 3.4 ตัวอย่าง

`GET /Account/GetAccounts?contactInfo=0909840822`

(ได้ 8 รายการ ตัดมาแสดง 1 รายการ รูปแบบข้อมูลเป็นของจริง ณ 16 พ.ย. 2026 ส่วนชื่อ id และเบอร์อื่นเป็นค่าสมมุติ)

```json
[
  {
    "id": "D4E5F6A7B8C94D0E8F1A2B3C4D5E6F70",
    "salutationTh": null,
    "firstnameTh": "สมหญิง",
    "lastnameTh": "ทดสอบ",
    "salutationEn": null,
    "firstnameEn": null,
    "lastnameEn": null,
    "fullName": "สมหญิง ทดสอบ",
    "genderId": "<id เพศหญิง>",
    "gender": "หญิง",
    "birthdate": "5/14/1988 0:00",
    "address": null,
    "areaId": null,
    "zipcode": null,
    "remark": null,
    "isScret": "F",
    "created": "07/02/2026 14:37",
    "contacts": [
      { "id": "<id>", "detailType": "MAIL", "detail": "somying@example.com" },
      { "id": "<id>", "detailType": "MOBILE", "detail": "0909840822" }
    ]
  }
]
```

**400: ไม่ได้ส่งเงื่อนไขเลย** (ProblemDetails ข้อความเป็นภาษาอังกฤษ ไม่ควรแสดงให้ผู้ใช้ตรง ๆ)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "": ["At least one of firstName, lastName or contactInfo is required."]
  },
  "traceId": "00-..."
}
```

### 3.5 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่พบข้อมูล `[]`) | `AccountSearchResult[]` (สูงสุด 100) |
| 400 | ไม่ส่งเงื่อนไขเลย หรือส่งแต่ค่าว่าง | ProblemDetails |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

### 4.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/account.ts
export type DetailType = "MOBILE" | "HOME" | "OFFICE" | "FAX" | "MAIL";

export interface AccountContact {
  id: string;
  detailType: DetailType | string;
  detail: string;
}

export interface AccountSearchResult {
  id: string;
  salutationTh: string | null;
  firstnameTh: string | null;
  lastnameTh: string | null;
  salutationEn: string | null;
  firstnameEn: string | null;
  lastnameEn: string | null;
  fullName: string;
  genderId: string | null;
  gender: string | null;
  birthdate: string | null; // "M/d/yyyy H:mm" แบบเดิม เดือนขึ้นก่อน
  address: string | null;
  areaId: string | null;
  areaPath: AreaItem[] | null; // null ใน GetAccounts, มีค่าใน GetAccountById (AreaItem จาก AreaId.md)
  zipcode: string | null;
  remark: string | null;
  isScret: string | null;
  isEnable: string | null; // "T" / "F" (GetAccountById ส่งคนที่ปิดใช้งานมาด้วย)
  created: string | null; // "MM/dd/yyyy HH:mm" แบบเดิม เดือนขึ้นก่อน
  contacts: AccountContact[];
}

export interface AccountSearchParams {
  firstName?: string;
  lastName?: string;
  contactInfo?: string;
}

/** API คืนได้สูงสุดเท่านี้ ถ้าได้ครบแปลว่าอาจมีมากกว่า */
export const MAX_ACCOUNT_RESULTS = 100;

export const DETAIL_TYPE_LABELS: Record<string, string> = {
  MOBILE: "มือถือ",
  HOME: "โทรศัพท์บ้าน",
  OFFICE: "ที่ทำงาน",
  FAX: "โทรสาร",
  MAIL: "อีเมล",
};

export class AccountApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export function hasAccountFilter(p: AccountSearchParams): boolean {
  return [p.firstName, p.lastName, p.contactInfo].some((v) => (v ?? "").trim() !== "");
}

export async function searchAccounts(
  params: AccountSearchParams,
  opts: { token: string; signal?: AbortSignal },
): Promise<AccountSearchResult[]> {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    const text = (value ?? "").trim();
    if (text !== "") qs.set(key, text);
  }
  if ([...qs.keys()].length === 0) throw new AccountApiError(400, "กรุณาระบุชื่อ นามสกุล หรือข้อมูลติดต่อ");

  const res = await fetch(`/api/backend/Account/GetAccounts?${qs}`, {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });

  if (res.ok) return res.json();
  if (res.status === 401) throw new AccountApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 400) throw new AccountApiError(400, "กรุณาระบุชื่อ นามสกุล หรือข้อมูลติดต่อ");
  throw new AccountApiError(res.status, "ค้นหาผู้ติดต่อไม่สำเร็จ");
}

/** แปลง "5/14/1988 0:00" หรือ "10/22/2012 15:07" (เดือน/วัน/ปี ค.ศ.) เป็น Date เวลาท้องถิ่น */
export function parseLegacyDate(text: string | null): Date | null {
  const m = text?.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4}) (\d{1,2}):(\d{2})$/);
  if (!m) return null;
  const [month, day, year, hour, minute] = m.slice(1).map(Number);
  return new Date(year, month - 1, day, hour, minute);
}

/** เรียง contacts ให้มือถือขึ้นก่อน (API เรียงตามตัวอักษรของ detailType) */
const TYPE_ORDER = ["MOBILE", "HOME", "OFFICE", "FAX", "MAIL"];
export function sortContacts(contacts: AccountContact[]): AccountContact[] {
  const rank = (t: string) => (TYPE_ORDER.indexOf(t) + 1 || TYPE_ORDER.length + 1);
  return [...contacts].sort((a, b) => rank(a.detailType) - rank(b.detailType));
}
```

### 4.2 ใช้ในหน้าค้นหา

```tsx
"use client";

import { useState } from "react";
import {
  AccountApiError, DETAIL_TYPE_LABELS, MAX_ACCOUNT_RESULTS,
  hasAccountFilter, parseLegacyDate, searchAccounts, sortContacts,
  type AccountSearchParams, type AccountSearchResult,
} from "@/lib/api/account";

export function AccountSearch(props: {
  token: string;
  onSelect?: (account: AccountSearchResult) => void; // เช่น เลือกแล้วเอา id ไปใส่ accountId ตอนเปิดเรื่อง
}) {
  const [filters, setFilters] = useState<AccountSearchParams>({});
  const [rows, setRows] = useState<AccountSearchResult[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function search() {
    setError(null);
    try {
      // ตัดขีด/ช่องว่างในเบอร์ แต่ถ้าเป็นอีเมลหรือที่อยู่ให้ส่งตามที่พิมพ์
      const info = filters.contactInfo?.trim() ?? "";
      const contactInfo = /^[\d\s-]+$/.test(info) ? info.replace(/\D/g, "") : info;
      setRows(await searchAccounts({ ...filters, contactInfo }, { token: props.token }));
    } catch (e) {
      setError(e instanceof AccountApiError ? e.message : "ค้นหาผู้ติดต่อไม่สำเร็จ");
    }
  }

  return (
    <div>
      <input placeholder="ชื่อ" value={filters.firstName ?? ""}
        onChange={(e) => setFilters({ ...filters, firstName: e.target.value })} />
      <input placeholder="นามสกุล" value={filters.lastName ?? ""}
        onChange={(e) => setFilters({ ...filters, lastName: e.target.value })} />
      <input placeholder="เบอร์โทร / อีเมล / ที่อยู่" value={filters.contactInfo ?? ""}
        onChange={(e) => setFilters({ ...filters, contactInfo: e.target.value })} />
      <button disabled={!hasAccountFilter(filters)} onClick={search}>ค้นหา</button>

      {error && <p role="alert">{error}</p>}
      {rows?.length === 0 && <p>ไม่พบข้อมูล</p>}
      {rows?.length === MAX_ACCOUNT_RESULTS && (
        <p>แสดง {MAX_ACCOUNT_RESULTS} รายการแรก กรุณาระบุเงื่อนไขเพิ่มเพื่อให้แคบลง</p>
      )}

      <table>
        <tbody>
          {rows?.map((a, i) => (
            // id ซ้ำกันได้ (ดูข้อ 5) จึงต่อ index เข้าไปใน key
            <tr key={`${a.id}-${i}`} onClick={() => props.onSelect?.(a)}>
              <td>{a.fullName || "-"}</td>
              <td>{a.gender ?? "-"}</td>
              <td>{parseLegacyDate(a.birthdate)?.toLocaleDateString("th-TH") ?? "-"}</td>
              <td>
                {sortContacts(a.contacts).map((c, j) => (
                  <div key={j}>{DETAIL_TYPE_LABELS[c.detailType] ?? c.detailType}: {c.detail}</div>
                ))}
              </td>
              <td>{a.address || "-"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

`toLocaleDateString("th-TH")` แสดงปีเป็น พ.ศ. ให้อยู่แล้ว เช่น `14/5/2531`

---

## 5. ข้อควรรู้ (สถานะ ณ 16 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ได้สูงสุด 100 รายการ ไม่มีจำนวนทั้งหมด ไม่แบ่งหน้า** | ถ้าได้ครบ 100 ให้บอกผู้ใช้ระบุเงื่อนไขเพิ่ม ถ้าต้องการ paging หรือจำนวนทั้งหมด แจ้ง backend |
| **`id` ซ้ำกันได้** | ข้อมูลเดิมมีผู้ติดต่อที่ใช้ `id` ซ้ำกันราว 536 แถว (จาก 19,965) อาจได้ `id` เดียวกัน 2 แถวในผลเดียว และสองแถวนั้นจะได้ `contacts` ชุดเดียวกัน **อย่าใช้ `id` อย่างเดียวเป็น React key** |
| **แยกตัวพิมพ์เล็ก/ใหญ่** | ชื่อภาษาอังกฤษต้องพิมพ์ตัวพิมพ์ให้ตรง |
| **ไม่มีชื่อพื้นที่ในรายการ** | `areaPath` เป็น `null` ใน endpoint นี้ ใช้ `areaPath` ของ `GetAccountById` ([AreaId.md](AreaId.md)) |
| **`isScret` ไม่มีผลกับ API** | API ส่งข้อมูลครบทุกคนไม่ว่าค่านี้จะเป็นอะไร ถ้ามีกติกาว่าต้องปกปิดข้อมูลคนที่เป็น `"T"` ต้องทำที่หน้าเว็บ (ตอนนี้ทุกคนเป็น `"F"`) |
| **วันที่เป็นข้อความ เดือน/วัน/ปี** | ใช้ `parseLegacyDate` อย่า `new Date()` ตรง ๆ |
| **ข้อมูลส่วนบุคคล** | response มีชื่อ เบอร์ อีเมล ที่อยู่ วันเกิด อย่าเก็บลง `localStorage` หรือ log |

ถ้า backend ปรับข้อใดข้างบน จะแจ้งและอัปเดตเอกสารนี้

---

## 6. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/Account/GetAccounts?contactInfo=0909840822
Authorization: Bearer <token>
```

```bash
curl -G "http://localhost:5018/Account/GetAccounts" \
  --data-urlencode "firstName=สม" --data-urlencode "lastName=ใจ" \
  -H "Authorization: Bearer <token>"
```

| Query | ผลที่ควรได้ (ข้อมูล ณ 16 พ.ย. 2026) |
|---|---|
| `contactInfo=0909840822` | 8 รายการ |
| `firstName=สม` | 100 รายการ (ชนเพดาน) |
| ไม่ส่งอะไรเลย | 400 |
