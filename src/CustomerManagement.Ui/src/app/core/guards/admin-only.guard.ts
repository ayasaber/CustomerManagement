import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const adminOnlyGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  if (!authService.isAuthenticated()) {
    return router.createUrlTree(['/auth/login'], {
      queryParams: { returnUrl: state.url }
    });
  }

  const isAdmin = authService.hasRole('admin');
  const hasConsoleAccess = authService.hasPermission('admin.console.access') || authService.hasPermission('admin.users.manage');

  if (isAdmin || hasConsoleAccess) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
