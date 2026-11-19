# พื้นที่ (`areaId`) และรายการพื้นที่ (`GET /ListOfValues/GetArea`) สำหรับ Frontend

> อัปเดต 19 พ.ย. 2026: ตาราง `TblArea` มีข้อมูลแล้ว (ย้ายมาจากระบบเดิม 8,437 แถว) จึงเลือกและบันทึก `areaId` ได้
> เอกสารฉบับเดิม (17 พ.ย.) ที่บอกว่าตารางว่างและบันทึกไม่ได้ ใช้ไม่ได้แล้ว

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetArea`), `Dtos/AreaResponse.cs`, `Helper/AreaTypes.cs`

---

## 1. สรุปสั้น

- `areaId` คือ id ของ **จังหวัด อำเภอ หรือตำบล** ในตาราง `TblArea` ใช้กับ:
  - ผู้ติดต่อ: `POST /Account/AddNewAccount`, `PUT /Account/UpdateAccountById`
  - เรื่อง: `POST /Services/AddNewService`
- ดึงรายการด้วย `GET /ListOfValues/GetArea` ทีละชั้น ทำ drop-down 3 ตัวต่อกัน: จังหวัด → อำเภอ → ตำบล
- ส่ง id ที่ไม่มีใน `TblArea` = 400 (`errors.AreaId`)
- **คนละชุดกับ** `GetProvince` / `GetDistrict` / `GetSubdistrict` / `GetAddressDataset` (ตาราง `TblProvince` ฯลฯ ใช้กรอกที่อยู่)
  id จากชุดนั้นส่งเป็น `areaId` ไม่ได้

## 2. ข้อมูลใน `TblArea`

ข้อมูล 3 ชั้น เชื่อมกันด้วย `refId` (ชี้ไปที่ชั้นบน)

| `areaType` | ความหมาย | จำนวน | `refId` ชี้ไปที่ | `zipcode` |
|---|---|---|---|---|
| `PROVINCE` | จังหวัด | 79 | **ตัวเอง** | มีเกือบทุกจังหวัด |
| `DISTRICT` | อำเภอ / เขต | 928 | จังหวัด | มี |
| `SUBDISTRICT` | ตำบล / แขวง | 7,430 | อำเภอ | ว่าง (ใช้ของอำเภอ) |

- **แถว "ไม่ระบุ":** แต่ละชั้นมี 1 แถวที่ `isDefault = "T"` คือ `--ไม่ระบุจังหวัด--` → `--ไม่ระบุอำเภอ--` → `--ไม่ระบุตำบล--` ต่อกันเป็นสาย endpoint ส่งแถวนี้มาเป็นแถวแรกเสมอ
- **ทุกแถวเปิดใช้งาน** และไม่มีชื่อภาษาอังกฤษ (`nameEn` เป็น `null`)
- **ชื่อซ้ำ:** อำเภอ "สูงเนิน" มี 2 แถวในจังหวัดเดียวกัน (ข้อมูลเดิม) ใช้ `id` เป็นค่าที่ส่ง ไม่ใช่ชื่อ

## 3. `GET /ListOfValues/GetArea`

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` |
| Query | `areaType` (**บังคับ**) = `PROVINCE` / `DISTRICT` / `SUBDISTRICT` ตัวพิมพ์เล็กหรือใหญ่ก็ได้ |
| | `refId` (ไม่บังคับ) = id ของชั้นบน เพื่อเอาเฉพาะลูกของมัน |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetArea?areaType=PROVINCE` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetArea?areaType=PROVINCE` |

**ลำดับการเรียก**

| drop-down | เรียก | ได้ |
|---|---|---|
| จังหวัด | `GetArea?areaType=PROVINCE` | 79 แถว |
| อำเภอ | `GetArea?areaType=DISTRICT&refId=<id จังหวัด>` | อำเภอของจังหวัดนั้น |
| ตำบล | `GetArea?areaType=SUBDISTRICT&refId=<id อำเภอ>` | ตำบลของอำเภอนั้น |

ไม่ส่ง `refId` = ได้ทุกแถวของชั้นนั้น (อำเภอ 928, ตำบล 7,430) ใช้ได้ แต่สำหรับ drop-down ควรส่ง `refId`

