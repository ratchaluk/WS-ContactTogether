# แก้ไขเรื่อง / เปิดเรื่องซ้ำ (`PUT /Services/UpdateService`) สำหรับ Frontend

ใช้ **แก้ไขเรื่อง (service request) ที่เปิดไว้แล้ว** จากปุ่ม "แก้ไข" ในหน้ารายละเอียดเรื่อง
และใช้ **เปิดเรื่องที่ปิดไปแล้วกลับมาใหม่ (reopen)** ด้วย endpoint เดียวกัน

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`UpdateService`), `Dtos/UpdateServiceRequest.cs`,
> `Dtos/ServiceFieldsRequest.cs`, `Dtos/ServiceResponse.cs`
>
> เอกสารที่ใช้คู่กัน: [GetServiceById.md](GetServiceById.md) (โหลดค่าเดิมมาเติมฟอร์ม และดูประวัติการเปิดซ้ำ),
> [AddNewService.md](AddNewService.md) (ความหมายของแต่ละ field และที่มาของ id),
> [ServiceStatusMain.md](ServiceStatusMain.md) (สถานะ)
>
> **เพิ่มเมื่อ 23 พ.ย. 2569** (backend ตั้ง `dateClosed` ให้เอง และเปิดซ้ำด้วยสถานะ OPEN)

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `PUT` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `id` (บังคับ) = `TblService.Id` hex 32 ตัว (ตัวเดียวกับ `id` ของ `GetServiceById`) ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| Body | JSON (`Content-Type: application/json`) **ส่งเฉพาะ field ที่เปลี่ยน** |
| Backend (dev) | `http://localhost:5018/Services/UpdateService?id=…` |
| URL จาก Next.js | `/api/backend/Services/UpdateService?id=…` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |
| สำเร็จ | `200` พร้อมเรื่องหลังแก้ไข (รูปแบบเดียวกับ response ของ `AddNewService`) |

**ห้ามใช้ `code` (เช่น `69080400002`) แทน `id`** จะได้ 404

---

## 2. Request body

### 2.1 กติกาการส่งค่า (สำคัญ)

| ส่งแบบไหน | ผล |
|---|---|
| **ไม่ส่ง field** หรือส่ง `null` | **คงค่าเดิม** |
| ส่ง `""` (ข้อความว่าง) | **ล้างค่า** เป็น NULL (เฉพาะ field ข้อความ) |
| ส่งค่าใหม่ | บันทึกค่าใหม่ (ตัดช่องว่างหน้า/หลังให้) |

ข้อยกเว้น:

- **`serviceTitleId`** ล้างไม่ได้ ส่ง `""` หรือไม่ส่ง = คงค่าเดิม
- **วันที่** (`dateOpened`, `dateClosed`, `onScene`) ล้างไม่ได้ ส่ง `null` = คงค่าเดิม
- **`dateClosed` backend จัดการให้:** ตั้งเป็นเวลาปัจจุบันตอนปิดเรื่อง และล้างตอนเปิดเรื่องซ้ำ (ข้อ 3)
  ปกติ**ไม่ต้องส่ง**

> ⚠️ ต่างจาก `AddNewService`: ที่นั่นแนะนำให้ไม่ส่ง `""` แต่ที่นี่ `""` แปลว่า **"ลบค่าออก"**
> ถ้าฟอร์มผูก input ไว้กับทุก field แล้วส่งทั้งก้อน ช่องที่ผู้ใช้ไม่ได้กรอกจะถูกล้างไปด้วย
> แนะนำให้ส่งเฉพาะ field ที่ผู้ใช้แก้ (เทียบกับค่าเดิมจาก `GetServiceById`) ดูโค้ดตัวอย่างข้อ 5

### 2.2 Field

ชื่อ field เหมือน `AddNewService` ทุกตัว (ความหมายและที่มาของ id ดู [AddNewService.md](AddNewService.md) ข้อ 2.1)
และเพิ่ม `reOpenComment`

