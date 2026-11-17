# เปิดเรื่อง (`POST /Services/AddNewService`) สำหรับ Frontend

ใช้ **เปิดเรื่องใหม่ (service request)** หนึ่งเรื่องต่อการกดบันทึกหนึ่งครั้ง backend สร้างเลขที่เรื่องให้เอง
แล้วคืนเรื่องที่บันทึกกลับมาทั้งแถว

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`AddNewService`), `Dtos/CreateServiceRequest.cs`,
> `Dtos/ServiceResponse.cs`
>
> เอกสารที่ใช้คู่กัน: [GetChannel.md](GetChannel.md) (ช่องทาง), [GetServices.md](GetServices.md) (ค้นหาเรื่องที่เปิดแล้ว)

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `POST` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Body | JSON (`Content-Type: application/json`) |
| Backend (dev) | `http://localhost:5018/Services/AddNewService` |
| URL จาก Next.js | `/api/backend/Services/AddNewService` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |
| สำเร็จ | `201` พร้อมเรื่องที่บันทึก |

---

## 2. Request body

### 2.1 Field

**API ไม่บังคับ field ไหนเลย** (ดู 2.3) ทุก field เป็น string หรือ `null` ยกเว้นวันที่
ชื่อ field ใน JSON เป็น camelCase

| Field | ยาวสุด | ความหมาย | เอาค่ามาจาก |
|---|---|---|---|
| `categoryId` | 200 | ประเภทบริการ | `id` จาก `GET /ListOfValues/GetServiceType` **เฉพาะรายการที่ `categoryType` เป็น `"SERVICE"`** (ดู 2.2) |
| `statusId` | 200 | สถานะของเรื่อง | `id` จาก `GET /ListOfValues/GetStatus` (สถานะประเภท SERVICE ดู [GetStatus.md](GetStatus.md)) บันทึกตามที่ส่ง ส่งสถานะ ACTIVITY = 400 |
| `ownerId` | 200 | เจ้าหน้าที่เจ้าของเรื่อง (`TblEmployee.Id`) ไม่ใช่ผู้บันทึก ผู้บันทึกมาจาก token เสมอ | ปกติไม่ต้องส่ง: ไม่ส่ง / `null` / `""` = **ผู้ที่ login อยู่** ส่งเองเฉพาะตอนมอบเรื่องให้เจ้าหน้าที่คนอื่น (ยังไม่มี endpoint รายชื่อเจ้าหน้าที่) ถ้าต้องใช้ id ของตัวเอง ใช้ `employeeId` จาก `POST /Auth/login` (ค่าเดียวกับ `id` ของ `GET /UserManagement/GetEmployeeProfile`) |
| `channelIncomingId` | 200 | ช่องทางที่ผู้ติดต่อเข้ามา | `id` จาก `GET /ListOfValues/GetChannel` |
| `channelOutgoingId` | 200 | ช่องทางที่จะติดต่อกลับ | `id` จาก `GET /ListOfValues/GetChannel` |
| `callBack` | 200 | ต้องการให้ติดต่อกลับไหม | `"Y"` ต้องการ / `"N"` ไม่ต้องการ / `"D"` สายหลุด |
| `accountId` | 200 | ผู้ติดต่อ | `id` จาก `GET /Account/GetAccounts` หรือจาก response ของ `POST /Account/AddNewAccount` (`GetAccountByPhoneNo` ยังไม่คืน `id`) |
| `summary` | 500 | หัวเรื่อง | ผู้ใช้กรอก |
| `detail` | 1000 | รายละเอียด | ผู้ใช้กรอก |
| `serviceArea` | 2000 | พื้นที่ที่เกี่ยวข้อง (ข้อความอิสระ) | ผู้ใช้กรอก |
| `organizationId` | 200 | หน่วยงานที่รับผิดชอบ | ยังไม่มี endpoint รายการ |
| `serviceReference` | 500 | ที่มาข้อมูล (lookup `TblReference`) | `id` จาก `GET /ListOfValues/GetReference` (เช่น Joomla, Other Website, เจ้าหน้าที่กระทรวง, อื่นๆ) ส่ง id ที่ไม่มีในตาราง = 400 |
| `serviceReferenceLink` | 500 | ลิงก์ที่มาข้อมูล (URL โพสต์/ข่าว) | ผู้ใช้กรอก |
| `remark` | 500 | หมายเหตุ | ผู้ใช้กรอก |
| `areaId` | 200 | พื้นที่ (lookup) | ⚠️ **อย่าส่ง** ตอนนี้ส่งค่าอะไรก็ได้ 400 (ดูข้อ 5) |
| `levelSeverityId` | 500 | ความเร่งด่วน | `id` จาก `GET /ListOfValues/GetLevel?levelType=SEVERITY` (ดู `docs/GetLevel.md`) ส่ง id ผิดประเภท = 400 |
| `levelPriorityId` | 500 | ความสำคัญ | `id` จาก `GET /ListOfValues/GetLevel?levelType=PRIORITY` |
| `levelSecretId` | 500 | ชั้นความลับ | `id` จาก `GET /ListOfValues/GetLevel?levelType=SECRET` |
| `dateOpened` | – | วันเวลาที่เปิดเรื่อง | ไม่ส่ง = **เวลาที่บันทึก** |
| `dateClosed` | – | วันเวลาที่ปิดเรื่อง | ส่งเมื่อเปิดแล้วปิดทันที ไม่ส่ง = ยังไม่ปิด |
| `onScene` | – | วันเวลาที่ถึงหน้างาน | ไม่บังคับ |

