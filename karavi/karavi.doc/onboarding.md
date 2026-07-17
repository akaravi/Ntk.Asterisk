# onboarding — Ntk.Asterisk

## ۱. شناسه محصول

- **productId:** `ntk-asterisk`
- **Solution:** `Ntk.Asterisk.sln`
- **نسخه جاری:** Assembly `1.1.3.0` · Package `1.1.2` (هر دو library)

## ۲. hostها / پروژه‌ها

| id | نقش | مسیر |
|---|---|---|
| ami-lib | NuGet library AMI | `src/DotNet/Ntk.AsterNet.AMI/` |
| ari-lib | NuGet library ARI | `src/DotNet/Ntk.AsterNet.ARI/` |
| console-ami | نمونه Console | `src/DotNet/Asterisk.Console.AMI/` |
| console-ari | نمونه Console | `src/DotNet/Asterisk.Console.ARI/` |
| winform-ami | نمونه WinForms | `src/DotNet/Asterisk.WinForm.AMI/` |
| winform-ari | نمونه WinForms | `src/DotNet/Asterisk.WinForm.ARI/` |

**خارجی (نه host این repo):** Asterisk AMI `:5038` · ARI `:8088`

## ۳. stackهای فعال

`build-profiles.json` → فقط `dotnet` (بدون Angular / Flutter / SPA).

## ۴. تحویل

- اصلی: **NuGet** (`GeneratePackageOnBuild`) برای AMI و ARI
- FTP/httpdocs: اعمال ندارد مگر Deploy صریح برای artifact سفارشی
- smoke: `dotnet build` + وجود `.nupkg` در `karavi.deploy.files/`

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
```

## ۷. پلن‌ها

`karavi/karavi.plans.prompt/cursor/Karavi.001.plan.md`

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
