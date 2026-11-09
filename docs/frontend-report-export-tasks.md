# งาน Frontend: Export PDF / Excel และปรับรายงาน 03, 04

สำหรับ: ทีม frontend (`mui-next-gcc`) · จาก: backend (`WS-ContactTogether`, branch `feature/report-export`) · 6/11/2569

Backend เพิ่ม endpoint export ไฟล์ของทั้ง 7 รายงาน และเปลี่ยนรายงาน 03, 04 เล็กน้อย เอกสารนี้บอกว่า frontend
ต้องแก้ไฟล์ไหนบ้าง รายละเอียด API ทั้งหมดอยู่ใน `report-api.md` (ข้อ 8 = export)

---

## สรุปงาน

| # | งาน | ไฟล์หลัก | จำเป็น |
|---|---|---|---|
| 1 | อัปเดตเอกสาร API เป็นฉบับใหม่ | `document/report-api.md` | ✅ |
| 2 | ตั้ง `proxyTimeout` ให้ proxy รอไฟล์ใหญ่ได้ | `next.config.ts` | ✅ |
| 3 | ฟังก์ชันดาวน์โหลดไฟล์ | `lib/report/client.ts`, `types/report.ts` | ✅ |
| 4 | ปุ่ม Export Excel / PDF ในหน้าผลรายงาน | `app/(portal)/report/_components/ExportButtons.tsx` (ใหม่), `report/[no]/page.tsx` | ✅ |
| 5 | รายงาน 04: หน่วยงานไม่บังคับ (ไม่เลือก = ทุกหน่วยงาน) | `ReportFilters.tsx`, `lib/report/query.ts`, `report/[no]/page.tsx` | ✅ |
| 6 | รายงาน 03: เพิ่มคอลัมน์ Contact_No | `types/report.ts`, `lib/report/rows.ts`, `SrReportTable.tsx` | ✅ |
| 7 | แก้คอมเมนต์ที่ล้าสมัย | `types/report.ts`, `lib/data/reports.ts` | – |

---

## สิ่งที่ backend เปลี่ยน

| | ก่อน | ตอนนี้ |
|---|---|---|
| Export | ไม่มี | `POST /Report/ExportReport01`–`07?format=xlsx\|pdf` ได้ไฟล์ทุกแถวในช่วงวันที่ (ไม่แบ่งหน้า) |
| รายงาน 04 | ต้องส่ง `p_MainOrg` + `p_SubOrg` (ไม่ส่ง `p_SubOrg` ได้ 500) | **ไม่บังคับทั้งคู่** ไม่ส่ง = ทุกหน่วยงาน |
| รายงาน 03 | – | แต่ละแถวมี `contactNo` (เบอร์จากบัญชีผู้ติดต่อ = คอลัมน์ Contact_No ของรายงานเดิม) |
| รายงาน 01 | ลำดับแถวอาจไม่แน่นอน | เรียงตามเลข SR เสมอ |
| Auth ของ export | – | **ยังไม่บังคับ token** (ปิดไว้ชั่วคราวระหว่างทดสอบ) |

`GetReportXX` ตัวอื่นไม่เปลี่ยน โค้ดเดิมใช้ต่อได้

ระหว่างทดสอบ ให้ตั้ง `BACKEND_ORIGIN` ไปที่ backend ที่รัน branch `feature/report-export` (ยังไม่ merge)

---

## 1. อัปเดตเอกสาร API

แทนที่ `document/report-api.md` ด้วยไฟล์ `docs/report-api.md` ฉบับล่าสุดจาก repo backend
ฉบับที่อยู่ใน frontend ตอนนี้เป็นฉบับก่อนมี export และยังบอกว่ารายงาน 04 ต้องส่งหน่วยงาน

---

## 2. `next.config.ts`: ตั้ง `proxyTimeout`

proxy ของ `rewrites()` ตัดการเชื่อมต่อเมื่อ backend เงียบเกิน **30 วินาที** (ค่าเริ่มต้นของ Next) แต่ PDF ช่วงวันที่กว้าง
ใช้เวลานานกว่านั้น (เช่น 60,000 แถว ≈ 75 วินาที) ถ้าไม่ตั้ง ผู้ใช้จะได้ error ทั้งที่ backend ยังสร้างไฟล์อยู่

