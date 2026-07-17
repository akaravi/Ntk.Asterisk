```json
{
  "metadata": {
    "title": "Karavi.001 — bootstrap Ntk.Asterisk",
    "updatedAt": "2026-07-17",
    "locale": "fa-IR",
    "projectId": "ntk-asterisk"
  },
  "part1": {
    "request": "راه‌اندازی karavi در مخزن Ntk.Asterisk از starter-kit canonical",
    "tasks": [
      "نصب install-karavi.ps1 از Prompts.Project.CMS/karavi",
      "تکمیل onboarding.md برای AMI/ARI libraries",
      "تنظیم build/deploy/publish configs (بدون SPA/Flutter)",
      "verify.karavi-structure.ps1",
      "ثبت history روزانه"
    ],
    "scope": "karavi/ + .gitignore karavi append",
    "constraints": [
      "no import from other project karavi trees",
      "starter-kit only",
      "no secrets in commit"
    ]
  },
  "result1": {
    "status": "complete",
    "verification": "install-karavi.ps1 OK; productId=ntk-asterisk; verify.karavi-structure.ps1 OK; gitignore karavi block present",
    "hosts": [
      "ami-lib",
      "ari-lib",
      "console-ami",
      "console-ari",
      "winform-ami",
      "winform-ari"
    ],
    "notes": [
      "Delivery model: NuGet packages, not HTTP hosts",
      "Asterisk AMI/ARI endpoints are external dependencies"
    ]
  },
  "part2": {
    "request": "ایجاد project skills از tutorials Dialplan/AGI/AMI/CallFile برای استفاده اجباری در تمام بخش‌های بعدی",
    "tasks": [
      "خواندن voiping_* tutorial PDFs در karavi.doc/Learn",
      "ساخت asterisk-voip-stack + چهار domain skill زیر .cursor/skills",
      "rule همیشه فعال asterisk-voip-skills.mdc",
      "به‌روزرسانی onboarding و README و history"
    ],
    "scope": ".cursor/skills/ · .cursor/rules/ · karavi.doc · karavi README",
    "constraints": [
      "skills مبتنی بر محتوای tutorialهای VoiPing.ir",
      "هم‌تراز با Ntk.AsterNet.AMI (AMI+FastAGI) و Ntk.AsterNet.ARI",
      "بدون secret در skill/docs"
    ]
  },
  "result2": {
    "status": "complete",
    "verification": "five skills + reference.md present; alwaysApply rule; onboarding section 8; README skills pointer",
    "hosts": [
      "ami-lib",
      "ari-lib"
    ],
    "notes": [
      "Mandatory entry: asterisk-voip-stack before any VoIP work",
      "Domain skills: dialplan, agi, ami, callfile"
    ]
  }
}
```
