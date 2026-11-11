# Contact Type API (`GET /ListOfValues/GetContactType`) สำหรับ Frontend

ใช้ดึงตัวเลือกส่วน **"ประเภทการรับสาย"** ของหน้ารับสาย มาเป็นต้นไม้ก้อนเดียว
ฝั่ง Service คือ New Service และ Follow Up ฝั่ง Non Service คือ Out of Scope, โทรผิด, โทรเล่น,
สัญญาณไม่ชัดเจน และสายหลุด แต่ละตัวอาจมี drop-down ย่อยอยู่ข้าง radio

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetContactType`), `Dtos/ContactTypeResponse.cs`
> ข้อมูลมาจาก `TblCategory` ที่ `CategoryType = "CONTACT"`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Request body / query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetContactType` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetContactType` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. โครงสร้างข้อมูลเทียบกับหน้าจอ

ต้นไม้มี 3 ระดับ และแต่ละระดับตรงกับส่วนบนหน้าจอดังนี้

| ระดับ | บนหน้าจอ | ตัวอย่าง |
|---|---|---|
| 1 (root) | หัวคอลัมน์ | `Service`, `Non Service` |
| 2 | radio | `New Service`, `Out of Scope`, `โทรผิด`, `สายหลุด` |
| 3 | drop-down ข้าง radio | `The Pizza Company`, `เด็กโทรเล่น` |

- radio ที่มี `children` ต้องแสดง drop-down ให้เลือกต่อ ได้แก่ Out of Scope, โทรผิด และโทรเล่น
- radio ที่ `children` เป็น `[]` ไม่ต้องมี drop-down ได้แก่ New Service, Follow Up, สัญญาณไม่ชัดเจน และสายหลุด

### ค่าที่ต้องบันทึก

ให้บันทึก **id ของรายการสุดท้ายที่ผู้ใช้เลือก (leaf)**:

- radio มี drop-down → ใช้ id ของรายการใน drop-down เช่น โทรเล่น > เด็กโทรเล่น = `…A200013`
- radio ไม่มี drop-down → ใช้ id ของ radio เอง เช่น สายหลุด = `…A200003`
- ❌ ห้ามส่ง id ของหัวคอลัมน์ (`Service` / `Non Service`) และห้ามส่ง id ของ radio ที่ยังมี drop-down
  (`Out of Scope` / `โทรผิด` / `โทรเล่น`)

ค่านี้คือค่าเดียวกับที่ระบบเดิมเก็บใน `TblContact.CategoryId` และรายงาน 05/06 นับจากค่านี้

> ⚠️ **ยังไม่มี API สำหรับบันทึกการรับสาย** ตอนนี้มีแค่ endpoint ดึงตัวเลือก ส่วนชื่อ field ที่ใช้ส่ง id
> จะแจ้งเมื่อ backend ทำ endpoint บันทึกเสร็จ
>
> **อย่าเอา id ชุดนี้ไปใส่ใน `categoryId` ของ `POST /Services/AddNewService`** เพราะ field นั้นคือ
> *ประเภทบริการ* (Q&A, Claim, …) ซึ่งเป็นคนละชุดกัน และ API จะไม่ฟ้อง error ด้วย เพราะเช็กแค่ว่า id
> มีอยู่ใน `TblCategory`

---

## 3. Response

### 3.1 รูปแบบ

200 ตอบเป็น **array ของ root** ไม่มี envelope `{ status, message, data }`

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblCategory.Id` (hex 32 ตัว) |
| `nameTh` | string | ชื่อที่ใช้แสดงผล |
| `nameEn` | string \| null | ชื่อภาษาอังกฤษ (บางรายการยังเป็นภาษาไทยอยู่ ไม่ควรใช้แสดงผล) |
| `children` | node[] | ลูกที่เปิดใช้งาน เรียงตาม `nameTh` ถ้าเป็น leaf จะเป็น `[]` (ไม่เป็น `null`) |

- ส่งมาเฉพาะรายการที่เปิดใช้งาน ถ้าแม่ถูกปิดใช้งาน ลูกทั้งหมดจะไม่ถูกส่งมาด้วย
- **ลำดับ:** root เรียงให้ `Service` มาก่อน `Non Service` ส่วนระดับอื่นเรียงตาม `nameTh` ตาม collation ไทยของ DB
  ลำดับ radio ฝั่ง Non Service ที่ได้จึงตรงกับ design แล้ว ให้แสดงตามลำดับที่ API ส่งมา ไม่ต้อง sort ใหม่

