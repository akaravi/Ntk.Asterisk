# Queue Panel via AMI (Karavi.004)

Live Asterisk **queues / members / waiting callers** in `Ntk.Asterisk.WebApi`, ported from Voip.AsteriskQueuePanel capabilities (not a Python rewrite).

## API

| Method | Route |
|--------|--------|
| GET | `/api/v1/Asterisk/Queues/GetList` |
| GET | `/api/v1/Asterisk/Queues/GetOne/{name}` |
| POST | `/api/v1/Asterisk/Queues/ActionPauseMember` |
| POST | `/api/v1/Asterisk/Queues/ActionUnpauseMember` |
| POST | `/api/v1/Asterisk/Queues/ActionHangupEntry` |

Spy/Whisper/Barge: reuse `POST /api/v1/Asterisk/Channels/ActionChanSpy`.

## Real-time

SignalR method `queuesUpdated` → groups `monitor` and `queues` (`SubscribeQueues`).

## Admin

Route `/queues` — expand queue · pause/unpause · hangup waiting · spy modes.

Settings (per server): `queueHideList` (comma), `queueShowList` (whitelist; empty = all except hidden), `queueRenameMap` (`real=display` per line). List returns `name` (display) + `realName` (AMI). Pause/unpause use AMI name.

### Queue ACL (Q13)

- Store: `App_Data/queue-acl-users.json` (PBKDF2 passwords)
- `POST /api/v1/Auth/login` · `GET /api/v1/Auth/status` · header `X-Queue-Acl-Token`
- Users CRUD: `/api/v1/Asterisk/QueueAclUsers/*`
- Gate: `RequireLogin` + enabled users → filter Queues REST + SignalR by `allowedQueues` (display or AMI name). Role `admin` sees all.
- Admin: `/login` · `/queue-acl`

### Historical stats (Q14)

- Periodic samples from live `QueueStatus` → `App_Data/queue-stats-snapshots.json`
- `GET /api/v1/Asterisk/QueueStats/GetList?from=&to=&queue=`
- Admin: `/queue-stats` (not Asterisk `queue_log` SQL)

## AMI

- `QueueStatus` (params + members + entries)
- `QueuePause`
- `Hangup` for waiting channels

Requires manager.conf `read`/`write` including `queue` / call classes as appropriate.
