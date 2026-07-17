# AdminPanel (D11)

Angular 19 standalone admin UI for Ntk.Asterisk Wave 1.

| Item | Value |
|------|--------|
| Dev port | `5314` |
| API | `http://localhost:5310` (`src/environments/environment*.ts`) |
| Hub | `/hubs/asterisk` |
| i18n | fa-first (`public/assets/i18n/fa.json`) + en |
| Auth | none (Wave 1) |

```powershell
cd src/Angular/AdminPanel
npm start
# or
npm run build
```

Features: Connection status · Settings (non-secret flags) · Monitor (peers/trunks/channels + hangup) · Jobs (list/cancel) · SignalR live updates.
