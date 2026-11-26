# HomeController ทำงานอย่างไร (สำหรับ backend)

`HomeController` อยู่เบื้องหลังกล่อง **"รายการให้บริการ"** ในหน้าแรกหลัง login เอกสารนี้อธิบายว่าโค้ดข้างในทำอะไร
ตามลำดับ และเชื่อมกับส่วนอื่นอย่างไร ส่วนวิธีเรียกใช้จากหน้าเว็บอยู่ในเอกสาร frontend:

- [HomeServices.md](HomeServices.md): `GetInboxServices` / `GetProcessingServices` (query, response, ตัวอย่าง TS)
- [InboxStream.md](InboxStream.md): `InboxStream` (hook `useInboxStream`, ข้อควรระวังฝั่งเบราว์เซอร์)
- [ReceiveService.md](ReceiveService.md): การรับงาน ซึ่งทำให้เรื่องหายจากรายการรับ

> โค้ด: [Controllers/HomeController.cs](../Controllers/HomeController.cs),
> [Realtime/InboxNotifier.cs](../Realtime/InboxNotifier.cs), [Realtime/IInboxNotifier.cs](../Realtime/IInboxNotifier.cs),
> [Dtos/InboxSignal.cs](../Dtos/InboxSignal.cs), [Dtos/HomeServiceListRequest.cs](../Dtos/HomeServiceListRequest.cs),
> [Dtos/HomeServiceListItemResponse.cs](../Dtos/HomeServiceListItemResponse.cs),
> `ServicesController.NotifyInbox` (จุดส่งสัญญาณ)
>
> ที่มา: [Spec-Home-Inbox-Processing-API.md](Spec-Home-Inbox-Processing-API.md) (หน้า Home ของระบบเดิม GCC)
>
> **เขียนเมื่อ 26 พ.ย. 2569**

---

## 1. ภาพรวม

| Endpoint | ใช้ทำอะไร | ตอบ |
|---|---|---|
| `GET /Home/GetInboxServices` | แท็บ **รายการรับ**: เรื่องที่คนอื่นโอนงานมาให้เรา และเรายังไม่ได้รับงาน | `PagedEnvelope<HomeServiceListItemResponse>` |
| `GET /Home/GetProcessingServices` | แท็บ **งานดำเนินการ**: เรื่องที่เราเป็นเจ้าของ และยังไม่ปิด | `PagedEnvelope<HomeServiceListItemResponse>` |
| `GET /Home/InboxStream` | ทำให้รายการรับ **อัปเดตเองแบบ real-time** (Server-Sent Events) | `text/event-stream` (เปิดค้างไว้) |

สิ่งที่ทั้งสาม endpoint ใช้ร่วมกัน:

- **`[Authorize]` ที่ระดับ class:** ทุก endpoint ต้องมี token
- **ผู้ใช้คือเจ้าของ token เสมอ:** อ่านรหัสพนักงานจาก claim `sub` (`TblEmployee.Id`) ส่งรหัสพนักงานมาทาง query ไม่ได้
  ใครดูรายการของคนอื่นก็ไม่ได้
- **ไม่มี service / repository layer** (ตามแบบของโปรเจกต์) controller ใช้ `ApplicationDbContext` โดยตรง
- inject 3 ตัว: `ApplicationDbContext`, `IInboxNotifier` (singleton ตัวกระจายสัญญาณ), `ITokenRevocationStore`
  (ไว้ตรวจว่า token ถูก logout แล้วหรือยัง)

```
เบราว์เซอร์ ──► Next.js (Vercel) rewrite /api/backend/*  ──► HomeController
                proxy.ts แนบ token จาก httpOnly cookie
                เป็น header Authorization: Bearer …
```

---

## 2. ทุก request ผ่านด่านเดียวกันก่อน

1. **JwtBearer** ตรวจ token: ลายเซ็น, issuer, audience, วันหมดอายุ
2. **`OnTokenValidated`** ใน `Program.cs` ปฏิเสธ token ที่ `jti` อยู่ใน `MemoryTokenRevocationStore` (logout แล้ว)
   ด่าน 1–2 ไม่ผ่านจะได้ **401 body ว่าง**
