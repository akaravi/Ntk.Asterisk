# WebPhone — Asterisk WSS / Docker setup for NTK softphone

**Plan:** Karavi.005 · **id:** D-DOCKER · **UI:** `http://localhost:5316/` · **API:** `http://localhost:5310`

The softphone registers over **SIP WebSocket** to Asterisk, not to WebApi. WebApi is **API-only** (provision `GetSipConfig` + stores). Softphone UI is `src/WebPhone/Ntk.Asterisk.WebPhone`.

## Required PBX surface

| Item | Typical value | Maps to |
|------|---------------|---------|
| WSS host | PBX IP/DNS | `AsteriskServerConfig.SipWebsocketUrl` or `Host` |
| WSS port | `8089` | `WebSocketPort` |
| Path | `/ws` | `WebSocketPath` |
| SIP domain | same as host or realm | `SipDomain` |
| TLS | true for `wss://` | `SipUseTls` |
| Extension secret | PJSIP `auth` password | `App_Data/webphone-extensions.json` |

## Provision file (WebApi)

Copy/seed:

`src/DotNet/Ntk.Asterisk.WebApi/App_Data/webphone-extensions.json.example`
→ `webphone-extensions.json` (gitignored).

Replace `change-me-local-only` with the real PJSIP secret. Set `serverId` to the default AMI server id when multi-server.

## Admin server SIP fields

In Settings / `AsteriskServers` Update, set additive fields:

- `sipWebsocketUrl` e.g. `wss://pbx.example.com:8089/ws` (optional full URL)
- `webSocketPort` = `8089`
- `webSocketPath` = `/ws`
- `sipDomain`
- `sipUseTls` = true for WSS
- `stunServersJson` optional JSON array for ICE

## Minimal PJSIP WebRTC endpoint (sketch)

```ini
[transport-wss]
type=transport
protocol=wss
bind=0.0.0.0:8089
cert_file=/etc/asterisk/keys/asterisk.crt
priv_key_file=/etc/asterisk/keys/asterisk.key

[1001]
type=endpoint
context=from-internal
disallow=all
allow=opus,ulaw
webrtc=yes
auth=1001
aors=1001
message_context=messages

[1001]
type=auth
auth_type=userpass
username=1001
password=REAL-SECRET

[1001]
type=aor
max_contacts=5
remove_existing=yes
```

## Docker / Issabel

- VOIZ upstream ships a full Asterisk+phone Docker image; NTK does **not** vendor that image into this repo (AGPL + ops scope).
- Prefer existing lab Asterisk or Issabel with WebSocket SIP enabled (`http.conf` / `pjsip` WSS transport).
- Issabel menu XML / `install.sh` from VOIZ remain external reference only.

## Smoke checklist

1. Start WebApi (5310) and WebPhone host (5316).
2. Open `http://localhost:5316/`.
3. Confirm browser console: `NTK WebPhone: SIP config loaded` + `NTK WebPhone bridge active`.
4. Confirm REGISTER 200 from Asterisk.
5. Place audio call; confirm `Cdr/GetList` grows after hangup.
6. Optional: enable `WebPhone:RequireApiKey` + `ApiKey` in Production overlay; set `phoneOptions.webPhoneApiKey`.
7. Production WebPhone: set `WebPhoneUi:ApiBaseUrl` to the public WebApi origin.

## Still deferred

- Full Docker compose for Asterisk inside Ntk.Asterisk monorepo (D-DOCKER remaining ops pack).
- Angular softphone rewrite without AGPL UI (D-NG).
- XMPP / SFU (D-XMPP / D-SFU).
