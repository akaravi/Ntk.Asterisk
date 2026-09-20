import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult } from '../models/api-result';
import { FastAgiPacket, FastAgiStatus } from '../models/fastagi.models';
import { ApiClientService } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class FastAgiApiService {
  private readonly api = inject(ApiClientService);
  private readonly base = '/api/v1/FastAgi';
  getStatus(): Observable<FastAgiStatus | null> {
    return this.api.getOne<FastAgiStatus>(`${this.base}/GetStatus`, {});
  }
  getRecentPackets(limit = 50): Observable<ApiResult<FastAgiPacket>> {
    return this.api.getListWithParams<FastAgiPacket>(`${this.base}/GetRecentPackets`, { limit });
  }
}
