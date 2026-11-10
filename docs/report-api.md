# Report API (`/Report/GetReport01`–`07`) สำหรับ Frontend

เอกสารนี้อธิบาย payload และวิธีเรียกรายงานทั้ง 7 ตัวของ `ReportController` ทุกตัวเป็น `POST` ที่รับ
JSON ช่วงวันที่ และตอบกลับเป็นข้อมูลแบบแบ่งหน้า (paged) ในรูปแบบเดียวกัน
ส่วน export เป็นไฟล์ PDF / Excel (`ExportReport01`–`07`) อยู่ที่ [ข้อ 8](#8-export-pdf--excel-exportreport01-07)

> อ้างอิงโค้ด: `Controllers/ReportController.cs`, `Controllers/ReportController.Export.cs`, `Dtos/RequestModel.cs`, `Dtos/ResponseModel.cs`

---

## 1. ภาพรวม

| Endpoint | รายงาน | field เพิ่มใน request | `data[]` เป็นชนิด | เรียงตาม |
|---|---|---|---|---|
| `POST /Report/GetReport01` | SR ทั้งหมดที่สร้างในช่วงเวลา | – | `ServiceRequestRow` | `code` |
| `POST /Report/GetReport02` | SR ประเภทสายหลุด (`CallBack = "D"`) | – | `SrCallbackRow` | `code` |
| `POST /Report/GetReport03` | SR ที่ `CallBack = "Y"` (Require CallBack) | – | `SrCallbackRow` (+ `contactNo`) | `code` |
| `POST /Report/GetReport04` | SR แยกตามหน่วยงาน (ไม่เลือก = ทุกหน่วยงาน) | `p_MainOrg`, `p_SubOrg` (ไม่บังคับ) | `SrCallbackRow` | `code` |
| `POST /Report/GetReport05` | จำนวน contact ต่อวัน ของทีม Claim | – | `ClaimDailyRow` | `contact_date` |
| `POST /Report/GetReport06` | จำนวน contact ต่อวัน ของเจ้าหน้าที่ที่ไม่ใช่ทีม Claim | – | `NonServiceDailyRow` | `contact_Date` |
| `POST /Report/GetReport07` | SR ของประเภทบริการที่เลือก | `p_Service` | `ServiceRequestRow` | `code` |

ข้อมูลทั่วไป:

| | |
|---|---|
| Method | `POST` ทุกตัว |
| Header | `Content-Type: application/json` |
| Auth | **ตอนนี้ยังไม่ต้องใช้ token** (`[Authorize]` ยังปิดอยู่) แต่ควรส่ง `Authorization: Bearer <token>` ไว้เลย เพราะจะเปิดภายหลัง |
| Backend (dev) | `http://localhost:5018` |
| URL จาก Next.js | `/api/backend/Report/GetReport01` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Request payload

### 2.1 Field ที่ทุกรายงานใช้

| Field | Type | บังคับ | ค่าเริ่มต้น | กติกา |
|---|---|---|---|---|
| `p_Start` | string | ✅ | – | วันเวลาเริ่ม `yyyy-MM-ddTHH:mm:ss` (วินาทีไม่บังคับ) |
| `p_Finish` | string | ✅ | – | วันเวลาสิ้นสุด รูปแบบเดียวกัน **รวมค่านี้ด้วย** (inclusive) |
| `pageNumber` | number | – | `1` | ต้อง `>= 1` |
| `pageSize` | number | – | `20` | ต้องอยู่ระหว่าง `1`–`200` |

ชื่อ field ไม่สนตัวพิมพ์เล็ก/ใหญ่ (`p_Start` หรือ `P_Start` ใช้ได้ทั้งคู่)

### 2.2 กติกาวันที่ (อ่านก่อนเขียนโค้ด)

- เป็น **เวลาไทย ไม่มี timezone**: `"2026-07-01T08:00:00"` หรือ `"2026-07-01T08:00"`
- ❌ ห้ามใช้ `date.toISOString()` เพราะได้เวลา UTC ที่ลงท้ายด้วย `Z` (เช่น `2026-07-01T01:00:00.000Z`) → API ตอบ 400
- ❌ ห้ามส่งแค่วันที่ (`"2026-07-01"`) หรือรูปแบบเก่า (`"07/01/2026"`) → 400
- ❌ ห้ามใช้ปี **พ.ศ.** ถ้า date picker ตั้ง locale ไทย ต้องแปลงเป็น ค.ศ. ก่อน (ปี `2569` ผ่าน validation แต่จะไม่เจอข้อมูลเลย)
- `p_Start` ต้องไม่มากกว่า `p_Finish`
- ค่าจาก `<input type="datetime-local">` (`"2026-07-01T08:00"`) ส่งได้ตรง ๆ
- **รายงาน 05 และ 06 ใช้แค่ส่วนวันที่** เวลาจะถูกตัดทิ้ง (นับทั้งวันของ `p_Start` ถึงทั้งวันของ `p_Finish`)

ถ้าจะเลือกทั้งเดือน ให้ส่ง `T00:00:00` ถึง `T23:59:59` ของวันสุดท้าย

### 2.3 ตัวอย่าง payload

**รายงาน 01, 02, 03, 05, 06** (ใช้แค่ field พื้นฐาน)

```json
{
  "p_Start": "2026-07-01T00:00:00",
  "p_Finish": "2026-07-31T23:59:59",
  "pageNumber": 1,
  "pageSize": 20
}
```

**รายงาน 04** (หน่วยงาน)

```json
{
  "p_Start": "2026-07-01T00:00:00",
  "p_Finish": "2026-07-31T23:59:59",
  "p_MainOrg": "00E071487E1343B987C548DEDAE71113",
  "p_SubOrg": "00E071487E1343B987C548DEDAE71267",
  "pageNumber": 1,
  "pageSize": 20
}
```

| Field | บังคับ | ความหมาย |
|---|---|---|
| `p_MainOrg` | – | `TblOrganization.Id` ของหน่วยงานหลัก |
| `p_SubOrg` | – | `TblOrganization.Id` ของหน่วยงานย่อย |

| ส่งอะไร | ได้ SR ของ |
|---|---|
| ไม่ส่งทั้งคู่ (หรือส่งค่าว่าง) | **ทุกหน่วยงาน** |
| `p_MainOrg` อย่างเดียว | หน่วยงานหลักนั้น และหน่วยงานย่อยทุกตัวที่อยู่ใต้มัน |
| `p_MainOrg` + `p_SubOrg` | หน่วยงานย่อยนั้นเท่านั้น (ต้องอยู่ใต้หน่วยงานหลักที่ส่งมา) |
| `p_SubOrg` อย่างเดียว | หน่วยงานย่อยนั้นเท่านั้น |

**รายงาน 07** (ประเภทบริการ)

```json
{
  "p_Start": "2026-07-01T00:00:00",
  "p_Finish": "2026-07-31T23:59:59",
  "p_Service": "0168B738100C4CBAB9AA45906AD4D8E5",
  "pageNumber": 1,
  "pageSize": 20
}
```

| Field | บังคับ | ความหมาย |
|---|---|---|
| `p_Service` | ✅ | `TblCategory.Id` ของประเภทบริการระดับบนสุด (Q&A, Claim, …) ตัวอย่างข้างบนคือ Q&A |

> ยังไม่มี endpoint สำหรับดึงรายการหน่วยงานหรือประเภทบริการมาทำ drop-down
> (`ListOfValues` มีแค่ `channels`) ระหว่างนี้ให้ขอ id จากทีม backend

---

## 3. Response

### 3.1 รูปแบบ (เหมือนกันทุกรายงาน) — HTTP 200

```json
{
  "status": "success",
  "message": "พบข้อมูล 135 รายการ",
  "data": [ /* แถวของรายงาน ดูข้อ 3.2 */ ],
  "meta": {
    "pagination": {
      "current_page": 1,
      "per_page": 20,
      "total": 135,
      "total_pages": 7
    }
  }
}
```

| Field | ความหมาย |
|---|---|
| `status` | `"success"` เสมอเมื่อได้ 200 |
| `message` | ข้อความภาษาไทย แสดงให้ผู้ใช้ดูได้: `"พบข้อมูล N รายการ"` หรือ `"ไม่พบข้อมูล"` |
| `data` | แถวของหน้าที่ขอ |
| `meta.pagination.total` | จำนวนแถวทั้งหมดทุกหน้า |
| `meta.pagination.total_pages` | จำนวนหน้าทั้งหมด (`0` ถ้าไม่มีข้อมูล) |

- **ไม่มีข้อมูลก็ยังเป็น 200** โดย `data: []`, `total: 0` และ `message: "ไม่พบข้อมูล"`
- ขอ `pageNumber` เกินจำนวนหน้า → 200 และ `data: []`
- key ใน `meta.pagination` เป็น **snake_case** (`current_page`) ส่วน key ใน `data` เป็น camelCase (ยกเว้นรายงาน 05)

### 3.2 แถวของแต่ละรายงาน

#### `ServiceRequestRow` — รายงาน 01, 07

```json
{
  "code": "SR-SAMPLE-0001",
  "summary": "สอบถามสิทธิ์",
  "detail": "รายละเอียดเรื่อง",
  "srReferenceLink": null,
  "srOpened": "2026-07-01T08:15:00",
  "srClosed": "",
  "srRequireCallBack": "Y",
  "created": "2026-07-01T08:15:30",
  "aNumber": "08xxxxxxxx",
  "channelName": "โทรศัพท์",
  "srTypeName": "Q&A",
  "srReference": "",
  "srStatusName": "ดำเนินการ",
  "createdUName": "agent01",
  "createrName": "นายตัวอย่าง ทดสอบ",
  "skillAgentCreated": 2,
  "ownerUName": "agent01",
  "ownerName": "นายตัวอย่าง ทดสอบ",
  "lastUpdatedUName": "agent02",
  "updateName": "นางสาวตัวอย่าง ทดสอบ",
  "subOrgId": "00E071487E1343B987C548DEDAE71267",
  "subOrgName": "หน่วยงานย่อย",
  "mainOrgId": "00E071487E1343B987C548DEDAE71113",
  "mainOrgName": "หน่วยงานหลัก",
  "contactName": "นายผู้ติดต่อ ตัวอย่าง",
  "gender": "ชาย",
  "remark": null
}
```

#### `SrCallbackRow` — รายงาน 02, 03, 04

เหมือน `ServiceRequestRow` แต่ **ชื่อ field บางตัวต่างกัน** และ **ไม่มี `gender`, `remark`**:

| รายงาน 01, 07 | รายงาน 02, 03, 04 |
|---|---|
| `createdUName` | `createdUname` |
| `createrName` | `creatorName` |
| `ownerUName` | `ownerUname` |
| `lastUpdatedUName` | `lastUpdatedUname` |
| `updateName` | `updaterName` |
| `gender`, `remark` | (ไม่มี) |

รายงาน 03 มี field เพิ่ม **`contactNo`**: เบอร์โทรจากบัญชีผู้ติดต่อ (มือถือก่อน แล้วบ้าน, ที่ทำงาน)
ตรงกับคอลัมน์ `Contact_No` ของรายงานเดิม รายงาน 02 และ 04 ไม่มี field นี้ใน JSON

ค่าที่ไม่มีข้อมูล: รายงาน 01/07 ส่วนใหญ่เป็น `""` ส่วนรายงาน 02/03/04 เป็น `null` → ให้เช็กทั้งสองแบบ (`value || "-"`)

ความหมาย field ที่ไม่ชัดจากชื่อ:

| Field | ความหมาย |
|---|---|
| `srOpened` / `srClosed` | วันเปิด/ปิด SR รูปแบบ `yyyy-MM-ddTHH:mm:ss` หรือ `""` ถ้ายังไม่ปิด |
| `created` | วันเวลาที่สร้าง SR (ISO ไม่มี timezone, อาจมีเศษวินาที) |
| `aNumber` | เบอร์โทรของผู้ติดต่อ |
| `srRequireCallBack` | ค่า CallBack ของ SR: `"Y"` = ต้องโทรกลับ, `"D"` = สายหลุด, `"N"` = ไม่ต้องการ (พบ `"E"` ด้วยแต่ยังไม่ทราบความหมาย) |
| `skillAgentCreated` | `1` = ผู้สร้างเป็นทีม Claim (แสดงข้อมูล), `2` = ไม่ใช่ (ปล่อยว่าง) |
| `subOrgId` / `mainOrgId` | หน่วยงานย่อยของ SR และหน่วยงานแม่ของมัน |

> แต่ละ SR อาจมีหลายแถวได้ ถ้า SR นั้นมีหลาย activity ที่เบอร์ติดต่อ (`aNumber`) ต่างกัน

#### `ClaimDailyRow` — รายงาน 05 (key เป็น snake_case)

```json
{
  "contact_date": "2026-07-01T00:00:00",
  "bad_signal": 3,
  "kidding_abnormal": 1,
  "kidding_child": 0,
  "kidding_rude": 2,
  "kidding_silent": 4,
  "flood": 0,
  "lost_connection": 5,
  "wrong_number_pizza": 1,
  "wrong_number_other": 2,
  "stress_social": 0,
  "stress_economic": 1,
  "stress_political": 0,
  "stress_legal": 0,
  "stress_natural_resources": 0,
  "consult_officer": 3,
  "total": 22
}
```

#### `NonServiceDailyRow` — รายงาน 06 (key เป็น `xxx_Yyy`)

ชื่อ key ของรายงาน 06 **ไม่เหมือนรายงาน 05** (ตัวหลัง `_` เป็นตัวใหญ่) และมียอดรวมกลุ่มเพิ่ม 2 ตัว

```json
{
  "contact_Date": "2026-07-01",
  "kidding_Abnormal": 1,
  "kidding_Child": 0,
  "kidding_Rude": 2,
  "kidding_Silent": 4,
  "flood": 0,
  "kidding_Total": 7,
  "bad_Signal": 3,
  "lost_Connection": 5,
  "wrong_Number_Pizza": 1,
  "wrong_Number_Other": 2,
  "consult_Officer": 3,
  "stress_Social": 0,
  "stress_Economic": 1,
  "stress_Political": 0,
  "stress_Legal": 0,
  "stress_Natural_Resources": 0,
  "out_Of_Scope_Total": 4,
  "total": 22
}
```

| คอลัมน์รายงาน | key รายงาน 05 | key รายงาน 06 |
|---|---|---|
| วันที่ | `contact_date` (`"2026-07-01T00:00:00"`) | `contact_Date` (`"2026-07-01"`) |
| Kidding: จิตไม่ปกติ / เด็กโทรเล่น / โทรด่าหยาบคาย / เสียงเงียบ / น้ำท่วม | `kidding_abnormal` / `kidding_child` / `kidding_rude` / `kidding_silent` / `flood` | `kidding_Abnormal` / `kidding_Child` / `kidding_Rude` / `kidding_Silent` / `flood` |
| รวม Kidding | – | `kidding_Total` |
| สัญญาณไม่ชัด / สายหลุด | `bad_signal` / `lost_connection` | `bad_Signal` / `lost_Connection` |
| โทรผิด: Pizza / อื่น ๆ | `wrong_number_pizza` / `wrong_number_other` | `wrong_Number_Pizza` / `wrong_Number_Other` |
| ขอคำปรึกษาเจ้าหน้าที่ | `consult_officer` | `consult_Officer` |
| ความเครียด: สังคม / เศรษฐกิจ / การเมือง / กฎหมาย / ทรัพยากร | `stress_social` … `stress_natural_resources` | `stress_Social` … `stress_Natural_Resources` |
| รวม Out of Scope | – | `out_Of_Scope_Total` |
| รวมทั้งวัน | `total` | `total` |

วันที่ไม่มี contact เลยจะ **ไม่มีแถว** (ไม่ได้ส่งแถวที่เป็น 0 มา) ถ้าต้องการแสดงทุกวันให้เติมเองฝั่ง frontend

---

## 4. Error

| HTTP | เมื่อไร | Body |
|---|---|---|
| 400 | ข้อมูลไม่ถูก: ไม่ส่งวันที่, รูปแบบผิด, start > finish, pageNumber/pageSize ผิด, ไม่ส่ง `p_Service` (รายงาน 07), JSON พัง | ProblemDetails (ด้านล่าง) |
| 401 / 403 | (หลังเปิด `[Authorize]`) ไม่มี token, token หมดอายุ, ไม่มีสิทธิ์ | ว่าง |
| 500 | ระบบผิดพลาด | `{ "statusCode": 500, "message": "เกิดข้อผิดพลาดภายในระบบ" }` |

**ตัวอย่าง 400**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "P_Start": ["P_Start ต้องอยู่ในรูปแบบ yyyy-MM-ddTHH:mm:ss"]
  },
  "traceId": "00-..."
}
```

- ข้อความใน `errors` เป็นภาษาไทย แสดงผู้ใช้ได้
- ปัญหาวันที่ **ทุกแบบ** (รวมถึง `p_Finish` ผิด) อยู่ใต้ key `P_Start` → ให้แสดงข้อความรวมของช่วงวันที่ อย่าผูกกับช่องใดช่องหนึ่ง
- key อื่นที่เป็นไปได้: `PageNumber`, `PageSize`, `P_Service`, `format` (export)
- ถ้า JSON พังหรือชนิดข้อมูลผิด (เช่น `pageNumber: "abc"`) ข้อความจะเป็นภาษาอังกฤษจาก framework และ key จะเป็นแบบ `$.pageNumber`

ข้อความ validation ที่เป็นไปได้:

| ข้อความ |
|---|
| `กรุณาระบุ P_Start และ P_Finish` |
| `P_Start ต้องอยู่ในรูปแบบ yyyy-MM-ddTHH:mm:ss` |
| `P_Finish ต้องอยู่ในรูปแบบ yyyy-MM-ddTHH:mm:ss` |
| `P_Start ต้องไม่มากกว่า P_Finish` |
| `PageNumber ต้องมากกว่าหรือเท่ากับ 1` |
| `PageSize ต้องอยู่ระหว่าง 1 ถึง 200` |
| `กรุณาระบุ P_Service` (รายงาน 07) |

---

## 5. ตัวอย่างโค้ด (TypeScript / Next.js)

### 5.1 Types

```ts
// types/report.ts
export interface ReportRequest {
  p_Start: string;   // "yyyy-MM-ddTHH:mm:ss" เวลาไทย
  p_Finish: string;
  pageNumber?: number; // default 1
  pageSize?: number;   // default 20, max 200
}
export interface Report04Request extends ReportRequest { p_MainOrg?: string; p_SubOrg?: string; }
export interface Report07Request extends ReportRequest { p_Service: string; }