```ts
const nextConfig: NextConfig = {
  env: { /* เดิม */ },
  /* ─── proxy รอ backend ได้นานขึ้น ───
     export PDF ช่วงวันที่กว้างใช้เวลาเกิน 30 วินาที (ค่าเริ่มต้นของ Next) */
  experimental: {
    proxyTimeout: 600_000 // 10 นาที
  },
  async rewrites() { /* เดิม */ }
};
```

ค่านี้มีผลกับ `next dev` / `next start` ถ้า deploy บน Vercel การ rewrite ไป origin ภายนอกมีเพดานเวลาของ Vercel เอง
ต้องตรวจสอบแยกตอน deploy

---

## 3. ฟังก์ชันดาวน์โหลดไฟล์

### `types/report.ts`

```ts
export type ReportFormat = 'xlsx' | 'pdf';
```

### `lib/report/client.ts`

export เป็น `POST` จึงใช้ `<a href>` ตรง ๆ ไม่ได้ ต้องโหลดเป็น blob แล้วสั่งดาวน์โหลด แยกการแปลง error ออกมาให้
`fetchReport` กับ `downloadReport` ใช้ร่วมกัน:

```ts
/** แปลง response ที่ไม่ใช่ 2xx เป็น ReportApiError — ข้อความไทยจาก API แสดงผู้ใช้ได้ตรง ๆ */
async function toReportError(res: Response): Promise<ReportApiError> {
  const err = await res.json().catch(() => null);
  if (res.status === 400 && err?.errors) {
    const first = Object.values(err.errors as Record<string, string[]>).flat()[0];
    return new ReportApiError(400, first ?? 'ข้อมูลไม่ถูกต้อง', err.errors);
  }
  if (res.status === 401) return new ReportApiError(401, 'กรุณาเข้าสู่ระบบใหม่');
  if (res.status === 403) return new ReportApiError(403, 'ไม่มีสิทธิ์ดูรายงานนี้');
  // 429 (มีคน export พร้อมกันครบจำนวน) และ 500 มี message ภาษาไทยมาใน body
  // proxy ของ Next ตอบ 500 แบบไม่มี body เมื่อต่อ backend ไม่ได้
  return new ReportApiError(res.status, err?.message ?? 'เกิดข้อผิดพลาดภายในระบบ');
}
```

ใน `fetchReport` เปลี่ยนส่วนท้ายเป็น:

```ts
  if (res.ok) return res.json();
  throw await toReportError(res);
```

แล้วเพิ่ม:

```ts
/* ─── ดาวน์โหลดไฟล์รายงาน (POST /Report/ExportReport<no>?format=xlsx|pdf) ───
   ได้ทุกแถวในช่วงวันที่ ไม่แบ่งหน้า — pageNumber/pageSize ที่ติดมา API ข้ามเอง */
export async function downloadReport(
  no: ReportNo,
  format: ReportFormat,
  body: ReportRequest,
  opts: { token?: string; signal?: AbortSignal } = {}
): Promise<string> {
  let res: Response;
  try {
    res = await fetch(`/api/backend/Report/ExportReport${no}?format=${format}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        // ยังไม่บังคับ token แต่ API จะเปิด [Authorize] ภายหลัง
        ...(opts.token ? { Authorization: `Bearer ${opts.token}` } : {})
      },
      body: JSON.stringify(body),
      signal: opts.signal,
      cache: 'no-store'
    });
  } catch (e) {
    if ((e as Error).name === 'AbortError') throw e;
    throw new ReportApiError(0, 'เชื่อมต่อระบบรายงานไม่ได้ กรุณาตรวจสอบเครือข่าย');
  }

  if (!res.ok) throw await toReportError(res);

  const name =
    fileNameFrom(res.headers.get('Content-Disposition')) ?? `Report${no}.${format}`;
  const url = URL.createObjectURL(await res.blob());
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  // รอให้เบราว์เซอร์เริ่มดาวน์โหลดก่อนค่อยคืนหน่วยความจำ
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  return name;
}

