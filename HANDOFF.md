# Handoff: Report API (01–07) ใช้ ISO 8601 + `_0BaseReturn` + ผล audit

วันที่ 2026-10-08 · branch `Foung98`

## สรุปสั้น

`ReportController` ทั้ง 7 รายงาน (`POST /Report/GetReport01`–`07`) ตอนนี้ใช้มาตรฐานเดียวกัน:
- รับช่วงวันเวลาแบบ ISO 8601 (`yyyy-MM-ddTHH:mm:ss`)
- ตอบด้วย `_0BaseReturn` พร้อม HTTP status ที่ถูกต้อง (200 / 400 / 500)
- input ผิดทุกแบบได้ 400 ภาษาไทย รวมถึงกรณี JSON พัง

audit ทีละรายงานแล้ว เจอและแก้บั๊ก 3 ตัว:
- 04 กรองหน่วยงานผิดชั้น
- 05 นับ contact ซ้ำ
- 07 input ผิดได้ 500

และแก้โครงสร้างที่จะนับซ้ำใน 06 ทุกอย่างทดสอบกับฐานข้อมูลจริงแล้ว

**commit แล้ว ยังไม่ push และยังไม่ได้เปิด PR** (ดู "สถานะ git")

## สถานะ git

commit บน `Foung98` แยกตามเรื่อง (ใหม่สุดอยู่ล่าง):

| commit | เนื้อหา |
|---|---|
| `fix(data): remove scaffolded OnConfiguring with hard-coded password` | ลบ `OnConfiguring` ที่ฝัง connection string ไว้ใน `ApplicationDbContext` |
| `feat: open Swagger UI in the browser on startup in Development` | `Helper/SwaggerBrowserLauncher.cs`, `Program.cs`, `appsettings.Development.json` |
| `feat(report): take ISO 8601 date-times and fix report bugs` | `ReportController.cs`, `RequestModel.cs`, `Helper/BaseReturnModelStateFilter.cs`, `ContactTogetherApi.http` |
| `docs: update CLAUDE.md and HANDOFF.md for the report changes` | ไฟล์นี้และ `CLAUDE.md` |

**ไม่ได้ commit ไว้โดยตั้งใจ** (ไม่ใช่งานนี้ ให้เจ้าของตัดสินใจเอง):
- `skills-lock.json`
- `.agents/skills/find-skills/` และ `.claude/skills/find-skills/`
- `APICOREPRINCIPLES.md`

diff ของ `ReportController.cs` ดูใหญ่เพราะ query ถูกย่อหน้าใหม่ ให้ใช้ `git show -w <commit>` เพื่อดูเฉพาะส่วนที่เปลี่ยนจริง

## Request (ทุกรายงาน)

```json
{ "p_Start": "2026-07-01T08:00:00", "p_Finish": "2026-07-31T17:00:00" }
```

- เวลาไทย **ต้องมีเวลา** ไม่ใส่วินาทีก็ได้ (`<input type="datetime-local">` ไม่ส่งวินาที) **ไม่รับ offset / `Z`**
- ช่วงวันที่รวมปลาย (`Created <= p_Finish`)
- parse ด้วย `TryBuildDateTimeRange` (`InvariantCulture` และไม่แปลง timezone) ค่าภาษาหรือ timezone ของเครื่องจึงไม่มีผล
- `p_Time_Start` / `p_Time_Finish` และรูปแบบ `MM/dd/yyyy` **ถูกตัดออกทั้งหมดแล้ว** ส่งแบบเก่ามาจะได้ 400

| รายงาน | DTO | ฟิลด์เพิ่ม |
|---|---|---|
| 01, 02, 03, 05, 06 | `ServiceRequestReportDateTimeRequest` | – |
| 04 | `ServiceRequestReportRequestMainOrganization` | `p_MainOrg` (บังคับ) |
| 07 | `ServiceRequestReportRequestService` | `p_Service` (บังคับ) |

05/06 นับรายวัน และ `TblContact.ContactStart` มีแค่วันที่ จึง**ใช้แค่ส่วนวันที่** (ผู้ใช้เลือก)

ฝั่ง frontend ถ้าฟอร์มแยกช่องวันที่กับเวลา ให้ต่อเป็น `${date}T${time}` ตอนส่ง ถ้าไม่กรอกเวลาให้ใช้ `00:00:00` สำหรับเวลาเริ่ม และ `23:59:59` สำหรับเวลาสิ้นสุด
ถ้ากรอกเวลาสิ้นสุดเป็น `HH:mm` ให้เติม `:59`

## Response และ status code

