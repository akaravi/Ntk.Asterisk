import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { QueueStatsSample } from '../../core/models/queue-acl.models';
import { QueueAclAuthService } from '../../core/services/queue-acl-auth.service';
import { downloadListAsExcel, downloadListAsPdf } from '../../core/utils/list-export.util';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import { ListQuery } from '../../core/models/asterisk.models';
import {
  AdvancedFilterField,
  ListToolbarComponent,
} from '../../shared/components/list-toolbar/list-toolbar.component';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';

@Component({
  selector: 'app-queue-stats-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, ListToolbarComponent, ListPagerComponent, DatePipe, DecimalPipe],
  templateUrl: './queue-stats-page.component.html',
  styleUrl: './queue-stats-page.component.scss',
})
export class QueueStatsPageComponent implements OnInit {
  private readonly auth = inject(QueueAclAuthService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly samplesAll = signal<QueueStatsSample[]>([]);
  readonly samplesRows = signal<QueueStatsSample[]>([]);
  readonly totalCount = signal(0);

  queueFilter = '';
  query: ListQuery = { ...defaultListQuery('snapshotUtc'), pageSize: 50, sortDir: 'desc' };
  readonly filters: AdvancedFilterField[] = [
    { key: 'name', labelKey: 'QUEUE_STATS.FILTER_QUEUE', placeholderKey: 'QUEUE_STATS.FILTER_QUEUE_PH', ltr: true },
  ];

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const to = new Date();
    const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);
    this.auth
      .getStats({
        from: from.toISOString(),
        to: to.toISOString(),
        queue: this.queueFilter.trim() || undefined,
      })
      .subscribe({
        next: (r) => {
          this.loading.set(false);
          if (!r.isSuccess) {
            this.error.set(r.errorMessage || this.i18n.instant('QUEUE_STATS.LOAD_FAIL'));
            return;
          }
          this.samplesAll.set(r.data ?? []);
          this.applyList();
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(err?.message || this.i18n.instant('QUEUE_STATS.LOAD_FAIL'));
        },
      });
  }

  onQueryChange(q: ListQuery): void {
    this.query = q;
    this.applyList();
  }

  exportPdf(): void {
    downloadListAsPdf(
      'queue-stats',
      [
        { key: 'snapshotUtc', header: 'UTC' },
        { key: 'name', header: 'Queue' },
        { key: 'callsWaiting', header: 'Waiting' },
        { key: 'completed', header: 'Completed' },
        { key: 'abandoned', header: 'Abandoned' },
        { key: 'serviceLevelPerf', header: 'SL%' },
      ],
      this.samplesRows().map((s) => ({
        snapshotUtc: s.snapshotUtc,
        name: s.name,
        callsWaiting: s.callsWaiting,
        completed: s.completed,
        abandoned: s.abandoned,
        serviceLevelPerf: s.serviceLevelPerf,
      })),
    );
  }

  exportExcel(): void {
    downloadListAsExcel(
      'queue-stats',
      [
        { key: 'snapshotUtc', header: 'UTC' },
        { key: 'name', header: 'Queue' },
        { key: 'realName', header: 'AMI' },
        { key: 'callsWaiting', header: 'Waiting' },
        { key: 'completed', header: 'Completed' },
        { key: 'abandoned', header: 'Abandoned' },
        { key: 'serviceLevelPerf', header: 'SL%' },
      ],
      this.samplesRows().map((s) => ({ ...s })),
    );
  }

  private applyList(): void {
    const { rows, totalCount } = applyClientList(
      this.samplesAll() as unknown as Record<string, unknown>[],
      this.query,
      ['name', 'realName'],
    );
    this.samplesRows.set(rows as unknown as QueueStatsSample[]);
    this.totalCount.set(totalCount);
  }
}
