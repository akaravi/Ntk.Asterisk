# AGI reference (VoiPing tutorial digest)

## Libraries cited in tutorial

| Name | Language | Notes |
|------|----------|-------|
| PHPAGI | PHP | FreePBX path `/var/lib/asterisk/agi-bin/phpagi.php` |
| AsterNET | .NET | AMI + FastAGI + Async AGI — ancestor of this repo |
| Asterisk-Java | Java | AMI/FastAGI |
| Pyst2 | Python | |
| Adhearsion | Ruby | |
| Ding-dong | Node | |
| Nanoagi | C++ | |

## PHP first script (tutorial shape)

```php
#!/usr/bin/php -q
<?php
require 'phpagi.php';
$agi = new AGI();
$agi->answer();
$agi->say_digits("123");
$agi->exec("wait", 3);
$agi->stream_file("hello-world");
$agi->hangup();
```

Shebang required for Standard AGI executables.

## stream_file / record_file / get_data

- Filenames **without** extension.
- Sounds root: `/var/lib/asterisk/sounds`.
- Formats: wav/gsm (mp3 sometimes).
- Escape digits cancel playback/recording.
- `get_data`: default timeouts; `#` submits; `#` itself hard to capture as digit.

## DB patterns

- Direct mysqli or FreePBX `sql.php` + `AGIDB` with `AMPDBUSER` / `AMPDBPASS` / `AMPDBNAME` channel vars.
- Parameterize queries — never concatenate untrusted DTMF/callerid into SQL.

## Queue option `c`

Continues dialplan after agent hangup so survey AGI can run.

## EAGI vs FastAGI

- **EAGI:** audio stream access for live analysis — advanced.
- **FastAGI:** offload CPU; isolate code on another host; TCP.

## Mapping to Ntk.AsterNet.AMI

When implementing:

1. Prefer command classes under `FastAGI/Command/` mirroring AGI ops.
2. Mirror sample Console/WinForm patterns for host lifecycle.
3. Document dialplan `AGI(agi://...)` contract in onboarding or sample README — not secrets.
