# Karavi.003 — ChanSpy / ExtenSpy supervisor integration (AMI + Admin)

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T11:45:00+03:30`

**Goal / هدف:** Productize Voip.AsteriskChanSpyPro ExtenSpy modes (listen / quiet / whisper / privateWhisper / barge / dtmf) inside Ntk.Asterisk WebApi + Admin Monitor via AMI Originate Application=ExtenSpy. Feature inventory stored in ChanSpyPro `FEATURES.md`.

**Architecture:** Reuse CallJobEngine command-job pattern (Hangup/Bridge). Originate supervisor channel → Application=ExtenSpy Data=`{ext},{flags}`. Additive API only. No auth change.

**Tech stack:** .NET 8 WebApi · Angular 19 AdminPanel · Ntk.AsterNet.AMI OriginateAction · i18n fa-first.

**Skills:** asterisk-voip-stack · asterisk-ami · asterisk-dialplan (ExtenSpy flags from ChanSpyPro install.sh).

**Reference:** `D:/SourceKaravi/GitTest/Voip.AsteriskChanSpyPro/FEATURES.md` · `install.sh` ExtenSpy flags.

**Hosts:** webapi `:5310` · admin-panel `:5314`

---

## Part 1 — Feature inventory + plan artifacts

### Request
Write FEATURES.md to ChanSpyPro; write this plan in Ntk karavi + mirror in ChanSpyPro.

### Result 1
- **status:** complete
- **verification:** FEATURES.md + PLAN mirror + Karavi.003 in Ntk
- **hosts:** docs
- **notes:** inventory covers Ntk Wave 1 + ChanSpyPro + gap matrix

---

## Part 2 — DTOs + CallJobType CommandChanSpy

### Request
Add SpyMode enum mapping, ChanSpyRequest DTO, CallJobType.CommandChanSpy, EnqueueChanSpyAsync.

### Result 2
- **status:** complete
- **verification:** Dtos.ChanSpyRequest · CallJobModels.CommandChanSpy · EnqueueChanSpyAsync
- **hosts:** webapi

---

## Part 3 — CallJobEngine ExtenSpy Originate

### Request
Execute CommandChanSpy: build tech/supervisor channel, Application=ExtenSpy, Data=target+flags per ChanSpyPro matrix; complete/fail from AMI response.

### Result 3
- **status:** complete
- **verification:** ExecuteChanSpyAsync + TryResolveExtenSpyFlags
- **hosts:** webapi

---

## Part 4 — ChannelsController ActionChanSpy

### Request
`POST /api/v1/Asterisk/Channels/ActionChanSpy` → envelope CallJobDto. Additive; Hangup/Bridge unchanged.

### Result 4
- **status:** complete
- **verification:** ChannelsController.ActionChanSpy
- **hosts:** webapi

---

## Part 5 — Admin Monitor UI + i18n

### Request
Supervisor extension input; spy actions on in-call extensions and channels; fa/en keys; action feedback.

### Result 5
- **status:** complete
- **verification:** monitor-page spy-strip + spy-actions · MONITOR.SPY_* fa/en
- **hosts:** admin-panel

---

## Part 6 — Docs + history + SHIP-READY verify

### Request
karavi.doc note + history; update Results; `dotnet build` + AdminPanel build.

### Result 6
- **status:** complete
- **verification:** Re-verify 2026-07-18T11:25+03:30 — `dotnet build` WebApi Release 0/0 · `ng build` AdminPanel production green (scss budget warn only)
- **hosts:** webapi, admin-panel, docs
- **notes:** docs `karavi.doc/chanspy-extenspy-ami.md` · history 2026-07-18 · all Parts 1–6 complete; no remaining plan work
---

## ExtenSpy flag matrix (binding)

| Mode | Flags | ChanSpyPro code |
|------|-------|-----------------|
| listen | Eq | *30 |
| quiet | Eqo | *31 |
| whisper | Eqw | *32 |
| privateWhisper | EqW | *33 |
| barge | EqB | *34 |
| dtmf | Eqd | *35 |
