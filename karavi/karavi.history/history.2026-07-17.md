# history.2026-07-17

## 2026-07-17 (Asia/Tehran) — Shared card-list UI for Admin lists
- **درخواست:** فرم زیبای فهرست کارهای تماس برای لیست‌های دیگر پروژه
- **تغییرات:**
  - الگوی entity-card مشترک در Admin `styles.scss`
  - Jobs + Monitor: کارت‌بورد به‌جای table · toolbar فیلتر + actions · pager پایین
  - `ListPagerComponent` · i18n SUBTITLE/TYPE/STATUS
- **تأیید:** ng build AdminPanel

## 2026-07-17 (Asia/Tehran) — Originate reason=0 → LocalContext default
- **درخواست:** تکرار خطای `OriginateResponse: Failure reason=0`
- **ریشه:** DirectTech با `DefaultTrunk=trunk-out` اغلب endpoint واقعی PJSIP نیست → reason=0
- **تغییرات:**
  - پیش‌فرض `OriginateVia=LocalContext` · `OriginateContext=from-internal` → `Local/{number}@from-internal/n`
  - تنظیمات Admin + DTO + App_Data · پیام خطا با Channel+Data
  - قرارداد dialplan به‌روز شد
- **تأیید:** dotnet build WebApi · ng build AdminPanel
- **اقدام کاربر:** restart WebApi سپس تماس مجدد

## 2026-07-17 (Asia/Tehran) — AMI guide expanded (≥100 words/section)
- **درخواست:** آموزش‌ها کامل شوند؛ هر بخش به همه موارد ارجاع دهد و هر توضیح ≥۱۰۰ کلمه
- **تغییرات:**
  - بازنویسی کامل `karavi/karavi.doc/ami-connection-setup-guide.md` (بخش‌های ۰–۱۰ + EN)
  - Settings UI: ۸ مرحله راهنما + intro؛ fa/en هر BODY ≥۱۰۰ کلمه با ارجاع متقابل
- **تأیید:** word-count script · `ng build` AdminPanel سبز

## 2026-07-17 (Asia/Tehran) — Settings: Test Connection button
- **درخواست:** دکمه تست اتصال روی تنظیمات ذخیره‌شده AMI
- **تغییرات:**
  - WebApi: `IAmiSession.TestConnectionAsync` · `POST /api/v1/Config/ActionTestConnection`
  - Admin Settings: دکمه «تست اتصال» (header + form) · نتیجه host/port/version/error · i18n fa/en
- **تأیید:** dotnet build WebApi · ng build AdminPanel

## 2026-07-17 (Asia/Tehran) — AMI connection setup guide (server + panel)
- **درخواست:** راهنمای مرحله‌به‌مرحله کامل پیاده‌سازی تنظیمات اتصال روی سرور/پنل Asterisk
- **تغییرات:**
  - `karavi/karavi.doc/ami-connection-setup-guide.md` — manager.conf · FreePBX/Issabel · trunk/ChannelTech · Admin Settings · فایروال · عیب‌یابی · امنیت
  - Admin Settings: بلوک راهنما (۶ مرحله) + i18n fa/en
  - لینک از `admin-panel.md` · `onboarding.md` · `asterisk-dialplan-contract.md`
- **تأیید:** فایل راهنما + build AdminPanel

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

## 2026-07-17 (Asia/Tehran) — Admin: load/save AMI connection settings
- **درخواست:** تنظیمات اتصال AMI از مدیریت سیستم دریافت و ذخیره شود
- **تغییرات:**
  - WebApi: `IAsteriskSettingsService` / `AsteriskSettingsService` — seed از appsettings · persist در `App_Data/asterisk-settings.json` · secret هرگز در GET برنمی‌گردد
  - `ConfigController`: `GET GetSiteSettings` · `POST UpdateSiteSettings` (+ reconnect اختیاری)
  - `AmiSession` / `MonitorService` / `CallJobEngine` از effective settings
  - Admin Settings: فرم reactive load/save + i18n fa/en
  - `.gitignore`: `App_Data/asterisk-settings.json`
  - doc: `karavi.doc/admin-panel.md`
- **تأیید:** `dotnet build` WebApi سبز · `ng build` AdminPanel سبز · smoke GET/POST SiteSettings (`isSuccess=true`, secretReturned=false, file persisted)

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

## 2026-07-17 (Asia/Tehran) — guide paths per lesson
- **درخواست:** آدرس فایل‌ها و URLها (کپی‌پذیر) هر آموزش باید مابین همان آموزش باشد
- **تغییرات:**
  - Admin Settings: بلوک paths داخل هر درس S1–S8 (نه یک بلوک سراسری) · i18n `GUIDE_Sn_PATHS` fa/en
  - `ami-connection-setup-guide.md`: زیربخش paths داخل بخش‌های ۰–۹
- **تأیید:** `npm run build` AdminPanel سبز

## 2026-07-17 (Asia/Tehran) — CallJob list reason + timing columns
- **درخواست:** نمایش دلیل موفق/ناموفق، زمان تماس، شروع، پایان، طول تماس در فهرست کارها
- **تغییرات:**
  - WebApi: `ResultReason` · `StartedAtUtc` · `EndedAtUtc` · `DurationSeconds` · `CallTimeUtc` روی CallJobDto؛ ثبت در engine
  - UserPanel + AdminPanel jobs list/export + i18n fa/en
- **تأیید:** `dotnet build` WebApi · `ng build` UserPanel + AdminPanel سبز

## 2026-07-17 (Asia/Tehran) — fix PJSIP Originate channel format (reason=0)
- **درخواست:** OriginateResponse Failure reason=0
- **ریشه:** فرمت chan_sip برای PJSIP (`PJSIP/trunk/number`) → endpoint نامعتبر؛ reason=0
- **تغییرات:** `FormatTrunkDial` → `PJSIP/{number}@{trunk}`؛ پیام خطای خوانا + به‌روزرسانی dialplan-contract
- **تأیید:** `dotnet build` WebApi سبز
- **اقدام اپراتور:** نام DefaultTrunk باید با `pjsip show endpoints` یکی باشد

## 2026-07-17 (Asia/Tehran) — design-auditor Signal Desk redesign
- **درخواست:** /design-auditor کلیه صفحات را از صفر طراحی کن
- **جهت:** Signal Desk — slate/teal · Vazirmatn + IBM Plex (self-host) · بدون purple/pill-slop
- **UserPanel:** shell · forms · jobs (card board با reason + timing)
- **AdminPanel:** dark rail · connection stats · monitor tabs · jobs/settings/toolbar tokens
- **گزارش:** `karavi/karavi.status/DesignAuditor_SignalDesk.json`
- **نمرات:** Design B+ · AI Slop A · Accessibility A-
- **تأیید:** `ng build` UserPanel + AdminPanel سبز

## 2026-07-17 (Asia/Tehran) — rebrand dashboards to مدیریت تماس
- **درخواست:** حذف عبارت آستریسک از داشبوردها؛ برند «مدیریت تماس»
- **تغییرات:** User/Admin titles · brand mark NTK · CALLS · index.html · i18n fa/en (عنوان، نسخه سرور، راهنما)
- **تأیید:** `ng build` UserPanel + AdminPanel سبز
