import { Component, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { environment } from '../../../environments/environment';
import { ConnectionStatus } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './settings-page.component.html',
  styleUrl: './settings-page.component.scss',
})
export class SettingsPageComponent implements OnInit {
  private readonly api = inject(AsteriskApiService);

  readonly apiBaseUrl = environment.apiBaseUrl;
  readonly hubUrl = `${environment.apiBaseUrl.replace(/\/$/, '')}${environment.hubPath}`;
  readonly status = signal<ConnectionStatus | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getConnectionStatus().subscribe({
      next: (s) => {
        this.status.set(s);
        this.loading.set(false);
      },
      error: (err: Error) => {
        this.error.set(err.message);
        this.loading.set(false);
      },
    });
  }

  flagLabel(configured: boolean | undefined): string {
    return configured ? 'SETTINGS.CONFIGURED' : 'SETTINGS.NOT_CONFIGURED';
  }
}
