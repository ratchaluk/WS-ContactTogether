# Organization API (`GET /ListOfValues/GetOrganization`) สำหรับ Frontend

ใช้ดึงรายการ **หน่วยงาน** (`TblOrganization`) ไปทำ drop-down หน่วยงาน 2 ชั้น: **กระทรวง → กรม**
แล้วส่ง `id` ของกรมที่เลือกเป็น `organizationId` ของ `POST /Services/AddNewService`

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetOrganization`), `Dtos/OrganizationResponse.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetOrganization` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetOrganization` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ของหน่วยงานที่เปิดใช้งาน **ทั้งหมดในครั้งเดียว** (395 รายการ ณ 17 พ.ย. 2026)
**เรียงตามชื่อ (`nameTh`)**

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblOrganization.Id` ใช้เป็น `value` ของ option และส่งเป็น `organizationId` |
| `refId` | string | หน่วยงานแม่ (ดู 2.2) |
| `nameTh` | string | ชื่อที่ใช้แสดงผล |
| `nameEn` | string \| null | ชื่อภาษาอังกฤษ (ตอนนี้ทุกแถวเหมือน `nameTh`) |

### 2.2 `refId` สัมพันธ์กันอย่างไร

เป็นต้นไม้ **2 ชั้นพอดี** (ตรวจใน DB แล้ว ไม่มีชั้นที่ 3 และไม่มีแถวที่หาแม่ไม่เจอ)

| ชั้น | วิธีดู | จำนวน | ตัวอย่าง |
|---|---|---|---|
| หน่วยงานหลัก (กระทรวง) | `refId === id` (ชี้ที่ตัวเอง) | 22 | กระทรวงการต่างประเทศ, หน่วยงานอิสระ, อื่น ๆ |
| หน่วยงานย่อย (กรม) | `refId` = `id` ของกระทรวง | 373 | กรมการกงสุล (ใต้ กระทรวงการต่างประเทศ) |

```
กระทรวงการต่างประเทศ   id = 00D1DA8C…  refId = 00D1DA8C…   ← ชี้ตัวเอง = หน่วยงานหลัก
 ├─ กรมการกงสุล          refId = 00D1DA8C…
 ├─ กรมยุโรป             refId = 00D1DA8C…
 ├─ กระทรวงการต่างประเทศ refId = 00D1DA8C…   ← กรมที่ชื่อเดียวกับกระทรวง (ดูข้อ 4)
 └─ …
```

### 2.3 ตัวอย่าง (ข้อมูลจริง ณ 17 พ.ย. 2026 ตัดมาบางส่วน)

```json
[
  { "id": "00D1DA8C956043D5AD9B38299566A7F0", "refId": "00D1DA8C956043D5AD9B38299566A7F0", "nameTh": "กระทรวงการต่างประเทศ", "nameEn": "กระทรวงการต่างประเทศ" },
  { "id": "0D26AFEE79DD44C680D3E2F606B5C52A", "refId": "00D1DA8C956043D5AD9B38299566A7F0", "nameTh": "กรมการกงสุล", "nameEn": "กรมการกงสุล" }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ

### 2.4 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `OrganizationItem[]` |
| 401 | ไม่มี token, token หมดอายุ, หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/organization.ts
export interface OrganizationItem {
  id: string;
  refId: string;
  nameTh: string;
  nameEn: string | null;
}

export async function fetchOrganizations(opts: { token: string; signal?: AbortSignal }): Promise<OrganizationItem[]> {
  const res = await fetch("/api/backend/ListOfValues/GetOrganization", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("โหลดรายการหน่วยงานไม่สำเร็จ");
}

/** แยกเป็นกระทรวง และกรมของแต่ละกระทรวง (ลำดับตามที่ API ส่งมา = เรียงตามชื่อ) */
export function splitOrganizations(list: OrganizationItem[]) {
  const ministries = list.filter((o) => o.refId === o.id);
  const departmentsOf = (ministryId: string) =>
    list.filter((o) => o.refId === ministryId && o.id !== ministryId);
  return { ministries, departmentsOf };
}
```

```tsx
// drop-down 2 ชั้น: เลือกกระทรวงก่อน แล้วค่อยเลือกกรม ส่ง departmentId เป็น organizationId
const { ministries, departmentsOf } = splitOrganizations(organizations);

<select value={ministryId} onChange={(e) => { setMinistryId(e.target.value); setDepartmentId(""); }}>
  <option value="">-- เลือกกระทรวง --</option>
  {ministries.map((m) => <option key={m.id} value={m.id}>{m.nameTh}</option>)}
</select>

<select value={departmentId} disabled={!ministryId} onChange={(e) => setDepartmentId(e.target.value)}>
  <option value="">-- เลือกกรม --</option>
  {departmentsOf(ministryId).map((d) => <option key={d.id} value={d.id}>{d.nameTh}</option>)}
</select>
```

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้

---

## 4. ข้อควรรู้ (สถานะ ณ 17 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **เรื่องเก่าเก็บหน่วยงานย่อย (กรม) ทั้งหมด** | 179 เรื่องที่มีหน่วยงาน ชี้ไปที่กรมทุกเรื่อง ให้ส่ง `id` ของ **กรม** เป็น `organizationId` (API ไม่ได้บังคับ ส่ง id กระทรวงก็บันทึกได้ แต่จะต่างจากข้อมูลเดิม) |
| **กรมที่ชื่อเดียวกับกระทรวง** | 21 กระทรวงมีกรมชื่อเดียวกับตัวเอง (เช่น "กระทรวงกลาโหม" ใต้ กระทรวงกลาโหม) ใช้แทน "ทั้งกระทรวง" มีเรื่องเก่าใช้ 27 เรื่อง แสดงตามปกติ |
| **ชื่อกรมซ้ำข้ามกระทรวง** | เช่น "สำนักงานรัฐมนตรี" มีใต้ 16 กระทรวง ถ้าแสดงในรายการเดียวหรือช่องค้นหา ให้แสดงชื่อกระทรวงประกอบ (หาได้จาก `refId`) |
| **ข้อมูลที่น่าจะผิด** | "สำนักงานปลัดกระทรวงวัฒนธรรม" มีใต้ กระทรวงวิทยาศาสตร์และเทคโนโลยี ด้วย (รอทีม DB ตรวจ frontend ไม่ต้องแก้) |
| **"อื่น ๆ"** | เป็นหน่วยงานหลักที่รวมหน่วยงานนอกภาครัฐ เช่น AIS, DTAC |
| **ไม่มีค่าเริ่มต้น** | ไม่มีหน่วยงานไหนตั้งเป็นค่าเริ่มต้น drop-down เริ่มที่ "ยังไม่เลือก" |

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetOrganization
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetOrganization -H "Authorization: Bearer <token>"
```
