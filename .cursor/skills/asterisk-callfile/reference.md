# Call File reference (VoiPing tutorial digest)

## Two execution flows after answer

```text
Channel: PJSIP/trunk1/0912…
Callerid: 22902602
WaitTime: 5
MaxRetries: 2
RetryTime: 60
Archive: yes

; Flow A — Application
Application: Playback
Data: hello-world

; Flow B — Dialplan (do not mix with Application in same file)
; Context: ads
; Extension: 110
; Priority: 1
; Setvar: CAMPAIGN_ID=42
```

Use **either** Application/Data **or** Context/Extension/Priority.

## Generator sketch (language-agnostic)

1. Read target numbers from DB or file.
2. For each number, write `/tmp/out-XXXX.call` with ASCII content.
3. `chown asterisk:asterisk` + mode `644`.
4. Optional `touch -t` for deferred spool.
5. `mv` to `/var/spool/asterisk/outgoing/`.
6. Persist attempt id; reconcile via archive Status or dialplan `failed`/CDR.

## When to choose Call File over AMI

| Call File | AMI Originate |
|-----------|---------------|
| Cron + filesystem ops | Always-on .NET service |
| Simple media after answer | Need OriginateResponse events in-process |
| Minimal dependencies on Asterisk host | Rich Action/Event API via `Ntk.AsterNet.AMI` |

## Spool paths

| Path | Role |
|------|------|
| `/var/spool/asterisk/outgoing/` | Active queue |
| `/var/spool/asterisk/outgoing_done/` | Archive when enabled |
