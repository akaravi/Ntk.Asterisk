# Asterisk dialplan contract (Wave 1)

Sample FreePBX-safe notes for AMI-driven Originate from `Ntk.Asterisk.WebApi`.
This repo does **not** own production dialplan on customer PBX — only documents the expected interface.

## Skills / interface

- Control plane: **AMI only** (`Ntk.AsterNet.AMI` / `ManagerConnection`)
- Pattern: Async `Originate` + `Application=Dial` (see `asterisk-ami` skill)
- Not used in Wave 1: FastAGI outbound, Call File spool writers, ARI Stasis

Step-by-step AMI enablement on Asterisk / FreePBX and Admin Settings mapping:  
[`ami-connection-setup-guide.md`](ami-connection-setup-guide.md)

## Manager user (least privilege)

Suggested `manager.conf` write classes for Wave 1:

| Class | Why |
|-------|-----|
| `originate` | Async Originate for CallJobs |
| `call` | Hangup / Bridge / channel status |
| `system` | Ping / FullyBooted awareness (optional but useful) |

Read classes: `call`, `system` (peer/channel events). Never commit real secrets — use `appsettings.Development.json` / Production overlay / vault.

## Channel technology

| Tech | Originate channel examples |
|------|----------------------------|
| `PJSIP` (default) | Extension: `PJSIP/100` · Outbound: `PJSIP/0912…@trunk-out` |
| `SIP` (legacy chan_sip) | Extension: `SIP/100` · Outbound: `SIP/trunk/0912…` |

Configured via `Asterisk:ChannelTech` in environment overlays (not base `appsettings.json`).

**Important:** PJSIP does **not** use `PJSIP/trunk/number` (that is chan_sip style). Wrong format → AMI `OriginateResponse Failure reason=0` (no such endpoint / bad tech).

## Trunk naming

- Outbound trunk peer/endpoint name: `Asterisk:DefaultTrunk` (e.g. `trunk-out`) — must match `pjsip show endpoints`
- Trunk inventory filter: `Asterisk:TrunkPeerFilter` regex (default `^(trunk|Trunk|TRUNK)`)
- Mobile legs (PJSIP): `{tech}/{mobileNumber}@{trunk}`
- Mobile legs (SIP): `{tech}/{trunk}/{mobileNumber}`

## CallJob Originate shapes

| Type | Channel (leg1) | Application Data (Dial) |
|------|----------------|-------------------------|
| ExtToExt | `{tech}/{fromExt}` | `{tech}/{toExt},{timeoutSec}` |
| MobileToExt (PJSIP) | `{tech}/{mobile}@{trunk}` | `{tech}/{ext},{timeoutSec}` |
| MobileToMobile (PJSIP) | `{tech}/{mobile1}@{trunk}` | `{tech}/{mobile2}@{trunk},{timeoutSec}` |
| MobileTo* (SIP) | `{tech}/{trunk}/{mobile}` | same pattern for Dial data |

Cancel: `Hangup` on tracked channel name when known.

### OriginateResponse reason codes (common)

| Reason | Meaning |
|--------|---------|
| 0 | No such endpoint/number or invalid channel tech/trunk name |
| 3 | Ringing / often answer timeout |
| 5 | Busy |
| 8 | Congestion / unavailable |

## FreePBX `_custom` notes

- Prefer edits under `extensions_custom.conf` / FreePBX Custom Destination — avoid overwriting `_additional` generated files.
- Peer `context` (e.g. `from-internal`) must allow the dialed extension; AMI `Application=Dial` bypasses context for the **second** leg but the **first** channel still obeys endpoint allow/deny and codecs.
- If single-Originate Application=Dial fails on a given PBX build, fallback is two-leg Originate + Bridge (Wave 1 engine can be extended; document PBX quirk in ticket).

## Sample custom context (illustrative only)

```ini
; extensions_custom.conf — illustrative; do not deploy blindly
[ntk-from-ami]
exten => _X.,1,NoOp(Ntk AMI dial ${EXTEN})
 same => n,Dial(PJSIP/${EXTEN},30)
 same => n,Hangup()
```

Wave 1 WebApi uses Application=Dial directly and does not require this context unless you switch to Context/Exten/Priority Originate mode later.

## Risks

- Async Originate correlation depends on `ActionID` + `OriginateResponse` / Hangup events
- PJSIP vs SIP mismatch if overlay `ChannelTech` does not match PBX
- External Asterisk AMI must be reachable (default TCP **5038**) — build verify does not require a live PBX
