import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result';
import { CallJob, CallJobAddRequest } from '../models/call-job';

@Injectable({ providedIn: 'root' })
export class CallJobsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/CallJobs`;

  add(request: CallJobAddRequest): Observable<CallJob> {
    return this.http
      .post<ApiResult<CallJob>>(`${this.baseUrl}/Add`, request)
      .pipe(map((r) => this.requireOne(r, 'Add')));
  }

  getList(options?: {
    pageIndex?: number;
    pageSize?: number;
    sortBy?: string;
    sortDir?: 'asc' | 'desc';
    quickSearch?: string;
    filter?: Record<string, string | undefined>;
  }): Observable<{ items: CallJob[]; totalCount?: number }> {
    let params = new HttpParams();
    if (options?.pageIndex != null) {
      params = params.set('pageIndex', String(options.pageIndex));
    }
    if (options?.pageSize != null) {
      params = params.set('pageSize', String(options.pageSize));
    }
    if (options?.sortBy) {
      params = params.set('sortBy', options.sortBy);
    }
    if (options?.sortDir) {
      params = params.set('sortDir', options.sortDir);
    }
    if (options?.quickSearch) {
      params = params.set('quickSearch', options.quickSearch);
    }
    if (options?.filter) {
      for (const [key, value] of Object.entries(options.filter)) {
        if (value) {
          params = params.set(`filter.${key}`, value);
        }
      }
    }

    return this.http
      .get<ApiResult<CallJob> & { totalCount?: number; pageIndex?: number; pageSize?: number }>(
        `${this.baseUrl}/GetList`,
        { params }
      )
      .pipe(
        map((r) => {
          this.ensureSuccess(r, 'GetList');
          return {
            items: (r.data ?? []).map((j) => this.normalize(j)),
            totalCount: r.totalCount,
          };
        })
      );
  }

  getOne(id: string): Observable<CallJob> {
    return this.http
      .get<ApiResult<CallJob>>(`${this.baseUrl}/GetOne/${encodeURIComponent(id)}`)
      .pipe(map((r) => this.requireOne(r, 'GetOne')));
  }

  actionCancel(id: string): Observable<CallJob> {
    return this.http
      .post<ApiResult<CallJob>>(`${this.baseUrl}/ActionCancel/${encodeURIComponent(id)}`, {})
      .pipe(map((r) => this.requireOne(r, 'ActionCancel')));
  }

  actionRedial(id: string): Observable<CallJob> {
    return this.http
      .post<ApiResult<CallJob>>(`${this.baseUrl}/ActionRedial/${encodeURIComponent(id)}`, {})
      .pipe(map((r) => this.requireOne(r, 'ActionRedial')));
  }

  downloadRecording(id: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/ActionDownloadRecording/${encodeURIComponent(id)}`, {
      responseType: 'blob',
    });
  }

  private requireOne(result: ApiResult<CallJob>, action: string): CallJob {
    this.ensureSuccess(result, action);
    const item = result.data?.[0];
    if (!item) {
      throw new Error(`${action}: empty data`);
    }
    return this.normalize(item);
  }

  private normalize(job: CallJob): CallJob {
    const status = job.status ?? job.state;
    return status ? { ...job, status, state: job.state ?? status } : job;
  }

  private ensureSuccess(result: ApiResult<unknown>, action: string): void {
    if (!result?.isSuccess) {
      throw new Error(result?.errorMessage || `${action} failed`);
    }
  }
}
