# หน้าแรก: รายการรับ / งานดำเนินการ (`GET /Home/GetInboxServices`, `GET /Home/GetProcessingServices`) สำหรับ Frontend

ใช้ทำกล่อง **"รายการให้บริการ"** ในหน้าแรกหลังล็อกอิน มี 2 แท็บ ใช้ตารางเดียวกัน ต่างกันแค่เงื่อนไข

| แท็บ | Endpoint | เรื่องที่ได้ |
|---|---|---|
| **รายการรับ** | `GET /Home/GetInboxServices` | เรื่องที่ **คนอื่นโอนงานมาให้เรา** และเรา **ยังไม่ได้รับงาน** |
| **งานดำเนินการ** | `GET /Home/GetProcessingServices` | เรื่องที่ **เราเป็นเจ้าของ** และ **ยังไม่ปิด** |

> อ้างอิงโค้ด: `Controllers/HomeController.cs`, `Dtos/HomeServiceListRequest.cs`, `Dtos/HomeServiceListItemResponse.cs`
>
> ที่มา: [Spec-Home-Inbox-Processing-API.md](Spec-Home-Inbox-Processing-API.md) (สรุปจากระบบเดิม) ส่วนที่ทำต่างจาก spec ดูข้อ 6
>
> เอกสารที่ใช้คู่กัน: [ReceiveService.md](ReceiveService.md) (กดเปิดเรื่องแล้วให้หายจากรายการรับ),
> [InboxStream.md](InboxStream.md) (ให้รายการรับอัปเดตเองแบบ real-time เพิ่ม 26 พ.ย. 2569)
>
> **เพิ่มเมื่อ 25 พ.ย. 2569**

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` ผู้ใช้ = ผู้ที่ login อยู่ (ส่งรหัสพนักงานมาเองไม่ได้) |
| Query | `PageNumber`, `PageSize`, `Lang` (ดูข้อ 2) เหมือนกันทั้งสอง endpoint |
| Backend (dev) | `http://localhost:5018/Home/GetInboxServices`, `http://localhost:5018/Home/GetProcessingServices` |
| URL จาก Next.js | `/api/backend/Home/GetInboxServices`, `/api/backend/Home/GetProcessingServices` (ผ่าน `rewrites()`) |
| ลำดับ | `code` มากไปน้อย (เรื่องใหม่ก่อน) |

**เงื่อนไข**

- เรื่องที่ "ยังไม่ปิด" = สถานะของเรื่องมี `isType` เป็น `OPEN` หรือ `PENDING` (เช่น เปิดเรื่อง, อยู่ระหว่างดำเนินการ, ทำซ้ำ)
- **รายการรับ:** เรื่องยังไม่ปิด และมีการดำเนินการ (งาน) ที่ **มอบให้เรา** ยัง **รอการตอบรับ** (`isReceived = "F"`)
  และ **สถานะของงานเป็นปิด** (`isType = CLOSE` คือขั้นตอนโอนทำเสร็จแล้ว ตามระบบเดิม) งานโอนที่ยังเปิดอยู่จะไม่ขึ้น
- **งานดำเนินการ:** เรื่องยังไม่ปิดที่ `ownerId` เป็นเรา
- เรื่องที่โอนมาแต่ยังไม่รับ **อาจอยู่ทั้งสองแท็บ** (ถ้าเราเป็นเจ้าของเรื่องด้วย) เหมือนระบบเดิม

**ทำให้หายจากรายการรับ:** ผู้รับเปิดหน้ารายละเอียดเรื่อง แล้ว frontend เรียก
`POST /Services/ReceiveService?id=<id>` ([ReceiveService.md](ReceiveService.md)) งานจะเป็น `"T"` และเรื่องหายจากรายการรับ

---

## 2. Query

| ชื่อ | ชนิด | ค่าเริ่มต้น | กฎ |
|---|---|---|---|
| `PageNumber` | int | `1` | ≥ 1 |
| `PageSize` | int | `10` | 1–100 |
| `Lang` | string | `th` | `th` หรือ `en` (ตัวพิมพ์ใหญ่ก็ได้) ใช้กับ `categoryName` และ `statusName` ถ้าไม่มีชื่ออังกฤษจะได้ชื่อไทย |

