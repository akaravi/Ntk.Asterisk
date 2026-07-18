# Karavi.006 — Queue Panel Q11 hide / show / rename

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T13:05:00+03:30`

**Goal:** Close FEATURES Q11 — QPanel-parity queue visibility (hide list, show whitelist, display rename) on effective Asterisk server settings; Admin Settings UI; AMI actions keep `realName`.

**Architecture:** Config on `AsteriskServerConfig` / `AsteriskOptions` · filter+rename in `QueueMonitorService.ApplyDisplay` · DTO `QueueDto.RealName` · Settings form + i18n.

**Hosts:** webapi `:5310` · admin-panel `:5314`

**Skills:** asterisk-voip-stack · asterisk-ami

---

## Part 1 — Backend config + ApplyDisplay

### Request
Persist `QueueHideList` / `QueueShowList` / `QueueRenameMap`; filter GetList / GetOne; set `Name`=display, `RealName`=AMI.

### Result 1
- **status:** complete
- **verification:** `AsteriskOptions` · `AsteriskServerConfig` · DTOs · `AsteriskSettingsService.ApplyQueueDisplayFields` · `QueueMonitorService.ApplyDisplay`
- **hosts:** webapi

---

## Part 2 — Admin Settings + Queues realName + i18n

### Request
Settings section for three fields; pause/unpause use `realName ?? name`; fa/en keys.

### Result 2
- **status:** complete
- **verification:** `settings-page` form/HTML · `queues-page.amiQueueName` · `SETTINGS.SECTION_QUEUES*` fa/en · models SiteSettings/AsteriskServer/UpdateRequest
- **hosts:** admin-panel

---

## Part 3 — Docs + FEATURES + SHIP-READY verify

### Request
Update FEATURES Q11 done · queue-panel-ami.md · history · build green.

### Result 3
- **status:** complete
- **verification:** see history.2026-07-18.md · FEATURES Q11 · queue-panel-ami.md
- **hosts:** docs · webapi · admin-panel