/** ชื่อไฟล์จาก header เช่น Report01_20260701-20260731.xlsx */
function fileNameFrom(disposition: string | null) {
  if (!disposition) return null;
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
  if (encoded) return decodeURIComponent(encoded);
  return /filename="?([^";]+)"?/i.exec(disposition)?.[1] ?? null;
}
```

ใช้ `document` ได้เพราะฟังก์ชันนี้ถูกเรียกจาก event handler ฝั่ง client เท่านั้น (ไม่ทำงานตอน SSR)

---

## 4. ปุ่ม Export ในหน้าผลรายงาน

### พฤติกรรม

| เรื่อง | ทำอย่างไร |
|---|---|
| ตำแหน่ง | `actions` ของการ์ด "ผลรายงาน" ข้างข้อความ "พบข้อมูล N รายการ" (FolderCard ย่อขนาดปุ่มในหัวการ์ดให้เอง) |
| แสดงเมื่อ | มีผลรายงานแล้วและไม่ error (เงื่อนไขเดียวกับข้อความ "พบข้อมูล") |
| ตัวกรองที่ส่ง | `reportRequest(query, report)` จาก **URL** ไม่ใช่ค่าในฟอร์มที่ยังไม่กดดูรายงาน ไฟล์จะตรงกับผลที่เห็น |
| ไม่มีข้อมูล (`total === 0`) | ปิดปุ่ม |
| ระหว่างสร้างไฟล์ | แทนปุ่มด้วย "กำลังสร้างไฟล์…" + ปุ่ม "ยกเลิก" ห้ามกดซ้ำ |
| PDF เกิน 10,000 แถว | ถามยืนยันก่อน และแนะนำ Excel (PDF ≈ 1.5 วินาที ต่อ 1,000 แถว, Excel 100,000 แถว ≈ 2 วินาที) |
| สำเร็จ | Snackbar + `Alert severity="info"` มุมขวาบน ปิดเองใน 3 วินาที (ตาม DESIGN.md ข้อ 7) |
| error | `Swal.fire` icon error ข้อความจาก `error.message` (`allowOutsideClick: false`) |
| 429 | เหมือน error: API ส่ง "มีการ Export อื่นกำลังทำงาน กรุณาลองใหม่อีกครั้ง" มาให้ |
| กดยกเลิก / ออกจากหน้า | `abort()` ไม่ต้องแสดง error server จะหยุดสร้างไฟล์เอง |

### `app/(portal)/report/_components/ExportButtons.tsx` (ไฟล์ใหม่)

```tsx
'use client';

import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Stack from '@mui/material/Stack';
import GridOnOutlinedIcon from '@mui/icons-material/GridOnOutlined';
import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import Swal from 'sweetalert2';
import { downloadReport, ReportApiError } from '@/lib/report/client';
import type { ReportFormat, ReportNo, ReportRequest } from '@/types/report';

/* ─── ปุ่ม Export ไฟล์รายงาน ───────────────────────────────────
   ใช้ตัวกรองชุดเดียวกับผลที่แสดงอยู่ (จาก URL) ไฟล์จึงตรงกับหน้าจอ แต่ได้ทุกแถวไม่แบ่งหน้า
   PDF สร้างช้ากว่า Excel มาก — ข้อมูลเยอะให้ถามก่อนและแนะนำ Excel */

const PDF_CONFIRM_ROWS = 10_000;