---

## 3. Response

### 3.1 รูปแบบ

200 ห่อด้วย envelope แบบเดียวกับ `SearchService` และรายงาน (`meta.pagination` เป็น snake_case)

```json
{
  "status": "success",
  "message": "พบข้อมูล 19 รายการ",
  "data": [
    {
      "id": "1D7FED1A1E0E4FB1A7341A8CA0FA560F",
      "code": "69112500001",
      "dateOpened": "2026-11-25T09:48:29",
      "categoryName": "แจ้งการทุจริต",
      "statusName": "เปิดเรื่อง",
      "summary": "test โยนเรื่อง",
      "callBack": "N",
      "requireCallBack": false,
      "elapsedHours": 8,
      "isOverDue": false
    }
  ],
  "meta": {
    "pagination": { "current_page": 1, "per_page": 10, "total": 19, "total_pages": 2 }
  }
}
```

ไม่มีข้อมูล: `data: []`, `message: "ไม่พบข้อมูล"`, `total: 0`, `total_pages: 0` (ยังเป็น 200)
ขอหน้าที่เลยหน้าสุดท้าย: `data: []` แต่ `total` ยังเป็นจำนวนจริง

### 3.2 Field

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `id` ของเรื่อง ใช้เปิดหน้ารายละเอียด (`GetServiceById?id=`) และ `ReceiveService?id=` |
| `code` | string | รหัสบริการ เช่น `69112500001` |
| `dateOpened` | string | วันเปิดบริการ เวลาไทย ไม่มี offset (`2026-11-25T09:48:29`) แปลงเป็นปี พ.ศ. ตอนแสดงเอง |
| `categoryName` | string \| null | ประเภทบริการ ตาม `Lang` |
| `statusName` | string | สถานะของเรื่อง ตาม `Lang` |
| `summary` | string \| null | สาระสำคัญ **ข้อความเต็ม** ตัดคำเอง (ระบบเดิมตัดที่ 50 ตัวอักษรตามช่องว่าง) |
| `callBack` | string \| null | ค่าเดิม `"Y"` ต้องการติดต่อกลับ / `"N"` ไม่ต้องการ / `"D"` สายหลุด / อื่น ๆ |
| `requireCallBack` | boolean | `true` เมื่อ `callBack === "Y"` เท่านั้น ใช้เลือกไอคอนโทรกลับ |
| `elapsedHours` | number \| null | ชั่วโมงที่ **ผ่านไปแล้ว** นับจาก `dateOpened` (ปัดเศษ) ไม่ใช่ชั่วโมงที่เหลือ |
| `isOverDue` | boolean | เปิดมาแล้วตั้งแต่ 7 วัน (10,080 นาที) ขึ้นไป |

**ไอคอนตามระบบเดิม**

| | `isOverDue: false` | `isOverDue: true` |
|---|---|---|
| `requireCallBack: false` | `time_in_due` | `time_over_due` |
| `requireCallBack: true` | `callback_in_due` | `callback_over_due` |

### 3.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล) | envelope ตามข้อ 3.1 |
| 400 | `PageNumber` < 1, `PageSize` นอกช่วง 1–100, `Lang` ไม่ใช่ `th`/`en`, ค่าไม่ใช่ตัวเลข | ProblemDetails (`errors.PageNumber` / `errors.PageSize` / `errors.Lang`) |
| 401 | ไม่มี token, token หมดอายุ, logout แล้ว (body ว่าง) หรือ token ไม่มีรหัสพนักงาน | ว่าง หรือ `{ statusCode, message }` |
| 500 | ข้อผิดพลาดฝั่ง server | อย่าพึ่งรูปแบบ |

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/home.ts
export interface HomeServiceItem {
  id: string;
  code: string;
  dateOpened: string | null;
  categoryName: string | null;
  statusName: string | null;
  summary: string | null;
  callBack: string | null;
  requireCallBack: boolean;
  elapsedHours: number | null;
  isOverDue: boolean;
}

