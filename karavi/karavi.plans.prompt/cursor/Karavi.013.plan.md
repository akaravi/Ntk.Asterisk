# Karavi.013 — WebPhone surfaces on AdminPanel + UserPanel

**planVersion:** 1.0.1  
**promptSpecVersion:** 1.5.0  
**updatedAt:** 2026-07-18  
**Status:** complete  

**Goal / هدف:** Surface all WebPhone backend APIs in Angular Admin (settings/management) and User (consume + softphone deep-link). Softphone UI remains `src/WebPhone` (port 5316) — deeplink only (1A). Scope 2A: Admin manages Extensions/SIP-WSS/lists; User gets softphone link + CDR/Buddies/Recordings (no Extensions).

**Architecture:** WebApi API-only · AdminPanel 5314 · UserPanel 5312 · WebPhone host 5316 · CORS already allows 5316.

**Defaults locked:** softphone = external link · Extensions = Admin only · Options = read-only display from GetWebPhoneOptions (flags from appsettings; RequireApiKey boolean only).

---

## Part 1 — Shared client + env

### Tasks
- Models `webphone.models.ts` Admin + User
- `WebPhoneApiService` wrapping `/api/v1/WebPhone/*`
- `environment.webPhoneUrl` = `http://localhost:5316`

### Result 1
- **status:** complete
- **verification:** Admin `core/models/webphone.models.ts` + `webphone-api.service.ts`; User `core/models/webphone.ts` + `webphone.api.ts`; both env `webPhoneUrl`
- **hosts:** AdminPanel · UserPanel

---

## Part 2 — Admin Settings SIP/WSS

### Tasks
- Extend `AsteriskServer` / update requests with SipWebsocket* fields
- Settings SECTION_WEBPHONE form + save/load

### Result 2
- **status:** complete
- **verification:** Settings form SECTION_WEBPHONE + SIP/WSS fields on server model; i18n `SETTINGS.SECTION_WEBPHONE*`
- **hosts:** AdminPanel

---

## Part 3 — Admin WebPhone pages

### Tasks
- Routes + nav (adminRoleGuard): extensions, buddies, cdr, recordings, qos
- Extensions CRUD page + options readout + Open Softphone
- Buddies CRUD; CDR/Recordings/QoS list pages with list toolbar

### Result 3
- **status:** complete
- **verification:** Routes under `/webphone/*`; lazy chunks in production build; nav WebPhone group
- **hosts:** AdminPanel 5314

---

## Part 4 — UserPanel WebPhone

### Tasks
- Softphone nav deep-link; routes cdr/buddies/recordings (read + buddy add)
- i18n fa/en

### Result 4
- **status:** complete
- **verification:** Nav softphone button + buddies/cdr/recordings routes; `.nav-softphone` styles; i18n in `i18n.service.ts`
- **hosts:** UserPanel 5312

---

## Part 5 — Verify

### Tasks
- AdminPanel + UserPanel production build
- history entry

### Result 5
- **status:** complete
- **verification:** `ng build --configuration=production` AdminPanel exit 0 · UserPanel exit 0 (budget warnings only on pre-existing SCSS)
- **notes:** WebPhone Options remain read-only (no Update API). Extensions/SIP secrets Admin-only by design.
