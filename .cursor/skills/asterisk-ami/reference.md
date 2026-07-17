# AMI reference (VoiPing tutorial digest)

## Libraries cited

| Name | Notes |
|------|-------|
| phpagi-asmanager.php | FreePBX agi-bin; TCP sockets |
| AsterNET | .NET AMI + FastAGI — maps to `Ntk.AsterNet.AMI` |
| Asterisk-Java, Pyst2, Adhearsion, asterisk.io | Other ecosystems |

## HTTP demo endpoints (AJAM)

Assuming `bindport=8088`:

- `http://ip:8088/manager`
- `http://ip:8088/amanager`
- `http://ip:8088/rawman?action=...`
- `http://ip:8088/arawman?action=...`
- `http://ip:8088/mxml?action=...`
- `http://ip:8088/amxml?action=...`
- `http://ip:8088/static/ajamdemo.html`
- `http://ip:8088/static/mantest.html`
- `http://ip:8088/httpstatus`

`http.conf` sketch:

```ini
[general]
enabled=yes
bindaddr=0.0.0.0
bindport=8088
```

`manager.conf`: `webenabled = yes`

## OriginateResponse (event)

Fields commonly include: ActionID, Response, Channel, Context, Exten, Application, Data, Reason, Uniqueid, CallerIDNum, CallerIDName.

Register handler for `OriginateResponse`, then `wait_response` / event loop (library-specific).

## Product questions raised by tutorial (design bar)

- Is scheduling only Cron-based enough for market sale?
- Can Action + Event scripts be one process? (possible, but separation aids ops)
- Completeness: retries, CDR, concurrency limits, blacklists — required for national-scale dialers

## Mapping to Ntk.AsterNet.AMI

- Prefer typed `ManagerAction` / `ManagerEvent` classes already in tree.
- Match Console/WinForm login → subscribe → action patterns.
- Keep host/port/username in Development/Production overlays — not library hardcodes.
