import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ListQuery } from '../../core/models/asterisk.models';
import { WebPhoneBuddy } from '../../core/models/webphone.models';
import { WebPhoneApiService } from '../../core/services/webphone-api.service';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';
import { ListToolbarComponent } from '../../shared/components/list-toolbar/list-toolbar.component';

@Component({
  selector: 'app-webphone-buddies-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, RouterLink, ListToolbarComponent, ListPagerComponent],
  templateUrl: './webphone-buddies-page.component.html',
})
export class WebphoneBuddiesPageComponent implements OnInit {
  private readonly api = inject(WebPhoneApiService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly rowsAll = signal<WebPhoneBuddy[]>([]);
  readonly rows = signal<WebPhoneBuddy[]>([]);
  readonly totalCount = signal(0);

  query: ListQuery = { ...defaultListQuery('displayName'), pageSize: 25, sortDir: 'asc' };

  form = {
    id: '' as string | null,
    type: 'extension',
    displayName: '',
    extensionNumber: '',
    description: '',
    mobileNumber: '',
    email: '',
    subscribe: false,
  };

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getBuddies({ pageIndex: 0, pageSize: 500 }).subscribe({
      next: (r) => {
        this.loading.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('WEBPHONE.LOAD_FAIL'));
          return;
        }
        this.rowsAll.set(r.data ?? []);
        this.applyList();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.message || this.i18n.instant('WEBPHONE.LOAD_FAIL'));
      },
    });
  }

  onQueryChange(q: ListQuery): void {
    this.query = q;
    this.applyList();
  }

  edit(row: WebPhoneBuddy): void {
    this.form = {
      id: row.id,
      type: row.type || 'extension',
      displayName: row.displayName,
      extensionNumber: row.extensionNumber || '',
      description: row.description || '',
      mobileNumber: row.mobileNumber || '',
      email: row.email || '',
      subscribe: row.subscribe,
    };
  }

  resetForm(): void {
    this.form = {
      id: null,
      type: 'extension',
      displayName: '',
      extensionNumber: '',
      description: '',
      mobileNumber: '',
      email: '',
      subscribe: false,
    };
  }

  save(): void {
    if (!this.form.displayName.trim()) {
      this.error.set(this.i18n.instant('WEBPHONE.DISPLAY_REQUIRED'));
      return;
    }
    const body = {
      id: this.form.id,
      type: this.form.type,
      displayName: this.form.displayName.trim(),
      extensionNumber: this.form.extensionNumber.trim() || null,
      description: this.form.description.trim() || null,
      mobileNumber: this.form.mobileNumber.trim() || null,
      email: this.form.email.trim() || null,
      subscribe: this.form.subscribe,
    };
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    const req = this.form.id ? this.api.updateBuddy(body) : this.api.addBuddy(body);
    req.subscribe({
      next: (r) => {
        this.busy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('WEBPHONE.SAVE_FAIL'));
          return;
        }
        this.success.set(this.i18n.instant('WEBPHONE.SAVE_OK'));
        this.resetForm();
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err?.message || this.i18n.instant('WEBPHONE.SAVE_FAIL'));
      },
    });
  }

  remove(row: WebPhoneBuddy): void {
    if (!confirm(this.i18n.instant('WEBPHONE.CONFIRM_DELETE'))) return;
    this.busy.set(true);
    this.api.deleteBuddy(row.id).subscribe({
      next: (r) => {
        this.busy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('WEBPHONE.DELETE_FAIL'));
          return;
        }
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err?.message || this.i18n.instant('WEBPHONE.DELETE_FAIL'));
      },
    });
  }

  private applyList(): void {
    const { rows, totalCount } = applyClientList(
      this.rowsAll() as unknown as Record<string, unknown>[],
      this.query,
      ['displayName', 'extensionNumber', 'type', 'mobileNumber'],
    );
    this.rows.set(rows as unknown as WebPhoneBuddy[]);
    this.totalCount.set(totalCount);
  }
}
