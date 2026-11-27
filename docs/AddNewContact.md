# Add Contact API (`POST /Contact/AddNewContact`) สำหรับ Frontend

ใช้บันทึก **การรับสาย** (เบอร์โทรเข้า + ช่องทางติดต่อ + ประเภทการรับสาย) **1 สาย = 1 แถวใน `TblContact`**
บันทึกตอนกดปุ่มหลักของกล่องยืนยันในหน้ารับสาย (ดู "ลำดับการทำงานของหน้ารับสาย" ข้อ 1)
`id` ที่ได้คือ `contactId` ที่ต้องส่งให้ `AddNewService` และ `AddNewServiceOperations`

> อ้างอิงโค้ด: `Controllers/ContactController.cs`, `Dtos/CreateContactRequest.cs`, `Dtos/ContactResponse.cs`
> เอกสารที่ต้องใช้คู่กัน: [GetChannel.md](GetChannel.md) (ตัวเลือกช่องทาง), [GetContactType.md](GetContactType.md) (ตัวเลือกประเภทการรับสาย),
> [AddNewService.md](AddNewService.md) และ [AddNewServiceOperations.md](AddNewServiceOperations.md) (ใช้ `contactId`)
>
> **ปรับ 26 พ.ย. 2569:** กำหนดจุดที่ต้องเรียก API นี้ในแต่ละกรณี (ข้อ 1) เพราะ `AddNewService` / `AddNewServiceOperations`
> บังคับ `contactId` แล้ว และช่อง "จำนวนสาย" ไม่ถูกเก็บ
>
> **ปรับ 27 พ.ย. 2569:** `contactEnd` ถูกเลื่อนเป็นเวลาบันทึกเรื่องเมื่อเปิดเรื่องใหม่จากสายนี้ (เหมือนระบบเดิม) และต้องเริ่มนับ
> `contactStart` ใหม่ทุกสาย (ข้อ 5.1)

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `POST` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Header | `Content-Type: application/json` |
| Backend (dev) | `http://localhost:5018/Contact/AddNewContact` |
| URL จาก Next.js | `/api/backend/Contact/AddNewContact` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

### ลำดับการทำงานของหน้ารับสาย

**1 สาย = 1 แถว** บันทึกตอนกด **ปุ่มหลักของกล่องยืนยัน** ไม่ใช่ตอนกด "ยืนยันข้อมูล" และไม่ใช่ตอนเปิดเรื่อง

1. เปิดหน้า → โหลด `GET /ListOfValues/GetChannel` และ `GET /ListOfValues/GetContactType`
   แล้ว **จำเวลาที่เริ่มรับสาย** ไว้ใช้เป็น `contactStart`
2. ผู้ใช้กรอกเบอร์ เลือกช่องทาง และเลือกประเภทการรับสาย ถ้าเป็น Service แสดงผู้ติดต่อของเบอร์นี้จาก
   `GET /Account/GetAccountByPhoneNo` ให้เลือก
3. กด "ยืนยันข้อมูล" → แสดงกล่องยืนยัน **ยังไม่บันทึก** (กด "กลับไปแก้ไข" ได้โดยไม่มีอะไรถูกบันทึก)
4. กดปุ่มหลักในกล่อง → `POST /Contact/AddNewContact` แล้วไปขั้นต่อไปพร้อม `id` ที่ได้ (`contactId`)

| กรณี | กล่องยืนยัน | ปุ่มที่บันทึก | หลังได้ `201` |
|---|---|---|---|
| Service + เลือกผู้ติดต่อแล้ว | ยืนยันข้อมูล | **ค้นหาข้อมูลบริการ** | ไปหน้าค้นหาบริการของเบอร์นี้พร้อม `contactId` แล้ว**เปิดเรื่องใหม่** (`POST /Services/AddNewService`) หรือ**เพิ่มการดำเนินการในเรื่องเดิม** (`POST /Services/AddNewServiceOperations`) โดยส่ง `contactId` นี้ |
| Service + เบอร์นี้ไม่มีผู้ติดต่อ | ไม่มีข้อมูลผู้ติดต่อ | **เพิ่มข้อมูลผู้ติดต่อ** | ไปหน้าเพิ่มผู้ติดต่อพร้อม `contactId` → `POST /Account/AddNewAccount` → ค้นหา/เปิดเรื่องด้วย `contactId` เดิม |
| Non Service | ยืนยันการบันทึก | **ยืนยันการบันทึก** | จบสาย ล้างฟอร์มรอสายถัดไป |

