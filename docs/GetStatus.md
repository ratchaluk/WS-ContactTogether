# Status API (`GET /ListOfValues/GetStatus`) สำหรับ Frontend

ใช้ดึงรายการ **สถานะของเรื่อง** (`TblStatus` ที่ `StatusType = SERVICE`) ไปแสดงใน drop-down สถานะ
ของฟอร์มเปิดเรื่อง แล้วส่ง `id` ที่ผู้ใช้เลือกเป็น `statusId` ของ `POST /Services/AddNewService` ได้เลย

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetStatus`), `Dtos/StatusResponse.cs`, `Helper/StatusTypes.cs`

> **เปลี่ยนแปลง (17 พ.ย. 2026):** ไม่ส่ง query ตอนนี้ได้สถานะประเภท `SERVICE` ทั้งหมดเป็นรายการเดียว
> (เดิมได้ `ACTIVITY`) และไม่มี field `children` แล้ว

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `statusType` (ไม่บังคับ) ไม่ส่ง = `SERVICE` (สถานะของเรื่อง) / `ACTIVITY` = สถานะของงาน ตัวพิมพ์เล็กก็ได้ |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetStatus` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetStatus` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ไม่มี envelope เฉพาะรายการที่เปิดใช้งาน **เรียงตามชื่อ (`nameTh`)**

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblStatus.Id` ใช้เป็น `value` ของ option และส่งเป็น `statusId` ตอนบันทึก |
| `refId` | string | สถานะหลักที่สถานะนี้อยู่ใต้ ถ้า `refId === id` คือสถานะหลัก (เปิดเรื่อง / ปิดเรื่อง / ยกเลิก) ใช้จัดกลุ่มใน drop-down ได้ |
| `nameTh` | string | ชื่อที่ใช้แสดงผล |
| `nameEn` | string \| null | ชื่อภาษาอังกฤษ |
| `isType` | `"OPEN"` \| `"PENDING"` \| `"CLOSE"` \| `"CANCEL"` | ความหมายของสถานะ ใช้แยกสี/ไอคอนได้ |
| `isDefault` | `"T"` \| `"F"` | ค่าเริ่มต้น **ภายในกลุ่มของมัน** ตามข้อมูลเดิม (ดูข้อ 4) |

### 2.2 ตัวอย่าง (ข้อมูลจริง ณ 17 พ.ย. 2026, 14 รายการ)

`GET /ListOfValues/GetStatus`

