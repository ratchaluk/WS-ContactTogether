# Level API (`GET /ListOfValues/GetLevel`) สำหรับ Frontend

ใช้ดึงรายการ **ระดับ** ไปแสดงใน drop-down 3 ตัวของฟอร์มเปิดเรื่อง ทั้ง 3 ตัวมาจากตารางเดียวกัน (`TblLevel`)
แยกกันด้วย query `levelType`

| drop-down บนหน้าจอ | `levelType` | ส่ง `id` ที่เลือกไปใน field ของ `POST /Services/AddNewService` |
|---|---|---|
| ความเร่งด่วน | `SEVERITY` | `levelSeverityId` |
| ความสำคัญ | `PRIORITY` | `levelPriorityId` |
| ชั้นความลับ | `SECRET` | `levelSecretId` |

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetLevel`), `Dtos/LevelResponse.cs`, `Helper/LevelTypes.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `levelType` (**บังคับ**): `SEVERITY`, `PRIORITY` หรือ `SECRET` (ตัวพิมพ์เล็กก็ได้) |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetLevel?levelType=SEVERITY` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetLevel?levelType=SEVERITY` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ไม่มี envelope `{ status, message, data }` แบบรายงาน

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblLevel.Id` (hex 32 ตัว) ใช้เป็น `value` ของ option และส่งกลับไปตอนบันทึก |
| `nameTh` | string | ชื่อที่ใช้แสดงผล |
| `nameEn` | string \| null | ชื่อภาษาอังกฤษ |
| `multiplier` | number \| null | ลำดับของระดับ (1–4) รายการ **เรียงตามค่านี้มาให้แล้ว** แสดงตามลำดับที่ได้ได้เลย |
| `isDefault` | `"T"` \| `"F"` | `"T"` = ตัวที่ควรเลือกไว้ให้ก่อนตอนเปิดฟอร์ม (เป็น **string** ไม่ใช่ boolean) |

- ส่งมาเฉพาะรายการที่เปิดใช้งาน (`IsEnable = "T"`)
- เรียงตาม `multiplier` แล้วตาม `nameTh`
- แต่ละประเภทมีตัว default 1 ตัว

### 2.2 ตัวอย่าง (ข้อมูลจริง ณ 16 พ.ย. 2026)

`GET /ListOfValues/GetLevel?levelType=SEVERITY` (ความเร่งด่วน)

```json
[
  { "id": "2B38BAE2A423C58915F7F2FA41982A50", "nameTh": "ปกติ", "nameEn": "Normal", "multiplier": 1, "isDefault": "T" },
  { "id": "FECCF9B69668C20E60F354E8F15822F9", "nameTh": "ด่วน", "nameEn": "Speed", "multiplier": 2, "isDefault": "F" },
  { "id": "82665A762F11FFC7E7F8D3307F274C15", "nameTh": "ด่วนมาก", "nameEn": "Very Speed", "multiplier": 3, "isDefault": "F" },
  { "id": "0279BCF140ECC4A5E16C138F594AB220", "nameTh": "ด่วนที่สุด", "nameEn": "Most Speed", "multiplier": 4, "isDefault": "F" }
]
```

`GET /ListOfValues/GetLevel?levelType=PRIORITY` (ความสำคัญ)

```json
[
  { "id": "D550B0AAB78CEEE3E6C027E477932A33", "nameTh": "1-ASAP", "nameEn": "1-ASAP", "multiplier": 1, "isDefault": "F" },
  { "id": "D550B0AAB78CEEE3E6C027E477932A34", "nameTh": "2-High", "nameEn": "2-High", "multiplier": 2, "isDefault": "F" },
  { "id": "D550B0AAB78CEEE3E6C027E477932A35", "nameTh": "3-Medium", "nameEn": "3-Medium", "multiplier": 3, "isDefault": "T" },
  { "id": "D550B0AAB78CEEE3E6C027E477932A36", "nameTh": "4-Low", "nameEn": "4-Low", "multiplier": 4, "isDefault": "F" }
]
```

`GET /ListOfValues/GetLevel?levelType=SECRET` (ชั้นความลับ)

```json
[
  { "id": "859E3EA3430F667B055476057E6993A5", "nameTh": "ปกติ", "nameEn": "Normal", "multiplier": 1, "isDefault": "T" },
  { "id": "D84C46E2B29B4AF50DB6012D26F919C0", "nameTh": "ลับ", "nameEn": "Secret", "multiplier": 2, "isDefault": "F" },
  { "id": "6C494B5A3A1A47E0450886A2F05D8556", "nameTh": "ลับมาก", "nameEn": "Very Secret", "multiplier": 3, "isDefault": "F" },
  { "id": "E06D0B457D81DE938E4957521B39A9C3", "nameTh": "ลับที่สุด", "nameEn": "Most Secret", "multiplier": 4, "isDefault": "F" }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ เพราะเพิ่ม/ปิดรายการได้จาก DB

> ทิศทางของ `multiplier` ไม่เหมือนกันทุกประเภท: ความเร่งด่วนและชั้นความลับ `1` = ต่ำสุด ("ปกติ")
> แต่ความสำคัญ `1` = เร่งที่สุด ("1-ASAP") อย่าเอา `multiplier` ไปเทียบข้ามประเภท

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `LevelItem[]` |
| 400 | ไม่ส่ง `levelType` หรือส่งค่าอื่นที่ไม่ใช่ 3 ค่านี้ | ProblemDetails (`errors.levelType`) |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

ตัวอย่าง 400:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "levelType": ["Must be one of SEVERITY, PRIORITY, SECRET."]
  }
}
```

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

### 3.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/level.ts
export type LevelType = "SEVERITY" | "PRIORITY" | "SECRET";

export interface LevelItem {
  id: string;
  nameTh: string;
  nameEn: string | null;
  multiplier: number | null;
  isDefault: "T" | "F";
}

export class LevelApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export async function fetchLevels(
  levelType: LevelType,
  opts: { token: string; signal?: AbortSignal },
): Promise<LevelItem[]> {
  const res = await fetch(`/api/backend/ListOfValues/GetLevel?levelType=${levelType}`, {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });

  if (res.ok) return res.json();
  if (res.status === 401) throw new LevelApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  throw new LevelApiError(res.status, "โหลดรายการระดับไม่สำเร็จ");
}
```

