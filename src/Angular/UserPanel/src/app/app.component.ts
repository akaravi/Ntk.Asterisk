import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { AppLocale, I18nService } from './core/i18n/i18n.service';
import { AsteriskHubService } from './core/services/asterisk-hub.service';
import { QueueAclAuthService } from './core/services/queue-acl-auth.service';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  private readonly i18n = inject(I18nService);
  private readonly hub = inject(AsteriskHubService);
  private readonly auth = inject(QueueAclAuthService);
  private readonly router = inject(Router);
  readonly theme = inject(ThemeService);

  hubState: 'connected' | 'disconnected' | 'reconnecting' = 'disconnected';

  readonly isLoginRoute = signal(false);
  readonly gateActive = signal(false);
  readonly isAuthenticated = signal(false);
  readonly username = signal<string | null>(null);
  readonly logoutBusy = signal(false);

  constructor() {
    effect(() => {
      const dir = this.i18n.dir();
      const lang = this.i18n.lang();
      document.documentElement.dir = dir;
      document.documentElement.lang = lang;
    });
  }

  ngOnInit(): void {
    this.i18n.setLocale('fa');
    this.syncLoginRoute(this.router.url);
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.syncLoginRoute(e.urlAfterRedirects));

    this.auth.refreshStatus().subscribe({
      next: (s) => {
        this.gateActive.set(!!s?.gateActive);
        this.isAuthenticated.set(!!s?.isAuthenticated);
        this.username.set(s?.username ?? null);
      },
    });

    void this.hub.start();
    this.hub.connectionState$.subscribe((state) => {
      this.hubState = state;
    });
    void this.hub.subscribeJobs();
  }

  setLocale(locale: AppLocale): void {
    this.i18n.setLocale(locale);
  }

  logout(): void {
    if (this.logoutBusy()) return;
    this.logoutBusy.set(true);
    this.auth.logout().subscribe({
      next: () => {
        this.logoutBusy.set(false);
        this.isAuthenticated.set(false);
        this.username.set(null);
        void this.hub.stop();
        void this.router.navigateByUrl('/login');
      },
      error: () => {
        this.logoutBusy.set(false);
        this.auth.clearSession();
        this.isAuthenticated.set(false);
        this.username.set(null);
        void this.hub.stop();
        void this.router.navigateByUrl('/login');
      },
    });
  }

  themeLabelKey(): string {
    switch (this.theme.mode()) {
      case 'light':
        return 'THEME.LIGHT';
      case 'dark':
        return 'THEME.DARK';
      default:
        return 'THEME.SYSTEM';
    }
  }

  hubLabelKey(): string {
    switch (this.hubState) {
      case 'connected':
        return 'HUB.CONNECTED';
      case 'reconnecting':
        return 'HUB.RECONNECTING';
      case 'disconnected':
        return 'HUB.DISCONNECTED';
      default: {
        const _exhaustive: never = this.hubState;
        return _exhaustive;
      }
    }
  }

  private syncLoginRoute(url: string): void {
    const path = url.split('?')[0] ?? url;
    this.isLoginRoute.set(path === '/login' || path.endsWith('/login'));
  }
}
