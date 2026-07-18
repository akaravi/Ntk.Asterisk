import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { WebPhoneRecording } from '../../core/models/webphone';
import { WebPhoneApi } from '../../core/services/webphone.api';

@Component({
  selector: 'app-webphone-recordings',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './webphone-recordings.component.html',
  styleUrl: './webphone-buddies.component.scss',
})
export class WebphoneRecordingsComponent implements OnInit {
  private readonly api = inject(WebPhoneApi);
  private readonly i18n = inject(I18nService);

  readonly rows = signal<WebPhoneRecording[]>([]);
  readonly loading = signal(false);
  readonly busyId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  quickSearch = '';

  ngOnInit(): void {
    this.reload();
  }

  filtered(): WebPhoneRecording[] {
    const q = this.quickSearch.trim().toLowerCase();
    if (!q) return this.rows();
    return this.rows().filter((r) => r.fileName.toLowerCase().includes(q));
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getRecordings().subscribe({
      next: (rows) => {
        this.loading.set(false);
        this.rows.set(rows);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.message || this.i18n.t('WEBPHONE.LOAD_FAIL'));
      },
    });
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
        this.error.set(err?.message || this.i18n.t('WEBPHONE.DOWNLOAD_FAIL'));
      },
    });
  }
}
