---
name: asterisk-callfile
description: >-
  Asterisk Call File spool auto-dialer patterns (outgoing spool, Channel,
  Application/Context, archive, failed extension). Use when building campaigns,
  scheduled calls, reminders, adhan/broadcast, or any .call file / pbx_spool work.
---

# Asterisk Call File

Canonical source: `karavi/karavi.doc/Learn/voiping_callfile_tutorial.pdf` (VoiPing.ir).

**Prerequisite:** [asterisk-voip-stack](../asterisk-voip-stack/SKILL.md). Compare with AMI Originate: [asterisk-ami](../asterisk-ami/SKILL.md).

## What a Call File is

- Extension: `.call`
- Watcher module: `pbx_spool.so`
- Drop directory: `/var/spool/asterisk/outgoing/`
- Asterisk **executes then deletes** the file; optional archive under `/var/spool/asterisk/outgoing_done/`
- Purpose: create a channel automatically; after answer, connect to an **Application** **or** a **Dialplan Context/Extension/Priority**.

## Minimal file

```text
Channel: PJSIP/401
Application: Playback
Data: hello-world
```

## Field reference

| Key | Meaning |
|-----|---------|
| `Channel` | Destination channel tech/endpoint/number |
| `Callerid` | Caller ID presented |
| `WaitTime` | Seconds to wait for answer (default ~45) |
| `MaxRetries` | Retries if no answer (default 0) |
| `RetryTime` | Seconds between retries (default ~300) |
| `Archive` | `yes` / `no` |
| `Application` + `Data` | App flow after answer |
| `Context` + `Extension` + `Priority` | Dialplan flow after answer (Priority may be label) |
| `Setvar` | `var=value` injected into dialplan |

Comments: lines starting with `#` or `;`.

Archive annotations Asterisk may append: `Status` (`Expired`/`Completed`/`Failed`), `StartRetry` / `EndRetry`.

## Failed dialplan hook

If using Context flow and the call fails, Asterisk jumps to extension **`failed`** in that context and sets `${REASON}`:

```ini
[ads]
exten => 110,1,NoOp(start the ads)
 same => n,Wait(3)
 same => n,Playback(hello-world)
 same => n,Hangup()

exten => failed,1,NoOp(call file failed)
 same => n,NoOp(${REASON})
```

Use for logging / CDR / DB.

## CRITICAL operational rules (binding)

1. **Never create** call files directly inside `outgoing/`.
2. **Never copy** call files into `outgoing/` (copy can race partial reads).
3. Always create elsewhere on the **same filesystem**, then **`mv`** into `outgoing/`.
4. Before move: file must be writable; **owner `asterisk:asterisk`** (utime for schedule).
5. **Never save as UTF-8/Unicode** — use plain ASCII/legacy single-byte encoding for `.call` content.
6. Analog FXO caveat: Asterisk may treat dial-tone as answer early — prefer dialplan flow + `Wait` for better behavior.
7. Schedule future attempts with `touch -t YYYYMMDDhhmm.ss` on the file before move.

Linux helpers:

```bash
chmod 644 /path/of/file.call
chown asterisk:asterisk /path/of/file.call
touch -t 201701051010.00 /path/of/file.call
mv /tmp/file.call /var/spool/asterisk/outgoing/
```

## Use cases

- Ad / campaign dialers
- Scheduled announcements (e.g. adhan to phones/speakers)
- Meeting reminders
- Controlled concurrency bursts of outbound calls

## Tutorial scenario shape

1. Read numbers from `/tmp/numbers.txt`
2. WaitTime 10s per callee
3. On answer → success log file; on fail → failed log file
4. Archive enabled
5. Generator often in PHP (or any language) + dialplan context for media

## Ntk.Asterisk mapping

This repo does not ship a Call File writer by default. When adding one:

- Prefer a small library helper that writes temp `.call` → sets owner → atomic move (document Linux deployment; Windows dev may only generate file content).
- For app-integrated dialers with live events, prefer **AMI Originate** ([asterisk-ami](../asterisk-ami/SKILL.md)) via `Ntk.AsterNet.AMI`.
- Call File remains the right tool for **spool/cron/filesystem** campaigns.

## Agent checklist

- [ ] Application **xor** Context triad chosen deliberately
- [ ] `failed` + `${REASON}` when using dialplan flow
- [ ] Atomic `mv` + `asterisk` ownership documented
- [ ] Encoding is not UTF-8
- [ ] Compared to AMI Originate for product requirements

## Additional resources

- Example campaign skeleton: [reference.md](reference.md)
