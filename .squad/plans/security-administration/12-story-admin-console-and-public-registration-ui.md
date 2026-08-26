# Story 12 - Admin Console And Public Registration UI

## Prerequisites

- Story 06 completed: `06-story-identity-auth-foundation.md`.
- Story 07 completed: `07-story-user-lifecycle-and-role-assignment.md`.
- Story 08 completed: `08-story-permission-model-and-policy-administration.md`.
- Story 09 completed: `09-story-audit-log-capture-and-query.md`.
- Story 10 completed: `10-story-system-settings-api-and-policy-enforcement.md`.
- Story 11 completed: `11-story-gateway-auth-forwarding-and-policy-consumption.md`.

---

## Story Goal

Deliver a polished Angular admin console for users/roles/permissions/audit/settings and a public role-select registration flow that redirects users to role-appropriate landing routes after auth.

User-visible outcomes:

1. Admin can manage users, roles, permissions, audit logs, and settings from one console.
2. Public registration starts with Agent vs Customer role cards and submits role-specific registration.
3. Login/registration redirects to Agent Dashboard or Customer Portal route.

Out of scope:

- Implementation of Agent Dashboard and Customer Portal feature internals.

---

## Context - Read These Files First

1. `src/CustomerManagement.Ui/src/app/app.routes.ts` - read ~lines 1-23. Current route setup and guard usage pattern.
2. `src/CustomerManagement.Ui/src/app/core/guards/agent-only.guard.ts` - read ~lines 1-13. Existing local role gate pattern to replace with auth-state-aware guards.
3. `src/CustomerManagement.Ui/src/app/core/services/customer-management-api.service.ts` - read ~lines 18-112. Existing API service structure and error mapping.
4. `src/CustomerManagement.Ui/src/app/features/customer-management/customer-management-page.component.ts` - read ~lines 36-209. Current container orchestration style and section decomposition.
5. `src/CustomerManagement.Ui/src/app/features/customer-management/customer-management-page.component.html` - read full file (~lines 1-47). Existing layout density and responsive section grid patterns.
6. `src/CustomerManagement.Gateway/ROUTES.md` - read full file for current gateway contract style and route documentation expectations.
7. Precedent story: `../customer-management/05-story-agent-customer-management-ui.md` for Angular story slicing and test expectations.

---

## Frontend Tasks

### 1. Add auth state and navigation foundation

Create file: `src/CustomerManagement.Ui/src/app/core/models/auth.models.ts`

- Define login/register/token contracts matching Story 06.

Create file: `src/CustomerManagement.Ui/src/app/core/services/auth.service.ts`

- Methods:
  - `register(request)`
  - `login(request)`
  - `refresh()`
  - `logout()`
  - `currentUser()` signal/observable
- Persist tokens in browser storage.

Create file: `src/CustomerManagement.Ui/src/app/core/guards/authenticated.guard.ts`

- Redirect unauthenticated users to `/auth/login`.

Create file: `src/CustomerManagement.Ui/src/app/core/guards/admin-only.guard.ts`

- Require `admin` role or `admin.console.access` permission.

### 2. Add route structure and role-target redirects

File: `src/CustomerManagement.Ui/src/app/app.routes.ts`

- Add routes:
  - `/auth/register`
  - `/auth/login`
  - `/admin`
  - `/admin/users`
  - `/admin/roles-permissions`
  - `/admin/audit-logs`
  - `/admin/system-settings`
  - `/agent/dashboard` (placeholder landing)
  - `/customer/portal` (placeholder landing)
- Redirect rules:
  - post-login/register: role-based route selection.

### 3. Build public registration experience (Upwork-style role selection first)

Create file: `src/CustomerManagement.Ui/src/app/features/auth/register/register-page.component.ts`

- Layout:
  - Step 1: two large cards (`I'm an Agent`, `I'm a Customer`).
  - Step 2: registration form with selected role displayed and locked.
- Form fields:
  - `displayName`, `email`, `password`, `confirmPassword`.
- Submission uses `/api/auth/register` and stores returned tokens.

### 4. Build login page

Create file: `src/CustomerManagement.Ui/src/app/features/auth/login/login-page.component.ts`

