import { NgClass } from '@angular/common';
import {
  AfterViewChecked,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { ListQuery, LiveEventItem } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { downloadListAsExcel, downloadListAsPdf } from '../../core/utils/list-export.util';
import { defaultListQuery } from '../../core/utils/list-query.util';
import {
  AdvancedFilterField,
  ListToolbarComponent,
} from '../../shared/components/list-toolbar/list-toolbar.component';

@Component({
  selector: 'app-live-events-page',
  standalone: true,
  imports: [NgClass, TranslatePipe, FormsModule, ListToolbarComponent],
  templateUrl: './live-events-page.component.html',
  styleUrl: './live-events-page.component.scss',
})
export class LiveEventsPageComponent implements OnInit, OnDestroy, AfterViewChecked {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
  private readonly i18n = inject(TranslateService);
  private sub?: Subscription;
  private shouldStickBottom = false;

  @ViewChild('feedEl') feedEl?: ElementRef<HTMLElement>;

  readonly loading = signal(false);
  readonly actionBusy = signal(false);
  readonly error = signal<string | null>(null);
  readonly paused = signal(false);
  readonly autoScroll = signal(true);
  readonly liveOk = signal(false);
  readonly events = signal<LiveEventItem[]>([]);
  readonly visible = signal<LiveEventItem[]>([]);
  readonly totalCount = signal(0);
  readonly selected = signal<LiveEventItem | null>(null);

  query: ListQuery = { ...defaultListQuery('atUtc'), pageSize: 200, sortDir: 'desc' };

  readonly filters: AdvancedFilterField[] = [
    {
      key: 'source',
      labelKey: 'EVENTS.FILTER_SOURCE',
      placeholderKey: 'EVENTS.FILTER_SOURCE_PH',
      ltr: true,
    },
    {
      key: 'level',
      labelKey: 'EVENTS.FILTER_LEVEL',
      placeholderKey: 'EVENTS.FILTER_LEVEL_PH',
      ltr: true,
    },
  ];

  ngOnInit(): void {
    void this.hub.start().then(() => this.hub.subscribeEvents());
    this.hub.hubConnected$.subscribe((v) => this.liveOk.set(v));
    this.sub = this.hub.hubEvents$.subscribe((ev) => {
      if (ev.kind !== 'liveEvent') return;
      if (this.paused()) return;
      this.error.set(null);
      this.prependLive(ev.payload);
    });
    this.reload();
  }

  ngAfterViewChecked(): void {
    if (!this.shouldStickBottom || !this.autoScroll() || this.paused()) return;
    const el = this.feedEl?.nativeElement;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
    this.shouldStickBottom = false;
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    void this.hub.unsubscribeEvents();
  }

  onQueryChange(q: ListQuery): void {
    this.query = q;
    this.applyFilter();
  }

  reload(retryOn404 = true): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .getEvents({
        pageIndex: 0,
        pageSize: 500,
        sortBy: 'atUtc',
        sortDir: 'desc',
        quickSearch: this.query.quickSearch || '',
        filter: { ...(this.query.filter || {}) },
      })
      .subscribe({
        next: (r) => {
          this.loading.set(false);
          if (!r.isSuccess) {
            this.error.set(r.errorMessage || 'Load failed');
            return;
          }
          // API returns newest-first; keep chronological for feed (oldest at top).
          const rows = [...(r.data ?? [])].reverse();
          this.events.set(rows);
          this.applyFilter();
          this.shouldStickBottom = true;
        },
        error: (err: Error) => {
          this.loading.set(false);
          const msg = err.message || String(err);
          // Stale WebApi without EventsController — retry once after brief delay.
          if (retryOn404 && /404/.test(msg)) {
            window.setTimeout(() => this.reload(false), 1500);
            this.error.set(msg);
            return;
          }
          this.error.set(msg);
        },
      });
  }

  togglePause(): void {
    this.paused.update((v) => !v);
  }

  toggleAutoScroll(): void {
    this.autoScroll.update((v) => !v);
    if (this.autoScroll()) this.shouldStickBottom = true;
  }

  clearBuffer(): void {
    this.actionBusy.set(true);
    this.error.set(null);
    this.api.clearEvents().subscribe({
      next: (r) => {
        this.actionBusy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Clear failed');
          return;
        }
        this.events.set([]);
        this.visible.set([]);
        this.totalCount.set(0);
        this.selected.set(null);
      },
      error: (err: Error) => {
        this.actionBusy.set(false);
        this.error.set(err.message);
      },
    });
  }

  select(row: LiveEventItem): void {
    this.selected.set(row);
  }

  exportPdf(): void {
    const cols = this.exportColumns();
    downloadListAsPdf(
      this.i18n.instant('NAV.EVENTS'),
      cols,
      this.visible().map((e) => e as unknown as Record<string, unknown>)
    );
  }

  exportExcel(): void {
    downloadListAsExcel(
      'live-events',
      this.exportColumns(),
      this.visible().map((e) => e as unknown as Record<string, unknown>)
    );
  }

  printTable(): void {
    this.exportPdf();
  }

  levelClass(level: string): string {
    const l = (level || '').toLowerCase();
    if (l === 'error' || l === 'critical') return 'lvl-error';
    if (l === 'warning') return 'lvl-warn';
    if (l === 'event') return 'lvl-event';
    if (l === 'information') return 'lvl-info';
    return 'lvl-muted';
  }

  sourceClass(source: string): string {
    const s = (source || '').toLowerCase();
    if (s === 'ami') return 'src-ami';
    if (s === 'app') return 'src-app';
    if (s === 'system') return 'src-system';
    return '';
  }

  formatTime(iso: string): string {
    try {
      return new Date(iso).toISOString().replace('T', ' ').replace('Z', ' UTC');
    } catch {
      return iso;
    }
  }

  attrEntries(row: LiveEventItem): { key: string; value: string }[] {
    if (!row.attributes) return [];
    return Object.entries(row.attributes).map(([key, value]) => ({ key, value }));
  }

  private prependLive(item: LiveEventItem): void {
    const next = [...this.events(), item];
    const capped = next.length > 2000 ? next.slice(next.length - 2000) : next;
    this.events.set(capped);
    this.applyFilter();
    this.shouldStickBottom = true;
  }

  private applyFilter(): void {
    const q = (this.query.quickSearch || '').trim().toLowerCase();
    const source = (this.query.filter?.['source'] || '').trim().toLowerCase();
    const level = (this.query.filter?.['level'] || '').trim().toLowerCase();

    let rows = this.events();
    if (source) rows = rows.filter((e) => (e.source || '').toLowerCase().includes(source));
    if (level) rows = rows.filter((e) => (e.level || '').toLowerCase().includes(level));
    if (q) {
      rows = rows.filter((e) => {
        const hay = [
          e.id,
          e.source,
          e.category,
          e.level,
          e.message,
          e.channel,
          e.uniqueId,
          e.privilege,
          ...(e.attributes ? Object.entries(e.attributes).flat() : []),
        ]
          .filter(Boolean)
          .join(' ')
          .toLowerCase();
        return hay.includes(q);
      });
    }

    this.visible.set(rows);
    this.totalCount.set(rows.length);
  }

  private exportColumns() {
    return [
      { key: 'atUtc', header: this.i18n.instant('EVENTS.COL_TIME') },
      { key: 'source', header: this.i18n.instant('EVENTS.COL_SOURCE') },
      { key: 'level', header: this.i18n.instant('EVENTS.COL_LEVEL') },
      { key: 'category', header: this.i18n.instant('EVENTS.COL_CATEGORY') },
      { key: 'message', header: this.i18n.instant('EVENTS.COL_MESSAGE') },
      { key: 'channel', header: this.i18n.instant('EVENTS.COL_CHANNEL') },
    ];
  }
}
