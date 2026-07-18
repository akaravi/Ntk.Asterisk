## 2026-07-18 - WebApi API-only + WebPhone host split
- **Request:** Project rule - WebApi API-only (no UI); move wwwroot/webphone to src/WebPhone
- **Rule:** `.cursor/rules/webapi-api-only-no-ui.mdc`
- **UI:** `src/WebPhone/Ntk.Asterisk.WebPhone` (static host, port 5316, WebPhoneUi:ApiBaseUrl -> /ntk-webphone-config.js)
- **WebApi:** removed UseStaticFiles/wwwroot; CORS +5316; API stores unchanged
- **Client:** phone.js / ntk-webphone-bridge.js absolute API base via config
- **Docs:** voiz catalog, WSS setup, local-dev-ports, browser-check, README
- **Verify:** WebApi + WebPhone Release build exit 0

## 2026-07-18 � Karavi.011 UserPanel Queue ACL auth parity
- **Request:** ????? ??? � close UserPanel gap when RequireLogin gate is on
- **UserPanel:** `QueueAclAuthService` � interceptor `X-Queue-Acl-Token` � `authGateGuard` on `/calls` `/jobs` � `/login` � logout � hub `accessTokenFactory` � shared token key with Admin � i18n fa/en
- **Docs:** `panel-auth-queue-acl.md` UserPanel section � `Karavi.011.plan.md` � FEATURES X01/N18 � plan collision fix Call File=008 Theme=Karavi.012
- **Verify:** UserPanel `ng build --configuration=production` exit 0 (jobs scss budget warn only)
- **Skills:** (ACL session � no VoIP domain skill)

## 2026-07-18 � Karavi.010 Panel-wide Queue ACL auth
- **Request:** ????? ? close FEATURES `gap-auth`
- **Backend:** `QueueAclGateMiddleware` for `/api/v1/*` when gate active � Auth/Health/WebPhone exempt � admin for Servers/ACL/CallFiles/Config writes
- **Admin:** `authGateGuard` + `adminRoleGuard` � login outside shell � logout � nav hide Settings/ACL for non-admin � i18n
- **Docs:** `panel-auth-queue-acl.md` � FEATURES Done � `Karavi.010.plan.md`
- **Verify:** WebApi Release 0/0 alt `karavi.build.files/auth-010-*` � AdminPanel production exit 0
- **Skills:** (ACL session ? no VoIP domain skill)

## 2026-07-18 ? Karavi.009 Multi-AMI live sessions
- **Request:** SHIP-READY execute plan autonomous loop ? close `gap-multi-ami-live`
- **Backend:** `AmiSession` per-server LiveEndpoint map � ops primary events � secondary keepalive � `ActionConnect`/`Disconnect` + `serverId` � `ActionConnectAll` � GetList without probe
- **Admin:** Connection per-row Connect/Disconnect � Connect all � i18n fa/en
- **Docs:** `multi-ami-live-sessions.md` � FEATURES Done � `Karavi.009.plan.md` complete
- **Verify:** WebApi Release 0/0 alt `karavi.build.files/multi-ami-009-final-*` � AdminPanel production exit 0 (scss budget warn only)
- **Skills:** asterisk-voip-stack � asterisk-ami

## 2026-07-18 ? Karavi.008 Call File spool
- **Request:** ????? (SHIP-READY) ? close FEATURES `gap-callfile`
- **Backend:** `CallFileService` stage?Move ASCII � `POST /api/v1/Asterisk/CallFiles/Add` � config `CallFileStagingDirectory`/`CallFileOutgoingDirectory` on Options/Server/DTOs/Settings � DI
- **Admin:** Settings Call File section � Jobs create form � i18n fa/en � `addCallFile`
- **Docs:** `karavi.doc/callfile-spool.md` � FEATURES Done � `Karavi.008.plan.md` complete
- **Verify:** WebApi Release 0/0 alt `karavi.build.files/callfile-008-*` � AdminPanel production exit 0 (scss budget warn only)
- **Skills:** asterisk-voip-stack � asterisk-callfile