ทุกรายงานตอบด้วย `{ callAPIStatus, callAPIStatusMessage, result }` แนวทาง status code ของทั้งระบบเขียนไว้ใน
`CLAUDE.md` หัวข้อ "Response envelope and status codes"

| กรณี | HTTP | `callAPIStatusMessage` |
|---|---|---|
| พบข้อมูล / ไม่พบข้อมูล | 200 | `พบข้อมูล N รายการ` / `ไม่พบข้อมูล` (`result` = `[]`) |
| ไม่ส่ง `p_Start` / `p_Finish` | 400 | `กรุณาระบุ P_Start และ P_Finish` |
| รูปแบบวันที่ผิด (รวมแบบเก่า, ไม่มีเวลา, มี `Z`) | 400 | `P_Start ต้องอยู่ในรูปแบบ yyyy-MM-ddTHH:mm:ss` |
| วันเริ่ม > วันสิ้นสุด | 400 | `P_Start ต้องไม่มากกว่า P_Finish` |
| 04 / 07 ไม่ส่ง `p_MainOrg` / `p_Service` | 400 | `กรุณาระบุ P_MainOrg` / `กรุณาระบุ P_Service` |
| JSON พัง / body ว่าง | 400 | `รูปแบบข้อมูลที่ส่งมาไม่ถูกต้อง` (ใช้ `[BaseReturnModelStateFilter]` แทน `ProblemDetails`) |
| exception อื่น | 500 | `เกิดข้อผิดพลาดภายในระบบ` (log ไว้ ไม่ส่ง `ex.Message` ออกไป) |

## สิ่งที่เปลี่ยนในแต่ละรายงาน

| รายงาน | สิ่งที่เปลี่ยน |
|---|---|
| 01 | รับ ISO (query ไม่ได้แตะ) |
| 02 (`CallBack = "D"`), 03 (`CallBack = "Y"`) | รับ ISO (query ไม่ได้แตะ) |
| 04 | **บั๊ก:** `p_MainOrg` เดิมเทียบกับ `org.Id` (หน่วยงาน**ย่อย**) ส่ง id กระทรวงจึงได้ 0 ตอนนี้ใช้ `org.Id == id \|\| org2.Id == id` ได้ SR ของหน่วยงานหลักและหน่วยงานย่อยทั้งหมด แปลง id เป็นตัวพิมพ์ใหญ่ก่อนเทียบ และส่ง `subOrgId` / `subOrgName` กลับมาด้วย (เดิมเป็น `null` เสมอ) |
| 05 | **บั๊ก:** join `TblEmployeeGroup` → `TblOrganizationGroup` ตรง ๆ (ทั้งสองตารางไม่มี key) แล้วนับแถวจาก join พนักงานที่อยู่หลายกลุ่ม claim ทำให้ contact ถูกนับซ้ำ ตอนนี้ left join กับรายชื่อพนักงาน claim (`RefGroupId` 53/77) แบบ `DISTINCT` และจัดกลุ่มด้วย `ContactStart.Value.Date` |
| 06 | **โครงสร้างเสี่ยง:** นับแถวที่กลุ่ม**ไม่ใช่** 53/77/79/68 พนักงาน 105 คนจะทำให้นับซ้ำ และ 2 คนทำให้นับทับกับ 05 นิยามใหม่ (ผู้ใช้เลือก) คือนับ contact ที่ผู้สร้างไม่อยู่ในกลุ่ม 53/77/79/68 **เลยสักกลุ่ม** contact หนึ่งตัวจึงอยู่ใน 05 หรือ 06 ได้อย่างเดียว ผลตอนนี้เท่าเดิม เพราะยังไม่มี contact ที่โดนผลกระทบ |
| 07 | **บั๊ก:** ไม่มี validation และไม่มี try/catch วันที่ผิดหรือไม่ส่ง `P_Start` ได้ 500 พร้อม stack trace, `TimeSpan.Parse("1")` อ่านเป็น 1 วัน, ไม่ส่ง `P_Service` ได้ 200 ว่าง ๆ และคืน array เปล่า ๆ ตอนนี้ใช้มาตรฐานเดียวกับรายงานอื่นทั้งหมด (query ไม่ได้แตะ) |

ลบโค้ดที่ไม่ใช้แล้ว: `TryBuildDateRange` และ DTO `ServiceRequestReportRequest`

## ผลทดสอบ (ฐานข้อมูลจริง)

build ไปไว้ใน scratchpad แล้วรันแยกบนพอร์ต 5099 โดยไม่แตะแอปที่เปิดอยู่บน 5018

