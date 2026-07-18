# Karavi.004 — Admin Monitor Bridge UI

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T12:55:00+03:30`

**Goal:** Close FEATURES `gap-bridge-ui` — expose existing `POST /api/v1/Asterisk/Channels/ActionBridge` on Admin Monitor (pick two live channels → Bridge).

**Architecture:** Additive UI only; API unchanged. Selection state client-side; tone default `no`.

**Hosts:** admin-panel `:5314` · webapi (existing ActionBridge)

---

## Part 1 — Plan + FEATURES gap update

### Result 1
- **status:** complete
- **verification:** Karavi.004.plan.md · FEATURES gap-bridge-ui updated

---

## Part 2 — Angular client BridgeRequest + API

### Result 2
- **status:** complete
- **verification:** `BridgeRequest` · `AsteriskApiService.bridgeChannels`

---

## Part 3 — Monitor bridge strip + channel pick + i18n

### Result 3
- **status:** complete
- **verification:** bridge-strip · BRIDGE_PICK on All/Channels · MONITOR.BRIDGE_* fa/en

---

## Part 4 — SHIP-READY verify

### Result 4
- **status:** complete
- **verification:** `ng build` AdminPanel production green · WebApi Release to alt outdir green (default outdir locked by running WebApi PID)
- **hosts:** admin-panel · webapi (API unchanged)
