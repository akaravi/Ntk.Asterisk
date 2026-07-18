import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { environment } from '../../../environments/environment';
import { AsteriskServer, ConnectionStatus, SiteSettings } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [TranslatePipe, ReactiveFormsModule],
  templateUrl: './settings-page.component.html',
  styleUrl: './settings-page.component.scss',
})
export class SettingsPageComponent implements OnInit {
  private readonly api = inject(AsteriskApiService);
  private readonly fb = inject(FormBuilder);

  readonly apiBaseUrl = environment.apiBaseUrl;
  readonly hubUrl = `${environment.apiBaseUrl.replace(/\/$/, '')}${environment.hubPath}`;

  readonly servers = signal<AsteriskServer[]>([]);
  readonly selectedServerId = signal<string | null>(null);
  readonly settings = signal<SiteSettings | null>(null);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly testResult = signal<ConnectionStatus | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly testing = signal(false);
  readonly serverBusy = signal(false);
  readonly serverQuickSearch = signal('');

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
    reconnectAfterSave: [true],
  });

  ngOnInit(): void {
    this.reload();
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
      persisted: true,
      note: null,
      serverId: server.id,
      serverName: server.name,
      isEnabled: server.isEnabled,
      isDefault: server.isDefault,
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
      reconnectAfterSave: true,
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
          this.reload(createdId);
        },
        error: (err: Error) => {
          this.serverBusy.set(false);
          this.error.set(err.message);
        },
      });
  }

  enableServer(server: AsteriskServer): void {
    this.runServerAction(() => this.api.enableServer(server.id), 'SETTINGS.SERVER_ENABLE_OK');
  }

  disableServer(server: AsteriskServer): void {
    this.runServerAction(() => this.api.disableServer(server.id), 'SETTINGS.SERVER_DISABLE_OK');
  }

  setDefaultServer(server: AsteriskServer): void {
    this.runServerAction(() => this.api.setDefaultServer(server.id), 'SETTINGS.SERVER_DEFAULT_OK');
  }

  deleteServer(server: AsteriskServer): void {
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
        reconnectAfterSave: v.reconnectAfterSave,
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
