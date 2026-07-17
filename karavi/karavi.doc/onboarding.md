# onboarding — Ntk.Asterisk

## ۱. شناسه محصول

- **productId:** `ntk-asterisk`
- **Solution:** `Ntk.Asterisk.sln`
- **نسخه جاری:** Assembly `1.1.3.0` · Package `1.1.2` (هر دو library)

## ۲. hostها / پروژه‌ها

| id | نقش | مسیر | Dev port |
|---|---|---|---|
| webapi | Web API + SignalR (AMI control plane) | `src/DotNet/Ntk.Asterisk.WebApi/` | `5310` · `/health` |
| admin-panel | Angular Admin (monitor/settings/jobs) | `src/Angular/AdminPanel/` | `5314` |
| user-panel | Angular User (call forms/jobs) | `src/Angular/UserPanel/` | `5312` |
| ami-lib | NuGet library AMI | `src/DotNet/Ntk.AsterNet.AMI/` | — |
| ari-lib | NuGet library ARI | `src/DotNet/Ntk.AsterNet.ARI/` | — |
| console-ami | نمونه Console | `src/DotNet/Asterisk.Console.AMI/` | — |
| console-ari | نمونه Console | `src/DotNet/Asterisk.Console.ARI/` | — |
| winform-ami | نمونه WinForms | `src/DotNet/Asterisk.WinForm.AMI/` | — |
| winform-ari | نمونه WinForms | `src/DotNet/Asterisk.WinForm.ARI/` | — |

**خارجی (نه host این repo):** Asterisk AMI `:5038` · ARI `:8088`

Dialplan/AMI contract: [`asterisk-dialplan-contract.md`](asterisk-dialplan-contract.md)

## ۳. stackهای فعال

`build-profiles.json` → `webapi` + `admin-panel` + `user-panel` + libraries/samples.

## ۴. تحویل

- **WebApi:** runnable روی `http://localhost:5310` (AMI via `Ntk.AsterNet.AMI`)
- Libraries: **NuGet** (`GeneratePackageOnBuild`) برای AMI و ARI
- FTP/httpdocs: اعمال ندارد مگر Deploy صریح
- smoke: `dotnet build Ntk.Asterisk.sln` + `GET /health`

## ۵. FTP و تست (محلی — در صورت نیاز)

```powershell
Copy-Item karavi\karavi.deploy.config\Deploy_FTP.info.example karavi\karavi.deploy.config\Deploy_FTP.info
Copy-Item karavi\karavi.deploy.config\Deploy_TestUsers.info.example karavi\karavi.deploy.config\Deploy_TestUsers.info
```

هرگز commit نکن. credentialهای Asterisk/AMI/ARI هم در git نروند.

## ۶. verify

```powershell
.\karavi\karavi.scripts.tools\verify.karavi-structure.ps1
dotnet build Ntk.Asterisk.sln -c Release --nologo
Push-Location src\Angular\AdminPanel; ng build --configuration=production; Pop-Location
Push-Location src\Angular\UserPanel; ng build --configuration=production; Pop-Location
```

## ۷. پلن‌ها

- `karavi/karavi.plans.prompt/cursor/Karavi.001.plan.md` — bootstrap karavi + VoIP skills
- `karavi/karavi.plans.prompt/cursor/Karavi.002.plan.md` — Wave 1 WebApi + panels

## ۸. Skills اجباری Asterisk / VoIP

قبل از هر کار Dialplan / AGI / AMI / Call File / ARI این skills را بخوان:

| Skill | مسیر | منبع tutorial |
|---|---|---|
| stack (ورود اجباری) | `.cursor/skills/asterisk-voip-stack/` | هر چهار PDF |
| dialplan | `.cursor/skills/asterisk-dialplan/` | `voiping_dialplan_tutorial.pdf` |
| agi / FastAGI | `.cursor/skills/asterisk-agi/` | `voiping_agi_programing_tutorial.pdf` |
| ami | `.cursor/skills/asterisk-ami/` | `voiping_ami_programing_tutorial.pdf` |
| callfile | `.cursor/skills/asterisk-callfile/` | `voiping_callfile_tutorial.pdf` |

PDFها: `karavi/karavi.doc/Learn/`. Rule همیشه فعال: `.cursor/rules/asterisk-voip-skills.mdc`.