3. ใน action: อ่าน `sub` ถ้าไม่มี (token แปลก ๆ) → `NoEmployee()` ได้ **401** `{ statusCode, message }`

---

## 3. `GetInboxServices` และ `GetProcessingServices`

สอง endpoint นี้ **ต่างกันแค่เงื่อนไข `WHERE`** ส่วนที่เหลือใช้เมธอด `ListAsync` ตัวเดียวกัน

```
GetInboxServices ───────┐
                        ├─► OpenServices() + เงื่อนไขของแท็บ ─► ListAsync(): ตรวจ query → นับ → ดึงหน้า → แปลงค่า → envelope
GetProcessingServices ──┘
```

### 3.1 `OpenServices()`: เรื่องที่ยังไม่ปิด (ใช้ทั้งสองแท็บ)

```csharp
_db.TblServices.AsNoTracking().Where(service => service.IsEnable == "T"
    && _db.TblStatuses.Any(status => status.Id == service.StatusId
        && OpenStatusIsTypes.Contains(status.IsType)));   // ["OPEN", "PENDING"]
```

- เรื่องต้องเปิดใช้งาน (`Is_Enable = 'T'`)
- สถานะของเรื่องต้องมี `IsType` เป็น `OPEN` หรือ `PENDING`
  - ระบบเดิมดูแค่ `PENDING` แต่ระบบนี้เปิดเรื่องและเปิดเรื่องซ้ำด้วยสถานะ "เปิดเรื่อง" (`OPEN`) จึงต้องนับทั้งสองค่า
- **ไม่ได้ดู `Date_Closed`** ใช้สถานะเป็นหลักตามระบบเดิม (ต่างจาก `GetInProcessServices` ตัวเดิมที่ดู `DateClosed == null`)

### 3.2 เงื่อนไขของแท็บรายการรับ

```csharp
OpenServices().Where(service => _db.TblActivities.Any(activity =>
    activity.ServiceId == service.Id
    && activity.IsEnable == "T"          // งานยังไม่ถูกลบ
    && activity.OwnerId == employeeId    // งานนี้มอบให้เรา
    && activity.IsReceived == "F"        // เรายังไม่ได้รับงาน
    && _db.TblStatuses.Any(status => status.Id == activity.StatusId
        && status.IsType == "CLOSE")));  // ขั้นตอนโอนทำเสร็จแล้ว
```

- **ข้อมูลรายการรับมาจาก `TblActivity` (การดำเนินการ) ไม่ใช่จาก `TblService`:** การโอนงานคือการเพิ่มการดำเนินการ
  ที่ผู้รับเป็นคนอื่น (`AddNewServiceOperations` ตั้ง `IsReceived = "F"` ให้เอง)
- **ใช้ `Any()` (SQL `EXISTS`) ไม่ใช้ `JOIN`:** ถ้าเรื่องเดียวมีงานโอนมาให้เราหลายงาน เรื่องนั้นก็ยังขึ้นแถวเดียว
- **งานต้องมีสถานะ `CLOSE`:** ตามกฎของระบบเดิม (`ServiceRepo.GetInbox`) ถ้าโอนโดยเลือกสถานะงานเป็น "เปิด" เรื่องจะไม่ขึ้น
  ปกติฟอร์มไม่มีปัญหา เพราะสถานะเริ่มต้นของงาน (`IsDefault = 'T'`) คือ "ปิด"
- **ออกจากรายการรับเมื่อ:**
  - ผู้รับเปิดดูเรื่อง ทำให้ `ReceiveService` เปลี่ยน `IsReceived` เป็น `"T"`
  - งานถูกลบ (`IsEnable = 'F'`)
  - งานถูกเปลี่ยนผู้รับ หรือเปลี่ยนสถานะงานเป็นอย่างอื่นที่ไม่ใช่ CLOSE
  - เรื่องถูกปิด

### 3.3 เงื่อนไขของแท็บงานดำเนินการ

```csharp
OpenServices().Where(service => service.OwnerId == employeeId);   // TblService.Owner_ID
```

เรื่องที่โอนมาแต่ยังไม่รับ อาจขึ้นทั้งสองแท็บ ถ้าเราเป็นเจ้าของเรื่องด้วย (เหมือนระบบเดิม)

