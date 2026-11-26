# เพิ่มการดำเนินการของเรื่อง (`POST /Services/AddNewServiceOperations`) สำหรับ Frontend

ใช้ **บันทึกการดำเนินการ (งาน / activity) หนึ่งรายการ** ของเรื่องที่เปิดไว้แล้ว เช่น "โทรติดต่อผู้ร้อง",
"ส่งเรื่องให้หน่วยงาน" หนึ่งเรื่องมีการดำเนินการได้หลายรายการ backend สร้างลำดับ (`line`) และเลขที่ (`code`) ให้เอง
แล้วคืนรายการที่บันทึกกลับมาทั้งแถว

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`AddNewServiceOperations`),
> `Dtos/CreateServiceOperationRequest.cs`, `Dtos/ServiceOperationResponse.cs`
>
> เอกสารที่ใช้คู่กัน: [UpdateServiceOperations.md](UpdateServiceOperations.md) (แก้ไขการดำเนินการ),
> [GetServiceOperations.md](GetServiceOperations.md) (รายการการดำเนินการของเรื่อง),
> [GetStatusByStatusTypeActivity.md](GetStatusByStatusTypeActivity.md) (สถานะของการดำเนินการ),
> [GetServiceById.md](GetServiceById.md) (เรื่องที่การดำเนินการนี้อยู่)
>
> **เพิ่มเมื่อ 23 พ.ย. 2569** · ปรับ 23 พ.ย. 2569: `categoryId` ต้องเป็นชุด ACTIVITY, `dateClosed` server ตั้งเอง (ไม่รับจาก client)
> · ปรับ 25 พ.ย. 2569: `groupId` ใช้ค่าจาก `GetEmployeeGroup` และเลือก `ownerId` จาก `GetEmployeeByGroup` ได้แล้ว
> · **ปรับ 26 พ.ย. 2569: `contactId` เป็นช่องบังคับ** ไม่ส่ง / ส่งค่าว่าง / id ที่ไม่มีจริง = 400 (ดูข้อ 2.4)

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `POST` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Body | JSON (`Content-Type: application/json`) |
| Backend (dev) | `http://localhost:5018/Services/AddNewServiceOperations` |
| URL จาก Next.js | `/api/backend/Services/AddNewServiceOperations` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |
| สำเร็จ | `201` พร้อมการดำเนินการที่บันทึก |

---

## 2. Request body

### 2.1 Field

ชื่อ field ใน JSON เป็น camelCase **บังคับ 4 ตัว** คือ `serviceId`, `statusId`, `summary`, `contactId`

| Field | บังคับ | ยาวสุด | ความหมาย | เอาค่ามาจาก / ค่าเริ่มต้น |
|---|---|---|---|---|
| `serviceId` | ✔ | 50 | เรื่องที่การดำเนินการนี้อยู่ | `id` ของ `GetServiceById` / `serviceId` ของ `SearchService` (**ไม่ใช่ `code`**) ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| `statusId` | ✔ | 50 | สถานะของการดำเนินการ (เปิด / ปิด / ยกเลิก) | `id` จาก `GET /ListOfValues/GetStatusByStatusTypeActivity` **ห้ามใช้สถานะของเรื่อง** (ชุด SERVICE) จะได้ 400 |
| `summary` | ✔ | ไม่จำกัด | หัวข้อการดำเนินการ | ผู้ใช้กรอก ส่ง `""` หรือช่องว่างล้วน = 400 |
| `detail` | | ไม่จำกัด | รายละเอียด | ผู้ใช้กรอก |
| `categoryId` | **ตอนนี้ต้องส่ง** | 50 | ประเภทการดำเนินการ (ชุด `ACTIVITY`) | ดู 2.2 |
| `ownerId` | | 50 | เจ้าหน้าที่ผู้รับผิดชอบ (ผู้รับ) | ไม่ส่ง = **ผู้ที่ login อยู่** เลือกกลุ่มแล้วใช้ `id` จาก `GET /ListOfValues/GetEmployeeByGroup` ([GetEmployeeByGroup.md](GetEmployeeByGroup.md)) ถ้าไม่มีช่องกลุ่มใช้ `GET /ListOfValues/GetEmployee` ([GetEmployee.md](GetEmployee.md)) |
| `contactId` | ✔ | 50 | การติดต่อที่เกี่ยวข้อง (`TblContact`) รายงานใช้หาเบอร์ผู้ติดต่อ | ดูข้อ 2.4 ส่ง `""` หรือช่องว่างล้วน = 400 |
| `groupId` | | 50 | กลุ่ม | `id` จาก `GET /ListOfValues/GetEmployeeGroup` ([GetEmployeeGroup.md](GetEmployeeGroup.md)) แล้วเลือกผู้รับ (`ownerId`) จาก `GET /ListOfValues/GetEmployeeByGroup` ([GetEmployeeByGroup.md](GetEmployeeByGroup.md)) API ยังไม่ตรวจค่านี้ |
| `dateOpened` | | – | วันเวลาที่เริ่มดำเนินการ | ไม่ส่ง = **เวลาที่บันทึก** |

