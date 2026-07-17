# Asterisk dialplan contract (Wave 1)

Sample FreePBX-safe notes for AMI-driven Originate from `Ntk.Asterisk.WebApi`.
This repo does **not** own production dialplan on customer PBX — only documents the expected interface.

## Skills / interface

- Control plane: **AMI only** (`Ntk.AsterNet.AMI` / `ManagerConnection`)
- Pattern: Async `Originate` + `Application=Dial` (see `asterisk-ami` skill)
- Not used in Wave 1: FastAGI outbound, Call File spool writers, ARI Stasis

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
| `PJSIP` (default) | `PJSIP/100`, `PJSIP/trunk-out/0912…` |
| `SIP` (legacy chan_sip) | `SIP/100`, `SIP/trunk/09…` |

Configured via `Asterisk:ChannelTech` in environment overlays (not base `appsettings.json`).

## Trunk naming

- Outbound trunk peer/endpoint name: `Asterisk:DefaultTrunk` (e.g. `trunk-out`)
- Trunk inventory filter: `Asterisk:TrunkPeerFilter` regex (default `^(trunk|Trunk|TRUNK)`)
- Mobile legs: `{tech}/{trunk}/{mobileNumber}`

## CallJob Originate shapes

| Type | Channel (leg1) | Application Data (Dial) |
|------|----------------|-------------------------|
| ExtToExt | `{tech}/{fromExt}` | `{tech}/{toExt},{timeoutSec}` |
| MobileToExt | `{tech}/{trunk}/{mobile}` | `{tech}/{ext},{timeoutSec}` |
| MobileToMobile | `{tech}/{trunk}/{mobile1}` | `{tech}/{trunk}/{mobile2},{timeoutSec}` |

Cancel: `Hangup` on tracked channel name when known.

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
