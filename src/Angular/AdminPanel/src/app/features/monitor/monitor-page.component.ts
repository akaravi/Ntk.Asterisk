import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { ChannelItem, ListQuery, PeerItem, TrunkItem } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { downloadListAsExcel, downloadListAsPdf } from '../../core/utils/list-export.util';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import {
  AdvancedFilterField,
  ListToolbarComponent,
} from '../../shared/components/list-toolbar/list-toolbar.component';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';

type MonitorTab = 'extensions' | 'trunks' | 'channels';

@Component({
  selector: 'app-monitor-page',
  standalone: true,
  imports: [TranslatePipe, FormsModule, ListToolbarComponent, ListPagerComponent],
  templateUrl: './monitor-page.component.html',
  styleUrl: './monitor-page.component.scss',
})
export class MonitorPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
  private readonly i18n = inject(TranslateService);
  private sub?: Subscription;

  readonly tab = signal<MonitorTab>('extensions');
  readonly loading = signal(false);
  readonly actionBusy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  readonly peersAll = signal<PeerItem[]>([]);
  readonly trunksAll = signal<TrunkItem[]>([]);
  readonly channelsAll = signal<ChannelItem[]>([]);

  readonly peersRows = signal<PeerItem[]>([]);
  readonly trunksRows = signal<TrunkItem[]>([]);
  readonly channelsRows = signal<ChannelItem[]>([]);
  readonly totalCount = signal(0);

  query: ListQuery = defaultListQuery('id');

  readonly peerFilters: AdvancedFilterField[] = [
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
    this.reload();
    this.sub = this.hub.hubEvents$.subscribe((ev) => {
      if (ev.kind === 'peers') {
        this.peersAll.set(ev.payload);
        this.repage();
      }
      if (ev.kind === 'channels') {
        this.channelsAll.set(ev.payload);
        this.repage();
      }
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  setTab(tab: MonitorTab): void {
    this.tab.set(tab);
    this.query = defaultListQuery(tab === 'channels' ? 'channel' : 'id');
    this.repage();
  }

  onQueryChange(q: ListQuery): void {
    this.query = q;
    this.repage();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
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
        this.peersAll.set(r.data || []);
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
        this.trunksAll.set(r.data || []);
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

  sort(column: string): void {
    if (this.query.sortBy === column) {
      this.query = {
        ...this.query,
        sortDir: this.query.sortDir === 'asc' ? 'desc' : 'asc',
      };
    } else {
      this.query = { ...this.query, sortBy: column, sortDir: 'asc' };
    }
    this.repage();
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

  private exportPayload(): {
    columns: { key: string; header: string }[];
    rows: Record<string, unknown>[];
    fileBase: string;
  } {
    const t = (key: string) => this.i18n.instant(key);
    const tab = this.tab();
    if (tab === 'extensions') {
      return {
        fileBase: 'monitor-extensions',
        columns: [
          { key: 'id', header: t('MONITOR.COL_ID') },
          { key: 'tech', header: t('MONITOR.COL_TECH') },
          { key: 'status', header: t('MONITOR.COL_STATUS') },
          { key: 'ip', header: t('MONITOR.COL_IP') },
          { key: 'channel', header: t('MONITOR.COL_CHANNEL') },
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
      ],
      rows: this.channelsRows() as unknown as Record<string, unknown>[],
    };
  }

  private repage(): void {
    const tab = this.tab();
    if (tab === 'extensions') {
      const { rows, totalCount } = applyClientList(
        this.peersAll() as unknown as Record<string, unknown>[],
        this.query,
        ['id', 'tech', 'status', 'ip', 'channel']
      );
      this.peersRows.set(rows as unknown as PeerItem[]);
      this.totalCount.set(totalCount);
      return;
    }
    if (tab === 'trunks') {
      const { rows, totalCount } = applyClientList(
        this.trunksAll() as unknown as Record<string, unknown>[],
        this.query,
        ['id', 'tech', 'status', 'ip']
      );
      this.trunksRows.set(rows as unknown as TrunkItem[]);
      this.totalCount.set(totalCount);
      return;
    }
    const { rows, totalCount } = applyClientList(
      this.channelsAll() as unknown as Record<string, unknown>[],
      this.query,
      ['channel', 'uniqueId', 'callerId', 'state', 'application']
    );
    this.channelsRows.set(rows as unknown as ChannelItem[]);
    this.totalCount.set(totalCount);
  }
}
