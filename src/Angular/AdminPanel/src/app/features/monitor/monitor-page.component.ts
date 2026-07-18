import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import {
  ChannelItem,
  ChanSpyMode,
  ConnectionStatus,
  ListQuery,
  MonitorUnifiedItem,
  PeerItem,
  TrunkItem,
} from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { downloadListAsExcel, downloadListAsPdf } from '../../core/utils/list-export.util';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import {
  AdvancedFilterField,
  ListToolbarComponent,
} from '../../shared/components/list-toolbar/list-toolbar.component';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';

type MonitorTab = 'all' | 'extensions' | 'trunks' | 'channels';
type StatusTone = MonitorUnifiedItem['tone'];

@Component({
  selector: 'app-monitor-page',
  standalone: true,
  imports: [TranslatePipe, FormsModule, ListToolbarComponent, ListPagerComponent, DatePipe],
  templateUrl: './monitor-page.component.html',
  styleUrl: './monitor-page.component.scss',
})
export class MonitorPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
  private readonly i18n = inject(TranslateService);
  private sub?: Subscription;

  readonly tab = signal<MonitorTab>('all');
  readonly loading = signal(false);
  readonly actionBusy = signal(false);
  readonly serverBusy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  /** Supervisor extension for ExtenSpy originate (persisted). */
  spySupervisor = '';

  readonly spyModes: { mode: ChanSpyMode; labelKey: string }[] = [
    { mode: 'listen', labelKey: 'MONITOR.SPY_LISTEN' },
    { mode: 'quiet', labelKey: 'MONITOR.SPY_QUIET' },
    { mode: 'whisper', labelKey: 'MONITOR.SPY_WHISPER' },
    { mode: 'privateWhisper', labelKey: 'MONITOR.SPY_PRIVATE_WHISPER' },
    { mode: 'barge', labelKey: 'MONITOR.SPY_BARGE' },
    { mode: 'dtmf', labelKey: 'MONITOR.SPY_DTMF' },
  ];

  /** Two-channel AMI Bridge selection (ActionBridge). */
  readonly bridgeChannel1 = signal<string | null>(null);
  readonly bridgeChannel2 = signal<string | null>(null);

  readonly servers = signal<ConnectionStatus[]>([]);
  readonly activeServerId = signal<string | null>(null);

  readonly peersAll = signal<PeerItem[]>([]);
  readonly trunksAll = signal<TrunkItem[]>([]);
  readonly channelsAll = signal<ChannelItem[]>([]);

  readonly peersRows = signal<PeerItem[]>([]);
  readonly trunksRows = signal<TrunkItem[]>([]);
  readonly channelsRows = signal<ChannelItem[]>([]);
  readonly unifiedRows = signal<MonitorUnifiedItem[]>([]);
  readonly totalCount = signal(0);

  query: ListQuery = { ...defaultListQuery('title'), pageSize: 50, sortDir: 'asc' };

  readonly peerFilters: AdvancedFilterField[] = [
    { key: 'status', labelKey: 'MONITOR.FILTER_STATUS', placeholderKey: 'MONITOR.FILTER_STATUS_PH' },
    { key: 'tech', labelKey: 'MONITOR.FILTER_TECH', placeholderKey: 'MONITOR.FILTER_TECH_PH', ltr: true },
    { key: 'ip', labelKey: 'MONITOR.FILTER_IP', placeholderKey: 'MONITOR.FILTER_IP_PH', ltr: true },
  ];

  readonly allFilters: AdvancedFilterField[] = [
    { key: 'kind', labelKey: 'MONITOR.FILTER_KIND', placeholderKey: 'MONITOR.FILTER_KIND_PH' },
    { key: 'status', labelKey: 'MONITOR.FILTER_STATUS', placeholderKey: 'MONITOR.FILTER_STATUS_PH' },
    { key: 'tech', labelKey: 'MONITOR.FILTER_TECH', placeholderKey: 'MONITOR.FILTER_TECH_PH', ltr: true },
    { key: 'ip', labelKey: 'MONITOR.FILTER_IP', placeholderKey: 'MONITOR.FILTER_IP_PH', ltr: true },
  ];

  readonly channelFilters: AdvancedFilterField[] = [
    { key: 'state', labelKey: 'MONITOR.FILTER_STATE', placeholderKey: 'MONITOR.FILTER_STATE_PH' },
    {
      key: 'channel',
      labelKey: 'MONITOR.FILTER_CHANNEL',
      placeholderKey: 'MONITOR.FILTER_CHANNEL_PH',
      ltr: true,
    },
  ];

  ngOnInit(): void {
    this.spySupervisor = localStorage.getItem('ntk.monitor.spySupervisor') || '';
    this.reloadServers();
    this.reload();
    void this.hub.subscribeMonitor();
    this.sub = this.hub.hubEvents$.subscribe((ev) => {
      if (ev.kind === 'peers') {
        this.applyPeersLive(ev.payload);
      }
      if (ev.kind === 'channels') {
        this.channelsAll.set(ev.payload);
        this.repage();
      }
      if (ev.kind === 'connection') {
        this.mergeServerLive(ev.payload);
      }
      if (ev.kind === 'amiHint') {
        if (this.peersAll().length === 0 && this.channelsAll().length === 0) {
          this.reload();
        }
      }
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  setTab(tab: MonitorTab): void {
    this.tab.set(tab);
    const sortBy = tab === 'channels' ? 'channel' : tab === 'all' ? 'title' : 'id';
    this.query = { ...defaultListQuery(sortBy), pageSize: tab === 'all' ? 50 : 25, sortDir: 'asc' };
    this.repage();
  }

  onQueryChange(q: ListQuery): void {
    this.query = q;
    this.repage();
  }

  advancedFields(): AdvancedFilterField[] {
    const t = this.tab();
    if (t === 'channels') return this.channelFilters;
    if (t === 'all') return this.allFilters;
    return this.peerFilters;
  }

  serverEndpoint(s: ConnectionStatus): string {
    const host = s.host?.trim() || '—';
    return s.port != null ? `${host}:${s.port}` : host;
  }

  isMonitoring(s: ConnectionStatus): boolean {
    if (s.isLiveSession) return true;
    const active = this.activeServerId();
    return !!active && !!s.serverId && s.serverId === active;
  }

  selectMonitorServer(s: ConnectionStatus): void {
    const id = s.serverId?.trim();
    if (!id || this.serverBusy() || this.isMonitoring(s)) return;

    this.serverBusy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.api.setDefaultServer(id, true).subscribe({
      next: (r) => {
        this.serverBusy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Set default server failed');
          return;
        }
        this.activeServerId.set(id);
        this.success.set('MONITOR.SERVER_SWITCH_OK');
        this.reloadServers();
        window.setTimeout(() => this.reload(), 800);
      },
      error: (err: Error) => {
        this.serverBusy.set(false);
        this.error.set(err.message);
      },
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.reloadServers();
    let pending = 3;
    const done = () => {
      pending -= 1;
      if (pending <= 0) {
        this.loading.set(false);
        this.repage();
      }
    };

    this.api.getPeers().subscribe({
      next: (r) => {
        if (!r.isSuccess) this.error.set(r.errorMessage || 'Peers failed');
        this.applyPeersLive(r.data || []);
        done();
      },
      error: (err: Error) => {
        this.error.set(err.message);
        done();
      },
    });

    this.api.getTrunks().subscribe({
      next: (r) => {
        if (!r.isSuccess) this.error.set(r.errorMessage || 'Trunks failed');
        if ((r.data || []).length > 0 && this.trunksAll().length === 0) {
          this.trunksAll.set(r.data || []);
        }
        done();
      },
      error: (err: Error) => {
        this.error.set(err.message);
        done();
      },
    });

    this.api.getChannels().subscribe({
      next: (r) => {
        if (!r.isSuccess) this.error.set(r.errorMessage || 'Channels failed');
        this.channelsAll.set(r.data || []);
        done();
      },
      error: (err: Error) => {
        this.error.set(err.message);
        done();
      },
    });
  }

  hangup(channel: string): void {
    if (!channel || this.actionBusy()) return;
    this.actionBusy.set(true);
    this.success.set(null);
    this.error.set(null);
    this.api.hangupChannel(channel).subscribe({
      next: (r) => {
        this.actionBusy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Hangup failed');
          return;
        }
        this.success.set('MONITOR.HANGUP_OK');
        this.reload();
      },
      error: (err: Error) => {
        this.actionBusy.set(false);
        this.error.set(err.message);
      },
    });
  }

  onSpySupervisorChange(value: string): void {
    this.spySupervisor = (value || '').trim();
    localStorage.setItem('ntk.monitor.spySupervisor', this.spySupervisor);
  }

  canSpy(targetExtension: string | null | undefined): boolean {
    return !!this.spySupervisor?.trim() && !!targetExtension?.trim() && !this.actionBusy();
  }

  spy(targetExtension: string, mode: ChanSpyMode): void {
    const supervisor = this.spySupervisor?.trim();
    const target = (targetExtension || '').trim();
    if (!supervisor || !target || this.actionBusy()) {
      this.error.set(this.i18n.instant('MONITOR.SPY_NEED_SUPERVISOR'));
      return;
    }
    this.actionBusy.set(true);
    this.success.set(null);
    this.error.set(null);
    this.api
      .chanSpy({
        supervisorExtension: supervisor,
        targetExtension: target,
        mode,
      })
      .subscribe({
        next: (r) => {
          this.actionBusy.set(false);
          if (!r.isSuccess) {
            this.error.set(r.errorMessage || 'ChanSpy failed');
            return;
          }
          this.success.set('MONITOR.SPY_OK');
        },
        error: (err: Error) => {
          this.actionBusy.set(false);
          this.error.set(err.message);
        },
      });
  }

  /** Peer id is usually the extension; strip tech prefix if present. */
  peerSpyTarget(peer: PeerItem): string {
    return this.normalizeExtension(peer.id);
  }

  channelSpyTarget(row: ChannelItem): string | null {
    return this.extensionFromChannel(row.channel);
  }

  extensionFromChannel(channel: string | null | undefined): string | null {
    if (!channel) return null;
    const m = /^(?:PJSIP|SIP|IAX2)\/([^@\-\/]+)/i.exec(channel.trim());
    return m?.[1] ? this.normalizeExtension(m[1]) : null;
  }

  private normalizeExtension(raw: string): string {
    return raw.trim().replace(/^exten[_-]?/i, '');
  }

  selectForBridge(channel: string): void {
    const ch = (channel || '').trim();
    if (!ch || this.actionBusy()) return;
    const a = this.bridgeChannel1();
    const b = this.bridgeChannel2();
    if (a === ch) {
      this.bridgeChannel1.set(null);
      return;
    }
    if (b === ch) {
      this.bridgeChannel2.set(null);
      return;
    }
    if (!a) {
      this.bridgeChannel1.set(ch);
      return;
    }
    if (!b) {
      if (a === ch) return;
      this.bridgeChannel2.set(ch);
      return;
    }
    // Both set — replace second
    this.bridgeChannel2.set(ch);
  }

  clearBridgeSelection(): void {
    this.bridgeChannel1.set(null);
    this.bridgeChannel2.set(null);
  }

  isBridgeSelected(channel: string | null | undefined): boolean {
    if (!channel) return false;
    return this.bridgeChannel1() === channel || this.bridgeChannel2() === channel;
  }

  canRunBridge(): boolean {
    const a = this.bridgeChannel1();
    const b = this.bridgeChannel2();
    return !!a && !!b && a !== b && !this.actionBusy();
  }

  runBridge(): void {
    const a = this.bridgeChannel1();
    const b = this.bridgeChannel2();
    if (!a || !b || a === b || this.actionBusy()) {
      this.error.set(this.i18n.instant('MONITOR.BRIDGE_NEED_TWO'));
      return;
    }
    this.actionBusy.set(true);
    this.success.set(null);
    this.error.set(null);
    this.api.bridgeChannels(a, b, 'no').subscribe({
      next: (r) => {
        this.actionBusy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Bridge failed');
          return;
        }
        this.success.set('MONITOR.BRIDGE_OK');
        this.clearBridgeSelection();
        this.reload();
      },
      error: (err: Error) => {
        this.actionBusy.set(false);
        this.error.set(err.message);
      },
    });
  }

  printTable(): void {
    window.print();
  }

  exportExcel(): void {
    const { columns, rows, fileBase } = this.exportPayload();
    downloadListAsExcel(fileBase, columns, rows);
  }

  exportPdf(): void {
    const { columns, rows, fileBase } = this.exportPayload();
    downloadListAsPdf(fileBase, columns, rows);
  }

  statusTone(status: string | null | undefined): StatusTone {
    const s = (status || '').trim().toLowerCase();
    if (!s) return 'neutral';
    if (s.startsWith('in use') || s.startsWith('inuse') || s.startsWith('dialing') || s.startsWith('busy')) {
      return 'active';
    }
    if (s.startsWith('ringing') || s.startsWith('ring')) {
      return 'warn';
    }
    if (
      s.startsWith('ok') ||
      s === 'reachable' ||
      s === 'registered' ||
      s === 'up' ||
      s === 'completed'
    ) {
      return 'ok';
    }
    if (
      s.startsWith('unreachable') ||
      s.includes('unavailable') ||
      s === 'unregistered' ||
      s === 'failed' ||
      s === 'cancelled' ||
      s === 'down'
    ) {
      return 'fail';
    }
    if (s.startsWith('lagged') || s === 'unknown' || s === 'unmonitored') {
      return 'warn';
    }
    if (
      s === 'bridged' ||
      s.includes('dialing') ||
      s === 'waiting_answer' ||
      s === 'queued' ||
      s === 'ringing'
    ) {
      return 'active';
    }
    return 'neutral';
  }

  peerTone(peer: PeerItem | TrunkItem): StatusTone {
    if (peer.inCall || peer.callState) {
      return this.statusTone(peer.callState || peer.status);
    }
    return this.statusTone(peer.status);
  }

  peerDuration(peer: PeerItem | TrunkItem): number | null {
    return peer.callDurationSeconds ?? null;
  }

  kindLabel(kind: MonitorUnifiedItem['kind']): string {
    if (kind === 'trunk') return this.i18n.instant('MONITOR.KIND_TRUNK');
    if (kind === 'channel') return this.i18n.instant('MONITOR.KIND_CHANNEL');
    return this.i18n.instant('MONITOR.KIND_EXTENSION');
  }

  channelDuration(row: ChannelItem): number | null {
    return row.durationSeconds ?? row.durationSec ?? null;
  }

  private reloadServers(): void {
    this.api.getConnectionStatusList({ pageIndex: 0, pageSize: 100 }).subscribe({
      next: (r) => {
        if (!r.isSuccess) return;
        const list = r.data || [];
        this.servers.set(list);
        const live = list.find((s) => s.isLiveSession) ?? list.find((s) => s.isDefault) ?? null;
        this.activeServerId.set(live?.serverId ?? null);
      },
      error: () => {
        /* strip is additive — monitor list still works */
      },
    });
  }

  private mergeServerLive(live: ConnectionStatus): void {
    const id = live.serverId;
    if (!id) return;
    const next = this.servers().map((row) => {
      if (row.serverId !== id) {
        return { ...row, isLiveSession: false };
      }
      return {
        ...row,
        ...live,
        isLiveSession: true,
        serverId: row.serverId,
        serverName: live.serverName || row.serverName,
      };
    });
    if (!next.some((s) => s.serverId === id) && live.isLiveSession) {
      next.unshift({ ...live, isLiveSession: true });
    }
    this.servers.set(next);
    if (live.isLiveSession) {
      this.activeServerId.set(id);
    }
  }

  private buildUnified(): MonitorUnifiedItem[] {
    const peers = this.peersAll();
    const trunkIds = new Set(
      (this.trunksAll().length ? this.trunksAll() : peers.filter((p) => p.isTrunk)).map((t) =>
        t.id.toLowerCase(),
      ),
    );

    const items: MonitorUnifiedItem[] = [];

    for (const p of peers) {
      const isTrunk = p.isTrunk === true || trunkIds.has(p.id.toLowerCase());
      const tone = this.peerTone(p);
      items.push({
        key: `peer:${p.id}`,
        kind: isTrunk ? 'trunk' : 'extension',
        title: p.id,
        status: p.status || '—',
        tone,
        tech: p.tech,
        ip: p.ip,
        detail: p.callerId || p.channel || null,
        lastActivityUtc: p.lastActivityUtc ?? null,
        durationSec: this.peerDuration(p),
        channel: p.channel ?? null,
        inCall: !!p.inCall,
        spyTarget: isTrunk ? null : this.normalizeExtension(p.id),
      });
    }

    for (const c of this.channelsAll()) {
      items.push({
        key: `ch:${c.channel}`,
        kind: 'channel',
        title: c.channel,
        status: c.state || '—',
        tone: this.statusTone(c.state),
        tech: null,
        ip: null,
        detail: c.callerId || c.application || null,
        lastActivityUtc: c.lastActivityUtc ?? null,
        durationSec: this.channelDuration(c),
        channel: c.channel,
        inCall: true,
        spyTarget: this.extensionFromChannel(c.channel),
      });
    }

    return items;
  }

  private exportPayload(): {
    columns: { key: string; header: string }[];
    rows: Record<string, unknown>[];
    fileBase: string;
  } {
    const t = (key: string) => this.i18n.instant(key);
    const tab = this.tab();
    if (tab === 'all') {
      return {
        fileBase: 'monitor-all',
        columns: [
          { key: 'kind', header: t('MONITOR.COL_KIND') },
          { key: 'title', header: t('MONITOR.COL_ID') },
          { key: 'status', header: t('MONITOR.COL_STATUS') },
          { key: 'tech', header: t('MONITOR.COL_TECH') },
          { key: 'ip', header: t('MONITOR.COL_IP') },
          { key: 'lastActivityUtc', header: t('MONITOR.COL_LAST_ACTIVITY') },
          { key: 'durationSec', header: t('MONITOR.COL_DURATION') },
        ],
        rows: this.unifiedRows() as unknown as Record<string, unknown>[],
      };
    }
    if (tab === 'extensions') {
      return {
        fileBase: 'monitor-extensions',
        columns: [
          { key: 'id', header: t('MONITOR.COL_ID') },
          { key: 'tech', header: t('MONITOR.COL_TECH') },
          { key: 'status', header: t('MONITOR.COL_STATUS') },
          { key: 'ip', header: t('MONITOR.COL_IP') },
          { key: 'channel', header: t('MONITOR.COL_CHANNEL') },
          { key: 'lastActivityUtc', header: t('MONITOR.COL_LAST_ACTIVITY') },
        ],
        rows: this.peersRows() as unknown as Record<string, unknown>[],
      };
    }
    if (tab === 'trunks') {
      return {
        fileBase: 'monitor-trunks',
        columns: [
          { key: 'id', header: t('MONITOR.COL_ID') },
          { key: 'tech', header: t('MONITOR.COL_TECH') },
          { key: 'status', header: t('MONITOR.COL_STATUS') },
          { key: 'ip', header: t('MONITOR.COL_IP') },
          { key: 'lastActivityUtc', header: t('MONITOR.COL_LAST_ACTIVITY') },
        ],
        rows: this.trunksRows() as unknown as Record<string, unknown>[],
      };
    }
    return {
      fileBase: 'monitor-channels',
      columns: [
        { key: 'channel', header: t('MONITOR.COL_CHANNEL') },
        { key: 'uniqueId', header: t('MONITOR.COL_UNIQUE') },
        { key: 'callerId', header: t('MONITOR.COL_CALLER') },
        { key: 'state', header: t('MONITOR.COL_STATE') },
        { key: 'application', header: t('MONITOR.COL_APP') },
        { key: 'durationSeconds', header: t('MONITOR.COL_DURATION') },
        { key: 'lastActivityUtc', header: t('MONITOR.COL_LAST_ACTIVITY') },
      ],
      rows: this.channelsRows() as unknown as Record<string, unknown>[],
    };
  }

  private applyPeersLive(peers: PeerItem[]): void {
    this.peersAll.set(peers);
    this.trunksAll.set(
      peers
        .filter((p) => p.isTrunk === true)
        .map((p) => ({
          id: p.id,
          tech: p.tech,
          status: p.status,
          ip: p.ip,
          lastActivityUtc: p.lastActivityUtc ?? null,
          callState: p.callState ?? null,
          callDurationSeconds: p.callDurationSeconds ?? null,
          callerId: p.callerId ?? null,
          inCall: p.inCall === true,
          channel: p.channel,
        })),
    );
    this.repage();
  }

  private repage(): void {
    const tab = this.tab();
    if (tab === 'all') {
      const { rows, totalCount } = applyClientList(
        this.buildUnified() as unknown as Record<string, unknown>[],
        this.query,
        ['kind', 'title', 'status', 'tech', 'ip', 'detail'],
      );
      this.unifiedRows.set(rows as unknown as MonitorUnifiedItem[]);
      this.totalCount.set(totalCount);
      return;
    }
    if (tab === 'extensions') {
      const { rows, totalCount } = applyClientList(
        this.peersAll() as unknown as Record<string, unknown>[],
        this.query,
        ['id', 'tech', 'status', 'ip', 'channel'],
      );
      this.peersRows.set(rows as unknown as PeerItem[]);
      this.totalCount.set(totalCount);
      return;
    }
    if (tab === 'trunks') {
      const { rows, totalCount } = applyClientList(
        this.trunksAll() as unknown as Record<string, unknown>[],
        this.query,
        ['id', 'tech', 'status', 'ip'],
      );
      this.trunksRows.set(rows as unknown as TrunkItem[]);
      this.totalCount.set(totalCount);
      return;
    }
    const { rows, totalCount } = applyClientList(
      this.channelsAll() as unknown as Record<string, unknown>[],
      this.query,
      ['channel', 'uniqueId', 'callerId', 'state', 'application'],
    );
    this.channelsRows.set(rows as unknown as ChannelItem[]);
    this.totalCount.set(totalCount);
  }
}
