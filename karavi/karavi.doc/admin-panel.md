# AdminPanel (Angular)

Host path: `src/Angular/AdminPanel/`  
Dev port: **5314** · API base: `http://localhost:5310` (environment files only).

Wave 1 surfaces:

- Connection — AMI + SignalR status via `GET /api/v1/Asterisk/Connection/GetStatus`
- Settings — load/save AMI connection via `GET/POST /api/v1/Config/GetSiteSettings` · `UpdateSiteSettings` (persisted `App_Data/asterisk-settings.json`; secret never returned)
- Settings — **Test connection** via `POST /api/v1/Config/ActionTestConnection` (force reconnect with saved settings)
- Settings UI — sectioned operator form (credentials · originate · trunk · recording · session) + status rail + single action cluster; full ops doc: [`ami-connection-setup-guide.md`](ami-connection-setup-guide.md)
- Monitor — peers / trunks / channels + `ActionHangup`
- Jobs — `CallJobs` list / cancel / **download recording** (`ActionDownloadRecording`) + SignalR `JobUpdated`
- Live events & logs — `/events` · snapshot `GET /api/v1/Events/GetList` · clear `POST /api/v1/Events/ActionClear` · SignalR group `events` / method `LiveEvent` (AMI + app logs + connection)

Envelope: `{ isSuccess, data[], errorMessage }`. No auth in Wave 1.
