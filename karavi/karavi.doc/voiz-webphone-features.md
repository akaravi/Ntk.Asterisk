# VOIZ-WebPhone feature catalog → Ntk.Asterisk

**Source (read-only):** `D:/SourceKaravi/GitTest/Voip.VOIZ-WebPhone`  
**API host:** `src/DotNet/Ntk.Asterisk.WebApi` (API only — no UI)  
**UI host:** `src/WebPhone/Ntk.Asterisk.WebPhone` (port 5316)  
**Shape:** `ui_plus_api` — SipJS softphone separate host + .NET envelope APIs over CORS  
**License:** AGPL-3.0 (vendored UI must retain LICENSE/NOTICE)  
**Plan:** `karavi/karavi.plans.prompt/cursor/Karavi.005.plan.md` (Cursor draft numbered 003; 003/004 already used)
**Rule:** `.cursor/rules/webapi-api-only-no-ui.mdc`

Status legend: `done` · `wave1` · `wave2` · `wave3` · `deferred` · `client-only` (SipJS in browser; no server work)

---

## A — SIP register / transport

| id | Feature | VOIZ | WebApi gap | status |
|----|---------|------|------------|--------|
| A01 | WebSocket SIP register (chan_sip / PJSIP) | phone.js + SipJS | Needs WSS URL provision | done |
| A02 | Provision credentials (`get_sip_config.php`) | PHP + Issabel MySQL | Replace with GetSipConfig + JSON store | done |
| A03 | STUN / ICE servers | localStorage / options | Additive server fields | done |
| A04 | WSS vs WS transport toggle | client | Config `SipUseTls` | done |
| A05 | Multi-profile / profileName | PHP cid_name | SipConfig DTO | done |

## B — Audio calling

| id | Feature | status |
|----|---------|--------|
| B01 | Outbound/inbound audio | client-only |
| B02 | Hold / mute / DTMF | client-only |
| B03 | Device select (mic/speaker/ringer) | client-only |
| B04 | Auto-answer / DND | client-only |

## C — Video calling

| id | Feature | status |
|----|---------|--------|
| C01 | Video call | client-only |
| C02 | Screen share | client-only |
| C03 | Scratchpad share (FabricJS) | client-only |
| C04 | Video/audio file share in-call | client-only |
| C05 | Asterisk SFU talker / CID | deferred (full SFU); dialplan notes below |

## D — Transfer / conference

| id | Feature | status |
|----|---------|--------|
| D01 | Blind transfer | client-only + feature flag | done |
| D02 | Attended transfer | client-only + feature flag | done |
| D03 | 3rd-party conference | client-only + feature flag | done |

## E — CDR

| id | Feature | VOIZ | WebApi | status |
|----|---------|------|--------|--------|
| E01 | Local IndexedDB CDR | client | Server ingest + GetList | done |
| E02 | Pagination / sort / quickSearch | — | GetList contract | done |

## F — Recording

| id | Feature | VOIZ | WebApi | status |
|----|---------|------|--------|--------|
| F01 | Client audio/video recording | IndexedDB / download | Upload → `App_Data/webphone-recordings/` | done |
| F02 | MixMonitor server recording | — | Existing CallJobs path (unchanged) | done |

## G — QoS / diagnostics

| id | Feature | status |
|----|---------|--------|
| G01 | Call QoS stats ingest | done |
| G02 | Console debug hooks | client-only |

## H — Messaging

| id | Feature | status |
|----|---------|--------|
| H01 | SIP MESSAGE (text/plain) | done (doc: `webphone-sip-message-dialplan.md`) |
| H02 | SIP message accept notification | client-only |
| H03 | XMPP / Openfire full stack | deferred |

## I — Buddies / contacts

| id | Feature | status |
|----|---------|--------|
| I01 | Buddy CRUD | done |
| I02 | Subscribe device state (BLF hint) | done |
| I03 | vCard / picture (XMPP) | deferred (needs H03) |

## J — Presence BLF / MWI

| id | Feature | AMI | status |
|----|---------|-----|--------|
| J01 | Extension / device state → UI | ExtensionStatus / DeviceStateChange → SignalR `webphonePresence` | done |
| J02 | Message Waiting Indicator | MessageWaiting / MailboxCount → SignalR `webphoneMwi` | done |

## K — Settings / themes / PWA

