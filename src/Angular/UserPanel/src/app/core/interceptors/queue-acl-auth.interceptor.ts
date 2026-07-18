import { HttpInterceptorFn } from '@angular/common/http';

const TOKEN_KEY = 'ntk.queueAcl.token';

/** Attach Queue ACL session token + correlation id on every API call. */
export const queueAclAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const correlationId =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `user-${Date.now()}-${Math.random().toString(16).slice(2)}`;

  let headers = req.headers.set('X-Correlation-Id', correlationId);
  try {
    const token = localStorage.getItem(TOKEN_KEY);
    if (token) {
      headers = headers.set('X-Queue-Acl-Token', token);
    }
  } catch {
    /* ignore storage access */
  }

  return next(req.clone({ headers }));
};
