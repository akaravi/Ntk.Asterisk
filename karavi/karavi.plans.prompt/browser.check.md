# browser.check — Ntk.Asterisk

**version:** 1.2.0 · **productId:** `ntk-asterisk`

## Hosts

| Host | URL | Routes |
|------|-----|--------|
| AdminPanel | http://localhost:5314 | /connection · /monitor · /jobs · /settings |
| UserPanel | http://localhost:5312 | /calls · /jobs |
| WebApi | http://localhost:5310 | /health · /swagger |

Auth: none on Wave1 panels (no login). Creds file reserved: `karavi/karavi.deploy.config/Deploy_TestUsers.info` (never paste).

## فاز ۱ — Bootstrap

1. Read skill + manifest + skills catalog
2. Discover matched skills; announce `browser check v1.2 — skills discovered: N — catalog applied.`
3. `Initialize-BrowserCheckSession.ps1 -ProjectName ntk-asterisk`

## فاز ۲ — Discovery

Per host: navigate → enumerate menu/links → register tree nodes (route × viewport).

## فاز ۳ — Page cycle (14 steps)

1. Template/layout/UI review
2. API data fetch correctness
3. Content correctness
4. All in-page links
5. All buttons/actions
6. Add row 1 (realistic data) when CRUD applicable
7. Add row 2 when CRUD applicable
8. Edit 1 when CRUD applicable
9. Delete 1 when CRUD applicable
10. List toolbar (pagination/sort/export/search) when grid
11. a11y WCAG AA baseline
12. Responsive current viewport + layout/RTL gate
13. design-auditor grades (design / aiSlop / accessibility)
14. Pass → next node; Fail → fix → restart step 1

### Layout/RTL gate (must fail checks)

- layout-edge-stuck · layout-narrow-container · layout-rtl-broken
- layout-footer-misaligned · layout-overflow-x · layout-hero-collapse

## فاز ۴ — Artifacts

- `karavi/karavi.status/BrowserCheck_Tree.json`
- `karavi/karavi.status/BrowserCheck.html`
- `karavi/karavi.logs/.browser-check-state.json`

## Forbidden

FTP without Deploy · credentials in chat · background-only · done without tree
