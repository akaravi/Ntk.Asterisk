# WebPhone — SIP MESSAGE dialplan (Asterisk)

**Plan:** Karavi.005 · **id:** D-DIALPLAN-MSG · **host:** PBX (not WebApi runtime)

The hosted softphone sends/receives SIP MESSAGE (SIMPLE) via SipJS. Asterisk must accept out-of-dialog MESSAGE and optionally relay.

## Minimal accept + echo (test)

```ini
; /etc/asterisk/extensions_custom.conf  (FreePBX/Issabel) or extensions.conf
[messages]
exten => _X.,1,NoOp(SIP MESSAGE from ${MESSAGE(from)} to ${EXTEN})
 same => n,Set(CONTENT=${MESSAGE(body)})
 same => n,MessageSend(${MESSAGE(from)},${MESSAGE(to)})
 same => n,Hangup()
```

Ensure `pjsip.conf` / transport allows MESSAGE, and the endpoint `context=` can reach a context that includes `messages` **or** set:

```ini
[global]
; chan_pjsip / older sip.conf patterns vary by version
; For PJSIP, use endpoint option:
; message_context=messages
```

Example PJSIP endpoint snippet:

```ini
[1001]
type=endpoint
context=from-internal
message_context=messages
...
```

## Relay to another extension

```ini
[messages]
exten => _X.,1,NoOp(Relay MESSAGE to ${EXTEN})
 same => n,MessageSend(pjsip:${EXTEN},${MESSAGE(from)})
 same => n,Hangup()
```

## NTK WebApi note

Wave 5 does **not** terminate SIP MESSAGE on the .NET host. Persistence of chat remains client-side (local stream) unless you add a future ingest API. Server-side CDR/QoS/recording sync is handled by `ntk-webphone-bridge.js`.

## Verify

1. Two softphones registered (e.g. 1001 / 1002).
2. Send text from buddy stream.
3. Asterisk CLI: `pjsip set logger on` — expect MESSAGE REQUEST/RESPONSE.
4. If 404/503: check `message_context` and dialplan match.

## Residual

Full SMS/email gateways and XMPP bridge remain deferred (D-XMPP).