export interface Paged<T> {
  status: string;
  message: string;
  data: T[];
  meta: { pagination: { current_page: number; per_page: number; total: number; total_pages: number } };
}

export type HomeTab = "inbox" | "processing";

export async function fetchHomeServices(opts: {
  tab: HomeTab;
  token: string;
  page?: number;
  pageSize?: number;
  lang?: "th" | "en";
  signal?: AbortSignal;
}): Promise<Paged<HomeServiceItem>> {
  const path = opts.tab === "inbox" ? "GetInboxServices" : "GetProcessingServices";
  const qs = new URLSearchParams({
    PageNumber: String(opts.page ?? 1),
    PageSize: String(opts.pageSize ?? 10),
    Lang: opts.lang ?? "th",
  });
  const res = await fetch(`/api/backend/Home/${path}?${qs}`, {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("โหลดรายการไม่สำเร็จ");
}

// ตัดคำแบบระบบเดิม: ไม่เกิน 50 ตัวอักษร ตัดที่ช่องว่าง
export function shortSummary(text: string | null, max = 50): string {
  if (!text || text.length <= max) return text ?? "";
  const cut = text.lastIndexOf(" ", max);
  return `${text.slice(0, cut > 0 ? cut : max)}...`;
}
```

- เปลี่ยนแท็บ = เปลี่ยน `tab` แล้วเริ่มที่หน้า 1 ใหม่ (ระบบเดิมมีบั๊กเปลี่ยนหน้าในแท็บรายการรับแล้วได้งานดำเนินการ อย่าใช้ state หน้าร่วมกันข้ามแท็บ)
- หลังกลับจากหน้ารายละเอียด (ที่เรียก `ReceiveService` แล้ว) ให้โหลดรายการรับใหม่ เรื่องนั้นจะหายไป

---

## 5. ทดสอบด้วย REST Client / curl

ตัวอย่างอยู่ใน `ContactTogetherApi.http` (หัวข้อ Home)

```bash
curl "http://localhost:5018/Home/GetInboxServices" -H "Authorization: Bearer <token>"
curl "http://localhost:5018/Home/GetProcessingServices?PageNumber=2&PageSize=10&Lang=en" -H "Authorization: Bearer <token>"
```

---

## 6. ต่างจาก spec / endpoint เดิม

| เรื่อง | Spec (ระบบเดิม) | ที่ทำ |
|---|---|---|
| เรื่องที่ยังไม่ปิด | สถานะ `PENDING` อย่างเดียว | `OPEN` หรือ `PENDING` เพราะระบบใหม่เปิดเรื่องและเปิดเรื่องซ้ำด้วยสถานะ "เปิดเรื่อง" (`OPEN`) ถ้าใช้ PENDING อย่างเดียว เรื่องที่โอนกันอยู่ตอนนี้จะไม่ขึ้นเลย |
| `requireCallBack` | `callBack != "N"` | `callBack == "Y"` ตรงกับรายงาน 03 (`"D"` สายหลุด ไม่นับ) |
| Route / ชื่อ query | `api/service-requests/inbox`, `page`, `pageSize`, `lang` | `/Home/GetInboxServices`, `PageNumber`, `PageSize`, `Lang` ตามแบบของโปรเจกต์ |
| Response | `{ success, data: { items, page, pageSize, totalCount, totalPages } }` | `{ status, message, data, meta.pagination }` แบบ `SearchService` |
| รับงาน | `POST api/service-requests/{id}/receive` | ใช้ `POST /Services/ReceiveService?id=` ที่มีอยู่ (ทำงานเหมือนกัน) |
| ประเภทบริการหาย | `JOIN` (เรื่องไม่ขึ้น) | เรื่องยังขึ้น `categoryName` เป็น `null` |

`GET /Services/GetPendingServices` และ `GET /Services/GetInProcessServices` (มีอยู่ก่อน) **ยังใช้ได้เหมือนเดิม** แต่ใช้เงื่อนไขคนละแบบ
(ดูงานที่ยังไม่ปิด / `dateClosed` ไม่ได้ดู `isReceived` และไม่แบ่งหน้า) หน้าแรกให้ใช้สอง endpoint ในเอกสารนี้
