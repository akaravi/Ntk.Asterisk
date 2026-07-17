import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { CallJob, ListQuery } from '../../core/models/asterisk.models';
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
  selector: 'app-jobs-page',
  standalone: true,
  imports: [TranslatePipe, ListToolbarComponent, ListPagerComponent, DatePipe],
  templateUrl: './jobs-page.component.html',
  styleUrl: './jobs-page.component.scss',
})
export class JobsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
  private readonly i18n = inject(TranslateService);
  private sub?: Subscription;

  readonly loading = signal(false);
  readonly actionBusyId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly jobsAll = signal<CallJob[]>([]);
  readonly jobsRows = signal<CallJob[]>([]);
  readonly totalCount = signal(0);

  query: ListQuery = { ...defaultListQuery('createdAtUtc'), sortDir: 'desc' };

  readonly filters: AdvancedFilterField[] = [
    { key: 'state', labelKey: 'JOBS.FILTER_STATE', placeholderKey: 'JOBS.FILTER_STATE_PH' },
    { key: 'type', labelKey: 'JOBS.FILTER_TYPE', placeholderKey: 'JOBS.FILTER_TYPE_PH', ltr: true },
    { key: 'from', labelKey: 'JOBS.FILTER_FROM', placeholderKey: 'JOBS.FILTER_FROM_PH', ltr: true },
    { key: 'to', labelKey: 'JOBS.FILTER_TO', placeholderKey: 'JOBS.FILTER_TO_PH', ltr: true },
  ];

  ngOnInit(): void {
    this.reload();
    this.sub = this.hub.hubEvents$.subscribe((ev) => {
      if (ev.kind !== 'job') return;
      const next = [...this.jobsAll()];
      const idx = next.findIndex((j) => j.id === ev.payload.id);
      if (idx >= 0) next[idx] = ev.payload;
      else next.unshift(ev.payload);
      this.jobsAll.set(next);
      this.repage();
    });
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
    this.api.getJobs().subscribe({
      next: (r) => {
        this.loading.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Jobs failed');
          return;
        }
        this.jobsAll.set(r.data || []);
        this.repage();
      },
      error: (err: Error) => {
        this.loading.set(false);
        this.error.set(err.message);
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

  canCancel(job: CallJob): boolean {
    const terminal = ['completed', 'failed', 'cancelled'];
    return !terminal.includes(String(job.state).toLowerCase());
  }

  canDownloadRecording(job: CallJob): boolean {
    return !!(job.hasRecording || job.recordingFileName || job.recordingAvailable);
  }

  downloadRecording(job: CallJob): void {
    if (!this.canDownloadRecording(job) || this.actionBusyId()) return;
    this.actionBusyId.set(job.id);
    this.error.set(null);
    this.success.set(null);
    this.api.downloadJobRecording(job.id).subscribe({
      next: (blob) => {
        this.actionBusyId.set(null);
        if (blob.type && blob.type.includes('json')) {
          blob.text().then((text) => {
            try {
              const parsed = JSON.parse(text) as { errorMessage?: string };
              this.error.set(parsed.errorMessage || 'JOBS.DOWNLOAD_FAIL');
            } catch {
              this.error.set('JOBS.DOWNLOAD_FAIL');
            }
          });
          return;
        }
        const name = job.recordingFileName || `ntk-${job.id}.wav`;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        a.rel = 'noopener';
        a.click();
        URL.revokeObjectURL(url);
        this.success.set('JOBS.DOWNLOAD_OK');
      },
      error: (err: Error) => {
        this.actionBusyId.set(null);
        this.error.set(err.message || 'JOBS.DOWNLOAD_FAIL');
      },
    });
  }

  normalizeState(state: string | null | undefined): string {
    return String(state || '').trim().toLowerCase();
  }

  typeLabel(type: string | null | undefined): string {
    const raw = String(type || '').trim();
    if (!raw) return '—';
    const key = `JOBS.TYPE.${raw}`;
    const translated = this.i18n.instant(key);
    return translated === key ? raw : translated;
  }

  stateLabel(state: string | null | undefined): string {
    const raw = this.normalizeState(state);
    if (!raw) return '—';
    const key = `JOBS.STATUS.${raw}`;
    const translated = this.i18n.instant(key);
    return translated === key ? String(state) : translated;
  }

  displayFrom(job: CallJob): string {
    return job.from || job.mobile1 || '—';
  }

  displayTo(job: CallJob): string {
    return job.to || job.mobile2 || '—';
  }

  reason(job: CallJob): string {
    return job.resultReason || job.errorMessage || '—';
  }

  reasonTone(job: CallJob): 'fail' | 'ok' | 'neutral' {
    const state = this.normalizeState(job.state);
    if (state === 'failed' || state === 'cancelled') return 'fail';
    if (state === 'completed' || state === 'bridged') return 'ok';
    return 'neutral';
  }

  cancel(job: CallJob): void {
    if (!this.canCancel(job) || this.actionBusyId()) return;
    this.actionBusyId.set(job.id);
    this.error.set(null);
    this.success.set(null);
    this.api.cancelJob(job.id).subscribe({
      next: (r) => {
        this.actionBusyId.set(null);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'Cancel failed');
          return;
        }
        this.success.set('JOBS.CANCEL_OK');
        if (r.data?.[0]) {
          const next = this.jobsAll().map((j) => (j.id === r.data[0].id ? r.data[0] : j));
          this.jobsAll.set(next);
          this.repage();
        } else {
          this.reload();
        }
      },
      error: (err: Error) => {
        this.actionBusyId.set(null);
        this.error.set(err.message);
      },
    });
  }

  printTable(): void {
    window.print();
  }

  exportExcel(): void {
    const t = (key: string) => this.i18n.instant(key);
    downloadListAsExcel(
      'call-jobs',
      [
        { key: 'id', header: t('JOBS.COL_ID') },
        { key: 'type', header: t('JOBS.COL_TYPE') },
        { key: 'state', header: t('JOBS.COL_STATE') },
        { key: 'resultReason', header: t('JOBS.COL_REASON') },
        { key: 'from', header: t('JOBS.COL_FROM') },
        { key: 'to', header: t('JOBS.COL_TO') },
        { key: 'callTimeUtc', header: t('JOBS.COL_CALL_TIME') },
        { key: 'startedAtUtc', header: t('JOBS.COL_STARTED') },
        { key: 'endedAtUtc', header: t('JOBS.COL_ENDED') },
        { key: 'durationSeconds', header: t('JOBS.COL_DURATION') },
        { key: 'updatedAtUtc', header: t('JOBS.COL_UPDATED') },
      ],
      this.jobsRows().map((job) => ({
        ...job,
        resultReason: this.reason(job),
        callTimeUtc: job.callTimeUtc || job.createdAtUtc,
        from: job.from || job.mobile1 || '',
        to: job.to || job.mobile2 || '',
      })) as unknown as Record<string, unknown>[]
    );
  }

  exportPdf(): void {
    const t = (key: string) => this.i18n.instant(key);
    downloadListAsPdf(
      t('NAV.JOBS'),
      [
        { key: 'id', header: t('JOBS.COL_ID') },
        { key: 'type', header: t('JOBS.COL_TYPE') },
        { key: 'state', header: t('JOBS.COL_STATE') },
        { key: 'resultReason', header: t('JOBS.COL_REASON') },
        { key: 'from', header: t('JOBS.COL_FROM') },
        { key: 'to', header: t('JOBS.COL_TO') },
        { key: 'callTimeUtc', header: t('JOBS.COL_CALL_TIME') },
        { key: 'startedAtUtc', header: t('JOBS.COL_STARTED') },
        { key: 'endedAtUtc', header: t('JOBS.COL_ENDED') },
        { key: 'durationSeconds', header: t('JOBS.COL_DURATION') },
        { key: 'updatedAtUtc', header: t('JOBS.COL_UPDATED') },
      ],
      this.jobsRows().map((job) => ({
        ...job,
        resultReason: this.reason(job),
        callTimeUtc: job.callTimeUtc || job.createdAtUtc,
        from: job.from || job.mobile1 || '',
        to: job.to || job.mobile2 || '',
      })) as unknown as Record<string, unknown>[]
    );
  }

  private repage(): void {
    const { rows, totalCount } = applyClientList(
      this.jobsAll() as unknown as Record<string, unknown>[],
      this.query,
      [
        'id',
        'type',
        'state',
        'from',
        'to',
        'mobile1',
        'mobile2',
        'errorMessage',
        'resultReason',
      ]
    );
    this.jobsRows.set(rows as unknown as CallJob[]);
    this.totalCount.set(totalCount);
  }
}