### 3.2 ตัวอย่าง (ข้อมูลจริง ณ 12 พ.ย. 2026)

```json
[
  {
    "id": "0168B738100C4CBAB9AA45906A100001",
    "nameTh": "Service",
    "nameEn": "Service",
    "children": [
      { "id": "0168B738100C4CBAB9AA45906A100022", "nameTh": "Follow Up", "nameEn": "Follow Up", "children": [] },
      { "id": "0168B738100C4CBAB9AA45906A100011", "nameTh": "New Service", "nameEn": "New Service", "children": [] }
    ]
  },
  {
    "id": "0168B738100C4CBAB9AA45906A100002",
    "nameTh": "Non Service",
    "nameEn": "Non Service",
    "children": [
      {
        "id": "0168B738100C4CBAB9AA45906A200005",
        "nameTh": "Out of Scope",
        "nameEn": "Out of Scope",
        "children": [
          { "id": "0168B738100C4CBAB9AA45906A200006", "nameTh": "ขอคำปรึกษาเจ้าหน้าที่", "nameEn": "ขอคำปรึกษาเจ้าหน้าที่", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200010", "nameTh": "ระบายความเครียดด้านกฏหมาย", "nameEn": "ระบายความเครียดด้านกฏหมาย", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200009", "nameTh": "ระบายความเครียดด้านการเมือง", "nameEn": "ระบายความเครียดด้านการเมือง", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200011", "nameTh": "ระบายความเครียดด้านทรัพยากรธรรมชาติ", "nameEn": "ระบายความเครียดด้านทรัพยากรธรรมชาติ", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200008", "nameTh": "ระบายความเครียดด้านเศรษฐกิจ", "nameEn": "ระบายความเครียดด้านเศรษฐกิจ", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200007", "nameTh": "ระบายความเครียดด้านสังคม", "nameEn": "ระบายความเครียดด้านสังคม", "children": [] }
        ]
      },
      {
        "id": "0168B738100C4CBAB9AA45906A200004",
        "nameTh": "โทรผิด",
        "nameEn": "Wrong Call",
        "children": [
          { "id": "0168B738100C4CBAB9AA45906A200017", "nameTh": "The Pizza Company", "nameEn": "The Pizza Company", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200016", "nameTh": "อื่น ๆ", "nameEn": "Other", "children": [] }
        ]
      },
      {
        "id": "0168B738100C4CBAB9AA45906A200001",
        "nameTh": "โทรเล่น",
        "nameEn": "Kidding Call",
        "children": [
          { "id": "0168B738100C4CBAB9AA45906A200012", "nameTh": "จิตไม่ปกติ", "nameEn": "Bugs Call", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200013", "nameTh": "เด็กโทรเล่น", "nameEn": "เด็กโทรเล่น", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200015", "nameTh": "โทรด่าหยาบคาย", "nameEn": "Discourteous", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200018", "nameTh": "น้ำท่วม", "nameEn": "น้ำท่วม", "children": [] },
          { "id": "0168B738100C4CBAB9AA45906A200014", "nameTh": "เสียงเงียบ", "nameEn": "Non Voice", "children": [] }
        ]
      },
      { "id": "0168B738100C4CBAB9AA45906A200002", "nameTh": "สัญญาณไม่ชัดเจน", "nameEn": "Noise", "children": [] },
      { "id": "0168B738100C4CBAB9AA45906A200003", "nameTh": "สายหลุด", "nameEn": "Communication Terminate", "children": [] }
    ]
  }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ เพราะเพิ่ม/ปิดรายการได้จาก DB
ให้สร้าง radio และ drop-down จาก `children` เสมอ

### 3.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `ContactTypeNode[]` |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 404 | backend ที่รันอยู่ยังเป็นเวอร์ชันเก่าที่ไม่มี endpoint นี้ (ให้ backend restart) | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

### 4.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/contact-type.ts
export interface ContactTypeNode {
  id: string;
  nameTh: string;
  nameEn: string | null;
  children: ContactTypeNode[];
}

export class ContactTypeApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export async function fetchContactTypes(
  opts: { token: string; signal?: AbortSignal },
): Promise<ContactTypeNode[]> {
  const res = await fetch("/api/backend/ListOfValues/GetContactType", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });

  if (res.ok) return res.json();
  if (res.status === 401) throw new ContactTypeApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  throw new ContactTypeApiError(res.status, "โหลดประเภทการรับสายไม่สำเร็จ");
}
```

