# รายละเอียดเรื่อง (`GET /Services/GetServiceById`) สำหรับ Frontend

ใช้ดึง **เรื่อง (service request) หนึ่งเรื่อง พร้อมข้อมูลผู้ติดต่อของเรื่อง** มาแสดงในหน้ารายละเอียดเรื่อง เรียกครั้งเดียวได้ข้อมูลของ 2 การ์ด:

- **"ข้อมูลผู้ติดต่อ":** ได้จาก object `account`
- **"ประวัติการให้บริการ":** ได้จาก field อื่นทั้งหมด และใช้เติมค่าในฟอร์มแก้ไขได้

หน้านี้ใช้แทน `ViewService.aspx?ServiceID=…` ของระบบเดิม

> อ้างอิงโค้ด: `Controllers/ServicesController.cs` (`GetServiceById`), `Dtos/ServiceDetailResponse.cs`
> (สืบทอดจาก `Dtos/ServiceResponse.cs`), `Dtos/LookupItemResponse.cs`, `Dtos/AccountSearchResponse.cs`
>
> **เปลี่ยนแปลง (18 พ.ย. 2026):** เพิ่ม `account` (ข้อมูลผู้ติดต่อทั้งก้อน) ส่วน field เดิมไม่เปลี่ยน

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | `id` (บังคับ) = `TblService.Id` hex 32 ตัว ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| Backend (dev) | `http://localhost:5018/Services/GetServiceById?id=…` |
| URL จาก Next.js | `/api/backend/Services/GetServiceById?id=…` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

**ได้ `id` มาจากไหน**

- **จากหน้าค้นหา:** ใช้ `serviceId` ของแถวใน `GET /Services/SearchService`
- **หลังเปิดเรื่องใหม่:** ใช้ `id` ใน response ของ `POST /Services/AddNewService`
- **ห้ามใช้ `code` (เช่น `69080400002`):** จะได้ 404 ถ้า URL ของหน้าต้องการให้คนอ่านรู้เรื่อง ให้ใช้ `id` ตามแบบระบบเดิม เช่น `/services/view?serviceId=427D66AA…`

---

## 2. Response

### 2.1 Field

200 ตอบเป็น **object เดียว** (ไม่มี envelope) ประกอบด้วยสองส่วน:

- **id ทุกตัวแบบเดียวกับ response ของ `AddNewService`:** ใช้เติมค่าในฟอร์มแก้ไข
- **ชื่อที่แปลงจาก id แล้ว:** ใช้แสดงผล ถ้า id ว่างหรือหาแถวไม่เจอ ชื่อนั้นเป็น `null`

| ส่วนในหน้า | Field ที่ใช้แสดง | id (สำหรับฟอร์มแก้ไข) |
|---|---|---|
| เลขที่เรื่อง | `code` | `id` |
| หัวข้อเรื่อง | `serviceTitleText` | `serviceTitleId` |
| ผู้รับผิดชอบ | `ownerName` | `ownerId` |
| สถานะ | `statusNameTh` (`statusNameEn`) และใช้ `statusIsType` ตัดสินว่าปิดหรือยกเลิกแล้ว | `statusId` |
| ประเภทบริการ | `categoryPath` (ดู 2.2) | `categoryId` (ชั้นล่างสุด) |
| หน่วยงาน | `organizationPath` (ดู 2.2) | `organizationId` (ชั้นล่างสุด) |
| ช่องทางรับเรื่อง / ตอบกลับ | `channelIncomingName` / `channelOutgoingName` | `channelIncomingId` / `channelOutgoingId` |
| ติดต่อกลับ | `callBack` (รหัส ดู 2.4) | `callBack` |
| ที่มา (อ้างอิง) | `serviceReferenceName` | `serviceReference` |
| ลิงก์อ้างอิง | `serviceReferenceLink` | |
| ระดับความรุนแรง / ความสำคัญ / ชั้นความลับ | `levelSeverityName` / `levelPriorityName` / `levelSecretName` | `levelSeverityId` / `levelPriorityId` / `levelSecretId` |
| ชื่อผู้ติดต่อ (แบบย่อ) | `accountFullName` (เท่ากับ `account.fullName`) | `accountId` |
| **การ์ดข้อมูลผู้ติดต่อ** | `account` (ดู 2.3) | `account.id` |
| เรื่องย่อ / รายละเอียด / หมายเหตุ | `summary` / `detail` / `remark` | |
| วันที่เปิด / ปิดเรื่อง | `dateOpened` / `dateClosed` | |
| ผู้บันทึก / ผู้แก้ไขล่าสุด | `createdByName` / `updatedByName` และ `created` / `updated` | `createdBy` / `updatedBy` |
| อื่น ๆ | `serviceArea`, `areaId` (เก็บไว้ pre-fill ฟอร์ม ไม่ได้แปลงเป็นชื่อให้), `onScene`, `isEnable` | |

