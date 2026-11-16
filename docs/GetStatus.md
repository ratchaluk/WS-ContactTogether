# Status API (`GET /ListOfValues/GetStatus`) สำหรับ Frontend

ใช้ดึงรายการ **สถานะ** (`TblStatus`) ไปแสดงใน drop-down สถานะ
เรียกแบบไม่ส่ง query จะได้สถานะประเภท **`ACTIVITY`** 3 รายการ: **เปิด / ปิด / ยกเลิก**
ให้เลือกตัวที่ **`isDefault = "T"`** (ตอนนี้คือ **ปิด**) ไว้ให้ก่อนตอนเปิดฟอร์ม

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetStatus`), `Dtos/StatusResponse.cs`, `Helper/StatusTypes.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `statusType` (ไม่บังคับ) ไม่ส่ง = `ACTIVITY` (ตัวพิมพ์เล็กก็ได้) |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetStatus` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetStatus` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ไม่มี envelope `{ status, message, data }` แบบรายงาน

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblStatus.Id` (hex 32 ตัว) ใช้เป็น `value` ของ option และส่งกลับไปตอนบันทึก |
| `nameTh` | string | ชื่อที่ใช้แสดงผล |
| `nameEn` | string \| null | ชื่อภาษาอังกฤษ |
| `isType` | `"OPEN"` \| `"CLOSE"` \| `"CANCEL"` | ความหมายของสถานะ ใช้แยกสี/ไอคอนได้ |
| `isDefault` | `"T"` \| `"F"` | **`"T"` = ตัวที่ต้องเลือกไว้ให้ก่อน** (เป็น string ไม่ใช่ boolean) |
| `children` | array | ของ `ACTIVITY` เป็น `[]` เสมอ ไม่ต้องใช้ |

- ส่งมาเฉพาะรายการที่เปิดใช้งาน (`IsEnable = "T"`)
- เรียง เปิด → ปิด → ยกเลิก (ตาม `isType`: OPEN → CLOSE → CANCEL)

### 2.2 ตัวอย่าง (ข้อมูลจริง ณ 16 พ.ย. 2026)

`GET /ListOfValues/GetStatus`

```json
[
  { "id": "0A5753389CDCD23F173268D8D46AC004", "nameTh": "เปิด",   "nameEn": "Open",   "isType": "OPEN",   "isDefault": "F", "children": [] },
  { "id": "CB0A973609E8D684F2944D92ED4126A1", "nameTh": "ปิด",    "nameEn": "Closed", "isType": "CLOSE",  "isDefault": "T", "children": [] },
  { "id": "CD6469896CF379C64A2FB75263FD3362", "nameTh": "ยกเลิก", "nameEn": "Cancel", "isType": "CANCEL", "isDefault": "F", "children": [] }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ ให้หาตัว default จาก `isDefault === "T"`

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `StatusItem[]` |
| 400 | ส่ง `statusType` ที่ไม่ใช่ `ACTIVITY` / `SERVICE` | ProblemDetails (`errors.statusType`) |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

### 3.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/status.ts
export interface StatusItem {
  id: string;
  nameTh: string;
  nameEn: string | null;
  isType: "OPEN" | "PENDING" | "CLOSE" | "CANCEL";
  isDefault: "T" | "F";
  children: StatusItem[];
}

export class StatusApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export async function fetchStatuses(opts: { token: string; signal?: AbortSignal }): Promise<StatusItem[]> {
  const res = await fetch("/api/backend/ListOfValues/GetStatus", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });

  if (res.ok) return res.json();
  if (res.status === 401) throw new StatusApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  throw new StatusApiError(res.status, "โหลดรายการสถานะไม่สำเร็จ");
}
```

### 3.2 drop-down ที่เลือกตัว `isDefault = "T"` ให้ (client component)

```tsx
"use client";

import { useEffect, useState } from "react";
import { fetchStatuses, type StatusItem } from "@/lib/api/status";

export function StatusSelect(props: {
  token: string;
  value: string;                 // "" = ยังไม่เลือก
  onChange: (id: string) => void;
}) {
  const [items, setItems] = useState<StatusItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ac = new AbortController();
    fetchStatuses({ token: props.token, signal: ac.signal })
      .then((list) => {
        setItems(list);
        // เลือกตัว isDefault = "T" (ปิด) ให้ ถ้ายังไม่ได้เลือกอะไร
        const def = list.find((s) => s.isDefault === "T");
        if (!props.value && def) props.onChange(def.id);
      })
      .catch((e) => { if (e.name !== "AbortError") setError(e.message); });
    return () => ac.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [props.token]);

  if (error) return <p>{error}</p>;

  return (
    <select value={props.value} onChange={(e) => props.onChange(e.target.value)}>
      <option value="">-- เลือกสถานะ --</option>
      {items.map((s) => (
        <option key={s.id} value={s.id}>{s.nameTh}</option>
      ))}
    </select>
  );
}
```

### 3.3 Cache

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้
(เช่น React Query / SWR ตั้ง `staleTime` ยาว ๆ)

---

## 4. ข้อควรรู้ (สถานะ ณ 16 พ.ย. 2026)

| เรื่อง | ตอนนี้ |
|---|---|
| ค่าเริ่มต้น | **ปิด** (`isDefault = "T"`) |
| ใช้กับ `statusId` ของ `AddNewService` ได้ไหม | **ยังไม่ได้** `AddNewService` รับเฉพาะสถานะประเภท `SERVICE` (ส่ง id ของ ACTIVITY จะได้ 400) ถ้าหน้าจอเปิดเรื่องต้องใช้รายการนี้ แจ้ง backend ก่อน |
| สถานะของเรื่อง (`SERVICE`) | เรียก `GET /ListOfValues/GetStatus?statusType=SERVICE` ได้ จะได้ต้นไม้ 2 ชั้น เปิดเรื่อง / ปิดเรื่อง / ยกเลิก → สถานะย่อย (ส่ง id ของสถานะย่อยเป็น `statusId`) |

ถ้า backend ปรับข้อมูลหรือเพิ่ม field จะแจ้งและอัปเดตเอกสารนี้

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetStatus
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetStatus -H "Authorization: Bearer <token>"
```
