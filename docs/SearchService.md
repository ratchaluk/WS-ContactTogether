# ค้นหาเรื่อง (`GET /Services/SearchService`) สำหรับ Frontend

ใช้ค้นหา **เรื่อง (service request)** จากเงื่อนไขต่าง ๆ เช่น เบอร์โทรของผู้ติดต่อ เลขที่เรื่อง สถานะ
ประเภท ที่มาข้อมูล ความสำคัญ หรือช่วงวันที่สร้าง เรียงล่าสุดก่อนหรือเก่าสุดก่อนได้ ผลลัพธ์แบ่งหน้า และบอกจำนวนทั้งหมดที่ค้นเจอ

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`SearchService`), `Dtos/ServiceSearchRequest.cs`,
> `Dtos/ServiceSearchResponse.cs`, `Dtos/ResponseModel.cs` (`PagedEnvelope<T>`)

> **เปลี่ยนแปลง (19 พ.ย. 2026):**
>
> - **เปลี่ยนชื่อ:** `GET /Services/GetServices` เป็น `GET /Services/SearchService` ชื่อเดิมใช้ไม่ได้แล้ว (ได้ 404)
>   parameter และ response เหมือนเดิมทุกอย่าง แก้แค่ URL
> - **เงื่อนไขใหม่:** `ServiceReference` (ที่มาข้อมูล), `LevelPriorityId` (ความสำคัญ) และ `AccountId` (ผู้ติดต่อ ดูข้อ 2.4)
> - **field ใหม่ใน `data`:** `accountId` ของผู้ติดต่อในแต่ละเรื่อง
> - **เรียงลำดับ:** `SortOrder=desc` (ล่าสุดก่อน ค่าเริ่มต้น) หรือ `SortOrder=asc` (เก่าสุดก่อน)

> **เปลี่ยนแปลง (16 พ.ย. 2026):** เดิม response เป็น array ตรง ๆ และตัดไว้ที่ 200 แถว
> ตอนนี้เป็น envelope `{ status, message, data, meta.pagination }` แบบเดียวกับ `GetReportXX`
> ถ้ามีโค้ดที่อ่าน response เป็น array อยู่ ต้องเปลี่ยนไปอ่าน `data` แทน

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` (ไม่มี body ส่งทุกอย่างทาง query string) |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Backend (dev) | `http://localhost:5018/Services/SearchService` |
| URL จาก Next.js | `/api/backend/Services/SearchService` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

ตัวอย่าง:

```
GET /Services/SearchService?AccountPhoneNo=0909840822&PageNumber=1&PageSize=20
```

---

## 2. Request (query string)

### 2.1 Parameter

ทุกตัวไม่บังคับ แต่ **ต้องมีเงื่อนไขค้นหาอย่างน้อย 1 ตัว** (`SortOrder`/`PageNumber`/`PageSize` ไม่นับ)
ส่งหลายตัวพร้อมกันได้ ผลลัพธ์ต้องตรง **ทุกเงื่อนไข** (AND) ชื่อ parameter ไม่สนตัวพิมพ์เล็ก/ใหญ่

