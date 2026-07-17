---
name: asterisk-agi
description: >-
  Asterisk Gateway Interface (AGI/FastAGI/EAGI) patterns for sync channel scripting,
  DB/CRM IVR logic, and Ntk.AsterNet.AMI FastAGI. Use when writing AGI scripts,
  FastAGI servers, agi-bin, survey/IVR data, phpagi, DeadAGI/EAGI decisions, or
  AsteriskFastAGI in this repo.
---

# Asterisk AGI / FastAGI

Canonical source: `karavi/karavi.doc/Learn/voiping_agi_programing_tutorial.pdf` (VoiPing.ir).

**Prerequisite:** [asterisk-voip-stack](../asterisk-voip-stack/SKILL.md). Dialplan glue: [asterisk-dialplan](../asterisk-dialplan/SKILL.md).

## What AGI is

- **AGI** = Asterisk Gateway Interface — sync bridge between a live channel and an external program.
- Unlike AMI, AGI is typically **synchronous**: channel stays in AGI until the script finishes, then returns to dialplan.
- Transport:
  - **Standard AGI:** stdin/stdout with Asterisk
  - **FastAGI:** TCP/IP (default port **4573/4574** per install; tutorial cites **4574**)
  - **EAGI:** Enhanced AGI with live audio FD access (harder)
- Script path (Standard): `/var/lib/asterisk/agi-bin/` — must be **executable**.
- On invoke, most **channel** variables (not globals) are passed into the AGI environment (`request` / AGI env).

Dialplan:

```ini
exten => 110,1,NoOp(start agi)
 same => n,AGI(voiping-first-agi.php,arg1)
 ; FastAGI:
 same => n,AGI(agi://host:4573/script)
```

## This repository’s canonical path

Prefer **FastAGI via .NET**:

- Library: `src/DotNet/Ntk.AsterNet.AMI/FastAGI/` (`AsteriskFastAGI`, commands under `FastAGI/Command/`)
- Tutorial explicitly lists **AsterNET** for AMI/FastAGI — this project continues that lineage as `Ntk.AsterNet.AMI`.

Standard AGI in PHP remains valid for FreePBX-side scripts (`phpagi.php`), but **new product code in this repo should use FastAGI/.NET** unless integrating an existing PHP agi-bin.

## Common AGI operations (conceptual)

| Op | Purpose |
|----|---------|
| `answer` / `hangup` | Channel state |
| `verbose` | CLI log |
| `stream_file` | Play sound (no extension; escape digits) |
| `record_file` | Record to sounds path |
| `get_data` | Play + collect DTMF |
| `say_number` / `say_digits` | Speak values |
| `get_variable` / `set_variable` | Channel vars (get does **not** return globals) |
| `exec` | Run dialplan application from AGI |

Result arrays typically use `result` / `data` (library-specific). Treat hangup as `-1` class failures.

AGI env highlights: `agi_channel`, `agi_language`, `agi_type`, `agi_uniqueid`, `agi_callerid`, `agi_calleridname`, `agi_dnid`, `agi_context`, `agi_extension`, `agi_priority`, `agi_arg_N`.

## Debug

1. Syntax-run the script outside Asterisk (`php script.php` / unit-test FastAGI handlers).
2. CLI: `agi set debug on` — watch AGI Tx/Rx.
3. Keep application logs (correlation with `agi_uniqueid`).

## Dead AGI / hangup

- **DeadAGI deprecated** since Asterisk 1.6 era — do not recommend as primary.
- Prefer: extension `h`, hangup handlers, or FastAGI cleanup after hangup detection.

## Do / Don’t (from tutorial — binding)

**Do**

1. Finish AGI scripts as fast as possible.
2. Use AGI primarily for **data** work; keep main call flow in Dialplan.
3. Migrate to **FastAGI** under high volume / many scripts.
4. Prefer local DB for Standard AGI on same host.
5. Learn Asterisk/Linux log reading.

**Don’t**

1. Run unbounded apps inside AGI: `Dial`, `MeetMe`, `MusicOnHold`, `Monitor`, `ChanSpy`, …
2. Use AGI to create outbound campaign calls → use AMI Originate or Call File.
3. Use DeadAGI on active channels.
4. Treat VM-heavy languages as best for **Standard** AGI on-box (tutorial prefers php/python/perl for Standard AGI). **Exception for this repo:** FastAGI .NET is the supported product path (network-isolated worker).

## Scenarios (tutorial)

- Post-queue **survey** (Queue option `c`, store score, record complaint if <3, email).
- **Phone-bank** IVR (card + PIN → balance SayNumber; email last transactions).

## Agent checklist

- [ ] Dialplan entry + return path defined
- [ ] FastAGI host/port documented in config (not hardcoded production URL in library defaults)
- [ ] No Dial/Originate campaign logic inside AGI
- [ ] Hangup / channel-down handled
- [ ] Maps to `Ntk.AsterNet.AMI` FastAGI types when implementing in this repo

## Additional resources

- Commands, survey/queue notes: [reference.md](reference.md)