- Form fields: `email`, `password`.
- On success, store token set and navigate by role:
  - Agent -> `/agent/dashboard`
  - Customer -> `/customer/portal`
  - Admin -> `/admin/users`

### 5. Build admin console shell and sections

Create file: `src/CustomerManagement.Ui/src/app/features/admin/admin-shell.component.ts`

- Layout: left sidebar navigation + right content panel.
- Sidebar items:
  - Users
  - Roles & Permissions
  - Audit Logs
  - System Settings

Create file: `src/CustomerManagement.Ui/src/app/features/admin/users/admin-users-page.component.ts`

- Table columns:
  - Email, Display Name, Roles, Active, Created (UTC), Updated (UTC), Actions.
- Actions:
  - Create user modal
  - Edit user side panel
  - Deactivate confirmation dialog
  - Assign roles modal

Create file: `src/CustomerManagement.Ui/src/app/features/admin/roles-permissions/admin-roles-permissions-page.component.ts`

- Role list panel + permission checklist panel.
- Save action updates role-permission mapping.

Create file: `src/CustomerManagement.Ui/src/app/features/admin/audit/admin-audit-log-page.component.ts`

- Filter controls:
  - user, date range, action type.
- Paginated table columns:
  - Time (UTC), Actor, Action Type, Entity, Entity Id, Result.

Create file: `src/CustomerManagement.Ui/src/app/features/admin/settings/admin-system-settings-page.component.ts`

- Settings form groups:
  - Password policy values
  - Access token expiry minutes
  - Refresh token expiry days
- Conflict handling using rowversion on update.

### 6. Add admin API service layer

Create file: `src/CustomerManagement.Ui/src/app/core/services/admin-api.service.ts`

- Methods:
  - users list/get/create/update/deactivate/assign roles
  - permissions list/create/update
  - role-permission assignment get/update
  - audit logs query
  - system settings list/update
- All methods send bearer token and central error mapping.

### 7. Modern UI consistency pass

File group: admin/auth component templates and styles

- Enforce consistent spacing scale, responsive behavior at laptop widths, readable typography hierarchy, and primary action emphasis.
- Avoid default scaffold look by defining shared panel/table/form utility classes in feature-level SCSS.

---

## Edge Cases & Failure Modes

- Registration role step bypass attempt: backend request must still reject `admin` account type.
- Token expires during admin form edits: auto-refresh attempt then redirect to login if refresh fails.
- Role-based redirect ambiguity (user has admin + agent): prioritize admin route (`/admin/users`).
- Audit query with invalid range from UI: show inline validation and block submit before API call.
- Concurrent system settings update: show conflict banner and reload latest values.

---

## Test Plan

1. Add auth service tests for login/register/refresh and role redirect mapping.
2. Add route guard tests for authenticated and admin-only routes.
3. Add component tests:
   - registration role-selection flow.
   - admin users table render + create/deactivate interactions.
   - audit filter form behavior.
   - settings conflict feedback.
4. Add smoke integration test flow:
   - register Agent -> redirected to `/agent/dashboard`.
   - register Customer -> redirected to `/customer/portal`.
   - admin login -> manage users and settings.

---

## Verification Steps

1. **Frontend runs:** from `src/CustomerManagement.Ui` run `npm start` and verify routes manually.
2. **Frontend tests:** from `src/CustomerManagement.Ui` run `npm test -- --watch=false --browsers=ChromeHeadless`.
3. **Frontend build:** from `src/CustomerManagement.Ui` run `npm run build`.
4. **Regression:** run gateway and API test projects:
   - `dotnet test tests/CustomerManagement.Gateway.Tests/CustomerManagement.Gateway.Tests.csproj /p:UseAppHost=false`
   - `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false`

---

## Done Criteria

- [x] Public registration uses explicit Agent/Customer card selection before form entry.
- [x] Login and registration redirect users to role-appropriate landing routes.
- [x] Admin console includes users, roles-permissions, audit logs, and settings sections with documented table/form structure.
- [x] UI uses authenticated/admin route guards and bearer-token service layer.
- [x] Component/service tests and smoke flow cover key interactions.