### 3.4 `ListAsync`: ขั้นตอนที่ใช้ร่วมกัน

| ขั้น | ทำอะไร | เหตุผล |
|---|---|---|
| 1. ตรวจ query | `Lang` (ตัด space, ทำเป็นตัวเล็ก, ว่าง = `th`) ต้องเป็น `th`/`en`, `PageNumber` ≥ 1, `PageSize` 1–100 ผิดข้อไหนใส่ `ModelState` แล้วตอบ `ValidationProblem` (400) | รวม error ทุกช่องไว้ใน response เดียว |
| 2. นับ | `total = CountAsync()` บน query ที่ **ยังไม่ join** | join ข้างล่างเป็นการ join ด้วย key จึงไม่ทำให้แถวเพิ่ม นับก่อนก็ได้ผลเท่ากันแต่ query เบากว่า |
| 3. คำนวณ `skip` | `(long)(PageNumber - 1) * PageSize` ถ้า `skip >= total` ตอบหน้าว่างทันที **ไม่ query รอบสอง** | ใช้ `long` เพราะถ้าส่ง `PageNumber` ใหญ่มาก `int` จะล้นเป็นค่าลบ แล้วได้ SQL error |
| 4. ดึงหน้า | join `TblStatus` (inner) และ `TblCategory` (**left**) เรียง `Code` มากไปน้อย ตามด้วย `Id` แล้ว `Skip`/`Take` ที่ DB | DB ไม่มี FK ต้อง join เอง; left join ทำให้เรื่องที่ประเภทหายยังขึ้น (`categoryName = null`); เรียงด้วย `Id` ต่อท้ายเพื่อไม่ให้แถวที่ `Code` ซ้ำสลับหน้ากัน |
| 5. แปลงค่า (ในหน่วยความจำ) | ดูตารางถัดไป | ค่าเหล่านี้ขึ้นกับเวลาปัจจุบัน หรือเป็น logic ที่ไม่ต้องให้ SQL ทำ |
| 6. ห่อ envelope | `Paged()` คืน `{ status, message, data, meta.pagination }` โดย `message` = `พบข้อมูล N รายการ` / `ไม่พบข้อมูล` | รูปแบบเดียวกับ `SearchService` และรายงาน |

**ขั้นที่ 5: ค่าที่คำนวณ** (อ่าน `now = DateTime.Now` ครั้งเดียวต่อ request)

| Field | คำนวณจาก |
|---|---|
| `categoryName`, `statusName` | `PickName`: ขอ `en` และมี `NameEn` ที่ไม่ว่าง ได้ `NameEn` ไม่เช่นนั้นได้ `NameTh` |
| `requireCallBack` | `Call_Back == "Y"` เท่านั้น (`"D"` สายหลุดไม่นับ เหมือนรายงาน 03) |
| `elapsedHours` | `now - Date_Opened` เป็นชั่วโมง ปัดเศษ (`null` ถ้าไม่มีวันเปิด) เป็น **เวลาที่ผ่านไปแล้ว** ไม่ใช่เวลาที่เหลือ |
| `isOverDue` | เปิดมาแล้วตั้งแต่ 7 วัน (10,080 นาที) ขึ้นไป |

`Date_Opened` เก็บเวลาไทย และ `DateTime.Now` ใช้เวลาของเครื่อง server ดังนั้น **server ต้องตั้ง time zone เป็นไทย**
(โค้ดส่วนอื่นของโปรเจกต์ก็ถือแบบนี้) ถ้า server เป็น UTC ค่า `elapsedHours` จะคลาด 7 ชั่วโมง

### 3.5 SQL ที่ได้ (เขียนย่อให้อ่านง่าย ไม่ใช่ข้อความที่ EF สร้างจริงทุกตัวอักษร)