#### ทำไมบันทึกตอนนี้ ไม่รอตอนเปิดเรื่อง

- `TblContact` คือบันทึกการรับสาย ไม่ใช่บันทึกเรื่อง สายที่ไม่ได้เปิดเรื่องใหม่ (โทรมาติดตามเรื่องเดิม, สอบถาม,
  วางสายระหว่างค้นหา) ก็ต้องถูกนับ ถ้ารอเปิดเรื่อง สายเหล่านี้จะหายไป
- การเพิ่มการดำเนินการในเรื่องเดิมก็บังคับ `contactId` ถ้ารอเปิดเรื่องจะไม่มีค่าให้ใช้
- ระบบเดิมก็บันทึกตั้งแต่รับสาย รวมสาย Service (ใน DB มีแถว New Service มากกว่า 1,200 แถว) แล้วใช้ค่านี้กับงานทุกงานของสายนั้น
- กรณีไม่มีผู้ติดต่อ บันทึก**ก่อน**ไปหน้าเพิ่มผู้ติดต่อ เพราะ `TblContact` ไม่เก็บผู้ติดต่อ และถ้าผู้โทรวางสายก่อนกรอกเสร็จ
  สายนี้ยังต้องถูกนับ
- ผลที่ตามมา: จะมีแถว New Service ที่ไม่มีเรื่องตามมา (เป็นสายจริง ถูกต้องแล้ว) และ `contactEnd` เริ่มจากเวลาที่กดปุ่มในกล่อง
  แล้วเลื่อนเป็นเวลาบันทึกเรื่องเมื่อเปิดเรื่องใหม่จากสายนี้ ไม่ใช่เวลาวางสาย (ข้อ 5.1)

#### กติกาที่ต้องทำตาม

1. **ปิดปุ่มระหว่างรอ response** กดสองครั้ง = บันทึกสองแถว และรายงาน 05/06 นับจำนวนแถว
2. **ส่ง `contactId` ต่อทุกหน้าของสายนี้** เช่น ใส่ใน URL (`...?contactId=<id>`) รีเฟรชแล้วไม่หาย และไม่ปนกับสายอื่น
3. **ย้อนกลับมาหน้ารับสายในสายเดิมแล้วยืนยันอีกครั้ง:** ถ้าเบอร์ ช่องทาง และประเภทเหมือนเดิม ให้ใช้ `contactId` เดิม
   ไม่เรียก API นี้ซ้ำ (เปลี่ยนแค่ผู้ติดต่อก็ใช้ id เดิม เพราะ `TblContact` ไม่เก็บผู้ติดต่อ) ถ้าเปลี่ยนประเภทจะได้แถวใหม่
   (ยังไม่มี API แก้แถวเดิม) ตัวอย่างการจำสายปัจจุบันอยู่ในข้อ 4.2
4. **ปุ่ม "เพิ่มข้อมูลผู้ติดต่อ" ที่มุมขวาของรายการผู้ติดต่อ** (เบอร์นี้มีผู้ติดต่อแล้ว แต่ผู้โทรเป็นคนใหม่) **ไม่บันทึกสาย**
   เพิ่มเสร็จให้กลับมาหน้ารับสาย เลือกคนใหม่ แล้วยืนยันตามปกติ
5. **เปิดเรื่องใหม่ต้องเริ่มจากหน้ารับสาย** เพราะ `AddNewService` บังคับ `contactId` ส่วนเรื่องเดิมที่เปิดจากหน้าแรกหรือหน้าค้นหา
   (ไม่ได้อยู่ในสาย) ใช้ `contactId` ของการดำเนินการเดิมในเรื่อง ดู [AddNewServiceOperations.md](AddNewServiceOperations.md) ข้อ 2.4
