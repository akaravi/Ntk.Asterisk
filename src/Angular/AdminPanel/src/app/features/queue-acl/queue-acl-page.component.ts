import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { QueueAclUser } from '../../core/models/queue-acl.models';
import { QueueAclAuthService } from '../../core/services/queue-acl-auth.service';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import { ListQuery } from '../../core/models/asterisk.models';
import {
  AdvancedFilterField,
  ListToolbarComponent,
} from '../../shared/components/list-toolbar/list-toolbar.component';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';

@Component({
  selector: 'app-queue-acl-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, ListToolbarComponent, ListPagerComponent],
  templateUrl: './queue-acl-page.component.html',
  styleUrl: './queue-acl-page.component.scss',
})
export class QueueAclPageComponent implements OnInit {
  private readonly auth = inject(QueueAclAuthService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly requireLogin = signal(false);
  readonly usersAll = signal<QueueAclUser[]>([]);
  readonly usersRows = signal<QueueAclUser[]>([]);
  readonly totalCount = signal(0);

  query: ListQuery = { ...defaultListQuery('username'), pageSize: 25, sortDir: 'asc' };
  readonly filters: AdvancedFilterField[] = [
    { key: 'role', labelKey: 'QUEUE_ACL.FILTER_ROLE', placeholderKey: 'QUEUE_ACL.FILTER_ROLE_PH', ltr: true },
  ];

  form = {
    id: '' as string | null,
    username: '',
    password: '',
    isEnabled: true,
    role: 'viewer',
    allowedQueuesText: '',
  };

  ngOnInit(): void {
    this.auth.refreshStatus().subscribe({
      next: (s) => this.requireLogin.set(!!s?.requireLogin),
    });
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.auth.getUsers().subscribe({
      next: (r) => {
        this.loading.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('QUEUE_ACL.LOAD_FAIL'));
          return;
        }
        this.usersAll.set(r.data ?? []);
        this.applyList();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.message || this.i18n.instant('QUEUE_ACL.LOAD_FAIL'));
      },
    });
  }

  onQueryChange(q: ListQuery): void {
    this.query = q;
    this.applyList();
  }

  edit(u: QueueAclUser): void {
    this.form = {
      id: u.id,
      username: u.username,
      password: '',
      isEnabled: u.isEnabled,
      role: u.role || 'viewer',
      allowedQueuesText: (u.allowedQueues ?? []).join(', '),
    };
  }

  resetForm(): void {
    this.form = {
      id: null,
      username: '',
      password: '',
      isEnabled: true,
      role: 'viewer',
      allowedQueuesText: '',
    };
  }

  save(): void {
    const allowedQueues = this.form.allowedQueuesText
      .split(/[,;\n]/)
      .map((s) => s.trim())
      .filter(Boolean);
    const body = {
      id: this.form.id,
      username: this.form.username.trim(),
      password: this.form.password || null,
      isEnabled: this.form.isEnabled,
      role: this.form.role,
      allowedQueues,
    };
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    const req = this.form.id ? this.auth.updateUser(body) : this.auth.addUser(body);
    req.subscribe({
      next: (r) => {
        this.busy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('QUEUE_ACL.SAVE_FAIL'));
          return;
        }
        this.success.set(this.i18n.instant('QUEUE_ACL.SAVE_OK'));
        this.resetForm();
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err?.message || this.i18n.instant('QUEUE_ACL.SAVE_FAIL'));
      },
    });
  }

  remove(u: QueueAclUser): void {
    if (!confirm(this.i18n.instant('QUEUE_ACL.CONFIRM_DELETE'))) return;
    this.busy.set(true);
    this.auth.deleteUser(u.id).subscribe({
      next: (r) => {
        this.busy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('QUEUE_ACL.DELETE_FAIL'));
          return;
        }
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err?.message || this.i18n.instant('QUEUE_ACL.DELETE_FAIL'));
      },
    });
  }

  toggleRequireLogin(checked: boolean): void {
    this.busy.set(true);
    this.auth.setRequireLogin(checked).subscribe({
      next: (r) => {
        this.busy.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || this.i18n.instant('QUEUE_ACL.SAVE_FAIL'));
          return;
        }
        this.requireLogin.set(!!r.data?.[0]?.requireLogin);
        this.success.set(this.i18n.instant('QUEUE_ACL.REQUIRE_LOGIN_OK'));
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err?.message || this.i18n.instant('QUEUE_ACL.SAVE_FAIL'));
      },
    });
  }

  private applyList(): void {
    const { rows, totalCount } = applyClientList(
      this.usersAll() as unknown as Record<string, unknown>[],
      this.query,
      ['username', 'role'],
    );
    this.usersRows.set(rows as unknown as QueueAclUser[]);
    this.totalCount.set(totalCount);
  }
}
