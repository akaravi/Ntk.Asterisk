import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult } from '../models/api-result';
import { ListQuery } from '../models/asterisk.models';
import {
  WebPhoneBuddy,
  WebPhoneBuddyUpsert,
  WebPhoneCdr,
  WebPhoneExtension,
  WebPhoneExtensionUpsert,
  WebPhoneOptions,
  WebPhoneQos,
  WebPhoneRecording,
} from '../models/webphone.models';
import { ApiClientService } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class WebPhoneApiService {
  private readonly api = inject(ApiClientService);
  private readonly base = '/api/v1/WebPhone';

  getOptions(): Observable<WebPhoneOptions | null> {
    return this.api.getOne<WebPhoneOptions>(`${this.base}/Config/GetWebPhoneOptions`);
  }

  getExtensions(): Observable<ApiResult<WebPhoneExtension>> {
    return this.api.getList<WebPhoneExtension>(`${this.base}/Extensions/GetList`);
  }

  addExtension(body: WebPhoneExtensionUpsert): Observable<ApiResult<WebPhoneExtension>> {
    return this.api.postAction<WebPhoneExtension>(`${this.base}/Extensions/Add`, body);
  }

  updateExtension(body: WebPhoneExtensionUpsert): Observable<ApiResult<WebPhoneExtension>> {
    return this.api.postAction<WebPhoneExtension>(`${this.base}/Extensions/Update`, body);
  }

  deleteExtension(id: string): Observable<ApiResult<object>> {
    return this.api.postAction<object>(`${this.base}/Extensions/ActionDelete`, { id });
  }

  getBuddies(query?: Partial<ListQuery>): Observable<ApiResult<WebPhoneBuddy>> {
    return this.api.getList<WebPhoneBuddy>(`${this.base}/Buddies/GetList`, query);
  }

  addBuddy(body: WebPhoneBuddyUpsert): Observable<ApiResult<WebPhoneBuddy>> {
    return this.api.postAction<WebPhoneBuddy>(`${this.base}/Buddies/Add`, body);
  }

  updateBuddy(body: WebPhoneBuddyUpsert): Observable<ApiResult<WebPhoneBuddy>> {
    return this.api.postAction<WebPhoneBuddy>(`${this.base}/Buddies/Update`, body);
  }

  deleteBuddy(id: string): Observable<ApiResult<object>> {
    return this.api.postAction<object>(`${this.base}/Buddies/ActionDelete`, { id });
  }

  getCdr(query?: Partial<ListQuery>): Observable<ApiResult<WebPhoneCdr>> {
    return this.api.getList<WebPhoneCdr>(`${this.base}/Cdr/GetList`, query);
  }

  getRecordings(): Observable<ApiResult<WebPhoneRecording>> {
    return this.api.getList<WebPhoneRecording>(`${this.base}/Recordings/GetList`);
  }

  downloadRecording(id: string): Observable<Blob> {
    return this.api.getBlob(`${this.base}/Recordings/GetOne/${encodeURIComponent(id)}`);
  }

  getQos(take = 100): Observable<ApiResult<WebPhoneQos>> {
    return this.api.getListWithParams<WebPhoneQos>(`${this.base}/Qos/GetList`, { take });
  }
}
