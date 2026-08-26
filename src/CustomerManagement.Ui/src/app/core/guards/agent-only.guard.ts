import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const agentOnlyGuard: CanActivateFn = () => {
  const router = inject(Router);
  const authService = inject(AuthService);
  const user = authService.currentUser()();

  if (!user) {
    return router.createUrlTree(['/auth/login']);
  }

  const hasAgentRole = user.roles.some((role) => role.toLowerCase() === 'agent');
  const hasAdminRole = user.roles.some((role) => role.toLowerCase() === 'admin');
  const canReadCustomers = user.permissions.some((permission) => permission.toLowerCase() === 'customers.read');

  if (hasAgentRole || hasAdminRole || canReadCustomers) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
