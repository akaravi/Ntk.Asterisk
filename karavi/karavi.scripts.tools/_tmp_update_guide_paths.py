# -*- coding: utf-8 -*-
import json
from pathlib import Path

fa_updates = {
    "GUIDE_PATHS_TITLE": "آدرس فایل‌ها و URLها (کپی‌پذیر)",
    "GUIDE_PATHS_BODY": """آدرس‌ها و مسیرهای canonical (توسعهٔ محلی):

URL پنل ادمین:
http://localhost:5314
http://localhost:5314/settings
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs

URL وب‌ای‌پی:
http://localhost:5310
http://localhost:5310/health
http://localhost:5310/hubs/asterisk
http://localhost:5310/api/v1/Config/GetSiteSettings
http://localhost:5310/api/v1/Config/UpdateSiteSettings
http://localhost:5310/api/v1/Config/ActionTestConnection
http://localhost:5310/api/v1/Asterisk/Connection/GetStatus

فایل‌های مخزن:
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.doc/asterisk-dialplan-contract.md
karavi/karavi.doc/admin-panel.md
karavi/karavi.build.config/local-dev-ports.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Production.json
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
src/Angular/AdminPanel/src/environments/environment.ts

فایل‌های سرور Asterisk:
/etc/asterisk/manager.conf
/etc/asterisk/manager.d/*.conf
/etc/asterisk/extensions_custom.conf

AMI شبکه:
tcp://ASTERISK_HOST:5038
""",
    "GUIDE_INTRO": """این راهنما همه لایه‌های لازم برای اتصال AMI را پوشش می‌دهد.

• سرور Asterisk یا پنل FreePBX / Issabel / Elastix
• کاربر Manager با حداقل دسترسی
• هم‌ترازی ChannelTech و ترانک
• شبکه و فایروال
• فرم Settings، تست اتصال، صفحه اتصال، مانیتور و جاب‌ها

قبل از ذخیره فرم:
Manager روی سرور باید فعال باشد و مسیر TCP از WebApi به پورت ۵۰۳۸ باز باشد.

رمز هرگز در پاسخ API برنمی‌گردد.
فیلد خالی یعنی حفظ رمز قبلی.

سند کامل:
karavi/karavi.doc/ami-connection-setup-guide.md

پورت‌ها (local-dev-ports.json):
WebApi = http://localhost:5310
Admin = http://localhost:5314
AMI خارجی = tcp host:5038

ترتیب پیشنهادی:
فعال‌سازی Manager → ساخت کاربر → پنل (در صورت نیاز) → ترانک → شبکه → ذخیره و تست → عیب‌یابی

لیست کامل URL و مسیر فایل در بلوک «آدرس فایل‌ها و URLها» همین صفحه آمده است.""",
    "GUIDE_S1_BODY": """روی سرور Asterisk این فایل‌ها را ویرایش کنید:

/etc/asterisk/manager.conf
یا
/etc/asterisk/manager.d/*.conf

در بخش [general]:
enabled = yes
port = 5038
bindaddr امن

پورت ۵۰۳۸ باید عین فیلد Port در Settings باشد.

Settings UI:
http://localhost:5314/settings

پس از ذخیره روی سرور:
asterisk -rx "manager reload"
asterisk -rx "manager show settings"

مرتبط با:
Host/Port · مرحله شبکه · Connection refused

بدون این مرحله، ساخت کاربر یا پر کردن فرم بی‌فایده است.

مرجع مخزن:
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.build.config/local-dev-ports.json""",
    "GUIDE_S2_BODY": """کاربر Manager را در همان فایل بسازید:

/etc/asterisk/manager.conf
(یا manager.d)

نام بخش = Username در Settings
secret = رمز فرم

الگوی امن:
deny همه
permit فقط IP سرور WebApi

کلاس‌های Wave-1:
read = system,call
write = system,call,originate

CLI تأیید:
asterisk -rx "manager show user USERNAME"

Settings برای وارد کردن همان مقادیر:
http://localhost:5314/settings

API ذخیره:
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings

مرتبط با:
FreePBX · Username/Secret · dialplan contract · Auth / Originate denied

قرارداد:
karavi/karavi.doc/asterisk-dialplan-contract.md""",
    "GUIDE_S3_BODY": """FreePBX معمولاً از URL پنل ادمین باز می‌شود:

https://ASTERISK_IP/admin/
سپس Settings → Asterisk Manager Users

Issabel / Elastix:
https://ASTERISK_IP/
(مسیر Tools مربوط به Asterisk Manager Users)

پس از Apply، همان Username/Secret را در این آدرس وارد کنید:
http://localhost:5314/settings

تأیید CLI روی سرور:
asterisk -rx "manager show user USERNAME"

فایل‌های تولیدشده additional را دستی خراب نکنید.
dialplan سفارشی فقط:
/etc/asterisk/extensions_custom.conf

اگر Login شکست خورد ولی کاربر در GUI هست:
شبکه و bindaddr را چک کنید.

مرجع:
karavi/karavi.doc/ami-connection-setup-guide.md
http://localhost:5310/api/v1/Config/ActionTestConnection""",
    "GUIDE_S4_BODY": """Originate به ChannelTech و نام ترانک وابسته است.

فیلدها در:
http://localhost:5314/settings

Channel tech: PJSIP یا SIP
Default trunk: عین endpoint ترانک خروجی

تأیید روی سرور:
asterisk -rx "pjsip show endpoints"

قرارداد شکل کانال:
karavi/karavi.doc/asterisk-dialplan-contract.md

پس از اتصال، مانیتور و جاب‌ها:
http://localhost:5314/monitor
http://localhost:5314/jobs

APIهای مرتبط:
http://localhost:5310/api/v1/Asterisk/Peers/GetList
http://localhost:5310/api/v1/Asterisk/Trunks/GetList
http://localhost:5310/api/v1/CallJobs/GetList

فیلتر غلط → لیست ترانک خالی با AMI سالم.
ناهمخوانی Tech → خطای کانال حتی با تست سبز.""",
    "GUIDE_S5_BODY": """مسیر شبکه باید از ماشین WebApi به AMI باز باشد:

tcp://ASTERISK_HOST:5038

توسعهٔ هم‌ماشین معمولاً:
tcp://127.0.0.1:5038

Health وب‌ای‌پی (جدا از AMI):
http://localhost:5310/health

اگر bindaddr فقط 127.0.0.1 است، WebApi باید همان ماشین باشد.
وگرنه bind LAN + permit فقط IP WebApi.

فایروال فقط همان IP را به ۵۰۳۸ راه دهد.

قبل از Save Secret در UI:
از ماشین WebApi به ASTERISK_HOST:5038 با nc یا Test-NetConnection تست کنید.

Settings:
http://localhost:5314/settings

وضعیت اتصال پس از تست:
http://localhost:5314/connection
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus""",
    "GUIDE_S6_BODY": """فرم را اینجا پر و ذخیره کنید:

http://localhost:5314/settings

APIها:
GET  http://localhost:5310/api/v1/Config/GetSiteSettings
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings
POST http://localhost:5310/api/v1/Config/ActionTestConnection

فایل ذخیره‌شده روی دیسک WebApi:
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
(مسیر runtime: ContentRoot/App_Data/asterisk-settings.json)

seed اولیه از:
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
یا appsettings.Production.json

Hub زنده:
http://localhost:5310/hubs/asterisk

پس از تست موفق:
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs

Secret خالی = حفظ رمز قبلی.
فایل App_Data در git نیست.""",
    "GUIDE_S7_BODY": """Authentication failed
→ Username/Secret یا reload
→ /etc/asterisk/manager.conf
→ http://localhost:5314/settings

Connection refused / timeout
→ tcp://ASTERISK_HOST:5038
→ bindaddr / فایروال
→ http://localhost:5310/health فقط سلامت API است نه AMI

Originate denied
→ write=originate در manager.conf
→ karavi/karavi.doc/asterisk-dialplan-contract.md

No such endpoint
→ pjsip show endpoints
→ فیلدهای Channel tech / Default trunk در /settings

Secret missing
→ هنوز Save نشده یا Clear شده
→ App_Data/asterisk-settings.json

Connected=false با Configured=true
→ GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
→ http://localhost:5314/connection

تست مجدد:
POST http://localhost:5310/api/v1/Config/ActionTestConnection

Secret را در لاگ چاپ نکنید.""",
    "GUIDE_S8_BODY": """امنیت AMI سطح ادمین است.

permit فقط IP وب‌ای‌پی · Secret قوی
هرگز در chat / plan / history / commit / پاسخ API

فایل‌های حساس روی دیسک:
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Production.json
/etc/asterisk/manager.conf

مستندات مخزن (کپی‌پذیر):
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.doc/asterisk-dialplan-contract.md
karavi/karavi.doc/admin-panel.md
karavi/karavi.doc/onboarding.md
karavi/karavi.build.config/local-dev-ports.json
src/Angular/AdminPanel/src/environments/environment.ts

URLهای توسعه:
http://localhost:5314/settings
http://localhost:5310/api/v1/Config/GetSiteSettings
http://localhost:5310/api/v1/Config/ActionTestConnection

چک‌لیست انتهابه‌انتها در سند کامل را تیک بزنید.""",
}