**backend ใส่ให้เอง** (ส่งมาก็ไม่มีผล): `id`, `code` (เลขที่เรื่อง), `isEnable` (= `"T"`),
`created` / `updated` (= เวลาที่บันทึก), `createdBy` / `updatedBy` (= ผู้ที่ login อยู่)

### 2.2 กติกาที่ควรรู้

- **id ทุกตัวที่ส่งต้องมีอยู่จริง** ไม่งั้นได้ 400 ที่ field นั้น (ข้อความ `No row with Id '...'`)
- ไม่มีค่าให้ **ไม่ต้องส่ง field นั้น หรือส่ง `null`** อย่าส่ง `""` เพราะ API จะข้ามการตรวจ id แล้วบันทึกค่าว่างลงไป
- **`categoryId`**: `GetServiceType` คืนประเภททุกชุดปนกัน (ประเภทการรับสาย `CONTACT`, ประเภทบริการ `SERVICE`,
  ประเภทงาน `ACTIVITY`) แต่ API ไม่ได้ตรวจว่าเป็นชุดไหน ให้กรองเฉพาะ `categoryType === "SERVICE"` ก่อนแสดง
  และ **อย่าใช้ `categoryId` ของหน้ารับสาย** (`GetContactType`) มาใส่ตรงนี้
- **`callBack`**: API ไม่ตรวจค่า ส่งได้เฉพาะ `"Y"`, `"N"`, `"D"` เพราะรายงานใช้ค่าเหล่านี้แยกเรื่อง
- **วันที่** เป็นเวลาไทย รูปแบบ `yyyy-MM-ddTHH:mm:ss` เช่น `2026-11-16T10:30:00`
  (ค่าจาก `<input type="datetime-local">` ที่ไม่มีวินาทีก็ใช้ได้) **ห้ามต่อท้ายด้วย `Z` หรือ `+07:00`**
  และอย่าใช้ `date.toISOString()` เพราะเวลาจะคลาดไป 7 ชั่วโมง เศษวินาทีจะถูกปัดทิ้ง
- API **ไม่ตรวจ** ว่า `dateClosed` อยู่หลัง `dateOpened` หรือว่าวันที่เป็นอนาคต ให้ตรวจที่หน้าเว็บ
- **กดซ้ำ = เปิดเรื่องซ้ำ** API ไม่กันรายการซ้ำ ต้องปิดปุ่มระหว่างรอ response

### 2.3 Field ที่ต้องบังคับกรอก: ให้ frontend ตรวจเอง

ตอนนี้ API ไม่มี field บังคับ ส่ง `{}` ก็ได้เรื่องเปล่า ๆ มาหนึ่งเรื่อง ให้ตรวจตาม design ก่อนส่ง
อย่างน้อยควรบังคับ `categoryId`, `statusId`, `accountId`, `channelIncomingId`, `summary`
ถ้าตกลงรายการ field บังคับกันได้ backend จะเพิ่มการตรวจให้ แล้วอัปเดตเอกสารนี้