**backend ใส่ให้เอง** (ส่งมาก็ไม่มีผล): `id`, `line`, `code`, `isEnable` (= `"T"`), `dateClosed` (ดู 2.2), `isReceived` (ดู 2.2),
`created` / `updated` (= เวลาที่บันทึก), `createdBy` / `updatedBy` (= ผู้ที่ login อยู่)

### 2.2 กติกาที่ควรรู้

- **id ทุกตัวที่ส่งต้องมีอยู่จริง** ไม่งั้นได้ 400 ที่ field นั้น ส่ง id ตามที่ได้จาก API (ตัวพิมพ์ใหญ่) อย่าแปลงเป็นตัวเล็ก
  เพราะฐานข้อมูลเทียบตัวพิมพ์ (ยกเว้น `serviceId` ที่ backend แปลงให้)
  **ยกเว้น `groupId`** ที่ backend ยังไม่ตรวจ บันทึกตามที่ส่งมา จึงต้องส่ง `id` จาก `GetEmployeeGroup` เท่านั้น
  (เป็น string เช่น `"5"` อย่าแปลงเป็นตัวเลข)
- **`statusId`**: ตอนนี้มี 3 ตัว เปิด (`OPEN`) / ปิด (`CLOSE`) / ยกเลิก (`CANCEL`) ให้เลือกจาก `isType` อย่า hard-code id
  ค่าเริ่มต้นของ drop-down ใช้แถวที่ `isDefault === "T"`
- **`dateClosed`: server ตั้งเอง ไม่รับจาก client** (ส่งมาจะถูกข้าม) ถ้า `statusId` มี `isType` เป็น `CLOSE` / `CANCEL`
  จะได้ `dateClosed` = เวลาของ server ตอนบันทึก สถานะเปิดได้ `null`
- **`isReceived` (ผู้รับผิดชอบรับงานแล้วหรือยัง): server ตั้งเอง ตามระบบเดิม** มอบให้ตัวเอง (`ownerId` = ผู้ที่ login
  หรือไม่ส่ง `ownerId`) = `"T"` รับแล้ว, มอบให้คนอื่น = `"F"` รอการตอบรับ ผู้รับจะรับงานเมื่อเปิดดูเรื่อง
  ([ReceiveService.md](ReceiveService.md))
- **`categoryId`: ต้องเป็นประเภทการดำเนินการ (`TblCategory` ชุด `ACTIVITY`) ที่เปิดใช้งาน** ไม่งั้นได้ 400
  ดึงรายการจาก `GET /ListOfValues/GetServiceType` แล้วกรอง `categoryType === "ACTIVITY"` (ตอนนี้มี 12 รายการ เช่น
  Call - Inbound, Call - Outbound, Email-Outbound, Follow Up, Q&A, Other ทั้งหมดเป็นชั้นเดียว)
  - **ไม่ส่ง** = ใช้ประเภท ACTIVITY ที่ตั้งเป็นค่าเริ่มต้น (`IsDefault = 'T'`) แต่**ตอนนี้ยังไม่มีแถวไหนเป็นค่าเริ่มต้น**
    ไม่ส่งจึงได้ 400 `errors.CategoryId` ("No default ACTIVITY category; send a CategoryId.") ให้ถือว่าเป็นช่องบังคับไปก่อน
  - **อย่าใช้ประเภทของเรื่อง** (ชุด `SERVICE`) หรือของการรับสาย (ชุด `CONTACT`) จะได้ 400
- **วันที่** เป็นเวลาไทย `yyyy-MM-ddTHH:mm:ss` (ค่าจาก `<input type="datetime-local">` ใช้ได้เลย)
  **ห้ามมี `Z` หรือ `+07:00`** และอย่าใช้ `date.toISOString()` เวลาจะคลาด 7 ชั่วโมง API ไม่ตรวจลำดับวันที่ ให้ตรวจที่หน้าเว็บ
- **กดซ้ำ = บันทึกซ้ำ** API ไม่กันรายการซ้ำ ปิดปุ่มระหว่างรอ response

