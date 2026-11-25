# Spec: API รายการรับ (Inbox) และ งานดำเนินการ (Processing)

> ย้ายฟีเจอร์จากหน้า Home ของระบบ GCC เดิม (ASP.NET Web Forms) มาเป็น .NET Core Web API
> ฐานข้อมูลเดิม: MySQL schema `db_gcc_new` (ใช้ตารางเดิม ไม่ต้องเปลี่ยน schema)
> เอกสารนี้สรุปจากการอ่านโค้ดระบบเดิม (`2022GCC_Production`) — ส่วนที่ยังต้องตัดสินใจอยู่ในหัวข้อ [11. คำถามที่ต้องตอบก่อนเริ่ม](#11-คำถามที่ต้องตอบก่อนเริ่ม)

---

## 1. ภาพรวม

ในหน้าแรกหลังล็อกอิน กล่อง "รายการให้บริการ" มี 2 แท็บ ใช้ตารางแสดงผลแบบเดียวกัน แต่เงื่อนไขดึงข้อมูลต่างกัน

| แท็บ | ความหมายทางธุรกิจ |
|---|---|
| **รายการรับ** (Inbox) | SR ที่ **คนอื่นโอนมาให้ฉัน** และฉัน **ยังไม่ได้เปิดดู/รับงาน** |
| **งานดำเนินการ** (Processing) | SR ที่ **ฉันเป็นเจ้าของ** และ **ยังไม่ปิด** |

API ที่ต้องทำ:

| # | Method | Route | หน้าที่ | ความสำคัญ |
|---|---|---|---|---|
| 1 | GET | `api/service-requests/inbox` | รายการรับ (แบ่งหน้า) | ต้องมี |
| 2 | GET | `api/service-requests/processing` | งานดำเนินการ (แบ่งหน้า) | ต้องมี |
| 3 | POST | `api/service-requests/{serviceId}/receive` | กดรับงาน (ทำให้หายจากรายการรับ) | ต้องมี (ดูหัวข้อ 4.3) |

---

## 2. ตารางที่เกี่ยวข้อง

แสดงเฉพาะคอลัมน์ที่ใช้ (ชนิดข้อมูลจาก EDMX ของระบบเดิม) ค่า flag ทุกตัวเป็น `char(1)` ค่า `'T'` / `'F'`

### `tbl_service` — ตัว SR
| คอลัมน์ | ชนิด | ใช้ทำอะไร |
|---|---|---|
| `ID` (PK) | varchar(32) | GUID ของ SR |
| `Code` | varchar(32) | รหัสบริการ (เช่น `69112500001`) ใช้เรียงลำดับ |
| `Category_ID` | varchar(32) NOT NULL | FK → `tbl_category.ID` |
| `Status_ID` | varchar(32) NOT NULL | FK → `tbl_status.ID` |
| `Owner_ID` | varchar(32) NOT NULL | เจ้าของ SR (employee ID) |
| `Date_Opened` | datetime NOT NULL | วันเปิดบริการ |
| `Call_Back` | varchar(32) NOT NULL | `"N"` = ไม่ต้องโทรกลับ, ค่าอื่น = ต้องโทรกลับ |
| `Summary` | varchar(1024) NOT NULL | สาระสำคัญ |
| `Is_Enable` | char(1) NOT NULL | `'T'` = ใช้งาน, `'F'` = ถูกลบ |

### `tbl_activity` — กิจกรรม/การโอนงานของ SR
PK เป็น composite: `Service_ID` + `Line` + `Is_Enable`

| คอลัมน์ | ชนิด | ใช้ทำอะไร |
|---|---|---|
| `Service_ID` | varchar(32) NOT NULL | FK → `tbl_service.ID` |
| `Line` | int NOT NULL | ลำดับ activity ใน SR (1 = activity แรกตอนสร้าง SR) |
| `Status_ID` | varchar(32) NOT NULL | FK → `tbl_status.ID` (สถานะของ activity ไม่ใช่ของ SR) |
| `Owner_ID` | varchar(32) NULL | ผู้รับมอบ activity |
| `Is_Received` | char(1) NOT NULL | `'F'` = ยังไม่รับงาน, `'T'` = รับแล้ว |
| `Is_Enable` | char(1) NOT NULL | `'T'` / `'F'` |

### `tbl_status` — สถานะ (ใช้ทั้งกับ SR และ activity)
| คอลัมน์ | ชนิด | ใช้ทำอะไร |
|---|---|---|
| `ID` (PK) | varchar(32) | |
| `Name_TH` | varchar(128) NOT NULL | ชื่อสถานะไทย (เช่น "อยู่ระหว่างดำเนินการ") |
| `Name_EN` | varchar(128) NULL | ชื่ออังกฤษ (อาจว่าง) |
| `Is_Type` | varchar(32) NOT NULL | **ประเภทสถานะ:** `PENDING` = ยังเปิดอยู่, `CLOSE` = ปิด/เสร็จ |

### `tbl_category` — ประเภทปัญหา / ประเภท activity
| คอลัมน์ | ชนิด | ใช้ทำอะไร |
|---|---|---|
| `ID` (PK) | varchar(32) | |
| `Name_TH` | varchar(128) NOT NULL | ชื่อไทย (เช่น "Claim", "Contact Info") |
| `Name_EN` | varchar(128) NULL | ชื่ออังกฤษ (อาจว่าง) |
| `Remark` | varchar(2048) | ประเภท activity ที่ `Remark = 'TRANSFER'` คือ "โอนงาน" |

### ค่า config ที่ใช้ (ระบบเดิมอยู่ใน `appSettings.config`)
| key เดิม | ค่า | ใช้ทำอะไร |
|---|---|---|
| `status_is_type_open` | `PENDING` | `Is_Type` ของสถานะ "ยังเปิดอยู่" |
| (hard-code ในโค้ด) | `CLOSE` | `Is_Type` ของสถานะ "ปิด" |

ให้ย้ายสองค่านี้ไปไว้ใน `appsettings.json` (ดูหัวข้อ 6.1) ห้าม hard-code ใน query

---

## 3. กฎทางธุรกิจ (Business Rules)

### 3.1 รายการรับ (Inbox)

SR จะอยู่ในรายการรับของพนักงาน `@emp` เมื่อ **ตรงทุกข้อ**:

1. `tbl_service.Is_Enable = 'T'`
2. สถานะของ SR: `tbl_status.Is_Type = 'PENDING'`
3. มี **activity อย่างน้อย 1 แถว** ของ SR นั้นที่
   - `tbl_activity.Is_Enable = 'T'`
   - `tbl_activity.Owner_ID = @emp`
   - `tbl_activity.Is_Received = 'F'`
   - สถานะของ activity: `tbl_status.Is_Type = 'CLOSE'` (หมายถึงขั้นตอนโอนงานทำเสร็จแล้ว)

SQL อ้างอิง (ใช้ตรวจผลกับ API ได้):
```sql
SELECT s.ID, s.Code, s.Date_Opened, cat.Name_TH AS Category, st.Name_TH AS Status, s.Summary, s.Call_Back
FROM tbl_service s
JOIN tbl_status   st  ON st.ID  = s.Status_ID
JOIN tbl_category cat ON cat.ID = s.Category_ID
WHERE s.Is_Enable = 'T'
  AND st.Is_Type = 'PENDING'
  AND s.ID IN (
      SELECT a.Service_ID
      FROM tbl_activity a
      JOIN tbl_status sa ON sa.ID = a.Status_ID
      WHERE a.Is_Enable = 'T'
        AND sa.Is_Type = 'CLOSE'
        AND a.Owner_ID = @emp
        AND a.Is_Received = 'F')
ORDER BY s.Code DESC;
```

### 3.2 งานดำเนินการ (Processing)

SR จะอยู่ในงานดำเนินการของ `@emp` เมื่อ **ตรงทุกข้อ**:

1. `tbl_service.Is_Enable = 'T'`
2. `tbl_service.Owner_ID = @emp`
3. สถานะของ SR: `tbl_status.Is_Type = 'PENDING'`

```sql
SELECT s.ID, s.Code, s.Date_Opened, cat.Name_TH AS Category, st.Name_TH AS Status, s.Summary, s.Call_Back
FROM tbl_service s
JOIN tbl_status   st  ON st.ID  = s.Status_ID
JOIN tbl_category cat ON cat.ID = s.Category_ID
WHERE s.Is_Enable = 'T'
  AND s.Owner_ID = @emp
  AND st.Is_Type = 'PENDING'
ORDER BY s.Code DESC;
```

### 3.3 การรับงาน (Receive)

เมื่อพนักงาน `@emp` รับงาน SR `@sid` ให้อัปเดต **ทุก** activity ที่ตรงเงื่อนไข:
```sql
UPDATE tbl_activity
SET Is_Received = 'T'
WHERE Service_ID = @sid
  AND Owner_ID   = @emp
  AND Is_Received = 'F'
  AND Is_Enable  = 'T';
```
ระบบเดิมทำขั้นตอนนี้ **อัตโนมัติเมื่อเปิดหน้ารายละเอียด SR** (ไม่มีปุ่มกดรับ) และ **ไม่ได้อัปเดต** `Updated` / `Updated_By`

### 3.4 การเรียงลำดับและแบ่งหน้า
- เรียงตาม `Code` จากมากไปน้อย (`ORDER BY Code DESC`)
- ค่าเริ่มต้น 10 แถวต่อหน้า
- ให้แบ่งหน้าที่ฝั่ง DB (`Skip/Take` บน `IQueryable`) — ระบบเดิมดึงทั้งหมดมาแบ่งในหน่วยความจำ ไม่ต้องทำตาม

### 3.5 ค่าที่คำนวณเพื่อแสดงไอคอน (ระบบเดิมคำนวณที่หน้าจอ)
| ค่า | สูตร |
|---|---|
| `ElapsedHours` | `round((ตอนนี้ - Date_Opened).TotalHours)` |
| `IsOverDue` | `(ตอนนี้ - Date_Opened).TotalMinutes >= 10080` (7 วัน) |
| `RequireCallBack` | `Call_Back != "N"` |

ไอคอนเดิม: ไม่เกิน 7 วัน = `time_in_due` / `callback_in_due`, เกิน 7 วัน = `time_over_due` / `callback_over_due`

### 3.6 ภาษา
- ระบบเดิม: รายการรับใช้ภาษาตามผู้ใช้ทั้งประเภทและสถานะ แต่งานดำเนินการใช้ชื่อสถานะ **ภาษาไทยเสมอ** (ไม่สม่ำเสมอ)
- API ใหม่: ใช้ภาษาตาม query `lang` (`th` ค่าเริ่มต้น / `en`) ทั้งสองคอลัมน์ ถ้า `Name_EN` เป็น `NULL` ให้ใช้ `Name_TH` แทน

---

## 4. วงจรของ SR (เพื่อเข้าใจว่าข้อมูลเปลี่ยนเมื่อไหร่)

### 4.1 งานเข้ารายการรับได้อย่างไร (การโอนงาน — นอกขอบเขต API ชุดนี้)
1. ผู้ใช้เพิ่ม activity ในหน้า SR โดยเลือกประเภท activity ที่ `tbl_category.Remark = 'TRANSFER'`
2. เลือกพนักงานผู้รับ และเลือกสถานะ activity
3. ระบบบันทึก `tbl_activity`: `Owner_ID` = ผู้รับ, `Is_Received` = `'F'` (ถ้าผู้รับไม่ใช่ตัวเอง, ถ้าเป็นตัวเอง = `'T'`)
4. ถ้าสถานะ activity เป็นประเภท `CLOSE` ระบบเปลี่ยน `tbl_service.Owner_ID` = ผู้รับด้วย

### 4.2 ตัวอย่าง: พนักงาน A โอนงานให้ B

| ขั้นตอน | รายการรับ ของ B | งานดำเนินการ ของ A | งานดำเนินการ ของ B |
|---|---|---|---|
| A สร้าง SR (Owner = A, สถานะ PENDING) | – | ✔ | – |
| A โอนให้ B (activity สถานะ CLOSE) | ✔ | หายไป | ✔ |
| B รับงาน (`Is_Received` → `'T'`) | หายไป | – | ✔ |
| ปิด SR (สถานะประเภท CLOSE) | – | – | หายไป |

หมายเหตุ: ช่วงที่ B ยังไม่รับงาน SR จะอยู่ **ทั้งสองแท็บ** ของ B (พฤติกรรมเดิม)

### 4.3 ทำไมต้องมี endpoint รับงาน
ถ้าไม่มีการตั้ง `Is_Received = 'T'` งานจะค้างในรายการรับตลอด ระบบเดิมตั้งค่าตอนเปิดหน้ารายละเอียด SR ใน API ใหม่ให้ frontend เรียก `POST .../receive` ตอนเปิดดูรายละเอียด (หรือมีปุ่ม "รับงาน" — ดูคำถามข้อ 2)

---

## 5. API Contract

ทุก endpoint ตอบกลับผ่าน `ApiResponse<T>` และ **ต้องล็อกอิน** — employee ID มาจาก token ของผู้ใช้ ห้ามรับ employee ID เป็น parameter (กันการดูงานของคนอื่น)

### 5.1 GET `api/service-requests/inbox`
### 5.2 GET `api/service-requests/processing`

Query parameters (เหมือนกันทั้งสอง endpoint):

| ชื่อ | ชนิด | ค่าเริ่มต้น | กฎ |
|---|---|---|---|
| `page` | int | 1 | ≥ 1 |
| `pageSize` | int | 10 | 1–100 |
| `lang` | string | `th` | `th` หรือ `en` |

Response `200 OK`:
```json
{
  "success": true,
  "message": null,
  "data": {
    "items": [
      {
        "id": "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6",
        "code": "69112500001",
        "dateOpened": "2026-11-25T09:51:08",
        "categoryName": "Claim",
        "statusName": "อยู่ระหว่างดำเนินการ",
        "summary": "ร้องเรียน testestsetset",
        "callBack": "N",
        "requireCallBack": false,
        "elapsedHours": 3,
        "isOverDue": false
      }
    ],
    "page": 1,
    "pageSize": 10,
    "totalCount": 11,
    "totalPages": 2
  }
}
```
- ไม่มีข้อมูล: `items = []`, `totalCount = 0`, `totalPages = 0` (ยังเป็น 200)
- `summary` ส่งข้อความเต็ม ให้ frontend ตัดคำเอง (ระบบเดิมตัดที่ 50 ตัวอักษรตามช่องว่าง)
- `dateOpened` ส่งเป็น ISO 8601 ให้ frontend แปลงเป็นปี พ.ศ. ตอนแสดง

Error:
| HTTP | กรณี |
|---|---|
| 400 | `page`/`pageSize`/`lang` ไม่ถูกต้อง |
| 401 | ไม่ได้ล็อกอิน / token ไม่มี employee ID |

### 5.3 POST `api/service-requests/{serviceId}/receive`

ไม่มี body

| HTTP | กรณี | `data` |
|---|---|---|
| 200 | อัปเดตแล้ว | `{ "updatedCount": 1 }` |
| 200 | ไม่มี activity ที่ต้องรับ (เคยรับแล้ว / ไม่ใช่ผู้รับ) | `{ "updatedCount": 0 }` — ไม่ถือเป็น error (เรียกซ้ำได้) |
| 401 | ไม่ได้ล็อกอิน | |

---

## 6. โครงสร้างโค้ด (ตามมาตรฐาน Controller → Service → Repository → DTO)

> ชื่อ Entity/Navigation ด้านล่างสมมติว่า scaffold จาก MySQL ได้ `TblService`, `TblActivity`, `TblStatus`, `TblCategory` และ navigation `Status`, `Category` — ปรับตามที่ scaffold ได้จริง

### 6.1 Config
```json
// appsettings.json
"ServiceStatus": {
  "OpenIsType": "PENDING",
  "CloseIsType": "CLOSE"
}
```
```csharp
// Common/ServiceStatusOptions.cs
public class ServiceStatusOptions
{
    public string OpenIsType { get; set; } = "PENDING";
    public string CloseIsType { get; set; } = "CLOSE";
}
```

### 6.2 DTOs
```csharp
// DTOs/ServiceRequest/ServiceRequestListQueryDto.cs
public class ServiceRequestListQueryDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    [RegularExpression("^(th|en)$")]
    public string Lang { get; set; } = "th";
}

// DTOs/ServiceRequest/ServiceRequestListItemDto.cs
public class ServiceRequestListItemDto
{
    public string Id { get; set; } = default!;
    public string Code { get; set; } = default!;
    public DateTime DateOpened { get; set; }
    public string CategoryName { get; set; } = default!;
    public string StatusName { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public string CallBack { get; set; } = default!;
    public bool RequireCallBack { get; set; }
    public int ElapsedHours { get; set; }
    public bool IsOverDue { get; set; }
}

// DTOs/ServiceRequest/ReceiveResultDto.cs
public class ReceiveResultDto
{
    public int UpdatedCount { get; set; }
}

// Common/PagedResultDto.cs
public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

### 6.3 Repository
```csharp
// Repositories/IServiceRequestRepository.cs
public interface IServiceRequestRepository
{
    Task<(List<TblService> Items, int TotalCount)> GetInboxAsync(
        string employeeId, int page, int pageSize, CancellationToken ct = default);

    Task<(List<TblService> Items, int TotalCount)> GetProcessingAsync(
        string employeeId, int page, int pageSize, CancellationToken ct = default);

    Task<int> MarkReceivedAsync(string serviceId, string employeeId, CancellationToken ct = default);
}

// Repositories/ServiceRequestRepository.cs
public class ServiceRequestRepository : IServiceRequestRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ServiceStatusOptions _status;

    public ServiceRequestRepository(ApplicationDbContext context, IOptions<ServiceStatusOptions> status)
    {
        _context = context;
        _status = status.Value;
    }

    public Task<(List<TblService> Items, int TotalCount)> GetInboxAsync(
        string employeeId, int page, int pageSize, CancellationToken ct = default)
    {
        var inboxServiceIds = _context.TblActivities
            .Where(a => a.IsEnable == "T"
                     && a.OwnerId == employeeId
                     && a.IsReceived == "F"
                     && a.Status.IsType == _status.CloseIsType)
            .Select(a => a.ServiceId);

        var query = _context.TblServices
            .Where(s => s.IsEnable == "T"
                     && s.Status.IsType == _status.OpenIsType
                     && inboxServiceIds.Contains(s.Id));

        return PageAsync(query, page, pageSize, ct);
    }

    public Task<(List<TblService> Items, int TotalCount)> GetProcessingAsync(
        string employeeId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.TblServices
            .Where(s => s.IsEnable == "T"
                     && s.OwnerId == employeeId
                     && s.Status.IsType == _status.OpenIsType);

        return PageAsync(query, page, pageSize, ct);
    }

    public async Task<int> MarkReceivedAsync(string serviceId, string employeeId, CancellationToken ct = default)
    {
        var activities = await _context.TblActivities
            .Where(a => a.ServiceId == serviceId
                     && a.OwnerId == employeeId
                     && a.IsReceived == "F"
                     && a.IsEnable == "T")
            .ToListAsync(ct);

        foreach (var a in activities)
            a.IsReceived = "T";

        await _context.SaveChangesAsync(ct);
        return activities.Count;
    }

    private static async Task<(List<TblService> Items, int TotalCount)> PageAsync(
        IQueryable<TblService> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(s => s.Status)
            .Include(s => s.Category)
            .OrderByDescending(s => s.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);
        return (items, total);
    }
}
```

### 6.4 Service
```csharp
// Services/IServiceRequestService.cs
public interface IServiceRequestService
{
    Task<PagedResultDto<ServiceRequestListItemDto>> GetInboxAsync(
        string employeeId, ServiceRequestListQueryDto query, CancellationToken ct = default);