| Parameter | Type | การค้นหา | ความหมาย |
|---|---|---|---|
| `AccountId` | string | ตรงทั้งค่า | id ผู้ติดต่อ (`TblAccount.Id`) ตัวพิมพ์เล็กหรือใหญ่ก็ได้ **ใช้ตัวนี้เมื่อเลือกผู้ติดต่อแล้ว** (ข้อ 2.4) |
| `AccountPhoneNo` | string | ตรงทั้งค่า | เบอร์โทรของผู้ติดต่อ (MOBILE / HOME / OFFICE / FAX) เช่น `0909840822` |
| `ServiceCode` | string | **บางส่วน** | เลขที่เรื่อง เช่น `690702` เจอเรื่องที่มี `690702` อยู่ในเลขที่ (เรื่องของวันที่ 2 ก.ค. 2569) |
| `ServiceSummary` | string | **บางส่วน** | หัวเรื่อง |
| `ServicesStatus` | string | ตรงทั้งค่า | id สถานะ (`TblStatus.Id`) |
| `ServiceType` | string | ตรงทั้งค่า | id ประเภทบริการ (`TblCategory.Id`) จาก `GET /ListOfValues/GetServiceType` |
| `ServiceTitleId` | string | ตรงทั้งค่า | id หัวข้อเรื่อง (`TblServiceTitles.Id`) ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| `ServiceReference` | string | ตรงทั้งค่า | id ที่มาข้อมูล (`TblReference.Id`) ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| `LevelPriorityId` | string | ตรงทั้งค่า | id ความสำคัญ (`TblLevel.Id` ประเภท `PRIORITY`) ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| `ServiceOwner` | string | ตรงทั้งค่า | id เจ้าหน้าที่เจ้าของเรื่อง (`TblEmployee.Id`) |
| `OrganizationGroup` | string | ตรงทั้งค่า | id หน่วยงานแม่ เจอเรื่องของหน่วยงานนั้นและหน่วยงานย่อยทั้งหมดของมัน |
| `Organization` | string | ตรงทั้งค่า | id หน่วยงาน (`TblOrganization.Id`) เจอเฉพาะหน่วยงานนั้น |
| `ServiceCreatedFrom` | date-time | ≥ | วันที่สร้างเรื่อง ตั้งแต่ (รวมค่านี้) |
| `ServiceCreatedTo` | date-time | ≤ | วันที่สร้างเรื่อง ถึง (รวมค่านี้) |
| `SortOrder` | string | | `desc` = ล่าสุดก่อน (ค่าเริ่มต้น), `asc` = เก่าสุดก่อน ตัวพิมพ์เล็กหรือใหญ่ก็ได้ ค่าอื่นได้ 400 |
| `PageNumber` | int | | หน้าที่ต้องการ เริ่มที่ `1` (ค่าเริ่มต้น `1`) |
| `PageSize` | int | | จำนวนแถวต่อหน้า `1`–`200` (ค่าเริ่มต้น `20`) |

### 2.2 กติกาที่ควรรู้

- **ค่าว่างหรือมีแต่ช่องว่าง = ไม่ได้ส่ง** และ backend ตัดช่องว่างหน้า/หลังให้เอง
  ส่ง `AccountPhoneNo=` เปล่า ๆ อย่างเดียวจึงได้ 400 ("ต้องมีเงื่อนไขอย่างน้อย 1 ตัว")
- **เบอร์โทร** ต้องตรงกับที่บันทึกไว้ทั้งค่า ส่งเป็นตัวเลขล้วน ห้ามมีขีดหรือช่องว่างตรงกลาง (`090-984-0822` ไม่เจอ)
- **`ServiceCode` / `ServiceSummary`** ค้นแบบ "มีคำนี้อยู่" และ **แยกตัวพิมพ์เล็ก/ใหญ่** (collation ของ DB)
- **วันที่** เป็นเวลาไทย ไม่มี offset ส่งได้ 2 แบบ:
  - `yyyy-MM-dd` เช่น `2026-07-02`
  - `yyyy-MM-ddTHH:mm:ss` เช่น `2026-07-02T08:30:00` (ค่าจาก `<input type="datetime-local">` ใช้ได้เลย)

  **ห้ามต่อท้ายด้วย `Z` หรือ `+07:00`** และอย่าส่งค่าจาก `date.toISOString()` เพราะเป็นเวลา UTC
- `ServiceCreatedTo` ที่เป็นวันที่อย่างเดียว (ไม่มีเวลา) **นับรวมทั้งวัน** ถึง 23:59:59
  ค้นเรื่องของวันเดียวจึงส่ง `ServiceCreatedFrom=2026-07-02&ServiceCreatedTo=2026-07-02` ได้เลย
- `ServiceCreatedFrom` ต้องไม่มากกว่า `ServiceCreatedTo`
- ได้เฉพาะเรื่องที่เปิดใช้งาน (`IsEnable = "T"`) ไม่ว่าจะปิดเรื่องแล้วหรือยัง

### 2.3 ที่มาของ id

| Parameter | ดึงรายการจาก |
|---|---|
| `ServiceType` | `GET /ListOfValues/GetServiceType` (`id`) |
| `ServiceTitleId` | `GET /ListOfValues/GetServiceTitle` ([GetServiceTitle.md](GetServiceTitle.md)) |
| `ServiceReference` | `GET /ListOfValues/GetReference` ([GetReference.md](GetReference.md)) |
| `LevelPriorityId` | `GET /ListOfValues/GetLevel?levelType=PRIORITY` ([GetLevel.md](GetLevel.md)) |
| `ServicesStatus` | `GET /ListOfValues/GetStatus` ([GetStatus.md](GetStatus.md)) |
| `ServiceOwner` | `GET /ListOfValues/GetEmployee` ([GetEmployee.md](GetEmployee.md)) |
| `OrganizationGroup`, `Organization` | `GET /ListOfValues/GetOrganization` ([GetOrganization.md](GetOrganization.md)) หน่วยงานหลัก / หน่วยงานย่อย |

