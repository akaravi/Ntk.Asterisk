import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { CallJob, CallJobStatus, resolveJobStatus } from '../../core/models/call-job';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { CallJobsApi } from '../../core/services/call-jobs.api';
import { downloadListAsExcel, downloadListAsPdf } from '../../core/utils/list-export.util';

const TERMINAL: CallJobStatus[] = ['completed', 'failed', 'cancelled'];

@Component({
  selector: 'app-jobs-list',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './jobs-list.component.html',
  styleUrl: './jobs-list.component.scss',
})
export class JobsListComponent implements OnInit, OnDestroy {
  private readonly api = inject(CallJobsApi);
  private readonly hub = inject(AsteriskHubService);
  private readonly i18n = inject(I18nService);
  private readonly subs = new Subscription();

  readonly pageSizeOptions = [10, 25, 50, 100];
  readonly jobs = signal<CallJob[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly cancellingId = signal<string | null>(null);

  pageIndex = 0;
  pageSize = 25;
  sortBy: 'updatedAtUtc' | 'createdAtUtc' | 'status' | 'type' = 'updatedAtUtc';
  sortDir: 'asc' | 'desc' = 'desc';
  quickSearch = '';
  advancedOpen = false;
  filterStatus = '';
  filterType = '';
  filterFrom = '';
  filterTo = '';

  ngOnInit(): void {
    this.reload();
    this.subs.add(
      this.hub.jobUpdated$.subscribe((job) => {
        this.mergeJob(job);
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  reload(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.api
      .getList({
        pageIndex: this.pageIndex,
        pageSize: this.pageSize,
        sortBy: this.sortBy,
        sortDir: this.sortDir,
        quickSearch: this.quickSearch.trim() || undefined,
        filter: {
          state: this.filterStatus.trim() || undefined,
          type: this.filterType.trim() || undefined,
          from: this.filterFrom.trim() || undefined,
          to: this.filterTo.trim() || undefined,
        },
      })
      .subscribe({
        next: (result) => {
          this.jobs.set(result.items);
          this.totalCount.set(result.totalCount ?? result.items.length);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.loading.set(false);
          this.errorMessage.set(err instanceof Error ? err.message : String(err));
        },
      });
  }

  onSearch(): void {
    this.pageIndex = 0;
    this.reload();
  }

  onPageSizeChange(): void {
    this.pageIndex = 0;
    this.reload();
  }

  toggleAdvanced(): void {
    this.advancedOpen = !this.advancedOpen;
  }

  toggleSort(column: typeof this.sortBy): void {
    if (this.sortBy === column) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = column;
      this.sortDir = 'desc';
    }
    this.reload();
  }

  prevPage(): void {
    if (this.pageIndex <= 0) {
      return;
    }
    this.pageIndex -= 1;
    this.reload();
  }

  nextPage(): void {
    const maxPage = Math.max(0, Math.ceil(this.totalCount() / this.pageSize) - 1);
    if (this.pageIndex >= maxPage) {
      return;
    }
    this.pageIndex += 1;
    this.reload();
  }

  canCancel(job: CallJob): boolean {
    const status = resolveJobStatus(job);
    return !!status && !TERMINAL.includes(status);
  }

  cancel(job: CallJob): void {
    if (!this.canCancel(job)) {
      return;
    }
    this.cancellingId.set(job.id);
    this.api.actionCancel(job.id).subscribe({
      next: (updated) => {
        this.mergeJob(updated);
        this.cancellingId.set(null);
      },
      error: (err: unknown) => {
        this.cancellingId.set(null);
        this.errorMessage.set(err instanceof Error ? err.message : String(err));
      },
    });
  }

  printTable(): void {
    window.print();
  }

  exportExcel(): void {
    downloadListAsExcel('user-call-jobs', this.exportColumns(), this.exportRows());
  }

  exportPdf(): void {
    downloadListAsPdf(this.i18n.t('JOBS.TITLE'), this.exportColumns(), this.exportRows());
  }

  statusKey(job: CallJob): string {
    const status = resolveJobStatus(job);
    return status ? `STATUS.${status}` : 'STATUS.queued';
  }

  typeKey(type: CallJob['type']): string {
    return `TYPE.${type}`;
  }

  displayFrom(job: CallJob): string {
    return job.from || job.mobile1 || '—';
  }

  displayTo(job: CallJob): string {
    return job.to || job.mobile2 || '—';
  }

  displayReason(job: CallJob): string {
    return job.resultReason || job.errorMessage || '—';
  }

  reasonTone(job: CallJob): 'fail' | 'ok' | 'neutral' {
    const status = resolveJobStatus(job);
    if (status === 'failed' || status === 'cancelled') {
      return 'fail';
    }
    if (status === 'completed' || status === 'bridged') {
      return 'ok';
    }
    return 'neutral';
  }

  callTime(job: CallJob): string | null | undefined {
    return job.callTimeUtc || job.createdAtUtc;
  }

  formatUtc(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) {
      return value;
    }
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
  }

  formatDuration(job: CallJob): string {
    if (job.durationSeconds == null || job.durationSeconds < 0) {
      return '—';
    }
    return this.i18n.t('JOBS.DURATION_SEC').replace('{sec}', String(job.durationSeconds));
  }

  resolveJobStatus = resolveJobStatus;

  private exportColumns(): { key: string; header: string }[] {
    return [
      { key: 'id', header: this.i18n.t('JOBS.COL_ID') },
      { key: 'type', header: this.i18n.t('JOBS.COL_TYPE') },
      { key: 'status', header: this.i18n.t('JOBS.COL_STATUS') },
      { key: 'resultReason', header: this.i18n.t('JOBS.COL_REASON') },
      { key: 'from', header: this.i18n.t('JOBS.COL_FROM') },
      { key: 'to', header: this.i18n.t('JOBS.COL_TO') },
      { key: 'callTimeUtc', header: this.i18n.t('JOBS.COL_CALL_TIME') },
      { key: 'startedAtUtc', header: this.i18n.t('JOBS.COL_STARTED') },
      { key: 'endedAtUtc', header: this.i18n.t('JOBS.COL_ENDED') },
      { key: 'durationSeconds', header: this.i18n.t('JOBS.COL_DURATION') },
      { key: 'updatedAtUtc', header: this.i18n.t('JOBS.COL_UPDATED') },
    ];
  }

  private exportRows(): Record<string, unknown>[] {
    return this.jobs().map((job) => ({
      id: job.id,
      type: job.type,
      status: resolveJobStatus(job) ?? '',
      resultReason: this.displayReason(job),
      from: this.displayFrom(job),
      to: this.displayTo(job),
      callTimeUtc: this.callTime(job) ?? '',
      startedAtUtc: job.startedAtUtc ?? '',
      endedAtUtc: job.endedAtUtc ?? '',
      durationSeconds: job.durationSeconds ?? '',
      updatedAtUtc: job.updatedAtUtc || job.createdAtUtc,
    }));
  }

  private mergeJob(job: CallJob): void {
    const list = [...this.jobs()];
    const idx = list.findIndex((j) => j.id === job.id);
    if (idx >= 0) {
      list[idx] = { ...list[idx], ...job };
      this.jobs.set(list);
      return;
    }
    if (this.pageIndex === 0) {
      this.jobs.set([job, ...list].slice(0, this.pageSize));
      this.totalCount.update((n) => n + 1);
    }
  }
}
