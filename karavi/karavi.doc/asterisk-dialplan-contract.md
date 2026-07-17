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

| Tech | DirectTech channel examples |
|------|----------------------------|
| `PJSIP` (default) | Extension: `PJSIP/100` · Outbound: `PJSIP/0912…@real-endpoint` |
| `SIP` (legacy chan_sip) | Extension: `SIP/100` · Outbound: `SIP/trunk/0912…` |

Configured via `Asterisk:ChannelTech` in environment overlays (not base `appsettings.json`).

**Important:** PJSIP does **not** use `PJSIP/trunk/number` (that is chan_sip style). Wrong format → AMI `OriginateResponse Failure reason=0`.

## OriginateVia (preferred for FreePBX)

| Mode | Setting | Behavior |
|------|---------|----------|
| **LocalContext** (default) | `OriginateVia=LocalContext`, `OriginateContext=from-internal` | `Local/{number}@from-internal/n` — FreePBX outbound routes select the real trunk. **DefaultTrunk is not required.** |
| **DirectTech** | `OriginateVia=DirectTech` | Dial `PJSIP/{number}@{DefaultTrunk}` — `DefaultTrunk` must be a real PJSIP endpoint name from `pjsip show endpoints` (labels like `trunk-out` often fail with reason=0). |

## Trunk naming (DirectTech only)

- Outbound trunk peer/endpoint name: `Asterisk:DefaultTrunk` — must match `pjsip show endpoints`
- Trunk inventory filter: `Asterisk:TrunkPeerFilter` regex (default `^(trunk|Trunk|TRUNK)`)
- Mobile legs (PJSIP): `{tech}/{mobileNumber}@{trunk}`
- Mobile legs (SIP): `{tech}/{trunk}/{mobileNumber}`

## CallJob Originate shapes

### LocalContext (default — Application Dial / B2BUA)

Mobile↔mobile silence root cause: `Context/Exten` click-to-call reports success when **leg1** answers; FreePBX dialplan + trunk `directmedia` often leaves no usable RTP between two trunk channels. ConfBridge/Bridge on `Local/;1` never touches phone RTP (`SIP/fanava-…`).

**Wave-1 default (two-way audio):**

1. Originate `Local/{leg1}@from-internal/n`
2. `Application=Dial` · `Data=Local/{leg2}@from-internal/n,{timeout},m({MusicOnHoldClass})tT`
3. Leg1 hears MOH until leg2 answers; wait AMI `DialEnd` DialStatus=`ANSWER` on Application channel (`Local/…;1`)
4. **Promote:** resolve `BRIDGEPEER` → `SIP/…` / `PJSIP/…` for both legs · AMI `Bridge` those trunk channels (native two-way RTP). Do **not** hang Local stubs after promote.

**Fallback / experimental:** `ExecuteLocalClickToCallAsync` · `ExecuteLocalTwoLegSipBridgeAsync` (MOH + AMI Bridge SIP peers — do not hang Local after Bridge).

| Type | Leg1 number | Leg2 number |
|------|-------------|-------------|
| ExtToExt | From | To |
| MobileToExt | Mobile1 | To ext |
| MobileToMobile | Mobile1 | Mobile2 |

**MOH:** FreePBX Music on Hold class (`MusicOnHoldClass`, default `default`).
### DirectTech

| Type | Channel (leg1) | Application Data (Dial) |
|------|----------------|-------------------------|
| ExtToExt | `{tech}/{fromExt}` | `{tech}/{toExt},{timeoutSec},m(default)` |
| MobileToExt (PJSIP) | `{tech}/{mobile}@{trunk}` | `{tech}/{ext},{timeoutSec},m(default)` |
| MobileToMobile (PJSIP) | `{tech}/{mobile1}@{trunk}` | `{tech}/{mobile2}@{trunk},{timeoutSec},m(default)` |
| MobileTo* (SIP) | `{tech}/{trunk}/{mobile}` | same pattern + `,m(class)` |

Cancel: `Hangup` on tracked channel name when known.

### OriginateResponse reason codes (common)

| Reason | Meaning |
|--------|---------|
| 0 | No such endpoint/number or invalid channel / context / trunk name |
| 3 | Ringing / often answer timeout |
| 5 | Busy |
| 8 | Congestion / unavailable |

If reason=0 on FreePBX: switch to **LocalContext** + `from-internal`, or fix DirectTech trunk to a real endpoint name.

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

Wave 1 WebApi **LocalContext** originates two Local legs (MOH + Wait), resolves trunk `SIP`/`PJSIP` via `BRIDGEPEER`, then AMI-bridges those media channels. Fallback: click-to-call `Context/Exten`. DirectTech uses `Application=Dial` with `,m(class)`.

## Risks

- Async Originate correlation depends on `ActionID` + `OriginateResponse` / Hangup events
- PJSIP vs SIP mismatch if overlay `ChannelTech` does not match PBX
- External Asterisk AMI must be reachable (default TCP **5038**) — build verify does not require a live PBX
