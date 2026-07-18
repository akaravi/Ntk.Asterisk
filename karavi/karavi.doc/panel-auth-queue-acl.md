# Panel auth (Queue ACL gate)

Product auth model for Admin Panel **and** User Panel — **not** a separate JWT stack.

## When the gate is active

`QueueAcl:RequireLogin` (or persisted store flag) **and** at least one enabled ACL user:

1. **WebApi** `QueueAclGateMiddleware` requires `X-Queue-Acl-Token` (or `Authorization: Bearer`) for `/api/v1/*` except:
   - `/api/v1/Auth/*`
   - `/api/v1/Health*`
   - `/api/v1/WebPhone/*` (own API-key gate)
2. **Admin role** required for: `AsteriskServers`, `QueueAclUsers`, `CallFiles`, non-GET `Config`
3. **Admin Panel** `authGateGuard` redirects to `/login` when gate active and unauthenticated
4. **adminRoleGuard** on Settings + Queue ACL pages
5. Shell shows username + Logout; hides Settings/ACL nav for non-admin
6. **User Panel** (`:5312`) mirrors the same token:
   - HTTP interceptor attaches `X-Queue-Acl-Token` from `localStorage` key `ntk.queueAcl.token` (shared with Admin in same browser)
   - `authGateGuard` on `/calls` and `/jobs`
   - `/login` + logout in shell; SignalR `accessTokenFactory` for hub reconnect after login

## Failure envelope

Middleware returns HTTP 200 with `{ isSuccess: false, data: [], errorMessage }` (same as controllers).

## Related

- `/api/v1/Auth/login|logout|status|me`
- WebPhone: `WebPhone:RequireApiKey` + `X-WebPhone-Api-Key`

Karavi.010 · Karavi.011 · skills: none VoIP-specific (ACL session)
