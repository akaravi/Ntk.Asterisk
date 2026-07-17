# Dialplan reference (VoiPing tutorial digest)

## Channel creation sources

1. Device requests call → Dialplan
2. CLI Originate
3. Dialplan `Dial` creates another channel
4. APIs: AMI Originate, Call File, ARI

## Famous channel technologies

`PJSIP` / `SIP`, `IAX2`, `DAHDI`, `Local`

## Peer definition vs behavior

```ini
; sip.conf / pjsip endpoint
[401]
context=voiping
; behavior is NOT here — only entry context

; extensions.conf
[voiping]
exten => 901,1,Dial(PJSIP/ali)
exten => ali,1,Dial(PJSIP/ali)
```

Defining a peer alone does **not** allow calling; dialplan must authorize routes.

## IVR building blocks

1. `Answer`
2. `Background(menu)` + `WaitExten` **or** `Read`
3. `GotoIf` / pattern extensions for digits
4. `Playback` / `SayNumber` for responses
5. `Hangup`

## Queue + post-call survey pattern

```ini
exten => 110,1,NoOp(queue entrance)
 same => n,Queue(110,c${other_options})
 same => n,AGI(survey.php)
```

- Without queue option `c`, dialplan after `Queue` may never run when agent hangs up.
- `setinterfacevar=yes` in `queues.conf` exposes `MEMBERINTERFACE`, `MEMBERNAME`, …
- Extension `h` / hangup handlers for post-hangup DB/email (channel already down — limited ops).

## Auto-attendant / manager scenarios (tutorial)

Typical learning path in the PDF:

1. Bank balance / last transactions IVR (Dial + Say*)
2. Digital receptionist (AA)
3. Manager of AA
4. Dial patterns for outbound ACL
5. ChanSpy improvements on FreePBX ISOs
6. Predefined special extensions, Macro/Subroutine, FreePBX dial routes

## CLI essentials

```text
asterisk -r
core set verbose 5
dialplan show
module reload pbx_config.so
; or: dialplan reload
```

## Expression / string work

Use dialplan expressions and string functions carefully; for heavy string/DB logic prefer AGI.

## Localization

Tutorial covers Persian sound packs and default language change — keep sound files under Asterisk sounds path; omit extensions in Playback args.