**ข้อมูลจริง ณ 19 พ.ย. 2026** (เรื่องที่เปิดใช้งาน 194 เรื่อง)

- **ความสำคัญ:** ทุกเรื่องเป็น `3-Medium` ค้นด้วยระดับอื่นจึงยังได้ `ไม่พบข้อมูล`
- **ที่มาข้อมูล:** ส่วนใหญ่เป็น `Joomla` (152 เรื่อง) มี 22 เรื่องที่ไม่ได้ระบุที่มา ค้นด้วยเงื่อนไขนี้ไม่เจอเรื่องกลุ่มนั้น

### 2.4 เบอร์เดียวมีผู้ติดต่อหลายคน: ใช้ `AccountId`

`AccountPhoneNo` ค้นจากเบอร์ จึงได้เรื่องของ **ทุกคนที่มีเบอร์นั้น** ข้อมูลจริงมีเบอร์ที่ผูกกับผู้ติดต่อมากกว่า 1 คนถึง 2,015 เบอร์

ตัวอย่าง (ข้อมูลจริง ณ 19 พ.ย. 2026): เบอร์ `0934153694` มี 2 คน

| ค้นด้วย | ได้ |
|---|---|
| `AccountPhoneNo=0934153694` | 14 เรื่อง: นาย อาม 1 + นาย อินดี้ 13 |
| `AccountId=5A15B0A6398344718D6473C1B7DFB7CA` | 1 เรื่อง ของนาย อาม เท่านั้น |

**Flow ที่แนะนำ (หน้ารับสาย → หน้าค้นหาบริการ)**

1. หน้ารับสายเรียก `GetAccountByPhoneNo` ได้ผู้ติดต่อหลายคน (แต่ละแถวมี `accountId`) ให้ผู้ใช้เลือกคนที่โทรเข้า
2. ส่ง `accountId` ของคนที่เลือกไปหน้าค้นหา แล้วเรียก `SearchService?AccountId=<accountId>` **ไม่ต้องส่ง `AccountPhoneNo`**
3. เบอร์ที่มีคนเดียวก็ใช้ `AccountId` ได้เหมือนกัน (ผลเท่ากับค้นด้วยเบอร์) จะได้มี flow เดียว

- **ส่งคู่กันได้:** ถ้าส่ง `AccountId` คู่กับ `AccountPhoneNo` ต้องตรงทั้งสองอย่าง (AND) ถ้าเบอร์ไม่ใช่ของคนนั้นจะได้ `ไม่พบข้อมูล`
- **อยากดูทุกเรื่องของเบอร์นี้:** ค้นด้วย `AccountPhoneNo` อย่างเดียวเหมือนเดิม แล้วใช้ `accountId` ของแต่ละแถว
  แยกกลุ่มหรือไฮไลต์เรื่องของคนที่เลือก อย่าแยกด้วย `accountFullname` เพราะชื่อซ้ำกันได้

---

## 3. Response

### 3.1 รูปแบบ

```ts
{
  status: "success",
  message: string,          // ข้อความภาษาไทยที่แสดงให้ผู้ใช้เห็นได้เลย
  data: ServiceSearchRow[], // แถวของหน้านี้
  meta: {
    pagination: {
      current_page: number, // = PageNumber ที่ส่งมา
      per_page: number,     // = PageSize ที่ส่งมา
      total: number,        // จำนวนเรื่องทั้งหมดที่ตรงเงื่อนไข (ทุกหน้ารวมกัน)
      total_pages: number   // ceil(total / per_page), เป็น 0 เมื่อไม่พบข้อมูล
    }
  }
}
```

- `message` เป็น `พบข้อมูล N รายการ` หรือ `ไม่พบข้อมูล`
- key ใน `pagination` เป็น **snake_case** ส่วน field ใน `data` เป็น camelCase

**Field ใน `data`**

