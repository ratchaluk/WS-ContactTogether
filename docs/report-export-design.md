# ออกแบบ Export PDF / Excel สำหรับรายงาน 01–07

สถานะ: **ทำเสร็จแล้ว** (2026-11-05) บน branch `feature/report-export` ดูข้อ 0 สำหรับสิ่งที่ทำจริงและจุดที่ต่างจากแผน
อ้างอิง: `docs/report01.pdf`–`report06.pdf` (ตัวอย่างจากระบบเดิม), `Controllers/ReportController.cs`, `docs/report-api.md`

---

## 0. สิ่งที่ทำจริง (สรุปหลังลงมือ)

**ไฟล์หลัก**
- `Controllers/ReportController.Export.cs`: `ExportReport01`–`07` (`?format=xlsx|pdf`, `[Authorize]`, concurrency limiter)
- `Controllers/ReportController.cs`: query ของแต่ละรายงานย้ายไปอยู่ใน `LoadReportXXAsync` ให้ `GetReportXX` และ export ใช้ร่วมกัน
- `Reports/ReportLayouts.cs`: หัวรายงานและคอลัมน์ของทั้ง 7 รายงาน + ตัวแปลงค่าแบบระบบเดิม (`ReportValues`)
- `Reports/Export/`: `ExcelReportWriter` (SpreadCheetah), `PdfReportWriter` (QuestPDF), `HeaderGrid` (หัวตารางหลายชั้น), ฟอนต์ Sarabun ฝังใน assembly

**จุดที่ต่างจากแผนเดิม**

| เรื่อง | แผนเดิม | ทำจริง | เหตุผล |
|---|---|---|---|
| คอลัมน์ | ชุดเดียวต่อ DTO | **ตามตัวอย่างแต่ละรายงาน** | report02–06.pdf มีคอลัมน์ต่างกัน: 02 ไม่มี Channel, 03 มี `Contact_No`, 04 มีหน่วยงานคอลัมน์เดียว |
| รูปแบบวันที่ | `M/d/yyyy` | **`d/M/yyyy H:mm:ss`** | อ่านผิดตอนแรก เลข SR ยืนยันว่าเป็นวันก่อนเดือน |
| คอลัมน์ require | Y = ต้องการ, อื่น ๆ = ไม่ต้องการ | **D = สายหลุด** เพิ่ม | report02.pdf แสดง "สายหลุด" |
| รายงาน 03 `Contact_No` | – | เพิ่ม `ContactNo` ใน DTO: เบอร์จาก `TblAccountDetail` (MOBILE → HOME → OFFICE) | ตัวอย่างมีคอลัมน์นี้ ต้องตรวจกับข้อมูลจริงอีกครั้ง |
| รายงาน 04 | ต้องส่ง `p_MainOrg` | **ไม่บังคับ** ไม่เลือก = ทุกหน่วยงาน (`GetReport04` ด้วย) | ตามที่ขอ และแก้ 500 ตอนไม่ส่ง `p_SubOrg` |
| รายงาน 05/06 คอลัมน์ Chinese / Eng | – | **ไม่ใส่** | ไม่มีหมวดนี้ในฐานข้อมูล (มีแค่ 5 หมวดใต้ Kidding Call) ใส่ไปจะเป็น 0 ปลอม |
| รายงาน 05 คอลัมน์รวมกลุ่ม | คำนวณเอง | **ไม่ใส่** ตามตัวอย่าง report05.pdf | |
| Excel | ClosedXML → อ่าน DB ทีละแถว | SpreadCheetah เขียนทีละแถว แต่ **ข้อมูลโหลดเข้า RAM ก่อน** | EF Core 10 แปล query แบบนี้ทีละแถวไม่ได้ (ดู comment ในรายงาน 07) |
| PDF ขนาดใหญ่ | วัดก่อนแล้วค่อยตัดสินใจ | **สร้างทีละ 3,000 แถวแล้วรวมไฟล์** (qpdf ใน QuestPDF) เลขหน้าใส่ทีหลังด้วย overlay | ลด RAM จาก ~60 MB เหลือ ~25 MB ต่อ 1,000 แถว |
| แถวข้ามหน้า (PDF) | – | **ไม่ตัดแถวข้ามหน้า** (แต่ละแถวเป็นตาราง 1 แถว + `PreventPageBreak`) | ตัวอย่างระบบเดิมไม่ตัดแถว |
| หัวรายงาน 05/06 | ตามตัวอย่าง | ใช้ชื่อที่ผู้ใช้ให้ ("Admin-รายงานสรุป…") | ตัวอย่าง PDF ไม่มีคำว่า "Admin-รายงาน" ปรับได้ใน `ReportLayouts.cs` |

