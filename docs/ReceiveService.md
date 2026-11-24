# รับงาน (`POST /Services/ReceiveService`) สำหรับ Frontend

ใช้ **บันทึกว่าผู้ใช้ "รับงาน" ที่ถูกมอบหมายในเรื่องนี้แล้ว** ระบบเดิมทำให้อัตโนมัติตอนผู้รับเปิดดูเรื่อง (ไม่มีปุ่มกดรับ)
ระบบใหม่จึงให้ frontend **เรียก endpoint นี้ทุกครั้งที่เปิดหน้ารายละเอียดเรื่อง**

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`ReceiveService`), `Dtos/ReceiveServiceResponse.cs`
>
> เอกสารที่ใช้คู่กัน: [GetServiceOperations.md](GetServiceOperations.md) (`isReceivedText`),
> [AddNewServiceOperations.md](AddNewServiceOperations.md), [UpdateServiceOperations.md](UpdateServiceOperations.md)
>
> **เพิ่มเมื่อ 24 พ.ย. 2569**

---

## 1. `isReceived` คืออะไร

แฟลกของแต่ละการดำเนินการ (งาน) ว่า **ผู้รับผิดชอบ (`ownerId`) รับงานนั้นแล้วหรือยัง** server เป็นคนตั้งเองทั้งหมด
frontend ส่งค่านี้มาไม่ได้ (ตามระบบเดิม)

| ค่า | ความหมาย | ข้อความ (`isReceivedText`) |
|---|---|---|
| `"F"` | ยังไม่รับ | `รอการตอบรับ` |
| `"T"` | รับแล้ว | `รับงานแล้ว` |

`isReceivedText` เป็น `""` เมื่อผู้รับผิดชอบคือผู้สร้างงานเอง (ไม่ต้องแสดง)

**ค่าเปลี่ยนเมื่อไหร่**

| เหตุการณ์ | `isReceived` |
|---|---|
| เปิดเรื่อง (`AddNewService`) → งานแรก "Call Inbound" | `"T"` (มอบให้ตัวเอง) |
| เพิ่มงาน (`AddNewServiceOperations`) มอบให้ตัวเอง / ไม่ระบุ `ownerId` | `"T"` |
| เพิ่มงาน มอบให้คนอื่น | `"F"` |
| แก้งาน (`UpdateServiceOperations`) เปลี่ยน `ownerId` | `"F"` (ผู้รับใหม่ยังไม่รับ) |
| ผู้รับเปิดดูเรื่อง → frontend เรียก **`ReceiveService`** | งานของผู้รับคนนั้นในเรื่องนี้ `"F"` → `"T"` |

---

## 2. ภาพรวม

| | |
|---|---|
| Method | `POST` (ไม่มี body) |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` ผู้รับ = ผู้ที่ login อยู่ |
| Query | `id` (บังคับ) = `id` ของเรื่อง (ตัวเดียวกับ `GetServiceById`) ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| Backend (dev) | `http://localhost:5018/Services/ReceiveService?id=…` |
| URL จาก Next.js | `/api/backend/Services/ReceiveService?id=…` (ผ่าน `rewrites()` ใน `next.config.ts`) |

**ทำอะไร:** เปลี่ยนการดำเนินการของเรื่องนี้ที่ **มอบให้ผู้ที่ login อยู่** และยัง `"F"` ให้เป็น `"T"`
- งานของคนอื่นไม่ถูกแตะ
- ไม่เปลี่ยน `updated` / `updatedBy` ของงาน (การรับงานไม่นับเป็นการแก้ไข)
- **เรียกซ้ำได้** ครั้งต่อไป `receivedCount` = 0 ไม่ error

---

## 3. Response

```json
{ "serviceId": "427D66AA13734763AC1D99775554659B", "receivedCount": 1 }
```

| Field | ความหมาย |
|---|---|
| `serviceId` | id ของเรื่อง (ตัวพิมพ์ใหญ่) |
| `receivedCount` | จำนวนงานที่เพิ่งรับในครั้งนี้ (0 = ไม่มีงานที่รอผู้ใช้คนนี้รับ) |

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวม `receivedCount: 0`) | `ReceiveServiceResponse` |
| 400 | ไม่ส่ง `id` หรือส่งค่าว่าง | ProblemDetails (`errors.id`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีเรื่องที่ `id` นี้ หรือเรื่องถูกปิดใช้งาน | `{ "statusCode": 404, "message": "ไม่พบข้อมูลเรื่อง" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
export async function receiveService(serviceId: string, opts: { token: string; signal?: AbortSignal }) {
  const res = await fetch(`/api/backend/Services/ReceiveService?id=${encodeURIComponent(serviceId)}`, {
    method: "POST",
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return (await res.json()) as { serviceId: string; receivedCount: number };
  if (res.status === 404) return null;
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("รับงานไม่สำเร็จ");
}

// หน้ารายละเอียดเรื่อง: รับงานก่อน แล้วค่อยโหลดรายการการดำเนินการ
await receiveService(serviceId, { token });
const [detail, operations] = await Promise.all([
  fetchServiceById(serviceId, { token }),
  fetchServiceOperations(serviceId, { token }),
]);
```

- เรียก **ครั้งเดียวต่อการเปิดหน้า** (ไม่ต้องเรียกตอน refresh รายการหลังแก้ไข)
- ถ้า `ReceiveService` ล้มเหลว (ยกเว้น 401) ยังแสดงหน้าได้ตามปกติ แค่สถานะการรับงานจะยังเป็น `รอการตอบรับ`
- ถ้าใช้ React Strict Mode (dev) effect จะรันสองครั้ง ไม่เป็นไร เพราะเรียกซ้ำได้

---

## 5. ทดสอบด้วย REST Client / curl

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (หัวข้อ Receive)

```bash
curl -X POST "http://localhost:5018/Services/ReceiveService?id=427D66AA13734763AC1D99775554659B" \
  -H "Authorization: Bearer <token>"
```

---

## 6. หมายเหตุ: Inbox / งานของฉัน (ยังไม่มี endpoint)

ระบบเดิมใช้ `Is_Received` กรองรายการในหน้าเหล่านี้ **backend ยังไม่ได้ทำ** จะทำเมื่อ frontend ถึงหน้านั้น
(แจ้ง backend ได้เลย) อ้างอิงจากโค้ดระบบเดิม:

| หน้าในระบบเดิม | เงื่อนไข (ระบบเดิม) |
|---|---|
| **Inbox** (`ServiceRepo.SelectMyInboxService`) | งานที่ `Owner_ID` = เรา และ `Is_Received = F` และ `Created_By` ≠ เรา (คนอื่นมอบมาและเรายังไม่รับ) |
| **หน้าแรก** (`ServiceRepo.GetInbox`, `Home/Default.aspx`) | งานที่สถานะเป็น CLOSE และ `Is_Received = F` และ `Owner_ID` = เรา แล้วแสดงเรื่องที่สถานะ `Is_Type` = OPEN |
| **งานของฉัน** (`ServiceRepo.SelectMyService`) | งานที่ `Owner_ID` = เรา และ `Is_Received = T` |

ระหว่างนี้ใช้ endpoint ที่มีอยู่แทนได้:
- `GET /Services/GetPendingServices`: เรื่องที่มีงานยังไม่ปิดมอบให้เรา (ไม่ได้ดู `isReceived`)
- `GET /Services/GetInProcessServices`: เรื่องที่เราเป็นเจ้าของและยังไม่ปิด
