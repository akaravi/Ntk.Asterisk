# History 2026-07-18

## 2026-07-18 (Asia/Tehran) — Monitor: live call state on peer tiles
- **درخواست:** هنگام تماس/تغییر وضعیت، کارت اکستنشن/ترانک عوض شود
- **Backend:** enrich peers از Status channels + DeviceState/ExtensionStatus · Status→`In Use`/`Ringing (Ns)` · AMI NewState/DialBegin/DeviceState
- **Admin:** tone فعال/زنگ · مدت تماس · callerId روی tile
- **Verify:** WebApi + AdminPanel build · Peers DTO additive fields

## 2026-07-18 (Asia/Tehran) — Monitor: active servers strip above list
- **درخواست:** بالای لیست Monitor سرورهای فعال برای انتخاب منبع مانیتور
- **Admin:** نوار `server-strip` از `Connection/GetList` · badge «در حال مانیتور» · کلیک → `ActionSetDefault` + reconnect + reload peers/channels
- **i18n:** fa/en `MONITOR.SERVERS_*` · `MONITORING` · `SERVER_SWITCH_OK`
- **Verify:** AdminPanel production build green

## 2026-07-18 (Asia/Tehran) — Monitor page fully live (SignalR)
- **ریشه:** سرور فقط `amiEvent` می‌فرستاد؛ UI منتظر `peersUpdated`/`channelsUpdated` بود و `SubscribeMonitor` صدا نمی‌شد
- **Backend:** `MonitorService` هر ۴ثانیه + debounce روی AMI → push `peersUpdated`/`channelsUpdated` به گروه `monitor`
- **Admin:** `subscribeMonitor` در Monitor · merge لایو peers/trunks/channels · bind `amiEvent`/`connectionStatus`
- **Verify:** WebApi build · Peers GetList=48 · AdminPanel production build

## 2026-07-18 (Asia/Tehran) — Persist CallJobs across WebApi restart
- **ریشه:** `CallJobStore` فقط RAM بود → بعد از restart/`بازخوانی` لیست خالی (`totalCount=0`)
- **Fix:** persist اتمی در `App_Data/call-jobs.json` روی Add/Update · load در ctor · jobهای نیمه‌کاره → Failed
- **gitignore:** `call-jobs.json` (+ tmp) · seed بازیابی دو job نمونه از ضبط‌های موجود
- **Verify:** GetList total=2 قبل/بعد restart WebApi

## 2026-07-18 (Asia/Tehran) — Recording filename from/to/date/time
- **فرمت:** `ntk-{yyyyMMdd}-{HHmmss}-from-{from}-to-{to}.wav`
- **Backend:** `CallRecordingService.BuildRecordingFileName` · MixMonitor path · download Content-Disposition
- **UI:** Admin/User download fallback همان الگو (نه jobId)

## 2026-07-18 (Asia/Tehran) — Live job status on User + Admin dashboards
- **ریشه:** API به گروه SignalR `jobs` می‌فرستاد؛ کلاینت‌ها `SubscribeJobs` را صدا نمی‌زدند
- **Admin/User hub:** `SubscribeJobs` پس از connect + rejoin روی reconnect
- **Jobs pages:** merge لایو وضعیت/دکمه Cancel↔Redial بدون Refresh دستی

## 2026-07-18 (Asia/Tehran) — Monitor tabs mosaic like All
- **درخواست:** مابقی تب‌ها (اکستنشن/SIP · ترانک · کانال زنده) مانند تب همه ریسپانسیو
- **Admin:** جایگزینی `entity-board`/`entity-card` با همان `tile-board`/`tile` auto-fill
- **Verify:** AdminPanel production build

## 2026-07-18 (Asia/Tehran) — Redial only after call ended
- **درخواست:** «تماس مجدد» فقط وقتی تماس پایان/متوقف شده؛ نه در حین جریان
- **UI:** Admin + User `canRedial` → terminal (`completed`/`failed`/`cancelled`) یا `endedAtUtc`
- **API:** `CallJobEngine.RedialAsync` همان گارد

## 2026-07-18 (Asia/Tehran) — Monitor All tab compact mosaic
- **درخواست:** تب همه (SIP+ترانک+کانال) · چیدمان کوچک ریسپانسیو · رنگ‌بندی وضعیت · آخرین فعالیت
- **Backend:** `PeerDto.LastActivityUtc` · `ChannelDto.DurationSeconds/LastActivityUtc` · track از PeerStatus + SIPPeers
- **Admin:** تب `همه` پیش‌فرض · tile-board auto-fill · `statusTone` برای OK (n ms) · i18n fa/en
- **Verify:** WebApi Debug build green · AdminPanel production build green
- **Skills:** asterisk-voip-stack + asterisk-ami