### 2.3 ตัวอย่าง

```json
{
  "serviceId": "427D66AA13734763AC1D99775554659B",
  "statusId": "0A5753389CDCD23F173268D8D46AC004",
  "categoryId": "1987E8FF8070CEFA483F881ED0CD2170",
  "contactId": "EF5355C696E24159BEADE68EFEEC442C",
  "summary": "โทรติดต่อผู้ร้องเพื่อสอบถามข้อมูลเพิ่มเติม",
  "detail": "ผู้ร้องจะส่งเอกสารเพิ่มเติมภายในวันพรุ่งนี้"
}
```

บันทึกพร้อมปิดงานทันที (ไม่ต้องส่ง `dateClosed` server ใส่ให้):

```json
{
  "serviceId": "427D66AA13734763AC1D99775554659B",
  "statusId": "CB0A973609E8D684F2944D92ED4126A1",
  "categoryId": "1987E8FF8070CEFA483F881ED0CD2179",
  "contactId": "EF5355C696E24159BEADE68EFEEC442C",
  "summary": "แจ้งผลการดำเนินการให้ผู้ร้องทราบ"
}
```

### 2.4 เอา `contactId` มาจากไหน (บังคับตั้งแต่ 26 พ.ย. 2569)

`contactId` คือ `id` ของการติดต่อใน `TblContact` ต้องมีอยู่จริง ส่ง id ตามที่ได้จาก API ห้ามแปลงตัวพิมพ์
ระบบเดิมใส่ให้เองจาก session (contact ของสายที่กำลังรับ) แต่ API ไม่มี session frontend จึงต้องเก็บไว้แล้วส่งมาเอง

| สถานการณ์ | ค่าที่ส่ง |
|---|---|
| กำลังรับสาย (มาจากหน้ารับสาย) | `id` ใน response ของ `POST /Contact/AddNewContact` ที่บันทึกตอนกดปุ่มหลักของกล่องยืนยัน ส่งต่อมากับหน้า (เช่น `?contactId=`) จนจบสาย ลำดับอยู่ใน [AddNewContact.md](AddNewContact.md) ข้อ 1 |
| เปิดเรื่องเดิมมาทำงานต่อ (ไม่ได้อยู่ในสาย) | `contactId` ของการดำเนินการในเรื่องนั้นจาก `GetServiceOperations` ([GetServiceOperations.md](GetServiceOperations.md)) ระบบเดิมใช้ของรายการแรก (`line` 1) ถ้าไม่มีให้ใช้รายการล่าสุดที่มีค่า |
| เรื่องเก่าที่ทุกรายการ `contactId` เป็น `null` (เปิดก่อน 26 พ.ย. 2569) หรือเป็นการติดต่อครั้งใหม่ เช่น โทรออก | บันทึกการติดต่อด้วย `AddNewContact` ก่อน แล้วใช้ `id` ที่ได้ |

ไม่ส่ง / `null` / `""` → 400 `errors.ContactId` = `"The ContactId field is required."`,
id ที่ไม่มีใน `TblContact` → 400 `errors.ContactId` = `"No row with Id '...'."`

---

## 3. Response

### 3.1 Field (201)

ตอบเป็น **object เดียว** (ไม่มี envelope) มีแต่ id ยังไม่มีชื่อสถานะ / ชื่อเจ้าหน้าที่

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string (GUID มีขีด เช่น `3f2b…-…`) | id ของการดำเนินการ **เก็บไว้ใช้กับ `UpdateServiceOperations`** |
| `serviceId` | string | เรื่องที่อยู่ |
| `line` | number | ลำดับการดำเนินการในเรื่องนี้ เริ่มที่ 1 |
| `code` | string | เลขที่การดำเนินการ `yyMMdd` (ปี พ.ศ.) + เลขรัน 5 หลักของวัน เช่น `69112300001` (นับแยกจากเลขที่เรื่อง) |
| `statusId`, `categoryId`, `ownerId`, `contactId`, `groupId` | string \| null | ค่าที่บันทึก (รวมค่าเริ่มต้นที่ backend ใส่ให้) `contactId` ของรายการที่เพิ่มใหม่มีค่าเสมอ แต่ type ยังเป็น `string \| null` เพราะรายการเก่าเป็น `null` |
| `summary`, `detail` | string / string \| null | |
| `isReceived` | string | `"T"` รับงานแล้ว / `"F"` รอการตอบรับ (ข้อ 2.2) |
| `isEnable` | string | `"T"` |
| `dateOpened` / `dateClosed` | string / string \| null | เวลาไทย ไม่มี offset |
| `created`, `createdBy`, `updated`, `updatedBy` | | ผู้บันทึกและเวลา |

