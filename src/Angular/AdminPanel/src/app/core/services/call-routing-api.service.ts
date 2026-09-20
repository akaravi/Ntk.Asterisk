import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult } from '../models/api-result';
import { CallRoute, CallRouteLookupRequest, CallRouteLookupResult, CallRouteUpsertRequest, SmartRouteDecision } from '../models/call-routing.models';
import { ApiClientService } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class CallRoutingApiService {
  private readonly api = inject(ApiClientService);
  private readonly base = '/api/v1/CallRoutes';

  getList(quickSearch?: string, isActive?: boolean): Observable<ApiResult<CallRoute>> {
    const params: Record<string, string | boolean | undefined> = {};
    if (quickSearch?.trim()) params['quickSearch'] = quickSearch.trim();
    if (isActive !== undefined && isActive !== null) params['isActive'] = isActive;
    return this.api.getListWithParams<CallRoute>(`${this.base}/GetList`, params);
  }

  getById(id: string): Observable<CallRoute | null> {
    return this.api.getOne<CallRoute>(`${this.base}/GetById`, { id });
  }

  save(body: CallRouteUpsertRequest): Observable<ApiResult<CallRoute>> {
    return this.api.postAction<CallRoute>(`${this.base}/Save`, body);
  }

  delete(id: string): Observable<ApiResult<boolean>> {
    return this.api.postAction<boolean>(`${this.base}/Delete?id=${encodeURIComponent(id)}`, {});
  }

  toggleActive(id: string): Observable<ApiResult<CallRoute>> {
    return this.api.postAction<CallRoute>(`${this.base}/ToggleActive?id=${encodeURIComponent(id)}`, {});
  }
  reorder(items: { id: string; priority: number }[]): Observable<ApiResult<boolean>> {
    return this.api.postAction<boolean>(`${this.base}/Reorder`, { items });
  }

  lookup(request: string | CallRouteLookupRequest): Observable<ApiResult<CallRouteLookupResult>> {
    const body = typeof request === 'string' ? { callerNumber: request, source: 'manual' } : request;
    return this.api.postAction<CallRouteLookupResult>(`${this.base}/Lookup`, body);
  }

  getRecentDecisions(limit = 50): Observable<ApiResult<SmartRouteDecision>> {
    return this.api.getListWithParams<SmartRouteDecision>(`${this.base}/GetRecentDecisions`, { limit });
  }
}