## 2026-07-18 ? Karavi.008 Theme (X04) + dialplan doc + Docker
- **Request:** ????? ? deferred X04 + WebPhone Wave-5 stubs ??????????
- **Theme:** Admin+User `ThemeService` light/dark/system � `data-theme` override � nav toggle
- **Docs:** `webphone-sip-message-dialplan.md` � H01/L03 catalog � Karavi.005 stubs D-DIALPLAN-MSG/D-DOCKER complete
- **Docker:** WebApi Dockerfile + compose � root `.dockerignore` (no Development overlays)
- **Still deferred:** D-XMPP � D-SFU � D-NG � X01 full JWT � X03 multi-AMI � Q12 FreeSWITCH
- **Verify:** Admin+User production builds green
- **Skills:** asterisk-voip-stack (docs)

## 2026-07-18 ? Karavi.007 Queue ACL (Q13) + stats snapshots (Q14)
- **Request:** ????? ? Q13 Auth ACL � Q14 historical stats
- **ACL:** `queue-acl-users.json` � Auth login/status � QueueAclUsers CRUD � filter Queues + SignalR � Admin `/login` `/queue-acl`
- **Stats:** sampler from QueueMonitor ? snapshots JSON � `QueueStats/GetList` � Admin `/queue-stats`
- **Verify:** WebApi Release 0/0 � AdminPanel production green
- **Docs:** FEATURES Q13/Q14 done � queue-panel-ami.md � Karavi.007.plan.md
- **Skills:** asterisk-voip-stack � asterisk-ami
- **Deferred OOS:** Q12 FreeSWITCH

## 2026-07-18 ? Karavi.006 Queue Panel Q11 hide/show/rename
- **Request:** ????? ? Q11 hide/rename queues
- **Backend:** `QueueHideList`/`QueueShowList`/`QueueRenameMap` on server config � `QueueMonitorService.ApplyDisplay` � `QueueDto.RealName`
- **Admin:** Settings SECTION_QUEUES � pause/unpause via `amiQueueName` � i18n fa/en
- **Docs:** FEATURES Q11 done � `queue-panel-ami.md` � `Karavi.006.plan.md`
- **Verify:** WebApi Release 0/0 � AdminPanel production build green (scss budget warn only)
- **Skills:** asterisk-voip-stack � asterisk-ami

## 2026-07-18 ? Karavi.005 Wave 4 continue (AUTH + PWA)
- **Request:** ????? Wave 4 stubs ? Auth GetSipConfig � PWA SW harden � skip XMPP/SFU/Angular
- **Part 11:** `WebPhone:RequireApiKey` + `ApiKey` � gate when both set � `X-WebPhone-Api-Key` � Dev open � phone.js header � Extensions write gated
- **Part 12:** `sw.js` local `lib/*` only � cacheID `ntk-webphone-v1` � purge old � no `/api/` cache
- **Docs:** catalog L04/K03 done � H01/C05 dialplan notes � planVersion 1.3.0 � stubs ? Wave 5+
- **Verify:** WebApi Release exit **0** (0/0) alt outdir `karavi.build.files/webphone-w4-20260718125307` � no commit � no FTP
- **Still deferred:** D-XMPP � D-SFU � D-NG � D-DOCKER � live WSS register

## 2026-07-18 ? SHIP-READY Karavi.005 WebPhone (autonomous loop)
- **Gap fix:** missing `App_Data/webphone-extensions.json` ? seeded from `.example` (gitignored placeholder); `WebPhoneExtensionStore.TrySeedFromExample` on empty; catalog L05 seed docs; wave A?K statuses ? done
- **Verify:** WebApi Release exit **0** (0/0, alt outdir) � `Ntk.Asterisk.sln` Release exit **0** (0 errors) � secrets-scan **PASS** � no commit � no FTP
- **Residual:** external Asterisk WSS + real SIP secret for live register; AGPL if network-serving modified UI
- **Skills:** asterisk-voip-stack � asterisk-ami
## 2026-07-18 ? SHIP-READY re-verify (autonomous loop #3)
- structure OK � sln Release � Admin+User ng production � secrets PASS
- live health+Queues OK � planPending=none � Karavi.004 complete
## 2026-07-18 ? Karavi.005 VOIZ-WebPhone ? WebApi
- **Request:** Feature inventory Voip.VOIZ-WebPhone ? static_plus_api in Ntk.Asterisk.WebApi (Angular OOS)
- **Note:** Cursor draft plan id 003; repo uses **Karavi.005** (003=ChanSpy, 004=Queue Panel)
- **Artifacts:** `karavi.doc/voiz-webphone-features.md` � `Karavi.005.plan.md`
- **Wave1:** wwwroot/webphone (AGPL NOTICE) � GetSipConfig � SIP/WSS additive server fields
- **Wave2:** Buddies/CDR JSON APIs � BLF/MWI AMI?SignalR
- **Wave3:** recording/QoS ingest � WebPhoneOptions flags
- **Skills:** asterisk-voip-stack � asterisk-ami

