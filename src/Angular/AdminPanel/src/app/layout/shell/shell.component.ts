import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';

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

  readonly lang = signal('fa');
  readonly hubOk = signal(false);

  ngOnInit(): void {
    void this.hub.start();
    this.hub.hubConnected$.subscribe((v) => this.hubOk.set(v));
    const current = this.translate.getCurrentLang() || 'fa';
    this.lang.set(current);
    document.documentElement.lang = current;
    document.documentElement.dir = current === 'fa' ? 'rtl' : 'ltr';
  }

  setLang(code: 'fa' | 'en'): void {
    this.translate.use(code).subscribe(() => {
      this.lang.set(code);
      document.documentElement.lang = code;
      document.documentElement.dir = code === 'fa' ? 'rtl' : 'ltr';
    });
  }
}
