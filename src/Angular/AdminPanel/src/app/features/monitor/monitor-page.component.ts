import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { ChannelItem, ListQuery, PeerItem, TrunkItem } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import {
  AdvancedFilterField,
  ListToolbarComponent,
} from '../../shared/components/list-toolbar/list-toolbar.component';

type MonitorTab = 'extensions' | 'trunks' | 'channels';

@Component({
  selector: 'app-monitor-page',
  standalone: true,
  imports: [TranslatePipe, FormsModule, ListToolbarComponent],
  templateUrl: './monitor-page.component.html',
  styleUrl: './monitor-page.component.scss',
})
export class MonitorPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
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