### 2.4 ตัวอย่าง

```json
{
  "categoryId": "<id ประเภทบริการจาก GetServiceType>",
  "statusId": "<id สถานะ>",
  "accountId": "<id ผู้ติดต่อจาก GetAccounts หรือ AddNewAccount>",
  "channelIncomingId": "332E6D76F258C8294B160C20642AB9F5",
  "callBack": "N",
  "summary": "สอบถามขั้นตอนการยื่นเอกสาร",
  "detail": "ผู้ติดต่อสอบถามเอกสารที่ต้องใช้ในการยื่นคำร้อง",
  "dateOpened": "2026-11-16T10:30:00"
}
```

(`332E6D76F258C8294B160C20642AB9F5` คือช่องทาง Phone ใน `GetChannel` ณ 16 พ.ย. 2026)

---

## 3. Response

### 3.1 201: บันทึกสำเร็จ

คืน **เรื่องที่บันทึกทั้งแถว** (object เดียว ไม่มี envelope) พร้อมชื่อสถานะ

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | id ของเรื่อง (hex 32 ตัว) ใช้อ้างถึงเรื่องนี้ต่อ |
| `code` | string | **เลขที่เรื่อง** 11 หลัก ใช้แสดงให้ผู้ใช้เห็น (ดู 3.2) |
| `statusNameTh` / `statusNameEn` | string \| null | ชื่อสถานะ (`null` ถ้าไม่ได้ส่ง `statusId`) |
| `isEnable` | string | `"T"` เสมอ |
| `created` / `updated` | string | เวลาที่บันทึก |
| `createdBy` / `updatedBy` | string | id ของผู้ที่ login อยู่ |
| field อื่น ๆ | | ค่าเดียวกับที่ส่งไป (`categoryId`, `statusId`, `ownerId`, `channelIncomingId`, `callBack`, `channelOutgoingId`, `accountId`, `summary`, `detail`, `serviceArea`, `organizationId`, `onScene`, `serviceReference`, `serviceReferenceLink`, `areaId`, `remark`, `levelSeverityId`, `levelPriorityId`, `levelSecretId`, `dateOpened`, `dateClosed`) |

- `ownerId` เป็น id ของผู้ที่ login ถ้าไม่ได้ส่งมาหรือส่งค่าว่าง ส่วน `dateOpened` เป็นเวลาที่บันทึกถ้าไม่ได้ส่งมา
- วันที่ทุกตัวเป็นเวลาไทย ไม่มี offset เช่น `2026-11-16T10:30:00` field ที่ไม่ได้ส่งเป็น `null`

```json
{
  "id": "3F2A9C1E7B5D4E6F8A0B1C2D3E4F5A6B",
  "code": "69111600001",
  "categoryId": "<id ประเภทบริการ>",
  "statusId": "<id สถานะ>",
  "statusNameTh": "อยู่ระหว่างดำเนินการ",
  "statusNameEn": "In Progress",
  "ownerId": "<id ของผู้ที่ login>",
  "dateOpened": "2026-11-16T10:30:00",
  "dateClosed": null,
  "channelIncomingId": "332E6D76F258C8294B160C20642AB9F5",
  "callBack": "N",
  "channelOutgoingId": null,
  "accountId": "<id ผู้ติดต่อ>",
  "summary": "สอบถามขั้นตอนการยื่นเอกสาร",
  "detail": "ผู้ติดต่อสอบถามเอกสารที่ต้องใช้ในการยื่นคำร้อง",
  "serviceArea": null,
  "organizationId": null,
  "onScene": null,
  "serviceReference": null,
  "serviceReferenceLink": null,
  "areaId": null,
  "remark": null,
  "levelSeverityId": null,
  "levelPriorityId": null,
  "levelSecretId": null,
  "isEnable": "T",
  "created": "2026-11-16T10:31:07",
  "createdBy": "<id ของผู้ที่ login>",
  "updated": "2026-11-16T10:31:07",
  "updatedBy": "<id ของผู้ที่ login>"
}
```

(id และชื่อสถานะในตัวอย่างเป็นค่าสมมุติ)

### 3.2 เลขที่เรื่อง (`code`)

