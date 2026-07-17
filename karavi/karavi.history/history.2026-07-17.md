# history.2026-07-17

## 2026-07-17 (Asia/Tehran) — UserPanel redial (تماس مجدد)
- **درخواست:** دکمه تماس مجدد روی فهرست کارهای تماس کاربر — تکرار همان Originate
- **API:** `POST /api/v1/CallJobs/ActionRedial/{id}` → `CallJobEngine.RedialAsync` کلون Type/From/To/Mobile1/Mobile2/Trunk/CallerId/Timeout و `AddAsync` (AMI Originate جدید)
- **UI:** UserPanel `jobs-list` دکمه «تماس مجدد» · AdminPanel jobs همان Action · i18n fa/en
- **تأیید:** `dotnet build` Release → `karavi.build.files/webapi-redial-verify` سبز · `ng build` UserPanel + AdminPanel سبز
- **ریسک:** WebApi در حال اجرا قفل bin داشت — برای فعال‌سازی endpoint باید API با باینری جدید restart شود
- **مهارت:** asterisk-voip-stack + asterisk-ami (Originate via existing job engine)

## 2026-07-17 (Asia/Tehran) — Live call test + log review (recording path)
- **اقدام:** restart WebApi با باینری جدید · Originate `MobileToMobile` `09125210076`→`09131183892`
- **Job:** `62e4fdc85a8d4fdba103e99d5ccc782d` · ~۳۷ث · `completed`
- **مسیر:** dialing_leg1 → waiting_answer → Dial ANSWER → **SIP promote skipped** → MixMonitor شروع شد (`hasRecording=true`, file `ntk-62e4fdc8….wav`) → Hangup cause=44
- **لاگ AMI:** BridgeLeave `SIP/fanava-0000001e` + Local halves · BridgeDestroy
- **دانلود:** `recordingAvailable=false` · API پیام «configure RecordingLocalDirectory / RecordingHttpBaseUrl» (فایل روی PBX است، mount/HTTP نیست)
- **ریسک باقی:** SIP promote skip → احتمال silence + فایل ضبط روی کانال Local ممکن است خالی/ناقص باشد · نیاز `directmedia=no` یا fix resolve SIP peers
- **لاگ فایل:** `karavi/karavi.logs/webapi-call-test-2026-07-17.log`

## 2026-07-17 (Asia/Tehran) — Call recording download in reports
- **درخواست:** در بخش گزارشات فایل صدای تماس را دانلود بده
- **پیاده‌سازی:**
  - AMI `MixMonitor` پس از SIP Bridge (گزینه `b`) روی `MediaChannel1`
  - `CallRecordingService`: کش `App_Data/recordings` · خواندن از `RecordingLocalDirectory` (UNC/mount) یا pull از `RecordingHttpBaseUrl`
  - API: `GET/POST /api/v1/CallJobs/ActionDownloadRecording/{id}`
  - Admin/User Jobs UI: دکمه «دانلود صدا» · Settings: بخش ضبط
  - تنظیمات env: `RecordingEnabled` · `RecordingAsteriskDirectory` · `RecordingLocalDirectory` · `RecordingHttpBaseUrl` · `RecordingFormat`
- **پیش‌نیاز عملیاتی:** مسیر monitor Asterisk برای WebApi قابل‌دسترسی باشد (mount/rsync/HTTP)
- **تأیید:** `dotnet build` Release → خروجی موقت `karavi.build.files/webapi-recording-verify` سبز · `ng build` AdminPanel + UserPanel سبز
- **نکته:** پروسه WebApi در حال اجرا قفل `bin` داشت؛ برای فعال‌سازی MixMonitor باید API با باینری جدید بالا بیاید