| Field | ยาวสุด | หมายเหตุ |
|---|---|---|
| `categoryId` | 200 | ประเภทบริการ (เฉพาะ `categoryType = "SERVICE"`) |
| `statusId` | 200 | สถานะชุด SERVICE ใช้ drop-down สถานะหลักได้เลย: ปิด/ยกเลิก = ปิดเรื่อง, "เปิดเรื่อง" ให้เรื่องที่ปิดแล้ว = เปิดเรื่องซ้ำ (ข้อ 3) |
| `serviceTitleId` | 50 | หัวข้อเรื่อง (`GetServiceTitle`) ต้องเป็นรายการที่เปิดใช้งาน ล้างไม่ได้ |
| `ownerId` | 200 | เจ้าหน้าที่เจ้าของเรื่อง (ไม่มีการใส่ผู้ login ให้อัตโนมัติแบบ `AddNewService`) |
| `channelIncomingId` / `channelOutgoingId` | 200 | ช่องทาง (`GetChannel`) |
| `callBack` | 200 | `"Y"` / `"N"` / `"D"` (API ไม่ตรวจค่า) |
| `accountId` | 200 | ผู้ติดต่อ |
| `summary` | 500 | |
| `detail` | ไม่จำกัด | |
| `serviceArea` | 2000 | |
| `organizationId` | 200 | |
| `serviceReference` | 500 | id จาก `GetReference` |
| `serviceReferenceLink` | 500 | |
| `areaId` | 200 | id จาก `GetArea` |
| `remark` | 500 | |
| `levelSeverityId` / `levelPriorityId` / `levelSecretId` | 500 | id จาก `GetLevel` ตามประเภท |
| `dateOpened` / `onScene` | – | เวลาไทย `yyyy-MM-ddTHH:mm:ss` ห้ามมี `Z` / `+07:00` |
| `dateClosed` | – | **ไม่ต้องส่ง** (ข้อ 3.1) ส่งเฉพาะตอนปิดเรื่องแล้วต้องการลงเวลาย้อนหลัง |
| **`reOpenComment`** | **100** | เหตุผลที่เปิดเรื่องซ้ำ ใช้เฉพาะตอนเปิดเรื่องซ้ำ ไม่ส่งก็ได้ (บันทึกเป็นค่าว่าง) |

- **id ทุกตัวที่ส่งต้องมีอยู่จริง** ไม่งั้นได้ 400 ที่ field นั้น (`No row with Id '...'`)
- **`reOpenComment` เกิน 100 ตัวอักษร** ได้ 400 (`errors.ReOpenComment`) ใส่ `maxLength={100}` ที่ช่องกรอกด้วย
- **backend ใส่ให้เอง** (ส่งมาก็ไม่มีผล): `updated` (= เวลาที่บันทึก), `updatedBy` (= ผู้ที่ login อยู่)
  ส่วน `id`, `code`, `isEnable`, `created`, `createdBy` แก้ไม่ได้

---

## 3. ปิดเรื่อง / เปิดเรื่องซ้ำ

ใช้ **drop-down สถานะหลัก 3 ตัว** ตาม [ServiceStatusMain.md](ServiceStatusMain.md) ได้เลย backend ดูจาก
`isType` ของสถานะที่ส่งมา (ไม่ได้ดูจากชื่อ) แล้วจัดการ `dateClosed` ให้เอง

### 3.1 `dateClosed` เปลี่ยนอย่างไร

| ส่ง `statusId` ที่ `isType` เป็น | เรื่องตอนนี้ | ผล |
|---|---|---|
| `CLOSE` / `CANCEL` (ปิดเรื่อง / ยกเลิก) | ยังไม่ปิด | **ปิดเรื่อง:** `dateClosed` = เวลาที่บันทึก (หรือค่าที่ส่งมา) |
| `CLOSE` / `CANCEL` | ปิดอยู่แล้ว | `dateClosed` คงเดิม (หรือค่าที่ส่งมา ถ้าต้องการแก้เวลาปิด) |
| `OPEN` (เปิดเรื่อง) | ปิดอยู่ (`dateClosed` มีค่า) | **เปิดเรื่องซ้ำ:** บันทึกประวัติ + ล้าง `dateClosed` (ข้อ 3.2) |
| `OPEN` | ยังไม่ปิด | เปลี่ยนสถานะอย่างเดียว |
| `PENDING` (อยู่ระหว่างดำเนินการ) | ใดก็ได้ | เปลี่ยนสถานะอย่างเดียว **ไม่นับเป็นการเปิดซ้ำ** |
| ไม่ส่ง `statusId` | ใดก็ได้ | สถานะและ `dateClosed` คงเดิม (แก้ช่องอื่นไม่ทำให้เปิดซ้ำ) |