**ผลวัด** (ข้อมูลจำลองรายงาน 01): Excel 100,000 แถว ~2 วิ / ~80 MB · PDF 10,000 แถว ~16 วิ / ~350 MB ·
PDF 60,000 แถว ~75 วิ / ~1.5 GB (ส่วนที่เพิ่มตามขนาดมาจาก qpdf ตอนรวมไฟล์)

**ข้อจำกัดที่เหลือ**
- คำไทยยาวที่ไม่พอดีช่องจะถูกตัดกลางคำตอนขึ้นบรรทัดใหม่ (QuestPDF ไม่มีตัวตัดคำไทย) เหมือนระบบเดิม
- QuestPDF สร้างแต่ละ chunk แบบ synchronous ยกเลิกได้ระหว่าง chunk ไม่ใช่กลาง chunk

---

## 1. วิเคราะห์ไฟล์ตัวอย่าง `report01.pdf`

| หัวข้อ | สิ่งที่เห็นในไฟล์ | สิ่งที่จะทำในระบบใหม่ |
|---|---|---|
| ที่มา | หน้าตาแบบ Crystal Reports ของระบบเดิม | สร้างใหม่ด้วย .NET ให้หน้าตาใกล้เคียง |
| กระดาษ | แนวนอน กว้างมาก ตัวอักษรเล็ก (25 คอลัมน์ในหน้าเดียว) | **A3 แนวนอน** ตัวอักษร 7–8pt |
| หัวรายงาน | `รายละเอียดข้อมูล SR สำหรับ Supervisor ทุกสังกัด` | ใช้ชื่อเดิม |
| บรรทัดช่วงวันที่ | `ประจำวันที่ 2007-01-02 ถึง 2026-11-05 เวลา ถึง` (ส่วนเวลาว่าง เป็น bug ของระบบเดิม) | `ประจำวันที่ 2007-01-02 00:00 ถึง 2026-11-05 23:59` |
| ท้ายหน้า | `หน้าที่ 1 / 28,292` | `หน้าที่ X / Y` และเพิ่ม "พิมพ์เมื่อ … โดย …" |
| ตาราง | มีเส้นตาราง ข้อความยาวตัดบรรทัดในช่อง ไม่ตัดทิ้ง | เหมือนเดิม และหัวตารางพิมพ์ซ้ำทุกหน้า |
| ความสูงแถว | แถวที่มี Description ยาวทำให้ได้ ~3 แถวต่อหน้า | เหมือนเดิม (ตัดบรรทัดอัตโนมัติ) |
| ฟอนต์ไทย | **copy ข้อความออกมาเป็นภาษาต่างดาว** (ฟอนต์ไม่มี Unicode mapping) | ฝังฟอนต์ไทย (Sarabun) เพื่อให้ค้นหาและ copy ได้ |
| ปริมาณ | **28,292 หน้า** สำหรับช่วง 19 ปี | export ทั้งหมดตามช่วงวันที่ ไม่จำกัดแถว ต้องออกแบบให้รับไหว (ข้อ 6) |
| รูปแบบวันที่ในตาราง | `6/2/2014 13:09:46` = 6 ก.พ. 2014 (**d/M/yyyy** วันก่อนเดือน ยืนยันจากเลข SR 57020600017) | **ใช้แบบเดิม** `d/M/yyyy H:mm:ss` (ตัดสินใจแล้ว) |

### คอลัมน์ของรายงาน 01 (เทียบกับ field ใน API)

