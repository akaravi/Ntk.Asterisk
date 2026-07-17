import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CallJob,
  ChannelItem,
  ConnectionStatus,
  LiveEventItem,
  PeerItem,
} from '../models/asterisk.models';

export type AsteriskHubEvent =
  | { kind: 'job'; payload: CallJob }
  | { kind: 'peers'; payload: PeerItem[] }
  | { kind: 'channels'; payload: ChannelItem[] }
  | { kind: 'connection'; payload: ConnectionStatus }
  | { kind: 'liveEvent'; payload: LiveEventItem };

@Injectable({ providedIn: 'root' })
export class AsteriskHubService implements OnDestroy {
  private readonly events$ = new Subject<AsteriskHubEvent>();
  private readonly connected$ = new BehaviorSubject<boolean>(false);
  private connection: signalR.HubConnection | null = null;
  private eventsSubscribed = false;

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
    this.bind('LiveEvent', (payload: LiveEventItem) => this.events$.next({ kind: 'liveEvent', payload }));
    this.bind('liveEvent', (payload: LiveEventItem) => this.events$.next({ kind: 'liveEvent', payload }));

    this.connection.onreconnected(async () => {
      this.connected$.next(true);
      if (this.eventsSubscribed) {
        await this.invokeSafe('SubscribeEvents');
      }
    });
    this.connection.onreconnecting(() => this.connected$.next(false));
    this.connection.onclose(() => this.connected$.next(false));

    try {
      await this.connection.start();
      this.connected$.next(true);
    } catch {
      this.connected$.next(false);
    }
  }

  async subscribeEvents(): Promise<void> {
    await this.start();
    await this.invokeSafe('SubscribeEvents');
    this.eventsSubscribed = true;
  }

  async unsubscribeEvents(): Promise<void> {
    if (!this.eventsSubscribed) return;
    await this.invokeSafe('UnsubscribeEvents');
    this.eventsSubscribed = false;
  }

  async stop(): Promise<void> {
    if (!this.connection) return;
    try {
      await this.connection.stop();
    } finally {
      this.connection = null;
      this.eventsSubscribed = false;
      this.connected$.next(false);
    }
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private bind(method: string, handler: (...args: any[]) => void): void {
    this.connection?.on(method, handler);
  }

  private async invokeSafe(method: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }
    try {
      await this.connection.invoke(method);
    } catch {
      // ignore subscribe races during reconnect
    }
  }
}