`yyMMdd` แบบปี **พ.ศ.** 6 หลัก + เลขรัน 5 หลักที่เริ่มนับใหม่ทุกวัน
เช่น `69111600001` = เรื่องแรกของวันที่ 16 พ.ย. 2569 ใช้วันที่ตอนบันทึก ไม่ใช่ `dateOpened`

### 3.3 400: ข้อมูลไม่ถูกต้อง

ProblemDetails key ใน `errors` เป็นชื่อ field แบบ **PascalCase** (`CategoryId`, `StatusId`, …)
ข้อความเป็นภาษาอังกฤษ ไม่ควรแสดงให้ผู้ใช้ตรง ๆ (ดูตัวอย่างแปลงข้อความใน 4.1)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "StatusId": ["No row with Id '332E6D76F258C8294B160C20642AB9XX'."],
    "AccountId": ["No row with Id 'ABC'."]
  },
  "traceId": "00-..."
}
```

| สาเหตุ | key ใน `errors` |
|---|---|
| id ไม่มีอยู่จริง | ชื่อ field นั้น เช่น `CategoryId`, `OwnerId`, `ChannelIncomingId` |
| ยาวเกินกำหนด | ชื่อ field นั้น เช่น `Summary` (`... maximum length of '500'.`) |
| `statusId` ไม่มีอยู่ หรือไม่ใช่สถานะประเภท SERVICE (เช่น ส่งสถานะ ACTIVITY มา) | `StatusId` |
| ส่ง `areaId` (ดูข้อ 5) | `AreaId` |
| `level...Id` ไม่มีอยู่ หรือผิดประเภท (เช่น id ชั้นความลับในช่องความเร่งด่วน) | `LevelSeverityId`, `LevelPriorityId`, `LevelSecretId` |
| JSON พัง หรือวันที่ผิดรูปแบบ | `$.dateOpened` (หรือ path ของ field ที่ผิด) และ/หรือ `request` |

ถ้ามีทั้งข้อความยาวเกินและ id ผิด จะได้แค่เรื่องความยาวก่อน แก้แล้วส่งใหม่ถึงจะเห็นเรื่อง id

### 3.4 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 201 | บันทึกสำเร็จ | `ServiceResponse` |
| 400 | ข้อมูลไม่ถูกต้อง (ดู 3.3) | ProblemDetails |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 409 | เลขรันของวันนั้นเกิน 99,999 เรื่อง (แทบไม่เกิด) | `{ "statusCode": 409, "message": "The daily limit of service codes has been reached." }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

### 4.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/services.ts
export interface CreateServiceRequest {
  categoryId?: string | null;
  statusId?: string | null;
  ownerId?: string | null;
  channelIncomingId?: string | null;
  channelOutgoingId?: string | null;
  callBack?: "Y" | "N" | "D" | null;
  accountId?: string | null;
  summary?: string | null;
  detail?: string | null;
  serviceArea?: string | null;
  organizationId?: string | null;
  serviceReference?: string | null;
  serviceReferenceLink?: string | null;
  remark?: string | null;
  dateOpened?: string | null; // "yyyy-MM-ddTHH:mm:ss" เวลาไทย ห้ามมี Z
  dateClosed?: string | null;
  onScene?: string | null;
  levelSeverityId?: string | null; // GetLevel?levelType=SEVERITY
  levelPriorityId?: string | null; // GetLevel?levelType=PRIORITY
  levelSecretId?: string | null;   // GetLevel?levelType=SECRET
  // areaId: ยังส่งไม่ได้ (ดูข้อ 5)
}

export interface ServiceResponse {
  id: string;
  code: string;
  categoryId: string | null;
  statusId: string | null;
  statusNameTh: string | null;
  statusNameEn: string | null;
  ownerId: string | null;
  dateOpened: string | null;
  dateClosed: string | null;
  channelIncomingId: string | null;
  callBack: string | null;
  channelOutgoingId: string | null;
  accountId: string | null;
  summary: string | null;
  detail: string | null;
  serviceArea: string | null;
  organizationId: string | null;
  onScene: string | null;
  serviceReference: string | null;
  serviceReferenceLink: string | null;
  areaId: string | null;
  remark: string | null;
  levelSeverityId: string | null;
  levelPriorityId: string | null;
  levelSecretId: string | null;
  isEnable: string;
  created: string;
  createdBy: string;
  updated: string;
  updatedBy: string;
}