```sql
-- รายการรับ (@me = claim sub)
SELECT s.Id, s.Code, s.Date_Opened, c.NameTh, c.NameEn, st.NameTh, st.NameEn, s.Summary, s.Call_Back
FROM TblService s
JOIN TblStatus st ON st.Id = s.Status_ID
LEFT JOIN TblCategory c ON c.Id = s.Category_ID
WHERE s.Is_Enable = 'T'
  AND st.IsType IN ('OPEN', 'PENDING')                       -- OpenServices()
  AND EXISTS (SELECT 1 FROM TblActivity a
              JOIN TblStatus sa ON sa.Id = a.StatusId
              WHERE a.ServiceId = s.Id AND a.IsEnable = 'T'
                AND a.OwnerId = @me AND a.IsReceived = 'F'
                AND sa.IsType = 'CLOSE')                     -- เงื่อนไขรายการรับ
ORDER BY s.Code DESC, s.Id
OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY;

-- งานดำเนินการ: เหมือนกัน แต่เปลี่ยน EXISTS (...) เป็น  AND s.Owner_ID = @me
```

`COUNT(*)` ใช้ `WHERE` ชุดเดียวกัน แต่ไม่มี `JOIN` / `ORDER BY` / `OFFSET`
ใช้ SQL นี้เทียบผลกับ API ตอนทดสอบได้ (จำนวนและลำดับ `Code` ต้องตรงกัน)

---

## 4. `InboxStream`: รายการรับแบบ real-time

### 4.1 หลักการ: ส่งแค่สัญญาณ ไม่ได้ส่งข้อมูล

stream **ไม่ได้ส่งรายการเรื่อง** ส่งแค่ "รายการรับของคุณอาจเปลี่ยน" แล้วให้ frontend เรียก `GetInboxServices` ใหม่เอง
เงื่อนไขของรายการรับจึงอยู่ที่ข้อ 3.2 ที่เดียว และสัญญาณที่หายไประหว่างทางก็ไม่ทำให้ข้อมูลผิด

```
A กด "บันทึก" งานใหม่ ผู้รับ = B
  └► ServicesController.AddNewServiceOperations
       ├─ SaveChanges (บันทึกลง DB ก่อน)
       └─ NotifyInbox("assigned", serviceId, B)
            └► InboxNotifier.Notify ─► เขียนสัญญาณลง channel ของทุก stream ที่ B เปิดอยู่ (ทุกแท็บ)

HomeController.InboxStream ของ B (วนรอใน loop อยู่)
  └► WaitToReadAsync ตื่น ─► เขียน "event: inbox\ndata: {...}" ─► flush

เบราว์เซอร์ของ B: EventSource ได้ event 'inbox' ─► เรียก GET /Home/GetInboxServices ─► เรื่องใหม่โผล่ในแท็บ
```

**ทำไมใช้ SSE ไม่ใช้ SignalR / WebSocket:** frontend อยู่บน Vercel และเรียก backend ผ่าน rewrite `/api/backend/*` เท่านั้น
โดยมี `proxy.ts` แนบ token จาก cookie ให้ SSE เป็น HTTP GET ธรรมดา จึงใช้เส้นทางนี้ได้เลย (นาฬิกา `/Helper/time/stream`
ก็ทำแบบนี้) ส่วน WebSocket ผ่าน rewrite ของ Vercel ยังไม่มีเอกสารยืนยันว่าใช้ได้

### 4.2 ชีวิตของ stream หนึ่งเส้น (ตามลำดับในโค้ด)

1. **อ่าน `sub`** ไม่มี = 401
2. **คำนวณอายุ stream:** ใช้ค่าที่น้อยกว่าระหว่าง 30 นาที กับเวลาที่เหลือก่อน token หมดอายุ (claim `exp`)
   เก็บ `jti` ไว้ตรวจ logout ในขั้นที่ 7
3. **ลงทะเบียน:** `_inboxNotifier.Subscribe(employeeId)` ได้ `InboxSubscription` มาหนึ่งตัว
   - `Reader` คือช่องที่สัญญาณจะเข้ามา
   - `Closed` จะ fire เมื่อ notifier สั่งปิด stream นี้
4. **รวมเหตุที่ทำให้ stream จบไว้ใน token เดียว (`streamEnd`):**
   - เบราว์เซอร์ตัดการเชื่อมต่อ (`CancellationToken` ของ request)
   - notifier สั่งปิด (`subscription.Closed`)
   - ครบอายุ (`CancelAfter(lifetime)`)
