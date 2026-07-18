import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { QueueAclAuthService } from '../../core/services/queue-acl-auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly hub = inject(AsteriskHubService);
  private readonly auth = inject(QueueAclAuthService);
  private readonly router = inject(Router);

  readonly lang = signal('fa');
  readonly hubOk = signal(false);
  readonly gateActive = signal(false);
  readonly isAuthenticated = signal(false);
  readonly isAdmin = signal(false);
  readonly username = signal<string | null>(null);
  readonly logoutBusy = signal(false);

  ngOnInit(): void {
    void this.hub.start();
    this.hub.hubConnected$.subscribe((v) => this.hubOk.set(v));
    const current = this.translate.getCurrentLang() || 'fa';
    this.lang.set(current);
    document.documentElement.lang = current;
    document.documentElement.dir = current === 'fa' ? 'rtl' : 'ltr';

    this.auth.refreshStatus().subscribe({
      next: (s) => {
        this.gateActive.set(!!s?.gateActive);
        this.isAuthenticated.set(!!s?.isAuthenticated);
        this.isAdmin.set(!!s?.isAdmin);
        this.username.set(s?.username ?? null);
      },
    });
  }

  setLang(code: 'fa' | 'en'): void {
    this.translate.use(code).subscribe(() => {
      this.lang.set(code);
      document.documentElement.lang = code;
      document.documentElement.dir = code === 'fa' ? 'rtl' : 'ltr';
    });
  }

  logout(): void {
    if (this.logoutBusy()) return;
    this.logoutBusy.set(true);
    this.auth.logout().subscribe({
      next: () => {
        this.logoutBusy.set(false);
        this.isAuthenticated.set(false);
        this.isAdmin.set(false);
        this.username.set(null);
        void this.router.navigateByUrl('/login');
      },
      error: () => {
        this.logoutBusy.set(false);
        this.auth.clearSession();
        void this.router.navigateByUrl('/login');
      },
    });
  }
}
