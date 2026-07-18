# Multi-AMI live sessions

WebApi can hold a persistent `ManagerConnection` per **enabled** Asterisk server.

## Behavior

| Concern | Behavior |
|---------|----------|
| Ops primary | Active default server — `FireAllEvents` + `SendAction` / Monitor / Jobs |
| Secondary | KeepAlive live socket, **no** event fan-out (avoids duplicate Monitor noise) |
| Startup | When `AutoConnectOnStartup`, connects **all** enabled configured servers |
| Status list | Reads live endpoint map — **no** Login/Logoff probe |

## API (additive)

| Route | Body | Notes |
|-------|------|--------|
| `POST .../Connection/ActionConnect` | `{ serverId?: string }` | null → default ops server |
| `POST .../Connection/ActionDisconnect` | `{ serverId?: string }` | null → default ops server |
| `POST .../Connection/ActionConnectAll` | `{}` | all enabled configured |
| `GET .../Connection/GetList` | — | one row per enabled server |

Legacy clients calling Connect/Disconnect with `{}` keep default-server behavior.

## Admin UI

Connection page: per-server Connect/Disconnect + **Connect all**.

## Skills

asterisk-voip-stack · asterisk-ami
