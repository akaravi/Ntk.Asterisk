# Karavi.008 — Call File spool (gap-callfile)

**planVersion:** `1.0.0` · **promptSpecVersion:** `1.5.0` · **updatedAt:** `2026-07-18T14:10:00+03:30`

**Goal:** Close FEATURES `gap-callfile` — stage→Move ASCII Call File into Asterisk spool; Admin Settings + Jobs create; docs.

**Architecture:** `CallFileService` · `POST /api/v1/Asterisk/CallFiles/Add` · config `CallFileStagingDirectory` / `CallFileOutgoingDirectory` · Queue ACL admin gate.

**Hosts:** webapi `:5310` · admin-panel `:5314`

**Note:** Theme/Docker plan renumbered to `Karavi.012` (number collision resolved 2026-07-18).

---

## Part 1 — CallFileService + API

### Result 1
- **status:** complete
- **verification:** stage→Move · `CallFilesController` · Options/Server DTOs · DI
- **hosts:** webapi

---

## Part 2 — Admin Settings + Jobs UI

### Result 2
- **status:** complete
- **verification:** Settings Call File section · Jobs create form · i18n fa/en · `addCallFile`
- **hosts:** admin-panel

---

## Part 3 — Docs + FEATURES

### Result 3
- **status:** complete
- **verification:** `karavi.doc/callfile-spool.md` · FEATURES Done
- **hosts:** docs · webapi · admin-panel