| Field | Type | ความหมาย |
|---|---|---|
| `serviceId` | string | id ของเรื่อง (`TblService.Id`) ใช้อ้างถึงเรื่องนี้ในการเรียกครั้งต่อไป |
| `serviceCode` | string \| null | เลขที่เรื่อง 11 หลัก: `yyMMdd` (พ.ศ.) + เลขรัน 5 หลัก เช่น `69070200055` |
| `serviceOwner` | string | ชื่อเต็มเจ้าหน้าที่เจ้าของเรื่อง (คำนำหน้า ชื่อ นามสกุล) เป็น `""` ถ้าไม่มีข้อมูล |
| `accountId` | string \| null | id ผู้ติดต่อ (`TblAccount.Id`) ใช้แยกว่าเรื่องไหนเป็นของใครเมื่อค้นด้วยเบอร์ (ข้อ 2.4) |
| `accountFullname` | string | ชื่อเต็มผู้ติดต่อ เป็น `""` ถ้าไม่มีข้อมูล |
| `serviceType` | string \| null | ชื่อประเภทบริการ (ภาษาไทย) |
| `serviceTitleId` | string \| null | id หัวข้อเรื่อง (ตัวพิมพ์ใหญ่) แปลงเป็นชื่อด้วย [GetServiceTitle.md](GetServiceTitle.md) |
| `serviceSummary` | string \| null | หัวเรื่อง |
| `servicesStatus` | string \| null | ชื่อสถานะ (ภาษาไทย) |
| `serviceCreated` | string \| null | วันเวลาที่สร้างเรื่อง เวลาไทย ไม่มี offset เช่น `2026-07-02T15:01:00` |

- **เรียงตาม `serviceCreated` แล้วตาม `serviceCode`:** ล่าสุดก่อนเป็นค่าเริ่มต้น หรือเก่าสุดก่อนเมื่อส่ง `SortOrder=asc`
  ลำดับคงที่ เปลี่ยนหน้าแล้วแถวไม่ซ้ำหรือหาย (เปลี่ยน `SortOrder` แล้วให้เริ่มที่หน้า 1)
- ขอหน้าที่เกินจำนวนหน้า (เช่น `PageNumber=99`) ได้ `200` กับ `data: []` แต่ `total` ยังเป็นจำนวนจริง

### 3.2 ตัวอย่าง

`GET /Services/SearchService?AccountPhoneNo=0909840822&PageNumber=1&PageSize=3`

(เลขที่เรื่อง ประเภท และสถานะเป็นข้อมูลจริง ณ 16 พ.ย. 2026 ส่วนชื่อคนและหัวเรื่องเป็นค่าสมมุติ)

```json
{
  "status": "success",
  "message": "พบข้อมูล 26 รายการ",
  "data": [
    {
      "serviceId": "9F1C2B7E4A3D4E5F8A6B7C8D9E0F1A2B",
      "serviceCode": "69070200055",
      "serviceOwner": "นางสาว สมใจ ตัวอย่าง",
      "accountFullname": "นาย สมชาย ทดสอบ",
      "serviceType": "Q&A",
      "serviceSummary": "สอบถามขั้นตอนการยื่นเอกสาร",
      "servicesStatus": "Internally Closed",
      "serviceCreated": "2026-07-02T15:01:00"
    },
    {
      "serviceId": "1A2B3C4D5E6F47809A1B2C3D4E5F6A7B",
      "serviceCode": "69070200052",
      "serviceOwner": "นางสาว สมใจ ตัวอย่าง",
      "accountFullname": "นาย สมชาย ทดสอบ",
      "serviceType": "Literature",
      "serviceSummary": "ขอเอกสารประกอบ",
      "servicesStatus": "อยู่ระหว่างดำเนินการ",
      "serviceCreated": "2026-07-02T14:54:00"
    },
    {
      "serviceId": "7B6A5F4E3D2C41B0A9F8E7D6C5B4A392",
      "serviceCode": "69070200046",
      "serviceOwner": "นาย ใจดี ตัวอย่าง",
      "accountFullname": "นาย สมชาย ทดสอบ",
      "serviceType": "Q&A",
      "serviceSummary": "ติดตามผลการดำเนินการ",
      "servicesStatus": "อยู่ระหว่างดำเนินการ",
      "serviceCreated": "2026-07-02T14:44:00"
    }
  ],
  "meta": {
    "pagination": { "current_page": 1, "per_page": 3, "total": 26, "total_pages": 9 }
  }
}
```

**ไม่พบข้อมูล** (ยังเป็น `200`)

```json
{
  "status": "success",
  "message": "ไม่พบข้อมูล",
  "data": [],
  "meta": {
    "pagination": { "current_page": 1, "per_page": 20, "total": 0, "total_pages": 0 }
  }
}
```

