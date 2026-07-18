# Ntk.Asterisk.WebPhone

SipJS Browser Phone (Voip.VOIZ-based) softphone UI.

- **Host:** static files under `wwwroot/` on port **5316** (dev)
- **API:** `Ntk.Asterisk.WebApi` only — never co-host UI under WebApi
- **Config:** `WebPhoneUi:ApiBaseUrl` → served as `/ntk-webphone-config.js`

```powershell
dotnet run --project src/WebPhone/Ntk.Asterisk.WebPhone
# http://localhost:5316/
```

AGPL vendor notice: `wwwroot/vendor-voiz/`.
