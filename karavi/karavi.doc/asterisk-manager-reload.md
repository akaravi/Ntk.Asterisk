# Reload پس از تغییر `/etc/asterisk/manager.conf`

**Audience:** مدیر سیستم / ادمین PBX · **Locale:** fa-IR (اصطلاحات فنی EN)  
**Skills:** `asterisk-voip-stack` · `asterisk-ami`  
**مرتبط:** [`ami-connection-setup-guide.md`](ami-connection-setup-guide.md) · [`asterisk-dialplan-contract.md`](asterisk-dialplan-contract.md)

پس از ویرایش `manager.conf` (یا فایل‌های include زیر `manager.d/`) باید Manager را reload کنید تا یوزر، `secret`، `permit`/`deny` و کلاس‌های `read`/`write` اعمال شوند. این سند فقط دستورات عملیاتی reload / verify / restart است.

---

## ۱) Reload امن (معمولاً کافی است)

از shell روی همان سرور Asterisk:

```bash
asterisk -rx "manager reload"
```

یا داخل CLI ستاره‌ای (`asterisk -r`):

```text
manager reload
```

جایگزین‌های معادل:

```bash
asterisk -rx "module reload res_manager.so"
```

```text
module reload res_manager.so
```

---

## ۲) Verify بعد از reload

```bash
asterisk -rx "manager show settings"
asterisk -rx "manager show users"
asterisk -rx "manager show user YOUR_USERNAME"
asterisk -rx "manager show connected"
```

اگر یوزر جدید ساخته‌اید یا `secret` / `permit` / کلاس‌های `read`/`write` را عوض کرده‌اید:

1. خروجی `manager show user` را با مقادیر Settings در AdminPanel یکی کنید.
2. از ماشین WebApi مسیر TCP را چک کنید: `nc -vz ASTERISK_IP 5038`
3. در AdminPanel → Settings → **تست اتصال** بزنید.

جزئیات پیکربندی و عیب‌یابی: [`ami-connection-setup-guide.md`](ami-connection-setup-guide.md) بخش‌های ۲، ۵، ۶، ۸.

---

## ۳) چه زمانی reload کافی نیست؟ (نیاز به restart)

تغییرات زیر در بخش `[general]` معمولاً با `manager reload` کامل اعمال نمی‌شوند و نیاز به **restart سرویس Asterisk** دارند:

| تغییر در `[general]` | توصیه |
|----------------------|--------|
| `enabled` | restart |
| `port` | restart |
| `bindaddr` | restart |
| بعضی گزینه‌های HTTP/AJAM (`webenabled` و مشابه) | اغلب restart |

Restart (بسته به distro):

```bash
systemctl restart asterisk
# Issabel / FreePBX / Elastix: معادل سرویس همان توزیع
```

**هشدار:** restart همه کانال‌های فعال را قطع می‌کند. در production پنجره نگهداری بگذارید.

پس از restart دوباره بخش ۲ (Verify) را اجرا کنید و در صورت تغییر `port`/`bindaddr`، فیلد Host/Port در AdminPanel و فایروال را هم‌تراز کنید.

---

## ۴) اثر روی کلاینت‌های AMI (`Ntk.AsterNet.AMI` / WebApi)

- بعد از reload، sessionهای باز ممکن است قطع شوند → کلاینت باید reconnect + `Login` دوباره بزند.
- اگر فقط `secret` یا ACL یوزر عوض شده، sessionهای قدیمی با credential قبلی معمولاً دیگر معتبر نیستند.
- قبل از تغییر production: `manager show connected` را ببینید.
- WebApi با `AmiSession` پس از تغییر Settings معمولاً force-reconnect می‌کند؛ اگر فقط روی سرور Asterisk reload کردید، از AdminPanel **تست اتصال** یا restart WebApi (در صورت Auto-connect) وضعیت را تازه کنید.

---

## ۵) اگر reload خطا داد

```bash
tail -n 50 /var/log/asterisk/full
asterisk -rx "core show settings"
```

خطاهای رایج:

| نشانه | علت محتمل | اقدام |
|-------|-----------|--------|
| Syntax error در لاگ | اشتباه در `manager.conf` | فایل را اصلاح کنید، دوباره reload |
| کاربر در `manager show users` نیست | فایل اشتباه / include نشده / reload نشده | مسیر فایل و `manager.d` را چک کنید |
| Authentication failed بعد از تغییر secret | کلاینت هنوز secret قدیمی دارد | Settings را به‌روز و تست اتصال بزنید |
| Connection refused | `enabled=no` یا bind/port یا فایروال | بخش ۳ + راهنمای AMI بخش ۵ |

Secret را هرگز در chat، history، plan یا git چاپ نکنید.

---

## ۶) چک‌لیست یک‌خطی (کپی‌پذیر)

```bash
# ویرایش manager.conf تمام شد →
asterisk -rx "manager reload"
asterisk -rx "manager show settings"
asterisk -rx "manager show users"
asterisk -rx "manager show user YOUR_USERNAME"
# از ماشین WebApi:
nc -vz ASTERISK_IP 5038
# سپس AdminPanel → Settings → تست اتصال
```

اگر `port` / `bindaddr` / `enabled` عوض شده:

```bash
systemctl restart asterisk
# سپس همان verify بالا
```

---

## EN — quick reference

After editing `/etc/asterisk/manager.conf`, run `asterisk -rx "manager reload"` (or `module reload res_manager.so`). Verify with `manager show settings|users|user`. Changes to `[general]` `enabled`, `port`, or `bindaddr` usually require `systemctl restart asterisk`. AMI clients may drop on reload and must re-Login; align AdminPanel Host/Port/Secret and run Test Connection. Never log or commit Manager secrets.
