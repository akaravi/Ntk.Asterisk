import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result';
import { ListQuery } from '../models/asterisk.models';

@Injectable({ providedIn: 'root' })
export class ApiClientService {
  private readonly http = inject(HttpClient);
  readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  getOne<T>(path: string, params?: Record<string, string | number | boolean | undefined | null>): Observable<T | null> {
    return this.getRaw<T>(path, params).pipe(map((r) => (r.data?.length ? r.data[0] : null)));
  }

  getList<T>(path: string, query?: Partial<ListQuery>): Observable<ApiResult<T>> {
    const params: Record<string, string | number | boolean | undefined | null> = {};
    if (query) {
      if (query.pageIndex != null) params['pageIndex'] = query.pageIndex;
      if (query.pageSize != null) params['pageSize'] = query.pageSize;
      if (query.sortBy) params['sortBy'] = query.sortBy;
      if (query.sortDir) params['sortDir'] = query.sortDir;
      if (query.quickSearch) params['quickSearch'] = query.quickSearch;
      if (query.filter) {
        for (const [k, v] of Object.entries(query.filter)) {
          if (v) params[`filter.${k}`] = v;
        }
      }
    }
    return this.getRaw<T>(path, params);
  }

  postAction<T>(path: string, body: unknown): Observable<ApiResult<T>> {
    const headers = this.correlationHeaders();
    return this.http
      .post<ApiResult<T>>(`${this.baseUrl}${path}`, body, { headers })
      .pipe(catchError((err) => this.handleError(err)));
  }

  private getRaw<T>(
    path: string,
    params?: Record<string, string | number | boolean | undefined | null>
  ): Observable<ApiResult<T>> {
    let httpParams = new HttpParams();
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value === undefined || value === null || value === '') continue;
        httpParams = httpParams.set(key, String(value));
      }
    }
    const headers = this.correlationHeaders();
    return this.http
      .get<ApiResult<T>>(`${this.baseUrl}${path}`, { headers, params: httpParams })
      .pipe(catchError((err) => this.handleError(err)));
  }

  getBlob(path: string): Observable<Blob> {
    const headers = this.correlationHeaders();
    return this.http
      .get(`${this.baseUrl}${path}`, { headers, responseType: 'blob' })
      .pipe(catchError((err) => this.handleBlobError(err)));
  }

  private correlationHeaders(): HttpHeaders {
    const id =
      typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `admin-${Date.now()}-${Math.random().toString(16).slice(2)}`;
    return new HttpHeaders({ 'X-Correlation-Id': id });
  }

  private handleError(err: unknown): Observable<never> {
    if (err instanceof HttpErrorResponse) {
      const body = err.error as ApiResult<unknown> | null;
      const message =
        body?.errorMessage ||
        err.message ||
        `HTTP ${err.status}`;
      return throwError(() => new Error(message));
    }
    return throwError(() => (err instanceof Error ? err : new Error(String(err))));
  }

  private handleBlobError(err: unknown): Observable<never> {
    if (err instanceof HttpErrorResponse && err.error instanceof Blob) {
      return new Observable((subscriber) => {
        const reader = new FileReader();
        reader.onload = () => {
          try {
            const parsed = JSON.parse(String(reader.result)) as ApiResult<unknown>;
            subscriber.error(new Error(parsed.errorMessage || `HTTP ${err.status}`));
          } catch {
            subscriber.error(new Error(err.message || `HTTP ${err.status}`));
          }
        };
        reader.onerror = () => subscriber.error(new Error(err.message || `HTTP ${err.status}`));
        reader.readAsText(err.error);
      });
    }
    return this.handleError(err);
  }
}
