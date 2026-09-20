# نام‌گذاری اسکریپت

```
karavi.scripts.command/{domain}.{action}.ps1   ← دستور صریح کاربر
karavi.scripts.tools/{domain}.{helper}.ps1     ← helper داخلی
```

| نمونه | نقش |
|---|---|
| `workspace.clean.ps1` | clean |
| `workspace.paths.ps1` | Get-KaraviPaths |
| `history.write.ps1` | append history |
| `verify.karavi-structure.ps1` | ساختار karavi |

دستور جدید با trigger کاربر → `command` · verify/build تک‌مرحله‌ای → `tools`.