6. **สายใหม่ต้องไม่มี `contactId` ของสายก่อน** ล้างค่าเดิมหลังบันทึก Non Service และเมื่อเริ่มรับสายใหม่
   ไม่งั้นสายใหม่จากเบอร์เดิมด้วยประเภทเดิมจะไปใช้ id ของสายก่อน (นับขาด)
   และเริ่มนับ `contactStart` ใหม่ในจังหวะเดียวกัน เพราะรายงาน 05/06 นับสายตามวันที่ของ `contactStart` (ข้อ 5.1)
7. **ช่อง "จำนวนสาย" ไม่ถูกเก็บ** API ไม่รับค่านี้ กดยืนยัน 1 ครั้ง = 1 แถว ช่องนี้จึงไม่มีผลกับข้อมูลที่บันทึก

---

## 2. Request

### 2.1 Field

| Field | Type | บังคับ | มาจากช่องในฟอร์ม | กติกา |
|---|---|---|---|---|
| `contactDetail` | string | ✅ | เบอร์โทรเข้า | ไม่เกิน **50** ตัวอักษร ช่องว่างหน้าหลังถูกตัดให้ ช่องว่างล้วนถือว่าไม่ได้กรอก |
| `channelId` | string | ✅ | ช่องทางติดต่อ | `id` จาก `GetChannel` |
| `categoryId` | string | ✅ | ประเภทการรับสาย | `id` ของ **leaf** จาก `GetContactType` (ดู 2.2) |
| `contactStart` | string | – | (ไม่มีในฟอร์ม) | เวลาเริ่มรับสาย `yyyy-MM-ddTHH:mm:ss` เป็น **เวลาไทย ไม่มี timezone** (ดู 2.3) ถ้าไม่ส่งจะใช้เวลาตอนบันทึก |

ชื่อ field ไม่สนตัวพิมพ์เล็ก/ใหญ่ ส่วนค่าอื่น server ใส่ให้เอง: `id` (UUID), `createdBy`
(พนักงานจาก token) และ `contactEnd` (เวลาที่บันทึก ถ้าเปิดเรื่องใหม่จากสายนี้จะถูกเลื่อนเป็นเวลาบันทึกเรื่อง ดูข้อ 5.1)

### 2.2 `categoryId` ต้องเป็น leaf

ส่ง id ของ **รายการสุดท้ายที่ผู้ใช้เลือก** เท่านั้น:

| ผู้ใช้เลือก | ส่ง `categoryId` เป็น |
|---|---|
| radio ที่ไม่มี drop-down (New Service, Follow Up, สัญญาณไม่ชัดเจน, สายหลุด) | id ของ radio |
| radio ที่มี drop-down (Out of Scope, โทรผิด, โทรเล่น) แล้วเลือกใน drop-down | id ของรายการใน drop-down |

ส่งแบบนี้ได้ **400**:

- id ของหัวคอลัมน์ `Service` / `Non Service`
- id ของ radio ที่มี drop-down (เช่น `โทรเล่น` เฉย ๆ โดยไม่เลือก drop-down)
- id ที่ไม่ใช่ประเภทการรับสาย เช่น ประเภทบริการ (Q&A, Claim, …)

ใช้ `resolveLeafId()` ใน [GetContactType.md ข้อ 4.2](GetContactType.md) หาค่านี้ได้เลย

### 2.3 กติกาเวลา `contactStart`

- เป็น **เวลาไทย ไม่มี timezone**: `"2026-11-12T10:15:00"`
- ❌ ห้ามใช้ `date.toISOString()` เพราะได้ UTC ลงท้ายด้วย `Z` (`2026-11-12T03:15:00.000Z`) → 400
- ❌ ห้ามมี offset เช่น `+07:00` → 400
- ❌ ห้ามใช้ปี **พ.ศ.** (เช่น locale `th-TH`) และห้ามใช้รูปแบบ `12/11/2026 10:15` → 400
- ห้ามเป็นเวลาในอนาคต (เผื่อนาฬิกาเครื่องเดินเร็วได้ไม่เกิน 1 นาที)
- เศษวินาทีถูกตัดทิ้ง
- รายงาน 05/06 นับสายตาม**วันที่**ของค่านี้ จึงต้องเริ่มนับใหม่ทุกสาย (ข้อ 1 กติกาข้อ 6)