| รายงาน | payload | ผล |
|---|---|---|
| 01 | ปี 2026 | 184 (ก.ค. 171 + ส.ค. 13) ไม่ซ้ำ และเรียงตาม `code` |
| 02 | ปี 2026 | 10 |
| 03 | ปี 2026 | 31 |
| 04 | `p_MainOrg` = กระทรวงการต่างประเทศ `00D1DA8C956043D5AD9B38299566A7F0` ปี 2026 | 26 (กรมการกงสุล 24 + กรมความร่วมมือระหว่างประเทศ 2) |
| 04 | `p_MainOrg` = กรมการกงสุล `0D26AFEE79DD44C680D3E2F606B5C52A` | 24 |
| 05 | ปี 2014 | 63 วัน รวม **26** (เดิม 27 ต่างกันที่ 2014-02-25 `silence` 2 → 1) |
| 06 | ปี 2014 | 63 วัน รวม 5 (เท่าเดิมทุกช่อง) |
| 07 | `p_Service` = Q&A `0168B738100C4CBAB9AA45906AD4D8E5` ปี 2026 | 147 |
| ทุกตัว | ปี 1999 | `ไม่พบข้อมูล` |
| ทุกตัว | input ผิดทุกแบบ | 400 `_0BaseReturn` |

ตัวอย่างทั้งหมดอยู่ใน `ContactTogetherApi.http` ใน Swagger ค่า default ของแต่ละฟิลด์คือค่าที่มีข้อมูลจริง

**ยังไม่ได้ทดสอบ:** กรณี 500 จาก DB ล่ม ตรวจแค่ว่าโค้ดถูกต้อง

## ผลลัพธ์ที่เปลี่ยน (ต้องแจ้งฝั่งที่ใช้รายงาน)

- **request ทุกรายงาน:** เปลี่ยนเป็น ISO 8601 และไม่มี `p_Time_*` แล้ว
- **07:** response เปลี่ยนจาก array เปล่า ๆ เป็น `_0BaseReturn` ต้องอ่านข้อมูลจาก `result`
- **04:** ความหมายของ `p_MainOrg` ถูกต้องแล้ว ส่ง id กระทรวงจะได้ทุกกรมในกระทรวง และ response มี `subOrgId` / `subOrgName`
- **05:** ยอดอาจลดลงในวันที่เคยนับซ้ำ
- **`created` / `contact_Start`:** เป็น ISO `2026-07-01T09:28:00` แต่ `srOpened` / `srClosed` ยังเป็นข้อความ `MM/dd/yyyy HH:mm`
  เพราะ `DateOpened` / `DateClosed` ยังเป็น string ใน DB

## คำถามค้าง / ยังไม่ได้ทำ

ต้องถามเจ้าของรายงาน:
1. **กรองสถานะไม่เหมือนกัน:** 01 ตัด SR ที่ status อยู่ใต้ `RefId = 1B751556C1458196BA0EB37037415A25` (inner join status)
   - 02/03/04 ไม่ตัด และ join status แบบ LEFT
   - 07 inner join แต่ไม่ตัด น่าจะลืมตอน copy มาจาก 01
2. **ความหมายของ `CallBack`** `"D"` (02) และ `"Y"` (03) ยังไม่มี comment ในโค้ด (มีค่า `"N"` ด้วย)
3. **กลุ่ม 79 (`1212`) และ 68 (`Training`, `newcomer`, `training`)** ไม่อยู่ทั้งใน 05 และ 06 ตอนนี้มี 3 contact ที่ไม่ได้อยู่ในรายงานไหนเลย
   ส่วนผู้สร้างที่ไม่มีกลุ่ม หรือมี `GroupId` ที่ไม่มีใน `TblOrganizationGroup` (พบ 139 แถว เช่น `"0"`) ถูกนับใน 06

ควรตัดสินก่อน frontend เริ่ม:
4. **ชื่อฟิลด์ response ไม่ตรงกัน** ระหว่าง `ServiceRequestReportDto` (01/07) กับ `SrCallbackResponse` (02/03/04):

   | 01 / 07 | 02 / 03 / 04 |
   |---|---|
   | `createdUName` | `createdUname` |
   | `createrName` | `creatorName` |
   | `ownerUName` | `ownerUname` |
   | `lastUpdatedUName` | `lastUpdatedUname` |
   | `updateName` | `updaterName` |
   | `gender`, `remark` | ไม่มี |

   ค่าที่หาไม่เจอก็ไม่เหมือนกัน 01/07 ส่ง `""` แต่ 02-04 ส่ง `null`
5. **05/06 response เป็น anonymous type** Swagger จึงไม่แสดงโครงสร้าง ส่วน `ServiceRequestReportContact` ใน `ResponseModel.cs` ยังไม่ได้ใช้ และใช้ชื่อฟิลด์อีกชุด
   นอกจากนี้วันที่มี contact แต่ไม่มีของทีมที่นับ จะได้แถวที่เป็น 0 ทุกช่อง และนับรวมใน "พบข้อมูล N รายการ"
