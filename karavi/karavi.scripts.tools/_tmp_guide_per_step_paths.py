# -*- coding: utf-8 -*-
import json
from pathlib import Path

root = Path(r"D:\SourceKaravi\GitHub\Ntk.Asterisk")

fa = {
    "GUIDE_PATHS_TITLE": "آدرس فایل‌ها و URLها (کپی‌پذیر)",
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

ترتیب پیشنهادی:
فعال‌سازی Manager → ساخت کاربر → پنل (در صورت نیاز) → ترانک → شبکه → ذخیره و تست → عیب‌یابی

آدرس فایل و URL مربوط به هر مرحله، داخل همان مرحله (بلوک کپی‌پذیر) آمده است.""",
    "GUIDE_S1_BODY": """روی سرور Asterisk بخش [general] را طوری تنظیم کنید که Manager روشن شود.

enabled = yes
port = 5038
bindaddr امن (هم‌ماشین: 127.0.0.1)

پورت باید عین فیلد Port در Settings باشد.
اگر پورت را عوض کنید بدون به‌روزرسانی UI، WebApi به هدف اشتباه وصل می‌شود.

پس از ذخیره فایل روی سرور:
manager reload
سپس manager show settings

بدون این مرحله، ساخت کاربر یا پر کردن فرم بی‌فایده است.

مرتبط با:
Host/Port · مرحله شبکه · Connection refused""",
    "GUIDE_S1_PATHS": """/etc/asterisk/manager.conf
/etc/asterisk/manager.d/*.conf
http://localhost:5314/settings
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.build.config/local-dev-ports.json
tcp://ASTERISK_HOST:5038""",
    "GUIDE_S2_BODY": """یک بخش کاربر جدا مثل ntk-ami بسازید.

نام بخش = Username در Settings
secret قوی = رمز فرم (هرگز در git/لاگ/API)

الگوی امن:
deny همه
permit فقط IP سرور WebApi

کلاس‌های Wave-1:
read = system,call
write = system,call,originate

کمبود originate → Login سبز ولی جاب قرمز.
بعد از اتصال موفق یک Originate آزمایشی روی lab ببینید.

پس از ویرایش:
manager reload
manager show user

مرتبط با:
پنل FreePBX · Username/Secret · Auth / Originate denied""",
    "GUIDE_S2_PATHS": """/etc/asterisk/manager.conf
/etc/asterisk/manager.d/*.conf
http://localhost:5314/settings
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings
karavi/karavi.doc/asterisk-dialplan-contract.md
karavi/karavi.doc/ami-connection-setup-guide.md""",
    "GUIDE_S3_BODY": """اگر از GUI استفاده می‌کنید، همان مفاهیم کاربر Manager و permit و کلاس‌ها برقرار است.

FreePBX: Settings → Asterisk Manager Users
Issabel/Elastix: Tools مربوط به Asterisk Manager Users

Username/Password عین این فرم، Permit فقط IP وب‌ای‌پی،
Read حداقل system,call و Write حداقل system,call,originate
سپس Apply Config و تأیید با manager show user

فایل‌های additional را دستی خراب نکنید.
اگر کاربر در پنل هست ولی Login شکست می‌خورد به شبکه/bindaddr برگردید.

مرتبط با:
مراحل ۱ و ۲ و ۵ و ۶ · تست اتصال""",
    "GUIDE_S3_PATHS": """https://ASTERISK_IP/admin/
https://ASTERISK_IP/
/etc/asterisk/extensions_custom.conf
http://localhost:5314/settings
POST http://localhost:5310/api/v1/Config/ActionTestConnection
karavi/karavi.doc/ami-connection-setup-guide.md""",
    "GUIDE_S4_BODY": """پس از Login موفق، Originate به ChannelTech و نام ترانک وابسته است.

Channel tech: PJSIP (مدرن) یا SIP (قدیمی)
Default trunk: عین نام endpoint ترانک خروجی
فیلتر ترانک: regex برای Monitor
CallerId و Timeout: پیش‌فرض Originate/Dial

ناهمخوانی Tech → خطای کانال حتی با تست سبز.
فیلتر غلط → لیست ترانک خالی با AMI سالم.

مرتبط با:
CallJobs · Monitor · No such endpoint""",
    "GUIDE_S4_PATHS": """http://localhost:5314/settings
http://localhost:5314/monitor
http://localhost:5314/jobs
http://localhost:5310/api/v1/Asterisk/Peers/GetList
http://localhost:5310/api/v1/Asterisk/Trunks/GetList
http://localhost:5310/api/v1/CallJobs/GetList
karavi/karavi.doc/asterisk-dialplan-contract.md""",
    "GUIDE_S5_BODY": """حتی با کاربر و رمز درست، بدون مسیر TCP تست اتصال refused/timeout می‌شود
و Connection ممکن است Connected=false بماند در حالی که Settings Configured است.

اگر bindaddr فقط لوکال است WebApi باید همان ماشین باشد.
وگرنه bind LAN + permit فقط IP وب‌ای‌پی.
فایروال فقط همان IP را به ۵۰۳۸ راه دهد.

قبل از Save Secret، از ماشین WebApi مسیر را با nc یا Test-NetConnection ثابت کنید.

مرتبط با:
Host/Port · general · Connection refused""",
    "GUIDE_S5_PATHS": """tcp://ASTERISK_HOST:5038
tcp://127.0.0.1:5038
http://localhost:5310/health
http://localhost:5314/settings
http://localhost:5314/connection
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
/etc/asterisk/manager.conf""",
    "GUIDE_S6_BODY": """فیلدهای فرم را از حقیقت سرور پر کنید؛ Secret خالی = حفظ رمز قبلی.

۱) ذخیره تنظیمات → App_Data نوشته می‌شود (gitignore)
۲) تست اتصال → force reconnect و نمایش وضعیت/نسخه/خطا
۳) تأیید Connection / Monitor / Jobs روی lab

اگر Login سبز و Originate قرمز است به کلاس write و نام ترانک برگردید.

مرتبط با:
همه مراحل قبلی · چک‌لیست سند کامل""",
    "GUIDE_S6_PATHS": """http://localhost:5314/settings
GET http://localhost:5310/api/v1/Config/GetSiteSettings
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings
POST http://localhost:5310/api/v1/Config/ActionTestConnection
http://localhost:5310/hubs/asterisk
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Production.json
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs""",
    "GUIDE_S7_BODY": """هر بار فقط یک ریشه را درست کنید و دوباره تست اتصال بزنید.
Secret را در لاگ و گفتگو چاپ نکنید.

Authentication failed → رمز/کاربر/reload
Connection refused → فایروال/bind/Host
Originate denied → write=originate یا Tech/Trunk
No such endpoint → نام ترانک/ChannelTech
Secret missing → ذخیره/Clear
Connected=false با Configured=true → سرویس/شبکه

جزئیات جدولی در سند کامل آمده است.""",
    "GUIDE_S7_PATHS": """/etc/asterisk/manager.conf
http://localhost:5314/settings
http://localhost:5314/connection
tcp://ASTERISK_HOST:5038
http://localhost:5310/health
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
POST http://localhost:5310/api/v1/Config/ActionTestConnection
karavi/karavi.doc/asterisk-dialplan-contract.md
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json""",
    "GUIDE_S8_BODY": """AMI سطح کنترل ادمین است.

permit فقط IP وب‌ای‌پی · Secret قوی
هرگز در chat / plan / history / commit / پاسخ API

در تولید: vault یا overlay محیطی — نه appsettings پایه.
App_Data را backup امن کنید و در git نگذارید.

پس از مراحل ۱ تا ۷ چک‌لیست انتهابه‌انتها در سند کامل را تیک بزنید.""",
    "GUIDE_S8_PATHS": """src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
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
http://localhost:5310/api/v1/Config/ActionTestConnection""",
}

en = {
    "GUIDE_PATHS_TITLE": "File paths and URLs (copy-ready)",
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

Recommended order:
Enable Manager → create user → panel (if needed) → trunk → network → save and test → troubleshoot

File paths and URLs for each lesson are inside that same lesson (copy-ready block).""",
    "GUIDE_S1_BODY": """On the Asterisk server configure [general] so Manager is enabled.

enabled = yes
port = 5038
safe bindaddr (same host: 127.0.0.1)

Port must match the Port field in Settings.
Changing the port without updating the UI makes WebApi hit the wrong target.

After saving on the server:
manager reload
then manager show settings

Without this step, creating a user or filling the form cannot work.

Related to:
Host/Port · network step · Connection refused""",
    "GUIDE_S1_PATHS": """/etc/asterisk/manager.conf
/etc/asterisk/manager.d/*.conf
http://localhost:5314/settings
karavi/karavi.doc/ami-connection-setup-guide.md
karavi/karavi.build.config/local-dev-ports.json
tcp://ASTERISK_HOST:5038""",
    "GUIDE_S2_BODY": """Create a dedicated Manager section such as ntk-ami.

Section name = Settings Username
strong secret = form password (never in git/logs/API)

Secure pattern:
deny all
permit only the WebApi host IP

Wave-1 classes:
read = system,call
write = system,call,originate

Missing originate → green Login but red jobs.
After connect, validate a lab Originate.

After editing:
manager reload
manager show user

Related to:
FreePBX panel · Username/Secret · Auth / Originate denied""",
    "GUIDE_S2_PATHS": """/etc/asterisk/manager.conf
/etc/asterisk/manager.d/*.conf
http://localhost:5314/settings
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings
karavi/karavi.doc/asterisk-dialplan-contract.md
karavi/karavi.doc/ami-connection-setup-guide.md""",
    "GUIDE_S3_BODY": """When using the GUI, the same Manager user, permit, and class concepts apply.

FreePBX: Settings → Asterisk Manager Users
Issabel/Elastix: Tools for Asterisk Manager Users

Same Username/Password as this form, Permit only WebApi IP,
Read at least system,call and Write at least system,call,originate
then Apply Config and verify with manager show user

Do not hand-edit generated additional files.
If the GUI user exists but Login fails, revisit network/bindaddr.

Related to:
steps 1, 2, 5, and 6 · Test Connection""",
    "GUIDE_S3_PATHS": """https://ASTERISK_IP/admin/
https://ASTERISK_IP/
/etc/asterisk/extensions_custom.conf
http://localhost:5314/settings
POST http://localhost:5310/api/v1/Config/ActionTestConnection
karavi/karavi.doc/ami-connection-setup-guide.md""",
    "GUIDE_S4_BODY": """After a successful Login, Originate depends on ChannelTech and trunk naming.

Channel tech: PJSIP (modern) or SIP (legacy)
Default trunk: exact outbound trunk endpoint name
Trunk filter: regex for Monitor
CallerId and Timeout: Originate/Dial defaults

Tech mismatch → channel error even with a green test.
Bad filter → empty trunk list with healthy AMI.

Related to:
CallJobs · Monitor · No such endpoint""",
    "GUIDE_S4_PATHS": """http://localhost:5314/settings
http://localhost:5314/monitor
http://localhost:5314/jobs
http://localhost:5310/api/v1/Asterisk/Peers/GetList
http://localhost:5310/api/v1/Asterisk/Trunks/GetList
http://localhost:5310/api/v1/CallJobs/GetList
karavi/karavi.doc/asterisk-dialplan-contract.md""",
    "GUIDE_S5_BODY": """Even with a correct user and secret, without a TCP path Test Connection ends refused/timeout
and Connection may stay Connected=false while Settings shows Configured.

If bindaddr is localhost only, WebApi must share that machine.
Otherwise LAN bind + permit only the WebApi IP.
Firewall must allow only that IP to 5038.

Before saving Secret, prove the path from the WebApi host with nc or Test-NetConnection.

Related to:
Host/Port · general · Connection refused""",
    "GUIDE_S5_PATHS": """tcp://ASTERISK_HOST:5038
tcp://127.0.0.1:5038
http://localhost:5310/health
http://localhost:5314/settings
http://localhost:5314/connection
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
/etc/asterisk/manager.conf""",
    "GUIDE_S6_BODY": """Fill the form from server truth; blank Secret keeps the previous value.

1) Save settings → App_Data written (gitignored)
2) Test Connection → force reconnect and show status/version/error
3) Verify Connection / Monitor / Jobs on a lab

If Login is green but Originate is red, revisit write classes and trunk names.

Related to:
all prior steps · full document checklist""",
    "GUIDE_S6_PATHS": """http://localhost:5314/settings
GET http://localhost:5310/api/v1/Config/GetSiteSettings
POST http://localhost:5310/api/v1/Config/UpdateSiteSettings
POST http://localhost:5310/api/v1/Config/ActionTestConnection
http://localhost:5310/hubs/asterisk
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Development.json
src/DotNet/Ntk.Asterisk.WebApi/appsettings.Production.json
http://localhost:5314/connection
http://localhost:5314/monitor
http://localhost:5314/jobs""",
    "GUIDE_S7_BODY": """Fix one root cause at a time and re-run Test Connection.
Never print the secret in logs or chat.

Authentication failed → secret/user/reload
Connection refused → firewall/bind/Host
Originate denied → write=originate or Tech/Trunk
No such endpoint → trunk name/ChannelTech
Secret missing → save/Clear
Connected=false with Configured=true → service/network

Full matrix is in the repository document.""",
    "GUIDE_S7_PATHS": """/etc/asterisk/manager.conf
http://localhost:5314/settings
http://localhost:5314/connection
tcp://ASTERISK_HOST:5038
http://localhost:5310/health
GET http://localhost:5310/api/v1/Asterisk/Connection/GetStatus
POST http://localhost:5310/api/v1/Config/ActionTestConnection
karavi/karavi.doc/asterisk-dialplan-contract.md
src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json""",
    "GUIDE_S8_BODY": """AMI is an admin control plane.

Permit only the WebApi IP · strong Secret
Never in chat / plan / history / commit / API responses

In production: vault or environment overlay — not base appsettings.
Back up App_Data securely and keep it out of git.

After steps 1–7, tick the end-to-end checklist in the full document.""",
    "GUIDE_S8_PATHS": """src/DotNet/Ntk.Asterisk.WebApi/App_Data/asterisk-settings.json
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
http://localhost:5310/api/v1/Config/ActionTestConnection""",
}

# remove obsolete global body if present
for rel, updates in [
    ("src/Angular/AdminPanel/public/assets/i18n/fa.json", fa),
    ("src/Angular/AdminPanel/public/assets/i18n/en.json", en),
]:
    path = root / rel
    data = json.loads(path.read_text(encoding="utf-8"))
    data["SETTINGS"].update(updates)
    data["SETTINGS"].pop("GUIDE_PATHS_BODY", None)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("updated", path)
