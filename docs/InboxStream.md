# รายการรับแบบ real-time (`GET /Home/InboxStream`) สำหรับ Frontend

ใช้ทำให้ **งานที่ถูกส่งมา เด้งเข้าแท็บรายการรับทันที** โดยไม่ต้องกดโหลดใหม่

> อ้างอิงโค้ด: `Controllers/HomeController.cs` (`InboxStream`), `Realtime/InboxNotifier.cs`, `Dtos/InboxSignal.cs`
>
> เอกสารที่ใช้คู่กัน: [HomeServices.md](HomeServices.md) (`GetInboxServices`), [ReceiveService.md](ReceiveService.md)
>
> **เพิ่มเมื่อ 26 พ.ย. 2569**

---

## 1. หลักการ

backend **ไม่ได้ส่งรายการมาทาง stream** ส่งแค่สัญญาณว่า "รายการรับของคุณอาจเปลี่ยน" แล้ว frontend เรียก
`GET /Home/GetInboxServices` ใหม่เอง (ใช้ hook / ฟังก์ชันเดิมที่มีอยู่แล้ว)

```
คนส่ง: AddNewServiceOperations (ownerId = B) ──► backend บันทึกเสร็จ ──► สัญญาณไปที่ stream ของ B
คนรับ B: EventSource('/api/backend/Home/InboxStream') ◄── event: inbox
         └► โหลด GetInboxServices ใหม่ → เรื่องใหม่โผล่ในแท็บรายการรับ + ตัวเลขที่กระดิ่งเปลี่ยน
```

ใช้ Server-Sent Events (`EventSource`) แบบเดียวกับนาฬิกา (`lib/date/useServerClock.ts`) ผ่าน rewrite `/api/backend/*`
`proxy.ts` แนบ token จาก cookie ให้เอง จึงไม่ต้องจัดการ token และไม่ติด CORS

---

## 2. ภาพรวม

| | |
|---|---|
| Method | `GET` (เปิดค้างไว้ ไม่จบเอง) |
| Auth | **ต้องใช้ token** (ผ่าน `proxy.ts` อัตโนมัติ) ไม่มี token หรือ token ใช้ไม่ได้ = `401` |
| URL จาก Next.js | `/api/backend/Home/InboxStream` |
| Content-Type | `text/event-stream` |

### ข้อความที่ได้

| ข้อความ | เมื่อไร | ต้องทำอะไร |
|---|---|---|
| `retry: 3000` | ทันทีที่ต่อติด | ไม่ต้องทำ (EventSource ใช้เป็นเวลารอก่อนต่อใหม่) |
| `event: ready` / `data: {}` | ทันทีที่ต่อติด **และทุกครั้งที่ต่อใหม่** | โหลดรายการรับใหม่ (กันงานที่เข้ามาตอนหลุด) ครั้งแรกหลังเปิดหน้าข้ามได้ ถ้าเพิ่งโหลดไป |
| `event: inbox` / `data: {"reason":"…","serviceId":"…"}` | รายการรับอาจเปลี่ยน | **โหลดรายการรับใหม่** |
| `: ping` | ทุก 25 วินาที | ไม่ต้องทำ (EventSource ข้ามให้เอง ใช้กัน Vercel ตัด stream ที่เงียบเกิน 120 วินาที) |

`reason` (ไว้ใช้เลือกข้อความแจ้งเตือน ไม่ต้องใช้ก็ได้):

| `reason` | เกิดจาก |
|---|---|
| `assigned` | มีคนส่งงานใหม่ให้คุณ (`AddNewServiceOperations`) |
| `updated` | งานถูกเปลี่ยนผู้รับ / เปลี่ยนสถานะ หรือเรื่องถูกปิด / เปิดซ้ำ อาจเข้าหรือออกจากรายการรับ |
| `removed` | งานที่รอคุณรับถูกลบ (`DeleteServiceOperations`) |
| `received` | คุณรับงานแล้ว (`ReceiveService`) จากแท็บอื่นหรือเครื่องอื่น ให้แท็บนี้ลดตัวเลขตาม |