| # | หัวคอลัมน์ (ตามตัวอย่าง) | field (`ServiceRequestReportDto`) | การแปลงค่า |
|---|---|---|---|
| 1 | Rec.no | – | ลำดับแถว 1, 2, 3… |
| 2 | Opened | `SrOpened` | รูปแบบวันที่ |
| 3 | SR | `Code` | |
| 4 | Type | `SrTypeName` | |
| 5 | a_number | `ANumber` | Excel: เก็บเป็น **text** (กันเลข 0 นำหน้าหาย) |
| 6 | หน่วยงาน | `MainOrgName` | |
| 7 | หน่วยงานรอง | `SubOrgName` | |
| 8 | Summary | `Summary` | ตัดบรรทัด |
| 9 | Description | `Detail` | ตัดบรรทัด คอลัมน์กว้างที่สุด |
| 10 | Channel | `ChannelName` | |
| 11 | Status | `SrStatusName` | |
| 12 | ที่มาข้อมูล | `SrReference` | |
| 13 | Link ที่มาข้อมูล | `SrReferenceLink` | |
| 14 | Closed | `SrClosed` | รูปแบบวันที่ |
| 15 | require | `SrRequireCallBack` | `"Y"` → `ต้องการ`, อื่น ๆ (รวม `"D"`) → `ไม่ต้องการ` (ตัดสินใจแล้ว) |
| 16 | Created_by_Id | `CreatedUName` | |
| 17 | Created_by | `CreaterName` | |
| 18 | Skill | `SkillAgentCreated` | `1` → `Info`, `2` → ว่าง |
| 19 | Owner_Id | `OwnerUName` | |
| 20 | Owner | `OwnerName` | |
| 21 | Last_updated_by_Id | `LastUpdatedUName` | หัวคอลัมน์เดิมถูกตัดเป็น `Last_updated_` จะแสดงเต็ม |
| 22 | Last_updated_by | `UpdateName` | |
| 23 | Contact_Name | `ContactName` | ว่างแสดง `none` เหมือนระบบเดิม (ตัดสินใจแล้ว) |
| 24 | Gender | `Gender` | |
| 25 | Remark | `Remark` | |

`Created` ใน DTO ไม่มีในไฟล์ตัวอย่าง จะไม่ใส่ใน export

---

## 2. ขอบเขต: ทั้ง 7 รายงานแบ่งเป็น 2 แบบ

| กลุ่ม | รายงาน | รูปแบบ | กระดาษ PDF |
|---|---|---|---|
| **A. รายการ SR** | 01, 02, 03, 04, 07 | ตารางรายละเอียด 1 SR ต่อแถว (เหมือนตัวอย่าง) | A3 แนวนอน |
| **B. สรุปรายวัน** | 05, 06 | ตารางตัวเลข 1 วันต่อแถว + แถวรวมท้ายตาราง | A4 แนวนอน |

### กลุ่ม A — ความต่างของแต่ละรายงาน

| รายงาน | ชื่อหัวรายงาน (เสนอ) | คอลัมน์ | บรรทัดเงื่อนไขใต้หัวรายงาน |
|---|---|---|---|
| 01 | รายละเอียดข้อมูล SR สำหรับ Supervisor ทุกสังกัด *(ตามตัวอย่าง)* | 25 คอลัมน์ข้างบน | ช่วงวันที่ |
| 02 | รายละเอียดข้อมูล SR สำหรับ Supervisor ประเภทสายหลุด (CallBack = `D`) | 23 (ไม่มี Gender, Remark) | ช่วงวันที่ |
| 03 | รายละเอียดข้อมูล SR สำหรับ Supervisor ประเภท Require CallBack (CallBack = `Y`) | 23 | ช่วงวันที่ |
| 04 | รายละเอียดข้อมูล SR สำหรับ Supervisor แยกตามหน่วยงาน | 23 | ช่วงวันที่ + `หน่วยงาน: <ชื่อหลัก> / <ชื่อรอง>` หรือ `ทุกหน่วยงาน` (ไม่เลือกหน่วยงาน = ทุกหน่วยงาน ใช้กับ `GetReport04` ด้วย) |
| 07 | รายละเอียดข้อมูล SR สำหรับ Supervisor แยกตามประเภทบริการ *(ยังไม่มีตัวอย่าง ใช้ชื่อนี้ไปก่อน)* | 25 | ช่วงวันที่ + `ประเภทบริการ: <ชื่อ>` |

รายงาน 02/03/04 ใช้ DTO `SrCallbackResponse` ที่ชื่อ field ต่างจาก 01/07 เล็กน้อย
(`CreatorName`, `UpdaterName`, `OwnerUname`…) แต่หัวคอลัมน์ใน export ใช้ชุดเดียวกัน

### กลุ่ม B — หัวตาราง 2 ชั้น

| หัวชั้นบน | หัวชั้นล่าง (คอลัมน์) |
|---|---|
| วันที่ | (merge 2 ชั้น) |
| Kidding Call | จิตไม่ปกติ · เด็กโทรเล่น · โทรด่าหยาบคาย · เสียงเงียบ · น้ำท่วม · **รวม** |
| สัญญาณไม่ชัด | (merge 2 ชั้น) |
| สายหลุด | (merge 2 ชั้น) |
| โทรผิด | The Pizza Company · อื่น ๆ |
| Out of Scope | ขอคำปรึกษาเจ้าหน้าที่ · เครียด-สังคม · เครียด-เศรษฐกิจ · เครียด-การเมือง · เครียด-กฎหมาย · เครียด-ทรัพยากร · **รวม** |
| รวม | (merge 2 ชั้น) |