```json
{
  "id": "6a1d0c4e-2f3b-f111-8e93-96f1f7a12f5e",
  "serviceId": "427D66AA13734763AC1D99775554659B",
  "line": 1,
  "code": "69112300001",
  "contactId": "EF5355C696E24159BEADE68EFEEC442C",
  "categoryId": "1987E8FF8070CEFA483F881ED0CD2170",
  "statusId": "0A5753389CDCD23F173268D8D46AC004",
  "dateOpened": "2026-11-23T15:10:00",
  "dateClosed": null,
  "summary": "โทรติดต่อผู้ร้องเพื่อสอบถามข้อมูลเพิ่มเติม",
  "detail": "ผู้ร้องจะส่งเอกสารเพิ่มเติมภายในวันพรุ่งนี้",
  "ownerId": "505981F1B7534D56BF4BF2F159322184",
  "groupId": null,
  "isReceived": "T",
  "isEnable": "T",
  "created": "2026-11-23T15:10:00",
  "createdBy": "505981F1B7534D56BF4BF2F159322184",
  "updated": "2026-11-23T15:10:00",
  "updatedBy": "505981F1B7534D56BF4BF2F159322184"
}
```

ตัวอย่างนี้ประกอบขึ้นเพื่ออธิบายรูปแบบ ค่าจริงจะต่างไป และลำดับ field อาจไม่ตรง

### 3.2 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 201 | บันทึกสำเร็จ | `ServiceOperation` |
| 400 | ไม่ส่ง field บังคับ (รวม `contactId`), id lookup ผิด, ยาวเกิน, รูปแบบวันที่ผิด, JSON พัง | ProblemDetails (`errors.<Field>`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีเรื่องที่ `serviceId` นี้ หรือเรื่องถูกปิดใช้งาน | `{ "statusCode": 404, "message": "ไม่พบข้อมูลเรื่อง" }` |
| 409 | เลขรันของวันเต็ม (99,999 รายการต่อวัน) | `MessageResponse` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

ตัวอย่าง 400:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "StatusId": ["No ACTIVITY TblStatus row with Id 'XXX'."],
    "CategoryId": ["No enabled ACTIVITY TblCategory row with Id 'XXX'."],
    "ContactId": ["No row with Id 'XXX'."],
    "Summary": ["The Summary field is required."]
  }
}
```

key ใน `errors` เป็น PascalCase (`ServiceId`, `StatusId`, `Summary`, `CategoryId`, `OwnerId`, `ContactId`)
map กลับเป็น camelCase ก่อนแสดงใต้ช่อง

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/serviceOperations.ts
export interface ServiceOperation {
  id: string;
  serviceId: string;
  line: number;
  code: string;
  contactId: string | null;
  categoryId: string;
  statusId: string;
  dateOpened: string;
  dateClosed: string | null;
  summary: string;
  detail: string | null;
  ownerId: string | null;
  groupId: string | null;
  isReceived: "T" | "F" | string;
  isEnable: string;
  created: string; createdBy: string;
  updated: string; updatedBy: string;
}

export interface CreateServiceOperationBody {
  serviceId: string;
  statusId: string;
  summary: string;
  detail?: string | null;
  categoryId: string; // ชุด ACTIVITY (ตอนนี้ยังไม่มีค่าเริ่มต้น จึงต้องส่ง)
  ownerId?: string | null;    // id จาก GetEmployeeByGroup (ไม่ส่ง = ผู้ที่ login อยู่)
  contactId: string;          // บังคับ: id จาก AddNewContact หรือ contactId ของงานเดิม (ข้อ 2.4)
  groupId?: string | null;    // id จาก GetEmployeeGroup (string เช่น "5")
  dateOpened?: string | null; // "yyyy-MM-ddTHH:mm:ss" เวลาไทย
  // ไม่มี dateClosed: server ตั้งเอง
}

export class ValidationError extends Error {
  constructor(public errors: Record<string, string[]>) { super("ข้อมูลไม่ถูกต้อง"); }
}

export async function addServiceOperation(
  body: CreateServiceOperationBody,
  opts: { token: string; signal?: AbortSignal },
): Promise<ServiceOperation> {
  const res = await fetch("/api/backend/Services/AddNewServiceOperations", {
    method: "POST",
    headers: { Authorization: `Bearer ${opts.token}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal: opts.signal,
  });
  if (res.status === 201) return res.json();
  if (res.status === 400) throw new ValidationError((await res.json()).errors ?? {});
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 404) throw new Error("ไม่พบข้อมูลเรื่อง");
  throw new Error("บันทึกการดำเนินการไม่สำเร็จ");
}
```

```ts
// ฟอร์มเพิ่มการดำเนินการ
const statuses = await fetchActivityStatuses({ token }); // GET /ListOfValues/GetStatusByStatusTypeActivity
const defaultStatus = statuses.find((s) => s.isDefault === "T");
const categories = (await fetchServiceTypes({ token }))   // GET /ListOfValues/GetServiceType
  .filter((c) => c.categoryType === "ACTIVITY");

