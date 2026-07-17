# History 2026-07-18

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
