import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ListQuery } from '../../core/models/asterisk.models';
import { WebPhoneRecording } from '../../core/models/webphone.models';
import { WebPhoneApiService } from '../../core/services/webphone-api.service';
import { applyClientList, defaultListQuery } from '../../core/utils/list-query.util';
import { ListPagerComponent } from '../../shared/components/list-pager/list-pager.component';
import { ListToolbarComponent } from '../../shared/components/list-toolbar/list-toolbar.component';

@Component({
  selector: 'app-webphone-recordings-page',
  standalone: true,
  imports: [TranslatePipe, RouterLink, ListToolbarComponent, ListPagerComponent],
  templateUrl: './webphone-recordings-page.component.html',
})
export class WebphoneRecordingsPageComponent implements OnInit {
  private readonly api = inject(WebPhoneApiService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly busyId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly rowsAll = signal<WebPhoneRecording[]>([]);
  readonly rows = signal<WebPhoneRecording[]>([]);
  readonly totalCount = signal(0);
  query: ListQuery = { ...defaultListQuery('createdAtUtc'), pageSize: 25, sortDir: 'desc' };

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getRecordings().subscribe({
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

  download(row: WebPhoneRecording): void {
    this.busyId.set(row.id);
    this.api.downloadRecording(row.id).subscribe({
      next: (blob) => {
        this.busyId.set(null);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = row.fileName || `${row.id}.bin`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.busyId.set(null);
        this.error.set(err?.message || this.i18n.instant('WEBPHONE.DOWNLOAD_FAIL'));
      },
    });
  }

  private applyList(): void {
    const { rows, totalCount } = applyClientList(
      this.rowsAll() as unknown as Record<string, unknown>[],
      this.query,
      ['fileName', 'buddyId', 'cdrId', 'notes'],
    );
    this.rows.set(rows as unknown as WebPhoneRecording[]);
    this.totalCount.set(totalCount);
  }
}