- **วันที่ทุก field:** เป็นเวลาไทย ไม่มี offset เช่น `2026-08-04T12:00:00`
- **ช่องที่ว่าง:** อาจมาเป็น `""` หรือ `null` ก็ได้ เพราะข้อมูลเก่าเก็บไว้ไม่เหมือนกัน ให้เช็กทั้งสองแบบ
- **`statusIsType`:** มีค่า `OPEN`, `PENDING`, `CLOSE`, `CANCEL` อย่าตัดสินจากชื่อสถานะ

### 2.2 `categoryPath` / `organizationPath`

เป็น array ของ `{ id, nameTh }` **เรียงจากชั้นบนลงล่าง** ตัวสุดท้ายคือค่าที่เรื่องเก็บไว้จริง (`categoryId` / `organizationId`)

- **ประเภทบริการ:** ลึก 1–4 ชั้น
- **หน่วยงาน:** 2 ชั้น (กระทรวง > กรม)
- **ถ้าเรื่องไม่มีค่า หรือหา id ไม่เจอ:** ได้ `[]`

```ts
const categoryText = detail.categoryPath.map((x) => x.nameTh).join(" > ");
// "Digital Wallet > การร้องเรียน > ประเด็นการฉ้อโกง"
```

ฟอร์มแก้ไขที่เป็น drop-down เลือกต่อกันหลายชั้น ใช้ `categoryPath[i].id` เป็นค่าเริ่มต้นของชั้นที่ `i` ได้

### 2.3 `account` (ข้อมูลผู้ติดต่อ)

ข้อมูลของผู้ติดต่อ (`TblAccount`) ที่ `accountId` ชี้ไป **รูปแบบเดียวกับแต่ละแถวของ `GET /Account/GetAccounts`**
ใช้ type เดียวกันได้ ถ้าเรื่องไม่มีผู้ติดต่อ หรือหาผู้ติดต่อไม่เจอ ได้ `account: null`

| ช่องในการ์ด | Field |
|---|---|
| ชื่อ-นามสกุล | `fullName` (คำนำหน้า ชื่อ นามสกุล ภาษาไทย) หรือแยกเป็น `salutationTh` / `firstnameTh` / `lastnameTh` และ `…En` |
| เพศ | `gender` (ชื่อ) / `genderId` |
| มือถือ / โทรศัพท์ / อีเมล | `contacts` แยกตาม `detailType` (ดูด้านล่าง) |
| ที่อยู่ | `address` + `zipcode` (ข้อความอิสระ) |
| วันเกิด | `birthdate` เป็นข้อความรูปแบบเดิม เดือนขึ้นก่อน เช่น `5/19/2026 0:00` อาจเป็น `""` |
| หมายเหตุ | `remark` |
| พื้นที่ | `areaId` และ `areaPath` (จังหวัด → อำเภอ → ตำบล) ดู [AreaId.md](AreaId.md) |
| อื่น ๆ | `id`, `isScret`, `created` (ข้อความรูปแบบเดิม เช่น `05/28/2026 11:06`) |