## 2026-07-18 ? SHIP-READY re-verify (autonomous loop #2)
- structure OK � sln Release 0 err � Admin+User ng production green � secrets PASS
- live: health 200 amiConnected � Queues/GetList isSuccess count=13
- Karavi.004 Parts 1?5 complete � no pending/in_progress plan parts
## 2026-07-18 ? SHIP-READY Karavi.004 (autonomous loop)
- **Gates:** karavi-structure OK � sln Release 0 err � Admin+User ng production green � secrets-scan PASS
- **Smoke:** health 200 amiConnected � Queues/GetList isSuccess count=13 � GetOne OK � swagger 200
- **Fix:** recreate karavi.logs/deploy/build/publish dirs � PauseMember ctor harden � admin-panel.md Queues
- **WebApi:** left running on http://127.0.0.1:5310 for operator smoke
## 2026-07-18 ? Karavi.004 Queue Panel ? WebApi
- **Request:** Feature inventory ? Voip.AsteriskQueuePanel; plan + implement queues in Ntk.Asterisk.WebApi
- **Artifacts:** Voip.AsteriskQueuePanel/FEATURES.Ntk.Asterisk.md � PLAN.Karavi.004 � karavi/.../Karavi.004.plan.md � karavi.doc/queue-panel-ami.md
- **API:** Queues GetList/GetOne � ActionPauseMember/Unpause � ActionHangupEntry � SignalR queuesUpdated
- **Admin:** /queues � pause/hangup/spy � i18n fa/en
- **Verify:** WebApi Release 0/0 � AdminPanel production green
# History 2026-07-18

## 2026-07-18 ? Karavi.007 Connection Connect/Disconnect UI
- **Admin:** ActionConnect/ActionDisconnect on live session (status + card) � i18n fa/en
- **FEATURES:** gap-connect-ui Done � gap-queue-ui / gap-persist-jobs synced
- **Verify:** AdminPanel production build

## 2026-07-18 ? Karavi.004 Admin Bridge UI
- **Request:** ????? ? ???? gap-bridge-ui
- **Admin:** bridge-strip � ?????? ?? ????? � ActionBridge � i18n fa/en
- **FEATURES:** gap-bridge-ui Done
- **Verify:** AdminPanel production build

## 2026-07-18 ? Karavi.003 ChanSpy / ExtenSpy (SHIP-READY)
- **Request:** Feature inventory ? Voip.AsteriskChanSpyPro; plan + implement spy modes in Ntk
- **Artifacts:** `Voip.AsteriskChanSpyPro/FEATURES.md` ? `PLAN.Karavi.003?` ? `karavi/.../Karavi.003.plan.md` ? `karavi.doc/chanspy-extenspy-ami.md`
- **API:** `POST /api/v1/Asterisk/Channels/ActionChanSpy` ? CallJobType.CommandChanSpy ? ExtenSpy flags Eq/Eqo/Eqw/EqW/EqB/Eqd
- **Admin:** Monitor supervisor field + spy actions on in-call peers/channels ? i18n fa/en
- **Verify:** WebApi Release 0/0 � AdminPanel production green � autonomous re-verify OK

## 2026-07-18 (Asia/Tehran) ? Monitor: live call state on peer tiles
- **???????:** ????? ????/????? ?????? ???? ???????/????? ??? ???
- **Backend:** enrich peers ?? Status channels + DeviceState/ExtensionStatus � Status?`In Use`/`Ringing (Ns)` � AMI NewState/DialBegin/DeviceState
- **Admin:** tone ????/??? � ??? ???? � callerId ??? tile
- **Verify:** WebApi + AdminPanel build � Peers DTO additive fields

