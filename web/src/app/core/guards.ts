import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isSignedIn() || inject(Router).createUrlTree(['/login']);
};

/** UI-only convenience: the API enforces the same rule with its Staff policy. */
export const staffGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isStaff() || inject(Router).createUrlTree(['/shipments']);
};

export const homeRedirect: CanActivateFn = () => {
  const auth = inject(AuthService);
  return inject(Router).createUrlTree([auth.isStaff() ? '/dashboard' : '/shipments']);
};