`contacts` คือรายการ `{ id, detailType, detail }` (`id` ใช้แก้/ลบด้วย [AccountDetail.md](AccountDetail.md)) เฉพาะที่เปิดใช้งาน เรียงตาม `detailType` คนหนึ่งมีได้หลายเบอร์

| `detailType` | ความหมาย |
|---|---|
| `MOBILE` | มือถือ |
| `HOME` | โทรศัพท์บ้าน |
| `OFFICE` | โทรศัพท์ที่ทำงาน |
| `FAX` | แฟกซ์ |
| `MAIL` | อีเมล |

```ts
const pick = (type: string) =>
  detail.account?.contacts.filter((c) => c.detailType === type).map((c) => c.detail).join(", ") || "-";
const mobile = pick("MOBILE");
const phone = [pick("HOME"), pick("OFFICE")].filter((x) => x !== "-").join(", ") || "-";
const email = pick("MAIL");
```

### 2.4 `callBack`

| รหัส | ความหมาย |
|---|---|
| `Y` | ต้องการให้ติดต่อกลับ |
| `N` | ไม่ต้องการ |
| `D` | สายหลุด |
| `E` | แก้ไข |

### 2.5 ตัวอย่าง (ข้อมูลจริง ณ 18 พ.ย. 2026)

`GET /Services/GetServiceById?id=427D66AA13734763AC1D99775554659B`

```json
{
  "id": "427D66AA13734763AC1D99775554659B",
  "code": "69080400002",
  "serviceTitleId": "81EE380A-96E2-F111-8E93-96F1F7A12F5E",
  "serviceTitleText": "การเมือง-การปกครอง",
  "statusId": "19C49E66B6F9EF1BCE11A3B93DED12C8",
  "statusNameTh": "อยู่ระหว่างดำเนินการ",
  "statusNameEn": "Pending",
  "statusIsType": "PENDING",
  "ownerId": "505981F1B7534D56BF4BF2F159322184",
  "ownerName": "นาย กันตณัฐิ์ ประกอบศุขราษฎร์",
  "categoryId": "0168B738100C4CBAB9AA45906AD4D810",
  "categoryPath": [
    { "id": "0168B738100C4CBAB9AA45906AD4D810", "nameTh": "Claim" }
  ],
  "organizationId": "00E071487E1343B987C548DEDAE71264",
  "organizationPath": [
    { "id": "00E071487E1343B987C548DEDAE71113", "nameTh": "กระทรวงการคลัง" },
    { "id": "00E071487E1343B987C548DEDAE71264", "nameTh": "กรมบัญชีกลาง" }
  ],
  "channelIncomingId": "332E6D76F258C8294B160C20642AB9F5",
  "channelIncomingName": "Phone",
  "channelOutgoingId": "",
  "channelOutgoingName": null,
  "callBack": "N",
  "serviceReference": "",
  "serviceReferenceName": null,
  "serviceReferenceLink": "",
  "levelSeverityId": "2B38BAE2A423C58915F7F2FA41982A50",
  "levelSeverityName": "ปกติ",
  "levelPriorityId": "D550B0AAB78CEEE3E6C027E477932A35",
  "levelPriorityName": "3-Medium",
  "levelSecretId": "859E3EA3430F667B055476057E6993A5",
  "levelSecretName": "ปกติ",
  "accountId": "F05AB03D7CF74CEFA75504047FD52343",
  "accountFullName": "นาย อิน ทดสอบ",
  "account": {
    "id": "F05AB03D7CF74CEFA75504047FD52343",
    "salutationTh": "นาย", "firstnameTh": "อิน", "lastnameTh": "ทดสอบ",
    "salutationEn": "", "firstnameEn": "", "lastnameEn": "",
    "fullName": "นาย อิน ทดสอบ",
    "genderId": "C833A9E6D354B2AC350A50CFB41852A8",
    "gender": "ชาย",
    "birthdate": "5/19/2026 0:00",
    "address": "456/456",
    "areaId": "16AC86F1FC2255923683F632CAC454DA",
    "areaPath": [
      { "id": "5EF900A3F3430FB51A813C37BA3D2BB4", "refId": "5EF900A3F3430FB51A813C37BA3D2BB4", "nameTh": "กรุงเทพมหานคร", "nameEn": null, "areaType": "PROVINCE", "zipcode": "10000", "isDefault": "F" },
      { "id": "4F3A0B8A4272BFEB369CB62889860FC9", "refId": "5EF900A3F3430FB51A813C37BA3D2BB4", "nameTh": "หลักสี่", "nameEn": null, "areaType": "DISTRICT", "zipcode": "10210", "isDefault": "F" },
      { "id": "16AC86F1FC2255923683F632CAC454DA", "refId": "4F3A0B8A4272BFEB369CB62889860FC9", "nameTh": "ตลาดบางเขน", "nameEn": null, "areaType": "SUBDISTRICT", "zipcode": null, "isDefault": "F" }
    ],
    "zipcode": "10210",
    "remark": "",
    "isScret": "F",
    "isEnable": "T",
    "created": "05/28/2026 11:06",
    "contacts": [
      { "id": "<id>", "detailType": "MOBILE", "detail": "0934153699" }
    ]
  },
  "summary": "ร้องเรียน โยนเรื่อง",
  "detail": "ร้องเรียน โยนเรื่อง",
  "remark": "",
  "serviceArea": "",
  "areaId": "",
  "onScene": null,
  "dateOpened": "2026-08-04T12:00:00",
  "dateClosed": null,
  "isEnable": "T",
  "created": "2026-08-04T12:02:00",
  "createdBy": "04EFF8A54A56F4D240E48E0520C223DB",
  "createdByName": "นางสาว นิภาพร รัตธนภาส",
  "updated": "2026-08-04T12:05:00",
  "updatedBy": "04EFF8A54A56F4D240E48E0520C223DB",
  "updatedByName": "นางสาว นิภาพร รัตธนภาส"
}
```