## 2026-07-18 (Asia/Tehran) ? Monitor: active servers strip above list
- **???????:** ????? ???? Monitor ??????? ???? ???? ?????? ???? ???????
- **Admin:** ???? `server-strip` ?? `Connection/GetList` � badge �?? ??? ???????� � ???? ? `ActionSetDefault` + reconnect + reload peers/channels
- **i18n:** fa/en `MONITOR.SERVERS_*` � `MONITORING` � `SERVER_SWITCH_OK`
- **Verify:** AdminPanel production build green

## 2026-07-18 (Asia/Tehran) ? Monitor page fully live (SignalR)
- **????:** ???? ??? `amiEvent` ?????????? UI ????? `peersUpdated`/`channelsUpdated` ??? ? `SubscribeMonitor` ??? ??????
- **Backend:** `MonitorService` ?? ?????? + debounce ??? AMI ? push `peersUpdated`/`channelsUpdated` ?? ???? `monitor`
- **Admin:** `subscribeMonitor` ?? Monitor � merge ???? peers/trunks/channels � bind `amiEvent`/`connectionStatus`
- **Verify:** WebApi build � Peers GetList=48 � AdminPanel production build

## 2026-07-18 (Asia/Tehran) ? Persist CallJobs across WebApi restart
- **????:** `CallJobStore` ??? RAM ??? ? ??? ?? restart/`????????` ???? ???? (`totalCount=0`)
- **Fix:** persist ???? ?? `App_Data/call-jobs.json` ??? Add/Update � load ?? ctor � job??? ????????? ? Failed
- **gitignore:** `call-jobs.json` (+ tmp) � seed ??????? ?? job ????? ?? ??????? ?????
- **Verify:** GetList total=2 ???/??? restart WebApi

## 2026-07-18 (Asia/Tehran) ? Recording filename from/to/date/time
- **????:** `ntk-{yyyyMMdd}-{HHmmss}-from-{from}-to-{to}.wav`
- **Backend:** `CallRecordingService.BuildRecordingFileName` � MixMonitor path � download Content-Disposition
- **UI:** Admin/User download fallback ???? ???? (?? jobId)

## 2026-07-18 (Asia/Tehran) ? Live job status on User + Admin dashboards
- **????:** API ?? ???? SignalR `jobs` ?????????? ????????? `SubscribeJobs` ?? ??? ????????
- **Admin/User hub:** `SubscribeJobs` ?? ?? connect + rejoin ??? reconnect
- **Jobs pages:** merge ???? ?????/???? Cancel?Redial ???? Refresh ????

## 2026-07-18 (Asia/Tehran) ? Monitor tabs mosaic like All
- **???????:** ????? ????? (???????/SIP � ????? � ????? ????) ????? ?? ??? ?????????
- **Admin:** ???????? `entity-board`/`entity-card` ?? ???? `tile-board`/`tile` auto-fill
- **Verify:** AdminPanel production build

## 2026-07-18 (Asia/Tehran) ? Redial only after call ended
- **???????:** �???? ????� ??? ???? ???? ?????/????? ???? ?? ?? ??? ?????
- **UI:** Admin + User `canRedial` ? terminal (`completed`/`failed`/`cancelled`) ?? `endedAtUtc`
- **API:** `CallJobEngine.RedialAsync` ???? ????

## 2026-07-18 (Asia/Tehran) ? Monitor All tab compact mosaic
- **???????:** ?? ??? (SIP+?????+?????) � ?????? ???? ????????? � ???????? ????? � ????? ??????
- **Backend:** `PeerDto.LastActivityUtc` � `ChannelDto.DurationSeconds/LastActivityUtc` � track ?? PeerStatus + SIPPeers
- **Admin:** ?? `???` ??????? � tile-board auto-fill � `statusTone` ???? OK (n ms) � i18n fa/en
- **Verify:** WebApi Debug build green � AdminPanel production build green
- **Skills:** asterisk-voip-stack + asterisk-ami

