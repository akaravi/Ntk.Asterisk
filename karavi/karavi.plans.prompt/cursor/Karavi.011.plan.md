# Karavi.011 — UserPanel Queue ACL auth parity

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T14:00:00+03:30`

**Goal:** When `QueueAcl` gate is active, UserPanel (`:5312`) attaches the same session token as Admin and gates `/calls` + `/jobs` behind login — so CallJobs REST works under `QueueAclGateMiddleware`.

**Architecture:** Shared token key `ntk.queueAcl.token` · HTTP interceptor `X-Queue-Acl-Token` · `authGateGuard` · `/login` · SignalR `accessTokenFactory` · same Auth API as Admin.

**Hosts:** user-panel `:5312` · webapi `:5310` (gate already shipped in Karavi.010)

---

## Part 1 — Auth service + HTTP interceptor

### Result 1
- **status:** complete
- **verification:** `QueueAclAuthService` · `queueAclAuthInterceptor` · shared `ntk.queueAcl.token`
- **hosts:** user-panel

---

## Part 2 — Login route + authGateGuard + shell logout

### Result 2
- **status:** complete
- **verification:** `/login` · guard on `/calls` `/jobs` · logout · hub reconnect after login
- **hosts:** user-panel

---

## Part 3 — i18n + docs + SHIP-READY

### Result 3
- **status:** complete
- **verification:** fa/en AUTH.* · `panel-auth-queue-acl.md` UserPanel section · `ng build` production
- **hosts:** user-panel