### stream ปิดเองเมื่อ

- logout (ปิดภายใน 25 วินาที) แล้วต่อใหม่จะได้ `401`
- token หมดอายุ หรือครบ 30 นาที แล้ว **EventSource ต่อใหม่เองหลัง 3 วินาที** (ได้ `ready` อีกครั้ง → โหลดใหม่)
- เปิดเกิน 10 stream ต่อคน (เช่น 10 แท็บ) ตัวที่เก่าสุดถูกปิด

ถ้าได้ `401` หรือ proxy ตอบ error, EventSource จะปิดถาวร (`readyState === CLOSED`) ให้เปิดใหม่เองเป็นระยะ เหมือน `useServerClock`

---

## 3. ขั้นต่ำ: ให้หน้าแรกโหลดใหม่เมื่อมีสัญญาณ

หน้าแรก (`app/(portal)/home/page.tsx`) โหลดใหม่ด้วยการเพิ่ม `attempt` อยู่แล้ว (ปุ่ม "โหลดใหม่") จึงแค่ให้สัญญาณไปเพิ่ม `attempt`

```ts
// lib/service/useInboxStream.ts
'use client';

import { useEffect, useRef } from 'react';

const STREAM_URL = '/api/backend/Home/InboxStream';
const RETRY_CLOSED_MS = 10_000; // ปิดถาวร (401 / proxy error) → เปิดใหม่เอง
const DEBOUNCE_MS = 300;        // สัญญาณมาติดกันหลายครั้ง (เช่น เปลี่ยนผู้รับ) → โหลดครั้งเดียว

export type InboxSignal = { reason: 'assigned' | 'updated' | 'removed' | 'received'; serviceId: string | null };

/** เรียก onChange เมื่อรายการรับอาจเปลี่ยน (ข้าม ready ครั้งแรก เพราะหน้าเพิ่งโหลดเอง) */
export function useInboxStream(onChange: (signal: InboxSignal | null) => void) {
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  useEffect(() => {
    let es: EventSource | null = null;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    let debounceTimer: ReturnType<typeof setTimeout> | undefined;
    let firstReady = true;

    const fire = (signal: InboxSignal | null) => {
      clearTimeout(debounceTimer);
      debounceTimer = setTimeout(() => onChangeRef.current(signal), DEBOUNCE_MS);
    };

    const connect = () => {
      es = new EventSource(STREAM_URL);
      es.addEventListener('ready', () => {
        if (firstReady) firstReady = false; // เปิดหน้าครั้งแรก: หน้าโหลดเองแล้ว
        else fire(null);                    // ต่อใหม่หลังหลุด: โหลดใหม่ กันพลาดงานช่วงหลุด
      });
      es.addEventListener('inbox', (e) => {
        try {
          fire(JSON.parse((e as MessageEvent<string>).data) as InboxSignal);
        } catch {
          fire(null);
        }
      });
      es.onerror = () => {
        if (es?.readyState === EventSource.CLOSED) {
          es.close();
          retryTimer = setTimeout(connect, RETRY_CLOSED_MS);
        }
      };
    };

    connect();
    return () => {
      clearTimeout(retryTimer);
      clearTimeout(debounceTimer);
      es?.close();
    };
  }, []);
}
```

ใน `HomeContent`:

```ts
const [attempt, setAttempt] = useState(0);
useInboxStream(() => setAttempt((n) => n + 1)); // มีสัญญาณ = โหลดทั้งสองแท็บใหม่ แบบเดียวกับปุ่ม "โหลดใหม่"
```

ผล: เปิดหน้าแรกค้างไว้ แล้วมีคนส่งงานมา ภายในประมาณ 1 วินาทีเรื่องนั้นจะโผล่ในแท็บรายการรับ และตัวเลขที่กระดิ่งเพิ่มขึ้น