## 2026-07-18 (Asia/Tehran) ? Connection status list all enabled servers
- **???????:** ???? ????? ??? ??????? ???? (?? ??? ???? ????)
- **API:** `GET /api/v1/Asterisk/Connection/GetList` � `AmiSession.GetStatusListAsync` (live + probe Login)
- **Admin:** ???? Connection ? ????/???? ??? ??????? enabled � toolbar/pager/export � i18n fa/en
- **Verify:** WebApi Debug/Release build green � AdminPanel production build green � smoke GetList total=2 (office live + s410 probe)
- **Skills:** asterisk-voip-stack + asterisk-ami

## 2026-07-18 (Asia/Tehran) ? Doc: manager.conf reload
- **???????:** ????? ??????? reload ?? ?? ????? `/etc/asterisk/manager.conf` ?? `karavi/karavi.doc`
- **??????:** `karavi.doc/asterisk-manager-reload.md` (reload � verify � restart � ??? ?????? AMI � ???????)
- **??????:** ??????????? `karavi.doc/README.md` � ???? ?? `ami-connection-setup-guide.md` ??? ?.? ? ???? ???????

## 2026-07-18 (Asia/Tehran) ? Settings tabs polish
- ??? ???? `tabpanel` ???? ???? � ????? �?????? ????: Name�
- ?? ?? Add? ?? ???? ???? ?????? ??????
- ?????? RTL: ArrowRight/Left � Home/End ??? tablist

## 2026-07-18 (Asia/Tehran) ? Settings servers as tabs
- **???????:** ??????? ?? ?? ???? ????? ???? ???? ??????
- **UI:** ???????? ??????? ?? `role=tablist` (????? Monitor) ? ?? ????: ??? + host:port + badge ????/???????
- **Panel:** ???? Enable/Disable/SetDefault/Delete ??? ?? ????
- **i18n:** ??????????? `SETTINGS.SERVERS_HINT` fa/en

## 2026-07-18 (Asia/Tehran) ? Multi-server AMI registry
- **???????:** ?????? ??? ???? + ????/??????? + ???????
- **Backend:** `AsteriskServerConfig` � `AsteriskSettingsService` multi-store � `AsteriskServersController`
- **Persist:** `App_Data/asterisk-servers.json` � migrate ?? `asterisk-settings.json`
- **AMI:** `GetEffective` = ???? ??????? ???? � `ConnectionStatus.ServerId/ServerName`
- **Admin:** Settings ???? ???? + Add/Enable/Disable/SetDefault/Delete � Connection ????? ???? ????
- **API legacy:** `GetSiteSettings` / `UpdateSiteSettings` (additive `serverId`) ??? ??
- **Verify:** `dotnet build WebApi -c Release` green
- **Skills:** asterisk-voip-stack + asterisk-ami

## 2026-07-18 (Asia/Tehran) ? Read Learn VoIP tutorials (problem context)
- **???????:** ?????? `karavi/karavi.doc/Learn` ???? ?? ???? ???/???
- **????? ??????????? (pypdf):**
  - `voiping_dialplan_tutorial.pdf` (59p) ? Channel/Call/Bridge � Dial � Local `/n` � ChanSpy/Monitor path
  - `voiping_ami_programing_tutorial.pdf` (40p) ? Login � Events � Originate/OriginateResponse � auto-dial module
  - `voiping_agi_programing_tutorial.pdf` (39p) ? AGI ????? Dial/MeetMe/MOH/Monitor ???? � outbound ?? AGI ?????
  - `voiping_callfile_tutorial.pdf` (15p) ? Channel + Context/App � spool encoding
- **????? ?? ???? ?????????????:**
  - Call = Bridge ??? Channel (Dialplan p. Channel/Bridge)
  - Local ?? ?? bridge ???????? ?? ??? ?????? ??? `/n` ? Originate ?? `Local/{n}@ctx/n` ???
  - Dial ??? Local?Local ?? bridge ??????? media ??? SIP peers ?? trunk `directmedia` ??? ??????? ? ???? softmix ??? PBX (ConfBridge) ?? AMI Bridge ????
  - ???/????: ???? monitor ? ??? Channel ???? ? MixMonitor ??? SIP ?? ?? ConfBridge
  - ???? ????: AMI Originate + Dialplan ConfBridge (?? AGI ???? Dial/Monitor ??????)