## 2026-07-17 (Asia/Tehran) — Settings UI redesign (design-auditor)
- **درخواست:** `/design-auditor` — طراحی از صفر صفحه Settings
- **Audit قبل:** Design C · AI Slop B · Accessibility B — فرم تخت auto-fit، اکشن تکراری، وضعیت LTR dump
- **طراحی جدید:** masthead + یک خوشه اکشن · status rail (pills + endpoints) · ۴ fieldset معنایی · نتیجه تست به‌صورت entity-card · guide حفظ شد
- **فایل‌ها:** `settings-page.component.{html,scss}` · i18n fa/en (SECTION_*) · `admin-panel.md`
- **تأیید:** `ng build` development سبز
- **نمرات هدف پس از redesign:** Design A− · AI Slop A · Accessibility A (WCAG AA floor)

## 2026-07-17 (Asia/Tehran) — Fix silent mobile↔mobile (Dial + SIP promote)
- **مشکل:** صدا جابجا نمی‌شد با وجود Bridged
- **ریشه:** مسیر Local (click-to-call / Dial فقط روی Local) + trunk `directmedia` → RTP بین دو SIP fanava از Local عبور می‌کند و اغلب silent
- **رفع:** LocalContext = `Application=Dial Local/leg2` (MOH تا پاسخ leg2) → سپس AMI `Bridge` روی `SIP/fanava-…` ↔ `SIP/fanava-…` (PromoteSipMediaBridge) · بدون hang Local
- **شواهد live job c55288e7…:** `Two-way SIP bridge SIP/fanava-0000001b <-> SIP/fanava-0000001c` · hold ~۴۵ث
- **تأیید:** `dotnet build` Release · API :5310 Healthy

## 2026-07-17 (Asia/Tehran) — Live test 09125210076→09131183892 + SIP Bridge hangup fix
- **درخواست:** تماس MobileToMobile و تشخیص چرا صدا جابجا نمی‌شود
- **شواهد live (job bf04f242… / 1b95d9eb… / 62fb1e13…):**
  - Originate Local + MOH/Wait OK
  - `BRIDGEPEER` → `SIP/fanava-…` جفت‌ها resolve شد · AMI Bridge SIP↔SIP
  - Hangup روی Local بعد از Bridge → cascade cause=16/44 · تماس ~۱۱–۱۴ث می‌میرد · RTP تلفن قطع
- **ریشه ConfBridge/Local Bridge:** اپ روی Local`;1`؛ RTP واقعی روی SIP trunk — ConfBridge/Bridge روی Local رسانه را جابجا نمی‌کند
- **رفع نهایی (audio-first):** LocalContext پیش‌فرض → **click-to-call** (`Context/Exten/Priority`) تا FreePBX Dial مالک RTP باشد
- **حفظ کد:** `ExecuteLocalTwoLegSipBridgeAsync` (MOH+SIP Bridge) · بدون hang Local بعد از Bridge · GetVar timeout کوتاه · Hangup Local بعد از resolve SIP نادیده
- **تأیید live click-to-call job e4504583…:** bridged در ~۳ث · hold ۳۵ث · `dotnet build` Release · API :5310
- **تماس ناموفق میانی:** leg1 قطع قبل از leg2 · AMI Originate timeout (timeout Originate ۱۰ث)

## 2026-07-17 (Asia/Tehran) — WebApi crash on AMI socket null
- **ریشه:** `ManagerReader.Run` با `SystemException: socket is null` کل پروسه را می‌کشت (.NET Core)
- **رفع:** خروج graceful از reader + catch بیرونی؛ MOH ConfBridge با timeout کوتاه (best-effort)
- **تأیید:** rebuild · restart :5310 · Health Healthy

## 2026-07-17 (Asia/Tehran) — ConfBridge for two-way audio + MOH
- **درخواست:** موزیک انتظار پخش شد ولی صدا بین دو گوشی رد نشد
- **ریشه:** AMI `Bridge` روی Localهای `MusicOnHold`/`Wait` مسیر RTP تلفن↔تلفن نمی‌سازد (FreePBX)
- **رفع LocalContext:** Leg1+Leg2 → `Application=ConfBridge` (یک room) · `ConfbridgeStartMoh` تا پاسخ leg2 · سپس `ConfbridgeStopMoh`
- **تأیید:** `dotnet build` WebApi Release · restart :5310 · health OK
- **اقدام:** تماس Mobile↔Mobile مجدد از UserPanel