5. **ตั้ง header กัน buffer:**
   - `Content-Type: text/event-stream`
   - `Cache-Control: no-cache, no-transform` (`no-transform` กัน proxy gzip ข้อมูล ซึ่งจะทำให้ข้อมูลค้างใน buffer)
   - `X-Accel-Buffering: no`
   - และ `DisableBuffering()`
6. **ส่งข้อความแรก:** `retry: 3000` และ `event: ready` แล้ว flush ทันที
   - `ready` บอก client ให้โหลดรายการใหม่ ครอบคลุมงานที่เข้ามาช่วงที่หลุดไป
7. **วนรอ (`while (true)`)** แต่ละรอบรอสัญญาณไม่เกิน 25 วินาที (`WaitToReadAsync` + `tick.CancelAfter(25s)`) ผลมีได้ 3 แบบ:

   | ผล | ทำอะไร |
   |---|---|
   | **ครบ 25 วินาทีแล้วไม่มีอะไรมา** | ถ้า token ถูก logout แล้ว (`_revocations.IsRevoked(jti)`) → จบ stream ไม่งั้นส่ง `: ping` (heartbeat) |
   | **มีสัญญาณ** | อ่านทุกตัวที่ค้างอยู่ (`TryRead`) เขียน `event: inbox` ทีละตัว แล้ว flush ครั้งเดียว |
   | **channel ถูกปิด** (`WaitToReadAsync` ได้ `false`) | notifier สั่งปิด → จบ stream |

8. **จบ:** `OperationCanceledException` จากขั้นที่ 4 ถือเป็นการจบปกติ จับไว้เงียบ ๆ จากนั้น `using` เรียก
   `subscription.Dispose()` ซึ่งเอา stream ออกจาก notifier แล้วตอบ `EmptyResult`
   EventSource ฝั่งเบราว์เซอร์จะต่อใหม่เองหลัง 3 วินาที

ตัวอย่างข้อมูลดิบที่ไหลออกมา:

```
retry: 3000

event: ready
data: {}

: ping

event: inbox
data: {"reason":"assigned","serviceId":"427D66AA13734763AC1D99775554659B"}

```

**stream จบเมื่อไร**

| เหตุ | ตรวจเจอที่ไหน | เร็วแค่ไหน |
|---|---|---|
| ปิดแท็บ / ออกจากหน้า | `CancellationToken` ของ request | ทันที |
| logout | รอบ heartbeat (ขั้นที่ 7) | ภายใน 25 วินาที (ต่อใหม่ได้ 401) |
| token หมดอายุ หรือครบ 30 นาที | `CancelAfter(lifetime)` | ตรงเวลา (EventSource ต่อใหม่ด้วย cookie ล่าสุด) |
| คนเดียวเปิดเกิน 10 stream | `subscription.Closed` (ตัวที่เก่าสุดถูกปิด) | ทันที |

**ทำไมต้อง heartbeat ทุก 25 วินาที:** rewrite ของ Vercel ตัด stream ที่เงียบเกิน 120 วินาที
และใช้รอบเดียวกันนี้ตรวจ logout ไปด้วย (token ที่ logout แล้วยังผ่าน JwtBearer ไปแล้วตอนเปิด stream จึงต้องตรวจซ้ำเอง)

### 4.3 `InboxNotifier`: ตัวกระจายสัญญาณ

ลงทะเบียนเป็น **singleton** ใน `Program.cs` เก็บ stream ที่เปิดอยู่ไว้ในหน่วยความจำ:

```
Dictionary<employeeId, List<Entry>>   (ป้องกันด้วย lock)
                         └─ Entry = Channel<InboxSignal> (จุได้ 8 ตัว, เต็มแล้วทิ้งตัวเก่าสุด) + CancellationTokenSource Closed
```

| เมธอด | ทำอะไร |
|---|---|
| `Subscribe(employeeId)` | สร้าง `Entry` ใหม่ใส่ท้าย list ถ้าคนนี้มีครบ 10 แล้ว เอาตัวแรก (เก่าสุด) ออกแล้วปิด (ปิดนอก lock เพราะการ cancel จะรัน callback ของ stream นั้นทันที) |
| `Notify(ids, signal)` | ข้าม id ที่ว่างหรือซ้ำ รวบรวม `Entry` ของทุก id ภายใน lock แล้ว `TryWrite` นอก lock (channel แบบ DropOldest รับเสมอ ไม่ block) |
| `Unsubscribe` (ผ่าน `Dispose`) | เอา `Entry` ออกจาก list ถ้า list ว่างให้ลบ key ทิ้ง แล้วปิด `Entry` |