**400: ข้อมูลที่ส่งมาไม่ถูกต้อง** (ProblemDetails ข้อความใน `errors` เป็นภาษาอังกฤษ ไม่ควรแสดงให้ผู้ใช้ตรง ๆ)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "": ["At least one search parameter is required."]
  },
  "traceId": "00-..."
}
```

`errors` อาจมี key อื่น เช่น `PageSize` (`PageSize must be between 1 and 200.`),
`PageNumber`, `SortOrder` (`SortOrder must be 'desc' or 'asc'.`),
`ServiceCreatedFrom` (วันที่เริ่มมากกว่าวันที่สิ้นสุด หรือรูปแบบวันที่ผิด)

### 3.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ **รวมกรณีไม่พบข้อมูล** (`data: []`) | `PagedEnvelope<ServiceSearchRow>` |
| 400 | ไม่มีเงื่อนไขค้นหาเลย, `SortOrder` ไม่ใช่ `desc`/`asc`, `PageNumber`/`PageSize` ผิดช่วง, วันที่ผิดรูปแบบ, วันที่เริ่มมากกว่าวันที่สิ้นสุด | ProblemDetails |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 4. โค้ดตัวอย่าง (Next.js + TypeScript)

### 4.1 Type และฟังก์ชันเรียก API

```ts
// lib/api/services.ts
export interface ServiceSearchParams {
  accountId?: string;      // ผู้ติดต่อที่เลือก (ข้อ 2.4)
  accountPhoneNo?: string;
  serviceCode?: string;
  serviceSummary?: string;
  servicesStatus?: string;
  serviceType?: string;
  serviceTitleId?: string;
  serviceReference?: string; // id จาก GetReference
  levelPriorityId?: string;  // id จาก GetLevel?levelType=PRIORITY
  serviceOwner?: string;
  organizationGroup?: string;
  organization?: string;
  serviceCreatedFrom?: string; // "yyyy-MM-dd" หรือ "yyyy-MM-ddTHH:mm:ss" เวลาไทย ห้ามมี Z
  serviceCreatedTo?: string;
  sortOrder?: "desc" | "asc"; // ล่าสุดก่อน (ค่าเริ่มต้น) / เก่าสุดก่อน
  pageNumber?: number;
  pageSize?: number;
}

export interface ServiceSearchRow {
  serviceId: string;
  serviceCode: string | null;
  serviceOwner: string;
  accountId: string | null;
  accountFullname: string;
  serviceType: string | null;
  serviceTitleId: string | null;
  serviceSummary: string | null;
  servicesStatus: string | null;
  serviceCreated: string | null;
}

export interface PagedEnvelope<T> {
  status: "success";
  message: string;
  data: T[];
  meta: {
    pagination: {
      current_page: number;
      per_page: number;
      total: number;
      total_pages: number;
    };
  };
}

export class ServicesApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
  }
}

const FILTER_KEYS: (keyof ServiceSearchParams)[] = [
  "accountId", "accountPhoneNo", "serviceCode", "serviceSummary", "servicesStatus", "serviceType",
  "serviceTitleId", "serviceReference", "levelPriorityId",
  "serviceOwner", "organizationGroup", "organization", "serviceCreatedFrom", "serviceCreatedTo",
]; // sortOrder / pageNumber / pageSize ไม่นับเป็นเงื่อนไข

/** true เมื่อมีเงื่อนไขค้นหาอย่างน้อย 1 ตัว ใช้ปิดปุ่มค้นหาไว้ก่อน จะได้ไม่ยิงไปแล้วได้ 400 */
export function hasSearchFilter(params: ServiceSearchParams): boolean {
  return FILTER_KEYS.some((k) => String(params[k] ?? "").trim() !== "");
}

export async function searchServices(
  params: ServiceSearchParams,
  opts: { token: string; signal?: AbortSignal },
): Promise<PagedEnvelope<ServiceSearchRow>> {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    const text = String(value ?? "").trim();
    if (text !== "") qs.set(key, text); // ชื่อ parameter ไม่สนตัวพิมพ์ ใช้ camelCase ได้
  }

  const res = await fetch(`/api/backend/Services/SearchService?${qs}`, {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });

  if (res.ok) return res.json();
  if (res.status === 401) throw new ServicesApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 400) {
    const problem = await res.json().catch(() => null);
    throw new ServicesApiError(400, "เงื่อนไขการค้นหาไม่ถูกต้อง", problem?.errors);
  }
  throw new ServicesApiError(res.status, "ค้นหาเรื่องไม่สำเร็จ");
}
```

### 4.2 ใช้ในหน้าค้นหา

```tsx
"use client";