**ส่ง `dateClosed` เอง** (ลงเวลาย้อนหลัง) ได้เฉพาะคู่กับสถานะปิด/ยกเลิก และต้อง:
- ไม่เป็นเวลาในอนาคต
- ไม่ก่อน `dateOpened`

ผิดเงื่อนไข หรือส่ง `dateClosed` กับสถานะอื่น ได้ 400 `errors.DateClosed`

### 3.2 เปิดเรื่องซ้ำ: สิ่งที่ backend ทำ (ใน transaction เดียว)

1. เพิ่มประวัติ 1 แถว: ครั้งที่ (`line`) ต่อจากครั้งล่าสุดของเรื่องนั้น เริ่มที่ 1, `reOpenComment`,
   วันที่ปิดเดิม, ผู้ปิด, เวลาที่เปิดซ้ำ และผู้เปิดซ้ำ (= ผู้ที่ login อยู่)
2. ล้าง `dateClosed` เป็น `null`
3. บันทึก field อื่นที่ส่งมาพร้อมกันตามปกติ

ประวัติดูได้จาก `reOpenHistory` ของ `GET /Services/GetServiceById` ([GetServiceById.md](GetServiceById.md) ข้อ 2.5)

### 3.3 ตัวอย่าง

ปิดเรื่อง (ไม่ต้องส่ง `dateClosed`):

```json
{ "statusId": "67F7D634C1D74FDE512C164D6A2A1EF9" }
```

เปิดเรื่องซ้ำ:

```json
{
  "statusId": "60CD725EA4455BA6770FC94BC78F466F",
  "reOpenComment": "ผู้ร้องแจ้งว่าปัญหายังไม่ได้รับการแก้ไข"
}
```

ผล: `200`, `dateClosed` = `null`, `updated`/`updatedBy` = ตอนนี้/ผู้ที่ login

id ข้างบนใช้อ้างอิงเท่านั้น ให้ดึงจาก `GET /ListOfValues/GetStatus` แล้วเลือกตาม `isType`

---

## 4. Response

### 4.1 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | แก้ไขสำเร็จ (รวมกรณีเปิดซ้ำ) | `ServiceResponse` (เรื่องหลังแก้ รูปแบบเดียวกับ `AddNewService`) |
| 400 | ไม่ส่ง `id`, id lookup ผิด, ยาวเกิน, รูปแบบวันที่ผิด, `dateClosed` ผิดกติกา (ข้อ 3.1), JSON พัง | ProblemDetails (`errors.<field>`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีเรื่องที่ `id` นี้ หรือเรื่องถูกปิดใช้งาน | `{ "statusCode": 404, "message": "ไม่พบข้อมูลเรื่อง" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

ตัวอย่าง 400:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "StatusId": ["No row with Id 'XXX'."] }
}
```

key ใน `errors` เป็น PascalCase (`StatusId`, `ServiceTitleId`, …) map กลับเป็น camelCase ก่อนแสดงใต้ช่อง

### 4.2 หลังบันทึก

response มีแค่ id ยังไม่มีชื่อ / path / `reOpenHistory` ถ้าจะแสดงหน้ารายละเอียดต่อ ให้เรียก
`GetServiceById` ใหม่ (หรือ revalidate ข้อมูลหน้า)

---

## 5. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/services.ts
import type { ServiceDetail } from "./services"; // type จาก GetServiceById.md

export interface UpdateServiceBody {
  categoryId?: string | null;
  statusId?: string | null;
  serviceTitleId?: string | null;
  ownerId?: string | null;
  channelIncomingId?: string | null;
  channelOutgoingId?: string | null;
  callBack?: "Y" | "N" | "D" | "" | null;
  accountId?: string | null;
  summary?: string | null;
  detail?: string | null;
  serviceArea?: string | null;
  organizationId?: string | null;
  serviceReference?: string | null;
  serviceReferenceLink?: string | null;
  areaId?: string | null;
  remark?: string | null;
  levelSeverityId?: string | null;
  levelPriorityId?: string | null;
  levelSecretId?: string | null;
  dateOpened?: string | null; // "yyyy-MM-ddTHH:mm:ss" เวลาไทย
  dateClosed?: string | null;
  onScene?: string | null;
  reOpenComment?: string | null; // ≤ 100
}

export class ValidationError extends Error {
  constructor(public errors: Record<string, string[]>) { super("ข้อมูลไม่ถูกต้อง"); }
}

export async function updateService(
  id: string,
  body: UpdateServiceBody,
  opts: { token: string; signal?: AbortSignal },
) {
  const res = await fetch(`/api/backend/Services/UpdateService?id=${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { Authorization: `Bearer ${opts.token}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 400) throw new ValidationError((await res.json()).errors ?? {});
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 404) throw new Error("ไม่พบข้อมูลเรื่อง");
  throw new Error("บันทึกไม่สำเร็จ");
}
```

**ส่งเฉพาะ field ที่เปลี่ยน** (กันการล้างค่าโดยไม่ตั้งใจ):

```ts
function diff(original: ServiceDetail, form: UpdateServiceBody): UpdateServiceBody {
  const changed: UpdateServiceBody = {};
  for (const key of Object.keys(form) as (keyof UpdateServiceBody)[]) {
    const before = (original as any)[key] ?? "";
    const after = form[key] ?? "";
    if (before !== after) (changed as any)[key] = after; // after = "" → ล้างค่า
  }
  return changed;
}

