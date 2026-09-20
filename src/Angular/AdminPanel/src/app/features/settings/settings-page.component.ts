import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AsteriskServer, ConnectionStatus, SiteSettings } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [TranslatePipe, ReactiveFormsModule],
  templateUrl: './settings-page.component.html',
  styleUrl: './settings-page.component.scss',
})
export class SettingsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly subs = new Subscription();
  readonly apiBaseUrl = environment.apiBaseUrl;
  readonly hubUrl = `${environment.apiBaseUrl.replace(/\/$/, '')}${environment.hubPath}`;

  readonly servers = signal<AsteriskServer[]>([]);
  readonly connections = signal<ConnectionStatus[]>([]);
  readonly selectedServerId = signal<string | null>(null);
  readonly settings = signal<SiteSettings | null>(null);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly testResult = signal<ConnectionStatus | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly testing = signal(false);
  readonly serverBusy = signal(false);
  readonly rowBusyId = signal<string | null>(null);
  readonly serverQuickSearch = signal('');
  readonly activeTab = signal<'ami' | 'originate' | 'smartrouting' | 'recording' | 'queues' | 'webphone' | 'guide'>('ami');
  readonly mode = signal<'list' | 'editor'>('list');

  readonly form = this.fb.nonNullable.group({
    name: ['Default', Validators.required],
    host: ['', Validators.required],
    port: [5038, [Validators.required, Validators.min(1), Validators.max(65535)]],
    username: ['', Validators.required],
    secret: [''],
    clearSecret: [false],
    channelTech: ['PJSIP', Validators.required],
    originateVia: ['LocalContext', Validators.required],
    originateContext: ['from-internal', Validators.required],
    musicOnHoldClass: ['default', Validators.required],
    defaultTrunk: [''],
    trunkPeerFilter: [''],
    defaultTimeoutMs: [30000, [Validators.required, Validators.min(1000)]],
    defaultCallerId: [''],
    keepAlive: [true],
    pingIntervalMs: [10000, [Validators.required, Validators.min(1000)]],
    autoConnectOnStartup: [true],
    recordingEnabled: [true],
    recordingLocalDirectory: [''],
    recordingHttpBaseUrl: [''],
    recordingAsteriskDirectory: ['/var/spool/asterisk/monitor'],
    recordingFormat: ['wav'],
    queueHideList: [''],
    queueShowList: [''],
    queueRenameMap: [''],
    callFileStagingDirectory: [''],
    callFileOutgoingDirectory: [''],
    sipWebsocketUrl: [''],
    sipWebsocketHost: [''],
    sipDomain: [''],
    webSocketPath: ['/ws'],
    webSocketPort: [8089],
    sipUseTls: [true],
    stunServersJson: [''],
    reconnectAfterSave: [true],
    ivrInterceptDelaySeconds: [3, [Validators.required, Validators.min(0), Validators.max(120)]],
    defaultExtensionTimeoutSeconds: [15, [Validators.required, Validators.min(5), Validators.max(120)]],
    defaultExternalTimeoutSeconds: [30, [Validators.required, Validators.min(5), Validators.max(120)]],
    defaultOutboundTrunk: ['trunk-default'],
    defaultFallbackContext: ['timeconditions,2,1'],
    enableDirectInboundRouting: [true],
    enableLiveIvrIntercept: [true],
    autoRecordSmartRoutes: [false],
  });

  ngOnInit(): void {
    this.reload();
    this.subs.add(
      this.hub.hubEvents$.subscribe((ev) => {
        if (ev.kind === 'connection') {
          this.connections.update((cur) => {
            const idx = cur.findIndex((c) => c.serverId === ev.payload.serverId);
            if (idx >= 0) {
              const next = [...cur];
              next[idx] = ev.payload;
              return next;
            }
            return [...cur, ev.payload];
          });
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  isServerConnected(server: AsteriskServer): boolean | null {
    const list = this.connections();
    const match = list.find((c) => c.serverId === server.id) ?? (server.isDefault ? list.find((c) => c.isDefault) : null);
    if (!match) return null;
    return match.connected;
  }
  filteredServers(): AsteriskServer[] {
    const q = this.serverQuickSearch().trim().toLowerCase();
    const list = this.servers();
    if (!q) return list;
    return list.filter(
      (s) =>
        s.name.toLowerCase().includes(q) ||
        (s.host ?? '').toLowerCase().includes(q) ||
        (s.username ?? '').toLowerCase().includes(q) ||
        s.id.toLowerCase().includes(q),
    );
  }

  selectedServer(): AsteriskServer | null {
    const id = this.selectedServerId();
    if (!id) return null;
    return this.servers().find((s) => s.id === id) ?? null;
  }

  serverEndpoint(server: AsteriskServer): string {
    const host = server.host?.trim() || '—';
    const port = server.port && server.port > 0 ? String(server.port) : '—';
    return `${host}:${port}`;
  }

  onServerSearch(value: string): void {
    this.serverQuickSearch.set(value);
  }

  openServerEditor(server: AsteriskServer): void {
    this.selectServer(server);
    this.mode.set('editor');
    this.activeTab.set('ami');
  }

  backToList(): void {
    this.mode.set('list');
  }

  setActiveTab(tab: 'ami' | 'originate' | 'smartrouting' | 'recording' | 'queues' | 'webphone' | 'guide'): void {
    this.activeTab.set(tab);
  }
  onServerTabKeydown(event: KeyboardEvent): void {
    const tabs = this.filteredServers();
    if (tabs.length === 0) return;

    const currentId = this.selectedServerId();
    const index = Math.max(
      0,
      tabs.findIndex((s) => s.id === currentId),
    );
    // RTL: ArrowRight moves to previous visual tab (index-1), ArrowLeft to next.
    let next = index;
    if (event.key === 'ArrowRight' || event.key === 'ArrowUp') {
      next = (index - 1 + tabs.length) % tabs.length;
    } else if (event.key === 'ArrowLeft' || event.key === 'ArrowDown') {
      next = (index + 1) % tabs.length;
    } else if (event.key === 'Home') {
      next = 0;
    } else if (event.key === 'End') {
      next = tabs.length - 1;
    } else {
      return;
    }

    event.preventDefault();
    this.selectServer(tabs[next]);
    queueMicrotask(() => {
      document.getElementById(`server-tab-${tabs[next].id}`)?.focus();
    });
  }

  reload(preferServerId?: string | null): void {
    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);
    this.testResult.set(null);

    this.api.getConnectionStatusList().subscribe({
      next: (res) => {
        this.connections.set(res.data ?? []);
      },
    });

    this.api.getServers({ pageIndex: 0, pageSize: 100, sortBy: 'name', sortDir: 'asc' }).subscribe({
      next: (r) => {
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Load failed');
          this.loading.set(false);
          return;
        }
        const list = r.data ?? [];
        this.servers.set(list);
        const targetId = preferServerId ?? this.selectedServerId();
        const preferred =
          (targetId ? list.find((s) => s.id === targetId) : null) ||
          list.find((s) => s.isDefault && s.isEnabled) ||
          list.find((s) => s.isDefault) ||
          list[0] ||
          null;
        if (preferred) {
          this.selectServer(preferred);
        } else {
          this.selectedServerId.set(null);
          this.settings.set(null);
        }
        this.loading.set(false);
      },
      error: (err: Error) => {
        this.error.set(err.message);
        this.loading.set(false);
      },
    });
  }

  selectServer(server: AsteriskServer): void {
    this.selectedServerId.set(server.id);
    this.settings.set({
      amiConfigured: server.amiConfigured,
      host: server.host,
      port: server.port,
      username: server.username,
      usernameConfigured: server.username,
      secretConfigured: server.secretConfigured,
      channelTech: server.channelTech,
      originateVia: server.originateVia,
      originateContext: server.originateContext,
      musicOnHoldClass: server.musicOnHoldClass,
      defaultTrunk: server.defaultTrunk,
      trunkPeerFilter: server.trunkPeerFilter,
      defaultTimeoutMs: server.defaultTimeoutMs,
      defaultCallerId: server.defaultCallerId,
      keepAlive: server.keepAlive,
      pingIntervalMs: server.pingIntervalMs,
      autoConnectOnStartup: server.autoConnectOnStartup,
      recordingEnabled: server.recordingEnabled,
      recordingLocalDirectory: server.recordingLocalDirectory,
      recordingHttpBaseUrl: server.recordingHttpBaseUrl,
      recordingAsteriskDirectory: server.recordingAsteriskDirectory,
      recordingFormat: server.recordingFormat,
      queueHideList: server.queueHideList,
      queueShowList: server.queueShowList,
      queueRenameMap: server.queueRenameMap,
      callFileStagingDirectory: server.callFileStagingDirectory,
      callFileOutgoingDirectory: server.callFileOutgoingDirectory,
      sipWebsocketUrl: server.sipWebsocketUrl,
      sipWebsocketHost: server.sipWebsocketHost,
      sipDomain: server.sipDomain,
      webSocketPath: server.webSocketPath,
      webSocketPort: server.webSocketPort,
      sipUseTls: server.sipUseTls,
      stunServersJson: server.stunServersJson,
      persisted: true,
      note: null,
      serverId: server.id,
      serverName: server.name,
      isEnabled: server.isEnabled,
      isDefault: server.isDefault,
      ivrInterceptDelaySeconds: server.ivrInterceptDelaySeconds ?? 3,
      defaultExtensionTimeoutSeconds: server.defaultExtensionTimeoutSeconds ?? 15,
      defaultExternalTimeoutSeconds: server.defaultExternalTimeoutSeconds ?? 30,
      defaultOutboundTrunk: server.defaultOutboundTrunk ?? 'trunk-default',
      defaultFallbackContext: server.defaultFallbackContext ?? 'timeconditions,2,1',
      enableDirectInboundRouting: server.enableDirectInboundRouting ?? true,
      enableLiveIvrIntercept: server.enableLiveIvrIntercept ?? true,
      autoRecordSmartRoutes: server.autoRecordSmartRoutes ?? false,
    });
    this.form.patchValue({
      name: server.name || 'Default',
      host: server.host ?? '',
      port: server.port && server.port > 0 ? server.port : 5038,
      username: server.username ?? '',
      secret: '',
      clearSecret: false,
      channelTech: server.channelTech || 'PJSIP',
      originateVia: server.originateVia || 'LocalContext',
      originateContext: server.originateContext || 'from-internal',
      musicOnHoldClass: server.musicOnHoldClass || 'default',
      defaultTrunk: server.defaultTrunk ?? '',
      trunkPeerFilter: server.trunkPeerFilter ?? '',
      defaultTimeoutMs: server.defaultTimeoutMs || 30000,
      defaultCallerId: server.defaultCallerId ?? '',
      keepAlive: server.keepAlive,
      pingIntervalMs: server.pingIntervalMs || 10000,
      autoConnectOnStartup: server.autoConnectOnStartup,
      recordingEnabled: server.recordingEnabled ?? true,
      recordingLocalDirectory: server.recordingLocalDirectory ?? '',
      recordingHttpBaseUrl: server.recordingHttpBaseUrl ?? '',
      recordingAsteriskDirectory: server.recordingAsteriskDirectory ?? '/var/spool/asterisk/monitor',
      recordingFormat: server.recordingFormat || 'wav',
      queueHideList: server.queueHideList ?? '',
      queueShowList: server.queueShowList ?? '',
      queueRenameMap: server.queueRenameMap ?? '',
      callFileStagingDirectory: server.callFileStagingDirectory ?? '',
      callFileOutgoingDirectory: server.callFileOutgoingDirectory ?? '',
      sipWebsocketUrl: server.sipWebsocketUrl ?? '',
      sipWebsocketHost: server.sipWebsocketHost ?? '',
      sipDomain: server.sipDomain ?? '',
      webSocketPath: server.webSocketPath ?? '/ws',
      webSocketPort: server.webSocketPort && server.webSocketPort > 0 ? server.webSocketPort : 8089,
      sipUseTls: server.sipUseTls ?? true,
      stunServersJson: server.stunServersJson ?? '',
      reconnectAfterSave: true,
      ivrInterceptDelaySeconds: server.ivrInterceptDelaySeconds ?? 3,
      defaultExtensionTimeoutSeconds: server.defaultExtensionTimeoutSeconds ?? 15,
      defaultExternalTimeoutSeconds: server.defaultExternalTimeoutSeconds ?? 30,
      defaultOutboundTrunk: server.defaultOutboundTrunk ?? 'trunk-default',
      defaultFallbackContext: server.defaultFallbackContext ?? 'timeconditions,2,1',
      enableDirectInboundRouting: server.enableDirectInboundRouting ?? true,
      enableLiveIvrIntercept: server.enableLiveIvrIntercept ?? true,
      autoRecordSmartRoutes: server.autoRecordSmartRoutes ?? false,
    });
  }

  addServer(): void {
    this.serverBusy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.api
      .addServer({
        name: `Server ${this.servers().length + 1}`,
        isEnabled: true,
        isDefault: this.servers().length === 0,
        host: '',
        port: 5038,
        username: '',
        channelTech: 'PJSIP',
        originateVia: 'LocalContext',
        originateContext: 'from-internal',
        musicOnHoldClass: 'default',
        reconnectAfterSave: false,
      })
      .subscribe({
        next: (r) => {
          this.serverBusy.set(false);
          if (!r.isSuccess) {
            this.error.set(r.errorMessage || 'Add failed');
            return;
          }
          const createdId = r.data?.[0]?.id ?? null;
          this.success.set('SETTINGS.SERVER_ADD_OK');
          this.mode.set('editor');
          this.reload(createdId);
        },
        error: (err: Error) => {
          this.serverBusy.set(false);
          this.error.set(err.message);
        },
      });
  }
  toggleAmiConnection(server: AsteriskServer): void {
    if (this.rowBusyId() || this.serverBusy()) return;
    this.rowBusyId.set(server.id);
    this.error.set(null);
    this.success.set(null);
    const connected = this.isServerConnected(server);
    const action$ = connected ? this.api.disconnectAmi(server.id) : this.api.connectAmi(server.id);
    action$.subscribe({
      next: (res) => {
        this.rowBusyId.set(null);
        if (!res.isSuccess) {
          this.error.set(res.errorMessage || 'AMI action failed');
          return;
        }
        this.success.set(connected ? 'CONNECTION.DISCONNECT_OK' : 'CONNECTION.CONNECT_OK');
        this.reload(server.id);
      },
      error: (err: Error) => {
        this.rowBusyId.set(null);
        this.error.set(err.message);
      },
    });
  }

  testServer(server: AsteriskServer): void {
    if (this.rowBusyId() || this.serverBusy()) return;
    this.rowBusyId.set(server.id);
    this.testing.set(true);
    this.testResult.set(null);
    this.error.set(null);
    this.api.connectAmi(server.id).subscribe({
      next: (r) => {
        this.rowBusyId.set(null);
        this.testing.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'SETTINGS.TEST_FAIL');
          return;
        }
        const resStatus = r.data?.[0] ?? null;
        this.testResult.set(resStatus);
        if (resStatus) {
          this.connections.update((cur) => {
            const idx = cur.findIndex((c) => c.serverId === server.id);
            if (idx >= 0) {
              const next = [...cur];
              next[idx] = resStatus;
              return next;
            }
            return [...cur, resStatus];
          });
        }
        this.success.set('SETTINGS.TEST_OK');
      },
      error: (err: Error) => {
        this.rowBusyId.set(null);
        this.testing.set(false);
        this.error.set(err.message);
      },
    });
  }

  enableServer(server: AsteriskServer): void {
    this.runServerAction(() => this.api.enableServer(server.id), 'SETTINGS.SERVER_ENABLE_OK');
  }

  disableServer(server: AsteriskServer): void {
    const enabledCount = this.servers().filter((s) => s.isEnabled).length;
    if (server.isDefault || enabledCount <= 1) {
      const msg = this.translate.instant('DASHBOARD.CONFIRM_DISABLE_DEFAULT');
      if (typeof window !== 'undefined' && !window.confirm(msg)) {
        return;
      }
    }
    this.runServerAction(() => this.api.disableServer(server.id), 'SETTINGS.SERVER_DISABLE_OK');
  }
  setDefaultServer(server: AsteriskServer): void {
    this.runServerAction(() => this.api.setDefaultServer(server.id), 'SETTINGS.SERVER_DEFAULT_OK');
  }

  deleteServer(server: AsteriskServer): void {
    if (server.isDefault) return;
    const confirmBase = this.translate.instant('SETTINGS.CONFIRM_SERVER_DELETE');
    const msg = `${confirmBase} (${server.name})`;
    if (typeof window !== 'undefined' && !window.confirm(msg)) return;
    this.runServerAction(() => this.api.deleteServer(server.id), 'SETTINGS.SERVER_DELETE_OK');
  }
  save(): void {
    const id = this.selectedServerId();
    if (!id) {
      this.error.set('SETTINGS.SERVER_SELECT_FIRST');
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    this.testResult.set(null);
    this.api
      .updateServer({
        id,
        name: v.name.trim(),
        host: v.host.trim(),
        port: Number(v.port),
        username: v.username.trim(),
        secret: v.clearSecret ? null : v.secret.trim() || null,
        clearSecret: v.clearSecret,
        channelTech: v.channelTech.trim(),
        originateVia: v.originateVia.trim(),
        originateContext: v.originateContext.trim(),
        musicOnHoldClass: v.musicOnHoldClass.trim() || 'default',
        defaultTrunk: v.defaultTrunk.trim() || null,
        trunkPeerFilter: v.trunkPeerFilter.trim() || null,
        defaultTimeoutMs: Number(v.defaultTimeoutMs),
        defaultCallerId: v.defaultCallerId.trim() || null,
        keepAlive: v.keepAlive,
        pingIntervalMs: Number(v.pingIntervalMs),
        autoConnectOnStartup: v.autoConnectOnStartup,
        recordingEnabled: v.recordingEnabled,
        recordingLocalDirectory: v.recordingLocalDirectory.trim() || null,
        recordingHttpBaseUrl: v.recordingHttpBaseUrl.trim() || null,
        recordingAsteriskDirectory: v.recordingAsteriskDirectory.trim() || null,
        recordingFormat: v.recordingFormat.trim() || 'wav',
        queueHideList: v.queueHideList.trim() || null,
        queueShowList: v.queueShowList.trim() || null,
        queueRenameMap: v.queueRenameMap.trim() || null,
        callFileStagingDirectory: v.callFileStagingDirectory.trim() || null,
        callFileOutgoingDirectory: v.callFileOutgoingDirectory.trim() || null,
        sipWebsocketUrl: v.sipWebsocketUrl.trim() || null,
        sipWebsocketHost: v.sipWebsocketHost.trim() || null,
        sipDomain: v.sipDomain.trim() || null,
        webSocketPath: v.webSocketPath.trim() || null,
        webSocketPort: Number(v.webSocketPort) || 8089,
        sipUseTls: v.sipUseTls,
        stunServersJson: v.stunServersJson.trim() || null,
        reconnectAfterSave: v.reconnectAfterSave,
        ivrInterceptDelaySeconds: Number(v.ivrInterceptDelaySeconds) || 3,
        defaultExtensionTimeoutSeconds: Number(v.defaultExtensionTimeoutSeconds) || 15,
        defaultExternalTimeoutSeconds: Number(v.defaultExternalTimeoutSeconds) || 30,
        defaultOutboundTrunk: v.defaultOutboundTrunk.trim() || 'trunk-default',
        defaultFallbackContext: v.defaultFallbackContext.trim() || 'timeconditions,2,1',
        enableDirectInboundRouting: v.enableDirectInboundRouting,
        enableLiveIvrIntercept: v.enableLiveIvrIntercept,
        autoRecordSmartRoutes: v.autoRecordSmartRoutes,
      })
      .subscribe({
        next: (r) => {
          this.saving.set(false);
          if (!r.isSuccess) {
            this.error.set(r.errorMessage || 'Save failed');
            return;
          }
          this.success.set('SETTINGS.SAVE_OK');
          this.form.patchValue({ secret: '', clearSecret: false });
          this.reload();
        },
        error: (err: Error) => {
          this.saving.set(false);
          this.error.set(err.message);
        },
      });
  }

  testConnection(): void {
    const selected = this.selectedServer();
    if (selected) {
      this.testServer(selected);
      return;
    }
    this.testing.set(true);
    this.error.set(null);
    this.success.set(null);
    this.testResult.set(null);
    this.api.testConnection().subscribe({
      next: (r) => {
        this.testing.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'SETTINGS.TEST_FAIL');
          return;
        }
        const status = r.data?.[0] ?? null;
        this.testResult.set(status);
        if (status) {
          const targetId = status.serverId;
          this.connections.update((cur) => {
            const idx = cur.findIndex((c) =>
              targetId ? c.serverId === targetId : c.isDefault,
            );
            if (idx >= 0) {
              const next = [...cur];
              next[idx] = status;
              return next;
            }
            return [...cur, status];
          });
        }
        this.success.set('SETTINGS.TEST_OK');
      },
      error: (err: Error) => {
        this.testing.set(false);
        this.error.set(err.message);
      },
    });
  }

  private runServerAction(
    action: () => ReturnType<AsteriskApiService['enableServer']>,
    okKey: string,
  ): void {
    this.serverBusy.set(true);
    this.error.set(null);
    this.success.set(null);
    action().subscribe({
      next: (r) => {
        this.serverBusy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Action failed');
          return;
        }
        this.success.set(okKey);
        this.reload();
      },
      error: (err: Error) => {
        this.serverBusy.set(false);
        this.error.set(err.message);
      },
    });
  }
}