    Task<PagedResultDto<ServiceRequestListItemDto>> GetProcessingAsync(
        string employeeId, ServiceRequestListQueryDto query, CancellationToken ct = default);

    Task<ReceiveResultDto> ReceiveAsync(string serviceId, string employeeId, CancellationToken ct = default);
}

// Services/ServiceRequestService.cs
public class ServiceRequestService : IServiceRequestService
{
    private const int OverDueMinutes = 7 * 24 * 60; // 10080
    private readonly IServiceRequestRepository _repository;

    public ServiceRequestService(IServiceRequestRepository repository) => _repository = repository;

    public async Task<PagedResultDto<ServiceRequestListItemDto>> GetInboxAsync(
        string employeeId, ServiceRequestListQueryDto query, CancellationToken ct = default)
    {
        var (items, total) = await _repository.GetInboxAsync(employeeId, query.Page, query.PageSize, ct);
        return ToPaged(items, total, query);
    }

    public async Task<PagedResultDto<ServiceRequestListItemDto>> GetProcessingAsync(
        string employeeId, ServiceRequestListQueryDto query, CancellationToken ct = default)
    {
        var (items, total) = await _repository.GetProcessingAsync(employeeId, query.Page, query.PageSize, ct);
        return ToPaged(items, total, query);
    }

