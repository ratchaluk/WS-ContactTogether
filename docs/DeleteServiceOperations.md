# ลบการดำเนินการของเรื่อง (`DELETE /Services/DeleteServiceOperations`) สำหรับ Frontend

ใช้กับ **ปุ่มลบ (ไอคอนถังขยะ) ในรายการประวัติการปฏิบัติงาน** ของหน้ารายละเอียดเรื่อง

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`DeleteServiceOperations`), `Dtos/ServiceOperationResponse.cs`
>
> เอกสารที่ใช้คู่กัน: [GetServiceOperations.md](GetServiceOperations.md) (รายการ + `id`),
> [UpdateServiceOperations.md](UpdateServiceOperations.md), [AddNewServiceOperations.md](AddNewServiceOperations.md)
> (type `ServiceOperation`)
>
> **เพิ่มเมื่อ 26 พ.ย. 2569**

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `DELETE` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `id` (บังคับ) = `id` ของการดำเนินการ (GUID) จาก `GetServiceOperations` มีหรือไม่มีขีด ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| Body | ไม่มี |
| Backend (dev) | `http://localhost:5018/Services/DeleteServiceOperations?id=…` |
| URL จาก Next.js | `/api/backend/Services/DeleteServiceOperations?id=…` (ผ่าน `rewrites()` ใน `next.config.ts`) |
| สำเร็จ | `200` พร้อมการดำเนินการที่ถูกลบ (`isEnable: "F"`) |

**ใช้ `id` (GUID) เท่านั้น** ห้ามใช้ `code` หรือ `line`

---

## 2. ลบแล้วเกิดอะไรขึ้น (ตามระบบเดิม)

- **ไม่ลบแถวจริง** backend ตั้ง `isEnable = "F"` พร้อม `updated` (= เวลาที่ลบ) และ `updatedBy` (= ผู้ที่ login อยู่)
- งานที่ลบ**หายจากทุกรายการ**: `GetServiceOperations`, รายการรับงาน / งานดำเนินการของหน้าแรก (`/Home/*`),
  `GetInProcessServices` และแก้ไขด้วย `UpdateServiceOperations` ไม่ได้อีก (404)
- **`line` ของงานอื่นไม่เปลี่ยน** เช่น ลบงานที่ 2 จาก 1, 2, 3 จะเหลือ 1, 3 งานที่เพิ่มใหม่จะได้ 4
- **ลบได้ทุกงาน รวมงานแรก (`line` 1)** ไม่ว่าสถานะหรือผู้รับเป็นใคร
- **สิทธิ์:** ระบบเดิมแสดงปุ่มตามสิทธิ์ลบของ role (`TblRolePage` หน้า Activity) แต่ตารางสิทธิ์ยังไม่มีข้อมูล
  ตอนนี้จึง**ลบได้ทุกคนที่ login**
- **ยังกู้คืนผ่าน API ไม่ได้** ลบซ้ำได้ 404

ข้อความของระบบเดิม ใช้ได้เลย:

| จังหวะ | ข้อความ |
|---|---|
| ก่อนลบ (ยืนยัน) | `คุณต้องการลบประวัติการปฏิบัติงาน ใช่หรือไม่` |
| ลบสำเร็จ | `ลบการปฏิบัติงานเรียบร้อยแล้ว` |

---

## 3. Response

### 3.1 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | ลบสำเร็จ | `ServiceOperation` (ดู [AddNewServiceOperations.md](AddNewServiceOperations.md) ข้อ 3.1) โดย `isEnable` = `"F"` |
| 400 | ไม่ส่ง `id` หรือไม่ใช่ GUID | ProblemDetails (`errors.id`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีการดำเนินการที่ `id` นี้ หรือถูกลบไปแล้ว | `{ "statusCode": 404, "message": "ไม่พบข้อมูลการดำเนินการ" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

### 3.2 ตัวอย่าง

```http
DELETE /Services/DeleteServiceOperations?id=6a1d0c4e-2f3b-f111-8e93-96f1f7a12f5e
Authorization: Bearer <token>
```

```json
{
  "id": "6a1d0c4e-2f3b-f111-8e93-96f1f7a12f5e",
  "serviceId": "427D66AA13734763AC1D99775554659B",
  "line": 2,
  "code": "69112600003",
  "isEnable": "F",
  "updated": "2026-11-26T10:15:00",
  "updatedBy": "04EFF8A54A56F4D240E48E0520C223DB",
  "...": "field อื่นเหมือน ServiceOperation"
}
```

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/serviceOperations.ts
import type { ServiceOperation } from "./serviceOperations"; // จาก AddNewServiceOperations.md

export async function deleteServiceOperation(
  id: string,
  opts: { token: string; signal?: AbortSignal },
): Promise<ServiceOperation> {
  const res = await fetch(`/api/backend/Services/DeleteServiceOperations?id=${encodeURIComponent(id)}`, {
    method: "DELETE",
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 404) throw new Error("ไม่พบข้อมูลการดำเนินการ");
  throw new Error("ลบการปฏิบัติงานไม่สำเร็จ");
}
```

**ปุ่มถังขยะ:** ยืนยันก่อน ลบ แล้วโหลดรายการใหม่

```ts
async function onDelete(op: ServiceOperation) {
  if (!confirm("คุณต้องการลบประวัติการปฏิบัติงาน ใช่หรือไม่")) return;
  await deleteServiceOperation(op.id, { token });
  toast.success("ลบการปฏิบัติงานเรียบร้อยแล้ว");
  await reloadOperations(); // GET /Services/GetServiceOperations?serviceId=
}
```

ถ้าได้ 404 แปลว่ามีคนลบไปก่อนแล้ว ให้โหลดรายการใหม่เหมือนกัน

---

## 5. ทดสอบด้วย REST Client / curl

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (หัวข้อ Delete an operation) แทน `id=00000000-…` ด้วย `id` จาก `GetServiceOperations`

```bash
curl -X DELETE "http://localhost:5018/Services/DeleteServiceOperations?id=<operation id>" \
  -H "Authorization: Bearer <token>"
```