### 3.2 drop-down เดียวใช้ได้ทั้ง 3 ประเภท (client component)

```tsx
"use client";

import { useEffect, useState } from "react";
import { fetchLevels, type LevelItem, type LevelType } from "@/lib/api/level";

export function LevelSelect(props: {
  levelType: LevelType;
  token: string;
  value: string;                 // "" = ยังไม่เลือก
  onChange: (id: string) => void;
  placeholder: string;
}) {
  const [items, setItems] = useState<LevelItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ac = new AbortController();
    fetchLevels(props.levelType, { token: props.token, signal: ac.signal })
      .then((list) => {
        setItems(list);
        // เลือกตัว default ให้ ถ้ายังไม่ได้เลือกอะไร
        const def = list.find((l) => l.isDefault === "T");
        if (!props.value && def) props.onChange(def.id);
      })
      .catch((e) => { if (e.name !== "AbortError") setError(e.message); });
    return () => ac.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [props.levelType, props.token]);

  if (error) return <p>{error}</p>;

  return (
    <select value={props.value} onChange={(e) => props.onChange(e.target.value)}>
      <option value="">{props.placeholder}</option>
      {items.map((l) => (
        <option key={l.id} value={l.id}>{l.nameTh}</option>
      ))}
    </select>
  );
}
```

ใช้ในฟอร์ม:

```tsx
<LevelSelect levelType="SEVERITY" placeholder="-- เลือกความเร่งด่วน --"
  token={token} value={severityId} onChange={setSeverityId} />
<LevelSelect levelType="PRIORITY" placeholder="-- เลือกความสำคัญ --"
  token={token} value={priorityId} onChange={setPriorityId} />
<LevelSelect levelType="SECRET" placeholder="-- เลือกชั้นความลับ --"
  token={token} value={secretId} onChange={setSecretId} />
```

### 3.3 ส่งค่าตอนเปิดเรื่อง (`POST /Services/AddNewService`)

```ts
const body = {
  // ...field อื่นของ AddNewService
  levelSeverityId: severityId || null,   // ถ้าไม่เลือกให้ส่ง null อย่าส่ง ""
  levelPriorityId: priorityId || null,
  levelSecretId: secretId || null,
};
```

backend **ตรวจว่า id ตรงประเภท** ถ้าส่ง id ที่ไม่มีอยู่ หรือส่งผิดช่อง (เช่น id ของชั้นความลับไปใส่
`levelSeverityId`) จะได้ `400` และเรื่องจะ **ไม่ถูกบันทึก**:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "LevelSeverityId": ["No row with Id '859E3EA3430F667B055476057E6993A5'."]
  }
}
```

key ใน `errors` ขึ้นต้นด้วยตัวใหญ่ (`LevelSeverityId`, `LevelPriorityId`, `LevelSecretId`)
ให้แสดง error ใต้ drop-down ตัวนั้น

### 3.4 Cache

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้
(เช่น React Query / SWR ใช้ `["levels", levelType]` เป็น key และตั้ง `staleTime` ยาว ๆ)

---

## 4. ข้อควรรู้ (สถานะ ณ 16 พ.ย. 2026)

| เรื่อง | ตอนนี้ |
|---|---|
| ค่าเริ่มต้น | ความเร่งด่วน = ปกติ, ความสำคัญ = 3-Medium, ชั้นความลับ = ปกติ |
| รายการที่ปิดใช้ | ความสำคัญมีอีก 4 รายการที่ปิดอยู่ (ปกติ, สำคัญ, สำคัญมาก, สำคัญที่สุด) API ไม่ส่งมา |
| บังคับเลือกไหม | backend **ไม่บังคับ** (ส่ง `null` ได้) ถ้าหน้าจอต้องบังคับ ให้ตรวจฝั่ง frontend |

ถ้า backend ปรับข้อมูลหรือเพิ่ม field จะแจ้งและอัปเดตเอกสารนี้

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetLevel?levelType=SEVERITY
Authorization: Bearer <token>
```

```bash
curl "http://localhost:5018/ListOfValues/GetLevel?levelType=PRIORITY" -H "Authorization: Bearer <token>"
```