    public async Task<ReceiveResultDto> ReceiveAsync(string serviceId, string employeeId, CancellationToken ct = default)
        => new() { UpdatedCount = await _repository.MarkReceivedAsync(serviceId, employeeId, ct) };

    private static PagedResultDto<ServiceRequestListItemDto> ToPaged(
        List<TblService> items, int total, ServiceRequestListQueryDto query)
    {
        var now = DateTime.Now;
        var isEn = query.Lang == "en";
        return new PagedResultDto<ServiceRequestListItemDto>
        {
            Items = items.Select(s => Map(s, now, isEn)).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    private static ServiceRequestListItemDto Map(TblService s, DateTime now, bool isEn)
    {
        var elapsed = now - s.DateOpened;
        return new ServiceRequestListItemDto
        {
            Id = s.Id,
            Code = s.Code,
            DateOpened = s.DateOpened,
            CategoryName = isEn && !string.IsNullOrEmpty(s.Category.NameEn) ? s.Category.NameEn : s.Category.NameTh,
            StatusName = isEn && !string.IsNullOrEmpty(s.Status.NameEn) ? s.Status.NameEn : s.Status.NameTh,
            Summary = s.Summary,
            CallBack = s.CallBack,
            RequireCallBack = s.CallBack != "N",
            ElapsedHours = (int)Math.Round(elapsed.TotalHours),
            IsOverDue = elapsed.TotalMinutes >= OverDueMinutes
        };
    }
}
```

### 6.5 Controller
```csharp
// Controllers/ServiceRequestController.cs
[ApiController]
[Authorize]
[Route("api/service-requests")]
public class ServiceRequestController : ControllerBase
{
    private readonly IServiceRequestService _service;
    public ServiceRequestController(IServiceRequestService service) => _service = service;

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox([FromQuery] ServiceRequestListQueryDto query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Invalid request"));

        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return Unauthorized(ApiResponse<object>.Fail("Employee not found in token"));

        var result = await _service.GetInboxAsync(employeeId, query, ct);
        return Ok(ApiResponse<PagedResultDto<ServiceRequestListItemDto>>.Ok(result));
    }

    [HttpGet("processing")]
    public async Task<IActionResult> GetProcessing([FromQuery] ServiceRequestListQueryDto query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Invalid request"));

        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return Unauthorized(ApiResponse<object>.Fail("Employee not found in token"));

        var result = await _service.GetProcessingAsync(employeeId, query, ct);
        return Ok(ApiResponse<PagedResultDto<ServiceRequestListItemDto>>.Ok(result));
    }

    [HttpPost("{serviceId}/receive")]
    public async Task<IActionResult> Receive(string serviceId, CancellationToken ct)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return Unauthorized(ApiResponse<object>.Fail("Employee not found in token"));

        var result = await _service.ReceiveAsync(serviceId, employeeId, ct);
        return Ok(ApiResponse<ReceiveResultDto>.Ok(result));
    }

    // ชื่อ claim ขึ้นกับระบบ auth ของ API ใหม่ (ดูคำถามข้อ 1)
    private string? GetEmployeeId() => User.FindFirst("employee_id")?.Value;
}
```

### 6.6 Register DI (`Program.cs`)
```csharp
builder.Services.Configure<ServiceStatusOptions>(builder.Configuration.GetSection("ServiceStatus"));
builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
```

---

## 7. ปัญหาในระบบเดิมที่ **ห้ามลอกมา**

| # | ปัญหาเดิม | ที่ API ใหม่ต้องทำ |
|---|---|---|
| 1 | เปลี่ยนหน้าในแท็บรายการรับแล้วได้ข้อมูลงานดำเนินการแทน (ปุ่มเปลี่ยนหน้าส่ง `false` เสมอ) | แยก endpoint ชัดเจน การแบ่งหน้าไม่ข้ามไปอีกรายการ |
| 2 | ต่อ SQL ด้วย `string.Format` (เสี่ยง SQL injection) | ใช้ LINQ / parameter เท่านั้น |
| 3 | ดึงข้อมูลทั้งหมดมาแบ่งหน้าในหน่วยความจำ | แบ่งหน้าที่ DB (`Skip/Take`) และนับด้วย `CountAsync` |
| 4 | tooltip "hour(s) to due" แสดงชั่วโมงที่ **ผ่านไปแล้ว** ไม่ใช่ที่เหลือ | ส่ง `elapsedHours` (ผ่านไปแล้ว) ตั้งชื่อตรงความหมาย |
| 5 | ชื่อสถานะในงานดำเนินการเป็นไทยเสมอ | ใช้ `lang` ทั้งสอง endpoint |
| 6 | ปุ่มแก้ไข/เปิดในหน้าเดิม error ถ้า SR ไม่มี contact ใน activity แถวแรก | ไม่เกี่ยวกับ API ชุดนี้ แต่ถ้าทำ API รายละเอียด SR ต่อ ต้องเช็ก null |

---

## 8. นอกขอบเขต (Out of scope)

- การสร้าง activity / โอนงาน (หน้า `Share/ProcessPages/Activity.aspx` เดิม)
- หน้ารายละเอียด SR / แก้ไข SR
- ค่า Session ที่ระบบเดิมตั้งตอนกดแก้ไข/เปิด (`ContactIDNow`, `AccountSelect`) — API แบบ stateless ไม่มี session ให้ frontend ส่งค่าที่ต้องใช้เอง
- การแปลงปี พ.ศ. และรูปแบบวันที่ (ทำที่ frontend)

---

## 9. Test cases (Acceptance)

เตรียมพนักงาน A และ B, สถานะ SR ประเภท `PENDING` 1 ตัว, สถานะประเภท `CLOSE` 1 ตัว

| # | ขั้นตอน | ผลที่ต้องได้ |
|---|---|---|
| 1 | A สร้าง SR (Owner = A, PENDING) | processing ของ A มี SR นี้; inbox ของ A ไม่มี |
| 2 | เพิ่ม activity: Owner = B, `Is_Received = 'F'`, สถานะ activity = CLOSE, และ SR.Owner = B | inbox ของ B มี; processing ของ B มี; processing ของ A ไม่มี |
| 3 | B เรียก `POST /receive` | `updatedCount = 1`; inbox ของ B ไม่มีแล้ว; processing ของ B ยังมี |
| 4 | B เรียก `POST /receive` ซ้ำ | `updatedCount = 0`, HTTP 200 |
| 5 | เปลี่ยนสถานะ SR เป็นประเภท CLOSE | ไม่อยู่ทั้ง inbox และ processing ของ B |
| 6 | activity Owner = B, `Is_Received = 'F'` แต่สถานะ activity เป็น PENDING | **ไม่** อยู่ใน inbox ของ B (ตามกฎเดิม) |
| 7 | SR `Is_Enable = 'F'` | ไม่อยู่ในรายการใดเลย |
| 8 | มี 25 รายการ, `pageSize = 10` | หน้า 1 = 10, หน้า 3 = 5, `totalCount = 25`, `totalPages = 3`; เรียง `Code` มาก→น้อย |
| 9 | `page = 0` หรือ `pageSize = 101` หรือ `lang = jp` | HTTP 400 |
| 10 | ไม่ส่ง token | HTTP 401 |
| 11 | `lang = en` และ `Name_EN` เป็น NULL | ได้ `Name_TH` แทน |
| 12 | SR เปิดมา 7 วันพอดี / 6 วัน 23 ชม. | `isOverDue = true` / `false` |

ผลของ API ต้องตรงกับ SQL อ้างอิงในหัวข้อ 3.1 และ 3.2 เมื่อใช้ `@emp` เดียวกัน

---

## 10. อ้างอิงโค้ดระบบเดิม (`2022GCC_Production`)

| เรื่อง | ไฟล์ |
|---|---|
| หน้าจอ, ปุ่ม, ตาราง, แบ่งหน้า | `Home/Default.aspx`, `Home/Default.aspx.cs` (`GetMyService`, `btnInbox_Click`, `btnProcessing_Click`, `btnSearch_Click`, `rptService_ItemDataBound`) |
| Query รายการรับ | `ClassCode/Repository/ServiceRepo.cs` → `GetInbox` |
| Query งานดำเนินการ | `ClassCode/Repository/ServiceRepo.cs` → `GetMyService` |
| รับงานอัตโนมัติ | `Service/ViewService.aspx.cs` → `SetReceivedService` |
| สร้าง activity / ตั้ง Is_Received / เปลี่ยน Owner ของ SR | `ClassCode/Repository/ActivityRepo.cs` → `Add` |
| เลือกผู้รับ (ประเภท TRANSFER) | `Share/ProcessPages/Activity.aspx(.cs)` |
| DTO เดิม | `ClassCode/Data/DataClass.cs` → `ServiceDataTable` |
| ค่า config | `appSettings.config` → `status_is_type_open` |

---

## 11. คำถามที่ต้องตอบก่อนเริ่ม

1. **Employee ID มาจากไหน** — API ใหม่ใช้ระบบ auth แบบไหน (JWT?) และ claim ชื่ออะไรที่เก็บ `tbl_employee.ID` (ในตัวอย่างใช้ `employee_id`)
2. **การรับงาน** — frontend เรียก `receive` อัตโนมัติตอนเปิดรายละเอียด SR (เหมือนเดิม) หรือมีปุ่ม "รับงาน" ให้กดเอง
3. **ตอนรับงานควรอัปเดต `Updated` / `Updated_By` ไหม** — ระบบเดิมไม่อัปเดต
4. **SR ที่โอนมาแต่ยังไม่รับ** ควรอยู่ในงานดำเนินการด้วยไหม — ระบบเดิมแสดงทั้งสองแท็บ
5. **กฎ "สถานะ activity ต้องเป็น CLOSE"** ยังต้องการอยู่ไหม — ถ้าโอนงานโดยเลือกสถานะอื่น ผู้รับจะไม่เห็นในรายการรับ
6. **จำนวนแถวต่อหน้า** — ระบบเดิมมีตั้งค่าส่วนตัวของผู้ใช้ (`RowPerPage` ค่าเริ่ม 10) ต้องดึงจากโปรไฟล์ผู้ใช้หรือให้ frontend ส่งมา
