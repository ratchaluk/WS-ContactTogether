# กลุ่ม (`GET /ListOfValues/GetEmployeeGroup`) สำหรับ Frontend

ใช้ทำ drop-down **กลุ่ม** ในฟอร์มเพิ่มงาน (ประเภทบริการ → กลุ่ม → ผู้รับ) ได้กลุ่มที่เปิดใช้งาน
**ทั้งหมดในครั้งเดียว** เป็น 2 ชั้นผ่าน `refId` แบบเดียวกับ `GetOrganization` ([GetOrganization.md](GetOrganization.md))
ส่ง `id` ที่เลือกไปเป็น `groupId` ของ `POST /Services/AddNewServiceOperations` ([AddNewServiceOperations.md](AddNewServiceOperations.md))
และใช้โหลดรายชื่อผู้รับจาก `GET /ListOfValues/GetEmployeeByGroup?groupId=` ([GetEmployeeByGroup.md](GetEmployeeByGroup.md))

> อ้างอิงโค้ด: `Controllers/ListOfValuesController.cs` (`GetEmployeeGroup`), `Dtos/EmployeeGroupResponse.cs`

---

## 1. ภาพรวม

| | |
|---|---|
| Method | `GET` |
| Auth | **ต้องใช้ token**: `Authorization: Bearer <token>` |
| Query | ไม่มี |
| Backend (dev) | `http://localhost:5018/ListOfValues/GetEmployeeGroup` |
| URL จาก Next.js | `/api/backend/ListOfValues/GetEmployeeGroup` |

---

## 2. Response

### 2.1 รูปแบบ

200 ตอบเป็น **array ตรง ๆ** ของกลุ่มที่เปิดใช้งาน (`TblUserGroup.Enable = 'T'`) **เรียงตามชื่อ**
(28 รายการ ณ 25 พ.ย. 2026: กลุ่มหลัก 12 กลุ่มย่อย 16)

| Field | Type | ความหมาย |
|---|---|---|
| `id` | string | `TblUserGroup.GroupId` (ในฐานข้อมูลเป็นตัวเลข แต่ส่งเป็น string เพราะ `groupId` ของงานเป็น string) |
| `refId` | string | กลุ่มหลัก (ดู 2.2) |
| `name` | string | ชื่อกลุ่ม คืนตามที่เก็บไว้ บางชื่อมีช่องว่างเกิน เช่น `"…อิสระ  - Agent."` |

### 2.2 `refId` สัมพันธ์กันอย่างไร

| ชั้น | วิธีดู | จำนวน | ตัวอย่าง |
|---|---|---|---|
| กลุ่มหลัก | `refId === id` (ชี้ที่ตัวเอง) | 12 | 1212, claim, QA, การคลัง /พาณิชย์/… |
| กลุ่มย่อย | `refId` = `id` ของกลุ่มหลัก | 16 | การคลัง… - Agent., การคลัง… - Chief/Sup. |

```
การคลัง /พาณิชย์/อุตสาหกรรม/วัฒนธรรม/อิสระ   id = "45"  refId = "45"   ← กลุ่มหลัก
 ├─ … - Agent.                              id = "5"   refId = "45"
 └─ … - Chief/Sup.                          id = "6"   refId = "45"
QA                                          id = "70"  refId = "70"   ← กลุ่มหลักที่ไม่มีกลุ่มย่อย เลือกได้เลย
```

- **กลุ่มหลักที่มีกลุ่มย่อย** (เช่น การคลัง…, claim, Language, Training): ให้ผู้ใช้เลือกกลุ่มย่อย
  เหมือนระบบเดิม พนักงานอยู่ในกลุ่มย่อย (ณ 25 พ.ย. 2026 มีแค่ Training ที่มีสมาชิกในกลุ่มหลักโดยตรง 1 คน)
- **กลุ่มหลักที่ไม่มีกลุ่มย่อย** (เช่น 1212, QA, QS, การให้บริการแผนที่เพื่อการเดินทาง): เลือกกลุ่มหลักนั้นได้เลย
- กลุ่มย่อยที่กลุ่มหลักถูกปิดไปแล้ว **ไม่ถูกส่งมา** (ระบบเดิมก็ไม่แสดงกลุ่มพวกนี้)

### 2.3 ตัวอย่าง (ข้อมูลจริง ณ 25 พ.ย. 2026 ตัดมาบางส่วน)

