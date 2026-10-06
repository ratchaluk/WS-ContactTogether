# Handoff: Report API ใช้ `_0BaseReturn` + แก้การกรองช่วงวันที่

วันที่ 2026-10-06 · branch `indy`

## สรุปสั้น

`ReportController` ทั้ง 6 รายงาน (`POST /Report/GetReport01`–`06`) เปลี่ยนมาตอบกลับด้วย envelope
`_0BaseReturn` และแยก HTTP status ตามกรณี และแก้บั๊กกรองช่วงวันที่ที่ทำให้ช่วงปี 1999 ยังได้ข้อมูลปี 2026
ทุกรายงานทดสอบกับฐานข้อมูลจริงแล้ว **commit ครบทั้ง 01–06 แล้ว ยังไม่ได้ push และยังไม่ได้เปิด PR**

## สถานะไฟล์

ทุกอย่าง commit อยู่บน branch `indy` (ยังไม่ push) เหลือแค่ไฟล์ `HANDOFF.md` นี้ที่ยังไม่ได้ commit

| commit | ไฟล์ | เนื้อหา |
|---|---|---|
| `c232b8d` | `Helper/0BaseReturn.cs` | เพิ่ม namespace `ContactTogetherApi.Helper` และ factory `Success(result, message)` / `Fail(message)` |
| `c232b8d` | `Controllers/ReportController.cs` | GetReport01 + helper `TryBuildDateRange` |
| `c0af557` | `CLAUDE.md` | อัปเดตให้ตรงกับ controller และ convention ปัจจุบัน |
| `d008544` | `.agents/`, `.claude/skills/dotnet-*`, `skills-lock.json` | เพิ่ม skill dotnet-best-practices และ dotnet-design-pattern-review |
| `cff55da` | `Controllers/ReportController.cs` | GetReport02–06 ใช้รูปแบบเดียวกับ 01 |
| `cff55da` | `Dtos/RequestModel.cs` | `ServiceRequestReportRequestMainOrganization` สืบทอดจาก `ServiceRequestReportRequest` (JSON ที่รับเข้าไม่เปลี่ยน) |
| `cff55da` | `ContactTogetherApi.http` | ตัวอย่าง request ของ 02–06 |

diff ของ `cff55da` ใน `ReportController.cs` ดูใหญ่เพราะ query ของ 02–04 ถูกย่อหน้าเข้าไปอยู่ใน `try`
ให้ใช้ `git show -w cff55da` เพื่อดูเฉพาะส่วนที่เปลี่ยนจริง

`.claude/skills/dotnet-*` บนดิสก์เป็น symlink ไปที่ `.agents/skills/` แต่ git บน Windows commit เป็นไฟล์ธรรมดา
เนื้อหา `SKILL.md` จึงซ้ำกันสองที่ใน repo

## รูปแบบการตอบกลับ

ไม่มีฟิลด์ `StatusCode` ใน body เพราะ status มากับ HTTP response อยู่แล้ว (ผู้ใช้ตัดสินใจ)

| กรณี | HTTP | `callAPIStatus` | `callAPIStatusMessage` | `result` |
|---|---|---|---|---|
| พบข้อมูล | 200 | `true` | `พบข้อมูล N รายการ` | รายการ |
| ไม่พบข้อมูล | 200 | `true` | `ไม่พบข้อมูล` | `[]` |
| ไม่ส่ง `P_Start`/`P_Finish` | 400 | `false` | `กรุณาระบุ P_Start และ P_Finish` | `null` |
| วันที่ไม่ใช่ `MM/dd/yyyy` | 400 | `false` | `P_Start ต้องอยู่ในรูปแบบ MM/dd/yyyy` | `null` |
| เวลาไม่ใช่ `HH:mm:ss` หรือ `HH:mm` | 400 | `false` | `P_Time_Start ต้องอยู่ในรูปแบบ HH:mm:ss` | `null` |
| วันเริ่ม > วันสิ้นสุด | 400 | `false` | `P_Start ต้องไม่มากกว่า P_Finish` | `null` |
| GetReport04 ไม่ส่ง `P_MainOrg` | 400 | `false` | `กรุณาระบุ P_MainOrg` | `null` |
| exception อื่น (DB ฯลฯ) | 500 | `false` | `เกิดข้อผิดพลาดภายในระบบ` (log ไว้ ไม่ส่ง `ex.Message` ออกไป) | `null` |

