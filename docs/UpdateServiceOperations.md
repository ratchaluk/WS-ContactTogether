# แก้ไขการดำเนินการของเรื่อง (`PUT /Services/UpdateServiceOperations`) สำหรับ Frontend

ใช้ **แก้ไขการดำเนินการ (งาน / activity) ที่บันทึกไว้แล้ว** เช่น เปลี่ยนสถานะเป็น "ปิด" เมื่อทำเสร็จ
หรือแก้รายละเอียด แก้เฉพาะ field ที่ส่งมา

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`UpdateServiceOperations`),
> `Dtos/UpdateServiceOperationRequest.cs`, `Dtos/ServiceOperationResponse.cs`
>
> เอกสารที่ใช้คู่กัน: [AddNewServiceOperations.md](AddNewServiceOperations.md) (ความหมายของแต่ละ field
> และ type `ServiceOperation`), [GetServiceOperations.md](GetServiceOperations.md) (รายการ + `id`),
> [GetStatusByStatusTypeActivity.md](GetStatusByStatusTypeActivity.md)
>
> **เพิ่มเมื่อ 23 พ.ย. 2569** · ปรับ 23 พ.ย. 2569: `dateClosed` server จัดการเอง, `categoryId` ชุด ACTIVITY, `isReceived` server จัดการเอง
> · ปรับ 25 พ.ย. 2569: `groupId` ใช้ค่าจาก `GetEmployeeGroup` และเลือก `ownerId` จาก `GetEmployeeByGroup` ได้แล้ว
> · ปรับ 26 พ.ย. 2569: `contactId` ล้างไม่ได้แล้ว (ส่ง `""` = คงเดิม)

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `PUT` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `id` (บังคับ) = `id` ของการดำเนินการ (GUID) จาก `GetServiceOperations` หรือ response ของ `AddNewServiceOperations` มีหรือไม่มีขีด ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| Body | JSON (`Content-Type: application/json`) **ส่งเฉพาะ field ที่เปลี่ยน** |
| Backend (dev) | `http://localhost:5018/Services/UpdateServiceOperations?id=…` |
| URL จาก Next.js | `/api/backend/Services/UpdateServiceOperations?id=…` (ผ่าน `rewrites()` ใน `next.config.ts`) |
| สำเร็จ | `200` พร้อมการดำเนินการหลังแก้ไข (รูปแบบเดียวกับ response ของ `AddNewServiceOperations`) |

**ใช้ `id` (GUID) เท่านั้น** ห้ามใช้ `code` หรือ `serviceId` + `line` จะได้ 400 / 404

---

## 2. Request body

### 2.1 กติกาการส่งค่า

ทุก field ไม่บังคับ

| Field | ไม่ส่ง / `null` | ส่ง `""` | หมายเหตุ |
|---|---|---|---|
| `statusId` | คงเดิม | คงเดิม | id จาก `GetStatusByStatusTypeActivity` เท่านั้น |
| `summary` | คงเดิม | คงเดิม | ล้างไม่ได้ |
| `categoryId` | คงเดิม | คงเดิม | ล้างไม่ได้ ต้องเป็นชุด `ACTIVITY` |
| `detail` | คงเดิม | **ล้างค่า** | |
| `ownerId` | คงเดิม | **ล้างค่า** | id จาก `GetEmployeeByGroup` (หรือ `GetEmployee` ถ้าไม่มีช่องกลุ่ม) เปลี่ยนผู้รับ = `isReceived` กลับเป็น `"F"` (ดูข้อ 2.2) |
| `contactId` | คงเดิม | คงเดิม | ล้างไม่ได้ (บังคับตอนเพิ่มงาน ตั้งแต่ 26 พ.ย. 2569) เปลี่ยนได้เป็น id ที่มีอยู่ใน `TblContact` เช่น id ของ `AddNewContact` |
| `groupId` | คงเดิม | **ล้างค่า** | `id` จาก `GetEmployeeGroup` ([GetEmployeeGroup.md](GetEmployeeGroup.md)) ผู้รับของกลุ่มดูจาก `GetEmployeeByGroup` |
| `dateOpened` | คงเดิม | – | ล้างไม่ได้ |