- **????? ?? ????:** `CallJobEngine.TryConfBridgePeersAsync` ?? dual Redirect + Answer/Wait/ConfBridge � download HTTP publish ? ???????? ?? ????????? ????
- **??????? ???:** `karavi/karavi.logs/learn-extract/*.txt`

## 2026-07-18 ? SHIP-READY verify (Karavi.002 gates)
- `dotnet build Ntk.Asterisk.sln -c Release` ? 0 errors
- AdminPanel `ng build --configuration=production` ? green (scss budget warnings only)
- UserPanel `ng build --configuration=production` ? green (scss budget warning only)
- Plans Karavi.001/002 Parts already Result=complete; no open Wave-1 code gaps

## 2026-07-18 (Asia/Tehran) ? Karavi.005 Wave 5 continue
- **???????:** ????? ??? (?? ?? Wave 4)
- **Bridge:** `wwwroot/webphone/ntk-webphone-bridge.js` ? sync CDR/QoS/recording/buddies + Presence/MWI poll + GetWebPhoneOptions
- **Client:** index.html load bridge ? sw.js `ntk-webphone-v2` ? phone.js Features from GetSipConfig
- **Docs:** `webphone-sip-message-dialplan.md` ? `webphone-asterisk-wss-setup.md`
- **Plan:** Karavi.005 planVersion 1.4.0 Parts 13?14 complete ? deferred XMPP/SFU/Angular ? Wave 6+

## 2026-07-18 (Asia/Tehran) � SHIP-READY Karavi.005 (1.4.1)
- **Gap fix:** Presence/Mwi ActionQuery accept body+query; bridge sends both
- **SW:** `ntk-webphone-v3`
- **Verify:** WebApi Release 0/0 � solution Release � secrets-scan PASS � docs+seed OK
- **Deferred:** D-XMPP / D-SFU / D-NG (Wave 6+)

## 2026-07-18 (Asia/Tehran) - Karavi.013 WebPhone panels complete
- **Goal:** Surface `/api/v1/WebPhone/*` on AdminPanel (manage) + UserPanel (consume) with softphone deep-link to `:5316`
- **Admin:** Settings SECTION_WEBPHONE SIP/WSS; Extensions CRUD + Options read-only; Buddies/CDR/Recordings/QoS; nav + adminRoleGuard
- **User:** Softphone open; Buddies (add/list); CDR; Recordings download; no Extensions
- **Verify:** AdminPanel + UserPanel `ng build --configuration=production` exit 0
- **Plan:** Karavi.013 planVersion 1.0.1 Status=complete Parts 1-5

## 2026-07-18 (Asia/Tehran) - SHIP-READY autonomous verify loop
- **Command:** SHIP-READY execute plan full autonomous loop until final verification
- **Plans:** Karavi.001-013 Parts complete (Wave6 deferred XMPP/SFU/Angular rewrite unchanged)
- **Gap fix:** src/WebPhone/Ntk.Asterisk.WebPhone missing on disk vs solution - restored from src/Html/Ntk.Asterisk.WebPhone (NTK-patched softphone); solution build unblocked
- **verify-gates:** karavi-structure OK; dotnet build Ntk.Asterisk.sln -c Release 0 errors; AdminPanel + UserPanel production ng build exit 0; secrets-scan PASS (base appsettings null placeholders only)
- **Residual:** solution+docs use src/WebPhone; git tracks mirror src/Html (118). Both on disk synced. Next commit: git mv Html->WebPhone
- **No:** commit / push / FTP / Deploy

## 2026-07-18 (Asia/Tehran) - WebPhone path Html to WebPhone
- **Request:** continue residual from SHIP-READY
- **Action:** relocate tracked softphone src/Html/Ntk.Asterisk.WebPhone -> src/WebPhone/Ntk.Asterisk.WebPhone (index rename staged; Html disk removed after stopping locked WebPhone.exe)
- **Verify:** WebPhone Release build 0/0; solution path matches docs/rule
- **No commit** unless user requests
