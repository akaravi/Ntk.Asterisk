import { Component, OnInit, effect, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { AppLocale, I18nService } from './core/i18n/i18n.service';
import { AsteriskHubService } from './core/services/asterisk-hub.service';

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

  hubState: 'connected' | 'disconnected' | 'reconnecting' = 'disconnected';

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
    void this.hub.start();
    this.hub.connectionState$.subscribe((state) => {
      this.hubState = state;
    });
    // Live job status on User dashboard (group "jobs")
    void this.hub.subscribeJobs();
  }

  setLocale(locale: AppLocale): void {
    this.i18n.setLocale(locale);
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
}
