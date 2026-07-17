import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { CallJob, CallJobStatus } from '../../core/models/call-job';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { CallJobsApi } from '../../core/services/call-jobs.api';

const TERMINAL: CallJobStatus[] = ['completed', 'failed', 'cancelled'];

@Component({
  selector: 'app-jobs-list',
  standalone: true,
  imports: [FormsModule, TranslatePipe, DatePipe],
  templateUrl: './jobs-list.component.html',
  styleUrl: './jobs-list.component.scss',
})
export class JobsListComponent implements OnInit, OnDestroy {
  private readonly api = inject(CallJobsApi);
  private readonly hub = inject(AsteriskHubService);
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
    return !TERMINAL.includes(job.status);
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

  statusKey(status: CallJobStatus): string {
    return `STATUS.${status}`;
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
