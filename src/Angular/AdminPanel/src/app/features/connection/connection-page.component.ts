import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { ConnectionStatus } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';

@Component({
  selector: 'app-connection-page',
  standalone: true,
  imports: [TranslatePipe, DatePipe],
  templateUrl: './connection-page.component.html',
  styleUrl: './connection-page.component.scss',
})
export class ConnectionPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly hub = inject(AsteriskHubService);
  private sub?: Subscription;

  readonly status = signal<ConnectionStatus | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly hubConnected = signal(false);
  readonly refreshedAt = signal<Date | null>(null);

  ngOnInit(): void {
    this.reload();
    this.sub = new Subscription();
    this.sub.add(this.hub.hubConnected$.subscribe((v) => this.hubConnected.set(v)));
    this.sub.add(
      this.hub.hubEvents$.subscribe((ev) => {
        if (ev.kind === 'connection') {
          this.status.set(ev.payload);
          this.refreshedAt.set(new Date());
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getConnectionStatus().subscribe({
      next: (s) => {
        this.status.set(s);
        this.refreshedAt.set(new Date());
        this.loading.set(false);
      },
      error: (err: Error) => {
        this.error.set(err.message);
        this.loading.set(false);
      },
    });
  }
}
