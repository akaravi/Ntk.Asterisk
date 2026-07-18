# راهنمای مرحله‌به‌مرحله — پیکربندی اتصال AMI برای Ntk.Asterisk

**Audience:** مدیر سیستم / ادمین PBX · **Locale:** fa-IR (اصطلاحات فنی EN)  
**Skills:** `asterisk-voip-stack` · `asterisk-ami` · قرارداد: [`asterisk-dialplan-contract.md`](asterisk-dialplan-contract.md)  
**UI:** AdminPanel → **Settings** · API: `GET/POST GetSiteSettings` · `UpdateSiteSettings` · `ActionTestConnection`  
**مرتبط:** [`admin-panel.md`](admin-panel.md) · [`onboarding.md`](onboarding.md) · `App_Data/asterisk-settings.json` · صفحات Connection / Monitor / Jobs

این سند مرجع عملیاتی کامل برای هم‌تراز کردن **سرور Asterisk یا پنل FreePBX/Issabel/Elastix** با فیلدهای **مدیریت سیستم (Settings)** در Ntk.Asterisk است.

هر بخش زیر حداقل حدود صد کلمه توضیح دارد و به بخش‌های دیگر، فیلدهای UI، کلاس‌های Manager، ترانک، شبکه، تست اتصال و عیب‌یابی ارجاع متقابل می‌دهد تا بدون حدس بتوانید انتهابه‌انتها پیاده‌سازی کنید.

---

## آدرس‌ها و مسیرها

هر بخش زیر یک زیربخش **آدرس فایل‌ها و URLها (کپی‌پذیر)** دارد که فقط مسیر/URL مربوط به همان مرحله را فهرست می‌کند.

ایندکس سریع پورت‌های توسعه: `karavi/karavi.build.config/local-dev-ports.json`  
Admin: `http://localhost:5314` · WebApi: `http://localhost:5310` · AMI: `tcp://ASTERISK_HOST:5038`

---

## ۰) تصویر کلی جریان و نقش هر لایه

```text
[AdminPanel Settings]  ──save──►  [WebApi App_Data/asterisk-settings.json]
                                         │
                                         ▼
                               [AmiSession → TCP :5038]
                                         │
                                         ▼
                         [Asterisk manager.conf / FreePBX Manager User]
                                         │
                    ┌────────────────────┼────────────────────┐
                    ▼                    ▼                    ▼
              Connection page      Monitor peers       CallJobs Originate
```

لایهٔ Asterisk مسئول حقیقت شبکه و احراز هویت است:

- `manager.conf` یا کاربر Manager در پنل
- `bindaddr` · `permit/deny`
- کلاس‌های `read` / `write`

لایهٔ AdminPanel فقط مقادیر عملیاتی را از کاربر می‌گیرد و از طریق API ذخیره می‌کند.

- Secret هرگز در پاسخ GET برنمی‌گردد
- فیلد خالی یعنی حفظ رمز قبلی

لایهٔ WebApi از `IAsteriskSettingsService` تنظیمات مؤثر را می‌خواند، در `AmiSession` لاگین می‌کند، و همان session را برای Connection Status، Monitor، Hangup و CallJob استفاده می‌کند.

اگر فقط یکی از این سه لایه درست باشد (مثلاً Settings پر شده ولی Manager روی سرور خاموش است)، تست اتصال شکست می‌خورد.

بنابراین بخش‌های ۲ تا ۹ را به ترتیب و با ارجاع متقابل اجرا کنید.

پورت پیش‌فرض AMI برابر **۵۰۳۸** است و باید با فیلد Port در Settings یکی باشد.

پس از ذخیره، دکمهٔ **تست اتصال** همان تنظیمات persist‌شده را force-reconnect می‌کند؛ جزئیات در بخش ۶ و ۷.

