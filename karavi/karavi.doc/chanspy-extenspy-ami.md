# ChanSpy / ExtenSpy from Admin Monitor (AMI)

**Related:** Voip.AsteriskChanSpyPro dialplan (`*30`–`*35`) · Ntk plan `Karavi.003`

## Product path (Ntk.Asterisk)

Supervisors use Admin **Monitor**:

1. Enter **supervisor extension** (phone that will ring).
2. On an in-call extension or live channel, choose mode:
   - Listen · Agent only · Whisper · Private whisper · Barge · DTMF
3. WebApi `POST /api/v1/Asterisk/Channels/ActionChanSpy` originates:
   - `Channel` = `{ChannelTech}/{supervisor}`
   - `Application` = `ExtenSpy`
   - `Data` = `{target},{flags}`

## Flag matrix (same as ChanSpyPro install.sh)

| Mode | Flags | Feature code |
|------|-------|--------------|
| listen | Eq | *30 |
| quiet | Eqo | *31 |
| whisper | Eqw | *32 |
| privateWhisper | EqW | *33 |
| barge | EqB | *34 |
| dtmf | Eqd | *35 |

## PBX prerequisite

Target extension must have an active channel ExtenSpy can attach to. For handset feature codes, install Voip.AsteriskChanSpyPro on the PBX; panel users do not need `*30` dialing.
