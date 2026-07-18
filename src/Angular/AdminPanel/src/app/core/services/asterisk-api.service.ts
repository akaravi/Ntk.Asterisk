import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  AsteriskServer,
  AsteriskServerAddRequest,
  AsteriskServerUpdateRequest,
  CallFileAddRequest,
  CallFileResult,
  CallJob,
  ChannelItem,
  ConnectionStatus,
  HangupRequest,
  ChanSpyRequest,
  ChanSpyMode,
  BridgeRequest,
  ListQuery,
  LiveEventItem,
  PeerItem,
  SiteSettings,
  SiteSettingsUpdateRequest,
  TrunkItem,
} from '../models/asterisk.models';
import { QueueItem, QueueMemberPauseRequest } from '../models/queue.models';
import { ApiResult } from '../models/api-result';
import { ApiClientService } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class AsteriskApiService {
  private readonly api = inject(ApiClientService);

  getConnectionStatus(): Observable<ConnectionStatus | null> {
    return this.api.getOne<ConnectionStatus>('/api/v1/Asterisk/Connection/GetStatus');
  }

  getConnectionStatusList(query?: Partial<ListQuery>): Observable<ApiResult<ConnectionStatus>> {
    return this.api.getList<ConnectionStatus>('/api/v1/Asterisk/Connection/GetList', query);
  }

  getSiteSettings(): Observable<SiteSettings | null> {
    return this.api.getOne<SiteSettings>('/api/v1/Config/GetSiteSettings');
  }

  updateSiteSettings(body: SiteSettingsUpdateRequest): Observable<ApiResult<SiteSettings>> {
    return this.api.postAction<SiteSettings>('/api/v1/Config/UpdateSiteSettings', body);
  }

  testConnection(): Observable<ApiResult<ConnectionStatus>> {
    return this.api.postAction<ConnectionStatus>('/api/v1/Config/ActionTestConnection', {});
  }

  getServers(query?: Partial<ListQuery>): Observable<ApiResult<AsteriskServer>> {
    return this.api.getList<AsteriskServer>('/api/v1/AsteriskServers/GetList', query);
  }

  getServer(id: string): Observable<AsteriskServer | null> {
    return this.api.getOne<AsteriskServer>(`/api/v1/AsteriskServers/GetOne/${encodeURIComponent(id)}`);
  }

  addServer(body: AsteriskServerAddRequest): Observable<ApiResult<AsteriskServer>> {
    return this.api.postAction<AsteriskServer>('/api/v1/AsteriskServers/Add', body);
  }

  updateServer(body: AsteriskServerUpdateRequest): Observable<ApiResult<AsteriskServer>> {
    return this.api.postAction<AsteriskServer>('/api/v1/AsteriskServers/Update', body);
  }

  enableServer(id: string, reconnectAfterSave = true): Observable<ApiResult<AsteriskServer>> {
    return this.api.postAction<AsteriskServer>('/api/v1/AsteriskServers/ActionEnable', {
      id,
      reconnectAfterSave,
    });
  }

  disableServer(id: string, reconnectAfterSave = true): Observable<ApiResult<AsteriskServer>> {
    return this.api.postAction<AsteriskServer>('/api/v1/AsteriskServers/ActionDisable', {
      id,
      reconnectAfterSave,
    });
  }

  setDefaultServer(id: string, reconnectAfterSave = true): Observable<ApiResult<AsteriskServer>> {
    return this.api.postAction<AsteriskServer>('/api/v1/AsteriskServers/ActionSetDefault', {
      id,
      reconnectAfterSave,
    });
  }

  deleteServer(id: string, reconnectAfterSave = true): Observable<ApiResult<AsteriskServer>> {
    return this.api.postAction<AsteriskServer>('/api/v1/AsteriskServers/ActionDelete', {
      id,
      reconnectAfterSave,
    });
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

  connectAmi(serverId?: string | null): Observable<ApiResult<ConnectionStatus>> {
    return this.api.postAction<ConnectionStatus>('/api/v1/Asterisk/Connection/ActionConnect', {
      serverId: serverId || null,
    });
  }

  connectAmiAll(): Observable<ApiResult<ConnectionStatus>> {
    return this.api.postAction<ConnectionStatus>('/api/v1/Asterisk/Connection/ActionConnectAll', {});
  }

  disconnectAmi(serverId?: string | null): Observable<ApiResult<ConnectionStatus>> {
    return this.api.postAction<ConnectionStatus>('/api/v1/Asterisk/Connection/ActionDisconnect', {
      serverId: serverId || null,
    });
  }

  chanSpy(body: ChanSpyRequest): Observable<ApiResult<CallJob>> {
    return this.api.postAction<CallJob>('/api/v1/Asterisk/Channels/ActionChanSpy', body);
  }

  bridgeChannels(channel1: string, channel2: string, tone = 'no'): Observable<ApiResult<CallJob>> {
    const body: BridgeRequest = { channel1, channel2, tone };
    return this.api.postAction<CallJob>('/api/v1/Asterisk/Channels/ActionBridge', body);
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

  redialJob(id: string): Observable<ApiResult<CallJob>> {
    return this.api.postAction<CallJob>(`/api/v1/CallJobs/ActionRedial/${encodeURIComponent(id)}`, {});
  }

  downloadJobRecording(id: string): Observable<Blob> {
    return this.api.getBlob(`/api/v1/CallJobs/ActionDownloadRecording/${encodeURIComponent(id)}`);
  }

  addCallFile(body: CallFileAddRequest): Observable<ApiResult<CallFileResult>> {
    return this.api.postAction<CallFileResult>('/api/v1/Asterisk/CallFiles/Add', body);
  }

  healthOk(): Observable<boolean> {
    return this.api.getList<unknown>('/api/v1/Health').pipe(
      map((r) => r.isSuccess),
    );
  }

  getEvents(query?: Partial<ListQuery>): Observable<ApiResult<LiveEventItem>> {
    return this.api.getList<LiveEventItem>('/api/v1/Events/GetList', query);
  }

  clearEvents(): Observable<ApiResult<unknown>> {
    return this.api.postAction<unknown>('/api/v1/Events/ActionClear', {});
  }

  getQueues(query?: Partial<ListQuery>): Observable<ApiResult<QueueItem>> {
    return this.api.getList<QueueItem>('/api/v1/Asterisk/Queues/GetList', query);
  }

  getQueue(name: string): Observable<QueueItem | null> {
    return this.api.getOne<QueueItem>(
      `/api/v1/Asterisk/Queues/GetOne/${encodeURIComponent(name)}`,
    );
  }

  pauseQueueMember(body: QueueMemberPauseRequest): Observable<ApiResult<unknown>> {
    return this.api.postAction<unknown>('/api/v1/Asterisk/Queues/ActionPauseMember', body);
  }

  unpauseQueueMember(body: QueueMemberPauseRequest): Observable<ApiResult<unknown>> {
    return this.api.postAction<unknown>('/api/v1/Asterisk/Queues/ActionUnpauseMember', body);
  }

  hangupQueueEntry(channel: string): Observable<ApiResult<unknown>> {
    return this.api.postAction<unknown>('/api/v1/Asterisk/Queues/ActionHangupEntry', { channel });
  }
}