| لایه | مسئولیت اصلی | ارجاع |
|------|---------------|--------|
| Asterisk / FreePBX | Manager، permit، کلاس‌ها، نام ترانک | بخش ۲ و ۳ و ۴ |
| Admin Settings | Host/Port/User/Secret/Tech/Trunk/… | بخش ۶ |
| WebApi AmiSession | Login، KeepAlive، Ping، Originate | بخش ۰ و ۷ و قرارداد dialplan |

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
http://localhost:5314/settings
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs
http://localhost:5310/health
http://localhost:5310/hubs/asterisk
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
/etc/asterisk/manager.conf
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.doc/asterisk-dialplan-contract.md
```

---

## ۱) پیش‌نیازها و جمع‌آوری اطلاعات قبل از هر تغییر

قبل از دست زدن به `manager.conf` یا پنل FreePBX باید پنج دسته اطلاعات را قطعی کنید تا بعداً بین Host اشتباه، ChannelTech غلط و ترانک ناموجود سرگردان نشوید.

1. دسترسی SSH یا کنسول ادمین به سرور PBX و مجوز Apply Config
2. IP یا hostnameای که **از ماشین WebApi** واقعاً به پورت ۵۰۳۸ می‌رسد (نه لزوماً IP عمومی نمایش‌داده‌شده به کاربر نهایی)
3. استک کانال **PJSIP** یا legacy **SIP** — عین فیلد Channel tech و قرارداد Originate در [`asterisk-dialplan-contract.md`](asterisk-dialplan-contract.md)
4. نام دقیق endpoint یا ترانک خروجی برای تماس موبایل (همان Default trunk)
5. WebApi و AdminPanel در حال اجرا (توسعه: API `5310` · Admin `5314`) تا بتوانید بلافاصله ذخیره، تست اتصال و صفحه Connection را ببینید

اگر Asterisk روی همان ماشین WebApi است، Host معمولاً `127.0.0.1` و bindaddr هم لوکال است.

اگر جداست، بخش ۵ (فایروال و permit) اجباری می‌شود.

این پیش‌نیازها به بخش‌های ۲، ۳، ۴، ۵ و ۶ گره خورده‌اند؛ بدون آن‌ها پر کردن فرم Settings فقط Configured ناقص می‌سازد.

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
karavi/karavi.build.config/local-dev-ports.json
karavi/karavi.doc/asterisk-dialplan-contract.md
karavi/karavi.doc/ami-connection-setup-guide.md
http://localhost:5310
http://localhost:5314
http://localhost:5314/settings
tcp://ASTERISK_HOST:5038
/etc/asterisk/manager.conf
```

---

## ۲) فعال‌سازی AMI روی Asterisk خام (`manager.conf`) — کامل

مسیر رایج `/etc/asterisk/manager.conf` است؛ بعضی توزیع‌ها قطعات را در `/etc/asterisk/manager.d/*.conf` می‌گذارند. هدف این بخش روشن کردن سرویس Manager، گوش دادن روی پورت درست، و ساخت کاربر با حداقل دسترسی است تا AdminPanel بتواند Login کند و CallJob بتواند Originate بزند بدون اینکه کل سطح ادمین Asterisk را باز کنید.

### ۲.۱ بخش `[general]`

```ini
[general]
enabled = yes
port = 5038
bindaddr = 127.0.0.1
; WebApi روی ماشین دیگر: bindaddr را به IP LAN بدهید و فایروال را محدود کنید
```

کلید `enabled` اگر `no` باشد هیچ Loginای موفق نمی‌شود و تست اتصال در Settings با timeout یا refused برمی‌گردد.

`port` باید با فیلد Port در Settings یکی باشد؛ تغییر پورت بدون به‌روزرسانی UI یعنی کل لایهٔ WebApi به هدف اشتباه وصل می‌شود.

`bindaddr` تعیین می‌کند کدام کارت شبکه گوش می‌دهد:

- لوکال فقط برای هم‌ماشین
- برای سرور جدا: IP قابل‌دسترس از WebApi + بخش ۵ (فایروال) و `permit` کاربر

این زیربخش به فیلدهای Host و Port در بخش ۶ و به عیب‌یابی «Connection refused» در بخش ۸ وصل است.

### ۲.۲ کاربر Manager با least privilege