### 3.1 Response (200)

array เรียงให้แถว "ไม่ระบุ" ขึ้นก่อน แล้วตามชื่อ ไม่พบ = `[]` ตัวอย่าง: อำเภอของจังหวัดนครนายก (ตัดมาแถวเดียว ชื่ออำเภอเมืองเก็บเป็น "เมือง" ทุกจังหวัด)

```json
[
  { "id": "49B4AB5B3D754BA2883582DDAB76AF46", "refId": "81DEE3BDDF62528B9DEB9C1C6D74CEC4", "nameTh": "เมือง", "nameEn": null, "areaType": "DISTRICT", "zipcode": "26000", "isDefault": "F" }
]
```

(แถว "ไม่ระบุ" จะมาเฉพาะเมื่อ `refId` ตรงกับมัน หรือไม่ได้ส่ง `refId`)

| Field | ความหมาย |
|---|---|
| `id` | `TblArea.Id` ส่งกลับเป็น `areaId` |
| `refId` | id ของชั้นบน (จังหวัดชี้ที่ตัวเอง) |
| `nameTh` / `nameEn` | ชื่อ (`nameEn` ว่างทั้งหมด) |
| `areaType` | `PROVINCE` / `DISTRICT` / `SUBDISTRICT` |
| `zipcode` | รหัสไปรษณีย์ มีที่อำเภอ ตำบลไม่มี |
| `isDefault` | `"T"` = แถว "ไม่ระบุ" ของชั้นนั้น |

| HTTP | เมื่อไร |
|---|---|
| 200 | สำเร็จ รวมกรณีไม่พบ (`[]`) |
| 400 | ไม่ส่ง `areaType` หรือส่งค่าอื่น (`errors.areaType`) |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว |

## 4. บันทึก `areaId`

- **ส่ง id ของชั้นที่ลึกที่สุดที่ผู้ใช้เลือก:** ปกติคือตำบล ถ้าเลือกแค่จังหวัดหรืออำเภอ ส่ง id ชั้นนั้นได้ API รับทุกชั้น
- **ไม่เลือก:** ส่ง `null` หรือ `""` (หรือ id ของแถว "ไม่ระบุ" ถ้าอยากเก็บค่าแบบระบบเดิม)
- **ผู้ติดต่อเดิม:** มี `areaId` อยู่ 61 คน เป็นตำบล 51, อำเภอ 4, จังหวัด 4 และอีก 2 คนเป็นข้อความ `" ราขเทวี"` ที่ไม่ใช่ id
  `UpdateAccountById` ยอมให้ส่งค่าเดิมกลับมาได้แม้จะไม่อยู่ใน `TblArea` จะได้ไม่ 400 ตอนกดบันทึกฟอร์มที่ไม่ได้แก้
- **เรื่อง (`TblService.Area_ID`):** ตอนนี้ยังว่างทุกเรื่อง

## 5. แสดงพื้นที่ของผู้ติดต่อหรือเรื่องที่มีอยู่ (`areaPath`)

ไม่ต้องไล่ `refId` เอง backend ไล่ให้แล้วส่ง `areaPath` มาคู่กับ `areaId`:

| ที่ไหน | field |
|---|---|
| `GET /Account/GetAccountById` (และ response ของ `UpdateAccountById`, `Add/Update/DeleteAccountDetail`) | `areaPath` |
| `GET /Services/GetServiceById` | `account.areaPath` (พื้นที่ของผู้ติดต่อ) และ `areaPath` (พื้นที่ของเรื่อง) |
| `GET /Account/GetAccounts` | `areaPath` เป็น `null` (ไม่ไล่ให้ในรายการค้นหา) ใช้ `GetAccountById` เมื่อเปิดดูคนนั้น |

