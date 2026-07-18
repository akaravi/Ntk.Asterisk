# Karavi.007 — Queue ACL (Q13) + historical snapshots (Q14)

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T13:15:00+03:30`

**Goal:** Port QPanel multi-user queue ACL and useful historical stats into WebApi/Admin — AMI-only; no Flask/SQL queue_log rewrite.

**Architecture:** `App_Data/queue-acl-users.json` + opaque session tokens · filter Queues REST + SignalR per connection · snapshot ring from `QueueMonitorService` → `App_Data/queue-stats-snapshots.json`.

**Hosts:** webapi `:5310` · admin-panel `:5314`

**Skills:** asterisk-voip-stack · asterisk-ami

---

## Part 1 — Queue ACL store + Auth + filter

### Request
Users CRUD · PBKDF2 · login/logout/me · filter GetList/GetOne/actions · SignalR per-client filter.

### Result 1
- **status:** complete
- **verification:** `QueueAclStore` · `QueueAclSessionService` · `AuthController` · `QueueAclUsersController` · Queues + Hub wiring
- **hosts:** webapi

---

## Part 2 — Queue stats snapshots

### Request
Periodic samples from live queue cache · GetList with from/to/queue · retention cap.

### Result 2
- **status:** complete
- **verification:** `QueueStatsSnapshotStore` · hosted sampler · `QueueStatsController`
- **hosts:** webapi

---

## Part 3 — Admin UI + i18n + docs

### Request
Login · ACL users page · stats page/tab · fa/en · FEATURES Q13/Q14 done · history.

### Result 3
- **status:** complete
- **verification:** Admin routes + i18n · FEATURES · queue-panel-ami.md · history
- **hosts:** admin-panel · docs
