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
import { QueueItem } from '../models/queue.models';

export type AsteriskHubEvent =
  | { kind: 'job'; payload: CallJob }
  | { kind: 'peers'; payload: PeerItem[] }
  | { kind: 'channels'; payload: ChannelItem[] }
  | { kind: 'queues'; payload: QueueItem[] }
  | { kind: 'connection'; payload: ConnectionStatus }
  | { kind: 'liveEvent'; payload: LiveEventItem }
  | { kind: 'amiHint'; payload: { type?: string; channel?: string | null; at?: string } };

@Injectable({ providedIn: 'root' })
export class AsteriskHubService implements OnDestroy {
  private readonly events$ = new Subject<AsteriskHubEvent>();
  private readonly connected$ = new BehaviorSubject<boolean>(false);
  private connection: signalR.HubConnection | null = null;
  private eventsSubscribed = false;
  private jobsSubscribed = false;
  private monitorSubscribed = false;
  private queuesSubscribed = false;

  readonly hubEvents$: Observable<AsteriskHubEvent> = this.events$.asObservable();
  readonly hubConnected$: Observable<boolean> = this.connected$.asObservable();

  async start(): Promise<void> {
    if (this.connection) {
      if (this.connection.state === signalR.HubConnectionState.Connected) {
        await this.ensureDefaultGroups();
      }
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
    this.bind('ChannelsUpdated', (payload: ChannelItem[]) =>
      this.events$.next({ kind: 'channels', payload }),
    );
    this.bind('channelsUpdated', (payload: ChannelItem[]) =>
      this.events$.next({ kind: 'channels', payload }),
    );
    this.bind('QueuesUpdated', (payload: QueueItem[]) => this.events$.next({ kind: 'queues', payload }));
    this.bind('queuesUpdated', (payload: QueueItem[]) => this.events$.next({ kind: 'queues', payload }));
    this.bind('ConnectionUpdated', (payload: ConnectionStatus) =>
      this.events$.next({ kind: 'connection', payload }),
    );
    this.bind('connectionUpdated', (payload: ConnectionStatus) =>
      this.events$.next({ kind: 'connection', payload }),
    );
    this.bind('LiveEvent', (payload: LiveEventItem) =>
      this.events$.next({ kind: 'liveEvent', payload }),
    );
    this.bind('liveEvent', (payload: LiveEventItem) =>
      this.events$.next({ kind: 'liveEvent', payload }),
    );
    this.bind('amiEvent', (payload: { type?: string; channel?: string | null; at?: string }) =>
      this.events$.next({ kind: 'amiHint', payload }),
    );
    this.bind('AmiEvent', (payload: { type?: string; channel?: string | null; at?: string }) =>
      this.events$.next({ kind: 'amiHint', payload }),
    );
    this.bind('connectionStatus', (payload: ConnectionStatus) =>
      this.events$.next({ kind: 'connection', payload }),
    );

    this.connection.onreconnected(async () => {
      this.connected$.next(true);
      await this.rejoinGroupsAfterReconnect();
    });
    this.connection.onreconnecting(() => this.connected$.next(false));
    this.connection.onclose(() => this.connected$.next(false));

    try {
      await this.connection.start();
      this.connected$.next(true);
      // Live call-job status for Admin dashboard
      this.jobsSubscribed = true;
      await this.ensureDefaultGroups();
    } catch {
      this.connected$.next(false);
    }
  }

  async subscribeJobs(): Promise<void> {
    await this.start();
    this.jobsSubscribed = true;
    await this.invokeSafe('SubscribeJobs');
  }

  async subscribeMonitor(): Promise<void> {
    await this.start();
    this.monitorSubscribed = true;
    const token = localStorage.getItem('ntk.queueAcl.token') || null;
    await this.invokeSafe('SubscribeMonitor', token);
  }

  async subscribeQueues(): Promise<void> {
    await this.start();
    this.queuesSubscribed = true;
    const token = localStorage.getItem('ntk.queueAcl.token') || null;
    await this.invokeSafe('SubscribeQueues', token);
    // Also join monitor — server pushes queuesUpdated to both groups
    this.monitorSubscribed = true;
    await this.invokeSafe('SubscribeMonitor', token);
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
      this.jobsSubscribed = false;
      this.monitorSubscribed = false;
      this.queuesSubscribed = false;
      this.connected$.next(false);
    }
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private async ensureDefaultGroups(): Promise<void> {
    const token = localStorage.getItem('ntk.queueAcl.token') || null;
    if (this.jobsSubscribed) {
      await this.invokeSafe('SubscribeJobs');
    }
    if (this.monitorSubscribed) {
      await this.invokeSafe('SubscribeMonitor', token);
    }
    if (this.queuesSubscribed) {
      await this.invokeSafe('SubscribeQueues', token);
    }
    if (this.eventsSubscribed) {
      await this.invokeSafe('SubscribeEvents');
    }
  }

  private async rejoinGroupsAfterReconnect(): Promise<void> {
    await this.ensureDefaultGroups();
  }

  private bind(method: string, handler: (...args: any[]) => void): void {
    this.connection?.on(method, handler);
  }

  private async invokeSafe(method: string, ...args: unknown[]): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }
    try {
      await this.connection.invoke(method, ...args);
    } catch {
      // ignore subscribe races during reconnect
    }
  }
}