export class CreateServiceError extends Error {
  constructor(
    public status: number,
    message: string,
    /** ข้อความภาษาไทยต่อ field (key เป็น camelCase ตรงกับ CreateServiceRequest) */
    public fieldErrors: Record<string, string> = {},
  ) {
    super(message);
  }
}

const FIELD_LABELS: Record<string, string> = {
  categoryId: "ประเภทบริการ",
  statusId: "สถานะ",
  ownerId: "เจ้าของเรื่อง",
  channelIncomingId: "ช่องทางที่ติดต่อเข้ามา",
  channelOutgoingId: "ช่องทางที่ติดต่อกลับ",
  accountId: "ผู้ติดต่อ",
  organizationId: "หน่วยงาน",
  summary: "หัวเรื่อง",
  detail: "รายละเอียด",
};

/** แปลง errors ของ ProblemDetails เป็นข้อความภาษาไทยต่อ field */
function toFieldErrors(errors: Record<string, string[]> | undefined): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, messages] of Object.entries(errors ?? {})) {
    // "StatusId" -> "statusId", "$.dateOpened" -> "dateOpened"
    const name = key.replace(/^\$\./, "").replace(/^./, (c) => c.toLowerCase());
    const label = FIELD_LABELS[name] ?? name;
    result[name] = messages.some((m) => m.includes("maximum length"))
      ? `${label} ยาวเกินกำหนด`
      : messages.some((m) => m.startsWith("No row with Id"))
        ? `ไม่พบ${label}ที่เลือก กรุณาเลือกใหม่`
        : `${label} ไม่ถูกต้อง`;
  }
  return result;
}

export async function createService(
  body: CreateServiceRequest,
  opts: { token: string; signal?: AbortSignal },
): Promise<ServiceResponse> {
  const res = await fetch("/api/backend/Services/AddNewService", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${opts.token}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
    signal: opts.signal,
  });

  if (res.status === 201) return res.json();
  if (res.status === 401) throw new CreateServiceError(401, "กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 400) {
    const problem = await res.json().catch(() => null);
    throw new CreateServiceError(400, "กรุณาตรวจสอบข้อมูล", toFieldErrors(problem?.errors));
  }
  if (res.status === 409) throw new CreateServiceError(409, "วันนี้เปิดเรื่องครบจำนวนสูงสุดแล้ว กรุณาติดต่อผู้ดูแลระบบ");
  throw new CreateServiceError(res.status, "เปิดเรื่องไม่สำเร็จ");
}

/** ค่าจาก <input type="datetime-local"> ("2026-11-16T10:30") ส่งได้เลย; ถ้าเป็น Date ให้ใช้ตัวนี้ */
export function toThaiLocalIso(d: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}
```

### 4.2 ใช้ในฟอร์มเปิดเรื่อง

```tsx
"use client";

import { useState } from "react";
import { createService, CreateServiceError, type CreateServiceRequest } from "@/lib/api/services";

/** field ที่ frontend บังคับเอง เพราะ API ยังไม่บังคับ (ดู 2.3) */
const REQUIRED: (keyof CreateServiceRequest)[] = [
  "categoryId", "statusId", "accountId", "channelIncomingId", "summary",
];

