import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ListQuery } from '../../core/models/asterisk.models';
import { WebPhoneQos } from '../../core/models/webphone.models';
import { WebPhoneApiService } from '../../core/services/webphone-api.service';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';
import { ListToolbarComponent } from '../../shared/components/list-toolbar/list-toolbar.component';

@Component({
  selector: 'app-webphone-qos-page',
  standalone: true,
  imports: [TranslatePipe, RouterLink, ListToolbarComponent, ListPagerComponent],
  templateUrl: './webphone-qos-page.component.html',
})
export class WebphoneQosPageComponent implements OnInit {
  private readonly api = inject(WebPhoneApiService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly rowsAll = signal<WebPhoneQos[]>([]);
  readonly rows = signal<WebPhoneQos[]>([]);
  readonly totalCount = signal(0);
  query: ListQuery = { ...defaultListQuery('atUtc'), pageSize: 25, sortDir: 'desc' };

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getQos(500).subscribe({
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

  private applyList(): void {
    const { rows, totalCount } = applyClientList(
      this.rowsAll() as unknown as Record<string, unknown>[],
      this.query,
      ['callId', 'buddyId'],
    );
    this.rows.set(rows as unknown as WebPhoneQos[]);
    this.totalCount.set(totalCount);
  }
}
