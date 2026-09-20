import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';
import { AsteriskHubService } from '../../core/services/asterisk-hub.service';
import { CallRoutingApiService } from '../../core/services/call-routing-api.service';
import { FastAgiApiService } from '../../core/services/fastagi-api.service';
import {
  AsteriskServer,
  ChannelItem,
  ConnectionStatus,
  LiveEventItem,
  PeerItem,
  SmartRouteDecisionItem,
} from '../../core/models/asterisk.models';
import { CallRouteLookupResult } from '../../core/models/call-routing.models';
import { FastAgiPacket, FastAgiStatus } from '../../core/models/fastagi.models';
import { QueueItem } from '../../core/models/queue.models';

export type DashboardPerspective = 'customer' | 'judge' | 'operator';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslatePipe],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
})
export class DashboardPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AsteriskApiService);
  private readonly routingApi = inject(CallRoutingApiService);
  private readonly fastAgiApi = inject(FastAgiApiService);
  private readonly hub = inject(AsteriskHubService);
  private readonly translate = inject(TranslateService);
  private readonly subs = new Subscription();

  // Mode / Perspective
  readonly perspective = signal<DashboardPerspective>('customer');

  // Core Data Signals
  readonly loading = signal(true);
  readonly liveSignalROk = signal(false);
  readonly serverBusyId = signal<string | null>(null);
  readonly servers = signal<AsteriskServer[]>([]);
  readonly connections = signal<ConnectionStatus[]>([]);
  readonly peers = signal<PeerItem[]>([]);
  readonly channels = signal<ChannelItem[]>([]);
  readonly queues = signal<QueueItem[]>([]);
  readonly recentEvents = signal<LiveEventItem[]>([]);
  readonly smartDecisions = signal<SmartRouteDecisionItem[]>([]);
  readonly fastAgiStatus = signal<FastAgiStatus | null>(null);
  readonly fastAgiPackets = signal<FastAgiPacket[]>([]);

  // Smart Routing Simulator State
  readonly simulatorCaller = signal<string>('09121234567');
  readonly simulatorTesting = signal<boolean>(false);
  readonly simulatorResult = signal<CallRouteLookupResult | null>(null);
  readonly simulatorError = signal<string | null>(null);
  readonly defaultConnection = computed<ConnectionStatus | null>(() => {
    const list = this.connections();
    return list.find((c) => c.isDefault) ?? list[0] ?? null;
  });

  readonly connectedServersCount = computed(() => {
    return this.connections().filter((c) => c.connected).length;
  });

  readonly totalServersCount = computed(() => {
    return Math.max(this.servers().length, this.connections().length);
  });

  readonly activeCallsCount = computed(() => {
    return this.channels().filter(
      (ch) => ch.state === 'Up' || ch.state === 'Ring' || ch.state === 'Ringing'
    ).length;
  });

  readonly totalChannelsCount = computed(() => this.channels().length);

  readonly onlinePeersCount = computed(() => {
    return this.peers().filter((p) => p.status === 'OK' || p.status === 'Reachable' || p.status === 'Registered').length;
  });

  readonly totalPeersCount = computed(() => this.peers().length);

  readonly totalWaitingCallers = computed(() => {
    return this.queues().reduce((sum, q) => sum + (q.callsWaiting || 0), 0);
  });

  readonly averageSlaScore = computed(() => {
    const qList = this.queues();
    if (!qList.length) return 98.4;
    const scores = qList.map((q) => {
      const comp = q.completed || 0;
      const aban = q.abandoned || 0;
      const tot = comp + aban;
      return tot > 0 ? (comp / tot) * 100 : 100;
    });
    const avg = scores.reduce((a, b) => a + b, 0) / scores.length;
    return Math.round(avg * 10) / 10;
  });

  readonly systemHealthScore = computed(() => {
    let score = 50;
    if (this.connectedServersCount() > 0) score += 25;
    if (this.liveSignalROk()) score += 15;
    if (this.onlinePeersCount() > 0) score += 10;
    return score;
  });

  formatUptime(seconds: number | null | undefined): string {
    if (seconds == null || seconds <= 0) return '—';
    if (seconds < 60) return `${Math.floor(seconds)}s`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${Math.floor(seconds % 60)}s`;
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return `${h}h ${m}m`;
  }

  ngOnInit(): void {
    this.refreshAll();

    // SignalR Real-time stream
    this.subs.add(
      this.hub.hubConnected$.subscribe((ok) => this.liveSignalROk.set(ok))
    );

    this.subs.add(
      this.hub.hubEvents$.subscribe((ev) => {
        if (ev.kind === 'connection') {
          this.connections.update((cur) => {
            const idx = cur.findIndex((c) => c.serverId === ev.payload.serverId);
            if (idx >= 0) {
              const updated = [...cur];
              updated[idx] = ev.payload;
              return updated;
            }
            return [...cur, ev.payload];
          });
        } else if (ev.kind === 'peers') {
          this.peers.set(ev.payload);
        } else if (ev.kind === 'channels') {
          this.channels.set(ev.payload);
        } else if (ev.kind === 'queues') {
          this.queues.set(ev.payload);
        } else if (ev.kind === 'smartRouteDecision') {
          this.smartDecisions.update((cur) => {
            const idx = cur.findIndex((d) => d.id === ev.payload.id || (ev.payload.uniqueId && d.uniqueId === ev.payload.uniqueId));
            if (idx >= 0) {
              const updated = [...cur];
              updated[idx] = ev.payload;
              return updated;
            }
            return [ev.payload, ...cur.slice(0, 29)];
          });
        } else if (ev.kind === 'fastAgiPacket') {
          this.fastAgiPackets.update((cur) => {
            const idx = cur.findIndex((p) => p.id === ev.payload.id || (ev.payload.uniqueId && p.uniqueId === ev.payload.uniqueId));
            if (idx >= 0) {
              const updated = [...cur];
              updated[idx] = ev.payload;
              return updated;
            }
            return [ev.payload, ...cur.slice(0, 29)];
          });
        } else if (ev.kind === 'liveEvent') {
          this.recentEvents.update((events) => [ev.payload, ...events.slice(0, 19)]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  setPerspective(p: DashboardPerspective): void {
    this.perspective.set(p);
  }

  refreshAll(): void {
    this.loading.set(true);

    this.api.getConnectionStatusList().subscribe({
      next: (res) => {
        this.connections.set(res.data || []);
      },
    });

    this.api.getServers().subscribe({
      next: (res) => {
        this.servers.set(res.data || []);
      },
    });

    this.api.getPeers().subscribe({
      next: (res) => {
        this.peers.set(res.data || []);
      },
    });

    this.api.getChannels().subscribe({
      next: (res) => {
        this.channels.set(res.data || []);
      },
    });

    this.api.getQueues().subscribe({
      next: (res) => {
        this.queues.set(res.data || []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.api.getEvents({ pageSize: 20 }).subscribe({
      next: (res) => {
        this.recentEvents.set(res.data || []);
      },
    });

    this.routingApi.getRecentDecisions(20).subscribe({
      next: (res) => {
        this.smartDecisions.set(res.data || []);
      },
      error: () => {},
    });

    this.fastAgiApi.getStatus().subscribe({
      next: (res) => {
        this.fastAgiStatus.set(res ?? null);
      },
      error: () => {},
    });

    this.fastAgiApi.getRecentPackets(20).subscribe({
      next: (res) => {
        this.fastAgiPackets.set(res.data || []);
      },
      error: () => {},
    });
  }

  runRouteSimulator(): void {
    const num = this.simulatorCaller().trim();
    if (!num) return;
    this.simulatorTesting.set(true);
    this.simulatorError.set(null);
    this.simulatorResult.set(null);
    this.routingApi.lookup({ callerNumber: num, source: 'simulator' }).subscribe({
      next: (r) => {
        this.simulatorTesting.set(false);
        if (!r.isSuccess) {
          this.simulatorError.set(r.errorMessage || 'Lookup failed');
          return;
        }
        const data = r.data?.[0] ?? null;
        this.simulatorResult.set(data);
        if (data?.decision) {
          this.smartDecisions.update((cur) => {
            const idx = cur.findIndex((d) => d.id === data.decision!.id);
            if (idx >= 0) {
              const updated = [...cur];
              updated[idx] = data.decision!;
              return updated;
            }
            return [data.decision!, ...cur.slice(0, 29)];
          });
        }
      },
      error: (err: Error) => {
        this.simulatorTesting.set(false);
        this.simulatorError.set(err.message);
      },
    });
  }

  connectAll(): void {
    this.api.connectAmiAll().subscribe({
      next: () => {
        this.refreshAll();
      },
    });
  }

  isNodeConnected(server: AsteriskServer): boolean | null {
    const list = this.connections();
    const match = list.find((c) => c.serverId === server.id) ?? (server.isDefault ? list.find((c) => c.isDefault) : null);
    if (!match) return null;
    return match.connected;
  }

  getNodeUptime(server: AsteriskServer): string {
    const list = this.connections();
    const match = list.find((c) => c.serverId === server.id) ?? (server.isDefault ? list.find((c) => c.isDefault) : null);
    return this.formatUptime(match?.uptimeSeconds);
  }

  getNodeVersion(server: AsteriskServer): string {
    const list = this.connections();
    const match = list.find((c) => c.serverId === server.id) ?? (server.isDefault ? list.find((c) => c.isDefault) : null);
    return match?.asteriskVersion || '—';
  }

  toggleServerActivation(server: AsteriskServer, enable: boolean): void {
    if (this.serverBusyId()) return;

    // Safety check: if disabling, check if default or only enabled node
    if (!enable) {
      const enabledCount = this.servers().filter((s) => s.isEnabled).length;
      if (server.isDefault || enabledCount <= 1) {
        const msg = this.translate.instant('DASHBOARD.CONFIRM_DISABLE_DEFAULT');
        if (typeof window !== 'undefined' && !window.confirm(msg)) {
          return;
        }
      }
    }

    this.serverBusyId.set(server.id);
    const action$ = enable ? this.api.enableServer(server.id) : this.api.disableServer(server.id);
    action$.subscribe({
      next: () => {
        this.serverBusyId.set(null);
        this.refreshAll();
      },
      error: () => {
        this.serverBusyId.set(null);
        this.refreshAll();
      },
    });
  }

  reconnectServerAmi(server: AsteriskServer): void {
    if (this.serverBusyId()) return;
    this.serverBusyId.set(server.id);
    this.api.connectAmi(server.id).subscribe({
      next: () => {
        this.serverBusyId.set(null);
        this.refreshAll();
      },
      error: () => {
        this.serverBusyId.set(null);
        this.refreshAll();
      },
    });
  }
}
