# Karavi.010 — Panel-wide Queue ACL auth gate

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T13:25:00+03:30`

**Goal:** Close FEATURES `gap-auth` — when RequireLogin is active, Admin Panel + protected APIs require Queue ACL session (no separate JWT product).

**Architecture:** `QueueAclGateMiddleware` · Angular `authGateGuard` / `adminRoleGuard` · Shell logout · WebPhone remains API-key gated.

**Hosts:** webapi `:5310` · admin-panel `:5314`

---

## Part 1 — Middleware gate

### Result 1
- **status:** complete
- **verification:** `QueueAclGateMiddleware` · Program.cs · Auth/Health/WebPhone exempt · admin paths for Servers/ACL/CallFiles/Config writes
- **hosts:** webapi

---

## Part 2 — Admin guards + shell logout + i18n

### Result 2
- **status:** complete
- **verification:** routes + guards · login outside shell · logout · fa/en
- **hosts:** admin-panel

---

## Part 3 — Docs + FEATURES + SHIP-READY

### Result 3
- **status:** complete
- **verification:** `panel-auth-queue-acl.md` · FEATURES Done · builds
- **hosts:** webapi · admin-panel