ทุก action รับ `CancellationToken` และมี `[ProducesResponseType(typeof(_0BaseReturn), ...)]` สำหรับ 200/400/500

## บั๊กกรองช่วงวันที่ที่แก้ไป

**อาการ:** ส่ง `01/01/1999`–`12/01/1999` แล้วได้ข้อมูลปี 2026 ออกมา 184 รายการ

**สาเหตุ:** คอลัมน์วันที่เป็นข้อความรูปแบบ `MM/dd/yyyy` และโค้ดเดิมเทียบด้วย `string.Compare` ซึ่ง SQL Server
เทียบทีละตัวอักษร เดือนจึงถูกเทียบก่อนปี เช่น `"07/01/2026"` อยู่ระหว่าง `"01/01/1999"` กับ `"12/01/1999"`

**วิธีแก้:** ใน query ใช้ `SUBSTRING` เรียงค่าใหม่ให้ปีขึ้นก่อน แล้วเทียบกับค่าที่ `TryBuildDateRange`
จัดรูปแบบเดียวกัน (EF แปลงเป็น `SUBSTRING` ฝั่ง SQL และแถวที่เป็น null ถูกตัดออกเอง)

| รายงาน | คอลัมน์ที่กรอง | รูปแบบในฐานข้อมูล | key ที่ใช้เทียบ |
|---|---|---|---|
| 01–04 | `TblService.Created` | `MM/dd/yyyy HH:mm` | `yyyyMMddHHmm` |
| 05–06 | `TblContact.ContactStart` | `MM/dd/yyyy` (มีแค่วันที่) | `yyyyMMdd` (ใช้ 8 ตัวแรกของ key) |

ตรวจรูปแบบในฐานข้อมูลด้วย `TRANSLATE(col,'0123456789','9999999999')` แล้ว: `TblService.Created` 184/184 แถว
และ `TblContact.ContactStart` 1,200/1,200 แถวเป็นรูปแบบเดียวกันทั้งหมด ส่วนตัวแรกของ `ContactStart` ไม่เกิน 12
จึงเป็น `MM/dd` ไม่ใช่ `dd/MM`

**ข้อจำกัด:** ถ้าในอนาคตมีแถวที่ไม่ได้เติม 0 นำหน้า (เช่น `7/1/2026`) แถวนั้นจะถูกกรองผิด
และ SQL Server ใช้ index บนคอลัมน์นี้ไม่ได้ (ต้องสแกนทั้งตาราง)

## ผลลัพธ์ที่เปลี่ยนจากเดิม (ควรแจ้งฝั่งที่ใช้รายงาน)

- **ทุกรายงาน:** response เปลี่ยนจาก array เปล่า ๆ เป็น `{ callAPIStatus, callAPIStatusMessage, result }`
  ฝั่ง client ต้องอ่านข้อมูลจาก `result`
- **ทุกรายงาน:** ช่วงวันที่ถูกต้องแล้ว ช่วงที่ข้ามปีหรือคนละปีจะได้จำนวนแถวต่างจากเดิม
- **05/06:** โค้ดเดิมไม่นับวันเริ่มต้นของช่วง (`"07/01/2026"` < `"07/01/2026 00:00"`) ตอนนี้นับแล้ว
- **05/06:** `P_Time_Start`/`P_Time_Finish` ไม่มีผล เพราะ `ContactStart` ไม่มีเวลา
- **เวลา:** รับทั้ง `HH:mm:ss` และ `HH:mm` เหมือนที่ `TimeSpan.Parse` เดิมรับ

## ผลทดสอบ

build ไปไว้ใน scratchpad แล้วรันแยกบนพอร์ต 5099 (ไม่ได้แตะแอปที่เปิดอยู่บนพอร์ต 5018)
แล้วยิงกับฐานข้อมูลจริง