// contactId (บังคับ): สายที่กำลังรับ ไม่งั้นใช้ของงานเดิมในเรื่อง (ข้อ 2.4)
const operations = await fetchServiceOperations(detail.id, { token }); // GET /Services/GetServiceOperations
const contactId =
  currentCall?.contactId                                   // id จาก POST /Contact/AddNewContact
  ?? operations.find((o) => o.line === 1)?.contactId
  ?? [...operations].reverse().find((o) => o.contactId)?.contactId;
if (!contactId) {
  // เรื่องเก่าที่ไม่มีการติดต่อ: ให้ผู้ใช้บันทึกการติดต่อ (AddNewContact) ก่อน
  throw new Error("กรุณาบันทึกข้อมูลการติดต่อก่อนเพิ่มการดำเนินการ");
}

await addServiceOperation({
  serviceId: detail.id,
  statusId: form.statusId,
  categoryId: form.categoryId,
  contactId,
  summary: form.summary.trim(),
  detail: form.detail || null,
  groupId: form.groupId || null, // drop-down กลุ่ม (GetEmployeeGroup)
  ownerId: form.ownerId || null, // drop-down ผู้รับ (GetEmployeeByGroup?groupId=)
}, { token });
// ถ้าเลือกสถานะปิด/ยกเลิก response จะมี dateClosed เป็นเวลาของ server
```

---

## 5. ทดสอบด้วย REST Client / curl

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (หัวข้อ Add an operation)

```bash
curl -X POST "http://localhost:5018/Services/AddNewServiceOperations" \
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
  -d '{"serviceId":"427D66AA13734763AC1D99775554659B","statusId":"0A5753389CDCD23F173268D8D46AC004","categoryId":"1987E8FF8070CEFA483F881ED0CD2170","contactId":"EF5355C696E24159BEADE68EFEEC442C","summary":"ทดสอบ"}'
```

---

## 6. ข้อควรรู้ (ณ 23 พ.ย. 2569)

| เรื่อง | ผลกับ frontend |
|---|---|
| **รายการการดำเนินการของเรื่อง** | ใช้ `GET /Services/GetServiceOperations?serviceId=` ([GetServiceOperations.md](GetServiceOperations.md)) หลังบันทึกเรียกใหม่เพื่อรีเฟรชรายการ |
| **response ไม่มีชื่อ** | มีแต่ id ถ้าต้องการชื่อให้ใช้ `GetServiceOperations` |
| **`dateClosed`** | server ตั้งเองตามสถานะ ไม่ต้องส่ง (ข้อ 2.2) |
| **`categoryId`** | ชุด ACTIVITY เท่านั้น และตอนนี้ต้องส่ง เพราะยังไม่มีค่าเริ่มต้น (ข้อ 2.2) |
| **`contactId`** (26 พ.ย. 2569) | บังคับ ต้องเป็น id ที่มีอยู่ใน `TblContact` (ข้อ 2.4) เรื่องที่เปิดก่อนวันนี้ทุกรายการเป็น `null` ต้องบันทึกการติดต่อก่อน |
| **`groupId`** | ไม่บังคับ ใช้ `id` จาก `GetEmployeeGroup` แล้วเลือกผู้รับจาก `GetEmployeeByGroup?groupId=` API ยังไม่ตรวจว่ามีกลุ่มนี้จริง หรือผู้รับอยู่ในกลุ่มนี้ |
| **ไฟล์แนบ** | ยังไม่รองรับ (ตารางมีคอลัมน์ไฟล์แนบ แต่ API ยังไม่รับ) |
| **ลบการดำเนินการ** | `DELETE /Services/DeleteServiceOperations` ([DeleteServiceOperations.md](DeleteServiceOperations.md)) |
| **สถานะของเรื่องไม่เปลี่ยนตาม** | ปิดการดำเนินการไม่ได้ปิดเรื่อง ถ้าต้องปิดเรื่องด้วยให้เรียก `UpdateService` แยก |
