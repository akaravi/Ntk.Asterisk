# Karavi.005 — VOIZ-WebPhone → Ntk.Asterisk.WebApi

**planVersion:** `1.4.1` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T13:35:00+03:30`

**Note:** Cursor draft used number 003; repo already has Karavi.003 (ChanSpy) and Karavi.004 (Queue Panel) — this plan is **005**.

**Goal / هدف:** Host Voip.VOIZ-WebPhone softphone under WebApi `/webphone` and replace PHP provision with envelope APIs; add buddies/CDR/BLF-MWI/recording-QoS server support. Angular out of scope.

**Architecture:** `static_plus_api` — SipJS from `wwwroot/webphone` · REST `{ isSuccess, data[], errorMessage }` · AMI ExtensionStatus/MessageWaiting → SignalR · JSON stores under `App_Data/webphone-*.json`.

**Tech stack:** .NET 8 WebApi · Ntk.AsterNet.AMI · AGPL vendor NOTICE · appsettings env overlays for SIP/WSS.

**Skills:** asterisk-voip-stack · asterisk-ami

**Inventory:** `karavi/karavi.doc/voiz-webphone-features.md`

**Hosts:** `webapi` `:5310` only

---

## Wave 1 — Catalog + static host + provision

### Part 1 — Feature catalog

#### Request
Write `voiz-webphone-features.md` with stable ids A–N and gap matrix.

#### Result 1
- **status:** complete
- **verification:** `karavi/karavi.doc/voiz-webphone-features.md`
- **hosts:** docs

---

### Part 2 — Orchestrator + history

#### Request
This plan file + history entry for 2026-07-18.

#### Result 2
- **status:** complete
- **verification:** Karavi.005 + history.2026-07-18.md
- **hosts:** docs

---

### Part 3 — Static softphone host

#### Request
Vendor Phone + lib under `wwwroot/webphone` with AGPL LICENSE/NOTICE; `UseDefaultFiles` + `UseStaticFiles`; hook `loadSipConfigFromServer` to NTK GetSipConfig; prefer local lib over CDN.

#### Result 3
- **status:** complete
- **verification:** `wwwroot/webphone` (~110 files) · `vendor-voiz/LICENSE`+`NOTICE` · `Program.cs` static `/webphone` · `phone.js` → `/api/v1/WebPhone/GetSipConfig` · `index.html` self-hosted lib
- **hosts:** webapi

---

### Part 4 — SipProvision API

#### Request
`GetSipConfig` + `App_Data/webphone-extensions.json`; no secrets in logs.

#### Result 4
- **status:** complete
- **verification:** `WebPhoneController.GetSipConfig` · `WebPhoneExtensionStore` · VOIZ JSON property names · example `webphone-extensions.json.example` · auto-seed example→live when missing · local `App_Data/webphone-extensions.json` seeded (gitignored; placeholder `change-me-local-only`)
- **hosts:** webapi

---

### Part 5 — Server config SIP/WSS additive

#### Request
Additive fields on `AsteriskServerConfig` / DTO / settings mapping: SipWebsocketUrl, SipDomain, WebSocketPath, WebSocketPort, StunServersJson, SipUseTls.

#### Result 5
- **status:** complete
- **verification:** `AsteriskServerConfig` + `AsteriskOptions` + DTO Add/Update + `ApplySipFields` · `dotnet build` Release 0/0 (alt outdir; default bin locked by running process)
- **hosts:** webapi

---

## Wave 2 — Contacts / CDR / BLF-MWI

### Part 6 — Buddies CRUD

#### Request
`webphone-buddies.json` + entity routes GetList/GetOne/Add/Update/ActionDelete.

#### Result 6
- **status:** complete
- **verification:** `WebPhoneBuddyStore` · routes under `api/v1/WebPhone/Buddies/*`
- **hosts:** webapi

---

### Part 7 — CDR ingest + GetList

#### Request
Client CDR Add + GetList with pagination/sort/quickSearch.

#### Result 7
- **status:** complete
- **verification:** `WebPhoneCdrStore` · `Cdr/GetList` + `Cdr/Add`
- **hosts:** webapi

---

### Part 8 — BLF / MWI SignalR

#### Request
Hosted service: AMI ExtensionStatus/DeviceStateChange/MessageWaiting → hub events `webphonePresence` / `webphoneMwi`; SubscribeWebPhone on hub.

#### Result 8
- **status:** complete
- **verification:** `WebPhonePresenceService` · `AsteriskHub.SubscribeWebPhone` · AmiSession `MessageWaiting` wired
- **hosts:** webapi

---

## Wave 3 — Recording / QoS / flags

### Part 9 — Client recording upload

#### Request
POST binary → `App_Data/webphone-recordings/` + metadata JSON.

#### Result 9
- **status:** complete
- **verification:** `WebPhoneRecordingStore` · `Recordings/Add|GetList|GetOne`
- **hosts:** webapi

---

### Part 10 — QoS ingest + WebPhoneOptions

#### Request
QoS JSON store + `WebPhoneOptions` feature flags (EnableTransfer/Conference/RecordAll) in config.

#### Result 10
- **status:** complete
- **verification:** `WebPhoneQosStore` · `WebPhoneOptions` in appsettings base + overlays · `Config/GetWebPhoneOptions`
- **hosts:** webapi

---

## Wave 4 — Auth gate + PWA hardening

### Part 11 — Optional API-key gate on GetSipConfig

#### Request
Additive `WebPhone:RequireApiKey` + `WebPhone:ApiKey`; gate active only when both set; headers `X-WebPhone-Api-Key` / `X-Api-Key`; Development open; phone.js sends key when configured; never log secret.

#### Result 11
- **status:** complete
- **verification:** `WebPhoneOptions.RequireApiKey/ApiKey/IsApiKeyGateActive` · `WebPhoneController.TryAuthorizeApiKey` on GetSipConfig + Extensions write · `WebPhoneOptionsDto.RequireApiKey` flag · phone.js header · Dev overlay open · Prod RequireApiKey=true with empty ApiKey still open until secret set
- **hosts:** webapi

---

### Part 12 — PWA service-worker local-lib hardening

#### Request
Replace CloudFront CDN precache URLs with self-hosted `lib/*`; bump cache id; purge old caches; never cache `/api/*`.

#### Result 12
- **status:** complete
- **verification:** `wwwroot/webphone/sw.js` cacheID `ntk-webphone-v2` (was v1 at Part 12 land; bumped in Wave 5) · local lib paths only · activate deletes legacy caches · fetch bypass for `/api/`
- **hosts:** webapi

---

## Wave 5 — Client sync bridge + dialplan/WSS docs

### Part 13 — Softphone → WebApi sync bridge

#### Request
Additive `ntk-webphone-bridge.js`: wrap CDR/QoS/recording/buddy saves to REST; pull server buddies; poll Presence/MWI; apply GetWebPhoneOptions; load from index.html; bump SW cache.

#### Result 13
- **status:** complete
- **verification:** `wwwroot/webphone/ntk-webphone-bridge.js` · index.html script · sw.js `ntk-webphone-v3` · phone.js Features from GetSipConfig · Presence/Mwi body+query (SHIP-READY 1.4.1)
- **hosts:** webapi

---

### Part 14 — D-DIALPLAN-MSG + D-DOCKER docs

#### Request
Operator docs for SIP MESSAGE dialplan and Asterisk WSS/provision smoke.

#### Result 14
- **status:** complete
- **verification:** `karavi/karavi.doc/webphone-sip-message-dialplan.md` · `karavi/karavi.doc/webphone-asterisk-wss-setup.md`
- **hosts:** docs

---

## Deferred stubs (Wave 6+)

| id | title | dependsOn | wave | status |
|----|-------|-----------|------|--------|
| D-XMPP | XMPP/Openfire full stack | H03 | 6 | deferred |
| D-SFU | SFU multi-stream server | C05 | 6 | deferred |
| D-NG | Angular softphone rewrite (no AGPL UI) | L01 | 6 | deferred |

---

## Acceptance

- Wave 1–4: **complete** (see prior notes)
- Wave 5: **complete** · client sync bridge · dialplan MESSAGE doc · WSS setup doc
- **SHIP-READY 2026-07-18 (planVersion 1.4.1):** fixed Presence/Mwi ActionQuery to accept body+query (bridge contract) · WebApi Release exit 0 · solution Release exit 0 · secrets-scan PASS · docs present · seed example present · Wave 6+ deferred unchanged
- Residual: live SIP register needs real Asterisk WSS + PJSIP secret · AGPL §13 · D-XMPP/D-SFU/D-NG · softphone uses REST poll for presence/MWI (SignalR hub remains for Admin subscribers)

---

## Amendment 2026-07-18 — UI host split (API-only WebApi)

- Softphone UI moved from `Ntk.Asterisk.WebApi/wwwroot/webphone` → `src/WebPhone/Ntk.Asterisk.WebPhone` (port **5316**).
- WebApi no longer hosts static UI (`webapi-api-only-no-ui` rule).
- Client uses `WebPhoneUi:ApiBaseUrl` / `phoneOptions.webPhoneApiBase` + CORS to call WebApi.
- Historical Parts above remain valid for API/stores; path references to `wwwroot/webphone` are superseded by `src/WebPhone/.../wwwroot`.