ทำไมจำกัด:
- **10 stream ต่อคน:** กันแท็บที่ลืมเปิดค้าง หรือ connection ที่ไม่ปิด สะสมจนหน่วยความจำโต
- **สัญญาณค้างได้ 8 ตัวต่อ stream:** สัญญาณแค่บอกให้โหลดใหม่ ทิ้งตัวเก่าไปก็ไม่เสียอะไร

### 4.4 ใครส่งสัญญาณ (`ServicesController.NotifyInbox`)

เรียก **หลังบันทึก DB สำเร็จแล้วเท่านั้น** (ถ้าส่งก่อน client อาจโหลดรายการเร็วเกินไปแล้วยังไม่เห็นข้อมูลใหม่)

| Endpoint | เงื่อนไข | ส่งให้ | `reason` |
|---|---|---|---|
| `AddNewServiceOperations` | งานใหม่มอบให้คนอื่น (`IsReceived = "F"`) | ผู้รับ | `assigned` |
| `UpdateServiceOperations` | เปลี่ยนผู้รับ | ผู้รับคนเดิม **และ** คนใหม่ | `updated` |
| `UpdateServiceOperations` | ผู้รับเดิม แต่เปลี่ยนสถานะของงานที่ยังรอรับ | ผู้รับ | `updated` |
| `DeleteServiceOperations` | ลบงานที่ยังรอรับ | ผู้รับ | `removed` |
| `ReceiveService` | รับงานได้อย่างน้อย 1 งาน | ตัวเอง (ให้แท็บอื่นของคนเดียวกันโหลดใหม่) | `received` |
| `UpdateService` | ปิดเรื่อง หรือเปิดเรื่องซ้ำ (ดูจาก `DateClosed` ที่เปลี่ยนจากว่างเป็นมีค่า หรือกลับกัน) | ผู้รับทุกคนของงานในเรื่องนี้ที่ยังรอรับ | `updated` |

> **กฎ:** โค้ดใหม่ที่เปลี่ยน `TblActivity.OwnerId` / `IsReceived` / `IsEnable` / `StatusId` หรือเปลี่ยนสถานะเปิด/ปิดของเรื่อง
> **ต้องเรียก `NotifyInbox` ด้วย** ไม่งั้นรายการรับของผู้ที่เกี่ยวข้องจะไม่อัปเดตเองจนกว่าจะโหลดหน้าใหม่ (เขียนไว้ใน `CLAUDE.md` แล้ว)

สัญญาณอาจมาเกิน เช่น ตอนเปลี่ยนผู้รับ ทั้งคนเดิมและคนใหม่ได้สัญญาณ บางคนโหลดแล้วรายการไม่เปลี่ยน ถือว่าปกติ

---

## 5. ค่าคงที่ที่ปรับได้

| ค่าคงที่ | ค่า | อยู่ที่ | ความหมาย |
|---|---|---|---|
| `MaxPageSize` | 100 | `HomeController` | `PageSize` สูงสุด |
| `OverDueMinutes` | 10,080 (7 วัน) | `HomeController` | เกณฑ์ `isOverDue` |
| `CallBackRequired` | `"Y"` | `HomeController` | ค่า `Call_Back` ที่นับเป็นต้องโทรกลับ |
| `OpenStatusIsTypes` | `OPEN`, `PENDING` | `HomeController` | `IsType` ของเรื่องที่ยังไม่ปิด |
| `InboxHeartbeat` | 25 วินาที | `HomeController` | รอบ `: ping` และรอบตรวจ logout ต้องน้อยกว่า 120 วินาทีของ Vercel |
| `InboxStreamLifetime` | 30 นาที | `HomeController` | อายุสูงสุดของ stream |
| `InboxRetryMs` | 3,000 | `HomeController` | ค่า `retry:` ที่ส่งให้ EventSource |
| `MaxStreamsPerEmployee` | 10 | `InboxNotifier` | stream สูงสุดต่อคน |
| `BufferedSignals` | 8 | `InboxNotifier` | สัญญาณค้างสูงสุดต่อ stream |

