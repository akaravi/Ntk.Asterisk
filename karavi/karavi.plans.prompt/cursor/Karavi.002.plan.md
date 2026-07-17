# Karavi.002 — Wave 1 WebApi + Admin/User panels (Asterisk ops)

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-17T20:30:00+03:30`

**Goal / هدف:** Bootstrap test/ops stack without user accounting — shared WebApi (AMI) + AdminPanel (monitor/settings/control) + UserPanel (call job forms).

**Architecture:** AMI-only via `Ntk.AsterNet.AMI`; in-memory CallJob + SignalR; no auth Wave 1.

**Tech stack:** .NET 8 Web API · Angular 19 ×2 · envelope `{ isSuccess, data[], errorMessage }` · i18n fa-first.

**Hosts:** `webapi` `:5310` · `admin-panel` `:5314` · `user-panel` `:5312`

---

## Part 1 — D01 WebApi bootstrap

### Request
Scaffold `Ntk.Asterisk.WebApi` net8, AMI ProjectReference, SatelliteResourceLanguages, CORS, invariant appsettings.

### Result 1
- **status:** complete
- **verification:** `dotnet build Ntk.Asterisk.sln -c Release` green; project in `Ntk.Asterisk.sln`
- **hosts:** webapi
- **notes:** overlays hold AMI host/port/user/secret

---

## Part 2 — D02 AMI session + connection health

### Request
`IAmiSession` + `GET /api/v1/Asterisk/Connection/GetStatus`

### Result 2
- **status:** complete
- **verification:** AmiSession Login/reconnect/PingInterval; GetStatus returns connected/configured/flags
- **hosts:** webapi
- **notes:** skills: asterisk-voip-stack + asterisk-ami

---

## Part 3 — D03 API envelope + correlation

### Request
`ApiResult<T>`, correlation middleware, structured scope logs

### Result 3
- **status:** complete
- **verification:** `X-Correlation-Id` middleware; controllers return envelope
- **hosts:** webapi

---

## Part 4 — D04 Monitoring inventory

### Request
Peers / Channels / Trunks GetList + AMI event push

### Result 4
- **status:** complete
- **verification:** SIPPeersAction + StatusAction; TrunkPeerFilter; SignalR `amiEvent`
- **hosts:** webapi

---

## Part 5 — D05 Control actions

### Request
Hangup / Bridge as CommandJobs

### Result 5
- **status:** complete
- **verification:** `ActionHangup`, `ActionBridge` → CallJobEngine
- **hosts:** webapi

---

## Part 6 — D06 CallJob engine

### Request
In-memory store/queue/cancel + state machine

### Result 6
- **status:** complete
- **verification:** Add/GetList/GetOne/ActionCancel; Channel queue worker
- **hosts:** webapi

---

## Part 7 — D07–D09 Dial flows

### Request
ExtToExt, MobileToExt, MobileToMobile Async Originate Application=Dial

### Result 7
- **status:** complete
- **verification:** CallJobEngine BuildOriginate + ActionID correlation
- **hosts:** webapi
- **notes:** live PBX optional — residual risk if Asterisk unreachable

---

## Part 8 — D10 SignalR

### Request
Hub `/hubs/asterisk` job + monitor events

### Result 8
- **status:** complete
- **verification:** AsteriskHub Path; jobUpdated + connectionStatus + amiEvent
- **hosts:** webapi, admin-panel, user-panel

---

## Part 9 — D11 AdminPanel

### Request
RTL fa-first: connection, settings, monitor, jobs + list toolbar

### Result 9
- **status:** complete
- **verification:** `ng build --configuration=production` AdminPanel green; port 5314
- **hosts:** admin-panel

---

## Part 10 — D12 UserPanel

### Request
Call forms + jobs list + SignalR; no AMI secrets UI

### Result 10
- **status:** complete
- **verification:** `ng build --configuration=production` UserPanel green; port 5312
- **hosts:** user-panel

---

## Part 11 — D13 Dialplan contract doc

### Request
`karavi/karavi.doc/asterisk-dialplan-contract.md`

### Result 11
- **status:** complete
- **verification:** file present; PJSIP/SIP, trunk, AMI classes documented
- **hosts:** —

---

## Part 12 — D14 Karavi reconcile + verify

### Request
Ports, build-profiles, version-manifest, verify-gates, onboarding, history

### Result 12
- **status:** complete
- **verification:**
  - `dotnet build Ntk.Asterisk.sln -c Release` — 0 errors
  - `ng build` AdminPanel + UserPanel production — green
  - no auth; no FTP/Deploy; appsettings base invariant
- **hosts:** webapi, admin-panel, user-panel

---

## Deferred (Wave 2+)

D20 Auth · D21 Persist jobs · D22 ARI · D23 CallFile · D24 PDF/Excel · D25 Role tokens
