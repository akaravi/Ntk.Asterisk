# history.2026-07-17

## 2026-07-17 (Asia/Tehran) — Karavi.001 bootstrap
- **درخواست:** پیاده‌سازی `karavi` از `Prompts.Project.CMS/karavi` در مخزن Ntk.Asterisk
- **تغییرات:**
  - اجرای `install-karavi.ps1 -TargetRepo` (starter-kit → `karavi/`)
  - append بلوک gitignore karavi
  - سفارشی‌سازی `productId=ntk-asterisk` برای libraries AMI/ARI + samples
  - تنظیم `version-manifest`, `build-profiles`, `local-dev-ports`, deploy/publish configs
  - تکمیل `karavi.doc/onboarding.md` و `Karavi.001.plan.md` (Result complete)
- **تأیید:** `verify.karavi-structure.ps1`

## 2026-07-17 (Asia/Tehran) — Asterisk VoIP project skills
- **درخواست:** خواندن tutorials Dialplan/AGI/AMI/CallFile و ایجاد skills پروژه‌ای برای استفاده در تمام بخش‌های بعدی
- **تغییرات:**
  - `.cursor/skills/asterisk-voip-stack/` (ورود اجباری + decision matrix)
  - `.cursor/skills/asterisk-dialplan/` + `reference.md`
  - `.cursor/skills/asterisk-agi/` + `reference.md` (FastAGI ↔ Ntk.AsterNet.AMI)
  - `.cursor/skills/asterisk-ami/` + `reference.md` (ManagerConnection)
  - `.cursor/skills/asterisk-callfile/` + `reference.md`
  - `.cursor/rules/asterisk-voip-skills.mdc` (`alwaysApply: true`)
  - به‌روزرسانی `karavi.doc/onboarding.md` §۸ · `karavi/README.md` · `Karavi.001.plan.md` Part2/Result2
- **منابع:** `karavi.doc/Learn/voiping_{dialplan,agi_programing,ami_programing,callfile}_tutorial.pdf`
- **تأیید:** وجود SKILL.mdها + rule + لینک از onboarding
