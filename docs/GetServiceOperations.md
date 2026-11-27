# รายการการดำเนินการของเรื่อง (`GET /Services/GetServiceOperations`) สำหรับ Frontend

ใช้ดึง **การดำเนินการ (งาน / activity) ทั้งหมดของเรื่องหนึ่งเรื่อง** ไปแสดงในการ์ด "ประวัติการดำเนินการ" ของหน้ารายละเอียดเรื่อง
พร้อมชื่อสถานะ ประเภท ผู้รับผิดชอบ และผู้บันทึก ไม่ต้องแปลง id เอง และใช้ `id` ของแต่ละรายการกับปุ่มแก้ไขได้เลย

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`GetServiceOperations`),
> `Dtos/ServiceOperationListItemResponse.cs` (สืบทอดจาก `Dtos/ServiceOperationResponse.cs`)
>
> เอกสารที่ใช้คู่กัน: [AddNewServiceOperations.md](AddNewServiceOperations.md) (เพิ่ม),
> [UpdateServiceOperations.md](UpdateServiceOperations.md) (แก้ไข), [GetServiceById.md](GetServiceById.md) (ตัวเรื่อง)
>
> **เพิ่มเมื่อ 23 พ.ย. 2569**

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `serviceId` (บังคับ) = `id` ของเรื่อง (ตัวเดียวกับ `GetServiceById`) ตัวพิมพ์เล็กหรือใหญ่ก็ได้ **ไม่ใช่ `code`** |
| Backend (dev) | `http://localhost:5018/Services/GetServiceOperations?serviceId=…` |
| URL จาก Next.js | `/api/backend/Services/GetServiceOperations?serviceId=…` (ผ่าน `rewrites()` ใน `next.config.ts`) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** (ไม่มี envelope ไม่มี paging) **เรียงตาม `line` จากรายการแรก**
มีเฉพาะการดำเนินการที่เปิดใช้งาน ไม่รวมไฟล์แนบ
เรื่องที่เปิดผ่าน `AddNewService` จะมีรายการแรก (`line` 1, "Call Inbound" / "เริ่มให้บริการ", สถานะปิด) เสมอ
([AddNewService.md](AddNewService.md) ข้อ 3.2) ส่วนเรื่องเก่าอาจได้ `[]`

แต่ละรายการมีทุก field แบบเดียวกับ response ของ `AddNewServiceOperations`
([AddNewServiceOperations.md](AddNewServiceOperations.md) ข้อ 3.1) **บวกชื่อที่แปลงแล้ว**:

| Field เพิ่ม | ความหมาย | แปลงจาก |
|---|---|---|
| `statusNameTh` | ชื่อสถานะ เช่น `เปิด` / `ปิด` / `ยกเลิก` | `statusId` |
| `statusIsType` | `OPEN` / `CLOSE` / `CANCEL` ใช้ตัดสินว่างานเสร็จหรือยัง (อย่าดูจากชื่อ) | `statusId` |
| `categoryNameTh` | ประเภทการดำเนินการ เช่น `Call - Outbound` | `categoryId` |
| `ownerName` | ผู้รับผิดชอบ (คำนำหน้า ชื่อ นามสกุล) | `ownerId` |
| `contactDetail` | เบอร์ที่รับสาย | `contactId` |
| `createdByName` / `updatedByName` | ผู้บันทึก / ผู้แก้ไขล่าสุด | `createdBy` / `updatedBy` |
| `isReceivedText` | `รับงานแล้ว` / `รอการตอบรับ` ตามระบบเดิม เป็น `""` เมื่อผู้รับผิดชอบคือผู้สร้างงานเอง | `isReceived` |

ชื่อเป็น `null` ถ้า id ว่างหรือหาแถวไม่เจอ ให้แสดง "-"

| ส่วนในการ์ด | Field |
|---|---|
| ลำดับ / เลขที่ | `line` / `code` |
| หัวข้อ / รายละเอียด | `summary` / `detail` |
| ประเภท | `categoryNameTh` |
| สถานะ | `statusNameTh` (+ `statusIsType` สำหรับสี/ไอคอน) |
| ผู้รับผิดชอบ | `ownerName` + `isReceivedText` (ถ้าไม่ว่าง) |
| วันที่เริ่ม / เสร็จ | `dateOpened` / `dateClosed` (เวลาไทย ไม่มี offset; `dateClosed` เป็นเวลาที่ server บันทึกตอนปิด) |
| ผู้บันทึก | `createdByName` + `created` |
| ปุ่มแก้ไข | ใช้ `id` กับ `PUT /Services/UpdateServiceOperations?id=` |

### 2.2 ตัวอย่าง

`GET /Services/GetServiceOperations?serviceId=427D66AA13734763AC1D99775554659B`

