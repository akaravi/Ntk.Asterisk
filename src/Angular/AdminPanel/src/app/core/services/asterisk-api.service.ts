import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  CallJob,
  ChannelItem,
  ConnectionStatus,
  HangupRequest,
  ListQuery,
  PeerItem,
  TrunkItem,
} from '../models/asterisk.models';
import { ApiResult } from '../models/api-result';
import { ApiClientService } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class AsteriskApiService {
  private readonly api = inject(ApiClientService);

  getConnectionStatus(): Observable<ConnectionStatus | null> {
    return this.api.getOne<ConnectionStatus>('/api/v1/Asterisk/Connection/GetStatus');
  }

  getPeers(query?: Partial<ListQuery>): Observable<ApiResult<PeerItem>> {
    return this.api.getList<PeerItem>('/api/v1/Asterisk/Peers/GetList', query);
  }

  getChannels(query?: Partial<ListQuery>): Observable<ApiResult<ChannelItem>> {
    return this.api.getList<ChannelItem>('/api/v1/Asterisk/Channels/GetList', query);
  }

  getTrunks(query?: Partial<ListQuery>): Observable<ApiResult<TrunkItem>> {
    return this.api.getList<TrunkItem>('/api/v1/Asterisk/Trunks/GetList', query);
  }

  hangupChannel(channel: string): Observable<ApiResult<CallJob>> {
    const body: HangupRequest = { channel };
    return this.api.postAction<CallJob>('/api/v1/Asterisk/Channels/ActionHangup', body);
  }

  getJobs(query?: Partial<ListQuery>): Observable<ApiResult<CallJob>> {
    return this.api.getList<CallJob>('/api/v1/CallJobs/GetList', query);
  }

  getJob(id: string): Observable<CallJob | null> {
    return this.api.getOne<CallJob>(`/api/v1/CallJobs/GetOne/${encodeURIComponent(id)}`);
  }

  cancelJob(id: string): Observable<ApiResult<CallJob>> {
    return this.api.postAction<CallJob>(`/api/v1/CallJobs/ActionCancel/${encodeURIComponent(id)}`, {});
  }

  healthOk(): Observable<boolean> {
    return this.api.getList<unknown>('/api/v1/Health').pipe(
      map((r) => r.isSuccess),
    );
  }
}
