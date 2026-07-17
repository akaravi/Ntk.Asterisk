import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ListQuery } from '../../../core/models/asterisk.models';
import { pageSizeOptions } from '../../../core/utils/list-query.util';

export interface AdvancedFilterField {
  key: string;
  labelKey: string;
  placeholderKey: string;
  ltr?: boolean;
}

@Component({
  selector: 'app-list-toolbar',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './list-toolbar.component.html',
  styleUrl: './list-toolbar.component.scss',
})
export class ListToolbarComponent {
  @Input({ required: true }) query!: ListQuery;
  @Input() totalCount = 0;
  @Input() advancedFields: AdvancedFilterField[] = [];
  @Input() busy = false;
  @Input() showPrint = true;

  @Output() queryChange = new EventEmitter<ListQuery>();
  @Output() refresh = new EventEmitter<void>();
  @Output() print = new EventEmitter<void>();

  advancedOpen = false;
  readonly pageSizes = pageSizeOptions();

  get pageCount(): number {
    if (!this.query?.pageSize) return 1;
    return Math.max(1, Math.ceil(this.totalCount / this.query.pageSize));
  }

  emit(): void {
    this.queryChange.emit({ ...this.query, filter: { ...this.query.filter } });
  }

  onQuickSearch(value: string): void {
    this.query = { ...this.query, quickSearch: value, pageIndex: 0 };
    this.emit();
  }

  onPageSize(value: number | string): void {
    this.query = { ...this.query, pageSize: Number(value) || 25, pageIndex: 0 };
    this.emit();
  }

  onFilter(key: string, value: string): void {
    this.query = {
      ...this.query,
      pageIndex: 0,
      filter: { ...this.query.filter, [key]: value },
    };
    this.emit();
  }

  prev(): void {
    if (this.query.pageIndex <= 0) return;
    this.query = { ...this.query, pageIndex: this.query.pageIndex - 1 };
    this.emit();
  }

  next(): void {
    if (this.query.pageIndex + 1 >= this.pageCount) return;
    this.query = { ...this.query, pageIndex: this.query.pageIndex + 1 };
    this.emit();
  }

  toggleAdvanced(): void {
    this.advancedOpen = !this.advancedOpen;
  }
}
