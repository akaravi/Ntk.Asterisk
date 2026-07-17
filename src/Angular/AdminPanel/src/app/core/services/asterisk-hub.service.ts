import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CallJob, ChannelItem, ConnectionStatus, PeerItem } from '../models/asterisk.models';

export type AsteriskHubEvent =
  | { kind: 'job'; payload: CallJob }
  | { kind: 'peers'; payload: PeerItem[] }
  | { kind: 'channels'; payload: ChannelItem[] }
  | { kind: 'connection'; payload: ConnectionStatus };

@Injectable({ providedIn: 'root' })
export class AsteriskHubService implements OnDestroy {
  private readonly events$ = new Subject<AsteriskHubEvent>();
  private readonly connected$ = new BehaviorSubject<boolean>(false);
  private connection: signalR.HubConnection | null = null;

  readonly hubEvents$: Observable<AsteriskHubEvent> = this.events$.asObservable();
  readonly hubConnected$: Observable<boolean> = this.connected$.asObservable();

  async start(): Promise<void> {
    if (this.connection) {
      return;
    }
    const url = `${environment.apiBaseUrl.replace(/\/$/, '')}${environment.hubPath}`;
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.bind('JobUpdated', (payload: CallJob) => this.events$.next({ kind: 'job', payload }));
    this.bind('jobUpdated', (payload: CallJob) => this.events$.next({ kind: 'job', payload }));
    this.bind('PeersUpdated', (payload: PeerItem[]) => this.events$.next({ kind: 'peers', payload }));
    this.bind('peersUpdated', (payload: PeerItem[]) => this.events$.next({ kind: 'peers', payload }));
    this.bind('ChannelsUpdated', (payload: ChannelItem[]) => this.events$.next({ kind: 'channels', payload }));
    this.bind('channelsUpdated', (payload: ChannelItem[]) => this.events$.next({ kind: 'channels', payload }));
    this.bind('ConnectionUpdated', (payload: ConnectionStatus) =>
      this.events$.next({ kind: 'connection', payload })
    );
    this.bind('connectionUpdated', (payload: ConnectionStatus) =>
      this.events$.next({ kind: 'connection', payload })
    );

    this.connection.onreconnected(() => this.connected$.next(true));
    this.connection.onreconnecting(() => this.connected$.next(false));
    this.connection.onclose(() => this.connected$.next(false));

    try {
      await this.connection.start();
      this.connected$.next(true);
    } catch {
      this.connected$.next(false);
    }
  }

  async stop(): Promise<void> {
    if (!this.connection) return;
    try {
      await this.connection.stop();
    } finally {
      this.connection = null;
      this.connected$.next(false);
    }
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private bind(method: string, handler: (...args: any[]) => void): void {
    this.connection?.on(method, handler);
  }
}