export interface PagedEnvelope<T> {
  status: "success";
  message: string;
  data: T[];
  meta: { pagination: { current_page: number; per_page: number; total: number; total_pages: number } };
}

interface SrBase {
  code: string | null;
  summary: string | null;
  detail: string | null;
  srReferenceLink: string | null;
  srOpened: string | null;
  srClosed: string | null;
  srRequireCallBack: string | null;
  created: string | null;
  aNumber: string | null;
  channelName: string | null;
  srTypeName: string | null;
  srReference: string | null;
  srStatusName: string | null;
  skillAgentCreated: 1 | 2;
  ownerName: string | null;
  subOrgId: string | null;
  subOrgName: string | null;
  mainOrgId: string | null;
  mainOrgName: string | null;
  contactName: string | null;
}

/** รายงาน 01, 07 */
export interface ServiceRequestRow extends SrBase {
  createdUName: string | null;
  createrName: string | null;
  ownerUName: string | null;
  lastUpdatedUName: string | null;
  updateName: string | null;
  gender: string | null;
  remark: string | null;
}

/** รายงาน 02, 03, 04 */
export interface SrCallbackRow extends SrBase {
  createdUname: string | null;
  creatorName: string | null;
  ownerUname: string | null;
  lastUpdatedUname: string | null;
  updaterName: string | null;
  /** รายงาน 03 เท่านั้น */
  contactNo?: string;
}