ลำดับ field ใน JSON จริงอาจไม่ตรงกับตัวอย่าง อย่าพึ่งลำดับ

### 2.6 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | พบเรื่อง | `ServiceDetail` |
| 400 | ไม่ส่ง `id` หรือส่งค่าว่าง | ProblemDetails (`errors.id`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 404 | ไม่มีเรื่องที่ `id` นี้ หรือเรื่องถูกปิดใช้งาน (`IsEnable` ไม่ใช่ `T`) | `{ "statusCode": 404, "message": "ไม่พบข้อมูลเรื่อง" }` |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/services.ts
export interface LookupItem { id: string; nameTh: string; }

// รูปแบบเดียวกับแถวของ GET /Account/GetAccounts ถ้ามี type นี้อยู่แล้วใช้ตัวเดิมได้เลย
export interface AccountContact { id: string; detailType: "MOBILE" | "HOME" | "OFFICE" | "FAX" | "MAIL" | string; detail: string; }
export interface AccountSearch {
  id: string;
  salutationTh: string | null; firstnameTh: string | null; lastnameTh: string | null;
  salutationEn: string | null; firstnameEn: string | null; lastnameEn: string | null;
  fullName: string;
  genderId: string | null; gender: string | null;
  birthdate: string | null; address: string | null; areaId: string | null; zipcode: string | null;
  areaPath: AreaItem[] | null; // AreaItem จาก AreaId.md
  remark: string | null; isScret: string; isEnable: string | null; created: string | null;
  contacts: AccountContact[];
}

export interface ServiceDetail {
  id: string;
  code: string;
  serviceTitleId: string | null;  serviceTitleText: string | null;
  statusId: string | null;        statusNameTh: string | null; statusNameEn: string | null;
  statusIsType: "OPEN" | "PENDING" | "CLOSE" | "CANCEL" | string | null;
  ownerId: string | null;         ownerName: string | null;
  categoryId: string | null;      categoryPath: LookupItem[];
  organizationId: string | null;  organizationPath: LookupItem[];
  channelIncomingId: string | null; channelIncomingName: string | null;
  channelOutgoingId: string | null; channelOutgoingName: string | null;
  callBack: "Y" | "N" | "D" | "E" | string | null;
  serviceReference: string | null; serviceReferenceName: string | null; serviceReferenceLink: string | null;
  levelSeverityId: string | null; levelSeverityName: string | null;
  levelPriorityId: string | null; levelPriorityName: string | null;
  levelSecretId: string | null;   levelSecretName: string | null;
  accountId: string | null;       accountFullName: string | null;
  account: AccountSearch | null;
  summary: string | null; detail: string | null; remark: string | null;
  serviceArea: string | null; areaId: string | null; onScene: string | null;
  dateOpened: string | null; dateClosed: string | null; isEnable: string | null;
  created: string | null; createdBy: string | null; createdByName: string | null;
  updated: string | null; updatedBy: string | null; updatedByName: string | null;
}

export async function fetchServiceById(
  id: string,
  opts: { token: string; signal?: AbortSignal },
): Promise<ServiceDetail | null> {
  const res = await fetch(`/api/backend/Services/GetServiceById?id=${encodeURIComponent(id)}`, {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 404) return null; // แสดง "ไม่พบข้อมูลเรื่อง"
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("โหลดรายละเอียดเรื่องไม่สำเร็จ");
}
```

```ts
export const CALL_BACK_TEXT: Record<string, string> = {
  Y: "ต้องการให้ติดต่อกลับ",
  N: "ไม่ต้องการ",
  D: "สายหลุด",
  E: "แก้ไข",
};

const pathText = (path: LookupItem[]) => path.map((x) => x.nameTh).join(" > ") || "-";
const isFinished = detail.statusIsType === "CLOSE" || detail.statusIsType === "CANCEL";
```

---

## 4. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/Services/GetServiceById?id=427D66AA13734763AC1D99775554659B
Authorization: Bearer <token>
```

```bash
curl "http://localhost:5018/Services/GetServiceById?id=427D66AA13734763AC1D99775554659B" -H "Authorization: Bearer <token>"
```

backend ในเครื่อง dev ต้อง restart แอปก่อน ถ้ายังเป็น build เก่าจะได้ 404 ทุก `id`

---

## 5. ข้อควรรู้ (ณ 18 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ข้อมูลผู้ติดต่อ** | ได้ครบใน `account` แล้ว ไม่ต้องเรียก API อื่นเพิ่ม |
| **เรื่องที่ไม่มีผู้ติดต่อ** | บางเรื่องเก่ามี `accountId` เป็น `""` หรือชี้ไปหาผู้ติดต่อที่ไม่มีอยู่ (4 เรื่อง) ได้ `account: null` และ `accountFullName: null` ให้การ์ดแสดง "-" ทุกช่อง |
| **ผู้ติดต่อที่ถูกปิดใช้งาน** | ยังแสดงใน `account` ตามปกติ เพื่อให้เรื่องเก่ายังเห็นว่าใครติดต่อเข้ามา |
| **ปุ่ม "ดูประวัติผู้ติดต่อ"** | ยังไม่มี endpoint ดึงรายการเรื่องของผู้ติดต่อคนนี้ |
| **พื้นที่ (ผู้ติดต่อ)** | ใช้ `account.areaPath` ได้เลย ไม่ต้องแปลง `areaId` เอง ([AreaId.md](AreaId.md)) เรื่อง (service) ไม่ได้คืน `areaPath` ให้ มีแค่ `areaId` ดิบ |
| **ประวัติการปฏิบัติงาน (activity)** | ยังไม่มี endpoint ดึงรายการ เพิ่ม แก้ หรือลบงาน |
| **ปุ่มแก้ไข / ลบเรื่อง** | ยังไม่มี endpoint แก้ไขหรือลบเรื่อง |
| **ใช้ข้อมูลชุดเดียวกับ `AddNewService`** | field id และวันที่ตรงกับ response ของ `AddNewService` ทุกตัว ใช้ type ร่วมกันได้ |