ท้ายตารางมีแถว **รวม** ที่บวกทุกคอลัมน์ตัวเลข

| รายงาน | ชื่อหัวรายงาน (เสนอ) | หมายเหตุ |
|---|---|---|
| 05 | Admin-รายงานสรุปจำนวนข้อมูลประเภท Non-Service (ตามการเลือกใช้บริการของประชาชน กด 2 Claim) | คอลัมน์ตาม DTO (ไม่มีคอลัมน์รวม Kidding / Out of Scope) |
| 06 | Admin-รายงานสรุปจำนวนข้อมูลประเภท Non-Service (ตามการเลือกใช้บริการของประชาชน Information) | มีคอลัมน์รวมกลุ่มใน DTO แล้ว |

---

## 3. สร้างไฟล์ที่ไหน: **ฝั่ง backend**

| ตัวเลือก | ข้อดี | ข้อเสีย | สรุป |
|---|---|---|---|
| **Backend (.NET) สร้างไฟล์** | ได้ข้อมูลครบทุกแถวในครั้งเดียว ฟอนต์ไทยควบคุมได้ หน้าตาเหมือนกันทุกเครื่อง | ต้องเพิ่ม library | ✅ เลือก |
| Frontend สร้างไฟล์ (jsPDF / SheetJS) | ไม่แตะ backend | API ให้ได้ทีละไม่เกิน 200 แถว ต้องยิงหลายรอบ, jsPDF จัดสระ/วรรณยุกต์ไทยเพี้ยน, เครื่องผู้ใช้ค้างเมื่อข้อมูลเยอะ | ❌ |
| `window.print()` จากหน้าเว็บ | ฟรี ภาษาไทยสวย | คุมการตัดหน้าไม่ได้ และข้อมูลจำกัดแค่ที่โหลดมาแสดง | ❌ |

---

## 4. Library ที่เสนอ

| งาน | Library | License | เหตุผล |
|---|---|---|---|
| Excel (.xlsx) | **SpreadCheetah** | MIT (ฟรี) | เขียนทีละแถวลงไฟล์ (streaming) ใช้ RAM คงที่ไม่ว่าจะกี่แสนแถว และทำ style, freeze, filter, merge, สูตรได้ |
| PDF | **QuestPDF** | Community: ฟรีถ้าองค์กรรายได้ < 1 ล้าน USD/ปี ไม่งั้นต้องซื้อ **(ต้องเช็ก ข้อ 9)** | จัดสระ/วรรณยุกต์ไทยถูกต้อง (มี text shaping), หัวตารางซ้ำทุกหน้า, เลขหน้า X/Y, ตัดบรรทัดในช่อง |
| ฟอนต์ | **Sarabun** (Google Fonts) | OFL (ฟรี) | ฟอนต์ราชการแบบ TH Sarabun, ฝังไว้ในโปรเจกต์ `Assets/Fonts/` |

ตัวเลือกที่ตัดทิ้ง:
- **PdfSharp/MigraDoc** (MIT) ไม่มี text shaping → สระบน/วรรณยุกต์ไทยซ้อนกันผิดตำแหน่ง
- **iText** เป็น AGPL หรือเสียเงิน และภาษาไทยต้องซื้อ add-on เพิ่ม
- **ClosedXML** (MIT) เขียนง่ายกว่า แต่โหลดทั้งไฟล์ไว้ใน RAM ก่อนบันทึก ไม่เหมาะเมื่อไม่จำกัดจำนวนแถว
- **EPPlus** ต้องซื้อ license เมื่อใช้เชิงพาณิชย์
- **Headless Chrome (HTML → PDF)** ไทยสวยแต่ต้องติดตั้ง Chromium บน server และช้ามากกับรายงานหลายพันหน้า

ถ้าใช้ QuestPDF ไม่ได้ (เรื่อง license) ทางสำรองคือ headless Chrome แต่จะช้ากว่ามากเมื่อ export ทั้งหมด

---

## 5. API

### 5.1 Endpoint ใหม่ (รายงานละ 1 ตัว)

```
POST /Report/ExportReport01?format=xlsx
POST /Report/ExportReport01?format=pdf
...
POST /Report/ExportReport07?format=pdf
```

- **body เหมือน `GetReportXX` ทุกอย่าง** (`p_Start`, `p_Finish`, `p_MainOrg`, `p_SubOrg`, `p_Service`) แต่ไม่สนใจ `pageNumber`/`pageSize` เพราะ export ทุกแถว
- `format`: `xlsx` หรือ `pdf` (ไม่สนตัวพิมพ์เล็ก/ใหญ่)

