# Reference API (`GET /ListOfValues/GetReference`) สำหรับ Frontend

ใช้ดึงรายการ **ที่มาข้อมูล** (เรื่องนี้มาจากแหล่งไหน เช่น Joomla, Other Website, เจ้าหน้าที่กระทรวง, อื่นๆ)
ไปแสดงใน drop-down **"ที่มาข้อมูล"** ของฟอร์มเปิดเรื่อง ค่าที่เลือกคือ `id` ซึ่งต้องส่งกลับไปใน
`serviceReference` ของ `POST /Services/AddNewService` และใช้กรองหน้าค้นหาด้วย `ServiceReference` ของ
`GET /Services/SearchService` ([SearchService.md](SearchService.md))

ชื่อที่เลือกจะไปแสดงในคอลัมน์ "ที่มาข้อมูล" ของรายงาน 01–04 และ 07 ถ้าไม่ได้ส่ง `id` จาก endpoint นี้
คอลัมน์นั้นในรายงานจะว่าง

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetReference`), `Dtos/ReferenceResponse.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Request body / query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetReference` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetReference` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ไม่มี envelope `{ status, message, data }` แบบรายงาน

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblReference.Id` (hex 32 ตัว) ใช้เป็น `value` ของ option และส่งกลับไปใน `serviceReference` |
| `nameTh` | string | ชื่อที่ใช้แสดงผล |
| `nameEn` | string \| null | ชื่อภาษาอังกฤษ ตอนนี้ทุกรายการเป็น `null` ให้แสดง `nameTh` เสมอ |
| `isDefault` | `"T"` \| `"F"` | `"T"` = ตัวที่ควรเลือกไว้ให้ก่อนตอนเปิดฟอร์ม (เป็น **string** ไม่ใช่ boolean) |

- ส่งมาเฉพาะรายการที่เปิดใช้งาน (`IsEnable = "T"`)
- **เรียงตาม `nameTh`** (collation ไทยของ DB) ชื่อภาษาอังกฤษจึงขึ้นก่อนชื่อภาษาไทย
- ถ้าไม่มีรายการที่เปิดใช้งานเลย จะได้ `200` กับ `[]`

### 2.2 ตัวอย่าง (ข้อมูลจริง ณ 16 พ.ย. 2026)

```json
[
  { "id": "0199D0ECBE0F08A02BDFDB8460CAB362", "nameTh": "Joomla", "nameEn": null, "isDefault": "F" },
  { "id": "01DADE829833C350D24D52B37BD09EEE", "nameTh": "Other Website", "nameEn": null, "isDefault": "F" },
  { "id": "026E237DFDC7C8BC427E03DC8CCF2F02", "nameTh": "เจ้าหน้าที่กระทรวง", "nameEn": null, "isDefault": "F" },
  { "id": "02B8AE9053A6B4A5E0CEB5DE705EFFF3", "nameTh": "อื่นๆ", "nameEn": null, "isDefault": "F" }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ เพราะเพิ่ม/ปิดรายการได้จาก DB

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `ReferenceItem[]` |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

### 3.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/reference.ts
export interface ReferenceItem {
  id: string;
  nameTh: string;
  nameEn: string | null;
  isDefault: "T" | "F";
}

export class ReferenceApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export async function fetchReferences(
  opts: { token: string; signal?: AbortSignal },
): Promise<ReferenceItem[]> {
  const res = await fetch("/api/backend/ListOfValues/GetReference", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });

  if (res.ok) return res.json();
  if (res.status === 401) throw new ReferenceApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  throw new ReferenceApiError(res.status, "โหลดรายการที่มาข้อมูลไม่สำเร็จ");
}
```

### 3.2 ใช้ใน drop-down (client component)

```tsx
"use client";

import { useEffect, useState } from "react";
import { fetchReferences, type ReferenceItem } from "@/lib/api/reference";

export function ReferenceSelect(props: {
  token: string;
  value: string;                 // "" = ยังไม่เลือก
  onChange: (id: string) => void;
}) {
  const [items, setItems] = useState<ReferenceItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ac = new AbortController();
    fetchReferences({ token: props.token, signal: ac.signal })
      .then((list) => {
        setItems(list);
        // เลือกตัว default ให้ ถ้ายังไม่ได้เลือกอะไร (ตอนนี้ยังไม่มีรายการไหนเป็น "T")
        const def = list.find((r) => r.isDefault === "T");
        if (!props.value && def) props.onChange(def.id);
      })
      .catch((e) => { if (e.name !== "AbortError") setError(e.message); });
    return () => ac.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [props.token]);

  if (error) return <p>{error}</p>;

  return (
    <select value={props.value} onChange={(e) => props.onChange(e.target.value)}>
      <option value="">-- เลือกที่มาข้อมูล --</option>
      {items.map((r) => (
        <option key={r.id} value={r.id}>{r.nameTh}</option>
      ))}
    </select>
  );
}
```

### 3.3 ส่งค่าตอนเปิดเรื่อง (`POST /Services/AddNewService`)

ในฟอร์มมี 2 ช่องที่เกี่ยวข้องกัน:

| ช่องบนหน้าจอ | Field ที่ส่ง | ใส่อะไร |
|---|---|---|
| ที่มาข้อมูล (drop-down) | `serviceReference` | `id` ที่เลือกจาก `ReferenceSelect` ถ้าไม่เลือกให้ส่ง `null` **อย่าส่ง `""`** |
| Link ที่มาข้อมูล (ช่องพิมพ์) | `serviceReferenceLink` | URL ที่ผู้ใช้พิมพ์ (เช่น ลิงก์โพสต์หรือข่าว) ยาวไม่เกิน 500 ตัว ไม่ต้องเลือกจาก list |

```ts
const body = {
  // ...field อื่นของ AddNewService
  serviceReference: referenceId || null,
  serviceReferenceLink: referenceLink.trim() || null,
};
```

**ส่ง `serviceReference` ที่ไม่มีในตาราง** (เช่น ข้อความที่ผู้ใช้พิมพ์ หรือ id ที่ถูกลบไปแล้ว)
จะได้ `400` แบบ ProblemDetails และเรื่องจะ **ไม่ถูกบันทึก**:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "ServiceReference": ["No row with Id 'abc'."]
  }
}
```

ให้แสดง error ใต้ drop-down ที่มาข้อมูล (key ใน `errors` ขึ้นต้นด้วยตัวใหญ่ `ServiceReference`)

### 3.4 Cache

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้
(เช่น React Query / SWR ตั้ง `staleTime` ยาว ๆ) ไม่ต้องยิงใหม่ทุกครั้งที่เปิดฟอร์ม

---

## 4. ข้อควรรู้ (สถานะ ณ 16 พ.ย. 2026)

| เรื่อง | ตอนนี้ |
|---|---|
| ค่าเริ่มต้น | ยังไม่มีรายการไหนเป็น `isDefault = "T"` drop-down จึงเริ่มที่ "-- เลือกที่มาข้อมูล --" |
| ชื่อภาษาอังกฤษ | `nameEn` เป็น `null` ทุกรายการ |
| บังคับเลือกไหม | backend **ไม่บังคับ** (`serviceReference` เป็น `null` ได้) ถ้าหน้าจอต้องบังคับ ให้ตรวจฝั่ง frontend |

ถ้า backend ปรับข้อมูลหรือเพิ่ม field จะแจ้งและอัปเดตเอกสารนี้

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetReference
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetReference -H "Authorization: Bearer <token>"
```