en_updates = {
    "GUIDE_PATHS_TITLE": "File paths and URLs (copy-ready)",
    "GUIDE_PATHS_BODY": """Canonical file paths and URLs (local development):

Admin panel URLs:
http://localhost:5314
http://localhost:5314/settings
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs

WebApi URLs:
http://localhost:5310
http://localhost:5310/health
http://localhost:5310/hubs/asterisk
http://localhost:5310/api/v1/Config/GetSiteSettings
http://localhost:5310/api/v1/Config/UpdateSiteSettings
http://localhost:5310/api/v1/Config/ActionTestConnection
http://localhost:5310/api/v1/Asterisk/Connection/GetStatus

Repository files:
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.doc/asterisk-dialplan-contract.md
karavi/karavi.doc/admin-panel.md
karavi/karavi.build.config/local-dev-ports.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Production.json
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
src/Angular/AdminPanel/src/environments/environment.ts

Asterisk server files:
/etc/asterisk/manager.conf
/etc/asterisk/manager.d/*.conf
/etc/asterisk/extensions_custom.conf

AMI network:
tcp://ASTERISK_HOST:5038
""",
    "GUIDE_INTRO": """This guide covers every layer required for AMI connectivity.

• Asterisk server or FreePBX / Issabel / Elastix panels
• Least-privilege Manager user
• ChannelTech and trunk alignment
• Network and firewall
• Settings form, Test Connection, Connection, Monitor, Jobs

Before saving:
Manager must be enabled and TCP from WebApi to port 5038 must work.

Secret is never returned by the API.
Blank secret keeps the previous value.

Full document:
karavi/karavi.doc/ami-connection-setup-guide.md

Ports (local-dev-ports.json):
WebApi = http://localhost:5310
Admin = http://localhost:5314
External AMI = tcp host:5038

Recommended order:
Enable Manager → create user → panel (if needed) → trunk → network → save and test → troubleshoot

The full URL and file-path list is in the «File paths and URLs» block on this page.""",
    "GUIDE_S1_BODY": """Edit these files on the Asterisk server:

/etc/asterisk/manager.conf
or
/etc/asterisk/manager.d/*.conf

In [general]:
enabled = yes
port = 5038
safe bindaddr

Port 5038 must match the Port field in Settings.

Settings UI:
http://localhost:5314/settings

After saving on the server:
asterisk -rx "manager reload"
asterisk -rx "manager show settings"

Related to:
Host/Port · network step · Connection refused

Without this step, creating a user or filling the form cannot work.

Repo refs:
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.build.config/local-dev-ports.json""",
    "GUIDE_S2_BODY": """Create the Manager user in:

/etc/asterisk/manager.conf
(or manager.d)

Section name = Settings Username
secret = form password

Secure pattern:
deny all
permit only the WebApi host IP

Wave-1 classes:
read = system,call
write = system,call,originate

CLI verify:
asterisk -rx "manager show user USERNAME"

Enter the same values at:
http://localhost:5314/settings

Save API:
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings

Related to:
FreePBX · Username/Secret · dialplan contract · Auth / Originate denied

Contract:
karavi/karavi.doc/asterisk-dialplan-contract.md""",
    "GUIDE_S3_BODY": """FreePBX is usually opened at:

https://ASTERISK_IP/admin/
then Settings → Asterisk Manager Users

Issabel / Elastix:
https://ASTERISK_IP/
(Tools → Asterisk Manager Users)

After Apply, enter the same Username/Secret at:
http://localhost:5314/settings

CLI verify on the server:
asterisk -rx "manager show user USERNAME"

Do not hand-edit generated additional dialplan files.
Custom dialplan only:
/etc/asterisk/extensions_custom.conf

If Login fails but the GUI user exists:
check network and bindaddr.

Refs:
karavi/karavi.doc/ami-connection-setup-guide.md
http://localhost:5310/api/v1/Config/ActionTestConnection""",
    "GUIDE_S4_BODY": """Originate depends on ChannelTech and trunk naming.

Fields at:
http://localhost:5314/settings

Channel tech: PJSIP or SIP
Default trunk: exact outbound trunk endpoint name

Verify on server:
asterisk -rx "pjsip show endpoints"

Channel shape contract:
karavi/karavi.doc/asterisk-dialplan-contract.md

After connect, Monitor and Jobs:
http://localhost:5314/monitor
http://localhost:5314/jobs

Related APIs:
http://localhost:5310/api/v1/Asterisk/Peers/GetList
http://localhost:5310/api/v1/Asterisk/Trunks/GetList
http://localhost:5310/api/v1/CallJobs/GetList

Bad filter → empty trunk list with healthy AMI.
Tech mismatch → channel error even with a green test.""",
    "GUIDE_S5_BODY": """Network path must be open from the WebApi host to AMI:

tcp://ASTERISK_HOST:5038

Same-host development is usually:
tcp://127.0.0.1:5038

WebApi health (separate from AMI):
http://localhost:5310/health

If bindaddr is only 127.0.0.1, WebApi must share that machine.
Otherwise LAN bind + permit only the WebApi IP.

Firewall must allow only that IP to 5038.

Before saving Secret in the UI:
test ASTERISK_HOST:5038 from the WebApi host with nc or Test-NetConnection.

Settings:
http://localhost:5314/settings

Connection status after test:
http://localhost:5314/connection
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus""",
    "GUIDE_S6_BODY": """Fill and save the form here:

http://localhost:5314/settings

APIs:
GET  http://localhost:5310/api/v1/Config/GetSiteSettings
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings
POST http://localhost:5310/api/v1/Config/ActionTestConnection

Persisted file on the WebApi disk:
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
(runtime: ContentRoot/App_Data/asterisk-settings.json)

Initial seed from:
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
or appsettings.Production.json

Live hub:
http://localhost:5310/hubs/asterisk

After a successful test:
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs

Blank Secret keeps the previous value.
App_Data is not in git.""",
    "GUIDE_S7_BODY": """Authentication failed
→ Username/Secret or reload
→ /etc/asterisk/manager.conf
→ http://localhost:5314/settings

Connection refused / timeout
→ tcp://ASTERISK_HOST:5038
→ bindaddr / firewall
→ http://localhost:5310/health is API health only, not AMI

Originate denied
→ write=originate in manager.conf
→ karavi/karavi.doc/asterisk-dialplan-contract.md

No such endpoint
→ pjsip show endpoints
→ Channel tech / Default trunk on /settings

Secret missing
→ not saved yet or Cleared
→ App_Data/asterisk-settings.json

Connected=false with Configured=true
→ GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
→ http://localhost:5314/connection

Retest:
POST http://localhost:5310/api/v1/Config/ActionTestConnection

Never print the secret in logs.""",
    "GUIDE_S8_BODY": """AMI is an admin control plane.

Permit only the WebApi IP · strong Secret
Never in chat / plan / history / commit / API responses

Sensitive files on disk:
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Production.json
/etc/asterisk/manager.conf

Repository docs (copy-ready):
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.doc/asterisk-dialplan-contract.md
karavi/karavi.doc/admin-panel.md
karavi/karavi.doc/onboarding.md
karavi/karavi.build.config/local-dev-ports.json
src/Angular/AdminPanel/src/environments/environment.ts

Dev URLs:
http://localhost:5314/settings
http://localhost:5310/api/v1/Config/GetSiteSettings
http://localhost:5310/api/v1/Config/ActionTestConnection

Tick the end-to-end checklist in the full document.""",
}

root = Path(r"D:\SourceKaravi\GitHub\Ntk.Asterisk")
for rel, updates in [
    ("src/Angular/AdminPanel/public/assets/i18n/fa.json", fa_updates),
    ("src/Angular/AdminPanel/public/assets/i18n/en.json", en_updates),
]:
    path = root / rel
    data = json.loads(path.read_text(encoding="utf-8"))
    data["SETTINGS"].update(updates)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("updated", path)
