import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="admin-layout">
      <main class="admin-shell">
        <div class="admin-grid">
          <aside class="admin-sidebar">
            <a routerLink="/admin/users" routerLinkActive="active">Users</a>
            <a routerLink="/admin/roles-permissions" routerLinkActive="active">Roles & Permissions</a>
            <a routerLink="/admin/audit-logs" routerLinkActive="active">Audit Logs</a>
            <a routerLink="/admin/system-settings" routerLinkActive="active">System Settings</a>
            <a routerLink="/admin/ticket-taxonomy" routerLinkActive="active">Ticket Taxonomy</a>
          </aside>

          <section class="admin-content">
            <router-outlet></router-outlet>
          </section>
        </div>
      </main>
    </div>
  `,
  styleUrls: ['./admin-console.shared.scss']
})
export class AdminShellComponent {}