export default function ExportButtons({
  no,
  request,
  total,
  onDone
}: {
  no: ReportNo;
  request: ReportRequest;
  total: number;
  onDone?: (fileName: string) => void;
}) {
  const [busy, setBusy] = useState<ReportFormat | null>(null);
  const ctrlRef = useRef<AbortController | null>(null);

  // ออกจากหน้าระหว่างสร้างไฟล์ → ยกเลิกคำขอ (server หยุดสร้างไฟล์ด้วย)
  useEffect(() => () => ctrlRef.current?.abort(), []);

  const run = async (format: ReportFormat) => {
    if (format === 'pdf' && total > PDF_CONFIRM_ROWS) {
      const answer = await Swal.fire({
        icon: 'question',
        title: 'สร้างไฟล์ PDF ขนาดใหญ่?',
        text: `ข้อมูล ${total.toLocaleString('th-TH')} รายการ ไฟล์ PDF จะใช้เวลาสร้างนาน แนะนำให้ Export เป็น Excel ซึ่งเร็วกว่ามาก`,
        showCancelButton: true,
        confirmButtonText: 'ตกลง',
        cancelButtonText: 'ยกเลิก',
        confirmButtonColor: '#7e57c2',
        allowOutsideClick: false
      });
      if (!answer.isConfirmed) return;
    }

    const ctrl = new AbortController();
    ctrlRef.current = ctrl;
    setBusy(format);
    try {
      const fileName = await downloadReport(no, format, request, {
        signal: ctrl.signal
      });
      onDone?.(fileName);
    } catch (e) {
      if ((e as Error)?.name === 'AbortError') return;
      await Swal.fire({
        icon: 'error',
        title: 'Export ไม่สำเร็จ',
        text: e instanceof ReportApiError ? e.message : 'เกิดข้อผิดพลาดที่ไม่คาดคิด',
        confirmButtonText: 'ตกลง',
        confirmButtonColor: '#7e57c2',
        allowOutsideClick: false
      });
    } finally {
      if (ctrlRef.current === ctrl) ctrlRef.current = null;
      setBusy(null);
    }
  };

  if (busy) {
    return (
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <CircularProgress size={14} />
        <Box component="span" sx={{ fontSize: 12 }}>
          กำลังสร้างไฟล์ {busy === 'pdf' ? 'PDF' : 'Excel'}…
        </Box>
        <Button size="small" color="inherit" onClick={() => ctrlRef.current?.abort()}>
          ยกเลิก
        </Button>
      </Stack>
    );
  }

  return (
    <Stack direction="row" spacing={1}>
      <Button
        size="small"
        variant="outlined"
        disableElevation
        startIcon={<GridOnOutlinedIcon />}
        disabled={total === 0}
        onClick={() => run('xlsx')}
      >
        Excel
      </Button>
      <Button
        size="small"
        variant="outlined"
        disableElevation
        startIcon={<PictureAsPdfOutlinedIcon />}
        disabled={total === 0}
        onClick={() => run('pdf')}
      >
        PDF
      </Button>
    </Stack>
  );
}
```

### `app/(portal)/report/[no]/page.tsx`

เพิ่ม import:

```tsx
import Snackbar from '@mui/material/Snackbar';
import ExportButtons from '../_components/ExportButtons';
```

เพิ่ม state ของ Snackbar ใน `ReportResult`:

```tsx
const [savedFile, setSavedFile] = useState<string | null>(null);
```

เปลี่ยน `actions` ของการ์ด "ผลรายงาน":

```tsx
<FolderCard
  title="ผลรายงาน"
  actions={
    query && pagination && !error ? (
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
        <Typography variant="body2" sx={{ fontWeight: 700 }}>
          {data.message}
        </Typography>
        <ExportButtons
          no={report.no}
          request={reportRequest(query, report)}
          total={pagination.total}
          onDone={setSavedFile}
        />
      </Stack>
    ) : undefined
  }
>
```

และวาง Snackbar ไว้ท้าย fragment ของ `ReportResult`:

```tsx
<Snackbar
  open={!!savedFile}
  autoHideDuration={3000}
  onClose={() => setSavedFile(null)}
  anchorOrigin={{ vertical: 'top', horizontal: 'right' }}
>
  <Alert severity="info" variant="filled" onClose={() => setSavedFile(null)}>
    ดาวน์โหลด {savedFile} แล้ว
  </Alert>
