# history.2026-07-17

## 2026-07-17 (Asia/Tehran) — browser check v1.2
- **درخواست:** browser check
- **تغییرات:**
  - bootstrap: `karavi/karavi.build.config/browser-check.*` · `karavi.plans.prompt/browser.check.md` · `Initialize-BrowserCheckSession.ps1`
  - visible IDE Browser: AdminPanel (5314) · UserPanel (5312) · WebApi `/health` (5310) · viewports desktop/mobile
  - fix: list PDF/Excel export (Admin toolbar + User jobs) · `CallJobDto.Status` alias · GetList `totalCount` + `filter.*` · UserPanel status normalize
- **تأیید:** tree 9 pass / 0 fail · [BrowserCheck.html](../karavi.status/BrowserCheck.html) · AMI زنده خارج از scope (timeout پروتکل بدون Asterisk)
- **skills:** browser-check · design-auditor · qa-browser-automation · a11y-audit · design-review · debug

## 2026-07-17 (Asia/Tehran) — SHIP-READY Karavi.002 Wave-1 complete (D01–D14)
- **درخواست:** execute Wave-1 plan to completion (autonomous)
- **تغییرات:**
  - تکمیل/هم‌ترازسازی WebApi AMI session · monitor · control · CallJob · SignalR · ConnectionStatus flags
  - AdminPanel + UserPanel `ng build` production سبز
  - `karavi.doc/asterisk-dialplan-contract.md` · `Karavi.002.plan.md` Parts/Results complete
  - reconcile: local-dev-ports · build-profiles · version-manifest · verify-gates · onboarding
- **تأیید:** `dotnet build Ntk.Asterisk.sln -c Release` 0 errors · AdminPanel + UserPanel production build green · verify.karavi-structure OK
- **ریسک باقی:** Asterisk AMI زنده خارجی برای smoke Originate — خارج از verify build
- **skills:** asterisk-voip-stack · asterisk-ami

## 2026-07-17 (Asia/Tehran) — D11 AdminPanel
- **درخواست:** پیاده‌سازی `src/Angular/AdminPanel/` (Angular 19 standalone، fa-first RTL، بدون auth)
- **تغییرات:**
  - Connection / Settings (non-secret flags) / Monitor (peers·trunks·channels + hangup) / Jobs (list·cancel)
  - SignalR `/hubs/asterisk` · environment → `http://localhost:5310` · port `5314`
  - i18n `public/assets/i18n/fa.json` + `en.json` · list toolbar (pagination/sort/search/print)
  - doc: `karavi/karavi.doc/admin-panel.md`
- **تأیید:** `npm run build` در AdminPanel — سبز
- **خارج از scope:** WebApi / UserPanel / sln / build-profiles (موازی backend)

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

## 2026-07-17 (Asia/Tehran) — Karavi.002 Wave-1 WebApi backend (D01–D10, D13)
- **درخواست:** پیاده‌سازی Wave-1 backend از plan WebApi/Angular — فقط D01–D10 + D13 + ثبت hostهای panel در karavi (بدون scaffold Angular)
- **تغییرات:**
  - src/DotNet/Ntk.Asterisk.WebApi/ — net8 WebApi + ProjectReference Ntk.AsterNet.AMI
  - AMI session · Monitor (Peers/Channels/Trunks) · Control Hangup/Bridge · CallJob engine (ExtToExt/Mobile*) · SignalR /hubs/asterisk
  - envelope { isSuccess, data[], errorMessage } · CorrelationId · appsettings overlays
  - karavi.doc/asterisk-dialplan-contract.md
  - karavi: local-dev-ports / build-profiles / version-manifest / verify-gates / onboarding · Karavi.002.plan.md
  - solution: WebApi added to Ntk.Asterisk.sln
- **تأیید:** dotnet build src/DotNet/Ntk.Asterisk.WebApi -c Release OK · skills: asterisk-voip-stack + asterisk-ami
- **خارج از scope این agent:** D11/D12 Angular apps (فقط ثبت port/host)
