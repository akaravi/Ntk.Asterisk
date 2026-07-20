import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { environment } from '../../../environments/environment';
import { ListQuery } from '../../core/models/asterisk.models';
import { WebPhoneExtension, WebPhoneOptions, WebPhoneProvisionToken, WebPhoneProvisionTokenCreated } from '../../core/models/webphone.models';
import { WebPhoneApiService } from '../../core/services/webphone-api.service';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';
import {
  AdvancedFilterField,
  ListToolbarComponent,
} from '../../shared/components/list-toolbar/list-toolbar.component';

@Component({
  selector: 'app-webphone-extensions-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, RouterLink, ListToolbarComponent, ListPagerComponent],
  templateUrl: './webphone-extensions-page.component.html',
  styleUrl: './webphone-extensions-page.component.scss',
})
export class WebphoneExtensionsPageComponent implements OnInit {
  private readonly api = inject(WebPhoneApiService);
  private readonly i18n = inject(TranslateService);

  readonly webPhoneUrl = (environment.webPhoneUrl || 'http://localhost:5316').replace(/\/$/, '');
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly options = signal<WebPhoneOptions | null>(null);
  readonly rowsAll = signal<WebPhoneExtension[]>([]);
  readonly rows = signal<WebPhoneExtension[]>([]);
  readonly totalCount = signal(0);
  readonly tokens = signal<WebPhoneProvisionToken[]>([]);
  readonly createdToken = signal<WebPhoneProvisionTokenCreated | null>(null);

  query: ListQuery = { ...defaultListQuery('sipUsername'), pageSize: 25, sortDir: 'asc' };
  readonly filters: AdvancedFilterField[] = [
    {
      key: 'enabled',
      labelKey: 'WEBPHONE.FILTER_ENABLED',
      placeholderKey: 'WEBPHONE.FILTER_ENABLED_PH',
      ltr: true,
    },
  ];

  form = {
    id: '' as string | null,
    sipUsername: '',
    sipPassword: '',
    profileName: '',
    serverId: '',
    isEnabled: true,
  };

  tokenForm = {
    extensionId: '',
    label: '',
  };

  ngOnInit(): void {
    this.reloadOptions();
    this.reload();
    this.reloadTokens();
  }

  openSoftphone(): void {
    window.open(this.webPhoneUrl + '/', '_blank', 'noopener,noreferrer');
  }

  openSoftphoneWithToken(token: string): void {
    const url =
      this.webPhoneUrl + '/?token=' + encodeURIComponent(token);
    window.open(url, '_blank', 'noopener,noreferrer');
  }

  reloadTokens(): void {
    this.api.getProvisionTokens().subscribe({
      next: (r) => {
        if (r.isSuccess) this.tokens.set(r.data ?? []);
      },
      error: () => this.tokens.set([]),
    });
  }

  createToken(): void {
    const extensionId = this.tokenForm.extensionId.trim();
    if (!extensionId) {
      this.error.set(this.i18n.instant('WEBPHONE.TOKEN_EXT_REQUIRED'));
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.createdToken.set(null);
    this.api
      .createProvisionToken({
        extensionId,
        label: this.tokenForm.label.trim() || null,
        isEnabled: true,
      })
      .subscribe({
        next: (r) => {
          this.busy.set(false);
          if (!r.isSuccess || !r.data?.[0]) {
            this.error.set(r.errorMessage || this.i18n.instant('WEBPHONE.TOKEN_CREATE_FAIL'));
            return;
          }
          this.createdToken.set(r.data[0]);
          this.success.set(this.i18n.instant('WEBPHONE.TOKEN_CREATE_OK'));
          this.tokenForm.label = '';
          this.reloadTokens();
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(err?.message || this.i18n.instant('WEBPHONE.TOKEN_CREATE_FAIL'));
        },
      });
  }

  copyTokenLink(token: string): void {
    const url = this.webPhoneUrl + '/?token=' + encodeURIComponent(token);
    void navigator.clipboard.writeText(url).then(() => {
      this.success.set(this.i18n.instant('WEBPHONE.TOKEN_LINK_COPIED'));
    });
  }

  removeToken(row: WebPhoneProvisionToken): void {
    if (!confirm(this.i18n.instant('WEBPHONE.TOKEN_CONFIRM_DELETE'))) return;
    this.busy.set(true);
    this.api.deleteProvisionToken(row.id).subscribe({
      next: (r) => {
        this.busy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('WEBPHONE.TOKEN_DELETE_FAIL'));
          return;
        }
        this.reloadTokens();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err?.message || this.i18n.instant('WEBPHONE.TOKEN_DELETE_FAIL'));
      },
    });
  }

  extensionLabel(id: string): string {
    const ext = this.rowsAll().find((x) => x.id === id);
    return ext ? `${ext.sipUsername} (${ext.profileName || ext.id})` : id;
  }

  reloadOptions(): void {
    this.api.getOptions().subscribe({
      next: (o) => this.options.set(o),
      error: () => this.options.set(null),
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getExtensions().subscribe({
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

  edit(row: WebPhoneExtension): void {
    this.form = {
      id: row.id,
      sipUsername: row.sipUsername,
      sipPassword: '',
      profileName: row.profileName || '',
      serverId: row.serverId || '',
      isEnabled: row.isEnabled,
    };
  }

  resetForm(): void {
    this.form = {
      id: null,
      sipUsername: '',
      sipPassword: '',
      profileName: '',
      serverId: '',
      isEnabled: true,
    };
  }

  save(): void {
    const body = {
      id: this.form.id,
      sipUsername: this.form.sipUsername.trim(),
      sipPassword: this.form.sipPassword || null,
      profileName: this.form.profileName.trim() || null,
      serverId: this.form.serverId.trim() || null,
      isEnabled: this.form.isEnabled,
    };
    if (!body.sipUsername) {
      this.error.set(this.i18n.instant('WEBPHONE.USERNAME_REQUIRED'));
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    const req = this.form.id ? this.api.updateExtension(body) : this.api.addExtension(body);
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

  remove(row: WebPhoneExtension): void {
    if (!confirm(this.i18n.instant('WEBPHONE.CONFIRM_DELETE'))) return;
    this.busy.set(true);
    this.api.deleteExtension(row.id).subscribe({
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
    let source = this.rowsAll();
    const en = this.query.filter?.['enabled']?.trim().toLowerCase();
    if (en === 'true' || en === '1' || en === 'yes') source = source.filter((x) => x.isEnabled);
    if (en === 'false' || en === '0' || en === 'no') source = source.filter((x) => !x.isEnabled);
    const q = { ...this.query, filter: { ...(this.query.filter || {}) } };
    delete q.filter!['enabled'];
    const { rows, totalCount } = applyClientList(
      source as unknown as Record<string, unknown>[],
      q,
      ['sipUsername', 'profileName', 'serverId'],
    );
    this.rows.set(rows as unknown as WebPhoneExtension[]);
    this.totalCount.set(totalCount);
  }
}