```ini
[ntk-ami]
secret = REPLACE_WITH_STRONG_SECRET
deny = 0.0.0.0/0.0.0.0
permit = 127.0.0.1/255.255.255.255
; مثال WebApi جدا: permit = 10.0.0.50/255.255.255.255
read = system,call,log
write = system,call,originate
```

نام بخش (`ntk-ami`) همان Username در Settings است.

`secret` همان Secret است و هرگز نباید در git، history یا پاسخ API ظاهر شود.

الگوی `deny` همه و سپس `permit` فقط IP WebApi استاندارد امنیتی است و با بخش ۹ هم‌راستاست.

کلاس‌های پیشنهادی Wave-1 از قرارداد dialplan:

- `write=originate` → CallJobs
- `write=call` → Hangup / Bridge
- `read=call,system` → Monitor و وضعیت کانال/Peer و FullyBooted/Ping

کمبود `originate` باعث می‌شود اتصال Login موفق باشد ولی Originate در Jobs شکست بخورد؛ بنابراین بخش ۴ و صفحه Jobs را بعد از Login موفق جداگانه تأیید کنید.

پس از ویرایش حتماً `manager reload` و سپس دستورات بخش ۲.۳.
مرجع کامل reload / restart / اثر روی کلاینت AMI: [`asterisk-manager-reload.md`](asterisk-manager-reload.md).

### ۲.۳ اعمال، CLI و تست شبکه

جزئیات و ماتریس «کی reload کافی نیست»: [`asterisk-manager-reload.md`](asterisk-manager-reload.md).

```bash
asterisk -rx "manager reload"
asterisk -rx "manager show settings"
asterisk -rx "manager show users"
asterisk -rx "manager show user ntk-ami"
# از ماشین WebApi:
nc -vz ASTERISK_IP 5038
```

این خروجی‌ها باید کاربر، permit و پورت را تأیید کنند قبل از اینکه وقت روی UI بگذارید. اگر `manager show user` کاربر را نشان ندهد، بخش ۳ (پنل) یا ذخیرهٔ فایل اشتباه است. اگر TCP بسته باشد، بخش ۵ را قبل از Settings انجام دهید. بعد از موفقیت شبکه، بخش ۶ را با همان Username/Secret/Host/Port پر کنید و تست اتصال بزنید.

