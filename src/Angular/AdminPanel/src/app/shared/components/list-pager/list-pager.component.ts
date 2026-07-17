import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ListQuery } from '../../../core/models/asterisk.models';

@Component({
  selector: 'app-list-pager',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    <div class="list-pager">
      <button type="button" class="btn btn--ghost" (click)="prev()" [disabled]="busy || query.pageIndex <= 0">
        {{ 'LIST.PREV' | translate }}
      </button>
      <span class="list-pager__meta ltr" dir="ltr">
        {{ query.pageIndex + 1 }} / {{ pageCount }} · {{ totalCount }}
      </span>
      <button
        type="button"
        class="btn btn--ghost"
        (click)="next()"
        [disabled]="busy || query.pageIndex + 1 >= pageCount"
      >
        {{ 'LIST.NEXT' | translate }}
      </button>
    </div>
  `,
  styles: [
    `
      .list-pager {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-2);
        justify-content: center;
        align-items: center;
        color: var(--muted);
      }
      .list-pager__meta {
        font-family: var(--font-mono);
        font-variant-numeric: tabular-nums;
      }
      @media print {
        .list-pager {
          display: none !important;
        }
      }
    `,
  ],
})
export class ListPagerComponent {
  @Input({ required: true }) query!: ListQuery;
  @Input() totalCount = 0;
  @Input() busy = false;
  @Output() queryChange = new EventEmitter<ListQuery>();

  get pageCount(): number {
    if (!this.query?.pageSize) return 1;
    return Math.max(1, Math.ceil(this.totalCount / this.query.pageSize));
  }

  prev(): void {
    if (this.query.pageIndex <= 0) return;
    this.queryChange.emit({ ...this.query, pageIndex: this.query.pageIndex - 1 });
  }

  next(): void {
    if (this.query.pageIndex + 1 >= this.pageCount) return;
    this.queryChange.emit({ ...this.query, pageIndex: this.query.pageIndex + 1 });
  }
}
