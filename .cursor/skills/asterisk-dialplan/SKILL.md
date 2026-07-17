---
name: asterisk-dialplan
description: >-
  Asterisk Dialplan authoring and review (extensions.conf, Context, Extension,
  Priority, include, dial patterns, IVR apps). Use when writing or debugging
  dialplan, extensions.conf, FreePBX custom/override, Answer/Playback/Dial/GotoIF,
  AA/IVR, ChanSpy, Macro/Gosub, or routing channels to contexts.
---

# Asterisk Dialplan

Canonical source: `karavi/karavi.doc/Learn/voiping_dialplan_tutorial.pdf` (VoiPing.ir).

**Prerequisite:** read [asterisk-voip-stack](../asterisk-voip-stack/SKILL.md) first.

## Mental model

- Dialplan lives in `/etc/asterisk/extensions.conf` (module `pbx_config.so`). Also AEL / Lua — this skill targets classic dialplan.
- Dialplan is the **heart of routing** — not where SIP peers are defined.
- Peers are defined in `sip.conf` / `pjsip.conf` / `iax.conf` / `chan_dahdi.conf`; their `context=` selects which dialplan Context they enter.

### Context

```ini
[voiping-context]
exten => 401,1,Wait(5)
same => n,Dial(PJSIP/401)
```

- Context = independent access/behavior unit for channels.
- Name: letters/digits, max ~79 chars.
- Reserved / avoid as context names: `general`, `globals`, `default`.
- File segments `[general]` and `[globals]` are **not** contexts (`general` = dialplan settings, `globals` = global vars).

### Extension / Priority

- **Extension** ≠ “internal phone number”. It is a named priority chain of applications.
- Syntax: `exten => name,priority,Application(args)`
- `n` = next priority; `same => n,...` = same extension shorthand.
- Labels: `exten => 401,n(my-label),NoOp()` for Goto targets.
- Comments: `;` line · `;-- ... --;` block.

### Includes

```ini
[context1]
include => context2
include => context2,08:00-17:00,*,*,*
#include voiping.conf
#tryinclude optional.conf
```

- Parent context searched first; then included children in include order.
- Timed include: `include => context,times,weekdays,mdays,months`
- File includes: `#include`, `#include /path/*.conf`, `#tryinclude`, `#exec` (needs `execincludes=yes` in `asterisk.conf`).

## FreePBX / Issabel / Elastix overlay rules

| File pattern | Owner | Rule |
|--------------|-------|------|
| `*.conf` (no suffix) | Asterisk core | Asterisk loads these |
| `*_additional.conf` | FreePBX | Regenerated on Apply — **do not hand-edit permanently** |
| `*_custom.conf` | Developer | New contexts/extensions or override one extension |
| `*_override.conf` | Developer | Override whole FreePBX contexts |

Always respect include order. Prefer `_custom` / `_override` for this project’s deployment guidance.

## Core applications (minimum set)

| App | Role |
|-----|------|
| `Answer([delay])` | Answer ringing channel |
| `Playback(file[&file2],opts)` | Play sound; default `/var/lib/asterisk/sounds`; **no file extension**; 8kHz 16-bit mono; opts `skip` / `noanswer` |
| `Background` + `WaitExten` | IVR digit collection while playing |
| `Hangup()` | End channel |
| `Goto` / `GotoIf` / `GotoIfTime` / `ExecIf` | Flow control |
| `Dial` | Create outbound leg / bridge |
| `Verbose` / `NoOp` | Debug |
| `Read` / `Authenticate` / `DISA` | Input / auth |
| `SayNumber` / `SayDigits` / `SayAlpha` / `SayPhonetic` | TTS-like playback of values |
| `System` | Shell (use sparingly; prefer AGI for data) |
| `ChanSpy` | Listen/whisper |
| `Queue` | ACD; option `c` continues dialplan after agent hangup |

## Variables & patterns

- Channel vars vs globals; functions in dialplan `${FUNC(args)}`.
- Dial patterns (`_NXXXXXX`, etc.) for outbound restrictions (e.g. block `00` / mobile for some extensions).
- Local channels for internal routing (`Local/ext@context`).

## Macros & subroutines

Prefer **Gosub** / modern subroutines over legacy Macro when targeting current Asterisk; Macro still appears in older FreePBX trees — match the PBX version.

## Debug checklist

```text
asterisk -rvvv
core show channels
dialplan show <context>
dialplan reload
pjsip show endpoints   ; or sip show peers
```

Verbose levels matter for `Verbose()` output.

## Decision: Dialplan vs AGI vs AMI vs Call File

- Pure menu / route / time / pattern → Dialplan.
- DB / CRM / complex logic → AGI/FastAGI ([asterisk-agi](../asterisk-agi/SKILL.md)).
- Originate / monitor from external app → AMI ([asterisk-ami](../asterisk-ami/SKILL.md)).
- Batch campaign spool → Call File ([asterisk-callfile](../asterisk-callfile/SKILL.md)).

## Ntk.Asterisk note

This repo ships **.NET AMI/ARI clients**, not dialplan files. When samples need dialplan, document Context/Extension/Priority contracts the library expects (e.g. Originate target `context/exten/priority`).

## Additional resources

- Detailed apps, scenarios, patterns: [reference.md](reference.md)