แก้ไม่ได้: `serviceId`, `line`, `code` (ส่งมาก็ไม่มีผล)
**backend ใส่ให้เอง**: `updated` (= เวลาที่บันทึก), `updatedBy` (= ผู้ที่ login อยู่), `dateClosed`, `isReceived` (ดูข้อ 2.2 ส่งมาจะถูกข้าม)

> ⚠️ ถ้าฟอร์มส่งทุก field ทั้งก้อน ช่อง `detail` / `ownerId` / `groupId` ที่ว่างจะถูกล้าง
> แนะนำให้ส่งเฉพาะ field ที่ผู้ใช้แก้ (เทียบกับค่าเดิม) ดูโค้ดตัวอย่างข้อ 4

### 2.2 กติกาที่ควรรู้

- **id ทุกตัวที่ส่งต้องมีอยู่จริง** ไม่งั้นได้ 400 ที่ field นั้น ส่ง id ตามที่ได้จาก API (ตัวพิมพ์ใหญ่) อย่าแปลงเป็นตัวเล็ก
  **ยกเว้น `groupId`** ที่ backend ยังไม่ตรวจ บันทึกตามที่ส่งมา จึงต้องส่ง `id` จาก `GetEmployeeGroup` เท่านั้น
  (เป็น string เช่น `"5"` อย่าแปลงเป็นตัวเลข)
- **`dateClosed` server จัดการเอง ไม่รับจาก client:**

  | ส่ง | ผลกับ `dateClosed` |
  |---|---|
  | `statusId` ที่ `isType` เป็น `CLOSE` / `CANCEL` | งานยังไม่ปิด = เวลาของ server ตอนบันทึก, ปิดอยู่แล้ว = คงเดิม |
  | `statusId` ที่ `isType` เป็น `OPEN` | `null` (เปิดงานกลับ) |
  | ไม่ส่ง `statusId` | คงเดิม |

- **`isReceived` server จัดการเอง (ตามระบบเดิม):** เปลี่ยน `ownerId` เป็นคนอื่น (หรือล้าง) → `"F"` รอการตอบรับ
  ผู้รับใหม่จะรับงานเมื่อเปิดดูเรื่อง ([ReceiveService.md](ReceiveService.md)) ไม่เปลี่ยนผู้รับ = คงเดิม
- **`categoryId`**: ต้องเป็นประเภทการดำเนินการ (ชุด `ACTIVITY`) ที่เปิดใช้งาน ส่งชุดอื่นได้ 400
- **วันที่** เป็นเวลาไทย `yyyy-MM-ddTHH:mm:ss` ห้ามมี `Z` / `+07:00` API ไม่ตรวจลำดับวันที่
- **ไม่มีการกันชนกัน**: สองคนแก้งานเดียวกัน คนที่บันทึกทีหลังทับเฉพาะ field ที่ตัวเองส่ง

### 2.3 ตัวอย่าง

ปิดงาน:

```http
PUT /Services/UpdateServiceOperations?id=6a1d0c4e-2f3b-f111-8e93-96f1f7a12f5e
Authorization: Bearer <token>
Content-Type: application/json

{
  "statusId": "CB0A973609E8D684F2944D92ED4126A1",
  "detail": "ได้รับเอกสารครบแล้ว ปิดงาน"
}
```

ผล: `dateClosed` = เวลาของ server ตอนบันทึก

มอบงานให้เจ้าหน้าที่คนอื่น:

```json
{ "ownerId": "04EFF8A54A56F4D240E48E0520C223DB" }
```

---

## 3. Response

