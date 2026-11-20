# Service Title API (`GET /ListOfValues/GetServiceTitle`) สำหรับ Frontend

ใช้ดึงรายการ **หัวข้อเรื่อง** (`TblServiceTitles`) ไปทำ drop-down "หัวข้อเรื่อง" มีที่ใช้ 2 หน้า:

| ใช้ที่ไหน | ส่ง `id` ที่เลือกเป็น |
|---|---|
| ฟอร์มเปิดเรื่อง `POST /Services/AddNewService` | `serviceTitleId` (**บังคับ**) |
| หน้าค้นหา `GET /Services/SearchService` | `ServiceTitleId` (ไม่บังคับ) |

ใช้แสดงชื่อหัวข้อของเรื่องที่มีอยู่ได้ด้วย: เทียบ `serviceTitleId` ของเรื่องกับ `id` ในรายการนี้
(ถ้าใช้ `GET /Services/GetServiceById` มี `serviceTitleText` มาให้แล้ว ไม่ต้องเทียบเอง)

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetServiceTitle`), `Dtos/ServiceTitleResponse.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` (ได้จาก `POST /Auth/login`) |
| Query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetServiceTitle` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetServiceTitle` (ผ่าน `rewrites()` ใน `next.config.ts` เพราะ API ยังไม่เปิด CORS) |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ไม่มี envelope เฉพาะหัวข้อที่เปิดใช้งาน (`IsEnable = "T"`) **เรียงตามลำดับที่เพิ่มเข้าระบบ**
แสดงตามลำดับที่ได้ได้เลย

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblServiceTitles.Id` เป็น GUID **มีขีด ตัวพิมพ์ใหญ่** เช่น `81EE380A-96E2-F111-8E93-96F1F7A12F5E` ใช้เป็น `value` ของ option |
| `serviceTitleText` | string | ชื่อหัวข้อที่ใช้แสดงผล |

ไม่มี `isDefault` ถ้าต้องการให้ฟอร์มเลือกค่าไว้ก่อน ให้ตกลงกับทีมว่าจะใช้หัวข้อไหน

### 2.2 ตัวอย่าง (ข้อมูลจริง ณ 17 พ.ย. 2026 มี 4 รายการ)

```json
[
  { "id": "81EE380A-96E2-F111-8E93-96F1F7A12F5E", "serviceTitleText": "การเมือง-การปกครอง" },
  { "id": "BED80212-96E2-F111-8E93-96F1F7A12F5E", "serviceTitleText": "เศรษฐกิจ" },
  { "id": "345AAC1D-96E2-F111-8E93-96F1F7A12F5E", "serviceTitleText": "สังคมและสวัสดิการ" },
  { "id": "B0F2CE29-96E2-F111-8E93-96F1F7A12F5E", "serviceTitleText": "การศึกษาและเทคโนโลยี" }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ

### 2.3 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `ServiceTitle[]` |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |
| 500 | ข้อผิดพลาดฝั่ง server | ว่าง หรือ JSON (อย่าพึ่งรูปแบบ) |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/serviceTitle.ts
export interface ServiceTitle {
  id: string;
  serviceTitleText: string;
}

export async function fetchServiceTitles(opts: { token: string; signal?: AbortSignal }): Promise<ServiceTitle[]> {
  const res = await fetch("/api/backend/ListOfValues/GetServiceTitle", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("โหลดรายการหัวข้อเรื่องไม่สำเร็จ");
}
```

```tsx
<select value={serviceTitleId} onChange={(e) => setServiceTitleId(e.target.value)} required>
  <option value="">-- เลือกหัวข้อเรื่อง --</option>
  {titles.map((t) => <option key={t.id} value={t.id}>{t.serviceTitleText}</option>)}
</select>
```

```ts
// แสดงชื่อหัวข้อจาก serviceTitleId ของเรื่อง (เช่น แถวของ SearchService)
const titleText = (id?: string | null) =>
  titles.find((t) => t.id === id?.toUpperCase())?.serviceTitleText ?? "-";
```

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้

---

## 4. ข้อควรรู้ (ณ 17 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **`id` เป็น GUID มีขีด** | ไม่ใช่ hex 32 ตัวแบบ id อื่นในระบบ ส่งกลับไปตามที่ได้รับ ห้ามตัดขีดออก |
| **ตัวพิมพ์ใหญ่** | `id` ส่งมาเป็นตัวพิมพ์ใหญ่ ตรงกับ `serviceTitleId` ใน response ของ Services ทุกตัว เทียบด้วย `===` ได้เลย |
| **บังคับตอนเปิดเรื่อง** | `AddNewService` ไม่มี `serviceTitleId` = 400 ส่ง id ที่ไม่มีอยู่หรือถูกปิดใช้งาน = 400 (`No enabled TblServiceTitles row with Id '…'`) |
| **เรื่องเก่าใช้หัวข้อเดียว** | ทั้ง 189 เรื่องในระบบตอนนี้เป็น "การเมือง-การปกครอง" ถ้าค้นหาด้วยหัวข้ออื่นจะยังไม่เจอเรื่อง |
| **หัวข้อที่ถูกปิดใช้งาน** | จะหายจากรายการนี้ แต่เรื่องเก่าที่ใช้หัวข้อนั้นยังอ้างอยู่ ถ้าหาใน `titles` ไม่เจอให้แสดง "-" หรือใช้ `serviceTitleText` จาก `GetServiceById` |

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetServiceTitle
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetServiceTitle -H "Authorization: Bearer <token>"
```