- **รูปแบบ:** array ของ object แบบเดียวกับแถวของ `GetArea` เรียง **จังหวัด → อำเภอ → ตำบล**
- **จำนวน:** `areaId` เป็นตำบลได้ 3 ตัว, อำเภอ 2 ตัว, จังหวัด 1 ตัว, ว่างหรือไม่อยู่ใน `TblArea` (เช่น `" ราขเทวี"`) ได้ `[]`
- **ตั้งค่า drop-down:** ดู `areaType` ของแต่ละตัว แล้วโหลดรายการของชั้นถัดไปด้วย `refId` = id ของชั้นบน
- **รหัสไปรษณีย์:** ใช้ `zipcode` ของตัวที่เป็น `DISTRICT`

ตัวอย่าง: ผู้ติดต่อที่ `areaId` = `16AC86F1FC2255923683F632CAC454DA` (ข้อมูลจริง)

```json
"areaPath": [
  { "id": "5EF900A3F3430FB51A813C37BA3D2BB4", "refId": "5EF900A3F3430FB51A813C37BA3D2BB4", "nameTh": "กรุงเทพมหานคร", "nameEn": null, "areaType": "PROVINCE", "zipcode": "10000", "isDefault": "F" },
  { "id": "4F3A0B8A4272BFEB369CB62889860FC9", "refId": "5EF900A3F3430FB51A813C37BA3D2BB4", "nameTh": "หลักสี่", "nameEn": null, "areaType": "DISTRICT", "zipcode": "10210", "isDefault": "F" },
  { "id": "16AC86F1FC2255923683F632CAC454DA", "refId": "4F3A0B8A4272BFEB369CB62889860FC9", "nameTh": "ตลาดบางเขน", "nameEn": null, "areaType": "SUBDISTRICT", "zipcode": null, "isDefault": "F" }
]
```

คือ **แขวงตลาดบางเขน เขตหลักสี่ กรุงเทพมหานคร 10210**

## 6. โค้ดตัวอย่าง (TypeScript)

```ts
export type AreaType = "PROVINCE" | "DISTRICT" | "SUBDISTRICT";
export interface AreaItem {
  id: string; refId: string; nameTh: string; nameEn: string | null;
  areaType: AreaType; zipcode: string | null; isDefault: "T" | "F";
}

export async function fetchAreas(areaType: AreaType, token: string, refId?: string): Promise<AreaItem[]> {
  const qs = new URLSearchParams({ areaType });
  if (refId) qs.set("refId", refId);
  const res = await fetch(`/api/backend/ListOfValues/GetArea?${qs}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`โหลดรายการพื้นที่ไม่สำเร็จ (${res.status})`);
  return res.json();
}

// เลือกจังหวัดแล้ว: ล้างอำเภอ/ตำบล แล้วโหลดอำเภอของจังหวัดนั้น
// const districts = await fetchAreas("DISTRICT", token, provinceId);
// areaId ที่ส่งไปบันทึก = subdistrictId ?? districtId ?? provinceId ?? null

/** แปลง areaPath เป็นค่าเริ่มต้นของ drop-down 3 ตัว */
export function areaSelection(path: AreaItem[] | null | undefined) {
  const pick = (t: AreaType) => path?.find((a) => a.areaType === t)?.id ?? null;
  return { provinceId: pick("PROVINCE"), districtId: pick("DISTRICT"), subdistrictId: pick("SUBDISTRICT") };
}

/** ข้อความแสดงผล เช่น "ตลาดบางเขน หลักสี่ กรุงเทพมหานคร 10210" */
export function areaText(path: AreaItem[] | null | undefined): string {
  if (!path?.length) return "";
  const zip = path.find((a) => a.areaType === "DISTRICT")?.zipcode ?? "";
  return [...path].reverse().map((a) => a.nameTh).concat(zip ? [zip] : []).join(" ");
}
```

- **เก็บรายการจังหวัดไว้ใน memory ได้:** ข้อมูลแทบไม่เปลี่ยน
- **zipcode:** ถ้าจะเติมรหัสไปรษณีย์อัตโนมัติ ใช้ `zipcode` ของอำเภอ (ตำบลไม่มี)

## 7. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetArea?areaType=DISTRICT&refId=81DEE3BDDF62528B9DEB9C1C6D74CEC4
Authorization: Bearer <token>
```

ใน `ContactTogetherApi.http` มีตัวอย่าง จังหวัด → อำเภอของนครนายก → ตำบลของอำเภอเมืองนครนายก