```json
[
  { "id": "79", "refId": "79", "name": "1212" },
  { "id": "77", "refId": "77", "name": "claim" },
  { "id": "81", "refId": "77", "name": "claim - Agent." },
  { "id": "80", "refId": "77", "name": "claim - Sup." },
  { "id": "70", "refId": "70", "name": "QA" },
  { "id": "45", "refId": "45", "name": "การคลัง /พาณิชย์/อุตสาหกรรม/วัฒนธรรม/อิสระ" },
  { "id": "5",  "refId": "45", "name": "การคลัง /พาณิชย์/อุตสาหกรรม/วัฒนธรรม/อิสระ  - Agent." },
  { "id": "6",  "refId": "45", "name": "การคลัง /พาณิชย์/อุตสาหกรรม/วัฒนธรรม/อิสระ  - Chief/Sup." }
]
```

ใช้เป็นตัวอย่างเท่านั้น **อย่า hard-code รายการหรือ id** ในหน้าเว็บ

### 2.4 Status code

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (รวมกรณีไม่มีข้อมูล `[]`) | `EmployeeGroupItem[]` |
| 401 | ไม่มี token, token หมดอายุ หรือ logout แล้ว | ว่าง |

---

## 3. โค้ดตัวอย่าง (Next.js + TypeScript)

```ts
// lib/api/employeeGroup.ts
export interface EmployeeGroupItem {
  id: string;
  refId: string;
  name: string;
}

export async function fetchEmployeeGroups(opts: { token: string; signal?: AbortSignal }): Promise<EmployeeGroupItem[]> {
  const res = await fetch("/api/backend/ListOfValues/GetEmployeeGroup", {
    headers: { Authorization: `Bearer ${opts.token}` },
    signal: opts.signal,
  });
  if (res.ok) return res.json();
  if (res.status === 401) throw new Error("กรุณาเข้าสู่ระบบใหม่");
  throw new Error("โหลดรายการกลุ่มไม่สำเร็จ");
}

/** จัดเป็นกลุ่มหลัก + กลุ่มย่อยของแต่ละกลุ่มหลัก (ลำดับตามที่ API ส่งมา = เรียงตามชื่อ) */
export function groupEmployeeGroups(list: EmployeeGroupItem[]) {
  const mains = list.filter((g) => g.refId === g.id);
  return mains.map((main) => ({
    main,
    subs: list.filter((g) => g.refId === main.id && g.id !== main.id),
  }));
}
```

```tsx
// drop-down เดียว เลือกได้ในครั้งเดียว: กลุ่มที่มีกลุ่มย่อยแสดงเป็นหัวข้อ (optgroup)
// กลุ่มที่ไม่มีกลุ่มย่อยเป็นตัวเลือกได้เลย พอเลือกแล้วโหลดผู้รับของกลุ่มนั้น
<select
  value={groupId}
  onChange={(e) => { setGroupId(e.target.value); setOwnerId(""); }}
>
  <option value="">-- เลือกกลุ่ม --</option>
  {groupEmployeeGroups(groups).map(({ main, subs }) =>
    subs.length > 0 ? (
      <optgroup key={main.id} label={main.name}>
        {subs.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
      </optgroup>
    ) : (
      <option key={main.id} value={main.id}>{main.name}</option>
    ),
  )}
</select>
```

รายการนี้แทบไม่เปลี่ยน โหลดครั้งเดียวต่อ session แล้วเก็บไว้ได้

---

## 4. ข้อควรรู้ (สถานะ ณ 25 พ.ย. 2026)

| เรื่อง | ผลกับ frontend |
|---|---|
| **ชื่อกลุ่มย่อยมีชื่อกลุ่มหลักอยู่แล้ว** | เช่น "การคลัง… - Agent." แสดงชื่อเต็มได้เลย ไม่ต้องต่อชื่อกลุ่มหลักเพิ่ม |
| **ชื่อกลุ่มย่อยบางตัวไม่ตรงกับกลุ่มหลัก** | เช่น "คมนาคม-กลาโหม-พลังงาน - Agent." อยู่ใต้ "คมนาคม/กลาโหม/พลังงาน+EGAT/…" เป็นข้อมูลจากระบบเดิม แสดงตามที่ได้ |
| **Training มีกลุ่มย่อยตัวพิมพ์เล็ก** | "newcomer" และ "training" อยู่ใต้ "Training" |
| **API ยังไม่ตรวจ `groupId`** | `AddNewServiceOperations` บันทึกค่าที่ส่งมาตรง ๆ ยังไม่ตรวจว่ามีกลุ่มนี้จริง หรือผู้รับอยู่ในกลุ่มนี้ |
| **ไม่มีค่าเริ่มต้น** | drop-down เริ่มที่ "ยังไม่เลือก" |

---

## 5. ทดสอบด้วย REST Client / curl

```http
GET http://localhost:5018/ListOfValues/GetEmployeeGroup
Authorization: Bearer <token>
```

```bash
curl http://localhost:5018/ListOfValues/GetEmployeeGroup -H "Authorization: Bearer <token>"
```
