import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import {
  QueueAclSession,
  QueueAclStatus,
  QueueAclUser,
  QueueAclUserUpsertRequest,
  QueueStatsSample,
} from '../models/queue-acl.models';
import { ApiResult } from '../models/api-result';
import { ListQuery } from '../models/asterisk.models';
import { ApiClientService } from './api-client.service';

const TOKEN_KEY = 'ntk.queueAcl.token';

@Injectable({ providedIn: 'root' })
export class QueueAclAuthService {
  private readonly api = inject(ApiClientService);
  readonly status = signal<QueueAclStatus | null>(null);
  readonly session = signal<QueueAclSession | null>(null);

  constructor() {
    const token = localStorage.getItem(TOKEN_KEY);
    if (token) {
      this.session.set({
        username: '',
        role: '',
        token,
        expiresAtUtc: '',
        allowedQueues: [],
        isAdmin: false,
      });
    }
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  clearSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.session.set(null);
  }

  refreshStatus(): Observable<QueueAclStatus | null> {
    return this.api.getOne<QueueAclStatus>('/api/v1/Auth/status').pipe(
      tap((s) => {
        this.status.set(s);
        if (s && !s.isAuthenticated && this.getToken()) {
          // token expired / invalid
          this.clearSession();
        }
      }),
    );
  }

  login(username: string, password: string): Observable<ApiResult<QueueAclSession>> {
    return this.api.postAction<QueueAclSession>('/api/v1/Auth/login', { username, password }).pipe(
      tap((r) => {
        if (r.isSuccess && r.data?.[0]?.token) {
          const s = r.data[0];
          localStorage.setItem(TOKEN_KEY, s.token);
          this.session.set(s);
        }
      }),
    );
  }

  logout(): Observable<ApiResult<unknown>> {
    return this.api.postAction<unknown>('/api/v1/Auth/logout', {}).pipe(
      tap(() => this.clearSession()),
    );
  }

  getUsers(query?: Partial<ListQuery>): Observable<ApiResult<QueueAclUser>> {
    return this.api.getList<QueueAclUser>('/api/v1/Asterisk/QueueAclUsers/GetList', query);
  }

  addUser(body: QueueAclUserUpsertRequest): Observable<ApiResult<QueueAclUser>> {
    return this.api.postAction<QueueAclUser>('/api/v1/Asterisk/QueueAclUsers/Add', body);
  }

  updateUser(body: QueueAclUserUpsertRequest): Observable<ApiResult<QueueAclUser>> {
    return this.api.postAction<QueueAclUser>('/api/v1/Asterisk/QueueAclUsers/Update', body);
  }

  deleteUser(id: string): Observable<ApiResult<unknown>> {
    return this.api.postAction('/api/v1/Asterisk/QueueAclUsers/ActionDelete', { id });
  }

  setRequireLogin(requireLogin: boolean): Observable<ApiResult<QueueAclStatus>> {
    return this.api
      .postAction<QueueAclStatus>('/api/v1/Asterisk/QueueAclUsers/ActionSetRequireLogin', {
        requireLogin,
      })
      .pipe(tap((r) => {
        if (r.isSuccess && r.data?.[0]) this.status.set(r.data[0]);
      }));
  }

  getStats(params: {
    from?: string;
    to?: string;
    queue?: string;
  }): Observable<ApiResult<QueueStatsSample>> {
    return this.api.getListWithParams<QueueStatsSample>('/api/v1/Asterisk/QueueStats/GetList', {
      from: params.from,
      to: params.to,
      queue: params.queue,
    });
  }
}
