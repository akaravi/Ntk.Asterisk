# Karavi.009 — Multi-AMI live sessions

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T13:20:00+03:30`

**Goal:** Close FEATURES `gap-multi-ami-live` — each enabled AMI server can hold a persistent live `ManagerConnection` (not only default + Login/Logoff probe).

**Architecture:** `AmiSession` endpoint map · ops primary = active default (events + SendAction) · secondary keepalive without event fan-out · additive `serverId` on Connect/Disconnect · `ActionConnectAll`.

**Constraints:** v1 additive · legacy ActionConnect/Disconnect without body = primary · GetList uses live sockets when present.

**Hosts:** webapi `:5310` · admin-panel `:5314`

**Skills:** asterisk-voip-stack · asterisk-ami

---

## Part 1 — AmiSession multi-endpoint + API serverId

### Result 1
- **status:** complete
- **verification:** `AmiSession` LiveEndpoint map · `EnsureConnectedAsync(serverId)` · `EnsureAllEnabledConnectedAsync` · `ActionConnect`/`Disconnect` body · `ActionConnectAll` · WebApi Release 0/0
- **hosts:** webapi
- **notes:** no probe Login/Logoff in GetList

---

## Part 2 — Admin Connection per-server Connect/Disconnect + i18n

### Result 2
- **status:** complete
- **verification:** per-row Connect/Disconnect · Connect all · fa/en i18n · `connectAmi(serverId)` / `connectAmiAll`
- **hosts:** admin-panel

---

## Part 3 — Docs + FEATURES + SHIP-READY verify

### Result 3
- **status:** complete
- **verification:** `multi-ami-live-sessions.md` · FEATURES Done · Admin+WebApi production builds
- **hosts:** webapi · admin-panel
