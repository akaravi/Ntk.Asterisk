import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { WebPhoneBuddy } from '../../core/models/webphone';
import { WebPhoneApi } from '../../core/services/webphone.api';

@Component({
  selector: 'app-webphone-buddies',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './webphone-buddies.component.html',
  styleUrl: './webphone-buddies.component.scss',
})
export class WebphoneBuddiesComponent implements OnInit {
  private readonly api = inject(WebPhoneApi);
  private readonly i18n = inject(I18nService);

  readonly rows = signal<WebPhoneBuddy[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  displayName = '';
  extensionNumber = '';
  mobileNumber = '';
  quickSearch = '';

  ngOnInit(): void {
    this.reload();
  }

  filtered(): WebPhoneBuddy[] {
    const q = this.quickSearch.trim().toLowerCase();
    if (!q) return this.rows();
    return this.rows().filter(
      (b) =>
        b.displayName.toLowerCase().includes(q) ||
        (b.extensionNumber ?? '').toLowerCase().includes(q) ||
        (b.mobileNumber ?? '').toLowerCase().includes(q),
    );
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getBuddies().subscribe({
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

  add(): void {
    if (!this.displayName.trim()) {
      this.error.set(this.i18n.t('WEBPHONE.DISPLAY_REQUIRED'));
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.api
      .addBuddy({
        displayName: this.displayName.trim(),
        extensionNumber: this.extensionNumber.trim() || null,
        mobileNumber: this.mobileNumber.trim() || null,
        type: 'extension',
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.success.set(this.i18n.t('WEBPHONE.SAVE_OK'));
          this.displayName = '';
          this.extensionNumber = '';
          this.mobileNumber = '';
          this.reload();
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(err?.message || this.i18n.t('WEBPHONE.SAVE_FAIL'));
        },
      });
  }
}
