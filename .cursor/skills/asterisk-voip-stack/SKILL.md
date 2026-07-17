---
name: asterisk-voip-stack
description: >-
  Mandatory Asterisk/VoIP development stack for Ntk.Asterisk (Dialplan, AGI/FastAGI,
  AMI, Call File, ARI). Use on ANY Asterisk, VoIP, PBX, IVR, dialplan, extensions.conf,
  AGI, FastAGI, AMI, Originate, Call File, spool/outgoing, channel, bridge, queue,
  FreePBX/Issabel/Elastix, or Ntk.AsterNet.AMI / Ntk.AsterNet.ARI work. Always read
  this skill first, then the matching domain skill before coding.
---

# Asterisk VoIP Stack (Ntk.Asterisk) — Mandatory

## When this applies

**Always** before implementing or reviewing anything related to:

- Dialplan / `extensions.conf` / Context / Extension / Priority
- AGI / DeadAGI / EAGI / FastAGI / `agi-bin`
- AMI / Manager / Originate / Events / `manager.conf` / port 5038
- Call File / `pbx_spool` / `/var/spool/asterisk/outgoing`
- ARI / Stasis / REST / WebSocket (this repo’s `Ntk.AsterNet.ARI`)
- Channels, Bridge, Queue, IVR, auto-dialer, CRM telephony hooks

Source tutorials (canonical knowledge base — VoiPing.ir):

- `karavi/karavi.doc/Learn/voiping_dialplan_tutorial.pdf`
- `karavi/karavi.doc/Learn/voiping_agi_programing_tutorial.pdf`
- `karavi/karavi.doc/Learn/voiping_ami_programing_tutorial.pdf`
- `karavi/karavi.doc/Learn/voiping_callfile_tutorial.pdf`

## Mandatory workflow (do not skip)

1. Read **this** skill.
2. Choose the interface with the decision matrix below.
3. Read the matching domain skill **before** writing code:
   - [asterisk-dialplan](../asterisk-dialplan/SKILL.md)
   - [asterisk-agi](../asterisk-agi/SKILL.md)
   - [asterisk-ami](../asterisk-ami/SKILL.md)
   - [asterisk-callfile](../asterisk-callfile/SKILL.md)
4. Align with this repo’s libraries:
   - AMI + FastAGI → `src/DotNet/Ntk.AsterNet.AMI/` (`ManagerConnection`, `AsteriskFastAGI`)
   - ARI → `src/DotNet/Ntk.AsterNet.ARI/`
5. Prefer existing samples under `src/DotNet/Asterisk.Console.*` and `Asterisk.WinForm.*`.

## Decision matrix — which tool?

| Need | Prefer | Avoid |
|------|--------|--------|
| Routing, IVR menus, dial patterns, includes, macros/Gosub | **Dialplan** | Putting heavy DB logic in dialplan |
| Sync work on live channel: DB read/write, CRM, survey score, bank balance | **AGI** (short) or **FastAGI** (.NET here) | Long-running Dial/MeetMe/MOH inside AGI |
| Live monitoring, Originate, queue/agent ops, async events | **AMI** | Using AGI to invent outbound campaigns |
| Batch / scheduled / campaign outbound from spool | **Call File** | Creating `.call` directly in `outgoing/` |
| Full media/app control from external app (Stasis) | **ARI** | Replacing AMI monitoring with ARI unless needed |

Development layers (from Dialplan tutorial):

```
Dialplan → AGI → AMI → Call File → ARI → CLI
```

99.99% of scenarios = Dialplan + APIs — not C-core module development.

## Channel vs Call (core model)

- **Channel** = bridge between Asterisk and a device (PJSIP/SIP, IAX2, DAHDI, Local, …).
- **Call** = usually two+ channels bridged.
- Channels are created by: inbound request (Dialplan), CLI Originate, Dial app, or APIs (AMI Originate / Call File / ARI).

## Repo mapping

| Tutorial concept | This repository |
|------------------|-----------------|
| AsterNET (AMI + FastAGI) | `Ntk.AsterNet.AMI` |
| ARI REST client | `Ntk.AsterNet.ARI` |
| Samples | Console/WinForm AMI & ARI projects |

## Security (always)

- Never commit AMI secrets, ARI passwords, `manager.conf` credentials, or softphone secrets.
- AMI is network-exposed (default TCP **5038**) — least privilege `read`/`write` classes; bind carefully.
- Call files and dialplan must not embed production credentials in git.

## Agent output contract

When delivering Asterisk-related work, state briefly:

1. Which interface(s) chosen and why
2. Which domain skill(s) applied
3. How it maps to `Ntk.AsterNet.AMI` / `ARI` (if code)
4. Risks: hangup timing, spool races, async Originate, FreePBX `_custom` vs `_additional`

## Forbidden

- Implementing VoIP features without reading the matching domain skill
- Using Standard AGI for outbound dialing campaigns
- Writing Call Files with UTF-8/Unicode encoding or creating them in-place under `outgoing/`
- Putting business DB loops in dialplan when AGI/FastAGI is the right layer
- Suggesting DeadAGI on modern Asterisk as primary pattern
