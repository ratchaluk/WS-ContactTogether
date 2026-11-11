# Channel API (`GET /ListOfValues/GetChannel`) สำหรับ Frontend

ใช้ดึงรายการ **ช่องทางการติดต่อ** (Phone, IVR, Email, Facebook, …) ไปแสดงใน drop-down
ค่าที่เลือกได้คือ `id` ซึ่งใช้ส่งกลับไปใน `channelIncomingId` / `channelOutgoingId` ตอนเปิดเรื่อง
(`POST /Services/AddNewService`)

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetChannel`), `Dtos/LookupItemResponse.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Request body / query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetChannel` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetChannel` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ไม่มี envelope `{ status, message, data }` แบบรายงาน

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblChannel.Id` (hex 32 ตัว) ใช้เป็น `value` ของ option และส่งกลับไปตอนบันทึก |
| `nameTh` | string | ชื่อที่ใช้แสดงผล |

- ส่งมาเฉพาะช่องทางที่เปิดใช้งาน (`IsEnable = "T"`)
- **เรียงตาม `nameTh`** (collation ไทยของ DB) ชื่อภาษาอังกฤษจึงขึ้นก่อนชื่อภาษาไทย
- ถ้าไม่มีช่องทางที่เปิดใช้งานเลย จะได้ `200` กับ `[]`

### 2.2 ตัวอย่าง (ข้อมูลจริง ณ 12 พ.ย. 2026)

```json
[
  { "id": "332E6D76F258C8294B160C20642AB9F6", "nameTh": "Email" },
  { "id": "332E6D76F258C8294B160C20642AB915", "nameTh": "Facebook" },
  { "id": "332E6D76F258C8294B160C20642AB9F7", "nameTh": "Fax" },
  { "id": "332E6D76F258C8294B160C20642AB913", "nameTh": "IVR (กด 3)" },
  { "id": "332E6D76F258C8294B160C20642AB917", "nameTh": "Pantip" },
  { "id": "332E6D76F258C8294B160C20642AB9F5", "nameTh": "Phone" },
  { "id": "332E6D76F258C8294B160C20642AB916", "nameTh": "Twitter" },
  { "id": "332E6D76F258C8294B160C20642AB911", "nameTh": "Voice Mail" },
  { "id": "332E6D76F258C8294B160C20642AB914", "nameTh": "Web Site" },
  { "id": "663DFEE172DD4B3F8D6727FC5B9CAE8A", "nameTh": "เน็ตประชารัฐ (กด 88)" },
  { "id": "76910162384A4222858509BFA073CB68", "nameTh": "สอบถาม CN (กด 9 กด 2)" },
  { "id": "4E729CDBDF8A46B59504E3B1D2B7FBAA", "nameTh": "สอบถาม ENG (กด 9 กด 1)" },
  { "id": "0E5E8C0EA58240AC86D3005FB6BD0027", "nameTh": "สอบถาม พรบ.อำนวยความสะดวก (กด 22)" },
  { "id": "BAADDC1AC841457F95A6E2427A833131", "nameTh": "สอบถาม ภัยพิบัติ (กด 5)" }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ เพราะเพิ่ม/ปิดช่องทางได้จาก DB

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `ChannelItem[]` |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

### 3.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/channel.ts
export interface ChannelItem {
  id: string;
  nameTh: string;
}

export class ChannelApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export async function fetchChannels(
  opts: { token: string; signal?: AbortSignal },
): Promise<ChannelItem[]> {
  const res = await fetch("/api/backend/ListOfValues/GetChannel", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });

  if (res.ok) return res.json();
  if (res.status === 401) throw new ChannelApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  throw new ChannelApiError(res.status, "โหลดรายการช่องทางไม่สำเร็จ");
}
```

### 3.2 ใช้ใน drop-down (client component)

```tsx
"use client";

import { useEffect, useState } from "react";
import { fetchChannels, type ChannelItem } from "@/lib/api/channel";

export function ChannelSelect(props: {
  token: string;
  value: string;
  onChange: (id: string) => void;
}) {
  const [channels, setChannels] = useState<ChannelItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ac = new AbortController();
    fetchChannels({ token: props.token, signal: ac.signal })
      .then(setChannels)
      .catch((e) => { if (e.name !== "AbortError") setError(e.message); });
    return () => ac.abort();
  }, [props.token]);

  if (error) return <p>{error}</p>;

  return (
    <select value={props.value} onChange={(e) => props.onChange(e.target.value)}>
      <option value="">-- เลือกช่องทาง --</option>
      {channels.map((c) => (
        <option key={c.id} value={c.id}>{c.nameTh}</option>
      ))}
    </select>
  );
}
```

ตอนบันทึกเรื่อง ส่ง `id` ที่เลือกไปใน `channelIncomingId` (ช่องทางที่ติดต่อเข้ามา) และ/หรือ
`channelOutgoingId` (ช่องทางที่ติดต่อกลับ) ของ `POST /Services/AddNewService`

### 3.3 Cache

รายการช่องทางแทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้
(เช่น React Query / SWR ตั้ง `staleTime` ยาว ๆ) ไม่ต้องยิงใหม่ทุกครั้งที่เปิดฟอร์ม

---

## 4. ข้อควรรู้ (สถานะ ณ 12 พ.ย. 2026)

เทียบกับ drop-down ใน design แล้วมีจุดต่างดังนี้ ทั้งหมดเป็นเรื่องข้อมูลหรือ API ฝั่ง backend
**frontend ไม่ต้องแก้หรือ hard-code ชดเชย**

| เรื่อง | Design | API ตอนนี้ |
|---|---|---|
| Tiktok, Instagram | มี | **ยังไม่มี** ใน DB |
| Twitter | ไม่มี | **มี** (ยังเปิดใช้งานอยู่) |
| ลำดับ | Phone ก่อน แล้วกลุ่ม IVR แล้วช่องทางออนไลน์ | เรียงตามชื่อ (`nameTh`) |
| ค่าเริ่มต้น | Phone | API ยังไม่บอกว่าตัวไหนเป็นค่าเริ่มต้น (ใน DB คือ Phone) |

ถ้า backend ปรับข้อมูลหรือเพิ่ม field (เช่น `isDefault`) จะแจ้งและอัปเดตเอกสารนี้

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetChannel
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetChannel -H "Authorization: Bearer <token>"
```