6. **รวมปลายหรือไม่รวมปลาย (half-open)** และ**จำกัดความยาวช่วงวันที่** ผู้ใช้ให้ไว้ก่อน
7. **`[Authorize]`** ของ `ReportController` ยังถูก comment ไว้ เพื่อให้ทดสอบได้ ต้องเปิดก่อนขึ้น production
   และถ้าต้องการให้ 401/403 เป็น `_0BaseReturn` ต้องเขียน `OnChallenge` / `OnForbidden` เพิ่ม
8. **CORS** ยังไม่มีใน `Program.cs` ถ้า Next.js เรียก API จาก browser ต้องเพิ่ม

เรื่องโค้ด (ไม่เร่ง):
- query ของ 02/03/04 ซ้ำกันเกือบทั้งหมด ควรรวมเป็น private method ก่อนแก้ข้อ 1 หรือข้อ 4
- join `TblActivities` (01–04, 07) อาจทำให้ SR ซ้ำหลายแถวถ้ามีหลายเบอร์ติดต่อ ข้อมูลตอนนี้ยังไม่มีกรณีนี้
- 04 รองรับลำดับชั้นหน่วยงานแค่ 2 ชั้น
- `Like "%Claim%"` ขึ้นกับตัวพิมพ์ (collation `_CS_`)
- category id และรหัสกลุ่มฝังอยู่ในโค้ด
- ชื่อ method `GetReport2`…`6` ไม่ตรงกับ route `GetReport02`…`06`
- warning CS8601 ที่ `SrReferenceLink` มีมาก่อนแล้ว

## ข้อควรรู้อื่น

- **password ของ DB หลุดอยู่ใน git history** `OnConfiguring` ที่ scaffold มา (มีตั้งแต่ commit แรก) ฝัง connection string ไว้ทั้งก้อน
  ลบออกจากโค้ดแล้ว แต่ยังอยู่ใน history และบน GitHub **ต้องเปลี่ยน password ของ DB user นี้**
  ตอนนี้แอปอ่าน connection string จาก user secrets ทุกคนในทีมต้องตั้ง `ConnectionStrings:DefaultConnection` ในเครื่องตัวเอง
- **Swagger เปิดเองตอนรัน** (`dotnet run` / `dotnet run watch`) ใน Development ปิดได้ด้วย `Swagger:OpenBrowser=false`
  ส่วน `dotnet run watch` ไม่มี hot reload ถ้าต้องการ hot reload ให้ใช้ `dotnet watch run`
- **ถ้าแอปเปิดอยู่ `dotnet build` จะติด MSB3021/MSB3027** เพราะ `bin/` ถูกล็อก ไม่ใช่ error ของโค้ด
  ให้ build ไปที่อื่นด้วย `dotnet build -p:OutDir=<path>\` หรือปิดแอปก่อน
- ตรวจข้อมูลใน DB แบบอ่านอย่างเดียวด้วย `sqlcmd` ได้ ชื่อคอลัมน์ใน SQL ขึ้นกับตัวพิมพ์ (เช่น `TblContact.ID`, `TblService.Category_ID`)

## วิธีตรวจซ้ำ

```powershell
dotnet build
dotnet run
```

แล้วยิงตัวอย่าง Report 01–07 ใน `ContactTogetherApi.http` หรือผ่าน `/swagger` ตัวเลขที่ควรได้อยู่ในตาราง "ผลทดสอบ"

## ประวัติก่อนหน้า (ย่อ)

- **2026-10-06:** รายงาน 01–06 เปลี่ยนมาใช้ `_0BaseReturn` และแก้บั๊กกรองวันที่
  - ตอนนั้นคอลัมน์วันที่เป็นข้อความ `MM/dd/yyyy` และเทียบด้วย `string.Compare` แบบตัวอักษร ช่วงปี 1999 จึงได้ข้อมูลปี 2026
  - แก้ด้วยการจัดเป็น key `yyyyMMddHHmm` ก่อนเทียบ
- **2026-10-07:** DB เปลี่ยน `TblService.Created`/`Updated` และ `TblContact.ContactStart`/`ContactEnd` เป็น `datetime2`
  แล้ว re-scaffold รายงานจึงเทียบ `DateTime` ตรง ๆ และวิธีเทียบด้วย key ถูกยกเลิก
- **2026-10-07–08:** ย้ายรายงานไปรับ ISO 8601 ทีละตัว และ audit 01–07 (เอกสารนี้)