import { useState } from "react";
import {
  hasSearchFilter, searchServices,
  type PagedEnvelope, type ServiceSearchParams, type ServiceSearchRow,
} from "@/lib/api/services";

const PAGE_SIZE = 20;

export function ServiceSearch({ token }: { token: string }) {
  const [filters, setFilters] = useState<ServiceSearchParams>({});
  const [result, setResult] = useState<PagedEnvelope<ServiceSearchRow> | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load(pageNumber: number) {
    setError(null);
    try {
      setResult(await searchServices({ ...filters, pageNumber, pageSize: PAGE_SIZE }, { token }));
    } catch (e) {
      setError((e as Error).message);
    }
  }

  const page = result?.meta.pagination;

  return (
    <div>
      <input
        placeholder="เบอร์โทร"
        value={filters.accountPhoneNo ?? ""}
        onChange={(e) => setFilters({ ...filters, accountPhoneNo: e.target.value })}
      />
      <select
        value={filters.sortOrder ?? "desc"}
        onChange={(e) => setFilters({ ...filters, sortOrder: e.target.value as "desc" | "asc" })}
      >
        <option value="desc">ล่าสุด</option>
        <option value="asc">เก่าสุด</option>
      </select>
      {/* ค้นใหม่ทุกครั้งให้กลับไปหน้า 1 */}
      <button disabled={!hasSearchFilter(filters)} onClick={() => load(1)}>ค้นหา</button>

      {error && <p>{error}</p>}
      {result && <p>{result.message}</p>}

      <table>
        <tbody>
          {result?.data.map((row) => (
            <tr key={row.serviceId}>
              <td>{row.serviceCode}</td>
              <td>{row.serviceSummary}</td>
              <td>{row.serviceType}</td>
              <td>{row.servicesStatus}</td>
              <td>{row.accountFullname}</td>
              <td>{row.serviceOwner}</td>
              <td>{row.serviceCreated?.replace("T", " ")}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {page && page.total_pages > 1 && (
        <div>
          <button disabled={page.current_page <= 1} onClick={() => load(page.current_page - 1)}>ก่อนหน้า</button>
          <span> หน้า {page.current_page} / {page.total_pages} </span>
          <button disabled={page.current_page >= page.total_pages} onClick={() => load(page.current_page + 1)}>ถัดไป</button>
        </div>
      )}
    </div>
  );
}
```

### 4.3 ข้อแนะนำ

- เปลี่ยนเงื่อนไขค้นหาหรือ `SortOrder` แล้วให้เริ่มที่ `PageNumber=1` เสมอ
- แสดง `message` ได้ตรง ๆ แต่ไม่ควรแสดงข้อความใน `errors` ของ 400 ให้ผู้ใช้เห็น
- `serviceCreated` เป็นเวลาไทยอยู่แล้ว ถ้าจะแปลงเป็น `Date` ให้ถือเป็นเวลาท้องถิ่น
  อย่าต่อ `Z` แล้วค่อย parse เพราะเวลาจะเลื่อนไป 7 ชั่วโมง
- ถ้าต้องการวันที่แบบ พ.ศ. ให้แปลงที่หน้าเว็บ API ส่งมาเป็น ค.ศ.

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/Services/SearchService?AccountPhoneNo=0909840822&PageNumber=1&PageSize=20
Authorization: Bearer <token>
```

```bash
curl "http://localhost:5018/Services/SearchService?AccountPhoneNo=0909840822&PageNumber=1&PageSize=20" \
  -H "Authorization: Bearer <token>"
```

ตัวอย่างอื่น:

```
# เรื่องของวันที่ 2 ก.ค. 2026 ทั้งวัน
GET /Services/SearchService?ServiceCreatedFrom=2026-07-02&ServiceCreatedTo=2026-07-02

# เลขที่เรื่องที่มี 690702 อยู่ หน้า 2 หน้าละ 50
GET /Services/SearchService?ServiceCode=690702&PageNumber=2&PageSize=50

# ที่มาข้อมูล Joomla เก่าสุดก่อน
GET /Services/SearchService?ServiceReference=0199D0ECBE0F08A02BDFDB8460CAB362&SortOrder=asc

# ความสำคัญ 3-Medium ล่าสุดก่อน (ค่าเริ่มต้น)
GET /Services/SearchService?LevelPriorityId=D550B0AAB78CEEE3E6C027E477932A35
```

ใน `ContactTogetherApi.http` ของ backend มี request นี้อยู่แล้ว (รัน `login` ก่อนเพื่อให้ได้ `{{token}}`)
