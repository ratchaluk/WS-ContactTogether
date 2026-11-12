# Add Contact API (`POST /Contact/AddNewContact`) สำหรับ Frontend

ใช้บันทึก **ฟอร์มรับสาย** (เบอร์โทรเข้า + ช่องทางติดต่อ + ประเภทการรับสาย) กดยืนยัน 1 ครั้ง
ได้ 1 แถวใน `TblContact`

> อ้างอิงโค้ด: `Controllers/ContactController.cs`, `Dtos/CreateContactRequest.cs`, `Dtos/ContactResponse.cs`
> เอกสารที่ต้องใช้คู่กัน: [GetChannel.md](GetChannel.md) (ตัวเลือกช่องทาง), [GetContactType.md](GetContactType.md) (ตัวเลือกประเภทการรับสาย)

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

1. เปิดหน้า → โหลด `GET /ListOfValues/GetChannel` และ `GET /ListOfValues/GetContactType`
   แล้ว **จำเวลาที่เริ่มรับสาย** ไว้ใช้เป็น `contactStart`
2. ผู้ใช้กรอกเบอร์ เลือกช่องทาง และเลือกประเภทการรับสาย
3. กด "ยืนยันข้อมูล" → `POST /Contact/AddNewContact`
4. ได้ `201` → แสดงผลสำเร็จ หรือไปขั้นตอนถัดไป

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
(พนักงานจาก token) และ `contactEnd` (เวลาที่บันทึก)

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
| `contactEnd` | string | เวลาที่บันทึก (เวลาไทย ไม่มี timezone) |
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
([GetContactType.md](GetContactType.md))

```tsx
"use client";

import { useRef, useState } from "react";
import { addNewContact, ContactApiError } from "@/lib/api/contact";
import { toThaiLocalIso } from "@/lib/thai-time";
import { ChannelSelect } from "@/components/ChannelSelect";
import { ContactTypePicker } from "@/components/ContactTypePicker";

export function ReceiveCallForm({ token }: { token: string }) {
  // เวลาที่เปิดฟอร์ม = เวลาเริ่มรับสาย
  const startedAt = useRef(new Date());

  const [contactDetail, setContactDetail] = useState("");
  const [channelId, setChannelId] = useState("");
  const [categoryId, setCategoryId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = contactDetail.trim() !== "" && channelId !== "" && categoryId !== null && !saving;

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit) return;

    setSaving(true); // กันกดซ้ำ: กดสองครั้ง = บันทึกสองแถว
    setError(null);
    try {
      const saved = await addNewContact(
        {
          contactDetail: contactDetail.trim(),
          channelId,
          categoryId: categoryId!,
          contactStart: toThaiLocalIso(startedAt.current),
        },
        { token },
      );
      // สำเร็จ: saved.id, saved.categoryNameTh ...
      alert(`บันทึกแล้ว: ${saved.categoryNameTh}`);
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

      {error && <p role="alert">{error}</p>}
      <button type="submit" disabled={!canSubmit}>
        {saving ? "กำลังบันทึก..." : "ยืนยันข้อมูล"}
      </button>
    </form>
  );
}
```

---

## 5. ข้อควรรู้ (สถานะ ณ 12 พ.ย. 2026)

- **กดซ้ำ = บันทึกซ้ำ**: API ไม่กันรายการซ้ำ ต้องปิดปุ่มระหว่างรอ response (ตามตัวอย่าง `saving`)
- **API ไม่ตรวจรูปแบบเบอร์**: ตรวจแค่ว่ากรอกและยาวไม่เกิน 50 ถ้าต้องการบังคับรูปแบบเบอร์โทร
  (เช่น ช่องทาง Phone ต้องเป็นตัวเลขขึ้นต้นด้วย 0) ให้ตรวจที่ frontend ไปก่อน
  และ API **ไม่ลบขีด/ช่องว่างกลางเบอร์** ให้ ควรส่งเป็นตัวเลขล้วน (`0812225566`)
  เพื่อให้ค้นตรงกับ `GET /Account/GetAccountByPhoneNo` ได้
- **ช่องทางเริ่มต้น**: design ตั้งเป็น Phone แต่ `GetChannel` ยังไม่บอกว่าตัวไหนเป็นค่าเริ่มต้น
- **ยังไม่ผูกกับการเปิดเรื่อง**: ถ้าเลือก New Service / Follow Up แล้วไปเปิดเรื่องต่อ
  (`POST /Services/AddNewService`) ตอนนี้ยังไม่มี field เชื่อมสองอย่างนี้ เก็บ `id` จาก response ไว้ก่อน
  ถ้า backend เพิ่มการเชื่อมโยงจะอัปเดตเอกสารนี้
- **อย่าเอา `categoryId` ของหน้านี้ไปใส่ใน `AddNewService`**: field นั้นคือประเภทบริการ คนละชุดกัน

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