</Snackbar>
```

จอแคบ: ถ้าข้อความกับปุ่มไม่พอในแถวหัวการ์ด ให้ใส่ `flexWrap: 'wrap'` หรือซ่อนข้อความ "พบข้อมูล" ที่ `xs`

---

## 5. รายงาน 04: หน่วยงานไม่บังคับ

| ผู้ใช้เลือก | ส่ง API | ได้ SR ของ |
|---|---|---|
| ไม่เลือกอะไร | ไม่ส่งทั้งคู่ | **ทุกหน่วยงาน** |
| หน่วยงานหลักอย่างเดียว | `p_MainOrg` | หน่วยงานหลักนั้น และหน่วยงานย่อยทุกตัวใต้มัน |
| หลัก + ย่อย | `p_MainOrg` + `p_SubOrg` | หน่วยงานย่อยนั้นเท่านั้น |

### `ReportFilters.tsx`

- ลบการตรวจบังคับเลือกหน่วยงาน (ส่วน `if (report.filter === 'org') { … }` ใน `submit`) รวมทั้งคอมเมนต์
  "API ตอบ 500 ถ้าไม่ส่งหน่วยงานย่อย"
- เอา `required` และ `sx={requiredFieldSx}` ออกจาก Autocomplete หน่วยงานหลัก/ย่อย
- ใส่คำแนะนำ เช่น helperText ของหน่วยงานหลัก: `ไม่เลือก = ทุกหน่วยงาน` และของหน่วยงานย่อย:
  `ไม่เลือก = ทุกหน่วยงานย่อยของหน่วยงานหลัก` (ช่องย่อยยังปิดไว้จนกว่าจะเลือกหน่วยงานหลัก เหมือนเดิม)

### `lib/report/query.ts`

```ts
// readReportQuery — ลบบรรทัดนี้ออก (หน่วยงานไม่บังคับแล้ว ไม่เลือก = ทุกหน่วยงาน)
if (report.filter === 'org' && (!mainOrg || !subOrg)) return null;
```

```ts
// reportQueryString — ใส่ลง URL เฉพาะที่เลือก
if (report.filter === 'org') {
  if (q.mainOrg) p.set('main', q.mainOrg);
  if (q.subOrg) p.set('sub', q.subOrg);
}
```

```ts
// reportRequest — ส่งเฉพาะที่เลือก
...(report.filter === 'org' && q.mainOrg ? { p_MainOrg: q.mainOrg } : {}),
...(report.filter === 'org' && q.subOrg ? { p_SubOrg: q.subOrg } : {}),
```

### `report/[no]/page.tsx` → `criteriaText`

```ts
if (report.filter === 'org') {
  const main = q.mainOrg ? (orgTitleById(q.mainOrg) ?? q.mainOrg) : '';
  const sub = q.subOrg ? (orgTitleById(q.subOrg) ?? q.subOrg) : '';
  parts.push(
    !main && !sub
      ? 'ทุกหน่วยงาน'
      : sub
        ? [main, sub].filter(Boolean).join(' › ')
        : `${main} (ทุกหน่วยงานย่อย)`
  );
}
```

---

## 6. รายงาน 03: คอลัมน์ Contact_No

รายงาน 03 ของระบบเดิมมีคอลัมน์ `Contact_No` (เบอร์ที่ใช้โทรกลับ) API ส่งมาเป็น `contactNo` เฉพาะรายงาน 03
และจะ **ไม่มี key นี้เลย** ถ้าบัญชีไม่มีเบอร์ (ตัดออกตอนเป็น null) จึงต้องดูจากเลขรายงาน ไม่ใช่ `'contactNo' in r`

| ไฟล์ | แก้ |
|---|---|
| `types/report.ts` | `SrCallbackRow` เพิ่ม `/** รายงาน 03 เท่านั้น — ไม่มี key ถ้าไม่มีเบอร์ */ contactNo?: string;` |
| `lib/report/rows.ts` | `SrRow` เพิ่ม `contactNo: string`; ใน `normalizeSrRow`: `contactNo: 'createrName' in r ? '' : s(r.contactNo),` |
| `SrReportTable.tsx` | เพิ่ม prop `withContactNo` แสดงคอลัมน์ "เบอร์ติดต่อกลับ" ถัดจากคอลัมน์เบอร์โทร (`a_number`) |
| `report/[no]/page.tsx` | `<SrReportTable … withContactNo={report.no === '03'} />` |

ในรายงาน 03 `aNumber` มักว่าง แต่ `contactNo` มีค่า (ตรงกับรายงานเดิม)

---

## 7. คอมเมนต์ที่ล้าสมัย

- `types/report.ts` → `p_SubOrg`: เปลี่ยน "ต้องส่งทุกครั้ง ไม่งั้น API ตอบ 500" เป็น "ไม่บังคับ — ไม่ส่งทั้ง p_MainOrg และ
  p_SubOrg = ทุกหน่วยงาน"
- `lib/data/reports.ts` → รายงาน 04 `desc`: "SR แยกตามหน่วยงาน ไม่เลือกหน่วยงาน = ทุกหน่วยงาน"
- `CLAUDE.md` ข้อ "ข้อมูลจริงจาก backend": เพิ่มว่ามี export (`ExportReport01–07`) ผ่าน `downloadReport`

---

## เรื่อง token

export ยังไม่บังคับ token ส่วนหน้า login ของ frontend ตอนนี้เป็น mock (ไม่มี token จริง) จึงเรียกได้เลยโดยไม่ส่ง
`Authorization` เมื่อ backend เปิด `[Authorize]` อีกครั้ง frontend ต้องมี login จริงก่อน แล้วส่ง `token` ให้ทั้ง
`fetchReport` และ `downloadReport` (ทั้งสองฟังก์ชันรับ `opts.token` อยู่แล้ว และแปลง 401 เป็นข้อความ "กรุณาเข้าสู่ระบบใหม่")

---

## ตรวจรับงาน

- [ ] รายงาน 01–07 กด Excel และ PDF แล้วได้ไฟล์ชื่อ `ReportXX_yyyyMMdd-yyyyMMdd.xlsx|pdf`
- [ ] จำนวนแถวในไฟล์เท่ากับ "พบข้อมูล N รายการ" (ไฟล์มีทุกแถว ไม่ใช่แค่หน้าที่เห็น)
- [ ] ไฟล์ใช้ตัวกรองที่กดดูรายงานล่าสุด ไม่ใช่ค่าที่พิมพ์ค้างไว้ในฟอร์ม
- [ ] รายงาน 04 ไม่เลือกหน่วยงาน → ได้ผลทุกหน่วยงาน ช่วงข้อมูลแสดง "ทุกหน่วยงาน" และใน PDF มีบรรทัด "หน่วยงาน: ทุกหน่วยงาน"
- [ ] รายงาน 04 เลือกหน่วยงานหลักอย่างเดียว → ได้ผล และ URL ไม่มี `sub=`
- [ ] รายงาน 03 มีคอลัมน์เบอร์ติดต่อกลับ
- [ ] ช่วงวันที่กว้าง (เช่น 1/1/2557 – ปัจจุบัน) PDF ที่ใช้เวลาเกิน 30 วินาทียังดาวน์โหลดได้ (ยืนยันว่าตั้ง `proxyTimeout` แล้ว)
- [ ] PDF เกิน 10,000 แถวมี dialog ถามก่อน
- [ ] กด "ยกเลิก" ระหว่างสร้างไฟล์ → ปุ่มกลับมา ไม่มี error
- [ ] เปิด 3 แท็บแล้ว export PDF ช่วงกว้างพร้อมกัน → แท็บที่ 3 เห็นข้อความ "มีการ Export อื่นกำลังทำงาน…"
- [ ] ปิด backend แล้วกด export → เห็น "เชื่อมต่อระบบรายงานไม่ได้…"
- [ ] `npm run lint` และ `npm run build` ผ่าน

---

## อ้างอิง

- `report-api.md` ข้อ 2.3 (body ของรายงาน 04), ข้อ 3.2 (`contactNo`), ข้อ 8 (export ทั้งหมด รวม payload ทุกรายงานในข้อ 8.6)
- `report-export-design.md` ข้อ 0: หน้าตาไฟล์และตัวเลขเวลาที่วัดได้
- ตัวอย่างไฟล์ของระบบเดิม: `docs/report01.pdf`–`report06.pdf` ใน repo backend