## 2026-07-18 (Asia/Tehran) — Connection status list all enabled servers
- **درخواست:** لیست وضعیت همه سرورهای فعال (نه فقط جلسه زنده)
- **API:** `GET /api/v1/Asterisk/Connection/GetList` · `AmiSession.GetStatusListAsync` (live + probe Login)
- **Admin:** صفحه Connection → کارت/لیست همه سرورهای enabled · toolbar/pager/export · i18n fa/en
- **Verify:** WebApi Debug/Release build green · AdminPanel production build green · smoke GetList total=2 (office live + s410 probe)
- **Skills:** asterisk-voip-stack + asterisk-ami

## 2026-07-18 (Asia/Tehran) — Doc: manager.conf reload
- **درخواست:** ذخیره دستورات reload پس از تغییر `/etc/asterisk/manager.conf` در `karavi/karavi.doc`
- **افزوده:** `karavi.doc/asterisk-manager-reload.md` (reload · verify · restart · اثر کلاینت AMI · چک‌لیست)
- **ایندکس:** به‌روزرسانی `karavi.doc/README.md` · لینک از `ami-connection-setup-guide.md` بخش ۲.۳ و جدول ارجاعات

## 2026-07-18 (Asia/Tehran) — Settings tabs polish
- فرم داخل `tabpanel` سرور فعال · عنوان «ویرایش سرور: Name»
- پس از Add، تب سرور جدید انتخاب می‌شود
- کیبورد RTL: ArrowRight/Left · Home/End روی tablist

## 2026-07-18 (Asia/Tehran) — Settings servers as tabs
- **درخواست:** استفاده از تب برای تشخیص واضح لیست سرورها
- **UI:** جایگزینی کارت‌ها با `role=tablist` (الگوی Monitor) — هر سرور: نام + host:port + badge فعال/پیش‌فرض
- **Panel:** اکشن Enable/Disable/SetDefault/Delete زیر تب فعال
- **i18n:** به‌روزرسانی `SETTINGS.SERVERS_HINT` fa/en

## 2026-07-18 (Asia/Tehran) — Multi-server AMI registry
- **درخواست:** افزودن چند سرور + فعال/غیرفعال + پیش‌فرض
- **Backend:** `AsteriskServerConfig` · `AsteriskSettingsService` multi-store · `AsteriskServersController`
- **Persist:** `App_Data/asterisk-servers.json` · migrate از `asterisk-settings.json`
- **AMI:** `GetEffective` = سرور پیش‌فرض فعال · `ConnectionStatus.ServerId/ServerName`
- **Admin:** Settings لیست سرور + Add/Enable/Disable/SetDefault/Delete · Connection نمایش سرور فعال
- **API legacy:** `GetSiteSettings` / `UpdateSiteSettings` (additive `serverId`) حفظ شد
- **Verify:** `dotnet build WebApi -c Release` green
- **Skills:** asterisk-voip-stack + asterisk-ami

## 2026-07-18 (Asia/Tehran) — Read Learn VoIP tutorials (problem context)
- **درخواست:** خواندن `karavi/karavi.doc/Learn` برای حل مشکل صدا/ضبط
- **منابع استخراج‌شده (pypdf):**
  - `voiping_dialplan_tutorial.pdf` (59p) — Channel/Call/Bridge · Dial · Local `/n` · ChanSpy/Monitor path
  - `voiping_ami_programing_tutorial.pdf` (40p) — Login · Events · Originate/OriginateResponse · auto-dial module
  - `voiping_agi_programing_tutorial.pdf` (39p) — AGI نباید Dial/MeetMe/MOH/Monitor بلند · outbound از AGI ممنوع
  - `voiping_callfile_tutorial.pdf` (15p) — Channel + Context/App · spool encoding
- **نگاشت به مشکل موبایل↔موبایل:**
  - Call = Bridge چند Channel (Dialplan p. Channel/Bridge)
  - Local پس از bridge اطلاعاتش از بین می‌رود مگر `/n` — Originate ما `Local/{n}@ctx/n` است
  - Dial فقط Local↔Local را bridge می‌کند؛ media روی SIP peers با trunk `directmedia` جدا می‌ماند → نیاز softmix روی PBX (ConfBridge) نه AMI Bridge خالی
  - ضبط/شنود: مسیر monitor و نقش Channel درست — MixMonitor روی SIP پس از ConfBridge
  - لایه درست: AMI Originate + Dialplan ConfBridge (نه AGI برای Dial/Monitor طولانی)
- **وضعیت کد فعلی:** `CallJobEngine.TryConfBridgePeersAsync` با dual Redirect + Answer/Wait/ConfBridge · download HTTP publish — هم‌راستا با آموزه‌های بالا
- **استخراج متن:** `karavi/karavi.logs/learn-extract/*.txt`