ฟังก์ชันแปลง `Date` เป็นรูปแบบนี้ โดยบังคับเป็นเวลาไทยไม่ว่าเครื่องผู้ใช้จะตั้ง timezone อะไรไว้:

```ts
// lib/thai-time.ts
/** Date -> "yyyy-MM-ddTHH:mm:ss" เวลาไทย (Asia/Bangkok) ปี ค.ศ. ไม่มี timezone */
export function toThaiLocalIso(date: Date): string {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat("en-CA", {
      timeZone: "Asia/Bangkok",
      year: "numeric", month: "2-digit", day: "2-digit",
      hour: "2-digit", minute: "2-digit", second: "2-digit",
      hourCycle: "h23",
    }).formatToParts(date).map((p) => [p.type, p.value]),
  );
  return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}:${parts.second}`;
}
```

### 2.4 ตัวอย่าง

```json
{
  "contactDetail": "0812225566",
  "channelId": "332E6D76F258C8294B160C20642AB9F5",
  "categoryId": "0168B738100C4CBAB9AA45906A200013",
  "contactStart": "2026-11-12T10:15:00"
}
```

(ช่องทาง Phone, ประเภท โทรเล่น > เด็กโทรเล่น)

---

## 3. Response

### 3.1 สำเร็จ: `201 Created`

คืนแถวที่บันทึก พร้อมชื่อช่องทางและชื่อประเภทการรับสาย ใช้แสดงข้อความยืนยันได้เลย

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | id ของการรับสายครั้งนี้ (hex 32 ตัว) |
| `contactDetail` | string | เบอร์ที่บันทึก (ตัดช่องว่างแล้ว) |
| `channelId` | string | |
| `channelNameTh` | string \| null | เช่น `"Phone"` |
| `categoryId` | string | |
| `categoryNameTh` | string \| null | ชื่อของ leaf ที่เลือก เช่น `"เด็กโทรเล่น"` |
| `contactStart` | string | เวลาเริ่ม (เวลาไทย ไม่มี timezone) |
| `contactEnd` | string | เวลาที่บันทึก (เวลาไทย ไม่มี timezone) ถ้าเปิดเรื่องใหม่จากสายนี้ภายหลัง ค่าใน DB จะถูกเลื่อนเป็นเวลาบันทึกเรื่อง (ข้อ 5.1) |
| `createdBy` | string | id ของพนักงานที่รับสาย |

```json
{
  "id": "6F1C2B0E9A4D4E2B8C7A5D3E1F0B9A87",
  "contactDetail": "0812225566",
  "channelId": "332E6D76F258C8294B160C20642AB9F5",
  "channelNameTh": "Phone",
  "categoryId": "0168B738100C4CBAB9AA45906A200013",
  "categoryNameTh": "เด็กโทรเล่น",
  "contactStart": "2026-11-12T10:15:00",
  "contactEnd": "2026-11-12T10:16:42",
  "createdBy": "5002EEF582684F6C9DEC3A3D06D21422"
}
```

### 3.2 ข้อมูลไม่ถูกต้อง: `400 Bad Request`

body เป็น ProblemDetails มาตรฐานของ ASP.NET Core ข้อความ error อยู่ใน `errors` แยกตามชื่อ field
(**คีย์เป็นตัวพิมพ์ใหญ่นำ** เช่น `CategoryId`) ข้อความเป็นภาษาอังกฤษสำหรับ debug
ให้แสดงข้อความภาษาไทยของ frontend เองตามคีย์

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "CategoryId": [
      "'0168B738100C4CBAB9AA45906A200005' is not a selectable call type. Use the id of a leaf from GET /ListOfValues/GetContactType."
    ]
  },
  "traceId": "00-..."
}
```