/** รายงาน 05 */
export interface ClaimDailyRow {
  contact_date: string;
  bad_signal: number; kidding_abnormal: number; kidding_child: number; kidding_rude: number;
  kidding_silent: number; flood: number; lost_connection: number;
  wrong_number_pizza: number; wrong_number_other: number;
  stress_social: number; stress_economic: number; stress_political: number;
  stress_legal: number; stress_natural_resources: number;
  consult_officer: number; total: number;
}

/** รายงาน 06 */
export interface NonServiceDailyRow {
  contact_Date: string | null;
  kidding_Abnormal: number; kidding_Child: number; kidding_Rude: number; kidding_Silent: number;
  flood: number; kidding_Total: number;
  bad_Signal: number; lost_Connection: number;
  wrong_Number_Pizza: number; wrong_Number_Other: number;
  consult_Officer: number;
  stress_Social: number; stress_Economic: number; stress_Political: number;
  stress_Legal: number; stress_Natural_Resources: number;
  out_Of_Scope_Total: number; total: number;
}

export interface ReportMap {
  "01": { req: ReportRequest;   row: ServiceRequestRow };
  "02": { req: ReportRequest;   row: SrCallbackRow };
  "03": { req: ReportRequest;   row: SrCallbackRow };
  "04": { req: Report04Request; row: SrCallbackRow };
  "05": { req: ReportRequest;   row: ClaimDailyRow };
  "06": { req: ReportRequest;   row: NonServiceDailyRow };
  "07": { req: Report07Request; row: ServiceRequestRow };
}
```

### 5.2 แปลงวันที่ให้ถูกรูปแบบ

```ts
// lib/report/date.ts
/** Date → "yyyy-MM-ddTHH:mm:ss" ตามเวลาเครื่อง (ห้ามใช้ toISOString เพราะเป็น UTC) */
export function toApiDateTime(d: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}` +
         `T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}