| HTTP | เมื่อไร | Body |
|---|---|---|
| 200 | สำเร็จ (ไม่มีข้อมูลก็ยังได้ไฟล์ที่มีหัวตารางและบรรทัด "ไม่พบข้อมูล") | ไฟล์ |
| 400 | วันที่ผิด, `format` ผิด, ไม่ส่ง `p_MainOrg` / `p_SubOrg` / `p_Service` | ProblemDetails (เหมือน Get) |
| 401/403 | ไม่ได้ login / ไม่มีสิทธิ์ (ดูข้อ 7) | ว่าง |
| 429 | มี export ทำงานพร้อมกันครบจำนวนแล้ว (ข้อ 6) | `{ "statusCode": 429, "message": "มีการ Export อื่นกำลังทำงาน กรุณาลองใหม่อีกครั้ง" }` |
| 500 | ระบบผิดพลาด | `MessageResponse` |

Response header:

```
Content-Type: application/pdf
              application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
Content-Disposition: attachment; filename="Report01_20260701-20260731.pdf"
```

ชื่อไฟล์เป็นภาษาอังกฤษล้วน เพื่อเลี่ยงปัญหาชื่อไทยในบางเบราว์เซอร์

### 5.2 ฝั่ง frontend เรียกยังไง

เป็น `POST` จึงใช้ `<a href>` ตรง ๆ ไม่ได้ ต้องโหลดเป็น blob:

