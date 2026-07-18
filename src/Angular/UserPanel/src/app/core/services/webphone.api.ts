import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result';
import {
  WebPhoneBuddy,
  WebPhoneBuddyUpsert,
  WebPhoneCdr,
  WebPhoneRecording,
} from '../models/webphone';

@Injectable({ providedIn: 'root' })
export class WebPhoneApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl.replace(/\/$/, '')}/api/v1/WebPhone`;

  private headers(): HttpHeaders {
    const id =
      typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `user-wp-${Date.now()}`;
    let h = new HttpHeaders({ 'X-Correlation-Id': id });
    try {
      const token = localStorage.getItem('ntk.queueAcl.token');
      if (token) h = h.set('X-Queue-Acl-Token', token);
    } catch {
      /* ignore */
    }
    return h;
  }

  getBuddies(): Observable<WebPhoneBuddy[]> {
    return this.http
      .get<ApiResult<WebPhoneBuddy>>(`${this.base}/Buddies/GetList`, {
        headers: this.headers(),
        params: new HttpParams().set('pageIndex', '0').set('pageSize', '200'),
      })
      .pipe(map((r) => this.requireList(r)));
  }

  addBuddy(body: WebPhoneBuddyUpsert): Observable<WebPhoneBuddy> {
    return this.http
      .post<ApiResult<WebPhoneBuddy>>(`${this.base}/Buddies/Add`, body, { headers: this.headers() })
      .pipe(map((r) => this.requireOne(r)));
  }

  getCdr(): Observable<WebPhoneCdr[]> {
    return this.http
      .get<ApiResult<WebPhoneCdr>>(`${this.base}/Cdr/GetList`, {
        headers: this.headers(),
        params: new HttpParams()
          .set('pageIndex', '0')
          .set('pageSize', '100')
          .set('sortBy', 'startedAtUtc')
          .set('sortDir', 'desc'),
      })
      .pipe(map((r) => this.requireList(r)));
  }

  getRecordings(): Observable<WebPhoneRecording[]> {
    return this.http
      .get<ApiResult<WebPhoneRecording>>(`${this.base}/Recordings/GetList`, { headers: this.headers() })
      .pipe(map((r) => this.requireList(r)));
  }

  downloadRecording(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/Recordings/GetOne/${encodeURIComponent(id)}`, {
      headers: this.headers(),
      responseType: 'blob',
    });
  }

  private requireList<T>(r: ApiResult<T>): T[] {
    if (!r.isSuccess) throw new Error(r.errorMessage || 'API failed');
    return r.data ?? [];
  }

  private requireOne<T>(r: ApiResult<T>): T {
    const list = this.requireList(r);
    if (!list.length) throw new Error(r.errorMessage || 'Empty data');
    return list[0];
  }
}