| คีย์ใน `errors` | สาเหตุ | ข้อความแนะนำให้แสดง |
|---|---|---|
| `ContactDetail` | ไม่ได้กรอก / ช่องว่างล้วน / ยาวเกิน 50 | กรุณากรอกเบอร์โทรเข้า |
| `ChannelId` | ไม่ได้เลือก / ช่องทางไม่มีหรือถูกปิดใช้งาน | กรุณาเลือกช่องทางติดต่อ |
| `CategoryId` | ไม่ได้เลือก / ไม่ใช่ leaf / ไม่ใช่ประเภทการรับสาย | กรุณาเลือกประเภทการรับสายให้ครบ |
| `ContactStart` | มี `Z` หรือ offset / เป็นเวลาในอนาคต | (บั๊กฝั่ง frontend: ตรวจ `toThaiLocalIso`) |
| `$.contactStart`, `request` | JSON อ่านค่าไม่ได้ เช่น วันที่ผิดรูปแบบ | (บั๊กฝั่ง frontend) |

### 3.3 Status code ทั้งหมด

| HTTP | เมื่อไร | Body |
|---|---|---|
| 201 | บันทึกสำเร็จ | `ContactResponse` |
| 400 | ข้อมูลไม่ถูกต้อง (ข้อ 3.2) | ProblemDetails |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 404 | backend ที่รันอยู่ยังเป็นเวอร์ชันเก่า (ให้ backend restart) | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

### 4.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/contact.ts
export interface AddNewContactRequest {
  contactDetail: string;
  channelId: string;
  categoryId: string;
  contactStart?: string; // "yyyy-MM-ddTHH:mm:ss" เวลาไทย
}

export interface ContactResponse {
  id: string;
  contactDetail: string;
  channelId: string;
  channelNameTh: string | null;
  categoryId: string;
  categoryNameTh: string | null;
  contactStart: string;
  contactEnd: string;
  createdBy: string;
}

export class ContactApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public fieldErrors?: Record<string, string[]>,
  ) { super(message); }
}

const FIELD_MESSAGES: Record<string, string> = {
  ContactDetail: "กรุณากรอกเบอร์โทรเข้า",
  ChannelId: "กรุณาเลือกช่องทางติดต่อ",
  CategoryId: "กรุณาเลือกประเภทการรับสายให้ครบ",
};

