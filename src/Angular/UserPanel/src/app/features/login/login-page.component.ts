import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { QueueAclAuthService } from '../../core/services/queue-acl-auth.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, RouterLink],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss',
})
export class LoginPageComponent implements OnInit {
  private readonly auth = inject(QueueAclAuthService);
  private readonly hub = inject(AsteriskHubService);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly gateActive = signal(false);

  username = '';
  password = '';

  ngOnInit(): void {
    this.auth.refreshStatus().subscribe({
      next: (s) => {
        this.gateActive.set(!!s?.gateActive);
        if (s?.isAuthenticated) {
          void this.router.navigateByUrl('/calls');
        }
      },
    });
  }

  submit(): void {
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(this.username.trim(), this.password).subscribe({
      next: (r) => {
        this.busy.set(false);
        if (!r.isSuccess || !r.data?.[0]) {
          this.error.set(r.errorMessage || this.i18n.t('AUTH.LOGIN_FAIL'));
          return;
        }
        this.password = '';
        void this.hub.stop().then(() => this.hub.subscribeJobs());
        void this.router.navigateByUrl('/calls');
      },
      error: (err: unknown) => {
        this.busy.set(false);
        const message = err instanceof Error ? err.message : this.i18n.t('AUTH.LOGIN_FAIL');
        this.error.set(message);
      },
    });
  }
}
