import { Routes } from '@angular/router';
import { adminOnlyGuard } from './core/guards/admin-only.guard';
import { authPageGuard } from './core/guards/auth-page.guard';
import { authenticatedGuard } from './core/guards/authenticated.guard';
import { agentOnlyGuard } from './core/guards/agent-only.guard';
import { customerOnlyGuard } from './core/guards/customer-only.guard';
import { AdminShellComponent } from './features/admin/admin-shell.component';
import { AdminAuditLogPageComponent } from './features/admin/audit/admin-audit-log-page.component';
import { AdminFaqPageComponent } from './features/admin/faq/admin-faq-page.component';
import { AdminRolesPermissionsPageComponent } from './features/admin/roles-permissions/admin-roles-permissions-page.component';
import { AdminSystemSettingsPageComponent } from './features/admin/settings/admin-system-settings-page.component';
import { AdminTicketTaxonomyPageComponent } from './features/admin/ticket-taxonomy/admin-ticket-taxonomy-page.component';
import { AdminUsersPageComponent } from './features/admin/users/admin-users-page.component';
import { LoginPageComponent } from './features/auth/login/login-page.component';
import { RegisterPageComponent } from './features/auth/register/register-page.component';
import { AgentDashboardPageComponent } from './features/agent-dashboard/agent-dashboard-page.component';
import { CustomerManagementPageComponent } from './features/customer-management/customer-management-page.component';
import { ForbiddenPageComponent } from './features/customer-management/forbidden-page.component';
import { CustomerPortalPageComponent } from './features/landing/customer-portal-page.component';
import { TicketManagementPageComponent } from './features/tickets/ticket-management-page.component';

export const routes: Routes = [
	{
		path: 'auth/login',
		canActivate: [authPageGuard],
		component: LoginPageComponent
	},
	{
		path: 'auth/register',
		canActivate: [authPageGuard],
		component: RegisterPageComponent
	},
	{
		path: 'admin',
		canActivate: [adminOnlyGuard],
		component: AdminShellComponent,
		children: [
			{
				path: '',
				pathMatch: 'full',
				redirectTo: 'users'
			},
			{
				path: 'users',
				component: AdminUsersPageComponent
			},
			{
				path: 'roles-permissions',
				component: AdminRolesPermissionsPageComponent
			},
			{
				path: 'audit-logs',
				component: AdminAuditLogPageComponent
			},
			{
				path: 'system-settings',
				component: AdminSystemSettingsPageComponent
			},
			{
				path: 'ticket-taxonomy',
				component: AdminTicketTaxonomyPageComponent
			},
			{
				path: 'faq',
				component: AdminFaqPageComponent
			}
		]
	},
	{
		path: 'agent/dashboard',
		canActivate: [agentOnlyGuard],
		component: AgentDashboardPageComponent
	},
	{
		path: 'customer/portal',
		canActivate: [customerOnlyGuard],
		component: CustomerPortalPageComponent
	},
	{
		path: 'customers',
		canActivate: [agentOnlyGuard],
		component: CustomerManagementPageComponent
	},
	{
		path: 'tickets',
		canActivate: [authenticatedGuard],
		component: TicketManagementPageComponent
	},
	{
		path: 'forbidden',
		component: ForbiddenPageComponent
	},
	{
		path: '',
		pathMatch: 'full',
		redirectTo: '/auth/login'
	},
	{
		path: '**',
		redirectTo: 'auth/login'
	}
];
