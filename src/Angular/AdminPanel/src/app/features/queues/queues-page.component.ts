import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { ChanSpyMode, ListQuery } from '../../core/models/asterisk.models';
import { QueueEntryItem, QueueItem, QueueMemberItem } from '../../core/models/queue.models';
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
  selector: 'app-queues-page',
  standalone: true,
  imports: [TranslatePipe, FormsModule, ListToolbarComponent, ListPagerComponent, DatePipe],
  templateUrl: './queues-page.component.html',
  styleUrl: './queues-page.component.scss',
})
export class QueuesPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
  private readonly i18n = inject(TranslateService);
  private sub?: Subscription;

  readonly loading = signal(false);
  readonly actionBusy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  readonly queuesAll = signal<QueueItem[]>([]);
  readonly queuesRows = signal<QueueItem[]>([]);
  readonly totalCount = signal(0);
  readonly expanded = signal<string | null>(null);

  spySupervisor = '';
  pauseReason = '';

  query: ListQuery = { ...defaultListQuery('name'), pageSize: 25, sortDir: 'asc' };

  readonly filters: AdvancedFilterField[] = [
    {
      key: 'strategy',
      labelKey: 'QUEUES.FILTER_STRATEGY',
      placeholderKey: 'QUEUES.FILTER_STRATEGY_PH',
      ltr: true,
    },
  ];

  readonly spyModes: { mode: ChanSpyMode; labelKey: string }[] = [
    { mode: 'listen', labelKey: 'MONITOR.SPY_LISTEN' },
    { mode: 'whisper', labelKey: 'MONITOR.SPY_WHISPER' },
    { mode: 'barge', labelKey: 'MONITOR.SPY_BARGE' },
  ];

  ngOnInit(): void {
    this.spySupervisor = localStorage.getItem('ntk.monitor.spySupervisor') || '';
    void this.hub.subscribeQueues();
    this.sub = this.hub.hubEvents$.subscribe((ev) => {
      if (ev.kind === 'queues') {
        this.queuesAll.set(ev.payload ?? []);
        this.applyList();
      }
    });
    this.reload();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  onSpySupervisorChange(): void {
    localStorage.setItem('ntk.monitor.spySupervisor', this.spySupervisor.trim());
  }

  onQueryChange(q: ListQuery): void {
    this.query = q;
    this.applyList();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getQueues().subscribe({
      next: (r) => {
        this.loading.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('QUEUES.LOAD_FAIL'));
          return;
        }
        this.queuesAll.set(r.data ?? []);
        this.applyList();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.message || this.i18n.instant('QUEUES.LOAD_FAIL'));
      },
    });
  }

  toggleExpand(name: string): void {
    this.expanded.set(this.expanded() === name ? null : name);
  }

  pauseMember(queue: QueueItem, member: QueueMemberItem): void {
    this.runAction(
      this.api.pauseQueueMember({
        interface: member.interface,
        queue: this.amiQueueName(queue),
        reason: this.pauseReason.trim() || undefined,
      }),
      'QUEUES.PAUSE_OK',
    );
  }

  unpauseMember(queue: QueueItem, member: QueueMemberItem): void {
    this.runAction(
      this.api.unpauseQueueMember({
        interface: member.interface,
        queue: this.amiQueueName(queue),
      }),
      'QUEUES.UNPAUSE_OK',
    );
  }

  hangupEntry(entry: QueueEntryItem): void {
    if (!entry.channel) return;
    this.runAction(this.api.hangupQueueEntry(entry.channel), 'QUEUES.HANGUP_OK');
  }

  spyOnMember(member: QueueMemberItem, mode: ChanSpyMode): void {
    const supervisor = this.spySupervisor.trim();
    if (!supervisor) {
      this.error.set(this.i18n.instant('QUEUES.SPY_NEED_SUPERVISOR'));
      return;
    }
    const target = this.extensionFromInterface(member.stateInterface || member.interface);
    if (!target) {
      this.error.set(this.i18n.instant('QUEUES.SPY_NEED_TARGET'));
      return;
    }
    this.actionBusy.set(true);
    this.error.set(null);
    this.api.chanSpy({ supervisorExtension: supervisor, targetExtension: target, mode }).subscribe({
      next: (r) => {
        this.actionBusy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('QUEUES.SPY_FAIL'));
          return;
        }
        this.flashSuccess('QUEUES.SPY_OK');
      },
      error: (err) => {
        this.actionBusy.set(false);
        this.error.set(err?.message || this.i18n.instant('QUEUES.SPY_FAIL'));
      },
    });
  }

  printTable(): void {
    window.print();
  }

  exportPdf(): void {
    const columns = [
      { key: 'name', header: 'Name' },
      { key: 'strategy', header: 'Strategy' },
      { key: 'callsWaiting', header: 'Waiting' },
      { key: 'completed', header: 'Completed' },
      { key: 'abandoned', header: 'Abandoned' },
      { key: 'serviceLevelPerf', header: 'SL%' },
    ];
    downloadListAsPdf(
      'queues',
      columns,
      this.queuesRows().map((q) => ({
        name: q.name,
        strategy: q.strategy ?? '',
        callsWaiting: q.callsWaiting,
        completed: q.completed,
        abandoned: q.abandoned,
        serviceLevelPerf: q.serviceLevelPerf,
      })),
    );
  }

  exportExcel(): void {
    const columns = [
      { key: 'name', header: 'Name' },
      { key: 'strategy', header: 'Strategy' },
      { key: 'callsWaiting', header: 'Waiting' },
      { key: 'completed', header: 'Completed' },
      { key: 'abandoned', header: 'Abandoned' },
      { key: 'serviceLevelPerf', header: 'SL%' },
    ];
    downloadListAsExcel(
      'queues',
      columns,
      this.queuesRows().map((q) => ({
        name: q.name,
        strategy: q.strategy ?? '',
        callsWaiting: q.callsWaiting,
        completed: q.completed,
        abandoned: q.abandoned,
        serviceLevelPerf: q.serviceLevelPerf,
      })),
    );
  }

  private applyList(): void {
    const { rows, totalCount } = applyClientList(this.queuesAll(), this.query, [
      'name',
      'strategy',
    ]);
    this.queuesRows.set(rows);
    this.totalCount.set(totalCount);
  }

  /** AMI queue name for actions — display `name` may be renamed. */
  private amiQueueName(queue: QueueItem): string {
    return (queue.realName || queue.name || '').trim();
  }

  private runAction(obs: ReturnType<AsteriskApiService['pauseQueueMember']>, okKey: string): void {
    this.actionBusy.set(true);
    this.error.set(null);
    obs.subscribe({
      next: (r) => {
        this.actionBusy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('QUEUES.ACTION_FAIL'));
          return;
        }
        this.flashSuccess(okKey);
        this.reload();
      },
      error: (err) => {
        this.actionBusy.set(false);
        this.error.set(err?.message || this.i18n.instant('QUEUES.ACTION_FAIL'));
      },
    });
  }

  private flashSuccess(key: string): void {
    this.success.set(key);
    setTimeout(() => this.success.set(null), 2500);
  }

  private extensionFromInterface(iface: string): string | null {
    const s = (iface || '').trim();
    if (!s) return null;
    const slash = s.lastIndexOf('/');
    const raw = slash >= 0 ? s.slice(slash + 1) : s;
    const at = raw.indexOf('@');
    return (at >= 0 ? raw.slice(0, at) : raw).trim() || null;
  }
}