---

## 6. ข้อจำกัดและเรื่องที่ควรรู้

- **โอนงานแล้วเจ้าของเรื่องไม่เปลี่ยน:** ระบบเดิมเปลี่ยน `TblService.Owner_ID` เป็นผู้รับ แต่ `AddNewServiceOperations` ยังไม่ทำ
  - ผู้รับเห็นเรื่องในรายการรับ แต่ไม่เห็นในงานดำเนินการ
  - ผู้โอนยังเห็นเรื่องในงานดำเนินการของตัวเอง
  - ยังไม่ได้ตกลงกันว่าจะแก้ที่ไหน (ดู handoff 25 พ.ย.)
- **`TblActivity` มีแต่ข้อมูลจากระบบใหม่:** งานของระบบเดิมไม่ได้นำเข้ามา รายการรับจึงมีแต่การโอนที่ทำในระบบใหม่
- **`InboxNotifier` และ `MemoryTokenRevocationStore` อยู่ในหน่วยความจำของ process:** ถ้าขยาย backend เป็นหลายเครื่อง
  สัญญาณที่เกิดบนเครื่องหนึ่งจะไปไม่ถึง stream ที่เปิดค้างกับอีกเครื่อง ต้องเปลี่ยนไปใช้ตัวกลาง เช่น Redis pub/sub (frontend ไม่ต้องแก้)
- **restart backend แล้ว stream ทุกเส้นหลุด:** EventSource ต่อใหม่เองและได้ `ready` จึงโหลดใหม่ ไม่มีอะไรตกหล่น
- **Swagger "Try it out" ของ `InboxStream` จะค้าง** เพราะ stream ไม่จบ ให้ใช้ `curl -N` แทน
- **ตอน dev บน `localhost` (HTTP/1.1)** เบราว์เซอร์เปิดได้ 6 connection ต่อ origin ถ้าเปิดหลายแท็บที่มี stream ค้าง request อื่นจะค้าง
- **endpoint เดิม `GET /Services/GetPendingServices` และ `GET /Services/GetInProcessServices` ยังอยู่** และใช้เงื่อนไขคนละแบบ:
  - ไม่ดู `isReceived`
  - ไม่แบ่งหน้า
  - หน้าแรกให้ใช้ `/Home/*`

---

## 7. ทดสอบเร็ว ๆ

```bash
# รายการ (ต้องมี token จาก POST /Auth/login)
curl "http://localhost:5018/Home/GetInboxServices" -H "Authorization: Bearer <token>"
curl "http://localhost:5018/Home/GetProcessingServices?PageNumber=1&PageSize=10&Lang=en" -H "Authorization: Bearer <token>"

# stream (-N = ไม่ buffer) ควรได้ retry/ready ทันที แล้ว ": ping" ทุก 25 วินาที
curl -N "http://localhost:5018/Home/InboxStream" -H "Authorization: Bearer <token>"
```

ทดสอบรายการรับให้ครบวงจรต้องใช้ **2 บัญชี** (ผู้โอน A และผู้รับ B):

1. **B** เปิด stream หรือเปิดหน้าแรกค้างไว้
2. **A** เพิ่มการดำเนินการในเรื่องที่ยังไม่ปิด โดยเลือก **ผู้รับ = B** และ **สถานะงาน = ปิด**
3. **B** ต้องได้ `event: inbox` (`assigned`) และ `GetInboxServices` ต้องมีเรื่องนั้น
4. **B** เรียก `POST /Services/ReceiveService?id=<id>` ต้องได้ `received` และเรื่องหายจากรายการรับ

ตัวอย่าง request อยู่ใน [ContactTogetherApi.http](../ContactTogetherApi.http) (หัวข้อ Home / Receive)
กรณีทดสอบที่ละเอียดกว่านี้ดู [InboxStream.md](InboxStream.md) ข้อ 6 และ handoff 26 พ.ย. ข้อ "วิธีทดสอบด่วน"
