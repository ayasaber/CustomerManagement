import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthUser } from './core/models/auth.models';
import { AuthService } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  readonly currentUser = this.authService.currentUser();
  readonly routePath = signal(this.router.url);

  readonly showShell = computed(() => {
    const path = this.routePath();
    return this.authService.isAuthenticated() && !path.startsWith('/auth/');
  });

  readonly navItems = computed(() => {
    const user = this.currentUser();
    if (!user) {
      return [] as Array<{ label: string; link: string }>;
    }

    const links: Array<{ label: string; link: string }> = [];
    const roles = user.roles.map((role) => role.toLowerCase());
    const canUseTickets =
      roles.includes('agent') ||
      roles.includes('admin') ||
      roles.includes('customer') ||
      this.hasPermission(user, 'tickets.read') ||
      this.hasPermission(user, 'tickets.write');

    if (roles.includes('admin') || this.hasPermission(user, 'admin.console.access') || this.hasPermission(user, 'admin.users.manage')) {
      links.push({ label: 'Admin Console', link: '/admin/users' });
    }

    if (roles.includes('agent') || roles.includes('admin') || this.hasPermission(user, 'customers.read')) {
      links.push({ label: 'Customer Workspace', link: '/customers' });
      links.push({ label: 'Agent Dashboard', link: '/agent/dashboard' });
    }

    if (canUseTickets) {
      links.push({ label: 'Tickets', link: '/tickets' });
    }

    if (roles.includes('customer')) {
      links.push({ label: 'Customer Portal', link: '/customer/portal' });
    }

    links.push({ label: 'Knowledge Base', link: '/knowledge-base' });

    return links;
  });

  readonly userInitial = computed(() => {
    const user = this.currentUser();
    if (!user?.email) {
      return 'U';
    }

    return user.email.charAt(0).toUpperCase();
  });

  constructor() {
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      this.routePath.set(this.router.url);
    });
  }

  logout(): void {
    this.authService.logout().subscribe(() => {
      void this.router.navigateByUrl('/auth/login');
    });
  }

  private hasPermission(user: AuthUser, permission: string): boolean {
    return user.permissions.some((candidate) => candidate.toLowerCase() === permission.toLowerCase());
  }
}
