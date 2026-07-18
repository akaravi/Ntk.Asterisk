import { Injectable, OnDestroy } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CallJob } from '../models/call-job';

@Injectable({ providedIn: 'root' })
export class AsteriskHubService implements OnDestroy {
  private connection: HubConnection | null = null;
  private jobsSubscribed = false;
  private readonly jobUpdatedSubject = new Subject<CallJob>();
  private readonly connectionStateSubject = new Subject<
    'connected' | 'disconnected' | 'reconnecting'
  >();

  readonly jobUpdated$ = this.jobUpdatedSubject.asObservable();
  readonly connectionState$ = this.connectionStateSubject.asObservable();

  async start(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.ensureJobsSubscription();
      return;
    }

    if (this.connection && this.connection.state === HubConnectionState.Connecting) {
      return;
    }

    const hubUrl = `${environment.apiBaseUrl.replace(/\/$/, '')}${environment.hubPath}`;
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => {
          try {
            return localStorage.getItem('ntk.queueAcl.token') ?? '';
          } catch {
            return '';
          }
        },
      })
      .withAutomaticReconnect()
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    this.connection.on('JobUpdated', (payload: CallJob) => this.emitJob(payload));
    this.connection.on('jobUpdated', (payload: CallJob) => this.emitJob(payload));

    this.connection.onreconnecting(() => this.connectionStateSubject.next('reconnecting'));
    this.connection.onreconnected(async () => {
      this.connectionStateSubject.next('connected');
      await this.ensureJobsSubscription();
    });
    this.connection.onclose(() => this.connectionStateSubject.next('disconnected'));

    try {
      await this.connection.start();
      this.connectionStateSubject.next('connected');
      this.jobsSubscribed = true;
      await this.ensureJobsSubscription();
    } catch {
      this.connectionStateSubject.next('disconnected');
    }
  }

  /** Join SignalR group that receives live CallJob state changes. */
  async subscribeJobs(): Promise<void> {
    await this.start();
    this.jobsSubscribed = true;
    await this.ensureJobsSubscription();
  }

  async stop(): Promise<void> {
    if (!this.connection) {
      return;
    }
    try {
      await this.connection.stop();
    } finally {
      this.connectionStateSubject.next('disconnected');
      this.connection = null;
      this.jobsSubscribed = false;
    }
  }

  ngOnDestroy(): void {
    void this.stop();
    this.jobUpdatedSubject.complete();
    this.connectionStateSubject.complete();
  }

  private emitJob(payload: CallJob): void {
    if (!payload?.id) {
      return;
    }
    const status = payload.status ?? payload.state;
    this.jobUpdatedSubject.next({
      ...payload,
      status,
      state: payload.state ?? status,
    });
  }

  private async ensureJobsSubscription(): Promise<void> {
    if (!this.jobsSubscribed) {
      return;
    }
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return;
    }
    try {
      await this.connection.invoke('SubscribeJobs');
    } catch {
      // ignore subscribe races during reconnect
    }
  }
}
