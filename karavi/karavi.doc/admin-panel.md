# AdminPanel (Angular)

Host path: `src/Angular/AdminPanel/`  
Dev port: **5314** · API base: `http://localhost:5310` (environment files only).

Wave 1 surfaces:

- Connection — AMI + SignalR status via `GET /api/v1/Asterisk/Connection/GetStatus`
- Settings — client endpoints + AMI **configured?** flags (never secrets)
- Monitor — peers / trunks / channels + `ActionHangup`
- Jobs — `CallJobs` list / cancel + SignalR `JobUpdated`

Envelope: `{ isSuccess, data[], errorMessage }`. No auth in Wave 1.
