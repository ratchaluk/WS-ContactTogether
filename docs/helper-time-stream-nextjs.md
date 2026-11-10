# Helper `time/stream` → Next.js integration

สรุปสั้น ๆ (TH): เอกสารนี้อธิบายวิธีต่อ SSE endpoint `GET /Helper/time/stream` จากฝั่ง Next.js
เพื่อโชว์ "นาฬิกาเดินสด" ที่ sync เวลาจาก server จริง ไม่ใช่เวลาเครื่อง client. ตัว endpoint เป็น
Server-Sent Events จึงต้องใช้ `EventSource` (ทำงานเฉพาะใน **browser / client component**) ห้ามเรียก
จาก Server Component และห้ามเปิด URL ตรง ๆ ในเบราว์เซอร์ (จะหมุนค้างเพราะ stream ไม่มีวันจบ).

## The endpoint

| | |
|---|---|
| Method / path | `GET /Helper/time/stream` |
| Auth | Anonymous (no token) |
| Content-Type | `text/event-stream` (SSE) |
| Query param | `intervalMs` — push interval, default `1000`, clamped `250..60000` |
| SSE event name | `time` |
| Reconnect hint | server sends `retry: <intervalMs>` so `EventSource` auto-reconnects |

Each frame looks like:

```
event: time
data: {"iso":"2026-10-29T11:29:51+07:00","serverLocal":"2026-10-29 11:29:51","serverUtc":"2026-10-29 04:29:51","unixMs":1793248191875,"timeZone":"SE Asia Standard Time","utcOffset":"07:00:00"}
```

### Payload type

```ts
// types/server-clock.ts
export interface ServerClock {
  /** ISO 8601 with server offset, e.g. "2026-10-29T11:29:51+07:00" */
  iso: string;
  /** "yyyy-MM-dd HH:mm:ss" server local time */
  serverLocal: string;
  /** "yyyy-MM-dd HH:mm:ss" in UTC */
  serverUtc: string;
  /** Unix epoch in milliseconds */
  unixMs: number;
  /** Server time-zone id, e.g. "SE Asia Standard Time" */
  timeZone: string;
  /** Offset from UTC, e.g. "07:00:00" */
  utcOffset: string;
}
```

## Prerequisite: cross-origin access (read this first)

Next.js dev runs on `http://localhost:3000`; the API on `http://localhost:5018`. That is a
**cross-origin** request and the API currently has **no CORS policy**, so a direct
`new EventSource('http://localhost:5018/...')` will be blocked by the browser. Pick one:

### Option A — Next.js rewrite proxy (recommended, no backend change)

Proxy `/api/*` to the backend so the browser only ever talks to its own origin — CORS never applies.

```js
// next.config.js
/** @type {import('next').NextConfig} */
const nextConfig = {
  async rewrites() {
    return [
      {
        source: '/api/backend/:path*',
        destination: `${process.env.BACKEND_ORIGIN ?? 'http://localhost:5018'}/:path*`,
      },
    ];
  },
};
module.exports = nextConfig;
```

Then the client connects to a **relative** URL: `new EventSource('/api/backend/Helper/time/stream')`.

> Note: streaming/SSE passes through Next.js rewrites fine in production (`next start`) and dev.
> If you deploy the frontend to a serverless platform, confirm the host does not buffer the proxied
> stream; a buffering edge can defeat SSE. The API already sets `X-Accel-Buffering: no`.

### Option B — enable CORS on the API (needs a small backend change)

In `Program.cs`, add a policy and apply it before `UseAuthentication()`:

```csharp
// builder
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins("http://localhost:3000") // add your prod origin too
    .AllowAnyHeader()
    .AllowAnyMethod()));

// app (before UseAuthentication/UseAuthorization)
app.UseCors("frontend");
```

Then the client can connect directly to `http://localhost:5018/Helper/time/stream`.
(EventSource does not send credentials/headers by default, which is fine — this endpoint is
anonymous.)