// ถ้าใช้ dayjs: dayjs(d).format("YYYY-MM-DDTHH:mm:ss")  (ระวัง plugin buddhistEra ต้องเป็น ค.ศ.)
```

`toApiDateTime` ใช้เวลาตามเครื่องผู้ใช้ ซึ่งถูกต้องเมื่อเครื่องตั้งเวลาไทย

### 5.3 ฟังก์ชันเรียกรายงาน

```ts
// lib/report/client.ts
import type { PagedEnvelope, ReportMap } from "@/types/report";

export class ReportApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public fieldErrors?: Record<string, string[]>,
  ) { super(message); }
}

export async function fetchReport<K extends keyof ReportMap>(
  no: K,
  body: ReportMap[K]["req"],
  opts: { token?: string; signal?: AbortSignal } = {},
): Promise<PagedEnvelope<ReportMap[K]["row"]>> {
  const res = await fetch(`/api/backend/Report/GetReport${no}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(opts.token ? { Authorization: `Bearer ${opts.token}` } : {}),
    },
    body: JSON.stringify(body),
    signal: opts.signal,
    cache: "no-store",
  });

  if (res.ok) return res.json();

  const err = await res.json().catch(() => null);
  if (res.status === 400 && err?.errors) {
    const first = Object.values(err.errors as Record<string, string[]>).flat()[0];
    throw new ReportApiError(400, first ?? "ข้อมูลไม่ถูกต้อง", err.errors);
  }
  if (res.status === 401) throw new ReportApiError(401, "กรุณาเข้าสู่ระบบใหม่");
  if (res.status === 403) throw new ReportApiError(403, "ไม่มีสิทธิ์ดูรายงานนี้");
  throw new ReportApiError(res.status, err?.message ?? "เกิดข้อผิดพลาดภายในระบบ");
}
```

### 5.4 ตัวอย่างใช้งาน

```ts
const result = await fetchReport("07", {
  p_Start: "2026-07-01T00:00:00",
  p_Finish: "2026-07-31T23:59:59",
  p_Service: "0168B738100C4CBAB9AA45906AD4D8E5",
  pageNumber: 1,
  pageSize: 50,
});

result.data;                         // ServiceRequestRow[]
result.meta.pagination.total;        // ใช้กับ rowCount ของตาราง
result.message;                      // "พบข้อมูล 135 รายการ"
```

**ใช้กับ MUI DataGrid แบบแบ่งหน้าฝั่ง server**: DataGrid นับหน้าจาก `0` แต่ API นับจาก `1`

```tsx
<DataGrid
  rows={rows}
  rowCount={total}                         // meta.pagination.total
  paginationMode="server"
  paginationModel={paginationModel}        // { page: 0, pageSize: 20 }
  onPaginationModelChange={setPaginationModel}
  pageSizeOptions={[20, 50, 100, 200]}     // ไม่เกิน 200
  getRowId={(r) => `${r.code}-${r.aNumber}`} // SR เดียวอาจมีหลายแถว ใช้ code อย่างเดียวไม่ได้
/>

// ตอนเรียก API
fetchReport("01", { ...range, pageNumber: paginationModel.page + 1, pageSize: paginationModel.pageSize });
```

สำหรับรายงาน 05/06 ใช้ `contact_date` / `contact_Date` เป็น row id ได้ (วันละหนึ่งแถว)

---

## 6. ทดสอบเร็ว ๆ ด้วย curl

```bash
curl -X POST http://localhost:5018/Report/GetReport01 \
  -H "Content-Type: application/json" \
  -d '{"p_Start":"2026-07-01T00:00:00","p_Finish":"2026-07-31T23:59:59","pageNumber":1,"pageSize":5}'
```

หรือเปิด Swagger ที่ `http://localhost:5018/swagger` (เฉพาะ Development) และดูตัวอย่างเพิ่มใน
`ContactTogetherApi.http`

---

## 7. สรุปจุดที่มักพลาด

1. ส่งวันที่จาก `toISOString()` (มี `Z`) หรือเป็นปี พ.ศ.
2. ลืมว่า `pageNumber` เริ่มที่ 1 (DataGrid เริ่มที่ 0)
3. ใช้ชื่อ field ของรายงาน 01/07 กับรายงาน 02/03/04 (เช่น `createrName` ≠ `creatorName`)
4. ใช้ key ของรายงาน 05 กับรายงาน 06 (`bad_signal` ≠ `bad_Signal`)
5. คิดว่า "ไม่พบข้อมูล" เป็น error (จริง ๆ เป็น 200 และ `data: []`)
6. ใช้ `code` เป็น row id ในรายงาน SR (ซ้ำได้)
7. Export แล้วไม่ได้ตั้ง `proxyTimeout` ใน Next.js (ไฟล์ใหญ่ดาวน์โหลดไม่สำเร็จ)

---

## 8. Export PDF / Excel (`ExportReport01`–`07`)

ได้ข้อมูลชุดเดียวกับ `GetReportXX` **ทุกแถวในช่วงวันที่ (ไม่แบ่งหน้า)** เป็นไฟล์ หน้าตาตามรายงานของระบบเดิม

### 8.1 วิธีเรียก

```
POST /Report/ExportReport01?format=xlsx
POST /Report/ExportReport01?format=pdf
…
POST /Report/ExportReport07?format=pdf
```

| | |
|---|---|
| Query | `format` = `xlsx` หรือ `pdf` (บังคับ) |
| Body | **เหมือน `GetReportXX` ทุกอย่าง** (ข้อ 2) ส่ง `pageNumber`/`pageSize` มาก็ไม่มีผล |
| Auth | **ตอนนี้ยังไม่บังคับ** (ปิดไว้ชั่วคราวระหว่างทดสอบ) แต่ให้ส่ง `Authorization: Bearer <token>` ไว้เลย เพราะจะเปิดภายหลัง ถ้าไม่ส่ง token ช่อง "พิมพ์โดย" ในไฟล์จะเป็น `-` |
| ชื่อไฟล์ | `Report01_20260701-20260731.xlsx` (อยู่ใน header `Content-Disposition`) |

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ ไม่มีข้อมูลก็ได้ไฟล์ที่เขียนว่า "ไม่พบข้อมูล" | ไฟล์ (`application/pdf` หรือ `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`) |
| 400 | วันที่ผิด, `format` ผิด/ไม่ส่ง, รายงาน 07 ไม่ส่ง `p_Service` | ProblemDetails (ข้อ 4) key ที่เพิ่มคือ `format` |
| 401 | (หลังเปิด auth อีกครั้ง) ไม่มี token / token หมดอายุ / logout แล้ว | ว่าง |
| 429 | มีคน export อยู่พร้อมกันครบจำนวนแล้ว (ค่าเริ่มต้น 2 งาน) | `{ "statusCode": 429, "message": "มีการ Export อื่นกำลังทำงาน กรุณาลองใหม่อีกครั้ง" }` |
| 500 | ระบบผิดพลาด | `{ "statusCode": 500, "message": "เกิดข้อผิดพลาดภายในระบบ" }` |

### 8.2 ดาวน์โหลดจาก Next.js

เป็น `POST` จึงใช้ `<a href>` ตรง ๆ ไม่ได้ ต้องโหลดเป็น blob แล้วสั่งดาวน์โหลด:

```ts
// lib/report/download.ts
export async function downloadReport(
  no: keyof ReportMap,
  format: "pdf" | "xlsx",
  body: object,
  opts: { token: string; signal?: AbortSignal },
) {
  const res = await fetch(`/api/backend/Report/ExportReport${no}?format=${format}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${opts.token}` },
    body: JSON.stringify(body),
    signal: opts.signal, // ปุ่ม "ยกเลิก" → controller.abort() แล้ว server หยุดสร้างไฟล์
  });

  if (!res.ok) {
    const err = await res.json().catch(() => null);
    const message = err?.errors
      ? (Object.values(err.errors as Record<string, string[]>).flat()[0] ?? "ข้อมูลไม่ถูกต้อง")
      : res.status === 401 ? "กรุณาเข้าสู่ระบบใหม่"
      : (err?.message ?? "Export ไม่สำเร็จ");
    throw new ReportApiError(res.status, message, err?.errors);
  }

  const disposition = res.headers.get("Content-Disposition") ?? "";
  const name = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? `Report${no}.${format}`;
  const url = URL.createObjectURL(await res.blob());
  const a = Object.assign(document.createElement("a"), { href: url, download: name });
  a.click();
  URL.revokeObjectURL(url);
}
```

UI ที่แนะนำ: ปุ่ม "Export Excel" / "Export PDF" ข้างปุ่มค้นหา ใช้เงื่อนไขเดียวกับที่ค้นหาอยู่ ระหว่างรอให้
ปิดปุ่ม แสดง "กำลังสร้างไฟล์…" และมีปุ่มยกเลิก ถ้าได้ 429 ให้แสดงข้อความจาก `message`

### 8.3 ต้องตั้ง `proxyTimeout` ใน Next.js

proxy ของ `rewrites()` ตัดการเชื่อมต่อเมื่อ backend เงียบเกิน **30 วินาที** (ค่าเริ่มต้นของ Next.js)
PDF ช่วงวันที่กว้างใช้เวลานานกว่านั้น จึงต้องเพิ่มใน `next.config.ts`:

```ts
const nextConfig: NextConfig = {
  experimental: {
    proxyTimeout: 600_000, // 10 นาที
  },
  async rewrites() { /* เดิม */ },
};
```

### 8.4 เวลาที่ใช้โดยประมาณ

วัดกับข้อมูลจำลองรายงาน 01 (ข้อความยาวพอ ๆ กับข้อมูลจริง):

| ไฟล์ | จำนวนแถว | เวลา | หน่วยความจำ server สูงสุด |
|---|---|---|---|
| Excel | 100,000 | ~2 วินาที | ~80 MB |
| PDF | 10,000 (~1,000 หน้า A3) | ~16 วินาที | ~350 MB |
| PDF | 60,000 (~6,000 หน้า) | ~75 วินาที | ~1.5 GB |

ช่วงวันที่กว้างมาก (หลายหมื่นแถว) **แนะนำให้ใช้ Excel** ซึ่งเร็วและเบากว่ามาก

### 8.5 หน้าตาไฟล์

- **PDF**: A3 แนวนอน (รายงาน 01–04, 07) / A4 แนวนอน (05, 06) ฟอนต์ Sarabun หัวรายงาน หัวตาราง และเลขหน้า
  "หน้าที่ X / Y" อยู่ทุกหน้า แถวไม่ถูกตัดข้ามหน้า (ยกเว้นแถวที่ยาวเกินหนึ่งหน้า) ค้นหาและ copy ข้อความไทยได้
- **Excel**: หัวรายงาน ช่วงวันที่ "พิมพ์โดย" แล้วตามด้วยตาราง หัวตาราง freeze ไว้และเปิด filter
  วันที่เป็นวันที่จริงของ Excel (เรียง/กรองได้) เบอร์โทรและเลข SR เป็น text เกิน 1,048,576 แถวจะขึ้น sheet ใหม่
- รายงาน 05/06 มีแถว **รวม** ท้ายตาราง (Excel เป็นสูตร `SUM`)
- วันที่ในตารางใช้รูปแบบของระบบเดิม: SR = `1/7/2026 10:58:00` (วัน/เดือน/ปี), 05 = `24 ก.พ. 57`, 06 = `10 - Feb - 2557`

### 8.6 Payload ของแต่ละรายงาน

ทุกตัวส่ง header เหมือนกัน (`Authorization` ยังไม่บังคับตอนนี้ แต่ส่งไว้เลย):

```
Authorization: Bearer <token>
Content-Type: application/json
```

`format` อยู่ใน query string (`xlsx` หรือ `pdf`) ไม่ได้อยู่ใน body

| รายงาน | Endpoint | Body | ชื่อไฟล์ที่ได้ (ตัวอย่าง) |
|---|---|---|---|
| 01 SR ทุกสังกัด | `POST /Report/ExportReport01?format=xlsx` | `p_Start`, `p_Finish` | `Report01_20260701-20260731.xlsx` |
| 02 ประเภทสายหลุด | `POST /Report/ExportReport02?format=xlsx` | `p_Start`, `p_Finish` | `Report02_20260701-20260731.xlsx` |
| 03 Require CallBack | `POST /Report/ExportReport03?format=xlsx` | `p_Start`, `p_Finish` | `Report03_20260701-20260731.xlsx` |
| 04 แยกตามหน่วยงาน | `POST /Report/ExportReport04?format=xlsx` | `p_Start`, `p_Finish`, `p_MainOrg`?, `p_SubOrg`? | `Report04_20260701-20260731.xlsx` |
| 05 Non-Service (Claim) | `POST /Report/ExportReport05?format=xlsx` | `p_Start`, `p_Finish` (ใช้แค่วันที่) | `Report05_20260701-20260731.xlsx` |
| 06 Non-Service (Information) | `POST /Report/ExportReport06?format=xlsx` | `p_Start`, `p_Finish` (ใช้แค่วันที่) | `Report06_20260701-20260731.xlsx` |
| 07 แยกตามประเภทบริการ | `POST /Report/ExportReport07?format=xlsx` | `p_Start`, `p_Finish`, **`p_Service`** | `Report07_20260701-20260731.xlsx` |

`?` = ไม่บังคับ, **ตัวหนา** = บังคับเพิ่มจาก `p_Start`/`p_Finish` · เปลี่ยน `xlsx` เป็น `pdf` ได้ทุกตัว

**รายงาน 01, 02, 03**

```http
POST /api/backend/Report/ExportReport01?format=xlsx
Authorization: Bearer <token>
Content-Type: application/json

{
  "p_Start": "2026-07-01T00:00:00",
  "p_Finish": "2026-07-31T23:59:59"
}
```

เปลี่ยนแค่ `ExportReport01` เป็น `ExportReport02` หรือ `ExportReport03` body เหมือนกัน

**รายงาน 04** (หน่วยงานไม่บังคับ ดูความหมายในข้อ 2.3)

```http
POST /api/backend/Report/ExportReport04?format=pdf
Authorization: Bearer <token>
Content-Type: application/json

{
  "p_Start": "2026-07-01T00:00:00",
  "p_Finish": "2026-07-31T23:59:59",
  "p_MainOrg": "00D1DA8C956043D5AD9B38299566A7F0",
  "p_SubOrg": "0D26AFEE79DD44C680D3E2F606B5C52A"
}
```

| ต้องการ | ส่ง | บรรทัดใต้หัวรายงานในไฟล์ |
|---|---|---|
| ทุกหน่วยงาน | ไม่ส่ง `p_MainOrg` / `p_SubOrg` | `หน่วยงาน: ทุกหน่วยงาน` |
| หน่วยงานหลัก + หน่วยงานย่อยทั้งหมดของมัน | `p_MainOrg` อย่างเดียว | `หน่วยงาน: <ชื่อหลัก> และหน่วยงานรอง` |
| หน่วยงานย่อยตัวเดียว | `p_MainOrg` + `p_SubOrg` | `หน่วยงาน: <ชื่อหลัก> / <ชื่อรอง>` |

**รายงาน 05, 06** (นับรายวัน เวลาใน `p_Start`/`p_Finish` ไม่มีผล)

```http
POST /api/backend/Report/ExportReport05?format=xlsx
Authorization: Bearer <token>
Content-Type: application/json

{
  "p_Start": "2026-07-01T00:00:00",
  "p_Finish": "2026-07-31T23:59:59"
}
```

เปลี่ยนเป็น `ExportReport06` ได้ body เหมือนกัน

**รายงาน 07** (`p_Service` บังคับ)

```http
POST /api/backend/Report/ExportReport07?format=pdf
Authorization: Bearer <token>
Content-Type: application/json

{
  "p_Start": "2026-07-01T00:00:00",
  "p_Finish": "2026-07-31T23:59:59",
  "p_Service": "0168B738100C4CBAB9AA45906AD4D8E5"
}
```

`p_Service` = `TblCategory.Id` ของประเภทบริการระดับบนสุด ตัวอย่างข้างบนคือ Q&A

**ใช้กับ `downloadReport()` (ข้อ 8.2)**

```ts
const range = { p_Start: "2026-07-01T00:00:00", p_Finish: "2026-07-31T23:59:59" };

await downloadReport("01", "xlsx", range, { token });
await downloadReport("04", "pdf", { ...range, p_MainOrg: mainOrgId }, { token });   // ไม่เลือก = ไม่ใส่ p_MainOrg
await downloadReport("07", "pdf", { ...range, p_Service: serviceId }, { token });
```

ใช้ body ชุดเดียวกับที่ส่งให้ `GetReportXX` ตอนค้นหาได้เลย (`pageNumber` / `pageSize` ที่ติดมาจะถูกข้าม)