```ts
export async function downloadReport(
  no: string, format: "pdf" | "xlsx", body: object,
  opts: { token?: string; signal?: AbortSignal } = {},   // signal: ให้ปุ่ม "ยกเลิก" หยุดงานได้
) {
  const res = await fetch(`/api/backend/Report/ExportReport${no}?format=${format}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...(opts.token ? { Authorization: `Bearer ${opts.token}` } : {}) },
    body: JSON.stringify(body),
    signal: opts.signal,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.errors ? Object.values(err.errors).flat()[0] as string : err?.message ?? "Export ไม่สำเร็จ");
  }
  const name = /filename="?([^";]+)"?/.exec(res.headers.get("Content-Disposition") ?? "")?.[1] ?? `Report${no}.${format}`;
  const url = URL.createObjectURL(await res.blob());
  const a = Object.assign(document.createElement("a"), { href: url, download: name });
  a.click();
  URL.revokeObjectURL(url);
}
```

UI: ปุ่ม "Export Excel" / "Export PDF" ข้างปุ่มค้นหา ใช้เงื่อนไขเดียวกับที่ค้นหาอยู่ และแสดง loading ระหว่างรอ

ต้องตั้ง `experimental.proxyTimeout` ใน `next.config.ts` ด้วย ไม่งั้นไฟล์ที่สร้างนานเกิน 30 วินาทีจะดาวน์โหลดไม่สำเร็จ (ข้อ 6)

---

## 6. ปริมาณข้อมูล: export ทั้งหมดตามช่วงวันที่ (ไม่จำกัดแถว)

ตัดสินใจแล้วว่า **ไม่จำกัดจำนวนแถว** export ทุกแถวตั้งแต่ `p_Start` ถึง `p_Finish`
ช่วง 19 ปีของรายงาน 01 ได้ประมาณ 28,000 หน้า ระบบจึงต้องรับมือเรื่องต่อไปนี้

| ปัญหา | ถ้าไม่จัดการ | วิธีจัดการ |
|---|---|---|
| query เดิมโหลดทุกแถวเข้า RAM (`ToListAsync`) | RAM สูงตามจำนวนแถว | ยังโหลดทุกแถวเหมือนหน้าจอ (EF Core 10 แปล query แบบนี้ทีละแถวไม่ได้) แต่ตัวเขียนไฟล์ไม่ถือไฟล์ทั้งไฟล์ไว้ใน RAM |
| EF Core ตัด SQL ที่รันเกิน **30 วินาที** (ค่าเริ่มต้น) | ช่วงวันที่กว้างได้ 500 | ตั้ง timeout ยาวขึ้นเฉพาะตอน export (`ReportExport:CommandTimeoutSeconds` เสนอ 300) |
| ถือไฟล์ทั้งไฟล์ไว้ใน RAM (`byte[]`) | RAM เท่าขนาดไฟล์ต่อคำขอ | เขียนลง temp file แล้วส่งเป็น stream และลบไฟล์เองเมื่อส่งเสร็จ |
| Excel รับได้ **1,048,576 แถวต่อ sheet** | ไฟล์เสีย | เกินแล้วขึ้น sheet ใหม่ `Report01 (2)` พร้อมหัวตาราง |
| PDF ต้องจัดหน้าทั้งเล่มก่อนเขียน (เพื่อรู้จำนวนหน้ารวม "X / Y") ข้อมูลทุกแถวจึงต้องอยู่ใน RAM | 28,000 หน้าอาจใช้หลายนาทีและ RAM หลาย GB | **วัดกับข้อมูลจริงในขั้น 3** ถ้าหนักเกินรับได้ ให้เปลี่ยน PDF ช่วงกว้างเป็นงานเบื้องหลัง (สั่งแล้วรอดาวน์โหลดทีหลัง) |
| proxy ของ Next.js (`rewrites`) ตัดการเชื่อมต่อเมื่อ backend เงียบเกิน **30 วินาที** | ไฟล์ใหญ่ดาวน์โหลดไม่สำเร็จ ทั้งที่ backend ยังทำงานอยู่ | ตั้ง `experimental.proxyTimeout: 600_000` (10 นาที) ใน `mui-next-gcc/next.config.ts` |
| IIS แบบ out-of-process ตัดคำขอที่เกิน 2 นาที | 502 | ถ้า deploy แบบนั้น ตั้ง `requestTimeout` ใน `web.config` |
| หลายคนกด export พร้อมกัน | server ช้าทั้งระบบ | จำกัด export พร้อมกัน (เสนอ 2 งาน) ด้วย concurrency limiter ที่มีใน ASP.NET Core อยู่แล้ว เกินแล้วตอบ 429 |
| ผู้ใช้ปิดหน้าหรือกดยกเลิก | server ทำต่อเปล่า ๆ | ส่ง `CancellationToken` ไปถึง query และ writer ให้หยุดทันที |

รายงาน 05/06 เป็นวันละแถว (19 ปี ≈ 7,000 แถว) จึงไม่มีปัญหาเรื่องปริมาณ

ฝั่ง frontend: ระหว่างรอให้แสดง "กำลังสร้างไฟล์ ช่วงวันที่กว้างอาจใช้เวลาหลายนาที" พร้อมปุ่มยกเลิก (`AbortController`)
และปิดปุ่ม export ไว้ไม่ให้กดซ้ำ

---

## 7. ความปลอดภัย

- ไฟล์ export มี **ชื่อ-นามสกุลและเบอร์โทรประชาชน** (ข้อมูลส่วนบุคคลตาม PDPA) แต่ตอนนี้ `ReportController` ยังเปิดให้เรียกได้โดยไม่ต้อง login
- endpoint export **ต้องมี `[Authorize]`** (ตัดสินใจแล้ว) ส่วน `GetReportXX` ยังเปิดอยู่เหมือนเดิม
- บันทึก log ทุกครั้งที่ export: ใคร (`sub`), รายงานไหน, ช่วงวันที่, จำนวนแถว, รูปแบบไฟล์
- ใส่ "พิมพ์โดย <username> เมื่อ <วันเวลา>" ท้ายทุกหน้าของ PDF และใน Excel (แถวหัวรายงาน)

---

## 8. โครงสร้างโค้ด

### 8.1 แนวคิด: นิยามคอลัมน์ครั้งเดียว ใช้ได้ทั้ง PDF และ Excel

```csharp
// Reports/Export/ReportColumn.cs
public enum ColumnKind { Text, LongText, DateTime, Number }

public sealed record ReportColumn<T>(
    string Header,              // หัวคอลัมน์
    Func<T, object?> Value,     // ดึงค่าจากแถว
    float PdfWidth,             // ความกว้างสัมพัทธ์ใน PDF
    ColumnKind Kind = ColumnKind.Text,
    string? Group = null,       // หัวกลุ่มชั้นบน (กลุ่ม B)
    bool Sum = false);          // รวมในแถวท้ายตาราง (กลุ่ม B)

// Reports/Export/ReportDocument.cs
public sealed record ReportDocument<T>(
    string Title,
    IReadOnlyList<string> SubTitles,   // ช่วงวันที่, หน่วยงาน, ประเภทบริการ
    IReadOnlyList<ReportColumn<T>> Columns,
    PdfPaper Paper,                    // A3Landscape / A4Landscape
    string PrintedBy,
    DateTime PrintedAt);