## Base URL via env

```bash
# .env.local
# Only needed for Option B (direct). With Option A you use the relative "/api/backend" path.
NEXT_PUBLIC_API_BASE=http://localhost:5018
# Server-side only, used by next.config.js rewrites (Option A):
BACKEND_ORIGIN=http://localhost:5018
```

## React hook (App Router, client component)

```tsx
// hooks/useServerClock.ts
'use client';

import { useEffect, useRef, useState } from 'react';
import type { ServerClock } from '@/types/server-clock';

type Status = 'connecting' | 'open' | 'error';

// Option A (proxy): '/api/backend/Helper/time/stream'
// Option B (direct + CORS): `${process.env.NEXT_PUBLIC_API_BASE}/Helper/time/stream`
const STREAM_URL = '/api/backend/Helper/time/stream';

export function useServerClock(intervalMs?: number) {
  const [clock, setClock] = useState<ServerClock | null>(null);
  const [status, setStatus] = useState<Status>('connecting');
  const esRef = useRef<EventSource | null>(null);

  useEffect(() => {
    const url = intervalMs ? `${STREAM_URL}?intervalMs=${intervalMs}` : STREAM_URL;
    const es = new EventSource(url);
    esRef.current = es;

    es.onopen = () => setStatus('open');
    es.addEventListener('time', (e: MessageEvent<string>) => {
      try {
        setClock(JSON.parse(e.data) as ServerClock);
      } catch {
        /* ignore malformed frame */
      }
    });
    // EventSource auto-reconnects using the server's `retry:`; just reflect the state.
    es.onerror = () => setStatus('error');

    return () => es.close(); // clean up on unmount / Strict Mode re-run
  }, [intervalMs]);

  return { clock, status };
}
```

## Example component

```tsx
// components/ServerClock.tsx
'use client';

import { useServerClock } from '@/hooks/useServerClock';

export default function ServerClock() {
  const { clock, status } = useServerClock(); // default 1s

  if (!clock) {
    return <span>{status === 'error' ? 'เชื่อมต่อไม่ได้' : 'กำลังเชื่อมต่อ…'}</span>;
  }

  return (
    <div>
      <div style={{ fontVariantNumeric: 'tabular-nums', fontSize: 24 }}>
        {clock.serverLocal}
      </div>
      <small>
        {clock.timeZone} (UTC{clock.utcOffset.startsWith('-') ? '' : '+'}
        {clock.utcOffset})
      </small>
    </div>
  );
}
```

Use it inside any page/layout:

```tsx
import ServerClock from '@/components/ServerClock';

export default function Page() {
  return <main><ServerClock /></main>;
}
```

## Notes & gotchas

- **Client only.** `EventSource` is a browser API. The hook/component must be a client component
  (`'use client'`). Do not call it from a Server Component, Route Handler, or during SSR.
- **Strict Mode double-mount (dev).** React 18 mounts effects twice in dev; the `es.close()` cleanup
  handles it — you may briefly see two connections in the Network tab during development.
- **One connection per tab.** Share one `EventSource` (e.g. via the hook in a top-level layout or a
  context) rather than opening it in many components.
- **Reconnection is automatic.** On a dropped connection `EventSource` reconnects after the
  server's `retry:` delay (= `intervalMs`). No manual retry loop needed.
- **`serverLocal` is already Thai local time** (the API does no time-zone conversion), so display it
  as-is. If you need to compute drift against the browser clock, use `unixMs`.
- **No ticking between frames.** The clock updates once per frame (default 1s). If you want a
  smoother display, interpolate client-side from `unixMs` with `requestAnimationFrame`, but then you
  are back to trusting the client clock between frames.
- **Do not test in the `.http`/Swagger/address bar** — they wait for the response to finish and the
  stream never ends, so they appear to hang. Use the browser `EventSource` or `curl -N`.
