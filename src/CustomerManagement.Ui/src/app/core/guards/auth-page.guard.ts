import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authPageGuard: CanActivateFn = () => {
  const router = inject(Router);
  const authService = inject(AuthService);

  if (!authService.isAuthenticated()) {
    return true;
  }

  const user = authService.currentUser()();
  if (!user) {
    return true;
  }

  return router.createUrlTree([authService.resolveLandingRoute(user)]);
};