```

- `ExcelReportWriter.WriteAsync<T>(layout, header, rows, output, ct)` (SpreadCheetah) เขียนทีละแถวลง output
- `PdfReportWriter.Write<T>(ReportDocument<T>, IReadOnlyList<T> rows, Stream output)` (QuestPDF) ต้องได้ทุกแถวก่อน เพราะจัดหน้าทั้งเล่ม
- `output` เป็น temp file (`FileOptions.DeleteOnClose`) แล้วคืนด้วย `File(stream, contentType, fileName)`
- writer ทั้งสองเป็นแบบ generic เขียนครั้งเดียว ใช้กับทั้ง 7 รายงาน

### 8.2 ไฟล์ที่จะเพิ่ม/แก้

| ไฟล์ | สิ่งที่ทำ |
|---|---|
| `ContactTogetherApi.csproj` | เพิ่ม `SpreadCheetah`, `QuestPDF` และ copy ฟอนต์ไป output |
| `Assets/Fonts/Sarabun-Regular.ttf`, `Sarabun-Bold.ttf` | ฟอนต์ไทย |
| `Reports/Export/ReportColumn.cs`, `ReportDocument.cs` | นิยามข้างบน |
| `Reports/Export/ExcelReportWriter.cs` | สร้าง xlsx |
| `Reports/Export/PdfReportWriter.cs` | สร้าง PDF |
| `Reports/ReportColumns.cs` | ชุดคอลัมน์: `ServiceRequest` (01/07), `SrCallback` (02/03/04), `ClaimDaily` (05), `NonServiceDaily` (06) |
| `Controllers/ReportController.cs` | **ย้าย query ของแต่ละรายงานออกเป็น private method ที่คืน `IQueryable`** (`BuildReport01Query(start, finish)` ฯลฯ) ให้ `GetReportXX` และ `ExportReportXX` ใช้ query เดียวกัน ข้อมูลใน export จะได้ตรงกับหน้าจอ และให้ `OrderBy` อยู่หลัง `Distinct` ทุกตัว (รายงาน 01 ตอนนี้เรียงก่อน `Distinct` ลำดับจึงอาจไม่แน่นอน) |
| `Controllers/ReportController.Export.cs` | `partial class` ที่มี `ExportReport01`–`07` แยกไฟล์เพราะ controller เดิมยาว 1,500 บรรทัดแล้ว |
| `Program.cs` | `QuestPDF.Settings.License = …`, ลงทะเบียนฟอนต์ตอน startup, `AddRateLimiter` (concurrency limiter ชื่อ `report-export`) |
| `appsettings.json` | `ReportExport:CommandTimeoutSeconds` (300), `ReportExport:MaxConcurrent` (2) |
| `docs/report-api.md`, `ContactTogetherApi.http` | เพิ่มวิธีเรียก export |

### 8.3 รายละเอียดไฟล์ Excel

- แถว 1: ชื่อรายงาน (ตัวหนา, merge ทั้งแถว) / แถว 2: ช่วงวันที่และเงื่อนไข / แถว 3: พิมพ์โดย…เมื่อ… / แถว 5: หัวตาราง (กลุ่ม B ใช้แถว 5–6)
- Freeze หัวตาราง, เปิด AutoFilter, หัวตารางพื้นเทาตัวหนา, เส้นตาราง
- วันที่เก็บเป็น **วันที่จริงของ Excel** (เรียง/กรองได้) แสดงด้วย number format `d/m/yyyy h:mm:ss` ให้หน้าตาเหมือนระบบเดิม ตัวเลขเก็บเป็นตัวเลข, `a_number` เก็บเป็น text
- เกิน 1,048,576 แถว ขึ้น sheet ใหม่ `Report01 (2)`, `Report01 (3)` … พร้อมหัวตาราง
- Summary/Description ตัดบรรทัด กว้างคงที่
- ตัดอักขระควบคุมที่ XML ไม่รับ (ข้อมูลเก่ามักมี) และตัดข้อความที่ยาวเกิน 32,767 ตัวอักษร (เพดานต่อช่องของ Excel)
- กลุ่ม B: แถวรวมท้ายตารางเป็นสูตร `SUM` เพื่อให้ผู้ใช้แก้ตัวเลขแล้วรวมใหม่ได้
- ชื่อ sheet: `Report01`

### 8.4 รายละเอียดไฟล์ PDF

- ส่วนหัวทุกหน้า: ชื่อรายงานและบรรทัดเงื่อนไข (กลาง ตัวหนา)
- หัวตารางพิมพ์ซ้ำทุกหน้า, ข้อความยาวตัดบรรทัดในช่อง, แถวยาวขึ้นตามเนื้อหา
- ท้ายหน้า: ซ้าย `พิมพ์โดย … เมื่อ …` / ขวา `หน้าที่ X / Y`
- ฟอนต์ Sarabun 7pt (กลุ่ม A, A3) / 9pt (กลุ่ม B, A4)
- กลุ่ม B: หัวตาราง 2 ชั้น + แถวรวมตัวหนา

---

## 9. การตัดสินใจ

| # | คำถาม | สถานะ |
|---|---|---|
| 1 | องค์กรใช้ **QuestPDF Community** (ฟรี) ได้ไหม (รายได้ต่อปี < 1 ล้าน USD) หรือมีงบซื้อ license | ✅ ใช้ได้ (Community) |
| 2 | **ชื่อหัวรายงานและไฟล์ตัวอย่างของ 02–07** | ✅ ได้ชื่อ 02–06 แล้ว (ข้อ 2), 07 ยังไม่มีตัวอย่าง |
| 3 | รูปแบบวันที่ในตาราง | ✅ แบบเดิม `d/M/yyyy H:mm:ss` |
| 4 | คอลัมน์ `require` | ✅ `"Y"` = ต้องการ, อื่น ๆ = ไม่ต้องการ |
| 5 | จำกัดจำนวนแถวไหม | ✅ ไม่จำกัด export ทั้งหมดตามช่วงวันที่ (ผลกระทบดูข้อ 6) |
| 6 | export ต้อง login ไหม | ✅ ต้อง login |
| 7 | `Contact_Name` ว่างให้แสดง `none` เหมือนเดิมไหม | ✅ แสดง `none` |

---

## 10. ลำดับการทำ

| ขั้น | งาน | เสร็จเมื่อ |
|---|---|---|
| 1 | ติดตั้ง package + ฟอนต์, ทำ `ReportColumn`/`ReportDocument`, ย้าย query ของรายงาน 01 ออกเป็น method, ตั้ง command timeout ตอน export | `GetReport01` ได้ผลเหมือนเดิม |
| 2 | `ExcelReportWriter` (streaming + temp file) + `ExportReport01?format=xlsx` | เปิดใน Excel ได้, ภาษาไทยถูก, วันที่เรียงได้, ช่วง 2007–2026 export ได้ครบโดย RAM ไม่พุ่ง |
| 3 | `PdfReportWriter` + `ExportReport01?format=pdf` แล้ว**วัดเวลาและ RAM ช่วง 2007–2026** | หน้าตาใกล้เคียง `report01.pdf`, copy ข้อความไทยได้ถูก, รู้ตัวเลขจริงเพื่อตัดสินว่าต้องทำงานเบื้องหลังไหม |
| 4 | รายงาน 02, 03, 04, 07 (ใช้ writer เดิม เพิ่มแค่คอลัมน์และหัวรายงาน) | ทั้ง 4 ตัว export ได้ทั้ง 2 แบบ |
| 5 | หัวตาราง 2 ชั้น + แถวรวม → รายงาน 05, 06 | ผลรวมตรงกับ `GetReport05/06` |
| 6 | concurrency limiter, `[Authorize]` (ถ้าตัดสินใจตามที่เสนอ), log การ export | export พร้อมกันเกินได้ 429, ไม่มี token ได้ 401 |
| 7 | อัปเดต `docs/report-api.md` + `.http`, ฝั่ง frontend: ปุ่ม export + `proxyTimeout` | frontend ดาวน์โหลดไฟล์ช่วงกว้างได้สำเร็จ |

### วิธีทดสอบ

- เทียบจำนวนแถวในไฟล์กับ `meta.pagination.total` ของ `GetReportXX` ช่วงวันที่เดียวกัน
- ตรวจคำไทยที่มีสระบนและวรรณยุกต์ซ้อน (`ที่`, `น้ำ`, `ผู้`, `กฎหมาย`) ใน PDF ว่าไม่ซ้อนทับ และ copy ออกมาได้ถูก
- เปิด xlsx ใน Excel และ LibreOffice: เรียง/กรองวันที่ได้, `a_number` ยังมีเลข 0 นำหน้า
- ส่งช่วงวันที่กว้าง ๆ (2007–2026) → ได้ไฟล์ครบทุกแถว จดเวลาที่ใช้และ RAM สูงสุดของ process ไว้
- กดยกเลิกระหว่างสร้างไฟล์ → server หยุดทำงาน (ดูจาก log) และไม่มี temp file ค้าง
- export ผ่าน `/api/backend` ของ Next.js ที่ใช้เวลาเกิน 30 วินาที → ยังดาวน์โหลดสำเร็จ (ยืนยันว่าตั้ง `proxyTimeout` แล้ว)
- ช่วงที่ไม่มีข้อมูล → ได้ไฟล์ที่มีหัวตารางและบรรทัด "ไม่พบข้อมูล"