export function NewServiceForm({ token }: { token: string }) {
  const [form, setForm] = useState<CreateServiceRequest>({ callBack: "N" });
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);

  const missing = REQUIRED.filter((k) => !String(form[k] ?? "").trim());
  const canSubmit = missing.length === 0 && !saving;

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit) return;
    setSaving(true); // ปิดปุ่มไว้ กันกดซ้ำแล้วได้ 2 เรื่อง
    setErrors({});
    setMessage(null);
    try {
      // ตัด field ที่เป็น "" ออก ให้กลายเป็น "ไม่ได้ส่ง" (ดู 2.2)
      const body = Object.fromEntries(
        Object.entries(form).filter(([, v]) => v !== "" && v != null),
      ) as CreateServiceRequest;
      const created = await createService(body, { token });
      setMessage(`เปิดเรื่องเลขที่ ${created.code} เรียบร้อยแล้ว`);
      setForm({ callBack: "N" });
    } catch (err) {
      if (err instanceof CreateServiceError) {
        setErrors(err.fieldErrors);
        setMessage(err.message);
      } else {
        setMessage("เปิดเรื่องไม่สำเร็จ");
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={onSubmit}>
      <input
        placeholder="หัวเรื่อง"
        maxLength={500}
        value={form.summary ?? ""}
        onChange={(e) => setForm({ ...form, summary: e.target.value })}
      />
      {errors.summary && <p role="alert">{errors.summary}</p>}

      <textarea
        placeholder="รายละเอียด"
        maxLength={1000}
        value={form.detail ?? ""}
        onChange={(e) => setForm({ ...form, detail: e.target.value })}
      />

      <input
        type="datetime-local"
        value={form.dateOpened ?? ""}
        // ค่าจาก datetime-local เป็นเวลาท้องถิ่น ไม่มี Z ส่งได้ตรง ๆ
        onChange={(e) => setForm({ ...form, dateOpened: e.target.value || null })}
      />

      {/* drop-down ประเภทบริการ (GetServiceType กรอง categoryType === "SERVICE"), สถานะ,
          ช่องทาง (GetChannel), ผู้ติดต่อ (GetAccounts) ... */}

      {message && <p role="status">{message}</p>}
      <button type="submit" disabled={!canSubmit}>
        {saving ? "กำลังบันทึก..." : "บันทึก"}
      </button>
    </form>
  );
}
```

ใส่ `maxLength` ให้ช่องกรอกตามตาราง 2.1 ผู้ใช้จะได้ไม่พิมพ์เกินแล้วโดน 400

---

## 5. ข้อควรรู้ (สถานะ ณ 16 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ส่ง `areaId` ไม่ได้** | API ตรวจ `areaId` กับตารางพื้นที่ ซึ่งตอนนี้ **ว่าง** ส่งค่าอะไรก็ได้ 400 ให้ซ่อนช่องนี้หรือไม่ส่งไปก่อน |
| **API ไม่มี field บังคับ** | frontend ต้องบังคับเอง (ดู 2.3) |
| **ยังไม่มี endpoint รายการ** หน่วยงาน / เจ้าหน้าที่ | ยังทำ drop-down `organizationId`, `ownerId` จาก API ไม่ได้ ถ้าต้องใช้ แจ้ง backend (สถานะใช้ `GetStatus` ได้แล้ว) |
| **`GetServiceType` คืนทุกประเภทปนกัน** | กรอง `categoryType === "SERVICE"` เอง |
| **`GetAccountByPhoneNo` ไม่คืน `id`** | หา `accountId` จากเบอร์โทรด้วย endpoint นั้นยังไม่ได้ ใช้ `GetAccounts` หรือ `AddNewAccount` แทน |
| **ยังไม่ผูกกับการรับสาย** | ถ้าเปิดเรื่องต่อจากหน้ารับสาย (`AddNewContact`) ตอนนี้ยังไม่มี field เชื่อมสองอย่างนี้ |
| **กดซ้ำ = เรื่องซ้ำ** | ปิดปุ่มระหว่างรอ response |

ถ้า backend แก้ข้อใดข้างบน จะแจ้งและอัปเดตเอกสารนี้

---

## 6. ทดสอบด้วย REST Client / curl

> ⚠️ ทุกครั้งที่ได้ 201 คือ **เปิดเรื่องจริงใน DB** และใช้เลขที่เรื่องของวันนั้นไปหนึ่งเลข

```http
POST http://localhost:5018/Services/AddNewService
Authorization: Bearer <token>
Content-Type: application/json

{
  "categoryId": "<id ประเภทบริการ>",
  "statusId": "<id สถานะ>",
  "accountId": "<id ผู้ติดต่อ>",
  "channelIncomingId": "332E6D76F258C8294B160C20642AB9F5",
  "callBack": "N",
  "summary": "ทดสอบเปิดเรื่อง",
  "detail": "รายละเอียด"
}
```

```bash
curl -X POST "http://localhost:5018/Services/AddNewService" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"summary":"ทดสอบเปิดเรื่อง","callBack":"N","channelIncomingId":"332E6D76F258C8294B160C20642AB9F5"}'
```

เปิดแล้วตรวจได้ด้วย `GET /Services/GetServices?ServiceCode=<code ที่ได้>`
หรือ `GET /Services/GetInProcessServices` (เรื่องที่ยังไม่ปิดของผู้ที่ login)
