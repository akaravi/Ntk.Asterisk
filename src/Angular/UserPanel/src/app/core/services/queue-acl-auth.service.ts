import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result';
import { QueueAclSession, QueueAclStatus } from '../models/queue-acl.models';

const TOKEN_KEY = 'ntk.queueAcl.token';

@Injectable({ providedIn: 'root' })
export class QueueAclAuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

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
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  }

  clearSession(): void {
    try {
      localStorage.removeItem(TOKEN_KEY);
    } catch {
      /* ignore */
    }
    this.session.set(null);
  }

  refreshStatus(): Observable<QueueAclStatus | null> {
    return this.http
      .get<ApiResult<QueueAclStatus>>(`${this.baseUrl}/api/v1/Auth/status`)
      .pipe(
        map((r) => {
          if (!r?.isSuccess) {
            throw new Error(r?.errorMessage || 'Auth/status failed');
          }
          return r.data?.[0] ?? null;
        }),
        tap((s) => {
          this.status.set(s);
          if (s && !s.isAuthenticated && this.getToken()) {
            this.clearSession();
          }
        }),
      );
  }

  login(username: string, password: string): Observable<ApiResult<QueueAclSession>> {
    return this.http
      .post<ApiResult<QueueAclSession>>(`${this.baseUrl}/api/v1/Auth/login`, {
        username,
        password,
      })
      .pipe(
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
    return this.http
      .post<ApiResult<unknown>>(`${this.baseUrl}/api/v1/Auth/logout`, {})
      .pipe(tap(() => this.clearSession()));
  }
}
