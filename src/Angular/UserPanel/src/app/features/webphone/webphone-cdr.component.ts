import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { WebPhoneCdr } from '../../core/models/webphone';
import { WebPhoneApi } from '../../core/services/webphone.api';

@Component({
  selector: 'app-webphone-cdr',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './webphone-cdr.component.html',
  styleUrl: './webphone-buddies.component.scss',
})
export class WebphoneCdrComponent implements OnInit {
  private readonly api = inject(WebPhoneApi);
  private readonly i18n = inject(I18nService);

  readonly rows = signal<WebPhoneCdr[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  quickSearch = '';
  pageSize = 25;
  pageIndex = 0;
  readonly pageSizeOptions = [10, 25, 50, 100];
  readonly Math = Math;

  ngOnInit(): void {
    this.reload();
  }

  filtered(): WebPhoneCdr[] {
    const q = this.quickSearch.trim().toLowerCase();
    let list = this.rows();
    if (q) {
      list = list.filter(
        (r) =>
          (r.withNumber ?? '').toLowerCase().includes(q) ||
          (r.displayName ?? '').toLowerCase().includes(q) ||
          (r.direction ?? '').toLowerCase().includes(q),
      );
    }
    return list;
  }

  paged(): WebPhoneCdr[] {
    const all = this.filtered();
    const start = this.pageIndex * this.pageSize;
    return all.slice(start, start + this.pageSize);
  }

  total(): number {
    return this.filtered().length;
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getCdr().subscribe({
      next: (rows) => {
        this.loading.set(false);
        this.rows.set(rows);
        this.pageIndex = 0;
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.message || this.i18n.t('WEBPHONE.LOAD_FAIL'));
      },
    });
  }

  onSearch(): void {
    this.pageIndex = 0;
  }
}