```json
[
  {
    "id": "6a1d0c4e-2f3b-f111-8e93-96f1f7a12f5e",
    "serviceId": "427D66AA13734763AC1D99775554659B",
    "line": 1,
    "code": "69112400001",
    "contactId": null,
    "contactDetail": null,
    "categoryId": "1987E8FF8070CEFA483F881ED0CD2171",
    "categoryNameTh": "Call - Inbound",
    "statusId": "CB0A973609E8D684F2944D92ED4126A1",
    "statusNameTh": "ปิด",
    "statusIsType": "CLOSE",
    "dateOpened": "2026-11-23T23:59:56",
    "dateClosed": "2026-11-24T00:00:24",
    "summary": "Call Inbound",
    "detail": "เริ่มให้บริการ",
    "ownerId": "505981F1B7534D56BF4BF2F159322184",
    "ownerName": "นาย กันตณัฐิ์ ประกอบศุขราษฎร์",
    "groupId": null,
    "isReceived": "T",
    "isReceivedText": "",
    "isEnable": "T",
    "created": "2026-11-24T00:00:24",
    "createdBy": "505981F1B7534D56BF4BF2F159322184",
    "createdByName": "นาย กันตณัฐิ์ ประกอบศุขราษฎร์",
    "updated": "2026-11-24T00:00:24",
    "updatedBy": "505981F1B7534D56BF4BF2F159322184",
    "updatedByName": "นาย กันตณัฐิ์ ประกอบศุขราษฎร์"
  }
]
```

ตัวอย่างคือรายการแรกที่ `AddNewService` สร้างให้ (ค่าประกอบขึ้นเพื่ออธิบายรูปแบบ) ลำดับ field อาจไม่ตรง

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | พบเรื่อง (รวมกรณีไม่มีการดำเนินการ `[]`) | `ServiceOperationListItem[]` |
| 400 | ไม่ส่ง `serviceId` หรือส่งค่าว่าง | ProblemDetails (`errors.serviceId`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีเรื่องที่ `serviceId` นี้ หรือเรื่องถูกปิดใช้งาน | `{ "statusCode": 404, "message": "ไม่พบข้อมูลเรื่อง" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/serviceOperations.ts
import type { ServiceOperation } from "./serviceOperations"; // จาก AddNewServiceOperations.md

export interface ServiceOperationListItem extends ServiceOperation {
  statusNameTh: string | null;
  statusIsType: "OPEN" | "CLOSE" | "CANCEL" | string | null;
  categoryNameTh: string | null;
  ownerName: string | null;
  contactDetail: string | null;
  createdByName: string | null;
  updatedByName: string | null;
  isReceivedText: "รับงานแล้ว" | "รอการตอบรับ" | "" | string;
}

export async function fetchServiceOperations(
  serviceId: string,
  opts: { token: string; signal?: AbortSignal },
): Promise<ServiceOperationListItem[] | null> {
  const res = await fetch(
    `/api/backend/Services/GetServiceOperations?serviceId=${encodeURIComponent(serviceId)}`,
    { headers: { Authorization: `Bearer ${opts.token}` }, signal: opts.signal },
  );
  if (res.ok) return res.json();
  if (res.status === 404) return null; // แสดง "ไม่พบข้อมูลเรื่อง"
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("โหลดการดำเนินการไม่สำเร็จ");
}
```

```tsx
{operations.length === 0 ? (
  <p>ยังไม่มีการดำเนินการ</p>
) : (
  <ol>
    {operations.map((op) => (
      <li key={op.id}>
        #{op.line} {op.summary} · {op.categoryNameTh ?? "-"} · {op.statusNameTh ?? "-"}
        · {op.ownerName ?? "-"} · {op.dateOpened}
        {(op.statusIsType === "CLOSE" || op.statusIsType === "CANCEL") && ` – ${op.dateClosed ?? ""}`}
        <button onClick={() => openEdit(op)}>แก้ไข</button>
      </li>
    ))}
  </ol>
)}
```

**ตอนเปิดหน้ารายละเอียดเรื่อง ให้เรียก `POST /Services/ReceiveService?id=<serviceId>` ก่อน** ([ReceiveService.md](ReceiveService.md))
แล้วค่อยเรียก endpoint นี้ งานที่มอบให้ผู้ใช้คนนี้จะได้ขึ้น `รับงานแล้ว` ตามระบบเดิม

หลังเพิ่มหรือแก้ไขการดำเนินการ ให้เรียก `fetchServiceOperations` ใหม่ (หรือ revalidate) เพื่อได้ชื่อที่แปลงแล้วและลำดับล่าสุด

---

## 4. ทดสอบด้วย REST Client / curl

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (หัวข้อ Operations of a service)

```bash
curl "http://localhost:5018/Services/GetServiceOperations?serviceId=427D66AA13734763AC1D99775554659B" \
  -H "Authorization: Bearer <token>"
```

---

## 5. ข้อควรรู้ (ณ 23 พ.ย. 2569)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ไม่มี paging** | คืนทุกรายการของเรื่องในครั้งเดียว (เรื่องหนึ่งมีไม่กี่รายการ) |
| **การดำเนินการที่ถูกปิดใช้งาน** | ไม่แสดง งานที่ลบด้วย `DeleteServiceOperations` ([DeleteServiceOperations.md](DeleteServiceOperations.md)) จะหายจากรายการนี้ |
| **ไฟล์แนบ** | ไม่รวม ยังไม่รองรับ |
| **`groupId`** | ยังไม่ใช้ ไม่มีชื่อแปลงให้ |