| รายงาน | ปี 1999 | ทั้งปี 2026 | ทั้งปี 2014 | input ผิด |
|---|---|---|---|---|
| 01 | ไม่พบข้อมูล | 184 (ก.ค. 171 + ส.ค. 13) | – | 400 |
| 02 (`CallBack = "D"`) | ไม่พบข้อมูล | 10 | – | 400 |
| 03 (`CallBack = "Y"`) | ไม่พบข้อมูล | 31 | ไม่พบข้อมูล | 400 |
| 04 (org `0D26AFEE79DD44C680D3E2F606B5C52A`) | ไม่พบข้อมูล | 24 | – | 400 |
| 05 / 06 | ไม่พบข้อมูล | 2 | 63 | 400 |

จำนวนแถวของ 05/06 ตรงกับ `COUNT(DISTINCT ContactStart)` ต่อปีที่ query ตรงจากฐานข้อมูล

**ยังไม่ได้ทดสอบ:** กรณี 500 (ต้องทำให้ฐานข้อมูลล่ม) ตรวจแค่ว่าโค้ดถูกต้อง

## งานที่เหลือ

1. **Push branch `indy` และเปิด PR ไป `main`** ตามขั้นตอนใน README (commit ครบแล้ว)
   ก่อน commit ล่าสุดได้ build ไปไว้ใน scratchpad แล้ว ไม่มี error
2. **Restart แอปบนพอร์ต 5018** แอปที่เปิดไว้ (PID 27220) ยังรันโค้ดเก่า และตอนแอปเปิดอยู่ `dotnet build`
   ในโปรเจกต์จะติด error MSB3021/MSB3027 ตอน copy ไฟล์ (`bin/` ถูกล็อก) ซึ่งไม่ใช่ error ของโค้ด
3. **อัปเดต `CLAUDE.md`** หัวข้อ "Dates are mixed" ยังเขียนว่ารายงานเทียบวันที่ด้วย `string.Compare` แบบผิด
   ซึ่งแก้แล้ว ควรเปลี่ยนเป็นบอกวิธีเทียบด้วย key แทน และเพิ่มว่า `TblContact.ContactStart` เป็น `MM/dd/yyyy`
4. **คำถามที่ต้องถามผู้ใช้หรือเจ้าของรายงาน (ยังไม่ได้แก้):**
   - **05/06:** query จัดกลุ่มตาม `ContactStart` แต่ไม่ได้ส่งวันที่ออกมา ผลลัพธ์จึงไม่บอกว่าแต่ละแถวเป็นของวันไหน
     ใน `Dtos/ResponseModel.cs` มี `ServiceRequestReportContact` (มีฟิลด์ `ContactStart`) ที่ยังไม่ได้ใช้
     ถ้าจะเพิ่มต้องเปลี่ยนรูปแบบ response
   - **04:** `P_MainOrg` ถูกเทียบกับ `org.Id` (หน่วยงานย่อยของ SR) ไม่ใช่ `org2.Id` (หน่วยงานหลัก)
     ชื่อกับเงื่อนไขไม่ตรงกัน
5. **ข้อควรรู้อื่น**
   - `[Authorize]` ของ `ReportController` ยังถูก comment ไว้ ทุกรายงานเรียกได้โดยไม่ต้อง login
   - 400 อัตโนมัติจาก `[ApiController]` (JSON พังหรือไม่มี body) ยังเป็น `ProblemDetails` ไม่ใช่ `_0BaseReturn`
     ถ้าต้องการให้เป็น envelope ต้องตั้ง `InvalidModelStateResponseFactory` ใน `Program.cs` ซึ่งกระทบทุก controller
   - warning CS8601 ที่ `SrReferenceLink` ใน GetReport01 มีมาก่อนแล้ว (DTO ประกาศเป็น non-nullable)

## วิธีตรวจซ้ำ

```powershell
dotnet build
dotnet run
```

แล้วยิงตัวอย่าง `Report 01`–`06` ใน `ContactTogetherApi.http` หรือผ่าน `/swagger`
ตัวอย่างที่ใช้ช่วง ก.ค. 2026 และปี 2014 มีข้อมูลจริงในฐานข้อมูลตอนนี้
