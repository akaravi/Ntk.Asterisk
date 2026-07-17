# karavi — Ntk.Asterisk

فضای کاری Agent برای **Ntk.Asterisk** (Asterisk AMI & ARI .NET client).

Bootstrap از `Prompts.Project.CMS/karavi/starter-kit` (**بدون وابستگی به karavi پروژهٔ دیگر**).

| مرجع | مسیر |
|---|---|
| قرارداد عمومی | `Prompts.Project.CMS/karavi/README.md` |
| شروع این پروژه | [`karavi.doc/onboarding.md`](karavi.doc/onboarding.md) |
| پلن فعلی | [`karavi.plans.prompt/cursor/Karavi.001.plan.md`](karavi.plans.prompt/cursor/Karavi.001.plan.md) |
| verify ساختار | `.\karavi\karavi.scripts.tools\verify.karavi-structure.ps1` |

```powershell
. .\karavi\karavi.scripts.tools\workspace.paths.ps1
Get-KaraviPaths
dotnet build Ntk.Asterisk.sln -c Release --nologo
```

**productId:** `ntk-asterisk` · **تحویل:** NuGet (`Ntk.AsterNet.AMI`, `Ntk.AsterNet.ARI`)

### Skills اجباری VoIP

ورود: `.cursor/skills/asterisk-voip-stack/` · دامنه: `asterisk-dialplan` · `asterisk-agi` · `asterisk-ami` · `asterisk-callfile`  
Tutorials: `karavi.doc/Learn/voiping_*.pdf` · Rule: `.cursor/rules/asterisk-voip-skills.mdc`