| فیلد Admin Settings | معادل Asterisk | بخش مرتبط |
|---------------------|----------------|-----------|
| Username | نام `[section]` | ۲.۲ · ۳ |
| Secret | `secret=` | ۲.۲ · ۶ · ۹ |
| Host | IP قابل‌دسترس تا bindaddr | ۲.۱ · ۵ · ۶ |
| Port | `port` در `[general]` | ۲.۱ · ۶ |

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
/etc/asterisk/manager.conf
/etc/asterisk/manager.d/*.conf
http://localhost:5314/settings
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.build.config/local-dev-ports.json
tcp://ASTERISK_HOST:5038
```

---

## ۳) پیاده‌سازی از پنل FreePBX / Issabel / Elastix — کامل

اگر به‌جای ویرایش دستی از GUI استفاده می‌کنید، هنوز همان مفاهیم بخش ۲ برقرار است؛ فقط مسیر ورود تغییر می‌کند.

**FreePBX**

- مسیر معمول: **Settings → Asterisk Manager Users** (نسخه‌های قدیمی‌تر گاهی Admin)
- Add Manager
- Username و Password عین Settings
- Deny گسترده · Permit فقط IP WebApi
- Read حداقل `system,call`
- Write حداقل `system,call,originate`
- Submit و **Apply Config**
- بلافاصله CLI: `manager show user <name>`

**Issabel / Elastix**

- مسیر تقریبی: **PBX → Tools → Asterisk Manager Users**
- پس از Apply همان مقادیر را در AdminPanel بگذارید

**نکات**

- فایل‌های تولیدشدهٔ `_additional` را دستی overwrite نکنید
- dialplan سفارشی فقط `extensions_custom.conf` طبق قرارداد dialplan
- اگر Login از Settings شکست خورد ولی کاربر در GUI دیده می‌شود → بخش ۵ و بخش ۸

این بخش به فیلدهای Username/Secret/Host و به تست اتصال بخش ۶ و به Monitor/Jobs پس از اتصال موفق گره خورده است.

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
https://ASTERISK_IP/admin/
https://ASTERISK_IP/
/etc/asterisk/extensions_custom.conf
http://localhost:5314/settings
POST http://localhost:5310/api/v1/Config/ActionTestConnection
karavi/karavi.doc/ami-connection-setup-guide.md
```

---

## ۴) ترانک، ChannelTech، فیلتر، CallerId و Timeout — هم‌ترازی با Originate

WebApi در Wave-1 کانال‌ها را طبق [`asterisk-dialplan-contract.md`](asterisk-dialplan-contract.md) می‌سازد:

- ExtToExt: `{ChannelTech}/{from}` و Dial به `{tech}/{to}`
- موبایل با **PJSIP**: `{ChannelTech}/{mobile}@{DefaultTrunk}` (مثلاً `PJSIP/0912…@trunk-out`) — نه `PJSIP/trunk/number`
- موبایل با **SIP** قدیمی: `{ChannelTech}/{DefaultTrunk}/{mobile}`
- `reason=0` روی OriginateResponse معمولاً endpoint/ترانک نامعتبر یا tech غلط است؛ نام DefaultTrunk را با `pjsip show endpoints` یکی کنید.

**Channel tech**

- باید دقیقاً با استک PBX یکی باشد (`PJSIP` مدرن یا `SIP` قدیمی)
- mismatch → Originate فوری Error حتی وقتی Login AMI سبز است

**Default trunk**

- عین نام endpoint ترانک خروجی
- FreePBX: Connectivity → Trunks
- CLI: `pjsip show endpoints`

**Trunk peer filter**

- regex برای تفکیک peerهای ترانک در Monitor (پیش‌فرض `^(trunk|Trunk|TRUNK)`)
- فیلتر غلط → لیست ترانک خالی در حالی که AMI سالم است

**Default CallerId / Timeout**

- CallerId وقتی Job مقدار ندهد روی Originate می‌نشیند
- timeout (ms) در مسیر Dial به ثانیه تبدیل می‌شود؛ مقدار خیلی کوچک تماس واقعی را قطع می‌کند

این بخش به CallJobs، Monitor، Settings و عیب‌یابی «No such endpoint» در بخش ۸ ارجاع دارد. بدون هم‌ترازی Tech/Trunk، تست اتصال ممکن است موفق باشد ولی Jobs شکست بخورد.

| نوع Job | شکل Channel | فیلد Settings وابسته |
|---------|-------------|----------------------|
| ExtToExt | `{tech}/{from}` → Dial `{tech}/{to}` | ChannelTech |
| MobileTo* (PJSIP) | `{tech}/{mobile}@{trunk}` | ChannelTech + DefaultTrunk |
| MobileTo* (SIP) | `{tech}/{trunk}/{mobile}` | ChannelTech + DefaultTrunk |
| نمایش ترانک در Monitor | فیلتر regex روی peer | TrunkPeerFilter |

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
http://localhost:5314/settings
http://localhost:5314/monitor
http://localhost:5314/jobs
http://localhost:5310/api/v1/Asterisk/Peers/GetList
http://localhost:5310/api/v1/Asterisk/Trunks/GetList
http://localhost:5310/api/v1/CallJobs/GetList
karavi/karavi.doc/asterisk-dialplan-contract.md
```

---

## ۵) شبکه، فایروال، bindaddr و permit — کامل

حتی با کاربر Manager درست، اگر از ماشین WebApi به `Host:Port` مسیر TCP نباشد:

- تست اتصال → refused یا timeout
- صفحه Connection → Connected=false
- در حالی که Settings ممکن است Configured=true باشد

اگر `bindaddr=127.0.0.1` است، WebApi باید روی همان ماشین باشد.

در غیر این صورت:

- bind را به IP LAN تغییر دهید
- `permit` کاربر را به IP WebApi محدود کنید

فایروال (firewalld/ufw/security group/SELinux) باید فقط همان IP را به پورت ۵۰۳۸ راه دهد.

باز کردن `permit=0.0.0.0/0.0.0.0` در production بدون VPN یا شبکهٔ خصوصی ممنوع است و با بخش ۹ در تضاد است.

از ماشین WebApi با `nc` یا `Test-NetConnection` قبل از ذخیرهٔ Secret در UI مسیر را ثابت کنید تا عیب شبکه با عیب رمز قاطی نشود.

این بخش به Host/Port در بخش ۶، به `[general]` در بخش ۲، و به ردیف‌های Connection refused در بخش ۸ وصل است.

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
tcp://ASTERISK_HOST:5038
tcp://127.0.0.1:5038
http://localhost:5310/health
http://localhost:5314/settings
http://localhost:5314/connection
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
/etc/asterisk/manager.conf
```

---

## ۶) پر کردن Settings در AdminPanel، ذخیره، تست اتصال و نگاشت همه فیلدها

در مرورگر به AdminPanel بروید (توسعه: `http://localhost:5314`) و منوی **تنظیمات** را باز کنید.

راهنمای روی صفحه در پایین فرم همان مفاهیم این سند را خلاصه می‌کند و مسیر کامل همین فایل است.

**فیلدها**

- Host = IP قابل‌دسترس از WebApi
- Port معمولاً ۵۰۳۸
- Username / Secret = کاربر Manager بخش ۲ یا ۳
- Channel tech / Default trunk / Trunk filter = بخش ۴
- Timeout / CallerId / Ping / KeepAlive = رفتار کلاینت AMI
- Auto-connect = اتصال هنگام استارت WebApi
- Reconnect after save = Login دوباره بعد از ذخیره

Secret خالی = حفظ رمز قبلی · تیک Clear secret = پاک کردن رمز ذخیره‌شده

**ترتیب تأیید**

1. **ذخیره** → وضعیت persisted و فایل `App_Data/asterisk-settings.json` (gitignore)
2. **تست اتصال** → `ActionTestConnection` و نمایش host/port/version/error
3. صفحه **اتصال** · در صورت موفقیت Monitor · Job آزمایشی روی lab

seed اولیه می‌تواند از overlayهای Development/Production آمده باشد؛ منبع حقیقت پس از اولین Save همان App_Data است.

این بخش به همهٔ بخش‌های ۲ تا ۵ و ۷ تا ۹ وابسته است.

| فیلد UI | مثال | منبع حقیقت | بخش |
|---------|------|------------|------|
| Host | `10.0.0.10` / `127.0.0.1` | IP تا bindaddr | ۲ · ۵ |
| Port | `5038` | manager port | ۲ |
| Username / Secret | `ntk-ami` / قوی | Manager user | ۲ · ۳ · ۹ |
| Channel tech | `PJSIP` | استک کانال | ۴ |
| Default trunk | `trunk-out` | endpoint ترانک | ۴ |
| Trunk filter | `^(trunk\|…)` | Monitor | ۴ |
| Timeout / CallerId / Ping / KeepAlive | عملیاتی | Originate/client | ۴ · قرارداد |
| Auto-connect / Reconnect | روشن | AmiSession | ۰ · ۷ |
| تست اتصال | دکمه | ActionTestConnection | ۷ · ۸ |

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
http://localhost:5314/settings
GET http://localhost:5310/api/v1/Config/GetSiteSettings
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings
POST http://localhost:5310/api/v1/Config/ActionTestConnection
http://localhost:5310/hubs/asterisk
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Production.json
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs
```

---

## ۷) چک‌لیست تأیید انتهابه‌انتها (همه سطوح)

این چک‌لیست ترتیب اثبات را از Asterisk تا UI مشخص می‌کند تا نصفه‌کاره «سبز» اعلام نکنید.

1. سرور: `manager show user` و permit درست · سرویس asterisk فعال
2. ماشین WebApi: TCP به `:5038`
3. Settings: ذخیره با persisted و Secret set
4. تست اتصال موفق · در صورت امکان نسخه Asterisk
5. صفحه Connection: Configured و Connected
6. Monitor: peers/channels بدون خطای Auth
7. lab: یک CallJob ExtToExt بین دو داخلی

دستورات کمکی:

```text
manager show connected
core show channels
pjsip show endpoints
```

هر شکست را با بخش ۸ نگاشت کنید.

اگر Login سبز و Originate قرمز است → کلاس write و بخش ۴

این بخش خلاصهٔ اجرایی بخش‌های ۲ تا ۶ است و با [`admin-panel.md`](admin-panel.md) هم‌خوان است.

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
http://localhost:5314/settings
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs
http://localhost:5310/health
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
POST http://localhost:5310/api/v1/Config/ActionTestConnection
POST http://localhost:5310/api/v1/CallJobs/Add
karavi/karavi.doc/admin-panel.md
karavi/karavi.doc/asterisk-dialplan-contract.md
```

---

## ۸) عیب‌یابی رایج با ارجاع به بخش‌ها

قبل از تغییر تصادفی چند فیلد با هم، با این ماتریس ریشهٔ واحد را پیدا کنید.

**Authentication failed**

- Username/Secret اشتباه یا reload نشده
- بخش ۲.۲ · ۳ · ۶ · `manager reload`

**Connection refused / timeout**

- شبکه · bindaddr · Host غلط
- بخش ۲.۱ · ۵

**Permission denied روی Originate**

- کمبود `write=originate`
- بخش ۲.۲ و قرارداد dialplan
- ممکن است تست Login هنوز موفق باشد

**No such endpoint / channel failed**

- ChannelTech یا DefaultTrunk غلط
- بخش ۴ · `pjsip show endpoints`

**Secret missing**

- هنوز ذخیره نشده یا Clear شده
- بخش ۶

**Connected=false با Configured=true**

- Asterisk خاموش یا شبکه بعد از ذخیره خراب
- بخش ۵ · ۷

همیشه Secret را در لاگ و chat چاپ نکنید (بخش ۹).

| نشانه | علت محتمل | اقدام / بخش |
|-------|-----------|-------------|
| Authentication failed | رمز/کاربر/reload | ۲ · ۳ · ۶ |
| Connection refused / timeout | فایروال/bind/Host | ۲.۱ · ۵ |
| Originate denied | کلاس write | ۲.۲ · ۴ · dialplan |
| No such endpoint | Tech/Trunk | ۴ |
| Secret missing | ذخیره/clear | ۶ |
| Connected=false و Configured=true | سرویس/شبکه | ۵ · ۷ |

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
/etc/asterisk/manager.conf
http://localhost:5314/settings
http://localhost:5314/connection
tcp://ASTERISK_HOST:5038
http://localhost:5310/health
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
POST http://localhost:5310/api/v1/Config/ActionTestConnection
karavi/karavi.doc/asterisk-dialplan-contract.md
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
```

---

## ۹) امنیت الزامی در تمام بخش‌ها

امنیت AMI سطح ادمین است و اشتباه در permit معادل باز کردن کنترل کامل تماس‌هاست.

- Secret قوی · هرگز در chat / plan / history / commit / پاسخ API
- GET SiteSettings عمداً Secret برنمی‌گرداند
- UI فقط وضعیت Secret set/missing را نشان می‌دهد
- `permit` فقط IP WebApi
- در production بدون لایهٔ خصوصی، permit همگانی ممنوع
- Production: vault یا overlay محیطی — نه `appsettings.json` پایه
- `App_Data/asterisk-settings.json` را backup امن کنید و در git نگذارید

این الزامات روی بخش‌های ۲، ۳، ۵، ۶ و ۸ اعمال می‌شوند.

نقض هر کدام کل استقرار را ناامن می‌کند حتی اگر تست اتصال سبز باشد.

**آدرس فایل‌ها و URLها (کپی‌پذیر) — این مرحله**

```text
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Production.json
/etc/asterisk/manager.conf
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.doc/asterisk-dialplan-contract.md
karavi/karavi.doc/admin-panel.md
karavi/karavi.doc/onboarding.md
karavi/karavi.build.config/local-dev-ports.json
src/Angular/AdminPanel/src/environments/environment.ts
http://localhost:5314/settings
http://localhost:5310/api/v1/Config/GetSiteSettings
http://localhost:5310/api/v1/Config/ActionTestConnection
```

---

## ۱۰) ارجاعات و مسیر ادامه یادگیری

| سند / skill | کاربرد | ارتباط با بخش‌ها |
|-------------|--------|------------------|
| [`asterisk-dialplan-contract.md`](asterisk-dialplan-contract.md) | Originate، کلاس Manager، Tech/Trunk | ۲ · ۴ · ۷ · ۸ |
| [`admin-panel.md`](admin-panel.md) | سطوح Admin و API Settings/Test | ۶ · ۷ |
| [`onboarding.md`](onboarding.md) | پورت‌ها و hosts | ۱ · ۵ |
| [`asterisk-manager-reload.md`](asterisk-manager-reload.md) | Reload / restart پس از `manager.conf` | ۲ · ۸ |
| `.cursor/skills/asterisk-ami/SKILL.md` | پروتکل Action/Event | ۰ · ۲ |
| `karavi.doc/Learn/voiping_ami_programing_tutorial.pdf` | آموزش عمیق AMI | همه |

---

## EN — expanded section notes (each ≥100 words)

### Overview
Ntk.Asterisk splits AMI responsibility across Asterisk (manager user, bind, permit, privileges), Admin Settings (editable non-secret fields plus secret write-only), and WebApi AmiSession (login, keepalive, originate). Saving Settings writes `App_Data/asterisk-settings.json`; GET never returns the secret; blank secret keeps the previous value. Test Connection force-reconnects using persisted values and surfaces host, port, version, and last error. Connection, Monitor, and CallJobs all consume the same effective options, so a green login with wrong ChannelTech or trunk still breaks jobs. Follow sections 2–9 in order and use the troubleshooting matrix before changing multiple fields at once.

### Server manager.conf
Enable manager with `enabled=yes`, align `port` with Admin Port (default 5038), and choose a safe `bindaddr`. Create a dedicated user whose section name is Admin Username and whose `secret` is Admin Secret. Prefer deny-all then permit only the WebApi host. Grant `read=system,call` and `write=system,call,originate` for Wave-1 monitor, hangup, and originate. Reload manager and verify with `manager show user`. Validate TCP from the WebApi host before trusting the UI. Map every Admin field to these keys so Host/Port/User/Secret stay consistent with FreePBX or raw Asterisk.

### FreePBX / Issabel
Use Asterisk Manager Users in the GUI to create the same username, password, deny/permit, and read/write classes, then Apply Config and confirm via CLI. Do not hand-edit generated `_additional` dialplan files; use `extensions_custom.conf` when custom routing is required per the dialplan contract. If the GUI user exists but login fails, revisit network bind/permit rather than recreating secrets blindly. After panel setup, enter identical values in Admin Settings, save, run Test Connection, then verify Connection and Monitor pages.

### Trunk and ChannelTech
Originate channel strings depend on ChannelTech and DefaultTrunk. For **PJSIP**, outbound must be `PJSIP/{number}@{trunkEndpoint}` — the chan_sip style `PJSIP/trunk/number` yields OriginateResponse Failure reason=0. DefaultTrunk must equal the outbound trunk endpoint name from FreePBX Trunks or `pjsip show endpoints`. TrunkPeerFilter is only for Monitor classification; a bad regex looks like “no trunks” while AMI is healthy. CallerId and timeout affect Originate defaults and must match carrier policy. Validate jobs on a lab after a successful connection test so login success is not mistaken for dialing success.

### Admin Settings and verification
Fill every Settings field from the server truth tables above, save to persist App_Data, then Test Connection. Check Connection status, Monitor lists, and optionally a lab ExtToExt job. Treat Configured without Connected as a network or Asterisk-down problem. Keep secrets out of logs and git. Use the checklist and troubleshooting sections as the operational close-out for production handoff.