### 4.2 คอมโพเนนต์ radio + drop-down

state มีแค่ 2 ค่า คือ radio ที่เลือก (`radioId`) และรายการใน drop-down ที่เลือก (`subId`)
ค่าที่ส่งออกไปบันทึกได้จาก `resolveLeafId()`

```tsx
"use client";

import { useEffect, useState } from "react";
import { fetchContactTypes, type ContactTypeNode } from "@/lib/api/contact-type";

/** id ที่ต้องบันทึก: drop-down ถ้า radio มีลูก ไม่งั้นใช้ตัว radio เอง; ยังเลือกไม่ครบ = null */
export function resolveLeafId(radio: ContactTypeNode | undefined, subId: string): string | null {
  if (!radio) return null;
  if (radio.children.length === 0) return radio.id;
  return radio.children.some((c) => c.id === subId) ? subId : null;
}

export function ContactTypePicker(props: {
  token: string;
  onChange: (leafId: string | null) => void;
}) {
  const [groups, setGroups] = useState<ContactTypeNode[]>([]);
  const [radioId, setRadioId] = useState("");
  const [subId, setSubId] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ac = new AbortController();
    fetchContactTypes({ token: props.token, signal: ac.signal })
      .then(setGroups)
      .catch((e) => { if (e.name !== "AbortError") setError(e.message); });
    return () => ac.abort();
  }, [props.token]);

  const radios = groups.flatMap((g) => g.children);
  const selected = radios.find((r) => r.id === radioId);

  useEffect(() => {
    props.onChange(resolveLeafId(selected, subId));
  }, [selected, subId]); // eslint-disable-line react-hooks/exhaustive-deps

  if (error) return <p>{error}</p>;

  return (
    <fieldset>
      <legend>ประเภทการรับสาย *</legend>
      <div style={{ display: "flex", gap: 32 }}>
        {groups.map((group) => (
          <div key={group.id}>
            <strong>{group.nameTh}</strong>
            {group.children.map((radio) => (
              <div key={radio.id}>
                <label>
                  <input
                    type="radio"
                    name="contactType"
                    value={radio.id}
                    checked={radioId === radio.id}
                    onChange={() => { setRadioId(radio.id); setSubId(""); }}
                  />
                  {radio.nameTh}
                </label>

                {radio.children.length > 0 && (
                  <select
                    value={radioId === radio.id ? subId : ""}
                    disabled={radioId !== radio.id}
                    onChange={(e) => setSubId(e.target.value)}
                  >
                    <option value="">{radio.nameTh}</option>
                    {radio.children.map((sub) => (
                      <option key={sub.id} value={sub.id}>{sub.nameTh}</option>
                    ))}
                  </select>
                )}
              </div>
            ))}
          </div>
        ))}
      </div>
    </fieldset>
  );
}
```

ข้อควรทำ:

- เปลี่ยน radio แล้วต้องล้างค่า drop-down (`setSubId("")`) ไม่งั้นจะได้ id ของ radio ตัวเก่าติดไป
- drop-down ของ radio ที่ไม่ได้เลือกให้ปิด (`disabled`) เหมือนใน design
- ปุ่มบันทึกควรกดได้เมื่อ `resolveLeafId()` ไม่เป็น `null` เท่านั้น เช่น เลือก "โทรเล่น" แล้วยังไม่เลือก drop-down ต้องยังกดไม่ได้

### 4.3 Cache

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้
(เช่น React Query / SWR ตั้ง `staleTime` ยาว ๆ)

---

## 5. ข้อควรรู้ (สถานะ ณ 12 พ.ย. 2026)

| เรื่อง | Design | API ตอนนี้ |
|---|---|---|
| **Follow Up** (ฝั่ง Service) | ไม่มี | **มี** เพราะยังเปิดใช้งานใน DB และมีการใช้งานจริงแล้ว รอทีม design ยืนยันว่าจะเอาออกหรือไม่ ระหว่างนี้แสดงตามที่ API ส่งมา |
| ค่าเริ่มต้น (radio ที่เลือกไว้ก่อน) | – | API ไม่ได้ระบุ ให้เริ่มแบบยังไม่เลือก |
| API บันทึกการรับสาย | – | ยังไม่มี (ดูข้อ 2) |

---

## 6. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetContactType
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetContactType -H "Authorization: Bearer <token>"
```