await updateService(detail.id, diff(detail, form), { token });
```

**ปิดเรื่อง / เปิดเรื่องซ้ำ** (สถานะหลักจาก [ServiceStatusMain.md](ServiceStatusMain.md)):

```ts
const main = statuses.filter((s) => s.refId === s.id); // จาก GET /ListOfValues/GetStatus
const closeStatus = main.find((s) => s.isType === "CLOSE")!;
const openStatus = main.find((s) => s.isType === "OPEN")!;

// ปิดเรื่อง: ไม่ต้องส่ง dateClosed
await updateService(detail.id, { statusId: closeStatus.id }, { token });

// เปิดเรื่องซ้ำ (แสดงปุ่มเฉพาะเรื่องที่ปิดแล้ว)
if (detail.dateClosed) {
  await updateService(detail.id, {
    statusId: openStatus.id,
    reOpenComment: comment.trim().slice(0, 100),
  }, { token });
}
```

- ถ้าใช้ฟอร์มแก้ไขรวมที่มี drop-down สถานะ ไม่ต้องแยกปุ่มก็ได้ เลือก "เปิดเรื่อง" ให้เรื่องที่ปิดแล้วก็เป็นการเปิดซ้ำ
  (ควรแสดงช่อง "เหตุผลที่เปิดซ้ำ" เมื่อเรื่องปิดอยู่และผู้ใช้เลือกสถานะ OPEN)
- ปิดปุ่มระหว่างรอ response
- วันที่: อย่าใช้ `date.toISOString()` เวลาจะคลาด 7 ชั่วโมง ใช้ค่าจาก `<input type="datetime-local">` ได้เลย

---

## 6. ทดสอบด้วย REST Client / curl

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (หัวข้อ Update a service / Close / Reopen)

```bash
curl -X PUT "http://localhost:5018/Services/UpdateService?id=427D66AA13734763AC1D99775554659B" \
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
  -d '{"summary":"แก้ไขสรุปเรื่อง","remark":"หมายเหตุใหม่"}'
```

---

## 7. ข้อควรรู้ (ณ 23 พ.ย. 2569)

| เรื่อง | ผลกับ frontend |
|---|---|
| **`""` = ล้างค่า** | ส่งเฉพาะ field ที่เปลี่ยน (ข้อ 2.1) |
| **ปิด / เปิดซ้ำ** | ใช้ drop-down สถานะหลักได้เลย backend ตั้ง/ล้าง `dateClosed` ให้ (ข้อ 3.1) |
| **Pending ไม่ใช่การเปิดซ้ำ** | ต้องเป็นสถานะ `isType = OPEN` |
| **`reOpenComment`** | ยาวสุด 100 ตัวอักษร |
| **ผู้ปิดเรื่องในประวัติ** | คือผู้แก้ไขล่าสุดก่อนเปิดซ้ำ ถ้าหลังปิดไม่มีใครแก้ช่องอื่น จะตรงกับผู้ปิดจริง |
| **ไม่มีการตรวจชนกันแบบ optimistic** | สองคนแก้เรื่องเดียวกัน คนที่บันทึกทีหลังทับเฉพาะ field ที่ตัวเองส่ง (field อื่นไม่ถูกทับ) |
| **ลบเรื่อง** | ยังไม่มี endpoint |