export async function addNewContact(
  body: AddNewContactRequest,
  opts: { token: string; signal?: AbortSignal },
): Promise<ContactResponse> {
  const res = await fetch("/api/backend/Contact/AddNewContact", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${opts.token}`,
    },
    body: JSON.stringify(body),
    signal: opts.signal,
  });

  if (res.status === 201) return res.json();

  if (res.status === 400) {
    const problem = await res.json().catch(() => null);
    const errors = (problem?.errors ?? {}) as Record<string, string[]>;
    const firstKey = Object.keys(errors)[0];
    throw new ContactApiError(400, FIELD_MESSAGES[firstKey] ?? "ข้อมูลไม่ถูกต้อง", errors);
  }
  if (res.status === 401) throw new ContactApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  throw new ContactApiError(res.status, "บันทึกการรับสายไม่สำเร็จ");
}
```

### 4.2 ใช้ในฟอร์มรับสาย

ใช้ร่วมกับ `ChannelSelect` ([GetChannel.md](GetChannel.md)) และ `ContactTypePicker`
([GetContactType.md](GetContactType.md)) กด "ยืนยันข้อมูล" แค่เปิดกล่องยืนยัน ส่วนการบันทึกอยู่ที่ปุ่มหลักของกล่อง (ข้อ 1)

จำสายที่กำลังรับไว้ใน `sessionStorage` ให้อยู่รอดเมื่อเปลี่ยนหน้าหรือย้อนกลับ (กติกาข้อ 3 และ 6):

```ts
// lib/current-call.ts
const KEY = "currentCall";

export interface CurrentCall {
  contactId: string;
  /** เบอร์|ช่องทาง|ประเภท ตอนบันทึก ถ้ายืนยันซ้ำด้วยค่าเดิมให้ใช้ contactId เดิม */
  formKey: string;
}

export const currentCall = {
  get(): CurrentCall | null {
    try { return JSON.parse(sessionStorage.getItem(KEY) ?? "null"); } catch { return null; }
  },
  set(value: CurrentCall) { sessionStorage.setItem(KEY, JSON.stringify(value)); },
  /** เรียกเมื่อเริ่มรับสายใหม่ และหลังบันทึก Non Service */
  clear() { sessionStorage.removeItem(KEY); },
};
```

```tsx
"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { addNewContact, ContactApiError } from "@/lib/api/contact";
import { currentCall } from "@/lib/current-call";
import { toThaiLocalIso } from "@/lib/thai-time";
import { ChannelSelect } from "@/components/ChannelSelect";
import { ContactTypePicker } from "@/components/ContactTypePicker";

export function ReceiveCallForm({ token, serviceLeafIds }: {
  token: string;
  /** id ของ leaf ใต้ root "Service" จาก GetContactType (New Service, Follow Up) */
  serviceLeafIds: Set<string>;
}) {
  const router = useRouter();
  // เวลาเริ่มรับสาย: ตอนเปิดหน้า และเริ่มใหม่ทุกครั้งที่ล้างฟอร์มรอสายถัดไป (รายงาน 05/06 นับตามวันที่ของค่านี้)
  const startedAt = useRef(new Date());

  const [contactDetail, setContactDetail] = useState("");
  const [channelId, setChannelId] = useState("");
  const [categoryId, setCategoryId] = useState<string | null>(null);
  const [accountId, setAccountId] = useState<string | null>(null); // ผู้ติดต่อที่เลือก (Service)
  const [confirming, setConfirming] = useState(false);              // กล่องยืนยันเปิดอยู่
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isService = categoryId !== null && serviceLeafIds.has(categoryId);
  const canSubmit = contactDetail.trim() !== "" && channelId !== "" && categoryId !== null;

  // "ยืนยันข้อมูล": เปิดกล่องยืนยันเท่านั้น ยังไม่บันทึก
  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (canSubmit) setConfirming(true);
  }

  // ปุ่มหลักในกล่อง: "ค้นหาข้อมูลบริการ" / "เพิ่มข้อมูลผู้ติดต่อ" / "ยืนยันการบันทึก"
  async function onConfirm() {
    if (saving) return;
    setSaving(true); // กันกดซ้ำ: กดสองครั้ง = บันทึกสองแถว
    setError(null);
    try {
      const body = {
        contactDetail: contactDetail.trim(),
        channelId,
        categoryId: categoryId!,
        contactStart: toThaiLocalIso(startedAt.current),
      };
      // ย้อนกลับมายืนยันสายเดิมด้วยเบอร์ ช่องทาง และประเภทเดิม = ใช้ id เดิม ไม่บันทึกซ้ำ
      const formKey = `${body.contactDetail}|${body.channelId}|${body.categoryId}`;
      const previous = currentCall.get();
      const contactId = previous?.formKey === formKey
        ? previous.contactId
        : (await addNewContact(body, { token })).id;

      if (!isService) {
        currentCall.clear();            // Non Service: จบสาย
        startedAt.current = new Date(); // สายถัดไปเริ่มนับเวลาใหม่
        // ล้างฟอร์มรอสายถัดไป ...
        return;
      }

      currentCall.set({ contactId, formKey });
      const q = new URLSearchParams({ phone: body.contactDetail, contactId });
      if (accountId) {
        q.set("accountId", accountId);
        router.push(`/services/search?${q}`); // ค้นหาข้อมูลบริการ (เส้นทางเป็นตัวอย่าง)
      } else {
        router.push(`/accounts/new?${q}`);    // เพิ่มข้อมูลผู้ติดต่อ (เส้นทางเป็นตัวอย่าง)
      }
    } catch (err) {
      setError(err instanceof ContactApiError ? err.message : "บันทึกการรับสายไม่สำเร็จ");
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={onSubmit}>
      <input
        placeholder="เบอร์โทรเข้า *"
        value={contactDetail}
        maxLength={50}
        onChange={(e) => setContactDetail(e.target.value)}
      />
      <ChannelSelect token={token} value={channelId} onChange={setChannelId} />
      <ContactTypePicker token={token} onChange={setCategoryId} />
      {/* Service: รายการผู้ติดต่อจาก GET /Account/GetAccountByPhoneNo → setAccountId */}

      {error && <p role="alert">{error}</p>}
      <button type="submit" disabled={!canSubmit}>ยืนยันข้อมูล</button>

      {confirming && (
        <ConfirmDialog
          primaryLabel={!isService ? "ยืนยันการบันทึก" : accountId ? "ค้นหาข้อมูลบริการ" : "เพิ่มข้อมูลผู้ติดต่อ"}
          busy={saving}
          onPrimary={onConfirm}
          onBack={() => setConfirming(false)} // "กลับไปแก้ไข": ยังไม่มีอะไรถูกบันทึก
        />
      )}
    </form>
  );
}
```

หน้าถัดไปอ่าน `contactId` จาก URL แล้วส่งเป็น `contactId` ของ `AddNewService` / `AddNewServiceOperations`

---

## 5. ข้อควรรู้ (ปรับล่าสุด 27 พ.ย. 2569)

- **กดซ้ำ = บันทึกซ้ำ**: API ไม่กันรายการซ้ำ ต้องปิดปุ่มระหว่างรอ response (ตามตัวอย่าง `saving`)
  และใช้ `contactId` เดิมเมื่อยืนยันสายเดิมซ้ำ (ข้อ 1 กติกาข้อ 3)
- **API ไม่ตรวจรูปแบบเบอร์**: ตรวจแค่ว่ากรอกและยาวไม่เกิน 50 ถ้าต้องการบังคับรูปแบบเบอร์โทร
  (เช่น ช่องทาง Phone ต้องเป็นตัวเลขขึ้นต้นด้วย 0) ให้ตรวจที่ frontend ไปก่อน
  และ API **ไม่ลบขีด/ช่องว่างกลางเบอร์** ให้ ควรส่งเป็นตัวเลขล้วน (`0812225566`)
  เพื่อให้ค้นตรงกับ `GET /Account/GetAccountByPhoneNo` ได้
- **ช่องทางเริ่มต้น**: design ตั้งเป็น Phone แต่ `GetChannel` ยังไม่บอกว่าตัวไหนเป็นค่าเริ่มต้น
- **ผูกกับการเปิดเรื่องด้วย `contactId`** (บังคับตั้งแต่ 26 พ.ย. 2569): `id` จาก response ต้องส่งเป็น `contactId` ของ
  `POST /Services/AddNewService` (backend ใส่ให้การดำเนินการรายการแรกของเรื่อง) และ `POST /Services/AddNewServiceOperations`
  ไม่ส่ง = 400
- **ช่อง "จำนวนสาย" ไม่ถูกเก็บ**: `TblContact` ไม่มีคอลัมน์นี้และ API ไม่รับค่านี้ 1 การยืนยัน = 1 แถว
- **Follow Up**: `GetContactType` ยังส่ง Follow Up มา (เปิดใช้งาน) ถ้าหน้ารับสายแสดงแค่ New Service สายที่โทรมาติดตามเรื่องเดิม
  จะถูกบันทึกเป็น New Service ถ้าอยากแยกสองแบบนี้ ให้แสดง Follow Up ด้วยเหมือนระบบเดิม (ไม่กระทบรายงานปัจจุบัน)
- **อย่าเอา `categoryId` ของหน้านี้ไปใส่ใน `AddNewService`**: field นั้นคือประเภทบริการ คนละชุดกัน
- **เวลาเริ่มและเวลาจบของสาย** (`contactStart` / `contactEnd`): ดู 5.1

### 5.1 `contactStart` และ `contactEnd` (27 พ.ย. 2569)

| Field | ความหมาย | ใส่ / เปลี่ยนตอนไหน |
|---|---|---|
| `contactStart` | เวลาเริ่มรับสาย รายงาน 05/06 นับสายตาม**วันที่**ของค่านี้ | ใส่ครั้งเดียวตอน `AddNewContact` จากค่าที่ frontend ส่ง (ไม่ส่ง = เวลาบันทึก) ไม่มีที่ไหนแก้ |
| `contactEnd` | เวลาที่จัดการสายนี้เสร็จ ไม่ใช่เวลาวางสาย (ระบบไม่รู้เวลาวางสาย) | `AddNewContact` ใส่เวลาบันทึก ถ้าเปิดเรื่องใหม่ด้วย `contactId` นี้ `AddNewService` จะเลื่อนเป็นเวลาบันทึกเรื่อง (เหมือนระบบเดิม และเลื่อนไปข้างหน้าเท่านั้น) |

ตัวอย่าง สายเริ่ม 10:00:00:

| กรณี | `contactEnd` สุดท้าย |
|---|---|
| Non Service กด "ยืนยันการบันทึก" 10:00:40 | 10:00:40 |
| กด "ค้นหาข้อมูลบริการ" 10:00:30 แล้วบันทึกเรื่องใหม่ 10:05:10 | 10:05:10 |
| กด "เพิ่มข้อมูลผู้ติดต่อ" 10:00:30 เพิ่มผู้ติดต่อ แล้วบันทึกเรื่องใหม่ 10:08:00 | 10:08:00 |
| กด "ค้นหาข้อมูลบริการ" 10:00:30 แล้วเพิ่มการดำเนินการในเรื่องเดิม | 10:00:30 |
| เปิด 2 เรื่องในสายเดียว (บันทึก 10:05:10 และ 10:12:00) | 10:12:00 |
| วางสายก่อนเปิดเรื่อง | 10:00:30 |

- การเพิ่มการดำเนินการในเรื่องเดิม (`AddNewServiceOperations`) **ไม่เลื่อน** `contactEnd` เพราะเรียกจากนอกสายได้ โดยใช้ `contactId`
  ของการดำเนินการเดิม ([AddNewServiceOperations.md](AddNewServiceOperations.md) ข้อ 2.4) ถ้าเลื่อน เวลาจบของสายเก่าจะไปอยู่หลายวันต่อมา
- **เริ่มนับ `contactStart` ตอนไหน:** ตอนเปิดหน้ารับสาย (ระบบโทรศัพท์เปิดหน้าพร้อมเบอร์ตอนสายเข้า) และเริ่มใหม่ทุกครั้งที่ล้างฟอร์ม
  รอสายถัดไป (ข้อ 1 กติกาข้อ 6) ถ้าเปิดหน้าค้างไว้รอนานแล้วกรอกสายเอง เวลาที่รอจะถูกนับรวม และถ้าค้างข้ามวัน
  รายงาน 05/06 จะนับสายนั้นเป็นของวันก่อน
- response 201 ของ API นี้ส่ง `contactEnd` ณ ตอนบันทึก ค่าที่ถูกเลื่อนภายหลังยังไม่มี API ไหนส่งกลับ
  (ยังไม่มีหน้าจอหรือรายงานที่ใช้ค่านี้)

---

## 6. ทดสอบด้วย REST Client / curl

ตัวอย่างครบทุกกรณี (สำเร็จ 7 แบบ, 400 10 แบบ, 401) อยู่ใน `ContactTogetherApi.http`
หัวข้อ `AddNewContact` (กรณีสำเร็จเขียนแถวจริงลง DB และใช้เบอร์ `0899999999` ไว้หาแถวทดสอบ)

```http
POST http://localhost:5018/Contact/AddNewContact
Authorization: Bearer <token>
Content-Type: application/json

{
  "contactDetail": "0812225566",
  "channelId": "332E6D76F258C8294B160C20642AB9F5",
  "categoryId": "0168B738100C4CBAB9AA45906A200003"
}
```

```bash
curl -X POST http://localhost:5018/Contact/AddNewContact \
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
  -d '{"contactDetail":"0812225566","channelId":"332E6D76F258C8294B160C20642AB9F5","categoryId":"0168B738100C4CBAB9AA45906A200003"}'
```
