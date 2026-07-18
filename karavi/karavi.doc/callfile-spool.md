# Call File spool (pbx_spool)

WebApi creates Asterisk `.call` files for auto-dial (campaigns, reminders, playback).

## Rules (binding)

1. Never write or `File.Copy` directly into Asterisk `outgoing/`.
2. Write ASCII bytes to a **staging** directory on the **same volume** as outgoing, then `File.Move` into outgoing.
3. Content must be ASCII (not UTF-8) — Asterisk Call File encoding constraint.
4. After answer: either `Application` + optional `Data`, **or** `Context` + `Extension` + `Priority` — never both.

## Config (Admin Settings → Call File spool)

| Setting | Purpose |
|---------|---------|
| `CallFileStagingDirectory` | Staging path on WebApi host. Empty → `{ContentRoot}/App_Data/callfile-staging` |
| `CallFileOutgoingDirectory` | Path WebApi can see that maps to `/var/spool/asterisk/outgoing` (UNC/mount). **Required** to submit. |

## API

`POST /api/v1/Asterisk/CallFiles/Add`

Body (`CallFileAddRequest`): `channel` (required), optional `callerId`, `waitTimeSec`, `maxRetries`, `retryTimeSec`, `archive` (`yes`/`no`), then either Application triad or Context triad.

Envelope: `{ isSuccess, data: [CallFileDto], errorMessage }`.

## Admin UI

- **Settings:** staging + outgoing paths (LTR).
- **Jobs:** Create Call File form (Application or Context mode).

## Ops note (Windows)

Mount the PBX spool as UNC (e.g. `\\pbx\outgoing`) and set staging on the same share (e.g. `\\pbx\callfile-staging`) so `File.Move` stays atomic and does not cross volumes.
