import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { QueueAclAuthService } from '../services/queue-acl-auth.service';

/** When Queue ACL gate is active, require a valid session (redirect to /login). */
export const authGateGuard: CanActivateFn = () => {
  const auth = inject(QueueAclAuthService);
  const router = inject(Router);
  return auth.refreshStatus().pipe(
    map((s) => {
      if (!s?.gateActive) return true;
      if (s.isAuthenticated) return true;
      return router.createUrlTree(['/login']);
    }),
    catchError(() => of(true)),
  );
};