### 3.1 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | แก้ไขสำเร็จ | `ServiceOperation` (ดู [AddNewServiceOperations.md](AddNewServiceOperations.md) ข้อ 3.1) |
| 400 | ไม่ส่ง `id` หรือไม่ใช่ GUID, id lookup ผิด, ยาวเกิน, รูปแบบวันที่ผิด, JSON พัง | ProblemDetails (`errors.<Field>` เช่น `errors.id`, `errors.StatusId`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีการดำเนินการที่ `id` นี้ หรือถูกปิดใช้งาน | `{ "statusCode": 404, "message": "ไม่พบข้อมูลการดำเนินการ" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

response มีแต่ id ไม่มีชื่อ ให้แปลงจาก lookup ที่โหลดไว้

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/serviceOperations.ts
import { ValidationError, type ServiceOperation } from "./serviceOperations"; // จาก AddNewServiceOperations.md

export interface UpdateServiceOperationBody {
  statusId?: string | null;
  summary?: string | null;
  detail?: string | null;
  categoryId?: string | null;
  ownerId?: string | null;    // id จาก GetEmployeeByGroup
  contactId?: string | null;
  groupId?: string | null;    // id จาก GetEmployeeGroup (string เช่น "5")
  dateOpened?: string | null;
  // ไม่มี dateClosed: server ตั้งเอง
}

export async function updateServiceOperation(
  id: string,
  body: UpdateServiceOperationBody,
  opts: { token: string; signal?: AbortSignal },
): Promise<ServiceOperation> {
  const res = await fetch(`/api/backend/Services/UpdateServiceOperations?id=${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { Authorization: `Bearer ${opts.token}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 400) throw new ValidationError((await res.json()).errors ?? {});
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 404) throw new Error("ไม่พบข้อมูลการดำเนินการ");
  throw new Error("บันทึกการดำเนินการไม่สำเร็จ");
}
```

**ส่งเฉพาะ field ที่เปลี่ยน:**

```ts
function diff(original: ServiceOperation, form: UpdateServiceOperationBody): UpdateServiceOperationBody {
  const changed: UpdateServiceOperationBody = {};
  for (const key of Object.keys(form) as (keyof UpdateServiceOperationBody)[]) {
    const before = (original as any)[key] ?? "";
    const after = form[key] ?? "";
    if (before !== after) (changed as any)[key] = after; // after = "" → ล้างค่า (เฉพาะ field ที่ล้างได้)
  }
  return changed;
}
```

**ปุ่ม "ปิดงาน":**

```ts
const closeStatus = statuses.find((s) => s.isType === "CLOSE")!; // GetStatusByStatusTypeActivity
await updateServiceOperation(op.id, { statusId: closeStatus.id }, { token }); // dateClosed = เวลา server
```

---

## 5. ทดสอบด้วย REST Client / curl

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (หัวข้อ Edit an operation) แทน `id=00000000-…` ด้วย `id` ที่ได้จาก
`AddNewServiceOperations`

```bash
curl -X PUT "http://localhost:5018/Services/UpdateServiceOperations?id=<operation id>" \
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
  -d '{"detail":"แก้ไขรายละเอียด"}'
```

---

## 6. ข้อควรรู้ (ณ 23 พ.ย. 2569)

| เรื่อง | ผลกับ frontend |
|---|---|
| **`id` ของการดำเนินการ** | ได้จาก `GetServiceOperations` ([GetServiceOperations.md](GetServiceOperations.md)) |
| **`""` ล้างค่า** | เฉพาะ `detail` / `ownerId` / `groupId` ส่งเฉพาะ field ที่เปลี่ยน (`contactId` ส่ง `""` = คงเดิม ตั้งแต่ 26 พ.ย. 2569) |
| **`dateClosed`** | server ตั้ง/ล้างเองตามสถานะ ไม่ต้องส่ง (ข้อ 2.2) |
| **สถานะของเรื่องไม่เปลี่ยนตาม** | ปิดการดำเนินการไม่ได้ปิดเรื่อง ใช้ `UpdateService` แยก |
| **ลบการดำเนินการ** | `DELETE /Services/DeleteServiceOperations` ([DeleteServiceOperations.md](DeleteServiceOperations.md)) |
