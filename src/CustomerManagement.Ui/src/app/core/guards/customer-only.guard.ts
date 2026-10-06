import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const customerOnlyGuard: CanActivateFn = () => {
  const router = inject(Router);
  const authService = inject(AuthService);
  const user = authService.currentUser()();

  if (!user) {
    return router.createUrlTree(['/auth/login']);
  }

  const hasCustomerRole = user.roles.some((role) => role.toLowerCase() === 'customer');

  if (hasCustomerRole) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