```json
[
  { "id": "5899EE5D72CF3652A4AAE69E429D9D4F", "refId": "67F7D634C1D74FDE512C164D6A2A1EF9", "nameTh": "Extenally Transfered", "nameEn": "Extenally Transfered", "isType": "CLOSE", "isDefault": "F" },
  { "id": "332E6D76F258C8294B160C20642AB9F6", "refId": "67F7D634C1D74FDE512C164D6A2A1EF9", "nameTh": "Internally Closed", "nameEn": "Internally Closed", "isType": "CLOSE", "isDefault": "F" },
  { "id": "ED219C0783D7F81CC6B4E2466A678480", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "ทำซ้ำ", "nameEn": "In Process", "isType": "OPEN", "isDefault": "T" },
  { "id": "F8E07541CE9A0039578FE2066B78E88B", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "โทรผิด", "nameEn": "Wrong Number", "isType": "CLOSE", "isDefault": "F" },
  { "id": "B56BFA064D39DDDCBA4E7F10A2BBA230", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "โทรเล่น-จิตไม่ปกติ", "nameEn": null, "isType": "CLOSE", "isDefault": "F" },
  { "id": "A7E0CAFC1FC4C18FC1544FB2E8933D6C", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "โทรเล่น-เด็กโทรเล่น", "nameEn": null, "isType": "CLOSE", "isDefault": "F" },
  { "id": "19C49E66B6F9EF1BCE11A3B93DED12C4", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "โทรเล่น-โทรด่าหยาบคาย", "nameEn": "โทรเล่น-โทรด่าหยาบคาย", "isType": "CLOSE", "isDefault": "F" },
  { "id": "8CC720CB54323BCD7EE436C12F7E660E", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "โทรเล่น-เสียงเงียบ", "nameEn": "โทรเล่น-เสียงเงียบ", "isType": "CLOSE", "isDefault": "F" },
  { "id": "67F7D634C1D74FDE512C164D6A2A1EF9", "refId": "67F7D634C1D74FDE512C164D6A2A1EF9", "nameTh": "ปิดเรื่อง", "nameEn": "Closed", "isType": "CLOSE", "isDefault": "F" },
  { "id": "60CD725EA4455BA6770FC94BC78F466F", "refId": "60CD725EA4455BA6770FC94BC78F466F", "nameTh": "เปิดเรื่อง", "nameEn": "Open", "isType": "OPEN", "isDefault": "T" },
  { "id": "1B751556C1458196BA0EB37037415A25", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "ยกเลิก", "nameEn": "Cancel", "isType": "CANCEL", "isDefault": "F" },
  { "id": "9CF68DA7874E672EFCA3A6F0824FE543", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "สัญญาณไม่ชัดเจน", "nameEn": "Noise", "isType": "CLOSE", "isDefault": "F" },
  { "id": "C833A9E6D354B2AC350A50CFB41852A8", "refId": "1B751556C1458196BA0EB37037415A25", "nameTh": "สายหลุด", "nameEn": "Communication Terminate", "isType": "CLOSE", "isDefault": "F" },
  { "id": "19C49E66B6F9EF1BCE11A3B93DED12C8", "refId": "60CD725EA4455BA6770FC94BC78F466F", "nameTh": "อยู่ระหว่างดำเนินการ", "nameEn": "Pending", "isType": "PENDING", "isDefault": "T" }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `StatusItem[]` |
| 400 | ส่ง `statusType` ที่ไม่ใช่ `SERVICE` / `ACTIVITY` | ProblemDetails (`errors.statusType`) |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/status.ts
export interface StatusItem {
  id: string;
  refId: string;
  nameTh: string;
  nameEn: string | null;
  isType: "OPEN" | "PENDING" | "CLOSE" | "CANCEL";
  isDefault: "T" | "F";
}

export async function fetchStatuses(opts: { token: string; signal?: AbortSignal }): Promise<StatusItem[]> {
  const res = await fetch("/api/backend/ListOfValues/GetStatus", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("โหลดรายการสถานะไม่สำเร็จ");
}
```

```tsx
// drop-down ธรรมดา: แสดงทุกรายการตามลำดับที่ API ส่งมา แล้วส่ง id ที่เลือกเป็น statusId
<select value={statusId} onChange={(e) => setStatusId(e.target.value)}>
  <option value="">-- เลือกสถานะ --</option>
  {statuses.map((s) => (
    <option key={s.id} value={s.id}>{s.nameTh}</option>
  ))}
</select>
```

ถ้าอยากจัดกลุ่มตามสถานะหลัก ใช้ `refId` จับกลุ่ม (สถานะหลักคือตัวที่ `refId === id`) แล้วใช้ `<optgroup>` ได้

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้

---

## 4. ข้อควรรู้ (สถานะ ณ 17 พ.ย. 2026)

| เรื่อง | ตอนนี้ |
|---|---|
| ใช้กับ `statusId` ของ `AddNewService` | **ได้ทุกตัวในรายการนี้** บันทึกตาม id ที่ส่ง |
| `isDefault` | เป็นค่าเริ่มต้นภายในกลุ่มตามข้อมูลเดิม ในรายการนี้มี `"T"` 3 ตัว (เปิดเรื่อง, อยู่ระหว่างดำเนินการ, ทำซ้ำ) **อย่าใช้เลือกค่าเริ่มต้นของ drop-down** ถ้าต้องเลือกไว้ให้ก่อน ให้ตกลงกับทีมว่าใช้ตัวไหน |
| เรื่องเก่าในระบบ | ส่วนใหญ่เก็บ อยู่ระหว่างดำเนินการ (101 เรื่อง) และ Internally Closed (83 เรื่อง) |
| สถานะของงาน | `?statusType=ACTIVITY` ได้ เปิด / ปิด / ยกเลิก **ใช้กับ `AddNewService` ไม่ได้** (400) |

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetStatus
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetStatus -H "Authorization: Bearer <token>"
curl "http://localhost:5018/ListOfValues/GetStatus?statusType=ACTIVITY" -H "Authorization: Bearer <token>"
```
