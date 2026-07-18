import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { ConnectionStatus, ListQuery } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { downloadListAsExcel, downloadListAsPdf } from '../../core/utils/list-export.util';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import {
  AdvancedFilterField,
  ListToolbarComponent,
} from '../../shared/components/list-toolbar/list-toolbar.component';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';

@Component({
  selector: 'app-connection-page',
  standalone: true,
  imports: [TranslatePipe, DatePipe, ListToolbarComponent, ListPagerComponent],
  templateUrl: './connection-page.component.html',
  styleUrl: './connection-page.component.scss',
})
export class ConnectionPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
  private readonly i18n = inject(TranslateService);
  private sub?: Subscription;

  readonly hubConnected = signal(false);
  readonly liveStatus = signal<ConnectionStatus | null>(null);
  readonly rowsAll = signal<ConnectionStatus[]>([]);
  readonly rows = signal<ConnectionStatus[]>([]);
  readonly totalCount = signal(0);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly refreshedAt = signal<Date | null>(null);

  query: ListQuery = { ...defaultListQuery('serverName'), sortDir: 'asc' };

  readonly filters: AdvancedFilterField[] = [
    {
      key: 'connected',
      labelKey: 'CONNECTION.FILTER_CONNECTED',
      placeholderKey: 'CONNECTION.FILTER_CONNECTED_PH',
    },
    {
      key: 'host',
      labelKey: 'CONNECTION.FILTER_HOST',
      placeholderKey: 'CONNECTION.FILTER_HOST_PH',
      ltr: true,
    },
  ];

  ngOnInit(): void {
    this.reload();
    this.sub = new Subscription();
    this.sub.add(this.hub.hubConnected$.subscribe((v) => this.hubConnected.set(v)));
    this.sub.add(
      this.hub.hubEvents$.subscribe((ev) => {
        if (ev.kind === 'connection') {
          this.liveStatus.set(ev.payload);
          this.mergeLiveIntoRows(ev.payload);
          this.refreshedAt.set(new Date());
        }
      }),
    );
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  onQueryChange(q: ListQuery): void {
    this.query = q;
    this.repage();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getConnectionStatusList({ pageIndex: 0, pageSize: 100 }).subscribe({
      next: (r) => {
        this.loading.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Connection list failed');
          return;
        }
        this.rowsAll.set(r.data || []);
        this.liveStatus.set(r.data?.find((s) => s.isLiveSession) ?? r.data?.[0] ?? null);
        this.repage();
        this.refreshedAt.set(new Date());
      },
      error: (err: Error) => {
        this.loading.set(false);
        this.error.set(err.message);
      },
    });
  }

  printTable(): void {
    window.print();
  }

  exportPdf(): void {
    const t = (key: string) => this.i18n.instant(key);
    downloadListAsPdf(
      t('NAV.CONNECTION'),
      [
        { key: 'serverName', header: t('CONNECTION.COL_SERVER') },
        { key: 'host', header: t('CONNECTION.COL_HOST') },
        { key: 'username', header: t('CONNECTION.COL_USER') },
        { key: 'connected', header: t('CONNECTION.COL_STATUS') },
        { key: 'isDefault', header: t('CONNECTION.COL_DEFAULT') },
        { key: 'isLiveSession', header: t('CONNECTION.COL_LIVE') },
        { key: 'asteriskVersion', header: t('CONNECTION.COL_VERSION') },
        { key: 'lastError', header: t('CONNECTION.COL_ERROR') },
      ],
      this.rows() as unknown as Record<string, unknown>[],
    );
  }

  exportExcel(): void {
    const t = (key: string) => this.i18n.instant(key);
    downloadListAsExcel(
      'ami-servers-connection',
      [
        { key: 'serverName', header: t('CONNECTION.COL_SERVER') },
        { key: 'host', header: t('CONNECTION.COL_HOST') },
        { key: 'username', header: t('CONNECTION.COL_USER') },
        { key: 'connected', header: t('CONNECTION.COL_STATUS') },
        { key: 'isDefault', header: t('CONNECTION.COL_DEFAULT') },
        { key: 'isLiveSession', header: t('CONNECTION.COL_LIVE') },
        { key: 'asteriskVersion', header: t('CONNECTION.COL_VERSION') },
        { key: 'lastError', header: t('CONNECTION.COL_ERROR') },
      ],
      this.rows() as unknown as Record<string, unknown>[],
    );
  }

  endpoint(s: ConnectionStatus): string {
    return `${s.host || '—'}:${s.port || '—'}`;
  }

  private mergeLiveIntoRows(live: ConnectionStatus): void {
    const id = live.serverId;
    if (!id) return;
    const next = this.rowsAll().map((row) =>
      row.serverId === id
        ? {
            ...row,
            connected: live.connected,
            lastError: live.lastError,
            uptimeSeconds: live.uptimeSeconds,
            asteriskVersion: live.asteriskVersion,
            isLiveSession: true,
          }
        : row,
    );
    this.rowsAll.set(next);
    this.repage();
  }

  private repage(): void {
    const { rows, totalCount } = applyClientList(
      this.rowsAll() as unknown as Record<string, unknown>[],
      this.query,
      ['serverName', 'host', 'username', 'lastError', 'channelTech', 'serverId'],
    );
    this.rows.set(rows as unknown as ConnectionStatus[]);
    this.totalCount.set(totalCount);
  }
}