| id | Feature | status |
|----|---------|--------|
| K01 | Dark/light mode | client-only |
| K02 | Multi-language packs | client-only (vendored lang/) |
| K03 | PWA service worker / offline | done (Wave 4: local lib SW, no CDN) |
| K04 | Feature flags server-side | done (`WebPhoneOptions` + bridge apply) |
| K05 | Client→API sync bridge | done (`ntk-webphone-bridge.js` CDR/QoS/recording/buddies + presence/MWI poll) |

## L — Hosting / ops

| id | Feature | status |
|----|---------|--------|
| L01 | Static host `src/WebPhone` (port 5316) | done (moved off WebApi wwwroot) |
| L02 | AGPL LICENSE + NOTICE | done |
| L03 | Docker / Issabel install scripts | done (WSS setup doc `webphone-asterisk-wss-setup.md`; full Issabel install still operator-side) |
| L04 | Auth on GetSipConfig | done (Wave 4: optional `RequireApiKey` + `X-WebPhone-Api-Key`) |
| L05 | Local extension seed | done |

### Local SIP seed (GetSipConfig smoke)

1. Committed template: `src/DotNet/Ntk.Asterisk.WebApi/App_Data/webphone-extensions.json.example` (placeholder password `change-me-local-only` — not a production secret).
2. Live file (gitignored): `App_Data/webphone-extensions.json`.
3. On WebApi start, if the live file is missing and the example exists, `WebPhoneExtensionStore` copies example → live automatically.
4. Manual seed: `Copy-Item App_Data\webphone-extensions.json.example App_Data\webphone-extensions.json`.
5. Replace `sipPassword` with a real Asterisk/PJSIP secret and set `serverId` / SIP WSS fields on the Asterisk server config before expecting register over WSS.

### GetSipConfig API-key gate (L04)

1. Config (additive): `WebPhone:RequireApiKey` + `WebPhone:ApiKey`.
2. Gate **active** only when `RequireApiKey=true` **and** `ApiKey` is non-empty.
3. Development: `RequireApiKey=false` → open (non-breaking).
4. Production overlay: `RequireApiKey=true`; set a non-empty `ApiKey` to enforce; empty ApiKey keeps open until operator configures secret.
5. Client sends header `X-WebPhone-Api-Key` (alias `X-Api-Key`) via `phoneOptions.webPhoneApiKey`, query `?webphoneApiKey=`, or `localStorage.webPhoneApiKey`.
6. Also gated: `Extensions/Add|Update|ActionDelete`. `Config/GetWebPhoneOptions` exposes `requireApiKey` boolean only (never the secret).

### Dialplan notes (PBX-side — not implemented in WebApi)

**H01 — SIP MESSAGE:** See `karavi/karavi.doc/webphone-sip-message-dialplan.md` (`message_context` + `[messages]` MessageSend). Softphone client remains SipJS MESSAGE; WebApi does not terminate MESSAGE.

**C05 — SFU:** Multi-party video SFU requires Asterisk SFU / ConfBridge video mix + dialplan — out of WebApi scope; softphone already has peer video (client-only). Deferred as D-SFU.

**WSS ops:** See `karavi/karavi.doc/webphone-asterisk-wss-setup.md`.

## M — Gap vs Ntk.Asterisk today

| Area | Before | After waves |
|------|--------|-------------|
| Softphone UI | none | Wave 1 static SipJS |
| SIP provision | none | Wave 1 GetSipConfig |
| Buddies / CDR store | none | Wave 2 JSON |
| BLF / MWI push | peers/monitor only | Wave 2 dedicated SignalR |
| Client recording / QoS | MixMonitor only | Wave 3 ingest |
| XMPP / SFU / Angular softphone | — | deferred (Wave 6+) |
| GetSipConfig auth | open Wave 1 | Wave 4 optional API-key |
| PWA SW CDN | CloudFront leftovers | Wave 4 local `lib/*` |
| Client local-only CDR/QoS | IndexedDB only | Wave 5 bridge → WebApi stores |

## N — API surface (target)

| Route | Wave |
|-------|------|
| `GET/POST /api/v1/WebPhone/GetSipConfig` | 1 |
| `GET/POST …/Buddies/GetList|GetOne|Add|Update|ActionDelete` | 2 |
| `GET/POST …/Cdr/GetList|Add` | 2 |
| SignalR `webphonePresence` / `webphoneMwi` | 2 |
| `POST …/Recordings/Add` · `…/Qos/Add` | 3 |
| `GET …/Config/GetWebPhoneOptions` | 3 (+ Wave4 `requireApiKey` flag) |
| Headers `X-WebPhone-Api-Key` on GetSipConfig when gate active | 4 |

Envelope: `{ isSuccess, data[], errorMessage }` (+ pagination root fields on list endpoints).
