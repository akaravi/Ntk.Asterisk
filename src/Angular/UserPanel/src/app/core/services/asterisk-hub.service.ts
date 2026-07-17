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
  private readonly jobUpdatedSubject = new Subject<CallJob>();
  private readonly connectionStateSubject = new Subject<'connected' | 'disconnected' | 'reconnecting'>();

  readonly jobUpdated$ = this.jobUpdatedSubject.asObservable();
  readonly connectionState$ = this.connectionStateSubject.asObservable();

  async start(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    const hubUrl = `${environment.apiBaseUrl}${environment.hubPath}`;
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    this.connection.on('JobUpdated', (payload: CallJob) => {
      if (payload?.id) {
        this.jobUpdatedSubject.next(payload);
      }
    });
    this.connection.on('jobUpdated', (payload: CallJob) => {
      if (payload?.id) {
        this.jobUpdatedSubject.next(payload);
      }
    });

    this.connection.onreconnecting(() => this.connectionStateSubject.next('reconnecting'));
    this.connection.onreconnected(() => this.connectionStateSubject.next('connected'));
    this.connection.onclose(() => this.connectionStateSubject.next('disconnected'));

    try {
      await this.connection.start();
      this.connectionStateSubject.next('connected');
    } catch {
      this.connectionStateSubject.next('disconnected');
    }
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
    }
  }

  ngOnDestroy(): void {
    void this.stop();
    this.jobUpdatedSubject.complete();
    this.connectionStateSubject.complete();
  }
}
