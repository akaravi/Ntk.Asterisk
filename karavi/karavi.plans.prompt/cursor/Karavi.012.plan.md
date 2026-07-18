# Karavi.012 — Theme toggle (X04) + WebPhone Wave-5 docs/Docker

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T14:10:00+03:30`

**Note:** Renumbered from colliding `Karavi.008` (Call File spool keeps `008`).

**Goal:** Close FEATURES X04 dark/light/system theme; complete Karavi.005 stubs D-DIALPLAN-MSG + D-DOCKER (doc + container). Leave XMPP/SFU/Angular rewrite deferred.

**Hosts:** admin-panel · user-panel · webapi · docs

---

## Part 1 — Theme toggle Admin + User

### Result 1
- **status:** complete
- **verification:** `ThemeService` · `data-theme` CSS · shell/topbar cycle · i18n THEME.*
- **hosts:** admin-panel · user-panel

---

## Part 2 — SIP MESSAGE dialplan doc (D-DIALPLAN-MSG)

### Result 2
- **status:** complete
- **verification:** `karavi.doc/webphone-sip-message-dialplan.md` · catalog H01
- **hosts:** docs

---

## Part 3 — WebApi Docker (D-DOCKER)

### Result 3
- **status:** complete
- **verification:** `Dockerfile` · `docker-compose.yml` · root `.dockerignore` (excludes Development overlays)
- **hosts:** webapi

---

## Still deferred

- D-XMPP · D-SFU · D-NG · Q12 FreeSWITCH
- Full JWT product (X01) — Queue ACL session is the auth model (Karavi.010–011)