## 2026-07-17 (Asia/Tehran) — Two-leg MOH + Bridge (audio + waiting music)
- **درخواست:** صدا رد نمی‌شود · موزیک انتظار تا پاسخ موبایل دوم
- **رفع LocalContext:** Leg1→MusicOnHold · Leg2→Wait · AMI Bridge · تنظیم `MusicOnHoldClass`
- **DirectTech:** Dial با `,m(class)`
- **تأیید:** dotnet build · WebApi restart
- **نتیجه بعدی:** MOH OK بود ولی Bridge هنوز audio نداد → جایگزین ConfBridge

## 2026-07-17 (Asia/Tehran) — Mobile↔Mobile ring but no audio
- **درخواست:** هر دو موبایل زنگ خوردند ولی صدا رد نمی‌شد
- **ریشه:** LocalContext با `Application=Dial` روی FreePBX معمولاً Local را early-Answer می‌کند → زنگ دوطرفه بدون RTP bridge
- **رفع:** LocalContext → `Channel=Local/…` + `Context/Exten/Priority=1` (click-to-call استاندارد)
- **تأیید:** dotnet build · WebApi restart

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

## 2026-07-17 (Asia/Tehran) — fix Events GetList 404 in Admin
- **ریشه:** WebApi قدیمی روی :5310 بدون `EventsController` → 404
- **رفع:** API با بیلد دارای Events در حال اجرا · صفحه live-events retry یک‌بار روی 404 · پاک کردن banner با ورود ایونت زنده
- **تأیید:** browser `/events` — LIVE + ردیف‌های AMI/app بدون خطای 404

## 2026-07-17 (Asia/Tehran) — ConfBridge audio + AstDB recording download harden
- **شکایت:** دانلود صدا خطا · رد نشدن صدا
- **صدا:** پس از Dial ANSWER → Redirect دو SIP به ConfBridge موقت + Wait(0.5) + mixing_interval · تأیید `confbridge list` parties=2 · fallback AMI Bridge · حفظ ResultReason ConfBridge (cause=44 دیگر آن را پاک نمی‌کند)
- **ضبط:** MixMonitor بدون option `b` (ConfBridge را bridge کلاسیک نمی‌داند → سکوت) · encode پس از تماس با Originate `System` روی `Local/s@default/n` · AstDB bulk `database show family` (نه DBGet پر از race) · chunk 1024 · enc flag · AMI send lock
- **شواهد live:** job `ce5e9431…` ConfBridge parties=2 · wav روی PBX `FOUND` (۷ فایل ntk-*.wav در monitor)
- **باقی:** دانلود کامل وابسته به اتمام encode AstDB (کند؛ یک `asterisk -rx` per chunk) · در صورت نیاز `RecordingLocalDirectory` UNC سریع‌تر است

## 2026-07-17 (Asia/Tehran) — Fix recording download + ConfBridge audio path
- **Root causes:** (1) MixMonitor `Command=` + `asterisk -rx` deadlocked AstDB encode; AstDB value max ~256B so 1024 chunks never stored; (2) ConfBridge dialplan with `CONFBRIDGE(user,*)` Sets yielded parties=0; AMI Bridge Success without RTP → empty 44-byte wav.
- **Fix:** HTTP publish to `/var/www/html/ntk-recordings` + HTTPS pull (self-signed OK); no MixMonitor Command encode; minimal ConfBridge Answer/Wait/ConfBridge; reject recordings < 2KB.
- **Verify:** job `dd5df820…` ConfBridge parties=2 · download `audio/wav` **380524** bytes · WebApi fix11.