**กันพลาดเพิ่ม (แนะนำ):** โหลดใหม่เมื่อกลับมาที่แท็บด้วย (`document.visibilityState === 'visible'`)
เพราะเบราว์เซอร์บางตัวพัก connection ของแท็บที่ไม่ได้เปิดดู

---

## 4. เพิ่มเติม (ไม่บังคับ): แจ้งเตือนทุกหน้า ไม่ใช่แค่หน้าแรก

ถ้าอยากให้เด้งแจ้งเตือนแม้อยู่หน้าอื่น ให้ย้าย stream ไปเปิด **ครั้งเดียวใน AppShell** (ที่เดียวกับนาฬิกา) แล้วแชร์ผ่าน context

- เมื่อได้สัญญาณ `assigned`: แสดง toast "มีงานส่งมาถึงคุณ" พร้อมปุ่มไปหน้ารายการรับ
- ตัวเลขรอรับ: เรียก `GetInboxServices?PageSize=1` แล้วใช้ `meta.pagination.total` แสดงที่เมนู / ไอคอนกระดิ่งบน AppBar
- ชื่อแท็บเบราว์เซอร์: `document.title = total ? `(${total}) ${baseTitle}` : baseTitle`
- แท็บไม่ได้เปิดดูอยู่ (`document.hidden`): ใช้ `Notification` API (ขอสิทธิ์ก่อนครั้งแรก) ให้เด้งที่มุมจอ

ถ้าทำข้อนี้ หน้าแรกควรฟัง context เดียวกัน ไม่เปิด EventSource ซ้ำ (ดูข้อ 5)

---

## 5. ข้อควรระวัง

| เรื่อง | รายละเอียด |
|---|---|
| **เปิด stream เส้นเดียวต่อแท็บ** | อย่าเปิดในหลาย component ซ้ำกัน แต่ละเส้นถือ connection ไว้ตลอด |
| **ตอน dev บน `localhost` (HTTP/1.1)** | เบราว์เซอร์เปิดได้ 6 connection ต่อ origin ถ้าแท็บละ 2 stream (นาฬิกา + รายการรับ) เปิด 3 แท็บจะเต็ม request อื่นจะค้าง ปิดแท็บที่ไม่ใช้ บน Vercel เป็น HTTP/2 จึงไม่มีปัญหานี้ |
| **สัญญาณไม่ใช่ข้อมูล** | อย่าแก้รายการจาก `serviceId` ในสัญญาณเอง ให้โหลดจาก `GetInboxServices` เสมอ (เงื่อนไขอยู่ที่ backend ที่เดียว) |
| **สัญญาณอาจมาเกิน** | เช่น เปลี่ยนผู้รับ ทั้งคนเดิมและคนใหม่ได้สัญญาณ บางครั้งโหลดแล้วรายการไม่เปลี่ยน ถือว่าปกติ |
| **backend หลายเครื่อง** | ตอนนี้ backend เครื่องเดียว ถ้าขยายเป็นหลายเครื่อง backend ต้องเปลี่ยนตัวส่งสัญญาณก่อน (ไม่กระทบ frontend) |

---

## 6. ทดสอบ

1. เปิดหน้าแรกด้วยบัญชี A ค้างไว้ที่แท็บรายการรับ
2. อีกเบราว์เซอร์ (หรือ `.http`) ใช้บัญชี B เพิ่มการดำเนินการในเรื่องใดก็ได้ โดยเลือกผู้รับเป็น A และสถานะเป็นปิดงาน
3. หน้าของ A ต้องมีเรื่องนั้นโผล่ในรายการรับภายในประมาณ 1 วินาที โดยไม่ต้องกดโหลดใหม่
4. A เปิดเรื่องนั้น (`ReceiveService`) แล้วกลับหน้าแรก → เรื่องหายจากรายการรับ แท็บอื่นของ A ก็หายตาม

ดู stream ดิบด้วย curl (ต้องมี token):

```bash
curl -N http://localhost:5018/Home/InboxStream -H "Authorization: Bearer <token>"
```
