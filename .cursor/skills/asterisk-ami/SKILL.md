---
name: asterisk-ami
description: >-
  Asterisk Manager Interface (AMI) Actions, Events, Responses, Originate,
  manager.conf, AJAM/HTTP, and Ntk.AsterNet.AMI ManagerConnection. Use when
  building monitoring, auto-dialers, CRM telephony, login/events, or .NET AMI
  clients against port 5038.
---

# Asterisk AMI

Canonical source: `karavi/karavi.doc/Learn/voiping_ami_programing_tutorial.pdf` (VoiPing.ir).

**Prerequisite:** [asterisk-voip-stack](../asterisk-voip-stack/SKILL.md). Outbound batch alternative: [asterisk-callfile](../asterisk-callfile/SKILL.md).

## What AMI is

- **AMI** = Asterisk Manager Interface — **asynchronous**, message-oriented control & monitoring.
- Default TCP port: **5038**.
- Security is critical — treat as privileged admin plane.
- Message classes:
  - **Action** → request (client → Asterisk), expects **Response**
  - **Event** → one-way notifications (Asterisk → client)
- Wire format: `Key: Value` lines ended with `\r\n`; message ended with `\r\n\r\n`.

## Message shapes

### Action

```text
Action: Login
Username: <user>
Secret: <secret>
```

Almost every session starts with **Login**.

### Event

```text
Event: Hangup
Privilege: call,all
...
```

Minimum: `Event` + `Privilege`.

### Response

```text
Response: Success
Message: Authentication accepted
```

`Response` values: `Success` | `Error` | `Follows`.

List-style actions:

```text
Response: Success
EventList: start
Message: Channels will follow
...
Event: CoreShowChannelsComplete
EventList: Complete
ListItems: N
```

## Configuration

`/etc/asterisk/manager.conf`:

```ini
[general]
; enabled, port, bindaddr, webenabled, …

[ami-username]
secret = …
deny/permit = …
read = …
write = …
```

After connect, Asterisk may emit `FullyBooted` (`Privilege: system,all`).

### CLI helpers

```text
manager show settings
manager show users
manager show user <user>
manager show connected
manager show events
manager show event <event>
manager show commands
manager show command <action>
```

## Transports

1. **AMI over TCP** (primary for libraries) — Telnet/socket; Login first; `Events: on/off`.
2. **AMI over HTTP (AJAM)** — enable `http.conf` mini web server + `webenabled=yes` in manager.
   - Demos: `/manager`, `/rawman`, `/mxml`, `/static/ajamdemo.html`, `/httpstatus`
   - HTTP event fetch requires **WaitEvent** action.
   - **Important:** PHP/web apps using **sockets** are still TCP AMI — not AJAM. WaitEvent rule applies only to HTTP mode.

## This repository

- Use `Ntk.AsterNet.AMI` → `ManagerConnection`, Action/Event/Response types under `Manager/`.
- Samples: `Asterisk.Console.AMI`, `Asterisk.WinForm.AMI`.
- Tutorial cites AsterNET NuGet/GitHub — same design lineage.

## Auto-dialer pattern (tutorial)

Split responsibilities:

| Process | Role |
|---------|------|
| Cron + Action script | Read DB batch → `Originate` (Async=yes) + ActionID |
| Long-running Event listener | Handle `OriginateResponse` → write success/fail to DB |

Example Originate fields: `Channel`, `Context`/`Exten`/`Priority` **or** `Application`/`Data`, `Timeout`, `CallerID`, `Variable`, `Async`, `ActionID`, `Codecs`, `EarlyMedia`, …

```text
Action: Originate
Channel: PJSIP/trunk/09…
Context: ivr
Exten: s
Priority: 1
Async: yes
ActionID: campaign_<id>
CallerID: …
```

Why Cron + separate event worker: productizable auto-dialer should not depend on one forever-running PHP file for scheduling; event consumer can stay resident.

## Decision: AMI vs Call File vs AGI

| Goal | Tool |
|------|------|
| App-driven originate + live result events | **AMI** |
| Spool/file-based campaign, simple Playback/Context | **Call File** |
| Logic while caller already in IVR | **AGI/FastAGI** |

## Security checklist

- [ ] Least-privilege `read`/`write` classes per user
- [ ] Bindaddr not `0.0.0.0` unless firewalled
- [ ] Secrets only in env overlays / local config — never in git
- [ ] ActionID correlation for async Originate
- [ ] Reconnect + FullyBooted handling in clients

## Additional resources

- AJAM URLs, OriginateResponse fields: [reference.md](reference.md)
